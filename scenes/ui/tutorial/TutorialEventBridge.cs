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
	private Callable fragmentAnalysisRequestedCallable;
	private Callable liftRobotCallable;
	private Callable dropRobotCallable;

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
		robotMovedCallable = Callable.From<BuildingComponent>(
			building => Publish(new TutorialEventContext(TutorialEvent.RobotMoved, building)));
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

		ConnectGameEvent(GameEvents.SignalName.BuildingPlaced, buildingPlacedCallable);
		ConnectGameEvent(GameEvents.SignalName.RobotSelected, robotSelectedCallable);
		ConnectGameEvent(GameEvents.SignalName.BuildingMoved, robotMovedCallable);
		ConnectGameEvent(GameEvents.SignalName.FragmentAnalysisRequested, fragmentAnalysisRequestedCallable);
		ConnectGameEvent(GameEvents.SignalName.LiftRobotButtonPressed, liftRobotCallable);
		ConnectGameEvent(GameEvents.SignalName.DropRobotButtonPressed, dropRobotCallable);
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
			DisconnectGameEvent(GameEvents.SignalName.FragmentAnalysisRequested, fragmentAnalysisRequestedCallable);
			DisconnectGameEvent(GameEvents.SignalName.LiftRobotButtonPressed, liftRobotCallable);
			DisconnectGameEvent(GameEvents.SignalName.DropRobotButtonPressed, dropRobotCallable);
		}
		connectedToGameEvents = false;
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
