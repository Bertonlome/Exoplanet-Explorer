using Game.Component;

namespace Game.UI.Tutorial.Scripts;

/// <summary>
/// Level 1, checkpoint 2 vertical slice. Later Level 1 checkpoints extend this sequence after rover
/// deployment; this slice intentionally covers implementation.md lines 270-272 only.
/// </summary>
public sealed class Level1Tutorial : TutorialScript
{
	public override void Build(TutorialBuilder tutorial)
	{
		tutorial.Step("level1.mission")
			.When(TutorialEvent.LevelReady)
			.Say(
				"LEVEL 1: ROVER MOVEMENT",
				"Your mission is to explore the island with a ground rover. This level starts with a base " +
				"already deployed, so you can focus on learning how the rover works.")
			.HardPause()
			.UntilContinue();

		tutorial.Step("level1.status-panels")
			.Say(
				"MISSION STATUS",
				"This panel tracks the time remaining and the resources stored at your base. Materials are " +
				"spent to deploy robots; gathered wood will appear here after it is returned to base.")
			.PointTo(TutorialTargetIds.StatusPanel)
			.HardPause()
			.UntilContinue();

		tutorial.Step("level1.preplaced-base")
			.Say(
				"BASE ALREADY ONLINE",
				"This is your pre-placed base. You do not need to place another one. Robots deploy nearby, " +
				"and later they can return here to unload resources and recharge.")
			.PointTo(TutorialTargetIds.PreplacedBase)
			.HardPause()
			.UntilContinue();

		tutorial.Step("level1.choose-rover")
			.Say(
				"DEPLOY A ROVER",
				"Select the Rover deployment card.")
			.PointTo(TutorialTargetIds.RoverDeployButton)
			.GuideAction()
			.UntilTargetPressed();

		tutorial.Step("level1.place-rover")
			.Say(
				"PLACE THE ROVER",
				"Move the rover preview to a valid tile near the base, then left-click to deploy it. The " +
				"tutorial advances only after a Rover is successfully placed.")
			.GuideAction()
			.UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(
				TutorialEvent.BuildingPlaced,
				context => context.Subject is BuildingComponent building &&
					building.BuildingResource?.DisplayName == "Rover");

		tutorial.Step("level1.rover-ready")
			.Say(
				"ROVER READY",
				"The rover is deployed. The next Level 1 checkpoint will teach direct movement and bridge " +
				"building.")
			.HardPause()
			.UntilContinue();
	}
}
