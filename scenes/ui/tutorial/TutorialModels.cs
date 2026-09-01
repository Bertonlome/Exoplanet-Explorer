using System;
using Godot;

namespace Game.UI.Tutorial;

public enum TutorialEvent
{
	None,
	LevelReady,
	BuildChoiceSelected,
	BuildingPlaced,
	RobotSelected,
	RobotMoved,
	ExplorationStarted,
	ExplorationStopped,
	FragmentAnalysisRequested,
	FragmentAnalysisOpened,
	FragmentAnalysisCompleted,
	BridgePlaced,
	AntennaPlaced,
	RobotLiftRequested,
	RobotDropRequested,
	RobotLifted,
	RobotDropped,
	PrototypeActionCompleted,
}

public enum TutorialTriggerKind
{
	Immediate,
	AfterStep,
	Event,
}

public enum TutorialCompletionKind
{
	Continue,
	TargetPressed,
	Event,
	State,
}

public enum TutorialCalloutPlacement
{
	Auto,
	TopRight,
}

public sealed class TutorialEventContext
{
	public TutorialEvent Event { get; }
	public GodotObject Subject { get; }
	public GodotObject SecondarySubject { get; }
	public object Payload { get; }
	public Vector2I? WorldPosition { get; }

	public TutorialEventContext(
		TutorialEvent tutorialEvent,
		GodotObject subject = null,
		GodotObject secondarySubject = null,
		object payload = null,
		Vector2I? worldPosition = null)
	{
		Event = tutorialEvent;
		Subject = subject;
		SecondarySubject = secondarySubject;
		Payload = payload;
		WorldPosition = worldPosition;
	}
}

public sealed class TutorialTrigger
{
	public TutorialTriggerKind Kind { get; }
	public string PreviousStepId { get; }
	public TutorialEvent Event { get; }
	public Func<TutorialEventContext, bool> Predicate { get; }

	private TutorialTrigger(
		TutorialTriggerKind kind,
		string previousStepId = null,
		TutorialEvent tutorialEvent = TutorialEvent.None,
		Func<TutorialEventContext, bool> predicate = null)
	{
		Kind = kind;
		PreviousStepId = previousStepId;
		Event = tutorialEvent;
		Predicate = predicate;
	}

	public static TutorialTrigger Immediate()
	{
		return new TutorialTrigger(TutorialTriggerKind.Immediate);
	}

	public static TutorialTrigger After(string previousStepId)
	{
		return new TutorialTrigger(TutorialTriggerKind.AfterStep, previousStepId);
	}

	public static TutorialTrigger OnEvent(
		TutorialEvent tutorialEvent,
		Func<TutorialEventContext, bool> predicate = null)
	{
		return new TutorialTrigger(TutorialTriggerKind.Event, tutorialEvent: tutorialEvent, predicate: predicate);
	}

	public bool Matches(TutorialEventContext context)
	{
		return context != null && context.Event == Event && (Predicate == null || Predicate(context));
	}
}

public sealed class TutorialCompletion
{
	public TutorialCompletionKind Kind { get; }
	public TutorialEvent Event { get; }
	public Func<TutorialEventContext, bool> EventPredicate { get; }
	public Func<bool> StatePredicate { get; }

	private TutorialCompletion(
		TutorialCompletionKind kind,
		TutorialEvent tutorialEvent = TutorialEvent.None,
		Func<TutorialEventContext, bool> eventPredicate = null,
		Func<bool> statePredicate = null)
	{
		Kind = kind;
		Event = tutorialEvent;
		EventPredicate = eventPredicate;
		StatePredicate = statePredicate;
	}

	public static TutorialCompletion Continue()
	{
		return new TutorialCompletion(TutorialCompletionKind.Continue);
	}

	public static TutorialCompletion TargetPressed()
	{
		return new TutorialCompletion(TutorialCompletionKind.TargetPressed);
	}

	public static TutorialCompletion OnEvent(
		TutorialEvent tutorialEvent,
		Func<TutorialEventContext, bool> predicate = null)
	{
		return new TutorialCompletion(
			TutorialCompletionKind.Event,
			tutorialEvent,
			eventPredicate: predicate);
	}

	public static TutorialCompletion State(Func<bool> predicate)
	{
		return new TutorialCompletion(TutorialCompletionKind.State, statePredicate: predicate);
	}

	public bool Matches(TutorialEventContext context)
	{
		return Kind == TutorialCompletionKind.Event && context != null && context.Event == Event &&
			(EventPredicate == null || EventPredicate(context));
	}
}

public sealed class TutorialStep
{
	public string Id { get; }
	public string Title { get; }
	public string Text { get; }
	public string TargetId { get; }
	public TutorialOverlayMode Mode { get; }
	public TutorialTrigger Trigger { get; }
	public TutorialCompletion Completion { get; }
	public double MissingTargetTimeoutSeconds { get; }
	public bool Skippable { get; }
	public bool DimBackground { get; }
	public TutorialCalloutPlacement CalloutPlacement { get; }

	internal TutorialStep(
		string id,
		string title,
		string text,
		string targetId,
		TutorialOverlayMode mode,
		TutorialTrigger trigger,
		TutorialCompletion completion,
		double missingTargetTimeoutSeconds,
		bool skippable,
		bool dimBackground,
		TutorialCalloutPlacement calloutPlacement)
	{
		Id = id;
		Title = title;
		Text = text;
		TargetId = targetId;
		Mode = mode;
		Trigger = trigger;
		Completion = completion;
		MissingTargetTimeoutSeconds = missingTargetTimeoutSeconds;
		Skippable = skippable;
		DimBackground = dimBackground;
		CalloutPlacement = calloutPlacement;
	}
}

public static class TutorialTargetIds
{
	public const string PrototypeTarget = "prototype.target";
	public const string PrototypeMissingTarget = "prototype.missing-target";
	public const string StatusPanel = "game-ui.status-panel";
	public const string DeploymentPanel = "game-ui.deployment-panel";
	public const string BaseDeployButton = "game-ui.deploy.base";
	public const string RoverDeployButton = "game-ui.deploy.rover";
	public const string DroneDeployButton = "game-ui.deploy.drone";
	public const string PreplacedBase = "world.preplaced-base";
}
