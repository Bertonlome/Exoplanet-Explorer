using System.Collections.Generic;
using Game.Component;
using Godot;

namespace Game.UI.Tutorial.Scripts;

/// <summary>
/// Level 2 teaches deployment, mineral conversion, aerial scouting, and cooperative lifting.
/// Every action checkpoint advances from a gameplay outcome rather than explanatory-button input.
/// </summary>
public sealed class Level2Tutorial : TutorialScript
{
	private readonly TutorialLevelContext context;

	public Level2Tutorial(TutorialLevelContext context)
	{
		this.context = context;
	}

	private static bool IsRobot(TutorialEventContext eventContext, string displayName) =>
		eventContext.Subject is BuildingComponent building &&
		building.BuildingResource?.DisplayName == displayName;

	private static bool IsRover(TutorialEventContext eventContext) => IsRobot(eventContext, "Rover");
	private static bool IsDrone(TutorialEventContext eventContext) => IsRobot(eventContext, "Drone");

	private static bool RoverCarriesThreeMineralTypes(TutorialEventContext eventContext)
	{
		if (!IsRover(eventContext) || eventContext.Subject is not BuildingComponent rover) return false;
		HashSet<string> mineralTypes = new();
		foreach (string resource in rover.resourceCollected)
		{
			if (resource is "red_ore" or "green_ore" or "blue_ore") mineralTypes.Add(resource);
		}
		return mineralTypes.Count >= 3;
	}

	private bool LiftedRoverReachedMonolith(TutorialEventContext eventContext)
	{
		if (!IsDrone(eventContext) || eventContext.Subject is not BuildingComponent drone ||
			!drone.IsLifting || !eventContext.WorldPosition.HasValue) return false;
		Vector2I distance = eventContext.WorldPosition.Value - context.MonolithPosition;
		return Mathf.Abs(distance.X) <= 2 && Mathf.Abs(distance.Y) <= 2;
	}

	public override void Build(TutorialBuilder tutorial)
	{
		tutorial.Step("level2.mission")
			.When(TutorialEvent.LevelReady)
			.Say("LEVEL 2: TEAM DEPLOYMENT",
				"This level begins without a base. You will deploy a rover, recover minerals, construct a drone, and combine both robots' capabilities to reach the hilltop monolith.")
			.HardPause().UntilContinue();

		tutorial.Step("level2.choose-base")
			.Say("DEPLOY THE BASE", "Select the Base deployment card.")
			.PointTo(TutorialTargetIds.BaseDeployButton)
			.GuideAction().UntilTargetPressed();

		tutorial.Step("level2.place-base")
			.Say("PLACE THE BASE",
				"Move the base preview onto a valid area and left-click to place it.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.BuildingPlaced, eventContext => IsRobot(eventContext, "Base"));

		tutorial.Step("level2.choose-rover")
			.Say("DEPLOY A ROVER",
				"There is not enough remaining material for a drone yet. Select the Rover card first.")
			.PointTo(TutorialTargetIds.RoverDeployButton)
			.GuideAction().UntilTargetPressed();

		tutorial.Step("level2.place-rover")
			.Say("PLACE THE ROVER", "Place the rover on a valid tile near the base.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.BuildingPlaced, IsRover);

		tutorial.Step("level2.select-rover")
			.Say("SELECT THE ROVER", "Left-click the rover to open its controls.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotSelected, IsRover);

		tutorial.Step("level2.gather-minerals")
			.Say("GATHER THREE MINERALS",
				"Explore and find red, green, and blue ore. All three mineral types are required to create material.")
			.PointTo(TutorialTargetIds.ResourcesCarried)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.ResourceCollected, RoverCarriesThreeMineralTypes);

		tutorial.Step("level2.unload-minerals")
			.Say("UNLOAD THE MINERALS",
				"When the rover reaches the base, press Drop resources to transfer all three minerals into base storage.")
			.PointTo(TutorialTargetIds.DropResourcesButton)
			.GuideAction().UndimBackground()
			.Until(TutorialEvent.ResourcesDropped, IsRover);

		tutorial.Step("level2.create-material")
			.Say("PROCESS THE MINERALS",
				"Press the material-conversion control. One red, green, and blue ore are processed together into two construction material.")
			.PointTo(TutorialTargetIds.AddMaterialButton)
			.GuideAction().UndimBackground()
			.Until(TutorialEvent.MaterialCreated);

		tutorial.Step("level2.choose-drone")
			.Say("CONSTRUCT A DRONE", "The converted material makes a Drone affordable. Select its deployment card.")
			.PointTo(TutorialTargetIds.DroneDeployButton)
			.GuideAction().UntilTargetPressed();

		tutorial.Step("level2.place-drone")
			.Say("PLACE THE DRONE", "Place the drone on a valid tile near the base.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.BuildingPlaced, IsDrone);

		tutorial.Step("level2.select-drone")
			.Say("SELECT THE DRONE", "Left-click the deployed drone to open its aerial controls.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotSelected, IsDrone);

		tutorial.Step("level2.scout-with-drone")
			.Say("AERIAL SCOUTING",
				"Scout more of the map with the drone. Either right-click a destination for a directed move, or choose an exploration mode and press Start Exploration.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.DroneScoutStarted, IsDrone);

		tutorial.Step("level2.capabilities")
			.Say("COMPLEMENTARY ROBOTS",
				"The rover travels through wooded ground but cannot climb steep elevation. The drone can cross elevation and water, but trees obstruct its flight path. Teaming lets each robot overcome the other's limitation.")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotSelected, IsDrone);

		tutorial.Step("level2.position-and-lift")
			.Say("LIFT THE ROVER",
				"Move the drone directly above the rover. When the lift control becomes available, press Lift Robot to attach the rover beneath the drone.")
			.PointTo(TutorialTargetIds.LiftRobotButton)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotLiftRequested, eventContext => IsDrone(eventContext) &&
				eventContext.SecondarySubject is BuildingComponent rover &&
				rover.BuildingResource?.DisplayName == "Rover");

		tutorial.Step("level2.carry-to-monolith")
			.Say("CARRY THE ROVER UPHILL",
				"With the rover attached, direct the drone toward the hilltop monolith. The pair moves together while lifted. BE CAREFUL: The drone's battery depletes fast when lifting!")
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotMoved, LiftedRoverReachedMonolith);

		tutorial.Step("level2.drop-at-monolith")
			.Say("DELIVER THE ROVER",
				"Press Drop Robot near the monolith. The rover can then collect the monolith sample and finish the level.")
			.PointTo(TutorialTargetIds.LiftRobotButton)
			.GuideAction().UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.RobotDropRequested, eventContext => IsDrone(eventContext));
	}
}
