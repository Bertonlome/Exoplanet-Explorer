using Game.Autoload;
using Godot;

namespace Game.UI.Tutorial;

public enum TutorialOverlayMode
{
	HardPause,
	GuidedAction,
}

/// <summary>
/// Presents tutorial copy while dimming everything except an optional screen-space target.
/// Pause ownership deliberately remains outside this view so a future TutorialDirector can
/// distinguish hard-paused explanations from guided actions that must reach gameplay code.
/// </summary>
public partial class TutorialOverlay : CanvasLayer
{
	[Signal]
	public delegate void ContinueRequestedEventHandler();

	[Signal]
	public delegate void SkipRequestedEventHandler();

	private const float FocusMargin = 10f;
	private const float ViewportMargin = 24f;
	private const float CalloutGap = 42f;

	private Control overlayRoot;
	private ColorRect topBlocker;
	private ColorRect bottomBlocker;
	private ColorRect leftBlocker;
	private ColorRect rightBlocker;
	private Panel focusBorder;
	private Line2D arrowLine;
	private Polygon2D arrowHead;
	private PanelContainer callout;
	private Label titleLabel;
	private Label bodyLabel;
	private Button continueButton;
	private Button skipButton;

	private Rect2? requestedFocusRect;
	private TutorialCalloutPlacement requestedCalloutPlacement;
	private Rect2 visibleFocusRect;
	private bool stepVisible;
	private double pulseTime;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		overlayRoot = GetNode<Control>("OverlayRoot");
		topBlocker = GetNode<ColorRect>("%TopBlocker");
		bottomBlocker = GetNode<ColorRect>("%BottomBlocker");
		leftBlocker = GetNode<ColorRect>("%LeftBlocker");
		rightBlocker = GetNode<ColorRect>("%RightBlocker");
		focusBorder = GetNode<Panel>("%FocusBorder");
		arrowLine = GetNode<Line2D>("%ArrowLine");
		arrowHead = GetNode<Polygon2D>("%ArrowHead");
		callout = GetNode<PanelContainer>("%Callout");
		titleLabel = GetNode<Label>("%TitleLabel");
		bodyLabel = GetNode<Label>("%BodyLabel");
		continueButton = GetNode<Button>("%ContinueButton");
		skipButton = GetNode<Button>("%SkipButton");

		AudioHelpers.RegisterButtons(new Button[] { continueButton, skipButton });
		continueButton.Pressed += OnContinuePressed;
		skipButton.Pressed += OnSkipPressed;
		GetViewport().SizeChanged += RefreshLayout;
		HideStep();
	}

	public override void _ExitTree()
	{
		if (continueButton != null)
		{
			continueButton.Pressed -= OnContinuePressed;
		}
		if (skipButton != null)
		{
			skipButton.Pressed -= OnSkipPressed;
		}
		if (GetViewport() != null)
		{
			GetViewport().SizeChanged -= RefreshLayout;
		}
	}

	public override void _Process(double delta)
	{
		if (!stepVisible)
		{
			return;
		}

		pulseTime += delta;
		// Use a deliberately broad range: the previous 0.72-1.0 pulse was imperceptible in play.
		float alpha = 0.35f + (0.65f * ((Mathf.Sin((float)pulseTime * 5f) + 1f) * 0.5f));
		focusBorder.Modulate = new Color(1f, 1f, 1f, alpha);
	}

	public void ShowStep(
		string title,
		string message,
		Rect2? targetScreenRect,
		TutorialOverlayMode mode,
		bool showContinue = true,
		bool showSkip = true,
		bool dimBackground = true,
		TutorialCalloutPlacement calloutPlacement = TutorialCalloutPlacement.Auto)
	{
		titleLabel.Text = title ?? string.Empty;
		bodyLabel.Text = message ?? string.Empty;
		requestedFocusRect = targetScreenRect;
		requestedCalloutPlacement = calloutPlacement;
		continueButton.Visible = showContinue;
		skipButton.Visible = showSkip;

		// A guided step with no resolved target is the safe text-only fallback: the dimmer remains
		// visible, but input passes through so a missing registration cannot trap the player.
		bool passThroughFallback = mode == TutorialOverlayMode.GuidedAction && !targetScreenRect.HasValue;
		SetBlockerMouseFilters(passThroughFallback
			? Control.MouseFilterEnum.Ignore
			: Control.MouseFilterEnum.Stop);
		SetBlockersVisible(dimBackground);
		overlayRoot.Visible = true;
		stepVisible = true;
		pulseTime = 0d;
		RefreshLayout();
		Callable.From(RefreshLayout).CallDeferred();

		if (showContinue)
		{
			continueButton.GrabFocus();
		}
		else if (showSkip)
		{
			skipButton.GrabFocus();
		}
	}

	public void SetFocusRect(Rect2? targetScreenRect)
	{
		requestedFocusRect = targetScreenRect;
		if (stepVisible)
		{
			RefreshLayout();
		}
	}

	public void HideStep()
	{
		stepVisible = false;
		requestedFocusRect = null;
		requestedCalloutPlacement = TutorialCalloutPlacement.Auto;
		pulseTime = 0d;
		if (overlayRoot != null)
		{
			overlayRoot.Visible = false;
		}
	}

	public void RefreshLayout()
	{
		if (!stepVisible || overlayRoot == null)
		{
			return;
		}

		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		overlayRoot.Size = viewportSize;
		visibleFocusRect = CalculateVisibleFocusRect(viewportSize);
		LayoutBlockers(viewportSize, visibleFocusRect);

		bool hasFocus = requestedFocusRect.HasValue && visibleFocusRect.Size.X > 0f &&
			visibleFocusRect.Size.Y > 0f;
		focusBorder.Visible = hasFocus;
		if (hasFocus)
		{
			ApplyRect(focusBorder, visibleFocusRect);
		}

		LayoutCallout(viewportSize, hasFocus);
		LayoutArrow(hasFocus);
	}

	private Rect2 CalculateVisibleFocusRect(Vector2 viewportSize)
	{
		if (!requestedFocusRect.HasValue)
		{
			return new Rect2();
		}

		Rect2 expanded = requestedFocusRect.Value.Grow(FocusMargin);
		float left = Mathf.Clamp(expanded.Position.X, 0f, viewportSize.X);
		float top = Mathf.Clamp(expanded.Position.Y, 0f, viewportSize.Y);
		float right = Mathf.Clamp(expanded.End.X, left, viewportSize.X);
		float bottom = Mathf.Clamp(expanded.End.Y, top, viewportSize.Y);
		return new Rect2(left, top, right - left, bottom - top);
	}

	private void LayoutBlockers(Vector2 viewportSize, Rect2 focusRect)
	{
		if (!requestedFocusRect.HasValue || focusRect.Size.X <= 0f || focusRect.Size.Y <= 0f)
		{
			ApplyRect(topBlocker, new Rect2(Vector2.Zero, viewportSize));
			ApplyRect(bottomBlocker, new Rect2());
			ApplyRect(leftBlocker, new Rect2());
			ApplyRect(rightBlocker, new Rect2());
			return;
		}

		float left = focusRect.Position.X;
		float top = focusRect.Position.Y;
		float right = focusRect.End.X;
		float bottom = focusRect.End.Y;

		ApplyRect(topBlocker, new Rect2(0f, 0f, viewportSize.X, top));
		ApplyRect(bottomBlocker, new Rect2(0f, bottom, viewportSize.X, viewportSize.Y - bottom));
		ApplyRect(leftBlocker, new Rect2(0f, top, left, bottom - top));
		ApplyRect(rightBlocker, new Rect2(right, top, viewportSize.X - right, bottom - top));
	}

	private void LayoutCallout(Vector2 viewportSize, bool hasFocus)
	{
		Vector2 minimumSize = callout.GetCombinedMinimumSize();
		minimumSize.X = Mathf.Max(minimumSize.X, 460f);
		minimumSize.Y = Mathf.Max(minimumSize.Y, 190f);
		minimumSize.X = Mathf.Min(minimumSize.X, Mathf.Max(240f, viewportSize.X - (ViewportMargin * 2f)));
		minimumSize.Y = Mathf.Min(minimumSize.Y, Mathf.Max(160f, viewportSize.Y - (ViewportMargin * 2f)));
		callout.Size = minimumSize;

		Vector2 position;
		if (requestedCalloutPlacement == TutorialCalloutPlacement.TopRight)
		{
			position = new Vector2(
				viewportSize.X - minimumSize.X - ViewportMargin,
				ViewportMargin);
		}
		else if (!hasFocus)
		{
			position = (viewportSize - minimumSize) * 0.5f;
		}
		else
		{
			float rightRoom = viewportSize.X - visibleFocusRect.End.X;
			float leftRoom = visibleFocusRect.Position.X;
			float bottomRoom = viewportSize.Y - visibleFocusRect.End.Y;
			float topRoom = visibleFocusRect.Position.Y;
			float largestRoom = Mathf.Max(Mathf.Max(rightRoom, leftRoom), Mathf.Max(bottomRoom, topRoom));

			if (largestRoom == rightRoom)
			{
				position = new Vector2(
					visibleFocusRect.End.X + CalloutGap,
					visibleFocusRect.GetCenter().Y - (minimumSize.Y * 0.5f));
			}
			else if (largestRoom == leftRoom)
			{
				position = new Vector2(
					visibleFocusRect.Position.X - minimumSize.X - CalloutGap,
					visibleFocusRect.GetCenter().Y - (minimumSize.Y * 0.5f));
			}
			else if (largestRoom == bottomRoom)
			{
				position = new Vector2(
					visibleFocusRect.GetCenter().X - (minimumSize.X * 0.5f),
					visibleFocusRect.End.Y + CalloutGap);
			}
			else
			{
				position = new Vector2(
					visibleFocusRect.GetCenter().X - (minimumSize.X * 0.5f),
					visibleFocusRect.Position.Y - minimumSize.Y - CalloutGap);
			}
		}

		position.X = Mathf.Clamp(position.X, ViewportMargin, Mathf.Max(ViewportMargin, viewportSize.X - minimumSize.X - ViewportMargin));
		position.Y = Mathf.Clamp(position.Y, ViewportMargin, Mathf.Max(ViewportMargin, viewportSize.Y - minimumSize.Y - ViewportMargin));
		callout.Position = position;
	}

	private void LayoutArrow(bool hasFocus)
	{
		if (!hasFocus)
		{
			arrowLine.Visible = false;
			arrowHead.Visible = false;
			return;
		}

		Rect2 calloutRect = callout.GetGlobalRect();
		Vector2 start = ClosestPointOnRect(calloutRect, visibleFocusRect.GetCenter());
		Vector2 target = ClosestPointOnRect(visibleFocusRect, calloutRect.GetCenter());
		Vector2 direction = target - start;
		if (direction.LengthSquared() < 64f)
		{
			arrowLine.Visible = false;
			arrowHead.Visible = false;
			return;
		}

		Vector2 normalized = direction.Normalized();
		start += normalized * 8f;
		Vector2 lineEnd = target - (normalized * 18f);
		arrowLine.Points = new Vector2[] { start, lineEnd };
		arrowLine.Visible = true;
		arrowHead.Position = target;
		arrowHead.Rotation = normalized.Angle();
		arrowHead.Visible = true;
	}

	private static Vector2 ClosestPointOnRect(Rect2 rect, Vector2 point)
	{
		return new Vector2(
			Mathf.Clamp(point.X, rect.Position.X, rect.End.X),
			Mathf.Clamp(point.Y, rect.Position.Y, rect.End.Y));
	}

	private void SetBlockerMouseFilters(Control.MouseFilterEnum mouseFilter)
	{
		topBlocker.MouseFilter = mouseFilter;
		bottomBlocker.MouseFilter = mouseFilter;
		leftBlocker.MouseFilter = mouseFilter;
		rightBlocker.MouseFilter = mouseFilter;
	}

	private void SetBlockersVisible(bool visible)
	{
		topBlocker.Visible = visible;
		bottomBlocker.Visible = visible;
		leftBlocker.Visible = visible;
		rightBlocker.Visible = visible;
	}

	private static void ApplyRect(Control control, Rect2 rect)
	{
		control.Position = rect.Position;
		control.Size = new Vector2(Mathf.Max(0f, rect.Size.X), Mathf.Max(0f, rect.Size.Y));
	}

	private void OnContinuePressed()
	{
		EmitSignal(SignalName.ContinueRequested);
	}

	private void OnSkipPressed()
	{
		EmitSignal(SignalName.SkipRequested);
	}
}
