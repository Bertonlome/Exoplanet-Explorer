using Game.Component;
using Godot;

namespace Game.UI.Tutorial.Scripts;

/// <summary>
/// Level 1, checkpoint 2 vertical slice. Later Level 1 checkpoints extend this sequence after rover
/// deployment; this slice intentionally covers implementation.md lines 270-272 only.
/// </summary>
public sealed class Level1Tutorial : TutorialScript
{
	private readonly TutorialLevelContext context;

	public Level1Tutorial(TutorialLevelContext context)
	{
		this.context = context;
	}

		private static bool IsRover(TutorialEventContext eventContext) =>
			eventContext.Subject is BuildingComponent building &&
			building.BuildingResource?.DisplayName == "Rover";

	private static bool RoverCarriesMoreThanFiveWood(TutorialEventContext eventContext)
	{
		if (!IsRover(eventContext) || eventContext.Subject is not BuildingComponent rover) return false;
		return rover.resourceCollected.FindAll(resource => resource == "wood").Count > 5;
	}

	private bool RoverReachedNorthIsland(TutorialEventContext eventContext)
	{
		if (!IsRover(eventContext) || !eventContext.WorldPosition.HasValue) return false;
		Vector2I relativePosition = eventContext.WorldPosition.Value - context.BasePosition;
		bool reachedNorthEastIsland = relativePosition.X > 4 && relativePosition.Y < -9;
		bool reachedNorthWestIsland = relativePosition.X < 0 && relativePosition.Y < 0;
		return reachedNorthEastIsland || reachedNorthWestIsland;
	}

	private bool RoverIsNearMonolith(TutorialEventContext eventContext)
	{
		if (!IsRover(eventContext) || !eventContext.WorldPosition.HasValue) return false;
		Vector2I distance = eventContext.WorldPosition.Value - context.MonolithPosition;
		return Mathf.Abs(distance.X) <= 2 && Mathf.Abs(distance.Y) <= 2;
	}

	public override void Build(TutorialBuilder tutorial)
	{
		tutorial.Step("level1.mission")
			.When(TutorialEvent.LevelReady)
			.Say(
				"LEVEL 1: ROVER MOVEMENT",
				"Your mission is to explore the exoplanet with a ground rover and analyse the monolith. This level starts with a base " +
				"already deployed, so you can focus on learning how the rover works.")
			.HardPause()
			.UntilContinue();

		tutorial.Step("level1.status-panels")
			.Say(
				"MISSION STATUS",
				"This panel tracks the time remaining to conclude the level and the resources stored at your base")
			.PointTo(TutorialTargetIds.StatusPanel)
			.HardPause()
			.UntilContinue();

		tutorial.Step("level1.preplaced-base")
			.Say(
				"BASE ALREADY DEPLOYED",
				"This is your pre-placed base. Robots can be deployed using material, ")
			.PointTo(TutorialTargetIds.PreplacedBase)
			.HardPause()
			.UntilContinue();

		tutorial.Step("level1.choose-rover")
			.Say(
				"DEPLOY A ROVER",
				"Select the Rover.")
			.PointTo(TutorialTargetIds.RoverDeployButton)
			.GuideAction()
			.UntilTargetPressed();

		tutorial.Step("level1.place-rover")
			.Say(
				"PLACE THE ROVER",
				"Move the rover preview to a valid tile near the base, then left-click to deploy it.")
			.GuideAction()
			.UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(
				TutorialEvent.BuildingPlaced,
				context => context.Subject is BuildingComponent building &&
					building.BuildingResource?.DisplayName == "Rover");

		tutorial.Step("level1.rover-ready")
			.Say(
				"CAMERA CONTROLS",
				"Before selecting the rover, scroll to zoom. Pan the camera with the arrow keys, or hold left-click and drag anywhere in the worldview. Then press Continue.")
			.GuideAction()
			.UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.UntilContinue();

		tutorial.Step("level1.select-rover")
			.Say(
				"SELECT THE ROVER",
				"Left-click the deployed rover to select it.")
			.PointTo(TutorialTargetIds.DeployedRover)
			.GuideAction()
			.UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(
				TutorialEvent.RobotSelected,
				IsRover);

		tutorial.Step("level1.manual-controls")
			.Say("MANUAL CONTROL",
				"Use W, A, S, and D keys to move the selected rover one tile at a time: W north, " +
				"A west, S south, and D east.")
			.GuideAction()
			.UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.UntilContinue();

		tutorial.Step("level1.manual-destination")
			.Say("DRIVE TO THE MARKER",
				"Use W, A, S, and D to move the rover onto the highlighted tile.")
			.PointTo(TutorialTargetIds.ManualMovementDestination)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotMoved, eventContext => IsRover(eventContext) &&
				eventContext.WorldPosition == context.ManualMovementDestination);

		tutorial.Step("level1.deployed-unit-battery")
			.Say("MOVEMENT USES BATTERY",
				"Every movement step consumes battery charge. The deployed-units panel shows the rover's remaining battery percentage.")
			.PointTo(TutorialTargetIds.DeployedRoverBattery)
			.HardPause().UntilContinue();

		tutorial.Step("level1.selected-unit-battery")
			.Say("MOVES REMAINING",
				"The selected-rover panel shows the battery value as the number of movement steps remaining.")
			.PointTo(TutorialTargetIds.SelectedRoverBattery)
			.HardPause().UntilContinue();

		tutorial.Step("level1.return-command")
			.Say("SEMI-AUTONOMOUS EXPLORATION",
				"Use right-click to the highlighted tile to gather one wood. The rover will plan and execute an efficient path using A*.")
			.PointTo(TutorialTargetIds.ReturnDestination)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.DirectedMoveRequested, eventContext => IsRover(eventContext) &&
				eventContext.WorldPosition == context.ReturnDestination);

		tutorial.Step("level1.astar-return")
			.Say("MOVEMENT IN PROGRESS",
				"The rover is planning and executing the route. Wait until it automatically gathers one wood.")
			.PointTo(TutorialTargetIds.ReturnDestination)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.ResourceCollected, eventContext => IsRover(eventContext) &&
				eventContext.Payload as string == "wood");

		tutorial.Step("level1.resources-carried")
			.Say("RESOURCES CARRIED",
				"The gathered wood is now carried by the rover. A ground rover can carry up to 8 items in total, wood and minerals.")
			.PointTo(TutorialTargetIds.ResourcesCarried)
			.HardPause()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.UntilContinue();

		tutorial.Step("level1.choose-return-mode")
			.Say("EXPLORATION MODES",
				"Now bring the carried wood to the base using a higher-level exploration mode. Open this menu and choose Return to base.")
			.PointTo(TutorialTargetIds.ExplorationModeMenu)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.ExplorationModeSelected, eventContext => IsRover(eventContext) &&
				eventContext.Payload as string == "ReturnToBase");

		tutorial.Step("level1.start-return-mode")
			.Say("START EXPLORATION",
				"Press Start Exploration. The rover will return to the base autonomously.")
			.PointTo(TutorialTargetIds.StartExplorationButton)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.ExplorationStarted, eventContext => IsRover(eventContext) &&
				eventContext.Payload as string == "ReturnToBase");

		tutorial.Step("level1.autonomous-return")
			.Say("RETURNING TO BASE")
			.PointTo(TutorialTargetIds.PreplacedBase)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotMoved, eventContext => IsRover(eventContext) &&
				eventContext.WorldPosition == context.BaseReturnDestination);

		tutorial.Step("level1.drop-resources")
			.Say("UNLOAD THE WOOD",
				"The rover is back at the base. Press Drop resources in the selected-rover panel to transfer its load to base storage and enable charging.")
			.PointTo(TutorialTargetIds.DropResourcesButton)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.ResourcesDropped, IsRover);

		tutorial.Step("level1.recharging")
			.Say("RECHARGING AT BASE",
				"The wood is now stored at the base and charging has begun. Each recharge consumes stored wood.")
			.PointTo(TutorialTargetIds.SelectedRoverBattery)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.BatteryRecharged, IsRover);

		tutorial.Step("level1.anomaly-radar")
			.Say("ANOMALY RADAR",
				"While exploring this patch of land, use the anomaly radar to compare the surrounding terrain and identify promising directions. The radar is a 3D view: hold left-click over it and drag to rotate it.")
			.PointTo(TutorialTargetIds.AnomalyRadar)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.UntilContinue();

		tutorial.Step("level1.anomaly-indicator")
			.Say("GRAVITATIONAL ANOMALY TREND",
				"This indicator records the rover's current gravitational anomaly reading on the tile and recent trends. Use it to judge whether movement is taking you toward a more promising region.")
			.PointTo(TutorialTargetIds.AnomalyIndicator)
			.HardPause()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.UntilContinue();

		tutorial.Step("level1.gather-bridge-wood")
			.Say("GATHER BRIDGE MATERIAL",
				"Explore the surrounding land and gather enough wood to build a bridge.")
			.PointTo(TutorialTargetIds.ResourcesCarried)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.ResourceCollected, RoverCarriesMoreThanFiveWood);

		tutorial.Step("level1.build-bridge")
			.Say("BRIDGE TO THE NORTH",
				"Build a bridge toward the next island north of here. For manual construction, press Place bridge and choose a valid adjacent tile. Alternatively, right-click a destination on the next island: the rover's planner can include required bridges in its plan.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotMoved, RoverReachedNorthIsland);

		tutorial.Step("level1.movement-complete")
			.Say("FIND THE MONOLITH",
				"You have learned the rover's three control styles. Continue exploring and use the anomaly readings to find the source of the strongest gravitational anomaly: the monolith.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotMoved, RoverIsNearMonolith);

		tutorial.Step("level1.monolith-discovered")
			.Say("THE MONOLITH",
				"This mysterious alien stone may hold secrets of the universe. In every level, your rover must find the monolith and collect a sample from it. Press Continue when you are ready to approach it.")
			.HardPause()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.UntilContinue();

		tutorial.Step("level1.touch-monolith")
			.Say("COLLECT THE SAMPLE",
				"Move the rover onto the monolith to touch it, collect its sample, and complete the level.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.MonolithTouched);
	}
}
