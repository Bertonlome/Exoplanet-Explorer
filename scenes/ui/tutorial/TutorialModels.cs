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
	DirectedMoveRequested,
	BatteryDecreased,
	BatteryRecharged,
	ChargingStarted,
	ResourceCollected,
	ResourcesDropped,
	MaterialCreated,
	ExplorationModeSelected,
	ExplorationStarted,
	DroneScoutStarted,
	CustomPathStarted,
	CustomPathExecuted,
	RobotOutOfAntennaCoverage,
	FragmentModeSelected,
	FragmentGlyphRevealed,
	FragmentGlyphUpright,
	FragmentReloaded,
	ExplorationStopped,
	FragmentAnalysisRequested,
	FragmentAnalysisOpened,
	FragmentAnalysisCompleted,
	FragmentAnalysisExited,
	BridgePlaced,
	MonolithTouched,
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
	TopLeft,
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
	public const string MinimapContainer = "game-ui.minimap-container";
	public const string DeploymentPanel = "game-ui.deployment-panel";
	public const string BaseDeployButton = "game-ui.deploy.base";
	public const string RoverDeployButton = "game-ui.deploy.rover";
	public const string DroneDeployButton = "game-ui.deploy.drone";
	public const string PreplacedBase = "world.preplaced-base";
	public const string DeployedRover = "world.deployed-rover";
	public const string ManualMovementDestination = "world.level1.manual-destination";
	public const string ReturnDestination = "world.level1.return-destination";
	public const string DeployedRoverBattery = "game-ui.unit.rover.battery";
	public const string SelectedRoverBattery = "selected-rover-ui.battery";
	public const string ExplorationModeMenu = "selected-rover-ui.exploration-mode";
	public const string StartExplorationButton = "selected-rover-ui.start-exploration";
	public const string DropResourcesButton = "selected-rover-ui.drop-resources";
	public const string ResourcesCarried = "selected-rover-ui.resources-carried";
	public const string AnomalyRadar = "selected-rover-ui.anomaly-radar";
	public const string AnomalyIndicator = "selected-rover-ui.anomaly-indicator";
	public const string PlaceBridgeButton = "selected-rover-ui.place-bridge";
	public const string AddMaterialButton = "game-ui.add-material";
	public const string LiftRobotButton = "selected-robot-ui.lift-robot";
	public const string CustomPathButton = "selected-robot-ui.custom-path";
	public const string ExecutePathButton = "game-ui.execute-path";
	public const string RakePanel = "game-ui.rake";
	public const string AnalyseSampleButton = "selected-robot-ui.analyse-sample";
	public const string PlaceAntennaButton = "selected-robot-ui.place-antenna";
	public const string MonolithFragment = "world.monolith-fragment";
	public const string FragmentManualButton = "fragment-analysis.manual-button";
	public const string ReloadFragmentButton = "fragment-analysis.reload-button";
	public const string FragmentAutonomousButton = "fragment-analysis.autonomous-button";
	public const string FragmentExitButton = "fragment-analysis.exit-button";
	public const string FragmentWorldBearing = "fragment-analysis.world-bearing";
	public const string FragmentCanvas = "fragment-analysis.canvas";
	public const string FragmentProcessingControls = "fragment-analysis.processing-controls";
	public const string FragmentRotationControls = "fragment-analysis.rotation-controls";
}

public sealed class TutorialLevelContext
{
	public Vector2I BasePosition { get; }
	public Vector2I ManualMovementDestination { get; }
	public Vector2I ReturnDestination { get; }
	public Vector2I BaseReturnDestination { get; }
	public Vector2I MonolithPosition { get; }
	public Vector2I FragmentPosition { get; }

	public TutorialLevelContext(
		Vector2I basePosition,
		Vector2I manualMovementDestination,
		Vector2I returnDestination,
		Vector2I baseReturnDestination,
		Vector2I monolithPosition,
		Vector2I fragmentPosition)
	{
		BasePosition = basePosition;
		ManualMovementDestination = manualMovementDestination;
		ReturnDestination = returnDestination;
		BaseReturnDestination = baseReturnDestination;
		MonolithPosition = monolithPosition;
		FragmentPosition = fragmentPosition;
	}
}
