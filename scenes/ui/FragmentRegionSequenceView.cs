using System;
using System.Collections.Generic;
using Godot;

public partial class FragmentRegionSequenceView : Control
{
	private const float ToolbarHeight = 46f;
	private FragmentObservableScan scan;
	private readonly List<FragmentCandidateRegion> regions = new();
	private readonly List<FragmentDetectedFeature> features = new();
	private readonly List<FragmentDetectedStructure> structures = new();
	private readonly List<FragmentLockedRegionView> lockedViews = new();
	private int pageStart;
	private int? selectedFeatureId;
	private int? selectedRegionId;
	private Color roverFeatureColor = new(1f, 0.15f, 0.75f, 0.95f);
	private Color acceptedRoverFeatureColor = new(1f, 0.72f, 0.1f, 0.98f);
	private Color playerFeatureColor = new(0.25f, 1f, 0.45f, 0.95f);
	private Color pendingFeatureColor = new(0.15f, 0.95f, 1f, 1f);
	private bool orientationIsolation;
	private const float OrientationAnimationDuration = 1.2f;
	private float orientationAnimationElapsed = OrientationAnimationDuration;
	private int? orientationSourceRegionId;
	private FragmentDetectedStructure orientationStructure;
	private FragmentOrientationHypothesis orientationHypothesis;
	private FragmentRotationCorrection rotationCorrection;
	private readonly List<FragmentDetectedFeature> orientationFeatures = new();
	private Color orientationStructureColor = new(1f, 0.2f, 0.85f, 1f);
	private bool showFeatures = true;
	private bool showStructures = true;
	private Label toolbarPageLabel;
	private Button toolbarPreviousButton;
	private Button toolbarNextButton;

	public int RegionCount => regions.Count;
	public bool CanGoPrevious => pageStart > 0;
	public bool CanGoNext => pageStart + 2 < regions.Count;
	public string PageText => regions.Count < 2
		? "REGION SEQUENCE: Draw or retain at least two regions"
			: $"REGIONS {pageStart + 1}–{Math.Min(pageStart + 2, regions.Count)} / {regions.Count}";
	public IReadOnlyList<int> DisplayedRegionIds
	{
		get
		{
			List<int> ids = new();
			for (int index = pageStart; index < Math.Min(pageStart + 2, regions.Count); index++)
				ids.Add(regions[index].Id);
			return ids;
		}
	}

	public event Action<int> RegionSelected;
	public event Action<int, FragmentRegionEditAction> RegionActionRequested;
	public event Action<int> RegionLockRequested;
	public event Action ExitRequested;
	public event Action PageChanged;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
		CreateToolbar();
	}

	private void CreateToolbar()
	{
		PanelContainer panel = new()
		{
			Name = "ComparisonToolbar",
			OffsetRight = 0f,
			OffsetBottom = ToolbarHeight
		};
		panel.SetAnchorsPreset(LayoutPreset.TopWide);
		AddChild(panel);
		HBoxContainer row = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);
		toolbarPreviousButton = new Button { Text = "←", TooltipText = "Previous region pair." };
		toolbarPageLabel = new Label
		{
			Text = PageText,
			CustomMinimumSize = new Vector2(300f, 0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		toolbarNextButton = new Button { Text = "→", TooltipText = "Next region pair." };
		Button exitButton = new()
		{
			Text = "QUIT SIDE-BY-SIDE",
			TooltipText = "Return to the normal analyzer while retaining locked references."
		};
		row.AddChild(toolbarPreviousButton);
		row.AddChild(toolbarPageLabel);
		row.AddChild(toolbarNextButton);
		row.AddChild(exitButton);
		toolbarPreviousButton.Pressed += PreviousPage;
		toolbarNextButton.Pressed += NextPage;
		exitButton.Pressed += () => ExitRequested?.Invoke();
		RefreshToolbar();
	}

	public void SetContent(
		FragmentObservableScan observableScan,
		IReadOnlyList<FragmentCandidateRegion> sourceRegions,
		IReadOnlyList<FragmentDetectedFeature> sourceFeatures,
		IReadOnlyList<FragmentDetectedStructure> sourceStructures,
		IReadOnlyList<FragmentLockedRegionView> sourceLockedViews,
		int? selectedFeature,
		int? selectedRegion)
	{
		scan = observableScan;
		selectedFeatureId = selectedFeature;
		selectedRegionId = selectedRegion;
		regions.Clear();
		features.Clear();
		structures.Clear();
		lockedViews.Clear();
		if (sourceRegions != null)
		{
			foreach (FragmentCandidateRegion region in sourceRegions)
				if (region.Disposition != FragmentAnnotationDisposition.Dismissed)
					regions.Add(region);
		}
		if (sourceFeatures != null)
		{
			foreach (FragmentDetectedFeature feature in sourceFeatures)
				if (feature.Disposition != FragmentAnnotationDisposition.Dismissed)
					features.Add(feature);
		}
		if (sourceStructures != null)
			foreach (FragmentDetectedStructure structure in sourceStructures)
				if (structure.Disposition != FragmentAnnotationDisposition.Dismissed)
					structures.Add(structure);
		if (sourceLockedViews != null) lockedViews.AddRange(sourceLockedViews);
		regions.Sort((first, second) => first.Id.CompareTo(second.Id));
		pageStart = Mathf.Clamp(pageStart, 0, Math.Max(regions.Count - 1, 0));
		if (pageStart % 2 != 0) pageStart--;
		RefreshToolbar();
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton button ||
			button.ButtonIndex != MouseButton.Left || button.Pressed) return;
		int paneIndex = FindPaneIndex(button.Position);
		int regionIndex = pageStart + paneIndex;
		if (paneIndex < 0 || regionIndex < 0 || regionIndex >= regions.Count) return;
		FragmentCandidateRegion region = regions[regionIndex];
		RegionSelected?.Invoke(region.Id);
		Rect2 pane = GetPaneRect(paneIndex);
		if (region.Disposition == FragmentAnnotationDisposition.Accepted)
		{
			if (GetLockButtonRect(pane).HasPoint(button.Position))
				RegionLockRequested?.Invoke(region.Id);
		}
		else if (region.Disposition == FragmentAnnotationDisposition.Proposed)
		{
			if (GetAcceptButtonRect(pane).HasPoint(button.Position))
				RegionActionRequested?.Invoke(region.Id, FragmentRegionEditAction.Accept);
			else if (GetDismissButtonRect(pane).HasPoint(button.Position))
				RegionActionRequested?.Invoke(region.Id, FragmentRegionEditAction.Dismiss);
		}
		AcceptEvent();
	}

	public void SetFeatureColors(Color rover, Color acceptedRover, Color player, Color pending)
	{
		roverFeatureColor = rover;
		acceptedRoverFeatureColor = acceptedRover;
		playerFeatureColor = player;
		pendingFeatureColor = pending;
		QueueRedraw();
	}

	public void SetShowFeatures(bool visible)
	{
		if (showFeatures == visible) return;
		showFeatures = visible;
		QueueRedraw();
	}

	public void SetShowStructures(bool visible)
	{
		if (showStructures == visible) return;
		showStructures = visible;
		QueueRedraw();
	}

	public void SetOrientationIsolation(
		bool isolated,
		int? sourceRegionId,
		FragmentDetectedStructure structure,
		FragmentOrientationHypothesis hypothesis,
		FragmentRotationCorrection correction,
		IReadOnlyList<FragmentDetectedFeature> sourceFeatures,
		Color color)
	{
		bool changedProposal = orientationHypothesis?.Id != hypothesis?.Id ||
			orientationHypothesis?.AxisDegrees != hypothesis?.AxisDegrees ||
			orientationSourceRegionId != sourceRegionId ||
			orientationIsolation != isolated;
		orientationIsolation = isolated;
		orientationSourceRegionId = sourceRegionId;
		orientationStructure = structure;
		orientationHypothesis = hypothesis;
		rotationCorrection = correction;
		orientationFeatures.Clear();
		if (sourceFeatures != null) orientationFeatures.AddRange(sourceFeatures);
		orientationStructureColor = color;
		if (changedProposal) orientationAnimationElapsed = 0f;
		QueueRedraw();
	}

	public void RestartOrientationPreviewAnimation()
	{
		orientationAnimationElapsed = 0f;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!orientationIsolation || orientationHypothesis == null ||
			orientationAnimationElapsed >= OrientationAnimationDuration) return;
		orientationAnimationElapsed = MathF.Min(
			orientationAnimationElapsed + (float)delta,
			OrientationAnimationDuration);
		QueueRedraw();
	}

	public void PreviousPage()
	{
		pageStart = Math.Max(pageStart - 2, 0);
		RefreshToolbar();
		QueueRedraw();
		PageChanged?.Invoke();
	}

	public void NextPage()
	{
		if (CanGoNext) pageStart += 2;
		RefreshToolbar();
		QueueRedraw();
		PageChanged?.Invoke();
	}

	public void EnsureRegionVisible(int regionId)
	{
		int index = regions.FindIndex(region => region.Id == regionId);
		if (index < 0 || (index >= pageStart && index < pageStart + 2)) return;
		pageStart = index - index % 2;
		RefreshToolbar();
		QueueRedraw();
		PageChanged?.Invoke();
	}

	private void RefreshToolbar()
	{
		if (!IsInstanceValid(toolbarPageLabel)) return;
		toolbarPageLabel.Text = PageText;
		toolbarPreviousButton.Disabled = !CanGoPrevious;
		toolbarNextButton.Disabled = !CanGoNext;
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.015f, 0.02f, 0.035f, 1f), true);
		if (scan?.Primitives == null || regions.Count < 2) return;
		for (int paneIndex = 0; paneIndex < 2; paneIndex++)
		{
			int regionIndex = pageStart + paneIndex;
			if (regionIndex >= regions.Count) break;
			Rect2 pane = GetPaneRect(paneIndex);
			DrawRegionExtract(pane, regions[regionIndex]);
		}
	}

	private void DrawRegionExtract(Rect2 pane, FragmentCandidateRegion region)
	{
		DrawRect(pane, new Color(0.025f, 0.03f, 0.045f, 1f), true);
		FragmentLockedRegionView lockedView = lockedViews.Find(view => view.RegionId == region.Id);
		FragmentObservableScan renderedScan = lockedView?.Scan ?? scan;
		Rect2 renderedBounds = lockedView?.NormalizedBounds ?? region.NormalizedBounds;
		IReadOnlyList<FragmentDetectedFeature> renderedFeatures = lockedView?.Features ?? features;
		Vector2 sourceSize = renderedBounds.Size * renderedScan.SampleSize;
		float fitScale = MathF.Min(
			pane.Size.X / MathF.Max(sourceSize.X, 1f),
			pane.Size.Y / MathF.Max(sourceSize.Y, 1f));
		Vector2 fittedSize = sourceSize * fitScale;
		Rect2 contentPane = new(pane.GetCenter() - fittedSize * 0.5f, fittedSize);
		if (orientationIsolation && orientationStructure != null &&
			orientationSourceRegionId == region.Id)
		{
			DrawOrientationStructure(renderedBounds, contentPane, orientationFeatures);
		}
		else
		{
			foreach (FragmentObservablePrimitive primitive in renderedScan.Primitives)
			{
				Vector2 start = primitive.Start;
				Vector2 end = primitive.End;
				if (!ClipSegment(renderedBounds, ref start, ref end)) continue;
				Vector2 paneStart = RegionToPane(start, renderedBounds, contentPane);
				Vector2 paneEnd = RegionToPane(end, renderedBounds, contentPane);
				Color color = new(
					primitive.Color.R,
					primitive.Color.G,
					primitive.Color.B,
					Mathf.Clamp(primitive.Color.A * MathF.Max(primitive.Intensity, 0.15f), 0.08f, 1f));
				float width = Mathf.Clamp(primitive.Width * fitScale, 1f, 8f);
				DrawLine(paneStart, paneEnd, color, width, true);
			}
			if (showFeatures)
				DrawFeatureAnnotations(renderedBounds, contentPane, renderedFeatures);
			if (showStructures)
				DrawStructures(renderedBounds, contentPane, renderedFeatures);
		}
		Color regionColor = region.Disposition == FragmentAnnotationDisposition.Accepted
			? new Color(0.25f, 1f, 0.45f, 1f)
			: new Color(1f, 0.72f, 0.15f, 1f);
		DrawRect(pane, selectedRegionId == region.Id ? Colors.White : Colors.Black, false, 5f);
		DrawRect(contentPane, regionColor, false, 2f);
		DrawString(ThemeDB.FallbackFont, pane.Position + new Vector2(12f, 24f),
			$"R{region.Id}", HorizontalAlignment.Left, -1, 18,
			regionColor);
		DrawRegionActions(pane, region);
	}

	private void DrawOrientationStructure(
		Rect2 region,
		Rect2 pane,
		IReadOnlyList<FragmentDetectedFeature> renderedFeatures)
	{
		foreach (int featureId in orientationStructure.FeatureIds)
		{
			FragmentDetectedFeature feature = null;
			foreach (FragmentDetectedFeature candidate in renderedFeatures)
				if (candidate.Id == featureId &&
					candidate.Disposition != FragmentAnnotationDisposition.Dismissed)
				{
					feature = candidate;
					break;
				}
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				DrawSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					DrawSegment(segment.Start, segment.End);
		}

		if (orientationHypothesis == null) return;
		if (!TryGetOrientationCenter(region, pane, renderedFeatures, out Vector2 center)) return;
		float progress = Mathf.SmoothStep(
			0f,
			1f,
			Mathf.Clamp(orientationAnimationElapsed / OrientationAnimationDuration, 0f, 1f));
		float previewDegrees = rotationCorrection != null &&
			rotationCorrection.SourceOrientationId == orientationHypothesis.Id &&
			rotationCorrection.Disposition != FragmentAnnotationDisposition.Dismissed
			? rotationCorrection.ProposedDegrees
			: -orientationHypothesis.AxisDegrees;
		float radians = Mathf.DegToRad(previewDegrees * progress);
		Color ghostColor = new(0.15f, 0.95f, 1f, 0.72f);
		foreach (int featureId in orientationStructure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindOrientationFeature(renderedFeatures, featureId);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				DrawGhostSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					DrawGhostSegment(segment.Start, segment.End);
		}
		float axisLength = Mathf.Clamp(MathF.Min(pane.Size.X, pane.Size.Y) * 0.28f, 40f, 170f);
		DrawDashedLine(center - Vector2.Up * axisLength, center + Vector2.Up * axisLength,
			new Color(0.65f, 0.8f, 1f, 0.72f), 2f);
		DrawString(ThemeDB.FallbackFont, pane.Position + new Vector2(12f, 46f),
			$"H{orientationHypothesis.Id} · PROPOSED UPRIGHT",
			HorizontalAlignment.Left, -1, 14, ghostColor);

		void DrawGhostSegment(Vector2 normalizedStart, Vector2 normalizedEnd)
		{
			Vector2 start = normalizedStart;
			Vector2 end = normalizedEnd;
			if (!ClipSegment(region, ref start, ref end)) return;
			Vector2 paneStart = center + (RegionToPane(start, region, pane) - center).Rotated(radians);
			Vector2 paneEnd = center + (RegionToPane(end, region, pane) - center).Rotated(radians);
			DrawDashedLine(paneStart, paneEnd, ghostColor, 3.5f);
		}

		void DrawSegment(Vector2 normalizedStart, Vector2 normalizedEnd)
		{
			Vector2 start = normalizedStart;
			Vector2 end = normalizedEnd;
			if (!ClipSegment(region, ref start, ref end)) return;
			Vector2 paneStart = RegionToPane(start, region, pane);
			Vector2 paneEnd = RegionToPane(end, region, pane);
			DrawLine(paneStart, paneEnd, Colors.Black, 9f, true);
			DrawLine(paneStart, paneEnd, orientationStructureColor, 5f, true);
		}
	}

	private bool TryGetOrientationCenter(
		Rect2 region,
		Rect2 pane,
		IReadOnlyList<FragmentDetectedFeature> renderedFeatures,
		out Vector2 center)
	{
		Vector2 sum = Vector2.Zero;
		int count = 0;
		foreach (int featureId in orientationStructure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindOrientationFeature(renderedFeatures, featureId);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				AddSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					AddSegment(segment.Start, segment.End);
		}
		center = count == 0 ? pane.GetCenter() : sum / count;
		return count > 0;

		void AddSegment(Vector2 normalizedStart, Vector2 normalizedEnd)
		{
			Vector2 start = normalizedStart;
			Vector2 end = normalizedEnd;
			if (!ClipSegment(region, ref start, ref end)) return;
			sum += RegionToPane(start, region, pane) + RegionToPane(end, region, pane);
			count += 2;
		}
	}

	private static FragmentDetectedFeature FindOrientationFeature(
		IReadOnlyList<FragmentDetectedFeature> renderedFeatures,
		int featureId)
	{
		foreach (FragmentDetectedFeature feature in renderedFeatures)
			if (feature.Id == featureId &&
				feature.Disposition != FragmentAnnotationDisposition.Dismissed)
				return feature;
		return null;
	}

	private void DrawRegionActions(Rect2 pane, FragmentCandidateRegion region)
	{
		if (region.Disposition == FragmentAnnotationDisposition.Accepted)
		{
			DrawBadge(new Rect2(pane.End - new Vector2(105f, 34f), new Vector2(95f, 25f)),
				"ACCEPTED", new Color(0.25f, 1f, 0.45f, 1f));
			bool isLocked = lockedViews.Exists(view => view.RegionId == region.Id);
			DrawBadge(GetLockButtonRect(pane), isLocked ? "LOCKED" : "LOCK",
				isLocked ? new Color(0.2f, 0.95f, 1f, 1f) : new Color(0.75f, 0.8f, 0.9f, 1f));
			return;
		}
		DrawBadge(GetAcceptButtonRect(pane), "ACCEPT", new Color(0.25f, 1f, 0.45f, 1f));
		DrawBadge(GetDismissButtonRect(pane), "DISMISS", new Color(1f, 0.3f, 0.35f, 1f));
	}

	private void DrawBadge(Rect2 rectangle, string text, Color color)
	{
		DrawRect(rectangle, new Color(0f, 0f, 0f, 0.9f), true);
		DrawRect(rectangle, color, false, 2f);
		DrawString(ThemeDB.FallbackFont, rectangle.Position + new Vector2(7f, 18f),
			text, HorizontalAlignment.Left, -1, 13, color);
	}

	private Rect2 GetPaneRect(int paneIndex)
	{
		const float gap = 10f;
		Vector2 paneSize = new((Size.X - gap) * 0.5f, MathF.Max(Size.Y - ToolbarHeight, 0f));
		return new Rect2(
			new Vector2(paneIndex * (paneSize.X + gap), ToolbarHeight),
			paneSize);
	}

	private int FindPaneIndex(Vector2 point)
	{
		for (int index = 0; index < 2; index++)
			if (GetPaneRect(index).HasPoint(point)) return index;
		return -1;
	}

	private static Rect2 GetAcceptButtonRect(Rect2 pane) =>
		new(pane.End - new Vector2(176f, 34f), new Vector2(76f, 25f));

	private static Rect2 GetDismissButtonRect(Rect2 pane) =>
		new(pane.End - new Vector2(94f, 34f), new Vector2(84f, 25f));

	private static Rect2 GetLockButtonRect(Rect2 pane) =>
		new(pane.End - new Vector2(193f, 34f), new Vector2(80f, 25f));

	private void DrawFeatureAnnotations(
		Rect2 region,
		Rect2 pane,
		IReadOnlyList<FragmentDetectedFeature> renderedFeatures)
	{
		foreach (FragmentDetectedFeature feature in renderedFeatures)
		{
			if (showStructures && IsAcceptedStructureMember(feature.Id)) continue;
			bool isPending = selectedFeatureId == feature.Id &&
				feature.Disposition == FragmentAnnotationDisposition.Proposed;
			Color color = feature.Provenance == FragmentAnnotationProvenance.Player
				? playerFeatureColor
				: feature.Disposition == FragmentAnnotationDisposition.Accepted
					? acceptedRoverFeatureColor
					: roverFeatureColor;
			if (isPending) color = pendingFeatureColor;
			float width = isPending ? 5f : selectedFeatureId == feature.Id ? 4f : 2.5f;
			bool drewFeature = false;
			Vector2 labelPosition = Vector2.Zero;
			if (feature.Segments.Count == 0)
			{
				DrawFeatureSegment(feature, feature.Start, feature.End, region, pane, color, width,
					ref drewFeature, ref labelPosition);
			}
			else
			{
				foreach (FragmentFeatureSegment segment in feature.Segments)
				{
					DrawFeatureSegment(feature, segment.Start, segment.End, region, pane, color, width,
						ref drewFeature, ref labelPosition);
				}
			}
			if (drewFeature) DrawFeatureLabel(feature.Id, labelPosition, color, isPending);
		}
	}

	private bool IsAcceptedStructureMember(int featureId) => structures.Exists(structure =>
		structure.Disposition == FragmentAnnotationDisposition.Accepted &&
		structure.FeatureIds.Contains(featureId));

	private void DrawStructures(
		Rect2 region,
		Rect2 pane,
		IReadOnlyList<FragmentDetectedFeature> renderedFeatures)
	{
		foreach (FragmentDetectedStructure structure in structures)
		{
			bool accepted = structure.Disposition == FragmentAnnotationDisposition.Accepted;
			foreach (int featureId in structure.FeatureIds)
			{
				FragmentDetectedFeature feature = null;
				foreach (FragmentDetectedFeature candidate in renderedFeatures)
					if (candidate.Id == featureId &&
						candidate.Disposition != FragmentAnnotationDisposition.Dismissed)
					{
						feature = candidate;
						break;
					}
				if (feature == null) continue;
				if (feature.Segments == null || feature.Segments.Count == 0)
					DrawSegment(feature.Start, feature.End);
				else
					foreach (FragmentFeatureSegment segment in feature.Segments)
						DrawSegment(segment.Start, segment.End);
			}

			void DrawSegment(Vector2 normalizedStart, Vector2 normalizedEnd)
			{
				Vector2 start = normalizedStart;
				Vector2 end = normalizedEnd;
				if (!ClipSegment(region, ref start, ref end)) return;
				Vector2 paneStart = RegionToPane(start, region, pane);
				Vector2 paneEnd = RegionToPane(end, region, pane);
				DrawLine(paneStart, paneEnd, Colors.Black, accepted ? 10f : 8f, true);
				if (accepted) DrawLine(paneStart, paneEnd, orientationStructureColor, 6f, true);
				else DrawDashedLine(paneStart, paneEnd, orientationStructureColor, 4f);
			}
		}
	}

	private void DrawFeatureSegment(
		FragmentDetectedFeature feature,
		Vector2 normalizedStart,
		Vector2 normalizedEnd,
		Rect2 region,
		Rect2 pane,
		Color color,
		float width,
		ref bool drewFeature,
		ref Vector2 labelPosition)
	{
		Vector2 start = normalizedStart;
		Vector2 end = normalizedEnd;
		if (!ClipSegment(region, ref start, ref end)) return;
		Vector2 paneStart = RegionToPane(start, region, pane);
		Vector2 paneEnd = RegionToPane(end, region, pane);
		DrawLine(paneStart, paneEnd, new Color(0f, 0f, 0f, 0.82f), width + 4f);
		if (feature.Provenance == FragmentAnnotationProvenance.Rover &&
			feature.Disposition == FragmentAnnotationDisposition.Proposed)
		{
			DrawDashedLine(paneStart, paneEnd, color, width);
		}
		else
		{
			DrawLine(paneStart, paneEnd, color, width);
		}
		if (!drewFeature) labelPosition = (paneStart + paneEnd) * 0.5f;
		drewFeature = true;
	}

	private void DrawDashedLine(Vector2 start, Vector2 end, Color color, float width)
	{
		Vector2 delta = end - start;
		float length = delta.Length();
		if (length <= 0.01f) return;
		Vector2 direction = delta / length;
		for (float offset = 0f; offset < length; offset += 13f)
			DrawLine(start + direction * offset,
				start + direction * MathF.Min(offset + 8f, length), color, width);
	}

	private void DrawFeatureLabel(int featureId, Vector2 position, Color color, bool isPending)
	{
		string label = isPending ? $"PENDING · F{featureId}" : $"F{featureId}";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 13;
		Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(position + new Vector2(5f, -size.Y - 4f), size + new Vector2(7f, 4f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.9f), true);
		DrawString(font, background.Position + new Vector2(3.5f, size.Y), label,
			HorizontalAlignment.Left, -1, fontSize, color);
	}

	private static Vector2 RegionToPane(Vector2 point, Rect2 region, Rect2 pane)
	{
		Vector2 relative = (point - region.Position) / region.Size;
		return pane.Position + relative * pane.Size;
	}

	private static bool ClipSegment(Rect2 rectangle, ref Vector2 start, ref Vector2 end)
	{
		Vector2 delta = end - start;
		float minimum = 0f;
		float maximum = 1f;
		if (!Clip(-delta.X, start.X - rectangle.Position.X, ref minimum, ref maximum) ||
			!Clip(delta.X, rectangle.End.X - start.X, ref minimum, ref maximum) ||
			!Clip(-delta.Y, start.Y - rectangle.Position.Y, ref minimum, ref maximum) ||
			!Clip(delta.Y, rectangle.End.Y - start.Y, ref minimum, ref maximum)) return false;
		Vector2 originalStart = start;
		start = originalStart + delta * minimum;
		end = originalStart + delta * maximum;
		return true;
	}

	private static bool Clip(float direction, float distance, ref float minimum, ref float maximum)
	{
		if (Mathf.IsZeroApprox(direction)) return distance >= 0f;
		float ratio = distance / direction;
		if (direction < 0f)
		{
			if (ratio > maximum) return false;
			if (ratio > minimum) minimum = ratio;
		}
		else
		{
			if (ratio < minimum) return false;
			if (ratio < maximum) maximum = ratio;
		}
		return true;
	}
}
