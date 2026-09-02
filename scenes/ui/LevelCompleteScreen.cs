using System;
using Game.Autoload;
using Game.Manager;
using Game.Resources.Level;
using Game.UI.Tutorial;
using Godot;

namespace Game.UI;

public partial class LevelCompleteScreen : CanvasLayer
{
	private Button returnToMenuButton;
	private Label timeCompleteLabel;
	private int timeElapsed;
	private Label mineralsAnalyzedLabel;
	[Export(PropertyHint.File, "*.tscn")]
	private string mainMenuScenePath;
	private BuildingManager buildingManager;
	private ConfirmationDialog tutorialChoiceDialog;
	private Button regularModeButton;
	private int pendingNextLevelIndex = -1;

	public override void _Ready()
	{
		returnToMenuButton = GetNode<Button>("%NextLevelButton");
		timeCompleteLabel = GetNode<Label>("%TimeCompleteLabel");
		mineralsAnalyzedLabel = GetNode<Label>("%MineralsAnalyzedLabel");
		// Attempt to get the BuildingManager node from BaseLevel
		buildingManager = GetParent<BaseLevel>().GetFirstNodeOfType<BuildingManager>();

		if (buildingManager != null)
		{
			GD.Print($"First child of the root: {buildingManager.Name}");
			int mineralsAnalyzed = buildingManager.mineralAnalyzedCount;
			mineralsAnalyzedLabel.Text = $"Minerals Analyzed: {mineralsAnalyzed} /3";

		}
		else
		{
			GD.PushError("BuildingManager node not found.");
		}

		AudioHelpers.PlayVictory();

		if(LevelManager.IsLastLevel())
		{
			returnToMenuButton.Text = "Return to Menu";
		}

		returnToMenuButton.Pressed += OnNextLevelButtonPressed;

	}

	private void OnNextLevelButtonPressed()
	{
		if(!LevelManager.IsLastLevel())
		{
			OpenNextLevel();
		}
		else
		{
			GetTree().ChangeSceneToFile(mainMenuScenePath);
		}
	}

	private void OpenNextLevel()
	{
		int nextLevelIndex = LevelManager.CurrentLevelIndex + 1;
		LevelDefinitionResource[] levels = LevelManager.GetLevelDefinitions();
		if (nextLevelIndex < 0 || nextLevelIndex >= levels.Length) return;

		LevelDefinitionResource nextLevel = levels[nextLevelIndex];
		if (!LevelManager.IsTutorialModeActive || !TutorialCatalog.HasTutorial(nextLevel.Id))
		{
			LevelManager.ChangeToLevel(nextLevelIndex, tutorialMode: false);
			return;
		}

		if (!SaveManager.HasTutorialStarted(nextLevel.Id))
		{
			StartTutorial(nextLevelIndex, nextLevel.Id);
			return;
		}

		pendingNextLevelIndex = nextLevelIndex;
		CreateTutorialChoiceDialog(nextLevelIndex);
		tutorialChoiceDialog.PopupCentered();
	}

	private void CreateTutorialChoiceDialog(int nextLevelIndex)
	{
		if (IsInstanceValid(tutorialChoiceDialog)) return;

		tutorialChoiceDialog = new ConfirmationDialog
		{
			Name = "NextLevelTutorialChoiceDialog",
			Title = $"Level {nextLevelIndex + 1} Tutorial",
			DialogText = "You have already started this tutorial. Continue in tutorial mode, or play the level without guidance?",
			OkButtonText = "CONTINUE TUTORIAL",
			Exclusive = true,
		};
		AddChild(tutorialChoiceDialog);
		tutorialChoiceDialog.GetCancelButton().Text = "CANCEL";
		regularModeButton = tutorialChoiceDialog.AddButton("PLAY WITHOUT TUTORIAL");
		tutorialChoiceDialog.Confirmed += OnContinueTutorialConfirmed;
		tutorialChoiceDialog.Canceled += OnTutorialChoiceCanceled;
		regularModeButton.Pressed += OnRegularModePressed;
		AudioHelpers.RegisterButtons(new Button[]
		{
			tutorialChoiceDialog.GetOkButton(),
			tutorialChoiceDialog.GetCancelButton(),
			regularModeButton,
		});
	}

	private void OnContinueTutorialConfirmed()
	{
		int nextLevelIndex = ConsumePendingNextLevelIndex();
		if (nextLevelIndex < 0) return;
		LevelDefinitionResource[] levels = LevelManager.GetLevelDefinitions();
		StartTutorial(nextLevelIndex, levels[nextLevelIndex].Id);
	}

	private void OnRegularModePressed()
	{
		int nextLevelIndex = ConsumePendingNextLevelIndex();
		tutorialChoiceDialog.Hide();
		if (nextLevelIndex >= 0)
		{
			LevelManager.ChangeToLevel(nextLevelIndex, tutorialMode: false);
		}
	}

	private void OnTutorialChoiceCanceled()
	{
		pendingNextLevelIndex = -1;
	}

	private static void StartTutorial(int levelIndex, string levelId)
	{
		SaveManager.MarkTutorialStarted(levelId);
		LevelManager.ChangeToLevel(levelIndex, tutorialMode: true);
	}

	private int ConsumePendingNextLevelIndex()
	{
		int nextLevelIndex = pendingNextLevelIndex;
		pendingNextLevelIndex = -1;
		return nextLevelIndex;
	}
	public void SetTimeElapsed(int seconds)
	{
		timeElapsed = seconds;
		var timeSpan = TimeSpan.FromSeconds(timeElapsed);
		timeCompleteLabel.Text = $"Completed in under {timeSpan:mm\\:ss}";
	}
}
