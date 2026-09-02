using System.Linq;
using Game.Resources.Level;
using Godot;

namespace Game.Autoload;

public partial class LevelManager : Node
{
	private static LevelManager instance;

	[Export]
	private LevelDefinitionResource[] levelDefinitions;

	[Export(PropertyHint.File, "*.tscn")]
	private string introCutScenePath;
	[Export(PropertyHint.File, "*.tscn")]
	private string mainMenuScenePath;

	private static int currentLevelIndex;
	public static bool IsTutorialModeActive { get; private set; }
	public static int CurrentLevelIndex => currentLevelIndex;

	public override void _Notification(int what)
	{
		if (what == NotificationSceneInstantiated)
		{
			instance = this;
		}
	}

	public static LevelDefinitionResource[] GetLevelDefinitions()
	{
		return instance.levelDefinitions.ToArray();
	}

	public static void ChangeToLevel(int levelIndex, bool tutorialMode = false)
	{
		if (levelIndex >= instance.levelDefinitions.Length || levelIndex < 0) return;
		currentLevelIndex = levelIndex;
		IsTutorialModeActive = tutorialMode;

		var levelDefinition = instance.levelDefinitions[currentLevelIndex];
		GD.Print($"Loading level '{levelDefinition.Id}' in " +
			$"{(tutorialMode ? "tutorial" : "regular")} mode.");
		instance.GetTree().ChangeSceneToFile(levelDefinition.LevelScenePath);
	}

	public static void ChangeToIntroCutScene()
	{
		IsTutorialModeActive = false;
		instance.GetTree().ChangeSceneToFile(instance.introCutScenePath);
	}

	public static void ChangeToMainMenu()
	{
		IsTutorialModeActive = false;
		instance.GetTree().ChangeSceneToFile(instance.mainMenuScenePath);
	}

	public static void ChangeToNextLevel()
	{
		ChangeToLevel(currentLevelIndex + 1, IsTutorialModeActive);
	}
	public static bool IsLastLevel()
	{
		return currentLevelIndex == instance.levelDefinitions.Length - 1;
	}
}
