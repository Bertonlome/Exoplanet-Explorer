using System.Collections.Generic;

namespace Game;

public class SaveData
{
    public Dictionary<string, LevelCompletionData> LevelCompletionStatus { get; private set; } = new();
    public HashSet<string> TutorialStartedLevelIds { get; private set; } = new();
    public OptionsData Options { get; private set; } = new();

    public void SavelevelCompletion(string id, bool completed, int timeCompletedInSeconds, int mineralsAnalyzed)
    {
        EnsureDefaults();
        if (!LevelCompletionStatus.ContainsKey(id))
        {
            LevelCompletionStatus[id] = new LevelCompletionData();
        }
        LevelCompletionStatus[id].IsCompleted = completed;
        LevelCompletionStatus[id].TimeCompletedInSeconds = timeCompletedInSeconds;
        LevelCompletionStatus[id].MineralsAnalyzed = mineralsAnalyzed;
    }

    public bool HasTutorialStarted(string levelId)
    {
        EnsureDefaults();
        return !string.IsNullOrWhiteSpace(levelId) && TutorialStartedLevelIds.Contains(levelId);
    }

    public void MarkTutorialStarted(string levelId)
    {
        EnsureDefaults();
        if (!string.IsNullOrWhiteSpace(levelId))
        {
            TutorialStartedLevelIds.Add(levelId);
        }
    }

    public void EnsureDefaults()
    {
        LevelCompletionStatus ??= new Dictionary<string, LevelCompletionData>();
        TutorialStartedLevelIds ??= new HashSet<string>();
        Options ??= new OptionsData();
    }
}

public class OptionsData
{
    public float SfxVolumePercent { get; set; } = 1f;
    public float MusicVolumePercent { get; set; } = 1f;
    public float GeigerVolumePercent { get; set; } = 0.313f;
    public bool IsFullscreen { get; set; }
}
