using System;
using System.Collections.Generic;
using System.Linq;
using Game.Autoload;
using Game.Component;
using Game.Manager;
using Game.Resources.Level;
using Game.Ui;
using Game.UI;
using Game.UI.Tutorial;
using Godot;

namespace Game;

public partial class BaseLevel : Node
{
	public event Action<Vector2I> FragmentAnalysisStatusChanged;

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
	private Vector2I? fragmentBeingAnalysed;
	private bool activeFragmentWasRestored;
	private FragmentAutonomyMode fragmentAutonomyMode = FragmentAutonomyMode.Off;
	private BuildingManager buildingManager;
	private bool isComplete;
	private bool isFailed;
	private int currentTimeElapsed = 0;
	private GravitationalAnomalyMap gravitationalAnomalyMap;
	SelectedRobotUI selectedRobotUI;
	private TutorialTargetRegistry tutorialTargetRegistry;
	private TutorialEventBridge tutorialEventBridge;
	private TutorialDirector tutorialDirector;
	private TutorialOverlay tutorialOverlay;
	private TutorialTargetRegistration preplacedBaseTutorialTarget;
	private TutorialTargetRegistration manualDestinationTutorialTarget;
	private TutorialTargetRegistration returnDestinationTutorialTarget;
	private TutorialTargetRegistration deployedRoverTutorialTarget;
	private Vector2I level1ManualDestination;
	private Vector2I level1ReturnDestination;

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
		GameEvents.Instance.Connect(
			GameEvents.SignalName.FragmentAnalysisRequested,
			Callable.From<Vector2I, BuildingComponent, int>(OnFragmentAnalysisRequested));

		if (LevelManager.IsTutorialModeActive)
		{
			CallDeferred(nameof(StartTutorialIfEnabled));
		}
	}

	private void StartTutorialIfEnabled()
	{
		if (!LevelManager.IsTutorialModeActive || levelDefinitionResource == null)
		{
			return;
		}
		bool hasPreplacedBase = levelDefinitionResource.Id == TutorialCatalog.Level1Id;
		BuildingComponent tutorialBase = BuildingComponent.GetBaseBuilding(this).FirstOrDefault();
		if (hasPreplacedBase && !GodotObject.IsInstanceValid(tutorialBase))
		{
			GD.PushWarning($"Tutorial level '{levelDefinitionResource.Id}' has no base.");
			return;
		}
		Vector2I basePosition = GodotObject.IsInstanceValid(tutorialBase)
			? tutorialBase.GetGridCellPosition()
			: Vector2I.Zero;
		level1ManualDestination = basePosition + new Vector2I(8, 1);
		level1ReturnDestination = basePosition + new Vector2I(12, 2);
		Vector2I level1BaseReturnDestination = basePosition + new Vector2I(2, 3);
		Vector2I monolithPosition = baseTerrainTilemapLayer.LocalToMap(
			baseTerrainTilemapLayer.ToLocal(monolith.GlobalPosition));
		TutorialLevelContext tutorialContext = new(
			basePosition,
			level1ManualDestination,
			level1ReturnDestination,
			level1BaseReturnDestination,
			monolithPosition);
		if (!TutorialCatalog.TryCreateScript(
			levelDefinitionResource.Id,
			tutorialContext,
			out TutorialScript script))
		{
			GD.PushWarning(
				$"Tutorial mode is active for '{levelDefinitionResource.Id}', but its script is not implemented yet.");
			return;
		}

		tutorialTargetRegistry = GetNode<TutorialTargetRegistry>("TutorialTargetRegistry");
		tutorialEventBridge = GetNode<TutorialEventBridge>("TutorialEventBridge");
		tutorialDirector = GetNode<TutorialDirector>("TutorialDirector");
		tutorialOverlay = GetNode<TutorialOverlay>("TutorialOverlay");

		// Level 1 already contains a base. Resolve that state before target registration so the UI
		// presents rover deployment rather than asking the player to place a second base.
		if (hasPreplacedBase) buildingManager.RefreshPreplacedBaseState();
		gameUI.RegisterTutorialTargets(tutorialTargetRegistry);
		if (hasPreplacedBase)
		{
			RegisterPreplacedBaseTutorialTarget();
			RegisterLevel1MovementTargets();
		}
		tutorialEventBridge.Start();
		tutorialDirector.Initialize(tutorialOverlay, tutorialEventBridge, tutorialTargetRegistry);
		tutorialDirector.StepStarted += OnTutorialStepTransition;
		tutorialDirector.StepCompleted += OnTutorialStepTransition;
		tutorialDirector.TutorialCompleted += OnTutorialEnded;
		tutorialDirector.TutorialSkipped += OnTutorialEnded;
		tutorialDirector.Start(script);
		tutorialEventBridge.Publish(
			new TutorialEventContext(TutorialEvent.LevelReady, payload: levelDefinitionResource.Id));
	}

	private void RegisterLevel1MovementTargets()
	{
		deployedRoverTutorialTarget = tutorialTargetRegistry.RegisterRectProvider(
			TutorialTargetIds.DeployedRover,
			this,
			() =>
			{
				BuildingComponent rover = BuildingComponent.GetValidBuildingComponents(this)
					.FirstOrDefault(building => building.BuildingResource?.DisplayName == "Rover");
				return GetBuildingScreenRect(rover);
			});
		manualDestinationTutorialTarget = tutorialTargetRegistry.RegisterRectProvider(
			TutorialTargetIds.ManualMovementDestination,
			this,
			() => GetWorldCellScreenRect(level1ManualDestination));
		returnDestinationTutorialTarget = tutorialTargetRegistry.RegisterRectProvider(
			TutorialTargetIds.ReturnDestination,
			this,
			() => GetWorldCellScreenRect(level1ReturnDestination));
	}

	private Rect2? GetWorldCellScreenRect(Vector2I cell)
	{
		if (GetViewport() == null) return null;
		Transform2D canvasTransform = GetViewport().GetCanvasTransform();
		Vector2 firstCorner = canvasTransform * (Vector2)(cell * 64);
		Vector2 oppositeCorner = canvasTransform * (Vector2)((cell + Vector2I.One) * 64);
		return new Rect2(
			new Vector2(Mathf.Min(firstCorner.X, oppositeCorner.X), Mathf.Min(firstCorner.Y, oppositeCorner.Y)),
			new Vector2(Mathf.Abs(oppositeCorner.X - firstCorner.X), Mathf.Abs(oppositeCorner.Y - firstCorner.Y)));
	}

	private void RegisterPreplacedBaseTutorialTarget()
	{
		BuildingComponent preplacedBase = BuildingComponent.GetBaseBuilding(this).FirstOrDefault();
		if (!GodotObject.IsInstanceValid(preplacedBase))
		{
			GD.PushWarning(
				$"Tutorial level '{levelDefinitionResource.Id}' expected a pre-placed base but none was found.");
			return;
		}

		preplacedBaseTutorialTarget = tutorialTargetRegistry.RegisterRectProvider(
			TutorialTargetIds.PreplacedBase,
			preplacedBase,
			() => GetBuildingScreenRect(preplacedBase));
	}

	private Rect2? GetBuildingScreenRect(BuildingComponent building)
	{
		if (!GodotObject.IsInstanceValid(building) || building.BuildingResource == null ||
			GetViewport() == null)
		{
			return null;
		}

		Vector2 worldSize = new(
			building.BuildingResource.Dimensions.X * 64f,
			building.BuildingResource.Dimensions.Y * 64f);
		Transform2D canvasTransform = GetViewport().GetCanvasTransform();
		Vector2 firstCorner = canvasTransform * building.GlobalPosition;
		Vector2 oppositeCorner = canvasTransform * (building.GlobalPosition + worldSize);
		Vector2 screenPosition = new(
			Mathf.Min(firstCorner.X, oppositeCorner.X),
			Mathf.Min(firstCorner.Y, oppositeCorner.Y));
		Vector2 screenSize = new(
			Mathf.Abs(oppositeCorner.X - firstCorner.X),
			Mathf.Abs(oppositeCorner.Y - firstCorner.Y));
		return new Rect2(screenPosition, screenSize);
	}

	public override void _ExitTree()
	{
		preplacedBaseTutorialTarget?.Dispose();
		preplacedBaseTutorialTarget = null;
		manualDestinationTutorialTarget?.Dispose();
		manualDestinationTutorialTarget = null;
		returnDestinationTutorialTarget?.Dispose();
		returnDestinationTutorialTarget = null;
		deployedRoverTutorialTarget?.Dispose();
		deployedRoverTutorialTarget = null;
		if (GodotObject.IsInstanceValid(gameUI))
		{
			gameUI.ClearTutorialTargets();
		}
		if (GodotObject.IsInstanceValid(tutorialDirector))
		{
			tutorialDirector.StepStarted -= OnTutorialStepTransition;
			tutorialDirector.StepCompleted -= OnTutorialStepTransition;
			tutorialDirector.TutorialCompleted -= OnTutorialEnded;
			tutorialDirector.TutorialSkipped -= OnTutorialEnded;
			tutorialDirector.Stop();
		}
		if (GodotObject.IsInstanceValid(tutorialEventBridge))
		{
			tutorialEventBridge.Stop();
		}
	}

	private void OnTutorialStepTransition(string stepId)
	{
		CancelTutorialPointerLatch();
		if (stepId == "level1.monolith-discovered")
		{
			BuildingComponent rover = BuildingComponent.GetValidBuildingComponents(this)
				.FirstOrDefault(building => building.BuildingResource?.DisplayName == "Rover");
			if (GodotObject.IsInstanceValid(rover))
			{
				gameCamera.FocusAtMaximumZoom(rover.GlobalPosition);
			}
		}
	}

	private void OnTutorialEnded()
	{
		CancelTutorialPointerLatch();
	}

	private void CancelTutorialPointerLatch()
	{
		if (GodotObject.IsInstanceValid(gameCamera))
		{
			gameCamera.SuppressMouseDragUntilRelease();
		}

		// Run once more after the current input event finishes. This catches a press that continues
		// propagating to GameCamera after a tutorial completion callback closed the overlay.
		Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(gameCamera))
			{
				gameCamera.SuppressMouseDragUntilRelease();
			}
		}).CallDeferred();
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
		tutorialEventBridge?.Publish(new TutorialEventContext(TutorialEvent.MonolithTouched));
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
		if (GodotObject.IsInstanceValid(tutorialTargetRegistry) &&
			buildingComponent.BuildingResource?.DisplayName == "Rover")
		{
			selectedRobotUI.RegisterTutorialTargets(tutorialTargetRegistry);
		}
	}

	private void OnFragmentAnalysisRequested(
		Vector2I fragmentPosition,
		BuildingComponent requestingRover,
		int actionOriginValue)
	{
		FragmentAnalysisActionOrigin actionOrigin = Enum.IsDefined(
			typeof(FragmentAnalysisActionOrigin),
			actionOriginValue)
			? (FragmentAnalysisActionOrigin)actionOriginValue
			: FragmentAnalysisActionOrigin.System;
		OpenFragmentAnalysis(fragmentPosition, requestingRover, actionOrigin);
	}

	private bool OpenFragmentAnalysis(
		Vector2I fragmentPosition,
		BuildingComponent requestingRover,
		FragmentAnalysisActionOrigin actionOrigin)
	{
		if (!GodotObject.IsInstanceValid(requestingRover) ||
			requestingRover.BuildingResource == null ||
			requestingRover.BuildingResource.IsAerial ||
			requestingRover.IsLifted)
		{
			GameUI.PushMessage("Only a ground rover can analyse a fragment", "red", true);
			return false;
		}

		if (!GodotObject.IsInstanceValid(selectedRobotUI) ||
			selectedRobotUI.selectedBuildingComponent != requestingRover)
		{
			GameUI.PushMessage("Fragment analysis request came from a stale rover selection", "red", true);
			return false;
		}

		IReadOnlyList<Vector2I> validSamples = gridManager.GetSampleLocationsAroundPosition(
			requestingRover.GetGridCellPosition());
		if (!validSamples.Contains(fragmentPosition))
		{
			GameUI.PushMessage("Selected fragment is no longer in analysis range", "red", true);
			return false;
		}

		if (fragmentBeingAnalysed.HasValue && GodotObject.IsInstanceValid(fragmentAnalysisUI))
		{
			GameUI.PushMessage("Fragment analysis is already open", "red", true);
			return false;
		}

		if (GodotObject.IsInstanceValid(fragmentAnalysisUI))
		{
			fragmentAnalysisUI.HideUI();
		}

		bool wasRestored = fragmentAnalysisStates.TryGetValue(
			fragmentPosition,
			out FragmentAnalysisState savedState);
		fragmentBeingAnalysed = fragmentPosition;
		activeFragmentWasRestored = wasRestored;
		fragmentAnalysisUI = fragmentAnalysisScene.Instantiate<FragmentAnalysisUI>();
		AddChild(fragmentAnalysisUI);
		fragmentAnalysisUI.StateSaved += OnFragmentAnalysisStateSaved;
		fragmentAnalysisUI.SetupUI(
			fragmentPosition,
			gridManager.monolithPosition,
			savedState,
			fragmentAutonomyMode,
			wasRestored,
			actionOrigin);
		FragmentAnalysisStatusChanged?.Invoke(fragmentPosition);
		return true;
	}

	private void OnFragmentAnalysisStateSaved(Vector2I fragmentPosition, FragmentAnalysisState state)
	{
		fragmentAnalysisStates[fragmentPosition] = state;
		fragmentBeingAnalysed = null;
		activeFragmentWasRestored = false;
		if (state?.RoverState != null)
		{
			SetFragmentAutonomyMode(state.RoverState.GlobalMode);
		}
		FragmentAnalysisStatusChanged?.Invoke(fragmentPosition);
	}

	public bool HasFragmentAnalysisState(Vector2I fragmentPosition)
	{
		return fragmentAnalysisStates.ContainsKey(fragmentPosition);
	}

	public FragmentSampleAvailability GetFragmentAnalysisStatus(Vector2I fragmentPosition)
	{
		if (fragmentBeingAnalysed == fragmentPosition)
		{
			return new FragmentSampleAvailability
			{
				Position = fragmentPosition,
				Status = FragmentSampleAnalysisStatus.Analysing,
				IsRestored = activeFragmentWasRestored
			};
		}

		if (!fragmentAnalysisStates.TryGetValue(fragmentPosition, out FragmentAnalysisState state))
		{
			return new FragmentSampleAvailability
			{
				Position = fragmentPosition,
				Status = FragmentSampleAnalysisStatus.Available
			};
		}

		return new FragmentSampleAvailability
		{
			Position = fragmentPosition,
			Status = state.WasCompleted || state.RoverState?.IsAnalysisCompleted == true
				? FragmentSampleAnalysisStatus.Completed
				: state.WasEverSolved || state.WasSolved
					? FragmentSampleAnalysisStatus.Solved
					: FragmentSampleAnalysisStatus.PreviouslyAnalysed
		};
	}

	private void SetFragmentAutonomyMode(FragmentAutonomyMode mode)
	{
		fragmentAutonomyMode = mode;
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
