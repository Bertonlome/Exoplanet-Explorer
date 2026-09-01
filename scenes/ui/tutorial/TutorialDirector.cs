using System;
using System.Collections.Generic;
using Godot;

namespace Game.UI.Tutorial;

public partial class TutorialDirector : Node
{
	[Signal]
	public delegate void StepStartedEventHandler(string stepId);

	[Signal]
	public delegate void StepCompletedEventHandler(string stepId);

	[Signal]
	public delegate void TargetFallbackUsedEventHandler(string stepId, string targetId);

	[Signal]
	public delegate void TutorialCompletedEventHandler();

	[Signal]
	public delegate void TutorialSkippedEventHandler();

	private TutorialOverlay overlay;
	private TutorialEventBridge eventBridge;
	private TutorialTargetRegistry targetRegistry;
	private IReadOnlyList<TutorialStep> steps = Array.Empty<TutorialStep>();
	private readonly HashSet<string> completedStepIds = new();
	private TutorialStep currentStep;
	private int nextStepIndex;
	private BaseButton subscribedTargetButton;
	private bool running;
	private bool waitingForTrigger;
	private bool waitingForTarget;
	private bool targetFallbackActive;
	private double missingTargetElapsed;
	private bool ownsPause;
	private bool previousPauseState;

	public bool IsRunning => running;
	public string CurrentStepId => currentStep?.Id;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
	}

	public void Initialize(
		TutorialOverlay tutorialOverlay,
		TutorialEventBridge tutorialEventBridge,
		TutorialTargetRegistry tutorialTargetRegistry)
	{
		if (running)
		{
			throw new InvalidOperationException("Cannot replace tutorial dependencies while a script is running.");
		}

		overlay = tutorialOverlay ?? throw new ArgumentNullException(nameof(tutorialOverlay));
		eventBridge = tutorialEventBridge ?? throw new ArgumentNullException(nameof(tutorialEventBridge));
		targetRegistry = tutorialTargetRegistry ?? throw new ArgumentNullException(nameof(tutorialTargetRegistry));
	}

	public void Start(TutorialScript script)
	{
		if (script == null)
		{
			throw new ArgumentNullException(nameof(script));
		}
		EnsureInitialized();
		StopInternal(hideOverlay: true);

		steps = script.CreateSteps();
		completedStepIds.Clear();
		nextStepIndex = 0;
		running = true;
		eventBridge.EventPublished += OnEventPublished;
		overlay.ContinueRequested += OnContinueRequested;
		overlay.SkipRequested += OnSkipRequested;
		TryActivateNextStep();
	}

	public void Stop()
	{
		StopInternal(hideOverlay: true);
	}

	public override void _Process(double delta)
	{
		if (!running || currentStep == null)
		{
			return;
		}

		if (waitingForTarget)
		{
			if (TryResolveAndPresentTarget())
			{
				return;
			}

			missingTargetElapsed += delta;
			if (missingTargetElapsed >= currentStep.MissingTargetTimeoutSeconds)
			{
				ShowMissingTargetFallback();
			}
			return;
		}

		if (!targetFallbackActive && !string.IsNullOrWhiteSpace(currentStep.TargetId))
		{
			if (targetRegistry.TryResolve(currentStep.TargetId, out Node targetNode, out Rect2 targetRect))
			{
				overlay.SetFocusRect(targetRect);
				AttachTargetButton(targetNode as BaseButton);
			}
			else
			{
				DetachTargetButton();
				waitingForTarget = true;
				missingTargetElapsed = 0d;
			}
		}

		if (currentStep.Completion.Kind == TutorialCompletionKind.State &&
			currentStep.Completion.StatePredicate?.Invoke() == true)
		{
			CompleteCurrentStep();
		}
	}

	public override void _ExitTree()
	{
		StopInternal(hideOverlay: true);
	}

	private void TryActivateNextStep()
	{
		if (!running)
		{
			return;
		}
		if (nextStepIndex >= steps.Count)
		{
			FinishTutorial();
			return;
		}

		TutorialStep candidate = steps[nextStepIndex];
		if (!IsTriggerSatisfied(candidate.Trigger))
		{
			waitingForTrigger = true;
			return;
		}

		waitingForTrigger = false;
		currentStep = candidate;
		nextStepIndex++;
		PresentCurrentStep();
	}

	private bool IsTriggerSatisfied(TutorialTrigger trigger)
	{
		return trigger.Kind switch
		{
			TutorialTriggerKind.Immediate => true,
			TutorialTriggerKind.AfterStep => completedStepIds.Contains(trigger.PreviousStepId),
			TutorialTriggerKind.Event => eventBridge.WasPublished(trigger.Event, trigger.Predicate),
			_ => false,
		};
	}

	private void PresentCurrentStep()
	{
		waitingForTarget = false;
		targetFallbackActive = false;
		missingTargetElapsed = 0d;
		ApplyPausePolicy(currentStep.Mode);
		EmitSignal(SignalName.StepStarted, currentStep.Id);

		if (string.IsNullOrWhiteSpace(currentStep.TargetId))
		{
			ShowOverlay(null, currentStep.Mode);
			CheckAlreadySatisfiedCompletion();
			return;
		}

		if (!TryResolveAndPresentTarget())
		{
			waitingForTarget = true;
			// Block interaction only while giving dynamic UI a brief opportunity to register.
			overlay.ShowStep(
				currentStep.Title,
				$"{currentStep.Text}\n\nLocating the highlighted control…",
				null,
				TutorialOverlayMode.HardPause,
				showContinue: false,
				showSkip: currentStep.Skippable,
				dimBackground: currentStep.DimBackground,
				calloutPlacement: currentStep.CalloutPlacement);
		}
		CheckAlreadySatisfiedCompletion();
	}

	private bool TryResolveAndPresentTarget()
	{
		if (!targetRegistry.TryResolve(currentStep.TargetId, out Node targetNode, out Rect2 targetRect))
		{
			return false;
		}

		waitingForTarget = false;
		targetFallbackActive = false;
		AttachTargetButton(targetNode as BaseButton);
		ShowOverlay(targetRect, currentStep.Mode);
		return true;
	}

	private void ShowMissingTargetFallback()
	{
		waitingForTarget = false;
		targetFallbackActive = true;
		DetachTargetButton();
		GD.PushWarning(
			$"Tutorial step '{currentStep.Id}' could not resolve target '{currentStep.TargetId}'. " +
			"Using text-only fallback.");

		bool targetPressNeedsEscape = currentStep.Completion.Kind == TutorialCompletionKind.TargetPressed;
		overlay.ShowStep(
			currentStep.Title,
			$"{currentStep.Text}\n\nThe highlighted control is unavailable. Continue with the described action.",
			null,
			currentStep.Mode,
			showContinue: targetPressNeedsEscape,
			showSkip: currentStep.Skippable,
			dimBackground: currentStep.DimBackground,
			calloutPlacement: currentStep.CalloutPlacement);
		EmitSignal(SignalName.TargetFallbackUsed, currentStep.Id, currentStep.TargetId);
	}

	private void ShowOverlay(Rect2? targetRect, TutorialOverlayMode mode)
	{
		overlay.ShowStep(
			currentStep.Title,
			currentStep.Text,
			targetRect,
			mode,
			showContinue: currentStep.Completion.Kind == TutorialCompletionKind.Continue,
			showSkip: currentStep.Skippable,
			dimBackground: currentStep.DimBackground,
			calloutPlacement: currentStep.CalloutPlacement);
	}

	private void CheckAlreadySatisfiedCompletion()
	{
		if (currentStep == null)
		{
			return;
		}
		if (currentStep.Completion.Kind == TutorialCompletionKind.Event &&
			eventBridge.WasPublished(currentStep.Completion.Event, currentStep.Completion.EventPredicate))
		{
			Callable.From(CompleteCurrentStep).CallDeferred();
		}
		else if (currentStep.Completion.Kind == TutorialCompletionKind.State &&
			currentStep.Completion.StatePredicate?.Invoke() == true)
		{
			Callable.From(CompleteCurrentStep).CallDeferred();
		}
	}

	private void OnEventPublished(TutorialEventContext context)
	{
		if (!running)
		{
			return;
		}

		if (currentStep != null && currentStep.Completion.Matches(context))
		{
			CompleteCurrentStep();
			return;
		}

		if (currentStep == null && waitingForTrigger && nextStepIndex < steps.Count &&
			steps[nextStepIndex].Trigger.Matches(context))
		{
			TryActivateNextStep();
		}
	}

	private void OnContinueRequested()
	{
		if (currentStep == null)
		{
			return;
		}
		if (currentStep.Completion.Kind == TutorialCompletionKind.Continue ||
			(targetFallbackActive && currentStep.Completion.Kind == TutorialCompletionKind.TargetPressed))
		{
			CompleteCurrentStep();
		}
	}

	private void OnSkipRequested()
	{
		if (currentStep == null || !currentStep.Skippable)
		{
			return;
		}

		StopInternal(hideOverlay: true);
		EmitSignal(SignalName.TutorialSkipped);
	}

	private void OnTargetPressed()
	{
		if (currentStep?.Completion.Kind == TutorialCompletionKind.TargetPressed)
		{
			CompleteCurrentStep();
		}
	}

	private void CompleteCurrentStep()
	{
		if (!running || currentStep == null)
		{
			return;
		}

		TutorialStep completedStep = currentStep;
		DetachTargetButton();
		overlay.HideStep();
		RestorePausePolicy();
		waitingForTarget = false;
		targetFallbackActive = false;
		currentStep = null;
		completedStepIds.Add(completedStep.Id);
		EmitSignal(SignalName.StepCompleted, completedStep.Id);
		TryActivateNextStep();
	}

	private void FinishTutorial()
	{
		StopInternal(hideOverlay: true);
		EmitSignal(SignalName.TutorialCompleted);
	}

	private void ApplyPausePolicy(TutorialOverlayMode mode)
	{
		RestorePausePolicy();
		if (mode != TutorialOverlayMode.HardPause)
		{
			return;
		}

		previousPauseState = GetTree().Paused;
		ownsPause = true;
		GetTree().Paused = true;
	}

	private void RestorePausePolicy()
	{
		if (!ownsPause || GetTree() == null)
		{
			return;
		}

		GetTree().Paused = previousPauseState;
		ownsPause = false;
	}

	private void AttachTargetButton(BaseButton button)
	{
		if (subscribedTargetButton == button)
		{
			return;
		}

		DetachTargetButton();
		subscribedTargetButton = button;
		if (subscribedTargetButton != null)
		{
			subscribedTargetButton.Pressed += OnTargetPressed;
		}
	}

	private void DetachTargetButton()
	{
		if (IsInstanceValid(subscribedTargetButton))
		{
			subscribedTargetButton.Pressed -= OnTargetPressed;
		}
		subscribedTargetButton = null;
	}

	private void StopInternal(bool hideOverlay)
	{
		DetachTargetButton();
		RestorePausePolicy();
		if (overlay != null)
		{
			overlay.ContinueRequested -= OnContinueRequested;
			overlay.SkipRequested -= OnSkipRequested;
			if (hideOverlay)
			{
				overlay.HideStep();
			}
		}
		if (eventBridge != null)
		{
			eventBridge.EventPublished -= OnEventPublished;
		}

		running = false;
		waitingForTrigger = false;
		waitingForTarget = false;
		targetFallbackActive = false;
		currentStep = null;
		steps = Array.Empty<TutorialStep>();
	}

	private void EnsureInitialized()
	{
		if (overlay == null || eventBridge == null || targetRegistry == null)
		{
			throw new InvalidOperationException(
				"TutorialDirector.Initialize must be called before starting a tutorial script.");
		}
	}
}
