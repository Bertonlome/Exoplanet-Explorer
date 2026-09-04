using System;
using Game;
using Game.Resources.Level;
using Game.Autoload;
using Godot;
using Newtonsoft.Json;

public partial class SaveManager : Node
{
	public static SaveManager Instance {get; private set;}

	public static SaveData saveData = new();

	private static readonly string SAVE_FILE_PATH = "user://save.json";

		public override void _Notification(int what)
	{
		if (what == NotificationSceneInstantiated)
		{
			Instance = this;
			LoadSaveData();
		}
	}

	public static bool IsLevelCompleted(string levelId)
	{
		saveData.EnsureDefaults();
		saveData.LevelCompletionStatus.TryGetValue(levelId, out var data);
		return data?.IsCompleted == true;
	}

	public static TimeSpan GetBestTimeForLevel(string levelId)
	{
		saveData.EnsureDefaults();
		saveData.LevelCompletionStatus.TryGetValue(levelId, out var data);
		if(data == null || data.TimeCompletedInSeconds <= 0)
		{
			return TimeSpan.Zero;
		}
		return TimeSpan.FromSeconds(data.TimeCompletedInSeconds);
	}

	public static int GetMineralsAnalyzedForLevel(string levelId)
	{
		saveData.EnsureDefaults();
		saveData.LevelCompletionStatus.TryGetValue(levelId, out var data);
		return data?.MineralsAnalyzed ?? 0;
	}

	public static void SavelevelCompletion(LevelDefinitionResource levelDefinitionResource, int timeCompletedInSeconds, int mineralsAnalyzed)
	{
		saveData.SavelevelCompletion(levelDefinitionResource.Id, true, timeCompletedInSeconds, mineralsAnalyzed);
		WriteSaveData();
	}

	public static bool HasTutorialStarted(string levelId)
	{
		return saveData.HasTutorialStarted(levelId);
	}

	public static void MarkTutorialStarted(string levelId)
	{
		if (string.IsNullOrWhiteSpace(levelId) || saveData.HasTutorialStarted(levelId))
		{
			return;
		}

		saveData.MarkTutorialStarted(levelId);
		WriteSaveData();
	}

	public static void SaveOptions(
		float sfxVolumePercent,
		float musicVolumePercent,
		float geigerVolumePercent,
		bool isFullscreen)
	{
		saveData.EnsureDefaults();
		saveData.Options.SfxVolumePercent = Mathf.Clamp(sfxVolumePercent, 0f, 1f);
		saveData.Options.MusicVolumePercent = Mathf.Clamp(musicVolumePercent, 0f, 1f);
		saveData.Options.GeigerVolumePercent = Mathf.Clamp(geigerVolumePercent, 0f, 1f);
		saveData.Options.IsFullscreen = isFullscreen;
		WriteSaveData();
	}

	private static void WriteSaveData()
	{
		var dataString = JsonConvert.SerializeObject(saveData);

		using var saveFile = FileAccess.Open(SAVE_FILE_PATH, FileAccess.ModeFlags.Write);
		saveFile.StoreLine(dataString);
	}

	private static void LoadSaveData()
	{
		if(!FileAccess.FileExists(SAVE_FILE_PATH))
		{
			saveData.EnsureDefaults();
			ApplySavedOptions();
			return;
		}

		using var saveFile = FileAccess.Open(SAVE_FILE_PATH, FileAccess.ModeFlags.Read);
		var dataString = saveFile.GetLine();
		try
		{
		saveData = JsonConvert.DeserializeObject<SaveData>(dataString) ?? new SaveData();
		saveData.EnsureDefaults();
		}
		catch(Exception _)
		{
			GD.PushWarning("Save JSON file was corrupted");
			saveData = new SaveData();
		}
		ApplySavedOptions();
	}

	private static void ApplySavedOptions()
	{
		saveData.EnsureDefaults();
		OptionsData options = saveData.Options;
		OptionsHelper.SetBusVolumePercent("SFX", options.SfxVolumePercent);
		OptionsHelper.SetBusVolumePercent("Music", options.MusicVolumePercent);
		OptionsHelper.SetBusVolumePercent("Geiger", options.GeigerVolumePercent);
		OptionsHelper.SetFullScreen(options.IsFullscreen);
	}
}
