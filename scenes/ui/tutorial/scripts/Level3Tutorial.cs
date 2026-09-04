using Game.Component;

namespace Game.UI.Tutorial.Scripts;

/// <summary>Level 3 introduces safe custom paths and the two-pass fragment workflow.</summary>
public sealed class Level3Tutorial : TutorialScript
{
	private readonly TutorialLevelContext context;

	public Level3Tutorial(TutorialLevelContext context) => this.context = context;

	private static bool IsRover(TutorialEventContext eventContext) =>
		eventContext.Subject is BuildingComponent building &&
		building.BuildingResource?.DisplayName == "Rover";

	public override void Build(TutorialBuilder tutorial)
	{
		tutorial.Step("level3.mission").When(TutorialEvent.LevelReady)
			.Say("LEVEL 3: FRAGMENT ANALYSIS",
				"Anomaly radar data is unavailable here. Locate the monolith by recovering and analysing its fragment, while planning safe routes through the canyon.")
			.HardPause().UntilContinue();

		tutorial.Step("level3.choose-rover")
			.Say("DEPLOY A ROVER", "Select the Rover deployment card.")
			.PointTo(TutorialTargetIds.RoverDeployButton).GuideAction().UntilTargetPressed();

		tutorial.Step("level3.place-rover")
			.Say("PLACE IN THE CANYON", "Place the rover on a valid tile in the canyon to the right of the base.")
			.GuideAction().UndimBackground().PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.BuildingPlaced, IsRover);

		tutorial.Step("level3.select-rover")
			.Say("SELECT THE ROVER", "Left-click the rover to open its controls.")
			.GuideAction().PlaceCallout(TutorialCalloutPlacement.TopRight).UndimBackground().Until(TutorialEvent.RobotSelected, IsRover);

		tutorial.Step("level3.mud-risk")
			.Say("MUD AND RECOVERY",
				"A rover can slip on mud and become stuck. A nearby drone can recover a stuck rover, so avoid risky mud when a recovery drone is unavailable.")
			.HardPause().UntilContinue();

		tutorial.Step("level3.fragment-target")
			.Say("MONOLITH FRAGMENT",
				"This fragment contains directional evidence leading to the monolith. Use a custom path to bring the rover within analysis range.")
			.PointTo(TutorialTargetIds.MonolithFragment).HardPause().UntilContinue();

		tutorial.Step("level3.open-custom-path")
			.Say("CUSTOM PATH MODE", "Press Custom Path in the selected-rover controls.")
			.PointTo(TutorialTargetIds.CustomPathButton).GuideAction().UndimBackground()
			.Until(TutorialEvent.CustomPathStarted, IsRover);

		tutorial.Step("level3.paint-path")
			.Say("PAINT OR CONNECT",
				"Freehand-paint individual path tiles, or specify a destination and let the planner connect departure and arrival efficiently. Build a route toward the highlighted fragment.")
			.PointTo(TutorialTargetIds.MonolithFragment).GuideAction().PlaceCallout(TutorialCalloutPlacement.TopRight).UndimBackground().UntilContinue();

		tutorial.Step("level3.avoid-path")
			.Say("AVOID UNSAFE TILES",
				"Right-click a painted tile to mark it as avoided—for example, a mud tile. The route planner will reconnect around that constraint.")
			.GuideAction().UndimBackground().PlaceCallout(TutorialCalloutPlacement.TopRight).UntilContinue();

		tutorial.Step("level3.rake")
			.Say("RAKE TOOL",
				"The rake offers a spatial alternative: drag it from this panel and use it to push path tiles away from mud, reshaping the route while preserving its intent.")
			.PointTo(TutorialTargetIds.RakePanel).GuideAction().UndimBackground().PlaceCallout(TutorialCalloutPlacement.TopRight).UntilContinue();

		tutorial.Step("level3.execute-path")
			.Say("EXECUTE THE PATH", "Press Execute Path when the route reaches the fragment's analysis range.")
			.PointTo(TutorialTargetIds.ExecutePathButton).GuideAction().UndimBackground()
			.Until(TutorialEvent.CustomPathExecuted, IsRover);

		tutorial.Step("level3.analyse-sample")
			.Say("ANALYSE SAMPLE",
				"When the rover arrives within range, press Analyse Sample in its selected-robot controls.")
			.PointTo(TutorialTargetIds.AnalyseSampleButton).GuideAction().UndimBackground().PlaceCallout(TutorialCalloutPlacement.TopRight)
			.Until(TutorialEvent.FragmentAnalysisOpened, IsRover);

		tutorial.Step("level3.choose-manual")
			.Say("MANUAL ANALYSIS",
				"Choose Manual in the analysis-mode prompt. You will reveal and interpret the sample without the help of the rover.")
			.PointTo(TutorialTargetIds.FragmentManualButton).GuideAction()
			.Until(TutorialEvent.FragmentModeSelected, eventContext => eventContext.Payload is int mode && mode == 0);

		tutorial.Step("level3.sample-lens")
			.Say("THE SAMPLE",
				"This area is a magnified view of the monolith fragment through the rover's sample lenses. We are searching its mineral structure for meaningful information.")
			.PointTo(TutorialTargetIds.FragmentCanvas).HardPause().UntilContinue();

		tutorial.Step("level3.processing-introduction")
			.Say("PROCESSING CHANNELS AND FILTERS",
				"Each channel observes a different property of the sample. Tune the polarization, spectral, and surface filters with their individual level controls. Then Toggle the electromagnetic, resonance, and X-ray channels independently, The right combination suppresses mineral noise and reveals the hidden symbol.")
			.PointTo(TutorialTargetIds.FragmentProcessingControls).HardPause().UntilContinue();

		tutorial.Step("level3.reveal-glyph")
			.Say("REVEAL THE GLYPH",
				"Adjust the processing channels and filter levels until the glyph is fully visible. The tutorial will continue when the analyser detects the complete signal.")
			.GuideAction()
			.UndimBackground()
			.PlaceCallout(TutorialCalloutPlacement.TopLeft)
			.Until(TutorialEvent.FragmentGlyphRevealed);

		tutorial.Step("level3.rotate-glyph")
			.Say("FIND THE UPRIGHT ORIENTATION",
				"Now use the rotation controls to turn the revealed glyph upright. Its orientation provides the reference needed to interpret the encoded arrow correctly.")
			.PointTo(TutorialTargetIds.FragmentRotationControls).GuideAction()
			.UndimBackground()
			.Until(TutorialEvent.FragmentGlyphUpright);

		tutorial.Step("level3.read-bearing")
			.Say("UNDERSTAND THE MONOLITH LOCATION",
				"With the glyph upright, the revealed arrow indicates the location of the monolith on the map. In this case the monolith is located to the south-south-west of the fragment.")
			.PointTo(TutorialTargetIds.FragmentCanvas).HardPause().UntilContinue();

		tutorial.Step("level3.manual-workflow")
			.Say("MANUAL ANALYSIS COMPLETED",
				"We will now perform the analysis with the assistance of the rover in autonomous mode. Press Reload and confirm.")
			.GuideAction().UndimBackground().PointTo(TutorialTargetIds.ReloadFragmentButton)
			.Until(TutorialEvent.FragmentReloaded);

		tutorial.Step("level3.choose-autonomous")
			.Say("ROVER AUTONOMOUS MODE",
				"For the new fragment puzzle, choose Rover autonomous mode. Observe the same analysis stages while the rover allocates and executes the work.")
			.PointTo(TutorialTargetIds.FragmentAutonomousButton).GuideAction()
			.Until(TutorialEvent.FragmentModeSelected, eventContext => eventContext.Payload is int mode && mode == 2);

		tutorial.Step("level3.autonomous-workflow")
			.Say("HUMAN-ROVER TEAM",
				"Follow the rover through sensing, orientation, signal regions, reconstruction, and its final directional result.")
			.GuideAction().UndimBackground().PlaceCallout(TutorialCalloutPlacement.TopLeft)
			.Until(TutorialEvent.FragmentAnalysisCompleted);

		tutorial.Step("level3.bearing-added")
			.Say("BEARING ADDED TO THE MINIMAP",
				"The completed analysis has converted the fragment's arrow into this world/grid bearing and added the same direction to the world minimap.")
			.PointTo(TutorialTargetIds.FragmentWorldBearing).HardPause()
			.PlaceCallout(TutorialCalloutPlacement.TopLeft)
			.UntilContinue();

		tutorial.Step("level3.exit-analysis")
			.Say("RETURN TO THE WORLD",
				"Press Quit to exit the sample analyser and follow the bearing on the map.")
			.PointTo(TutorialTargetIds.FragmentExitButton).GuideAction()
			.PlaceCallout(TutorialCalloutPlacement.TopLeft)
			.Until(TutorialEvent.FragmentAnalysisExited);

		tutorial.Step("level3.use-result")
			.Say("FOLLOW THE RESULT",
				"Follow the direction indicated by the fragment bearing on the minimap to guide the rover toward the monolith.")
			.PointTo(TutorialTargetIds.MinimapContainer).HardPause().UntilContinue();

		tutorial.Step("level3.communication")
			.When(TutorialEvent.RobotOutOfAntennaCoverage)
			.Say("COMMUNICATION COVERAGE",
				"Robots can only receive commands inside communication coverage. Place antennas to extend the base network; nearby robots can also form a chain that carries coverage farther.")
			.PointTo(TutorialTargetIds.PlaceAntennaButton)
			.GuideAction().UndimBackground()
			.Until(TutorialEvent.AntennaPlaced);
	}
}
