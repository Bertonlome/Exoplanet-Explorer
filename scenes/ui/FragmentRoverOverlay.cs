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
	private bool regionDrawingArmed;
	private Vector2 regionDrawStart;
	private Vector2 regionDrawCurrent;
	private int resizeRegionId = -1;
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

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public void SetState(FragmentAutonomyState state)
    {
        this.state = state;
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
		if (!visible) resizeRegionId = -1;
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

	public void SetRegionDrawingArmed(bool armed)
	{
		regionDrawingArmed = armed;
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
		resizeRegionId = -1;
		navigationTarget = null;
		navigationTargetRegionId = null;
		navigationActive = false;
		MouseDefaultCursorShape = CursorShape.PointingHand;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
		if (inputEvent is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
		{
			CancelRegionResize();
			AcceptEvent();
			return;
		}
        if (inputEvent is InputEventMouseButton button)
        {
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
				if (button.Pressed && button.DoubleClick && !regionDrawingArmed)
				{
					int regionId = FindRegionAt(button.Position);
					if (regionId >= 0)
					{
						resizeRegionId = regionId;
						isPointerDown = false;
						RegionSelected?.Invoke(regionId);
						MouseDefaultCursorShape = CursorShape.Cross;
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
					else if (regionDrawingArmed)
					{
						Rect2 normalizedBounds = ViewportRectToNormalized(regionDrawStart, button.Position);
						regionDrawingArmed = false;
						MouseDefaultCursorShape = CursorShape.PointingHand;
						RegionDrawn?.Invoke(normalizedBounds);
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
			if (regionDrawingArmed)
			{
				regionDrawCurrent = motion.Position;
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
		DrawLockedReferenceBackgrounds();
		DrawRegions();
		DrawLockedReferenceFeatures();
        foreach (FragmentDetectedFeature feature in state.DetectedFeatures)
        {
			if (!IsFeatureVisible(feature) || IsFeatureInsideLockedReference(feature)) continue;
            bool selected = state.SelectedFeatureId == feature.Id;
			bool pending = selected &&
				feature.Disposition == FragmentAnnotationDisposition.Proposed;
            Color color = feature.Provenance == FragmentAnnotationProvenance.Player
                ? playerFeatureColor
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
		if (regionDrawingArmed && isPointerDown)
		{
			Rect2 preview = OrderedRect(regionDrawStart, regionDrawCurrent);
			DrawRect(preview, new Color(0.25f, 1f, 0.45f, 0.18f), true);
			DrawRect(preview, new Color(0.25f, 1f, 0.45f, 0.95f), false, 2f);
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
		foreach (FragmentLockedRegionView lockedView in state.LockedRegionViews)
		{
			foreach (FragmentDetectedFeature feature in lockedView.Features)
			{
				if (feature.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
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
			Color color = region.Provenance == FragmentAnnotationProvenance.Player
				? new Color(0.25f, 1f, 0.45f, 0.32f)
				: region.Disposition == FragmentAnnotationDisposition.Accepted
					? new Color(0.2f, 1f, 0.35f, 0.3f)
					: candidateRegionColor;
			if (locked) color.A = 0.04f;
			DrawRect(rectangle, color, true);
			Color border = new(color.R, color.G, color.B, 0.95f);
			DrawRect(rectangle, Colors.Black, false, selected ? 5f : 4f);
			DrawRect(rectangle, border, false, selected ? 3f : 2f);
			DrawRegionNumber(region, rectangle, border);
			if (resizeRegionId == region.Id) DrawResizeHandles(rectangle);
		}
	}

	private void DrawResizeHandles(Rect2 rectangle)
	{
		foreach (Vector2 corner in GetCorners(rectangle))
		{
			Rect2 handle = new(corner - new Vector2(5f, 5f), new Vector2(10f, 10f));
			DrawRect(handle, Colors.Black, true);
			DrawRect(handle.Grow(-2f), Colors.White, true);
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

	private void DrawRegionNumber(FragmentCandidateRegion region, Rect2 rectangle, Color color)
	{
		string label = $"R{region.Id}";
		Font font = ThemeDB.FallbackFont;
		const int fontSize = 14;
		Vector2 size = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
		Rect2 background = new(rectangle.Position + new Vector2(3f, 3f), size + new Vector2(8f, 5f));
		DrawRect(background, new Color(0f, 0f, 0f, 0.9f), true);
		DrawString(font, background.Position + new Vector2(4f, size.Y), label,
			HorizontalAlignment.Left, -1, fontSize, color);
	}

	private int FindRegionAt(Vector2 point)
	{
		if (state == null || !showRegions) return -1;
		for (int index = state.CandidateRegions.Count - 1; index >= 0; index--)
		{
			FragmentCandidateRegion region = state.CandidateRegions[index];
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed ||
				(!showRoverRegions && region.Provenance == FragmentAnnotationProvenance.Rover)) continue;
			if (NormalizedRectToViewport(region.NormalizedBounds).HasPoint(point)) return region.Id;
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
