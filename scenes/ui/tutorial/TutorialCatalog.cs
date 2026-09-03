using System.Collections.Generic;

namespace Game.UI.Tutorial;

/// <summary>
/// Stable tutorial availability contract. Script construction is added when each level tutorial is
/// implemented; Checkpoint 1.5 only needs to identify tutorial-capable level definitions.
/// </summary>
public static class TutorialCatalog
{
	public const string Level1Id = "when_cucumbers_fall";
	public const string Level2Id = "cats_are_playing";
	public const string Level3Id = "concrete_bricks";

	private static readonly HashSet<string> TutorialLevelIds = new()
	{
		Level1Id,
		Level2Id,
		Level3Id,
	};

	public static bool HasTutorial(string levelId)
	{
		return !string.IsNullOrWhiteSpace(levelId) && TutorialLevelIds.Contains(levelId);
	}

	public static bool TryCreateScript(
		string levelId,
		TutorialLevelContext context,
		out TutorialScript script)
	{
		script = levelId switch
		{
			Level1Id when context != null => new Scripts.Level1Tutorial(context),
			Level2Id when context != null => new Scripts.Level2Tutorial(context),
			Level3Id when context != null => new Scripts.Level3Tutorial(context),
			_ => null,
		};
		return script != null;
	}
}
