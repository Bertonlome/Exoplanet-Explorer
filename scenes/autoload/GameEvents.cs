using Game.Component;
using Game.Resources.Building;
using Godot;
using Godot.Collections;

namespace Game.Autoload;

public partial class GameEvents : Node
{
	public static GameEvents Instance { get; private set; }

	[Signal]
	public delegate void BuildingPlacedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void FragmentAnalysisRequestedEventHandler(
		Vector2I fragmentPosition,
		BuildingComponent requestingRover,
		int actionOrigin);
	[Signal]
	public delegate void BuildingDestroyedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void BuildingDisabledEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void BuildingEnabledEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void BuildingMovedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void DirectedMoveRequestedEventHandler(
		BuildingComponent buildingComponent,
		Vector2I destination);
	[Signal]
	public delegate void CustomPathRequestedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void BuildingStuckEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void BuildingUnStuckEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void RobotSelectedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void NoMoreRobotSelectedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void PlaceBridgeButtonPressedEventHandler(BuildingComponent buildingComponent, BuildingResource buildingResource);
	[Signal]
	public delegate void PlaceAntennaButtonPressedEventHandler(BuildingComponent buildingComponent, BuildingResource buildingResource);
	[Signal]
	public delegate void LiftRobotButtonPressedEventHandler(BuildingComponent buildingComponent, BuildingComponent groundRobot);
	[Signal]
	public delegate void DropRobotButtonPressedEventHandler(BuildingComponent buildingComponent, BuildingComponent groundRobot);
	[Signal]
	public delegate void GroundRobotBelowUavEventHandler(BuildingComponent groundRobot);
	[Signal]
	public delegate void NoGroundRobotBelowUavEventHandler();
	[Signal]
	public delegate void AllRobotStoppedEventHandler();
	[Signal]
	public delegate void CarriedResourceCountChangedEventHandler(int carriedResourceCount);
	[Signal]
	public delegate void RobotBackToIdleEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void ResourceCollectedEventHandler(BuildingComponent buildingComponent, string resourceType);
	[Signal]
	public delegate void ExplorationModeSelectedEventHandler(BuildingComponent buildingComponent, string mode);
	[Signal]
	public delegate void ExplorationStartedEventHandler(BuildingComponent buildingComponent, string mode);
	[Signal]
	public delegate void ResourcesDroppedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void MaterialCreatedEventHandler();
	[Signal]
	public delegate void CustomPathExecutedEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void RobotOutOfAntennaCoverageEventHandler(BuildingComponent buildingComponent);
	[Signal]
	public delegate void FragmentModeSelectedEventHandler(int mode);
	[Signal]
	public delegate void FragmentReloadedEventHandler();

	public override void _Notification(int what)
	{
		if (what == NotificationSceneInstantiated)
		{
			Instance = this;
		}
	}

	public static void EmitBuildingPlaced(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingPlaced, buildingComponent);
	}

	public static void EmitFragmentAnalysisRequested(
		Vector2I fragmentPosition,
		BuildingComponent requestingRover,
		FragmentAnalysisActionOrigin actionOrigin)
	{
		Instance.EmitSignal(
			SignalName.FragmentAnalysisRequested,
			fragmentPosition,
			requestingRover,
			(int)actionOrigin);
	}

	public static void EmitBuildingMoved(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingMoved, buildingComponent);
	}

	public static void EmitDirectedMoveRequested(
		BuildingComponent buildingComponent,
		Vector2I destination)
	{
		Instance.EmitSignal(SignalName.DirectedMoveRequested, buildingComponent, destination);
	}

	public static void EmitCustomPathRequested(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.CustomPathRequested, buildingComponent);
	}

	public static void EmitResourceCollected(BuildingComponent buildingComponent, string resourceType) =>
		Instance.EmitSignal(SignalName.ResourceCollected, buildingComponent, resourceType);

	public static void EmitExplorationModeSelected(BuildingComponent buildingComponent, string mode) =>
		Instance.EmitSignal(SignalName.ExplorationModeSelected, buildingComponent, mode);

	public static void EmitExplorationStarted(BuildingComponent buildingComponent, string mode) =>
		Instance.EmitSignal(SignalName.ExplorationStarted, buildingComponent, mode);

	public static void EmitResourcesDropped(BuildingComponent buildingComponent) =>
		Instance.EmitSignal(SignalName.ResourcesDropped, buildingComponent);

	public static void EmitMaterialCreated() =>
		Instance.EmitSignal(SignalName.MaterialCreated);

	public static void EmitCustomPathExecuted(BuildingComponent buildingComponent) =>
		Instance.EmitSignal(SignalName.CustomPathExecuted, buildingComponent);

	public static void EmitRobotOutOfAntennaCoverage(BuildingComponent buildingComponent) =>
		Instance.EmitSignal(SignalName.RobotOutOfAntennaCoverage, buildingComponent);

	public static void EmitFragmentModeSelected(int mode) =>
		Instance.EmitSignal(SignalName.FragmentModeSelected, mode);

	public static void EmitFragmentReloaded() =>
		Instance.EmitSignal(SignalName.FragmentReloaded);

	public static void EmitRobotSelected(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.RobotSelected, buildingComponent);
	}

	public static void EmitNoMoreRobotSelected(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.NoMoreRobotSelected, buildingComponent);
	}

	public static void EmitBuildingDestroyed(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingDestroyed, buildingComponent);
	}

	public static void EmitBuildingDisabled(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingDisabled, buildingComponent);
	}

	public static void EmitBuildingEnabled(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingEnabled, buildingComponent);
	}

	public static void EmitBuildingStuck(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingStuck, buildingComponent);
	}

	public static void EmitBuildingUnStuck(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.BuildingUnStuck, buildingComponent);
	}

	public static void EmitAllRobotStop()
	{
		Instance.EmitSignal(SignalName.AllRobotStopped);
	}

	public static void EmitCarriedResourceCountChanged(int carriedResourceCount)
	{
		Instance.EmitSignal(SignalName.CarriedResourceCountChanged, carriedResourceCount);
	}

	public static void EmitPlaceBridgeButtonPressed(BuildingComponent buildingComponent, BuildingResource buildingResource)
	{
		Instance.EmitSignal(SignalName.PlaceBridgeButtonPressed, buildingComponent, buildingResource);
	}

	public static void EmitPlaceAntennaButtonPressed(BuildingComponent buildingComponent, BuildingResource buildingResource)
	{
		Instance.EmitSignal(SignalName.PlaceAntennaButtonPressed, buildingComponent, buildingResource);
	}

	public static void EmitLiftRobotButtonPressed(BuildingComponent buildingComponent, BuildingComponent groundRobot)
	{
		Instance.EmitSignal(SignalName.LiftRobotButtonPressed, buildingComponent, groundRobot);
	}

	public static void EmitDropRobotButtonPressed(BuildingComponent buildingComponent, BuildingComponent groundRobot)
	{
		Instance.EmitSignal(SignalName.DropRobotButtonPressed, buildingComponent, groundRobot);
	}

	public static void EmitGroundRobotBelowUav(BuildingComponent groundRobot)
	{
		Instance.EmitSignal(SignalName.GroundRobotBelowUav, groundRobot);
	}

	public static void EmitNoGroundRobotBelowUav()
	{
		Instance.EmitSignal(SignalName.NoGroundRobotBelowUav);
	}

	public static void EmitRobotBackToIdle(BuildingComponent buildingComponent)
	{
		Instance.EmitSignal(SignalName.RobotBackToIdle, buildingComponent);
	}
}
