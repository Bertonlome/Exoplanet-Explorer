using System;
using System.Collections.Generic;
using System.Linq;
using Game.Autoload;
using Game.Component;
using Game.Manager;
using Game.Resources.Level;
using Game.Ui;
using Game.UI;
using Godot;

namespace Game;

public partial class BaseLevel : Node
{
	private readonly StringName ESCAPE_ACTION = "escape";
	[Export]
	private PackedScene levelCompleteScreenScene;
	[Export]
	private PackedScene levelFailedScreenScene;
	[Export]
	private PackedScene selectedRobotUIScene;
	[Export]
	private LevelDefinitionResource levelDefinitionResource;
	[Export]
	private PackedScene escapeMenuScene;
	[Export]
	private PackedScene fragmentAnalysisScene;

	private GridManager gridManager;
	private Monolith monolith;
	private GameCamera gameCamera;
	private Node2D baseBuilding;
	private TileMapLayer baseTerrainTilemapLayer;
	private GameUI gameUI;
	private FragmentAnalysisUI fragmentAnalysisUI;
	private readonly Dictionary<Vector2I, FragmentAnalysisState> fragmentAnalysisStates = new();
	private BuildingManager buildingManager;
	private bool isComplete;
	private bool isFailed;
	private int currentTimeElapsed = 0;
	private GravitationalAnomalyMap gravitationalAnomalyMap;
	SelectedRobotUI selectedRobotUI;

	public override void _Ready()
	{
		isComplete = false;
		isFailed = false;
		gridManager = GetNode<GridManager>("GridManager");
		monolith = GetNode<Monolith>("%Monolith");
		gameCamera = GetNode<GameCamera>("GameCamera");
		baseTerrainTilemapLayer = GetNode<TileMapLayer>("%BaseTerrainTileMapLayer");
		gameUI = GetNode<GameUI>("GameUI");
		buildingManager = GetNode<BuildingManager>("BuildingManager");
		gravitationalAnomalyMap = GetNode<GravitationalAnomalyMap>("GravitationalAnomalyMap");

		buildingManager.SetStartingResourceCount(levelDefinitionResource.StartingWoodCount);
		buildingManager.SetStartingMaterialCount(levelDefinitionResource.StartingMaterialCount);
		gameUI.SetTimeToCompleteLevel(levelDefinitionResource.LevelDuration);
		//gameUI.TimeIsUp += ShowLevelFailed;
		buildingManager.BasePlaced += OnBasePlaced;
		buildingManager.ClockIsTicking += OnClockisTicking;

		gameCamera.SetBoundingRect(baseTerrainTilemapLayer.GetUsedRect());
		//if (baseBuilding != null)
		//{
			//gameCamera.CenterOnPosition(baseBuilding.GlobalPosition);
		//}
		gameCamera.Zoom = new Vector2((float)0.5, (float)0.5);
		gridManager.AerialRobotHasVisionOfMonolith += OnAerialRobotHasVisionOfMonolith;
		gridManager.GroundRobotTouchingMonolith += OnGroundRobotTouchingMonolith;
		gridManager.BaseTouchingMonolith += OnBaseTouchingMonolith;

		GameEvents.Instance.Connect(GameEvents.SignalName.RobotSelected, Callable.From<BuildingComponent>(OnRobotSelected));
		GameEvents.Instance.Connect(GameEvents.SignalName.FragmentAnalysisRequested, Callable.From<Vector2I>(OnFragmentAnalysisRequested));
	}

	public void OnBasePlaced()
	{
		baseBuilding = BuildingComponent.GetValidBuildingComponents(this)
			.First((buildingComponent) => buildingComponent.BuildingResource.IsBase);
	}

	public Rect2I GetLevelTileBounds()
	{
		return baseTerrainTilemapLayer?.GetUsedRect() ?? new Rect2I();
	}

	public override void _UnhandledInput(InputEvent evt)
	{
		if (evt.IsActionPressed(ESCAPE_ACTION))
		{
			var escapeMenu = escapeMenuScene.Instantiate<EscapeMenu>();
			AddChild(escapeMenu);
			GetViewport().SetInputAsHandled();
		}
	}

	private void ShowLevelComplete()
	{
		if (!isComplete)
		{
			isComplete = true;
			SaveManager.SavelevelCompletion(levelDefinitionResource, currentTimeElapsed, buildingManager.mineralAnalyzedCount);
			var levelCompleteScreen = levelCompleteScreenScene.Instantiate<LevelCompleteScreen>();
			AddChild(levelCompleteScreen);
			levelCompleteScreen.SetTimeElapsed(currentTimeElapsed);
			monolith.SetActive();
			gameUI.HideUI();
			selectedRobotUI.HideUI();
			if (GodotObject.IsInstanceValid(fragmentAnalysisUI))
			{
				fragmentAnalysisUI.HideUI();
			}
		}
	}

	public void ShowLevelFailed()
	{
		if (!isFailed && !isComplete)
		{
			isFailed = true;
			var levelFailedScreen = levelFailedScreenScene.Instantiate<LevelFailedScreen>();
			AddChild(levelFailedScreen);
			if (GodotObject.IsInstanceValid(fragmentAnalysisUI))
			{
				fragmentAnalysisUI.HideUI();
			}

			gameUI.HideUI();
			if (selectedRobotUI != null)
			{
				selectedRobotUI.HideUI();
			}
		}
	}

	private void OnAerialRobotHasVisionOfMonolith()
	{
		monolith.SetVisible();
	}

	private void OnGroundRobotTouchingMonolith()
	{
		if (isComplete) return;
		ShowLevelComplete();
	}

	private void OnBaseTouchingMonolith()
	{
		ShowLevelFailed();
	}

	private void OnRobotSelected(BuildingComponent buildingComponent)
	{
		if (GodotObject.IsInstanceValid(selectedRobotUI))
		{
			selectedRobotUI.QueueFree();
		}

		selectedRobotUI = selectedRobotUIScene.Instantiate<SelectedRobotUI>();
		AddChild(selectedRobotUI);
		//selectedRobotUI.selectedBuildingComponent = buildingComponent;
		selectedRobotUI.SetupUI(buildingComponent, gravitationalAnomalyMap); // Call setup after adding to tree
	}

	private void OnFragmentAnalysisRequested(Vector2I fragmentPosition)
	{
		if (GodotObject.IsInstanceValid(fragmentAnalysisUI))
		{
			fragmentAnalysisUI.HideUI();
		}

		fragmentAnalysisStates.TryGetValue(fragmentPosition, out FragmentAnalysisState savedState);
		fragmentAnalysisUI = fragmentAnalysisScene.Instantiate<FragmentAnalysisUI>();
		AddChild(fragmentAnalysisUI);
		fragmentAnalysisUI.StateSaved += OnFragmentAnalysisStateSaved;
		fragmentAnalysisUI.SetupUI(fragmentPosition, gridManager.monolithPosition, savedState);
	}

	private void OnFragmentAnalysisStateSaved(Vector2I fragmentPosition, FragmentAnalysisState state)
	{
		fragmentAnalysisStates[fragmentPosition] = state;
	}

	private void OnClockisTicking()
	{
		currentTimeElapsed++;
		if (currentTimeElapsed >= levelDefinitionResource.LevelDuration)
		{
			ShowLevelFailed();
		}
	}
}
