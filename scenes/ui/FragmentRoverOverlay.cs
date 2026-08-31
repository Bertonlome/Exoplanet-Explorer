using System;
using Godot;

public partial class FragmentRoverOverlay : Control
{
    private FragmentAutonomyState state;
    private Vector2 sampleSize = Vector2.One;
    private float viewZoom = 1f;
    private Vector2 viewPan;
    private Color roverFeatureColor = new(1f, 0.15f, 0.75f, 0.95f);
    private Color acceptedRoverFeatureColor = new(1f, 0.72f, 0.1f, 0.98f);
    private Color playerFeatureColor = new(0.25f, 1f, 0.45f, 0.95f);
	private Color pendingFeatureColor = new(0.15f, 0.95f, 1f, 1f);
	private Color candidateRegionColor = new(1f, 0.7f, 0.15f, 0.35f);
    private bool showRoverFeatures = true;
	private bool showFeatures = true;
	private bool showRegions = true;
	private bool showRoverRegions = true;
	private bool showStructures = true;
	private bool showRoverStructures = true;
	private bool showOrientations = true;
	private bool showArrows = true;
	private bool orientationIsolation;
	private const float OrientationAnimationDuration = 1.2f;
	private const float RegionInteractionPadding = 14f;
	private const float StructureStrokeDragThreshold = 10f;
	private float orientationAnimationElapsed = OrientationAnimationDuration;
	private bool structureEditing;
	private int editingStructureId = -1;
	private int editingRegionId = -1;
	private int selectedStructureStrokeId = -1;
	private bool isStructureDrawGesture;
	private Vector2 structureDrawStart;
	private Vector2 structureDrawCurrent;
	private Color structureColor = new(1f, 0.2f, 0.85f, 0.9f);
	private Color orientationColor = new(0.15f, 0.95f, 1f, 0.95f);
	private Color orientationReferenceColor = new(1f, 1f, 1f, 0.72f);
	private Color orientationGhostColor = new(0.25f, 1f, 0.75f, 0.48f);
	private bool regionDrawingArmed;
	private bool arrowDrawingArmed;
	private Vector2 arrowDrawStart;
	private Vector2 arrowDrawCurrent;
	private Vector2 regionDrawStart;
	private Vector2 regionDrawCurrent;
	private int resizeRegionId = -1;
	private int deleteRegionId = -1;
	private Vector2 resizeAnchor;
	private Vector2 resizeCurrent;
    private bool isPointerDown;
	private bool isPanGesture;
	private Vector2 pointerDownPosition;
	private Rect2? navigationTarget;
	private int? navigationTargetRegionId;
	private bool navigationActive;
	private Color navigationTargetColor = new(1f, 1f, 1f, 0.9f);

    public event Action<int> FeatureSelected;
    public event Action<Vector2> PanRequested;
    public event Action<Vector2, float> ZoomRequested;
	public event Action<int> RegionSelected;
	public event Action<Rect2> RegionDrawn;
	public event Action<int, Rect2> RegionResized;
	public event Action<int> RegionDeleteRequested;
	public event Action<int> RegionLockRequested;
	public event Action<int, int> StructureEditRequested;
	public event Action<int, int> StructureValidateRequested;
	public event Action<int> StructureFeatureToggled;
	public event Action<int> StructureFeatureRemoved;
	public event Action<Vector2, Vector2> StructureStrokeDrawn;
	public event Action StructureEditingCancelled;
    public event Action<Vector2, Vector2> ArrowDrawn;

	public bool IsStructureEditing => structureEditing;
	public bool IsEditingStructure(int regionId, int structureId) =>
		structureEditing && editingRegionId == regionId && editingStructureId == structureId;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public void SetState(FragmentAutonomyState state)
    {
        this.state = state;
		if (resizeRegionId >= 0 &&
			state?.LockedRegionViews.Exists(view => view.RegionId == resizeRegionId) == true)
		{
			CancelRegionResize();
			SetArrowDrawingArmed(false);
		}
		if (deleteRegionId >= 0 && state?.CandidateRegions.Exists(region =>
			region.Id == deleteRegionId &&
			region.Disposition != FragmentAnnotationDisposition.Dismissed) != true)
			deleteRegionId = -1;
        QueueRedraw();
    }

    public void SetShowRoverFeatures(bool visible)
    {
        if (showRoverFeatures == visible) return;
        showRoverFeatures = visible;
        QueueRedraw();
    }

	public void SetShowFeatures(bool visible)
	{
		showFeatures = visible;
		QueueRedraw();
	}

	public void SetShowRegions(bool visible)
	{
		showRegions = visible;
		if (!visible)
		{
			resizeRegionId = -1;
			deleteRegionId = -1;
		}
		QueueRedraw();
	}

	public void SetShowRoverRegions(bool visible)
	{
		showRoverRegions = visible;
		if (!visible && resizeRegionId >= 0)
		{
			FragmentCandidateRegion region = state?.CandidateRegions.Find(
				candidate => candidate.Id == resizeRegionId);
			if (region?.Provenance == FragmentAnnotationProvenance.Rover) CancelRegionResize();
		}
		QueueRedraw();
	}

	public void SetShowStructures(bool visible)
	{
		showStructures = visible;
		if (!visible) SetStructureEditing(false);
		QueueRedraw();
	}

	public void SetShowRoverStructures(bool visible)
	{
		showRoverStructures = visible;
		QueueRedraw();
	}

	public void SetShowOrientations(bool visible)
	{
		showOrientations = visible;
		QueueRedraw();
	}

	public void SetShowArrows(bool visible)
	{
		showArrows = visible;
		if (!visible) SetArrowDrawingArmed(false);
		QueueRedraw();
	}

	public void SetArrowDrawingArmed(bool armed)
	{
		arrowDrawingArmed = armed;
		if (armed)
		{
			regionDrawingArmed = false;
			structureEditing = false;
			resizeRegionId = -1;
		}
		isPointerDown = false;
		isPanGesture = false;
		MouseDefaultCursorShape = armed ? CursorShape.Cross : CursorShape.PointingHand;
		QueueRedraw();
	}

	public void SetOrientationIsolation(bool isolated)
	{
		if (orientationIsolation == isolated) return;
		orientationIsolation = isolated;
		QueueRedraw();
	}

	public void RestartOrientationPreviewAnimation()
	{
		orientationAnimationElapsed = 0f;
		QueueRedraw();
	}

	public void SetStructureEditing(bool editing, int structureId = -1, int regionId = -1)
	{
		structureEditing = editing;
		editingStructureId = editing ? structureId : -1;
		editingRegionId = editing ? regionId : -1;
		selectedStructureStrokeId = -1;
		isStructureDrawGesture = false;
		if (editing)
		{
			arrowDrawingArmed = false;
			regionDrawingArmed = false;
			resizeRegionId = -1;
			GrabFocus();
		}
		else ReleaseFocus();
		isPointerDown = false;
		isPanGesture = false;
		MouseDefaultCursorShape = editing || regionDrawingArmed || arrowDrawingArmed
			? CursorShape.Cross
			: CursorShape.PointingHand;
		QueueRedraw();
	}

	public void SetRegionDrawingArmed(bool armed)
	{
		regionDrawingArmed = armed;
		if (armed) arrowDrawingArmed = false;
		isPointerDown = false;
		isPanGesture = false;
		MouseDefaultCursorShape = armed ? CursorShape.Cross : CursorShape.PointingHand;
		QueueRedraw();
	}

    public void SetView(Vector2 sampleSize, float zoom, Vector2 pan)
    {
        Vector2 nextSampleSize = new(
            MathF.Max(sampleSize.X, 1f),
            MathF.Max(sampleSize.Y, 1f));
        float nextZoom = MathF.Max(zoom, 0.001f);
        if (this.sampleSize.IsEqualApprox(nextSampleSize) &&
            Mathf.IsEqualApprox(viewZoom, nextZoom) &&
            viewPan.IsEqualApprox(pan))
        {
            return;
        }
        this.sampleSize = nextSampleSize;
        viewZoom = nextZoom;
        viewPan = pan;
        QueueRedraw();
    }

	public void SetFeatureColors(
        Color roverColor,
        Color acceptedRoverColor,
        Color playerColor,
		Color pendingColor)
    {
        roverFeatureColor = roverColor;
        acceptedRoverFeatureColor = acceptedRoverColor;
        playerFeatureColor = playerColor;
		pendingFeatureColor = pendingColor;
        QueueRedraw();
    }

	public void SetCandidateRegionColor(Color color)
	{
		candidateRegionColor = color;
		QueueRedraw();
	}

	public void SetStructureColor(Color color)
	{
		structureColor = color;
		QueueRedraw();
	}

	public void SetOrientationColors(Color axis, Color reference, Color ghost)
	{
		orientationColor = axis;
		orientationReferenceColor = reference;
		orientationGhostColor = ghost;
		QueueRedraw();
	}

	public void SetNavigationTargetColor(Color color)
	{
		navigationTargetColor = color;
		QueueRedraw();
	}

	public void SetNavigationTarget(Rect2? bounds, int? regionId, bool active)
	{
		navigationTarget = bounds;
		navigationTargetRegionId = regionId;
		navigationActive = active;
		QueueRedraw();
	}

	public void Clear()
    {
        state = null;
        isPointerDown = false;
        isPanGesture = false;
		regionDrawingArmed = false;
		arrowDrawingArmed = false;
		structureEditing = false;
		editingStructureId = -1;
		editingRegionId = -1;
		selectedStructureStrokeId = -1;
		isStructureDrawGesture = false;
		resizeRegionId = -1;
		deleteRegionId = -1;
		navigationTarget = null;
		navigationTargetRegionId = null;
		navigationActive = false;
		orientationIsolation = false;
		orientationAnimationElapsed = OrientationAnimationDuration;
		MouseDefaultCursorShape = CursorShape.PointingHand;
	        QueueRedraw();
	    }

	public override void _Process(double delta)
	{
		if (orientationAnimationElapsed >= OrientationAnimationDuration) return;
		orientationAnimationElapsed = MathF.Min(
			orientationAnimationElapsed + (float)delta,
			OrientationAnimationDuration);
		QueueRedraw();
	}

    public override void _GuiInput(InputEvent inputEvent)
    {
		if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode == Key.Escape)
			{
				if (structureEditing)
				{
					SetStructureEditing(false);
					StructureEditingCancelled?.Invoke();
				}
				CancelRegionResize();
				deleteRegionId = -1;
				AcceptEvent();
				return;
			}
			if (structureEditing &&
				(key.Keycode == Key.Delete || key.Keycode == Key.Backspace) &&
				selectedStructureStrokeId >= 0)
			{
				int featureId = selectedStructureStrokeId;
				selectedStructureStrokeId = -1;
				StructureFeatureRemoved?.Invoke(featureId);
				QueueRedraw();
				AcceptEvent();
				return;
			}
			if (!structureEditing &&
				(key.Keycode == Key.Delete || key.Keycode == Key.Backspace) &&
				deleteRegionId >= 0)
			{
				int regionId = deleteRegionId;
				deleteRegionId = -1;
				CancelRegionResize();
				RegionDeleteRequested?.Invoke(regionId);
				AcceptEvent();
				return;
			}
		}
        if (inputEvent is InputEventMouseButton button)
        {
			if (button.Pressed && button.ButtonIndex == MouseButton.Right && structureEditing)
			{
				SetStructureEditing(false);
				StructureEditingCancelled?.Invoke();
				AcceptEvent();
				return;
			}
			if (button.Pressed && button.ButtonIndex == MouseButton.Right && resizeRegionId >= 0)
			{
				CancelRegionResize();
				AcceptEvent();
				return;
			}
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomRequested?.Invoke(button.Position, 1.15f);
                AcceptEvent();
                return;
            }
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomRequested?.Invoke(button.Position, 1f / 1.15f);
                AcceptEvent();
                return;
            }
			if (button.ButtonIndex == MouseButton.Left)
			{
				if (button.Pressed && !button.DoubleClick) deleteRegionId = -1;
				if (button.Pressed && !button.DoubleClick && !regionDrawingArmed &&
					!arrowDrawingArmed && TryFindStructureValidateButton(
						button.Position, out int validateRegionId, out int validateStructureId))
				{
					StructureValidateRequested?.Invoke(validateRegionId, validateStructureId);
					AcceptEvent();
					return;
				}
				if (button.Pressed && !button.DoubleClick && !regionDrawingArmed &&
					!arrowDrawingArmed && TryFindRegionLockButton(
						button.Position, out int lockRegionId))
				{
					RegionLockRequested?.Invoke(lockRegionId);
					AcceptEvent();
					return;
				}
				if (button.Pressed && !button.DoubleClick && !regionDrawingArmed &&
					!arrowDrawingArmed && TryFindStructureEditButton(
						button.Position, out int editRegionId, out int editStructureId))
				{
					StructureEditRequested?.Invoke(editRegionId, editStructureId);
					AcceptEvent();
					return;
				}
				if (button.Pressed && button.DoubleClick && !regionDrawingArmed &&
					!arrowDrawingArmed)
				{
					if (structureEditing)
					{
						AcceptEvent();
						return;
					}
					int regionId = FindRegionAt(button.Position, RegionInteractionPadding);
					if (regionId >= 0)
					{
						bool locked = state?.LockedRegionViews.Exists(view =>
							view.RegionId == regionId) == true;
						resizeRegionId = locked ? -1 : regionId;
						deleteRegionId = regionId;
						isPointerDown = false;
						RegionSelected?.Invoke(regionId);
						MouseDefaultCursorShape = locked
							? CursorShape.PointingHand
							: CursorShape.Cross;
						GrabFocus();
						QueueRedraw();
						AcceptEvent();
						return;
					}
				}
                if (button.Pressed)
                {
                    isPointerDown = true;
                    isPanGesture = false;
                    pointerDownPosition = button.Position;
					if (resizeRegionId >= 0)
					{
						Rect2 rectangle = GetRegionViewportRect(resizeRegionId);
						resizeAnchor = GetOppositeCorner(rectangle, button.Position);
						resizeCurrent = button.Position;
					}
					if (regionDrawingArmed)
					{
						regionDrawStart = button.Position;
						regionDrawCurrent = button.Position;
					}
					if (arrowDrawingArmed)
					{
						arrowDrawStart = button.Position;
						arrowDrawCurrent = button.Position;
					}
					if (structureEditing)
					{
						structureDrawStart = button.Position;
						structureDrawCurrent = button.Position;
						isStructureDrawGesture = false;
					}
                }
                else if (isPointerDown)
                {
					if (resizeRegionId >= 0)
					{
						Rect2 normalizedBounds = ViewportRectToNormalized(resizeAnchor, button.Position);
						int completedRegionId = resizeRegionId;
						CancelRegionResize();
						if (normalizedBounds.Size.X >= 0.01f && normalizedBounds.Size.Y >= 0.01f)
							RegionResized?.Invoke(completedRegionId, normalizedBounds);
					}
					else if (arrowDrawingArmed)
					{
						Vector2 start = arrowDrawStart;
						Vector2 end = button.Position;
						SetArrowDrawingArmed(false);
						if (start.DistanceTo(end) >= 12f)
							ArrowDrawn?.Invoke(ViewportToNormalized(start), ViewportToNormalized(end));
					}
					else if (regionDrawingArmed)
					{
						Rect2 normalizedBounds = ViewportRectToNormalized(regionDrawStart, button.Position);
						regionDrawingArmed = false;
						MouseDefaultCursorShape = CursorShape.PointingHand;
						RegionDrawn?.Invoke(normalizedBounds);
					}
					else if (structureEditing)
					{
						if (isStructureDrawGesture &&
							structureDrawStart.DistanceTo(button.Position) >=
								StructureStrokeDragThreshold)
						{
							StructureStrokeDrawn?.Invoke(
								ViewportToNormalized(structureDrawStart),
								ViewportToNormalized(button.Position));
							selectedStructureStrokeId = -1;
						}
						else
						{
							int featureId = FindNearestFeature(button.Position, 16f);
							selectedStructureStrokeId = featureId;
							if (featureId >= 0 && !IsEditingStructureMember(featureId))
								StructureFeatureToggled?.Invoke(featureId);
						}
						isStructureDrawGesture = false;
						QueueRedraw();
					}
					else if (!isPanGesture)
                    {
                        int featureId = FindNearestFeature(button.Position, 12f);
						if (featureId >= 0) FeatureSelected?.Invoke(featureId);
						else
						{
							int regionId = FindRegionAt(button.Position);
							if (regionId >= 0) RegionSelected?.Invoke(regionId);
						}
                    }
                    isPointerDown = false;
                    isPanGesture = false;
                }
                AcceptEvent();
                return;
            }
        }

		if (isPointerDown && inputEvent is InputEventMouseMotion motion)
		{
			if (resizeRegionId >= 0)
			{
				resizeCurrent = motion.Position;
				QueueRedraw();
				AcceptEvent();
				return;
			}
			if (arrowDrawingArmed)
			{
				arrowDrawCurrent = motion.Position;
				QueueRedraw();
				AcceptEvent();
				return;
			}
			if (regionDrawingArmed)
			{
				regionDrawCurrent = motion.Position;
				QueueRedraw();
				AcceptEvent();
				return;
			}
			if (structureEditing)
			{
				structureDrawCurrent = motion.Position;
				if (!isStructureDrawGesture &&
					structureDrawStart.DistanceTo(motion.Position) >= StructureStrokeDragThreshold)
					isStructureDrawGesture = true;
				QueueRedraw();
				AcceptEvent();
				return;
			}
            if (!isPanGesture && pointerDownPosition.DistanceTo(motion.Position) >= 5f)
                isPanGesture = true;
            if (isPanGesture) PanRequested?.Invoke(motion.Relative);
            AcceptEvent();
        }
    }

	    public override void _Draw()
	    {
	        if (state == null) return;
		if (orientationIsolation)
		{
			DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black, true);
			DrawIsolatedOrientationStructure();
			DrawOrientationCues();
			DrawArrows(true);
			return;
		}
		DrawLockedReferenceBackgrounds();
		DrawRegions();
		DrawStructures();
		DrawLockedReferenceFeatures();
	        foreach (FragmentDetectedFeature feature in state.DetectedFeatures)
	        {
			if (!IsFeatureVisible(feature) || IsFeatureInsideLockedReference(feature) ||
				(showStructures && IsVisibleStructureMember(feature.Id))) continue;
            bool selected = state.SelectedFeatureId == feature.Id;
			bool pending = selected &&
				feature.Disposition == FragmentAnnotationDisposition.Proposed;
			Color color = feature.Provenance == FragmentAnnotationProvenance.Player
                ? playerFeatureColor
				: feature.IsInferred
					? new Color(1f, 0.45f, 0.85f, 0.92f)
                : feature.Disposition == FragmentAnnotationDisposition.Accepted
                    ? acceptedRoverFeatureColor
                    : roverFeatureColor;
			if (pending) color = pendingFeatureColor;
            float width = pending ? 5f : selected ? 4f : 2.5f;
            if (feature.Segments.Count == 0)
            {
                DrawFeatureSegment(feature, feature.Start, feature.End, color, width);
            }
            else
            {
                foreach (FragmentFeatureSegment segment in feature.Segments)
                    DrawFeatureSegment(feature, segment.Start, segment.End, color, width);
            }

            if (selected)
            {
				Color selectionColor = pending ? pendingFeatureColor : Colors.White;
				DrawCircle(NormalizedToViewport(feature.Start), 6f, selectionColor, false, 2f);
				DrawCircle(NormalizedToViewport(feature.End), 6f, selectionColor, false, 2f);
            }
            else if (feature.Provenance == FragmentAnnotationProvenance.Rover)
            {
                Vector2 marker = NormalizedToViewport((feature.Start + feature.End) * 0.5f);
                DrawCircle(marker, 4f, color, true);
                DrawCircle(marker, 6f, Colors.Black, false, 2f);
            }

			if (feature.Provenance == FragmentAnnotationProvenance.Rover)
				DrawFeatureNumber(feature, color, pending);
		}
		DrawStructureLabels();
		DrawArrows();
		DrawStructureEditButtons();
		DrawRegionLockButtons();
		if (regionDrawingArmed && isPointerDown)
		{
			Rect2 preview = OrderedRect(regionDrawStart, regionDrawCurrent);
			DrawRect(preview, new Color(0.25f, 1f, 0.45f, 0.18f), true);
			DrawRect(preview, new Color(0.25f, 1f, 0.45f, 0.95f), false, 2f);
		}
		if (arrowDrawingArmed && isPointerDown)
		{
			Vector2 direction = arrowDrawCurrent - arrowDrawStart;
			DrawLine(arrowDrawStart, arrowDrawCurrent, Colors.Black, 8f, true);
			DrawLine(arrowDrawStart, arrowDrawCurrent, Colors.White, 4f, true);
			if (direction.LengthSquared() > 1f)
				DrawArrowHead(arrowDrawCurrent, direction.Normalized(), Colors.White);
		}
		if (structureEditing && isPointerDown && isStructureDrawGesture)
		{
			DrawLine(structureDrawStart, structureDrawCurrent, Colors.Black, 9f, true);
			DrawLine(structureDrawStart, structureDrawCurrent, structureColor, 5f, true);
		}
		if (resizeRegionId >= 0 && isPointerDown)
		{
			Rect2 preview = OrderedRect(resizeAnchor, resizeCurrent);
			DrawRect(preview, new Color(1f, 1f, 1f, 0.12f), true);
			DrawRect(preview, Colors.White, false, 3f);
		}
		DrawNavigationTarget();
		DrawLockedReferenceIndicators();
    }

	private void DrawArrows(bool orientationSpace = false)
	{
		if (!showArrows || state == null) return;
		foreach (FragmentArrowCandidate candidate in state.ArrowCandidates)
		{
			if (orientationSpace && state.OrientationSourceView?.RegionId is int sourceRegionId &&
				candidate.RegionId >= 0 && candidate.RegionId != sourceRegionId) continue;
			bool selected = state.SelectedArrowId == candidate.Id;
			if (candidate.Disposition == FragmentAnnotationDisposition.Dismissed && !selected)
				continue;
			FragmentDetectedStructure orientationSource = orientationSpace
				? GetOrientationSourceStructure()
				: null;
			Vector2 tail = orientationSource == null
				? NormalizedToViewport(candidate.Tail)
				: OrientationPointToViewport(candidate.Tail, orientationSource);
			Vector2 tip = orientationSource == null
				? NormalizedToViewport(candidate.Tip)
				: OrientationPointToViewport(candidate.Tip, orientationSource);
			Vector2 direction = tip - tail;
			if (direction.LengthSquared() < 1f) continue;
			Color color = candidate.Disposition switch
			{
				FragmentAnnotationDisposition.Accepted => new Color(0.25f, 1f, 0.45f, 1f),
				FragmentAnnotationDisposition.Dismissed => new Color(1f, 0.25f, 0.22f, 0.9f),
				_ when candidate.IsPlayerDefined => new Color(1f, 0.35f, 0.9f, 1f),
				_ => new Color(1f, 0.78f, 0.12f, 0.95f)
			};
			float width = selected ? 5f : 3f;
			DrawLine(tail, tip, Colors.Black, width + 5f, true);
			DrawLine(tail, tip, color, width, true);
			DrawArrowHead(tip, direction.Normalized(), color);
			DrawCircle(tail, selected ? 6f : 4f, color, false, 2f);
			string label = $"A{candidate.Id}" +
				(candidate.IsPlayerDefined ? " · PLAYER" : string.Empty);
			Font font = ThemeDB.FallbackFont;
			const int fontSize = 13;
			Vector2 textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
			Rect2 background = new((tail + tip) * 0.5f + new Vector2(7f, -textSize.Y - 4f),
				textSize + new Vector2(8f, 5f));
			DrawRect(background, new Color(0f, 0f, 0f, 0.92f), true);
			DrawString(font, background.Position + new Vector2(4f, textSize.Y), label,
				HorizontalAlignment.Left, -1, fontSize, color);
		}
	}

	private void DrawOrientationCues()
	{
		if (!showOrientations || state.SelectedOrientationId is not int hypothesisId) return;
		FragmentOrientationHypothesis hypothesis = state.OrientationHypotheses.Find(candidate =>
			candidate.Id == hypothesisId);
		if (hypothesis == null) return;
		// Orientation can legitimately be reconstructed straight from the selected region's
		// observable features. That creates a transient source structure which is intentionally not
		// inserted into DetectedStructures, so always render from the retained orientation snapshot.
		FragmentDetectedStructure structure = GetOrientationSourceStructure();
		if (structure == null || !TryGetStructureGeometry(
			structure, out Vector2 sourceCenter, out Rect2 viewportBounds)) return;

		Color axisColor = hypothesis.Disposition switch
		{
			FragmentAnnotationDisposition.Accepted => playerFeatureColor,
			FragmentAnnotationDisposition.Dismissed => new Color(1f, 0.28f, 0.24f, 0.9f),
			_ => orientationColor
		};
		float length = Mathf.Clamp(
			MathF.Max(viewportBounds.Size.X, viewportBounds.Size.Y) * 0.62f, 55f, 230f);
		Vector2 center = orientationIsolation
			? new Vector2(Size.X * 0.75f, Size.Y * 0.5f)
			: sourceCenter;
		Vector2 upright = Vector2.Up;

		DrawDashedLine(center - Vector2.Up * length, center + Vector2.Up * length,
			orientationReferenceColor, 2f);
		DrawLine(center - upright * length, center + upright * length,
			new Color(0f, 0f, 0f, 0.84f), 7f);
		DrawLine(center - upright * length, center + upright * length, axisColor, 3.5f);
		DrawArrowHead(center + upright * length, upright, axisColor);
		FragmentRotationCorrection correction = state.RotationCorrection;
		float previewDegrees = correction != null &&
			correction.SourceOrientationId == hypothesis.Id &&
			correction.Disposition != FragmentAnnotationDisposition.Dismissed
			? correction.ProposedDegrees
			: -hypothesis.AxisDegrees;
		DrawOrientationGhost(
			structure,
			sourceCenter,
			center,
			previewDegrees,
			axisColor);

		string label = $"ROT{hypothesis.Id} · UPRIGHT?";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 14;
		Vector2 textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(
			center + upright * length + new Vector2(7f, -textSize.Y - 5f),
			textSize + new Vector2(10f, 6f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.94f), true);
		DrawString(font, background.Position + new Vector2(5f, textSize.Y + 1f), label,
			HorizontalAlignment.Left, -1, fontSize, axisColor);
		if (orientationIsolation)
		{
			DrawString(font, new Vector2(Size.X * 0.25f - 34f, 30f), "CURRENT",
				HorizontalAlignment.Left, -1, fontSize, structureColor);
			DrawString(font, new Vector2(Size.X * 0.75f - 42f, 30f), "PROPOSED",
				HorizontalAlignment.Left, -1, fontSize, axisColor);
		}
	}

	private void DrawIsolatedOrientationStructure()
	{
		FragmentDetectedStructure structure = GetOrientationSourceStructure();
		if (structure == null) return;
		foreach (int featureId in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindOrientationFeature(featureId);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				DrawIsolatedSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					DrawIsolatedSegment(segment.Start, segment.End);
		}

		void DrawIsolatedSegment(Vector2 start, Vector2 end)
		{
			Vector2 viewportStart = OrientationPointToViewport(start, structure);
			Vector2 viewportEnd = OrientationPointToViewport(end, structure);
			DrawLine(viewportStart, viewportEnd, new Color(0f, 0f, 0f, 0.95f), 9f, true);
			DrawLine(viewportStart, viewportEnd, structureColor, 5f, true);
		}
	}

	private FragmentDetectedStructure GetOrientationSourceStructure()
	{
		if (state == null) return null;
		if (state.OrientationSourceStructure != null)
			return state.OrientationSourceStructure;
		if (state.SelectedOrientationId is int hypothesisId)
		{
			FragmentOrientationHypothesis hypothesis = state.OrientationHypotheses.Find(candidate =>
				candidate.Id == hypothesisId);
			if (hypothesis != null)
				return state.DetectedStructures.Find(candidate =>
					candidate.Id == hypothesis.SourceStructureId &&
					candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
		}
		return state.SelectedStructureId is int structureId
			? state.DetectedStructures.Find(candidate =>
				candidate.Id == structureId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed)
			: null;
	}

	private System.Collections.Generic.IReadOnlyList<FragmentDetectedFeature>
		GetOrientationFeatures() =>
			state?.OrientationSourceView?.Features ??
			(System.Collections.Generic.IReadOnlyList<FragmentDetectedFeature>)state?.DetectedFeatures ??
			Array.Empty<FragmentDetectedFeature>();

	private FragmentDetectedFeature FindOrientationFeature(int featureId)
	{
		foreach (FragmentDetectedFeature feature in GetOrientationFeatures())
			if (feature.Id == featureId &&
				feature.Disposition != FragmentAnnotationDisposition.Dismissed)
				return feature;
		return null;
	}

	private Vector2 GetOrientationSampleSize() =>
		state?.OrientationSourceView?.Scan?.SampleSize ?? sampleSize;

	private Vector2 OrientationPointToViewport(
		Vector2 normalizedPoint,
		FragmentDetectedStructure structure)
	{
		if (!orientationIsolation || !TryGetStructureSampleBounds(structure, out Rect2 bounds))
			return NormalizedToViewport(normalizedPoint);
		const float margin = 54f;
		Vector2 available = new(
			MathF.Max(Size.X * 0.5f - margin * 2f, 1f),
			MathF.Max(Size.Y - margin * 2f, 1f));
		float scale = MathF.Min(
			available.X / MathF.Max(bounds.Size.X, 0.0001f),
			available.Y / MathF.Max(bounds.Size.Y, 0.0001f));
		Vector2 fittedSize = bounds.Size * scale;
		Vector2 origin = new(
			(Size.X * 0.5f - fittedSize.X) * 0.5f,
			(Size.Y - fittedSize.Y) * 0.5f);
		Vector2 samplePoint = normalizedPoint * GetOrientationSampleSize();
		return origin + (samplePoint - bounds.Position) * scale;
	}

	private bool TryGetStructureSampleBounds(
		FragmentDetectedStructure structure,
		out Rect2 bounds)
	{
		bool initialized = false;
		Rect2 result = new();
		foreach (int featureId in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindOrientationFeature(featureId);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				AddSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					AddSegment(segment.Start, segment.End);
		}
		bounds = result;
		return initialized;

		void AddSegment(Vector2 start, Vector2 end)
		{
				AddPoint(start * GetOrientationSampleSize());
				AddPoint(end * GetOrientationSampleSize());
		}

		void AddPoint(Vector2 point)
		{
			if (!initialized)
			{
				result = new Rect2(point, Vector2.Zero);
				initialized = true;
			}
			else result = result.Expand(point);
		}
	}

	private void DrawOrientationGhost(
		FragmentDetectedStructure structure,
		Vector2 sourceCenter,
		Vector2 targetCenter,
		float correctionDegrees,
		Color axisColor)
	{
		Color ghost = orientationGhostColor;
		ghost.A = MathF.Min(ghost.A, axisColor.A * 0.55f);
		float progress = Mathf.SmoothStep(
			0f,
			1f,
			Mathf.Clamp(orientationAnimationElapsed / OrientationAnimationDuration, 0f, 1f));
		float radians = Mathf.DegToRad(correctionDegrees * progress);
		foreach (int featureId in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindOrientationFeature(featureId);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				DrawGhostSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					DrawGhostSegment(segment.Start, segment.End);
		}

		void DrawGhostSegment(Vector2 normalizedStart, Vector2 normalizedEnd)
		{
			Vector2 start = targetCenter +
				(OrientationPointToViewport(normalizedStart, structure) - sourceCenter).Rotated(radians);
			Vector2 end = targetCenter +
				(OrientationPointToViewport(normalizedEnd, structure) - sourceCenter).Rotated(radians);
			DrawDashedLine(start, end, ghost, 3f);
		}
	}

	private bool TryGetStructureGeometry(
		FragmentDetectedStructure structure,
		out Vector2 center,
		out Rect2 bounds)
	{
		Vector2 pointSum = Vector2.Zero;
		Rect2 localBounds = new();
		int pointCount = 0;
		bool initialized = false;
		foreach (int featureId in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindOrientationFeature(featureId);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				AddSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					AddSegment(segment.Start, segment.End);
		}
		if (pointCount == 0)
		{
			center = Vector2.Zero;
			bounds = new Rect2();
			return false;
		}
		center = pointSum / pointCount;
		bounds = localBounds;
		return true;

		void AddSegment(Vector2 normalizedStart, Vector2 normalizedEnd)
		{
			AddPoint(OrientationPointToViewport(normalizedStart, structure));
			AddPoint(OrientationPointToViewport(normalizedEnd, structure));
		}

		void AddPoint(Vector2 point)
		{
			pointSum += point;
			pointCount++;
			if (!initialized)
			{
				localBounds = new Rect2(point, Vector2.Zero);
				initialized = true;
			}
			else localBounds = localBounds.Expand(point);
		}
	}

	private void DrawArrowHead(Vector2 tip, Vector2 direction, Color color)
	{
		Vector2 back = -direction.Normalized();
		DrawLine(tip, tip + back.Rotated(0.55f) * 14f, color, 3.5f);
		DrawLine(tip, tip + back.Rotated(-0.55f) * 14f, color, 3.5f);
	}

	private void DrawStructures()
	{
		if (!showStructures) return;
		foreach (FragmentDetectedStructure structure in state.DetectedStructures)
		{
			if (structure.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverStructures &&
				 structure.Provenance == FragmentAnnotationProvenance.Rover &&
				 structure.Disposition != FragmentAnnotationDisposition.Accepted)) continue;
			Color color = structureColor;
			foreach (int featureId in structure.FeatureIds)
			{
				FragmentDetectedFeature feature = FindStructureFeature(featureId);
				if (feature == null) continue;
				bool strokeSelected = structureEditing && structure.Id == editingStructureId &&
					featureId == selectedStructureStrokeId;
				if (feature.Segments.Count == 0)
					DrawStructureSegment(feature.Start, feature.End, color,
						structure.Disposition == FragmentAnnotationDisposition.Accepted &&
						(!structureEditing || structure.Id != editingStructureId));
				else
					foreach (FragmentFeatureSegment segment in feature.Segments)
						DrawStructureSegment(segment.Start, segment.End, color,
							structure.Disposition == FragmentAnnotationDisposition.Accepted &&
							(!structureEditing || structure.Id != editingStructureId));
				if (strokeSelected)
				{
					Vector2 marker = NormalizedToViewport(GetFeatureCenter(feature));
					DrawCircle(marker, 9f, structureColor, false, 3f);
				}
			}
		}
	}

	private void DrawStructureLabels()
	{
		if (!showStructures) return;
		foreach (FragmentDetectedStructure structure in state.DetectedStructures)
		{
			if (structure.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverStructures &&
				 structure.Provenance == FragmentAnnotationProvenance.Rover &&
				 structure.Disposition != FragmentAnnotationDisposition.Accepted)) continue;
			Vector2 centroid = Vector2.Zero;
			int count = 0;
			foreach (int featureId in structure.FeatureIds)
			{
				FragmentDetectedFeature feature = state.DetectedFeatures.Find(candidate =>
					candidate.Id == featureId &&
					candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
				if (feature == null) continue;
				centroid += (feature.Start + feature.End) * 0.5f;
				count++;
			}
			if (count == 0) continue;
			bool selected = state.SelectedStructureId == structure.Id;
			DrawStructureLabel(structure, centroid / count, structureColor, selected);
		}
	}

	private void DrawStructureSegment(
		Vector2 start,
		Vector2 end,
		Color color,
		bool accepted)
	{
		Vector2 viewportStart = NormalizedToViewport(start);
		Vector2 viewportEnd = NormalizedToViewport(end);
		DrawLine(viewportStart, viewportEnd, new Color(0f, 0f, 0f, 0.82f), 8f);
		if (accepted) DrawLine(viewportStart, viewportEnd, color, 4f);
		else DrawDashedLine(viewportStart, viewportEnd, color, 4f);
	}

	private bool IsVisibleStructureMember(int featureId) => state.DetectedStructures.Exists(structure =>
		structure.Disposition != FragmentAnnotationDisposition.Dismissed &&
		structure.FeatureIds.Contains(featureId));

	private FragmentDetectedFeature FindStructureFeature(int featureId)
	{
		FragmentDetectedFeature live = state.DetectedFeatures.Find(feature => feature.Id == featureId);
		// A current review decision is authoritative. In particular, do not resurrect an old
		// locked-view copy after the player dismissed this ID or after rotation refreshed it.
		if (live != null)
			return live.Disposition == FragmentAnnotationDisposition.Dismissed ? null : live;
		foreach (FragmentLockedRegionView lockedView in state.LockedRegionViews)
		{
			FragmentDetectedFeature locked = lockedView.Features.Find(feature =>
				feature.Id == featureId &&
				feature.Disposition != FragmentAnnotationDisposition.Dismissed);
			if (locked != null) return locked;
		}
		return state.DetectedFeatures.Find(feature =>
			feature.Id == featureId &&
			feature.Disposition != FragmentAnnotationDisposition.Dismissed);
	}

	private void DrawStructureLabel(
		FragmentDetectedStructure structure,
		Vector2 normalizedCenter,
		Color color,
		bool selected)
	{
		string label = selected && structureEditing
			? $"EDITING · S{structure.Id}"
			: $"S{structure.Id}";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 15;
		Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(
			NormalizedToViewport(normalizedCenter) + new Vector2(8f, 8f),
			size + new Vector2(10f, 6f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.94f), true);
		DrawString(font, background.Position + new Vector2(5f, size.Y + 1f), label,
			HorizontalAlignment.Left, -1, fontSize, color);
	}

	private void DrawLockedReferenceBackgrounds()
	{
		foreach (FragmentLockedRegionView lockedView in state.LockedRegionViews)
		{
			if (lockedView?.Scan?.Primitives == null) continue;
			Rect2 viewportBounds = NormalizedRectToViewport(lockedView.NormalizedBounds);
			DrawRect(viewportBounds, new Color(0.01f, 0.02f, 0.035f, 0.97f), true);
			foreach (FragmentObservablePrimitive primitive in lockedView.Scan.Primitives)
			{
				Vector2 start = primitive.Start;
				Vector2 end = primitive.End;
				if (!ClipSegment(lockedView.NormalizedBounds, ref start, ref end)) continue;
				Color color = primitive.Color;
				color.A = Mathf.Clamp(color.A * MathF.Max(primitive.Intensity, 0.15f), 0.08f, 1f);
				DrawLine(
					NormalizedToViewport(start),
					NormalizedToViewport(end),
					color,
					Mathf.Clamp(primitive.Width * viewZoom, 1f, 8f),
					true);
			}
		}
	}

	private void DrawLockedReferenceFeatures()
	{
		if (!showFeatures) return;
		foreach (FragmentLockedRegionView lockedView in state.LockedRegionViews)
		{
			foreach (FragmentDetectedFeature feature in lockedView.Features)
			{
				FragmentDetectedFeature current = state.DetectedFeatures.Find(candidate =>
					candidate.Id == feature.Id);
				if (feature.Disposition == FragmentAnnotationDisposition.Dismissed ||
					current?.Disposition == FragmentAnnotationDisposition.Dismissed ||
					(showStructures && IsVisibleStructureMember(feature.Id))) continue;
				Color color = feature.Provenance == FragmentAnnotationProvenance.Player
					? playerFeatureColor
					: feature.Disposition == FragmentAnnotationDisposition.Accepted
						? acceptedRoverFeatureColor
						: roverFeatureColor;
				if (feature.Segments.Count == 0)
					DrawLockedFeatureSegment(feature.Start, feature.End, lockedView.NormalizedBounds, color);
				else
					foreach (FragmentFeatureSegment segment in feature.Segments)
						DrawLockedFeatureSegment(segment.Start, segment.End,
							lockedView.NormalizedBounds, color);
			}
		}
	}

	private void DrawLockedFeatureSegment(Vector2 start, Vector2 end, Rect2 bounds, Color color)
	{
		if (!ClipSegment(bounds, ref start, ref end)) return;
		Vector2 viewportStart = NormalizedToViewport(start);
		Vector2 viewportEnd = NormalizedToViewport(end);
		DrawLine(viewportStart, viewportEnd, new Color(0f, 0f, 0f, 0.82f), 6f);
		DrawLine(viewportStart, viewportEnd, color, 2.5f);
	}

	private void DrawLockedReferenceIndicators()
	{
		foreach (FragmentLockedRegionView lockedView in state.LockedRegionViews)
		{
			Rect2 rectangle = NormalizedRectToViewport(lockedView.NormalizedBounds);
			Color color = new(0.2f, 0.95f, 1f, 1f);
			DrawRect(rectangle, color, false, 3f);
			string label = $"LOCKED REFERENCE · R{lockedView.RegionId}";
			Font font = ThemeDB.FallbackFont;
			const int fontSize = 14;
			Vector2 textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
			Rect2 background = new(rectangle.Position + new Vector2(4f, 4f),
				textSize + new Vector2(10f, 6f));
			DrawRect(background, new Color(0f, 0f, 0f, 0.94f), true);
			DrawString(font, background.Position + new Vector2(5f, textSize.Y + 1f), label,
				HorizontalAlignment.Left, -1, fontSize, color);
		}
	}

	private bool IsFeatureInsideLockedReference(FragmentDetectedFeature feature)
	{
		Vector2 center = (feature.Start + feature.End) * 0.5f;
		return state.LockedRegionViews.Exists(view => view.NormalizedBounds.HasPoint(center));
	}

	private void DrawNavigationTarget()
	{
		if (!navigationTarget.HasValue) return;
		Rect2 rectangle = NormalizedRectToViewport(navigationTarget.Value);
		Color fill = navigationTargetColor;
		fill.A = navigationActive ? 0.16f : 0.08f;
		DrawRect(rectangle, fill, true);
		DrawDashedRect(rectangle, navigationTargetColor, navigationActive ? 4f : 3f);
		string label = navigationActive
			? $"NAVIGATING · R{navigationTargetRegionId}"
			: $"NEXT TARGET · R{navigationTargetRegionId}";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 15;
		Vector2 textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(
			rectangle.Position + new Vector2(4f, -textSize.Y - 7f),
			textSize + new Vector2(10f, 6f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.92f), true);
		DrawString(font, background.Position + new Vector2(5f, textSize.Y + 1f), label,
			HorizontalAlignment.Left, -1, fontSize, navigationTargetColor);
	}

	private void DrawDashedRect(Rect2 rectangle, Color color, float width)
	{
		Vector2 topRight = new(rectangle.End.X, rectangle.Position.Y);
		Vector2 bottomLeft = new(rectangle.Position.X, rectangle.End.Y);
		DrawDashedLine(rectangle.Position, topRight, color, width);
		DrawDashedLine(topRight, rectangle.End, color, width);
		DrawDashedLine(rectangle.End, bottomLeft, color, width);
		DrawDashedLine(bottomLeft, rectangle.Position, color, width);
	}

	private void DrawRegions()
	{
		if (!showRegions) return;
		foreach (FragmentCandidateRegion region in state.CandidateRegions)
		{
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverRegions && region.Provenance == FragmentAnnotationProvenance.Rover)) continue;
			Rect2 rectangle = NormalizedRectToViewport(region.NormalizedBounds);
			bool selected = state.SelectedRegionId == region.Id;
			bool locked = state.LockedRegionViews.Exists(view => view.RegionId == region.Id);
			Color color = region.Disposition == FragmentAnnotationDisposition.Proposed
				? candidateRegionColor
				: region.Provenance == FragmentAnnotationProvenance.Player
					? new Color(0.25f, 1f, 0.45f, 0.32f)
				: region.Disposition == FragmentAnnotationDisposition.Accepted
					? new Color(0.2f, 1f, 0.35f, 0.3f)
					: candidateRegionColor;
			if (locked) color.A = 0.04f;
			DrawRect(rectangle, color, true);
			Color border = new(color.R, color.G, color.B, 0.95f);
			DrawRect(rectangle, Colors.Black, false, selected ? 5f : 4f);
			DrawRect(rectangle, border, false, selected ? 3f : 2f);
			DrawRegionHeader(region, rectangle, border, selected, locked);
			if (resizeRegionId == region.Id) DrawResizeHandles(rectangle);
		}
	}

	private void DrawResizeHandles(Rect2 rectangle)
	{
		foreach (Vector2 corner in GetCorners(rectangle))
		{
			Rect2 handle = new(corner - new Vector2(7f, 7f), new Vector2(14f, 14f));
			DrawRect(handle, Colors.Black, true);
			DrawRect(handle.Grow(-3f), Colors.White, true);
		}
	}

	private Rect2 GetRegionViewportRect(int regionId)
	{
		FragmentCandidateRegion region = state?.CandidateRegions.Find(candidate => candidate.Id == regionId);
		return region == null ? new Rect2() : NormalizedRectToViewport(region.NormalizedBounds);
	}

	private static Vector2 GetOppositeCorner(Rect2 rectangle, Vector2 point)
	{
		Vector2[] corners = GetCorners(rectangle);
		int nearest = 0;
		float nearestDistance = point.DistanceSquaredTo(corners[0]);
		for (int index = 1; index < corners.Length; index++)
		{
			float distance = point.DistanceSquaredTo(corners[index]);
			if (distance >= nearestDistance) continue;
			nearest = index;
			nearestDistance = distance;
		}
		return corners[3 - nearest];
	}

	private static Vector2[] GetCorners(Rect2 rectangle) => new[]
	{
		rectangle.Position,
		new Vector2(rectangle.End.X, rectangle.Position.Y),
		new Vector2(rectangle.Position.X, rectangle.End.Y),
		rectangle.End
	};

	private void CancelRegionResize()
	{
		resizeRegionId = -1;
		isPointerDown = false;
		isPanGesture = false;
		MouseDefaultCursorShape = regionDrawingArmed ? CursorShape.Cross : CursorShape.PointingHand;
		ReleaseFocus();
		QueueRedraw();
	}

	private void DrawRegionHeader(
		FragmentCandidateRegion region,
		Rect2 rectangle,
		Color color,
		bool selected,
		bool locked)
	{
		string label = $"R{region.Id}";
		if (region.Id == deleteRegionId)
			label += " · DELETE TO REMOVE";
		else if (selected)
			label += locked ? " · REGION LOCKED" : " · DOUBLE CLICK TO RESIZE REGION";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 14;
		Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(rectangle.Position + new Vector2(3f, 3f), size + new Vector2(8f, 5f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.9f), true);
		DrawString(font, background.Position + new Vector2(4f, size.Y), label,
			HorizontalAlignment.Left, -1, fontSize, color);
	}

	private void DrawStructureEditButton(
		FragmentCandidateRegion region,
		FragmentDetectedStructure structure,
		Rect2 regionRectangle)
	{
		Rect2 button = GetStructureEditButtonRect(regionRectangle);
		bool active = structureEditing && editingRegionId == region.Id &&
			editingStructureId == structure.Id;
		Color color = structureColor;
		DrawRect(button, new Color(0f, 0f, 0f, 0.94f), true);
		DrawRect(button, color, false, active ? 3f : 2f);
		string label = active ? "EDITING STRUCTURE" : "EDIT STRUCTURE";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 12;
		Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		DrawString(font,
			button.Position + new Vector2((button.Size.X - size.X) * 0.5f, 16f),
			label, HorizontalAlignment.Left, -1, fontSize, color);

		Rect2 validateButton = GetStructureValidateButtonRect(regionRectangle);
		DrawRect(validateButton, new Color(0f, 0f, 0f, 0.94f), true);
		DrawRect(validateButton, color, false, active ? 3f : 2f);
		const string validateLabel = "VALIDATE STRUCTURE";
		Vector2 validateSize = font.GetStringSize(
			validateLabel, HorizontalAlignment.Left, -1, fontSize);
		DrawString(font,
			validateButton.Position + new Vector2(
				(validateButton.Size.X - validateSize.X) * 0.5f, 16f),
			validateLabel, HorizontalAlignment.Left, -1, fontSize, color);
		if (!active) return;

		const string instructions =
			"CLICK + DEL: DELETE STROKE · CLICK + DRAG: ADD STROKE";
		const int instructionFontSize = 11;
		Vector2 instructionSize = font.GetStringSize(
			instructions, HorizontalAlignment.Left, -1, instructionFontSize);
		Vector2 instructionPosition = new(
			MathF.Max(regionRectangle.Position.X + 3f,
				regionRectangle.End.X - instructionSize.X - 11f),
			regionRectangle.Position.Y + 30f);
		Rect2 instructionBackground = new(
			instructionPosition,
			instructionSize + new Vector2(8f, 5f));
		DrawRect(instructionBackground, new Color(0f, 0f, 0f, 0.94f), true);
		DrawString(font,
			instructionBackground.Position + new Vector2(4f, instructionSize.Y),
			instructions, HorizontalAlignment.Left, -1, instructionFontSize, color);
	}

	private void DrawStructureEditButtons()
	{
		if (state == null || !showRegions || !showStructures) return;
		foreach (FragmentCandidateRegion region in state.CandidateRegions)
		{
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverRegions && region.Provenance == FragmentAnnotationProvenance.Rover))
				continue;
			FragmentDetectedStructure structure = FindStructureInRegion(region);
			if (structure != null)
				DrawStructureEditButton(
					region,
					structure,
					NormalizedRectToViewport(region.NormalizedBounds));
		}
	}

	private static Rect2 GetStructureEditButtonRect(Rect2 regionRectangle)
	{
		const float width = 132f;
		const float validateWidth = 148f;
		const float gap = 4f;
		const float height = 23f;
		float x = MathF.Max(regionRectangle.Position.X + 3f,
			regionRectangle.End.X - width - validateWidth - gap - 3f);
		return new Rect2(new Vector2(x, regionRectangle.Position.Y + 3f),
			new Vector2(width, height));
	}

	private static Rect2 GetStructureValidateButtonRect(Rect2 regionRectangle)
	{
		Rect2 editButton = GetStructureEditButtonRect(regionRectangle);
		return new Rect2(
			new Vector2(editButton.End.X + 4f, editButton.Position.Y),
			new Vector2(148f, editButton.Size.Y));
	}

	private bool TryFindStructureEditButton(
		Vector2 point,
		out int regionId,
		out int structureId)
	{
		regionId = -1;
		structureId = -1;
		if (state == null || !showRegions || !showStructures) return false;
		for (int index = state.CandidateRegions.Count - 1; index >= 0; index--)
		{
			FragmentCandidateRegion region = state.CandidateRegions[index];
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverRegions && region.Provenance == FragmentAnnotationProvenance.Rover))
				continue;
			FragmentDetectedStructure structure = FindStructureInRegion(region);
			if (structure == null) continue;
			Rect2 rectangle = NormalizedRectToViewport(region.NormalizedBounds);
			if (!GetStructureEditButtonRect(rectangle).Grow(6f).HasPoint(point)) continue;
			regionId = region.Id;
			structureId = structure.Id;
			return true;
		}
		return false;
	}

	private bool TryFindStructureValidateButton(
		Vector2 point,
		out int regionId,
		out int structureId)
	{
		regionId = -1;
		structureId = -1;
		if (state == null || !showRegions || !showStructures) return false;
		for (int index = state.CandidateRegions.Count - 1; index >= 0; index--)
		{
			FragmentCandidateRegion region = state.CandidateRegions[index];
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverRegions && region.Provenance == FragmentAnnotationProvenance.Rover))
				continue;
			FragmentDetectedStructure structure = FindStructureInRegion(region);
			if (structure == null) continue;
			Rect2 rectangle = NormalizedRectToViewport(region.NormalizedBounds);
			if (!GetStructureValidateButtonRect(rectangle).Grow(2f).HasPoint(point)) continue;
			regionId = region.Id;
			structureId = structure.Id;
			return true;
		}
		return false;
	}

	private void DrawRegionLockButtons()
	{
		if (state == null || !showRegions) return;
		foreach (FragmentCandidateRegion region in state.CandidateRegions)
		{
			if (!CanShowRegionLockButton(region)) continue;
			bool locked = state.LockedRegionViews.Exists(view => view.RegionId == region.Id);
			DrawRegionLockButton(
				GetRegionLockButtonRect(NormalizedRectToViewport(region.NormalizedBounds)),
				locked);
		}
	}

	private void DrawRegionLockButton(Rect2 button, bool locked)
	{
		Color color = locked ? orientationColor : Colors.White;
		DrawRect(button, new Color(0f, 0f, 0f, 0.94f), true);
		DrawRect(button, color, false, 2f);
		string label = locked ? "UNLOCK" : "LOCK";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 12;
		Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		DrawString(font,
			button.Position + new Vector2((button.Size.X - size.X) * 0.5f, 16f),
			label, HorizontalAlignment.Left, -1, fontSize, color);
	}

	private static Rect2 GetRegionLockButtonRect(Rect2 regionRectangle)
	{
		const float width = 72f;
		const float height = 23f;
		return new Rect2(
			regionRectangle.End - new Vector2(width + 3f, height + 3f),
			new Vector2(width, height));
	}

	private bool TryFindRegionLockButton(Vector2 point, out int regionId)
	{
		regionId = -1;
		if (state == null || !showRegions) return false;
		for (int index = state.CandidateRegions.Count - 1; index >= 0; index--)
		{
			FragmentCandidateRegion region = state.CandidateRegions[index];
			if (!CanShowRegionLockButton(region)) continue;
			Rect2 rectangle = NormalizedRectToViewport(region.NormalizedBounds);
			if (!GetRegionLockButtonRect(rectangle).Grow(6f).HasPoint(point)) continue;
			regionId = region.Id;
			return true;
		}
		return false;
	}

	private bool CanShowRegionLockButton(FragmentCandidateRegion region) =>
		region != null &&
		region.Disposition == FragmentAnnotationDisposition.Accepted &&
		(showRoverRegions || region.Provenance != FragmentAnnotationProvenance.Rover);

	private FragmentDetectedStructure FindStructureInRegion(FragmentCandidateRegion region)
	{
		if (structureEditing && region.Id == editingRegionId)
		{
			FragmentDetectedStructure editing = state.DetectedStructures.Find(structure =>
				structure.Id == editingStructureId &&
				structure.Disposition != FragmentAnnotationDisposition.Dismissed);
			if (editing != null) return editing;
		}
		FragmentDetectedStructure best = null;
		foreach (FragmentDetectedStructure structure in state.DetectedStructures)
		{
			if (structure.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverStructures && structure.Provenance == FragmentAnnotationProvenance.Rover &&
				 structure.Disposition != FragmentAnnotationDisposition.Accepted)) continue;
			bool overlaps = false;
			foreach (int featureId in structure.FeatureIds)
			{
				FragmentDetectedFeature feature = FindStructureFeature(featureId);
				if (feature != null && region.NormalizedBounds.HasPoint(GetFeatureCenter(feature)))
				{
					overlaps = true;
					break;
				}
			}
			if (!overlaps) continue;
			if (structure.Id == state.SelectedStructureId) return structure;
			if (best == null ||
				(structure.Disposition == FragmentAnnotationDisposition.Accepted &&
				 best.Disposition != FragmentAnnotationDisposition.Accepted))
				best = structure;
		}
		return best;
	}

	private bool IsEditingStructureMember(int featureId) =>
		state?.DetectedStructures.Exists(structure =>
			structure.Id == editingStructureId && structure.FeatureIds.Contains(featureId)) == true;

	private static Vector2 GetFeatureCenter(FragmentDetectedFeature feature)
	{
		if (feature.Segments == null || feature.Segments.Count == 0)
			return (feature.Start + feature.End) * 0.5f;
		Vector2 sum = Vector2.Zero;
		foreach (FragmentFeatureSegment segment in feature.Segments)
			sum += (segment.Start + segment.End) * 0.5f;
		return sum / feature.Segments.Count;
	}

	private int FindRegionAt(Vector2 point, float padding = 0f)
	{
		if (state == null || !showRegions) return -1;
		for (int index = state.CandidateRegions.Count - 1; index >= 0; index--)
		{
			FragmentCandidateRegion region = state.CandidateRegions[index];
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverRegions && region.Provenance == FragmentAnnotationProvenance.Rover)) continue;
			if (NormalizedRectToViewport(region.NormalizedBounds).Grow(padding).HasPoint(point))
				return region.Id;
		}
		return -1;
	}

	private Rect2 NormalizedRectToViewport(Rect2 normalized)
	{
		Vector2 start = NormalizedToViewport(normalized.Position);
		Vector2 end = NormalizedToViewport(normalized.End);
		return OrderedRect(start, end);
	}

	private Rect2 ViewportRectToNormalized(Vector2 first, Vector2 second)
	{
		Vector2 start = ViewportToNormalized(first);
		Vector2 end = ViewportToNormalized(second);
		return OrderedRect(start, end);
	}

	private Vector2 ViewportToNormalized(Vector2 point)
	{
		Vector2 virtualPoint = sampleSize * 0.5f + (point - Size * 0.5f - viewPan) / viewZoom;
		return (virtualPoint / sampleSize).Clamp(Vector2.Zero, Vector2.One);
	}

	private static Rect2 OrderedRect(Vector2 first, Vector2 second)
	{
		Vector2 minimum = new(MathF.Min(first.X, second.X), MathF.Min(first.Y, second.Y));
		Vector2 maximum = new(MathF.Max(first.X, second.X), MathF.Max(first.Y, second.Y));
		return new Rect2(minimum, maximum - minimum);
	}

	private void DrawFeatureNumber(FragmentDetectedFeature feature, Color color, bool pending)
	{
		Vector2 marker = NormalizedToViewport((feature.Start + feature.End) * 0.5f);
		string number = pending ? $"PENDING · F{feature.Id}" : feature.Id.ToString();
		Font font = ThemeDB.FallbackFont;
		int fontSize = 14;
		Vector2 textSize = font.GetStringSize(number, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(marker + new Vector2(7f, -textSize.Y - 5f), textSize + new Vector2(8f, 5f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.9f), true);
		DrawString(font, background.Position + new Vector2(4f, textSize.Y), number,
			HorizontalAlignment.Left, -1, fontSize, color);
	}

    private bool IsFeatureVisible(FragmentDetectedFeature feature)
    {
		return showFeatures && feature.Disposition != FragmentAnnotationDisposition.Dismissed &&
            (showRoverFeatures || feature.Provenance != FragmentAnnotationProvenance.Rover);
    }

    private void DrawFeatureSegment(
        FragmentDetectedFeature feature,
        Vector2 normalizedStart,
        Vector2 normalizedEnd,
        Color color,
        float width)
    {
        Vector2 start = NormalizedToViewport(normalizedStart);
        Vector2 end = NormalizedToViewport(normalizedEnd);
        DrawLine(start, end, new Color(0f, 0f, 0f, 0.8f), width + 4f);
        if (feature.Provenance == FragmentAnnotationProvenance.Rover &&
            feature.Disposition == FragmentAnnotationDisposition.Proposed)
        {
            DrawDashedLine(start, end, color, width);
        }
        else
        {
            DrawLine(start, end, color, width);
        }
    }

    private void DrawDashedLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length <= 0.01f) return;
        Vector2 direction = delta / length;
        const float dashLength = 8f;
        const float gapLength = 5f;
        for (float offset = 0f; offset < length; offset += dashLength + gapLength)
        {
            float dashEnd = MathF.Min(offset + dashLength, length);
            DrawLine(start + direction * offset, start + direction * dashEnd, color, width);
        }
    }

    private int FindNearestFeature(Vector2 point, float maximumDistance)
    {
        if (state == null) return -1;
        int nearestId = -1;
        float nearestDistanceSquared = maximumDistance * maximumDistance;
        foreach (FragmentDetectedFeature feature in state.DetectedFeatures)
        {
            if (!IsFeatureVisible(feature)) continue;
            if (feature.Segments.Count == 0)
            {
                ConsiderSegment(feature.Id, feature.Start, feature.End);
            }
            else
            {
                foreach (FragmentFeatureSegment segment in feature.Segments)
                    ConsiderSegment(feature.Id, segment.Start, segment.End);
            }
        }
        return nearestId;

        void ConsiderSegment(int featureId, Vector2 normalizedStart, Vector2 normalizedEnd)
        {
            Vector2 closest = Geometry2D.GetClosestPointToSegment(
                point,
                NormalizedToViewport(normalizedStart),
                NormalizedToViewport(normalizedEnd));
            float distanceSquared = point.DistanceSquaredTo(closest);
            if (distanceSquared >= nearestDistanceSquared) return;
            nearestDistanceSquared = distanceSquared;
            nearestId = featureId;
        }
    }

    private Vector2 NormalizedToViewport(Vector2 normalized)
    {
        Vector2 virtualPoint = normalized * sampleSize;
        return Size * 0.5f + viewPan + (virtualPoint - sampleSize * 0.5f) * viewZoom;
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
