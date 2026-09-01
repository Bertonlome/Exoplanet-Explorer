using Godot;

namespace Game.UI.Tutorial;

/// <summary>
/// Standalone Checkpoint 1 harness. Run TutorialOverlayPrototype.tscn as the current scene.
/// It exercises Continue, target-press, semantic-event, missing-target fallback, and cleanup.
/// </summary>
public partial class TutorialOverlayPrototype : Control
{
	private sealed class PrototypeTutorialScript : TutorialScript
	{
		public override void Build(TutorialBuilder tutorial)
		{
			tutorial.Step("continue-step")
				.When(TutorialEvent.LevelReady)
				.Say(
					"STEP 1 — CONTINUE",
					"This step is hard-paused. Press Continue to verify the director receives overlay input and advances the script.")
				.HardPause()
				.UntilContinue();

			tutorial.Step("target-step")
				.After("continue-step")
				.Say(
					"STEP 2 — TARGET CLICK",
					"Only the highlighted gameplay target should be usable. Click it to complete this guided-action step.")
				.PointTo(TutorialTargetIds.PrototypeTarget)
				.GuideAction()
				.UntilTargetPressed();

			tutorial.Step("event-step")
				.After("target-step")
				.Say(
					"STEP 3 — EVENT COMPLETION",
					"This step deliberately requests an unregistered target. After the fallback appears, click EVENT SOURCE to publish its semantic completion event.")
				.PointTo(TutorialTargetIds.PrototypeMissingTarget)
				.GuideAction()
				.FallbackAfter(0.8d)
				.Until(TutorialEvent.PrototypeActionCompleted);
		}
	}

	private TutorialOverlay overlay;
	private TutorialDirector director;
	private TutorialEventBridge eventBridge;
	private TutorialTargetRegistry targetRegistry;
	private Button mockTarget;
	private Button eventSourceButton;
	private Label statusLabel;
	private TutorialTargetRegistration mockTargetRegistration;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		overlay = GetNode<TutorialOverlay>("TutorialOverlay");
		director = GetNode<TutorialDirector>("TutorialDirector");
		eventBridge = GetNode<TutorialEventBridge>("TutorialEventBridge");
		targetRegistry = GetNode<TutorialTargetRegistry>("TutorialTargetRegistry");
		mockTarget = GetNode<Button>("%MockTarget");
		eventSourceButton = GetNode<Button>("%EventSourceButton");
		statusLabel = GetNode<Label>("%StatusLabel");

		mockTarget.Pressed += OnMockTargetPressed;
		eventSourceButton.Pressed += OnEventSourcePressed;
		director.StepStarted += OnStepStarted;
		director.StepCompleted += OnStepCompleted;
		director.TargetFallbackUsed += OnTargetFallbackUsed;
		director.TutorialCompleted += OnTutorialCompleted;
		director.TutorialSkipped += OnTutorialSkipped;

		eventBridge.Start();
		mockTargetRegistration = targetRegistry.RegisterControl(
			TutorialTargetIds.PrototypeTarget,
			mockTarget);
		director.Initialize(overlay, eventBridge, targetRegistry);
		director.Start(new PrototypeTutorialScript());
		Callable.From(() => eventBridge.Publish(TutorialEvent.LevelReady)).CallDeferred();
	}

	public override void _ExitTree()
	{
		director?.Stop();
		eventBridge?.Stop();
		mockTargetRegistration?.Dispose();
		mockTargetRegistration = null;

		if (mockTarget != null)
		{
			mockTarget.Pressed -= OnMockTargetPressed;
		}
		if (eventSourceButton != null)
		{
			eventSourceButton.Pressed -= OnEventSourcePressed;
		}
		if (director != null)
		{
			director.StepStarted -= OnStepStarted;
			director.StepCompleted -= OnStepCompleted;
			director.TargetFallbackUsed -= OnTargetFallbackUsed;
			director.TutorialCompleted -= OnTutorialCompleted;
			director.TutorialSkipped -= OnTutorialSkipped;
		}
	}

	private void OnMockTargetPressed()
	{
		statusLabel.Text = "The registered target received its click.";
	}

	private void OnEventSourcePressed()
	{
		statusLabel.Text = "Publishing PrototypeActionCompleted…";
		eventBridge.Publish(TutorialEvent.PrototypeActionCompleted, "prototype-button");
	}

	private void OnStepStarted(string stepId)
	{
		statusLabel.Text = $"Director started: {stepId}";
	}

	private void OnStepCompleted(string stepId)
	{
		statusLabel.Text = $"Director completed: {stepId}";
	}

	private void OnTargetFallbackUsed(string stepId, string targetId)
	{
		statusLabel.Text = $"Missing-target fallback active for '{targetId}'. EVENT SOURCE should now be clickable.";
	}

	private void OnTutorialCompleted()
	{
		statusLabel.Text = "CHECKPOINT 1 PASSED: all three completion types ran and the overlay cleaned up.";
	}

	private void OnTutorialSkipped()
	{
		statusLabel.Text = "Tutorial skipped. The overlay and any hard pause were cleaned up.";
	}
}
