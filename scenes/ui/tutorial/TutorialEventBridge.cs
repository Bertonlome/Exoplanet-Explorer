using System;
using System.Collections.Generic;
using Game.Autoload;
using Game.Component;
using Godot;

namespace Game.UI.Tutorial;

/// <summary>
/// Converts durable gameplay signals and local tutorial publications into one semantic stream.
/// It is level-scoped and explicitly started/stopped so scene changes cannot retain subscriptions.
/// </summary>
public partial class TutorialEventBridge : Node
{
	public event Action<TutorialEventContext> EventPublished;

	private readonly Dictionary<TutorialEvent, TutorialEventContext> latestEvents = new();
	private bool started;
	private bool connectedToGameEvents;
	private Callable buildingPlacedCallable;
	private Callable robotSelectedCallable;
	private Callable robotMovedCallable;
	private Callable directedMoveCallable;
	private Callable fragmentAnalysisRequestedCallable;
	private Callable liftRobotCallable;
	private Callable dropRobotCallable;
	private Callable resourceCollectedCallable;
	private Callable explorationModeSelectedCallable;
	private Callable explorationStartedCallable;
	private Callable robotBackToIdleCallable;
	private Callable resourcesDroppedCallable;
	private Callable materialCreatedCallable;
	private Callable customPathRequestedCallable;
	private Callable customPathExecutedCallable;
	private Callable robotOutOfAntennaCoverageCallable;
	private Callable fragmentModeSelectedCallable;
	private Callable fragmentReloadedCallable;
	private readonly Dictionary<BuildingComponent, List<Callable>> robotSignalCallables = new();
	private readonly Dictionary<BuildingComponent, int> lastBatteryValues = new();

	public void Start()
	{
		if (started)
		{
			return;
		}

		started = true;
		if (GameEvents.Instance == null)
		{
			GD.PushWarning("TutorialEventBridge started before GameEvents was available.");
			return;
		}

		buildingPlacedCallable = Callable.From<BuildingComponent>(OnBuildingPlaced);
		robotSelectedCallable = Callable.From<BuildingComponent>(
			building => Publish(new TutorialEventContext(TutorialEvent.RobotSelected, building)));
		robotMovedCallable = Callable.From<BuildingComponent>(building => Publish(new TutorialEventContext(
			TutorialEvent.RobotMoved,
			building,
			worldPosition: building?.GetGridCellPosition())));
		directedMoveCallable = Callable.From<BuildingComponent, Vector2I>(OnDirectedMoveRequested);
		fragmentAnalysisRequestedCallable = Callable.From<Vector2I, BuildingComponent, int>(
			(position, rover, origin) => Publish(new TutorialEventContext(
				TutorialEvent.FragmentAnalysisRequested,
				rover,
				payload: origin,
				worldPosition: position)));
		liftRobotCallable = Callable.From<BuildingComponent, BuildingComponent>(
			(aerialRobot, groundRobot) => Publish(new TutorialEventContext(
				TutorialEvent.RobotLiftRequested,
				aerialRobot,
				groundRobot)));
		dropRobotCallable = Callable.From<BuildingComponent, BuildingComponent>(
			(aerialRobot, groundRobot) => Publish(new TutorialEventContext(
				TutorialEvent.RobotDropRequested,
				aerialRobot,
				groundRobot)));
		resourceCollectedCallable = Callable.From<BuildingComponent, string>((building, resourceType) =>
			Publish(new TutorialEventContext(TutorialEvent.ResourceCollected, building, payload: resourceType)));
		explorationModeSelectedCallable = Callable.From<BuildingComponent, string>((building, mode) =>
			Publish(new TutorialEventContext(TutorialEvent.ExplorationModeSelected, building, payload: mode)));
		explorationStartedCallable = Callable.From<BuildingComponent, string>(OnExplorationStarted);
		robotBackToIdleCallable = Callable.From<BuildingComponent>(building =>
			Publish(new TutorialEventContext(TutorialEvent.ExplorationStopped, building)));
		resourcesDroppedCallable = Callable.From<BuildingComponent>(building =>
			Publish(new TutorialEventContext(TutorialEvent.ResourcesDropped, building)));
		materialCreatedCallable = Callable.From(() => Publish(TutorialEvent.MaterialCreated));
		customPathRequestedCallable = Callable.From<BuildingComponent>(building =>
			Publish(new TutorialEventContext(TutorialEvent.CustomPathStarted, building)));
		customPathExecutedCallable = Callable.From<BuildingComponent>(building =>
			Publish(new TutorialEventContext(TutorialEvent.CustomPathExecuted, building)));
		robotOutOfAntennaCoverageCallable = Callable.From<BuildingComponent>(building =>
			Publish(new TutorialEventContext(
				TutorialEvent.RobotOutOfAntennaCoverage,
				building,
				worldPosition: building?.GetGridCellPosition())));
		fragmentModeSelectedCallable = Callable.From<int>(mode =>
			Publish(new TutorialEventContext(TutorialEvent.FragmentModeSelected, payload: mode)));
		fragmentReloadedCallable = Callable.From(() => Publish(TutorialEvent.FragmentReloaded));

		ConnectGameEvent(GameEvents.SignalName.BuildingPlaced, buildingPlacedCallable);
		ConnectGameEvent(GameEvents.SignalName.RobotSelected, robotSelectedCallable);
		ConnectGameEvent(GameEvents.SignalName.BuildingMoved, robotMovedCallable);
		ConnectGameEvent(GameEvents.SignalName.DirectedMoveRequested, directedMoveCallable);
		ConnectGameEvent(GameEvents.SignalName.FragmentAnalysisRequested, fragmentAnalysisRequestedCallable);
		ConnectGameEvent(GameEvents.SignalName.LiftRobotButtonPressed, liftRobotCallable);
		ConnectGameEvent(GameEvents.SignalName.DropRobotButtonPressed, dropRobotCallable);
		ConnectGameEvent(GameEvents.SignalName.ResourceCollected, resourceCollectedCallable);
		ConnectGameEvent(GameEvents.SignalName.ExplorationModeSelected, explorationModeSelectedCallable);
		ConnectGameEvent(GameEvents.SignalName.ExplorationStarted, explorationStartedCallable);
		ConnectGameEvent(GameEvents.SignalName.RobotBackToIdle, robotBackToIdleCallable);
		ConnectGameEvent(GameEvents.SignalName.ResourcesDropped, resourcesDroppedCallable);
		ConnectGameEvent(GameEvents.SignalName.MaterialCreated, materialCreatedCallable);
		ConnectGameEvent(GameEvents.SignalName.CustomPathRequested, customPathRequestedCallable);
		ConnectGameEvent(GameEvents.SignalName.CustomPathExecuted, customPathExecutedCallable);
		ConnectGameEvent(GameEvents.SignalName.RobotOutOfAntennaCoverage, robotOutOfAntennaCoverageCallable);
		ConnectGameEvent(GameEvents.SignalName.FragmentModeSelected, fragmentModeSelectedCallable);
		ConnectGameEvent(GameEvents.SignalName.FragmentReloaded, fragmentReloadedCallable);
		connectedToGameEvents = true;
	}

	public void Stop()
	{
		if (!started)
		{
			return;
		}

		started = false;
		if (connectedToGameEvents && GameEvents.Instance != null)
		{
			DisconnectGameEvent(GameEvents.SignalName.BuildingPlaced, buildingPlacedCallable);
			DisconnectGameEvent(GameEvents.SignalName.RobotSelected, robotSelectedCallable);
			DisconnectGameEvent(GameEvents.SignalName.BuildingMoved, robotMovedCallable);
			DisconnectGameEvent(GameEvents.SignalName.DirectedMoveRequested, directedMoveCallable);
			DisconnectGameEvent(GameEvents.SignalName.FragmentAnalysisRequested, fragmentAnalysisRequestedCallable);
			DisconnectGameEvent(GameEvents.SignalName.LiftRobotButtonPressed, liftRobotCallable);
			DisconnectGameEvent(GameEvents.SignalName.DropRobotButtonPressed, dropRobotCallable);
			DisconnectGameEvent(GameEvents.SignalName.ResourceCollected, resourceCollectedCallable);
			DisconnectGameEvent(GameEvents.SignalName.ExplorationModeSelected, explorationModeSelectedCallable);
			DisconnectGameEvent(GameEvents.SignalName.ExplorationStarted, explorationStartedCallable);
			DisconnectGameEvent(GameEvents.SignalName.RobotBackToIdle, robotBackToIdleCallable);
			DisconnectGameEvent(GameEvents.SignalName.ResourcesDropped, resourcesDroppedCallable);
			DisconnectGameEvent(GameEvents.SignalName.MaterialCreated, materialCreatedCallable);
			DisconnectGameEvent(GameEvents.SignalName.CustomPathRequested, customPathRequestedCallable);
			DisconnectGameEvent(GameEvents.SignalName.CustomPathExecuted, customPathExecutedCallable);
			DisconnectGameEvent(GameEvents.SignalName.RobotOutOfAntennaCoverage, robotOutOfAntennaCoverageCallable);
			DisconnectGameEvent(GameEvents.SignalName.FragmentModeSelected, fragmentModeSelectedCallable);
			DisconnectGameEvent(GameEvents.SignalName.FragmentReloaded, fragmentReloadedCallable);
		}
		connectedToGameEvents = false;
		foreach ((BuildingComponent robot, List<Callable> callables) in robotSignalCallables)
		{
			if (!IsInstanceValid(robot)) continue;
			robot.Disconnect(BuildingComponent.SignalName.BatteryChange, callables[0]);
			robot.Disconnect(BuildingComponent.SignalName.StartCharging, callables[1]);
		}
		robotSignalCallables.Clear();
		lastBatteryValues.Clear();
		latestEvents.Clear();
	}

	public void Publish(TutorialEvent tutorialEvent, object payload = null)
	{
		Publish(new TutorialEventContext(tutorialEvent, payload: payload));
	}

	public void Publish(TutorialEventContext context)
	{
		if (context == null || context.Event == TutorialEvent.None)
		{
			return;
		}

		latestEvents[context.Event] = context;
		EventPublished?.Invoke(context);
	}

	public bool WasPublished(
		TutorialEvent tutorialEvent,
		Func<TutorialEventContext, bool> predicate = null)
	{
		return latestEvents.TryGetValue(tutorialEvent, out TutorialEventContext context) &&
			(predicate == null || predicate(context));
	}

	public override void _ExitTree()
	{
		Stop();
		EventPublished = null;
	}

	private void OnBuildingPlaced(BuildingComponent building)
	{
		AttachRobotSignals(building);
		Publish(new TutorialEventContext(TutorialEvent.BuildingPlaced, building));
		string displayName = building?.BuildingResource?.DisplayName;
		if (displayName == "Bridge")
		{
			Publish(new TutorialEventContext(TutorialEvent.BridgePlaced, building));
		}
		else if (displayName == "Antenna")
		{
			Publish(new TutorialEventContext(TutorialEvent.AntennaPlaced, building));
		}
	}

	private void OnDirectedMoveRequested(BuildingComponent building, Vector2I destination)
	{
		Publish(new TutorialEventContext(
			TutorialEvent.DirectedMoveRequested,
			building,
			worldPosition: destination));
		if (building?.BuildingResource?.DisplayName == "Drone")
		{
			Publish(new TutorialEventContext(
				TutorialEvent.DroneScoutStarted,
				building,
				payload: "DirectedMove",
				worldPosition: destination));
		}
	}

	private void OnExplorationStarted(BuildingComponent building, string mode)
	{
		Publish(new TutorialEventContext(TutorialEvent.ExplorationStarted, building, payload: mode));
		if (building?.BuildingResource?.DisplayName == "Drone")
		{
			Publish(new TutorialEventContext(TutorialEvent.DroneScoutStarted, building, payload: mode));
		}
	}

	private void AttachRobotSignals(BuildingComponent building)
	{
		if (!IsInstanceValid(building) || building.BuildingResource == null ||
			building.BuildingResource.IsBase || robotSignalCallables.ContainsKey(building)) return;

		lastBatteryValues[building] = building.Battery;
		Callable batteryCallable = Callable.From<int>(value =>
		{
			int previous = lastBatteryValues.TryGetValue(building, out int oldValue) ? oldValue : value;
			lastBatteryValues[building] = value;
			if (value == previous) return;
			Publish(new TutorialEventContext(
				value > previous ? TutorialEvent.BatteryRecharged : TutorialEvent.BatteryDecreased,
				building,
				payload: value,
				worldPosition: building.GetGridCellPosition()));
		});
		Callable chargingCallable = Callable.From(() => Publish(new TutorialEventContext(
			TutorialEvent.ChargingStarted,
			building,
			worldPosition: building.GetGridCellPosition())));
		building.Connect(BuildingComponent.SignalName.BatteryChange, batteryCallable);
		building.Connect(BuildingComponent.SignalName.StartCharging, chargingCallable);
		robotSignalCallables[building] = new List<Callable> { batteryCallable, chargingCallable };
	}

	private static void ConnectGameEvent(StringName signalName, Callable callable)
	{
		if (!GameEvents.Instance.IsConnected(signalName, callable))
		{
			GameEvents.Instance.Connect(signalName, callable);
		}
	}

	private static void DisconnectGameEvent(StringName signalName, Callable callable)
	{
		if (!GameEvents.Instance.IsConnected(signalName, callable))
		{
			return;
		}
		GameEvents.Instance.Disconnect(signalName, callable);
	}
}
