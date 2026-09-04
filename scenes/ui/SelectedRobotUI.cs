using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Game.Autoload;
using Game.Component;
using Game.Resources.Building;
using Game.UI.Tutorial;
using Godot;

namespace Game.UI;

public partial class SelectedRobotUI : CanvasLayer
{
	[Export]
	BuildingResource bridgeBuildingResource;
	[Export]
	BuildingResource antennaBuildingResource;
	[Export]
	Texture2D mutedGeigerTexture;
	[Export]
	Texture2D unmutedGeigerTexture;
	[Export]
	Texture2D materialIconTexture;
	[Export]
	Texture2D woodIconTexture;
	[Export]
	Texture2D mineralIconTexture;
	private Button randomExplorButton;
	private Button stopExplorbutton;
	private Button trackRobotButton;
	private Button gradientSearchButton;
	private Button returnToBaseButton;
	private Button startExplorButton;
	private Button customPathButton;
	private OptionButton explorModeOptionsButton;
	private Label gravAnomValueLabel;
	private Label batteryLabel;
	private BoxContainer resourcesCarriedContainer;
	private HBoxContainer resourceLabel;
	private Label woodCountLabel;
	private TextureRect woodIconRect;
	private Label mineralCountLabel;
	private TextureRect mineralIconRect;
	private Label titleLabel;
	private Button placeBridgeButton;
	private Button toggleSoundGeigerButton;

	private Button placeAntennaButton;
	private Button liftRobotButton;
	private Button analyseSampleButton;
	private Button dropResourcesButton;
	private VBoxContainer fragmentSampleContainer;
	private readonly List<FragmentSampleAvailability> nearbyFragmentSamples = new();
	private Vector2I? selectedFragmentSample;
	private BaseLevel baseLevel;

	private MultiPurposeButtonState currentButtonState;
	private bool signalsDisconnected;
	private TutorialTargetRegistration batteryTutorialTarget;
	private TutorialTargetRegistration explorationModeTutorialTarget;
	private TutorialTargetRegistration startExplorationTutorialTarget;
	private TutorialTargetRegistration dropResourcesTutorialTarget;
	private TutorialTargetRegistration resourcesCarriedTutorialTarget;
	private TutorialTargetRegistration anomalyRadarTutorialTarget;
	private TutorialTargetRegistration anomalyIndicatorTutorialTarget;
	private TutorialTargetRegistration placeBridgeTutorialTarget;
	private TutorialTargetRegistration liftRobotTutorialTarget;
	private TutorialTargetRegistration customPathTutorialTarget;
	private TutorialTargetRegistration analyseSampleTutorialTarget;
	private TutorialTargetRegistration placeAntennaTutorialTarget;
	public BuildingComponent selectedBuildingComponent;
	public BuildingComponent groundRobotBelowUav;
	private MiniMapController miniMapController;
	private readonly Queue<int> anomalyHistory = new();
	private const int MaxAnomalyHistoryEntries = 10;
	private const int AnomalyBarLength = 24;
	private const float MaxAnomalyValuePossible = 500f;

	public void RegisterTutorialTargets(TutorialTargetRegistry registry)
	{
		batteryTutorialTarget?.Dispose();
		Control batteryDisplay = GetNodeOrNull<Control>("%RobotInfoContainer3");
		if (registry != null && IsInstanceValid(batteryDisplay))
		{
			ScrollContainer scrollContainer = FindAncestorScrollContainer(batteryDisplay);
			batteryTutorialTarget = registry.RegisterRectProvider(
				TutorialTargetIds.SelectedRoverBattery,
				batteryDisplay,
				() =>
				{
					if (!IsInstanceValid(batteryDisplay) || !batteryDisplay.IsVisibleInTree())
					{
						return null;
					}
					scrollContainer?.EnsureControlVisible(batteryDisplay);
					return batteryDisplay.GetGlobalRect();
				},
				batteryDisplay);
		}
		explorationModeTutorialTarget?.Dispose();
		startExplorationTutorialTarget?.Dispose();
		dropResourcesTutorialTarget?.Dispose();
		resourcesCarriedTutorialTarget?.Dispose();
		anomalyRadarTutorialTarget?.Dispose();
		anomalyIndicatorTutorialTarget?.Dispose();
		placeBridgeTutorialTarget?.Dispose();
		liftRobotTutorialTarget?.Dispose();
		customPathTutorialTarget?.Dispose();
		analyseSampleTutorialTarget?.Dispose();
		placeAntennaTutorialTarget?.Dispose();
		if (registry != null)
		{
			explorationModeTutorialTarget = registry.RegisterControl(
				TutorialTargetIds.ExplorationModeMenu,
				explorModeOptionsButton);
			startExplorationTutorialTarget = registry.RegisterControl(
				TutorialTargetIds.StartExplorationButton,
				startExplorButton);
			ScrollContainer resourceScrollContainer = FindAncestorScrollContainer(dropResourcesButton);
			dropResourcesTutorialTarget = registry.RegisterRectProvider(
				TutorialTargetIds.DropResourcesButton,
				dropResourcesButton,
				() =>
				{
					if (!IsInstanceValid(dropResourcesButton) || !dropResourcesButton.IsVisibleInTree())
					{
						return null;
					}
					resourceScrollContainer?.EnsureControlVisible(dropResourcesButton);
					return dropResourcesButton.GetGlobalRect();
				},
				dropResourcesButton);
			ScrollContainer carriedResourcesScroll = FindAncestorScrollContainer(resourcesCarriedContainer);
			resourcesCarriedTutorialTarget = registry.RegisterRectProvider(
				TutorialTargetIds.ResourcesCarried,
				resourcesCarriedContainer,
				() =>
				{
					if (!IsInstanceValid(resourcesCarriedContainer) ||
						!resourcesCarriedContainer.IsVisibleInTree()) return null;
					carriedResourcesScroll?.EnsureControlVisible(resourcesCarriedContainer);
					return resourcesCarriedContainer.GetGlobalRect();
				},
				resourcesCarriedContainer);
			if (IsInstanceValid(miniMapController?.AnomalyRadarControl))
			{
				anomalyRadarTutorialTarget = registry.RegisterControl(
					TutorialTargetIds.AnomalyRadar,
					miniMapController.AnomalyRadarControl);
			}
			Control anomalyIndicator = GetNodeOrNull<Control>("%RobotInfoContainer");
			if (IsInstanceValid(anomalyIndicator))
			{
				ScrollContainer anomalyScroll = FindAncestorScrollContainer(anomalyIndicator);
				anomalyIndicatorTutorialTarget = registry.RegisterRectProvider(
					TutorialTargetIds.AnomalyIndicator,
					anomalyIndicator,
					() =>
					{
						if (!IsInstanceValid(anomalyIndicator) || !anomalyIndicator.IsVisibleInTree()) return null;
						anomalyScroll?.EnsureControlVisible(anomalyIndicator);
						return anomalyIndicator.GetGlobalRect();
					},
					anomalyIndicator);
			}
			ScrollContainer bridgeScroll = FindAncestorScrollContainer(placeBridgeButton);
			placeBridgeTutorialTarget = registry.RegisterRectProvider(
				TutorialTargetIds.PlaceBridgeButton,
				placeBridgeButton,
				() =>
				{
					if (!IsInstanceValid(placeBridgeButton) || !placeBridgeButton.IsVisibleInTree()) return null;
					bridgeScroll?.EnsureControlVisible(placeBridgeButton);
					return placeBridgeButton.GetGlobalRect();
				},
				placeBridgeButton);
			if (selectedBuildingComponent?.BuildingResource?.IsAerial == true)
			{
				ScrollContainer liftScroll = FindAncestorScrollContainer(liftRobotButton);
				liftRobotTutorialTarget = registry.RegisterRectProvider(
					TutorialTargetIds.LiftRobotButton,
					liftRobotButton,
					() =>
					{
						if (!IsInstanceValid(liftRobotButton) || !liftRobotButton.IsVisibleInTree()) return null;
						liftScroll?.EnsureControlVisible(liftRobotButton);
						return liftRobotButton.GetGlobalRect();
					},
					liftRobotButton);
			}
			customPathTutorialTarget = registry.RegisterControl(
				TutorialTargetIds.CustomPathButton,
				customPathButton);
			if (selectedBuildingComponent?.BuildingResource?.IsAerial == false)
			{
				analyseSampleTutorialTarget = registry.RegisterControl(
					TutorialTargetIds.AnalyseSampleButton,
					analyseSampleButton);
				placeAntennaTutorialTarget = registry.RegisterControl(
					TutorialTargetIds.PlaceAntennaButton,
					placeAntennaButton);
			}
		}
	}

	private static ScrollContainer FindAncestorScrollContainer(Node node)
	{
		Node current = node?.GetParent();
		while (current != null)
		{
			if (current is ScrollContainer scrollContainer) return scrollContainer;
			current = current.GetParent();
		}
		return null;
	}

	public enum MultiPurposeButtonState
	{
		Placebridge,
		LiftRobot,
		DropRobot
	}

	public enum ExplorMode
	{
		Random,
		Gradient,
		ReturnToBase,
		None
	}

	private ExplorMode currentexplorMode = ExplorMode.None;

	public override void _Ready()
	{
		//InitializeUI();


		CallDeferred("SetAnomalySignal");
		CallDeferred("SetBatterySignal");
		CallDeferred("SetResourceSignal");
		GameEvents.Instance.Connect(GameEvents.SignalName.NoMoreRobotSelected, Callable.From<BuildingComponent>(OnNoMoreRobotSelected));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingStuck, Callable.From<BuildingComponent>(OnBuildingStuck));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingUnStuck, Callable.From<BuildingComponent>(OnBuildingUnStuck));
		GameEvents.Instance.Connect(GameEvents.SignalName.AllRobotStopped, Callable.From(OnAllRobotsStopped));
		GameEvents.Instance.Connect(GameEvents.SignalName.CarriedResourceCountChanged, Callable.From<int>(OnResourceCarriedCountChanged));
		GameEvents.Instance.Connect(GameEvents.SignalName.GroundRobotBelowUav, Callable.From<BuildingComponent>(OnGroundRobotBelowUav));
		GameEvents.Instance.Connect(GameEvents.SignalName.NoGroundRobotBelowUav, Callable.From(OnNoGroundRobotBelowUav));
	}

	public void SetupUI(BuildingComponent component, GravitationalAnomalyMap anomalyMap)
	{
		selectedBuildingComponent = component;
		baseLevel = GetParent() as BaseLevel;
		if (baseLevel != null)
		{
			baseLevel.FragmentAnalysisStatusChanged += OnFragmentAnalysisStatusChanged;
		}
		selectedBuildingComponent.ModeChanged += OnModeChanged;
		InitializeUI();
		
		
		// MiniMapController is optional - only initialize if it exists
		if (HasNode("%MiniMapController"))
		{
			miniMapController = GetNode<MiniMapController>("%MiniMapController");
			miniMapController.Initialize(selectedBuildingComponent, anomalyMap, anomalyMap.MapSize);
			
			// Set to robot-centered window mode with sensor radius
			int sensorRadius = selectedBuildingComponent.BuildingResource.AnomalySensorRadius;
			int windowSize = sensorRadius * 2; // diameter of sensor range
			miniMapController.SetMode(false, new Vector2I(windowSize, windowSize)); // false = robot window mode
			
			// Update robot position (this will refresh internally)
			miniMapController.SetRobotCell(selectedBuildingComponent.GetGridCellPosition());
			// Removed redundant Refresh() call - SetRobotCell already refreshes
			
		}
		else
		{
			GD.PrintErr("MiniMapController node not found in SelectedRobotUI scene.");
		}

		Callable buildingMovedCallable = Callable.From<BuildingComponent>(OnBuildingMovedForMinimap);
		if (!GameEvents.Instance.IsConnected(GameEvents.SignalName.BuildingMoved, buildingMovedCallable))
		{
			GameEvents.Instance.Connect(GameEvents.SignalName.BuildingMoved, buildingMovedCallable);
		}
		RefreshNearbySampleAvailability();
		ApplyLevelSpecificSensorVisibility();
		
		Visible = true;
	}

	private void ApplyLevelSpecificSensorVisibility()
	{
		if (baseLevel?.LevelId != TutorialCatalog.Level3Id) return;

		miniMapController?.AnomalyRadarControl?.Hide();
		GetNodeOrNull<Control>("%RobotInfoContainer")?.Hide();
		AudioHelpers.StopGeigerCounter();
	}

	private void InitializeUI()
	{
		explorModeOptionsButton = GetNode<OptionButton>("%ExplorModeOptionsButton");
		randomExplorButton = GetNode<Button>("%RandomExplorButton");
		if (!selectedBuildingComponent.BuildingResource.IsAerial)
		{
			explorModeOptionsButton.RemoveItem(0); // Removes the first item (index 0)
			explorModeOptionsButton.Select(0); // Preselect "Search high anomaly", now at index 0
			currentexplorMode = ExplorMode.Gradient;
		}
		gradientSearchButton = GetNode<Button>("%GradientSearchButton");
		returnToBaseButton = GetNode<Button>("%ReturnToBaseButton");
		stopExplorbutton = GetNode<Button>("%StopExplorButton");
		trackRobotButton = GetNode<Button>("%TrackRobotButton");
		startExplorButton = GetNode<Button>("%StartExplorButton");
		customPathButton = GetNode<Button>("%CustomPathButton");
		placeBridgeButton = GetNode<Button>("%PlaceBridgeButton");
		liftRobotButton = GetNode<Button>("%LiftRobotButton");
		analyseSampleButton = GetNode<Button>("%AnalyseSampleButton");
		InitializeFragmentSampleSelector();
		placeAntennaButton = GetNode<Button>("%PlaceAntennaButton");
		dropResourcesButton = GetNode<Button>("%DropResourcesButton");
		placeBridgeButton.Hide();
		liftRobotButton.Hide();
		analyseSampleButton.Hide();
		toggleSoundGeigerButton = GetNode<Button>("%ToggleSoundGeigerButton");
		toggleSoundGeigerButton.Icon = mutedGeigerTexture;
		gravAnomValueLabel = GetNode<Label>("%GravAnomValueLabel");
		batteryLabel = GetNode<Label>("%BatteryLabel");
		resourcesCarriedContainer = GetNode<BoxContainer>("%ResourcesCarriedContainer");
		resourceLabel = GetNode<HBoxContainer>("%ResourceLabel");
		woodCountLabel = GetNode<Label>("%WoodCountLabel");
		woodIconRect = GetNode<TextureRect>("%WoodIcon");
		mineralCountLabel = GetNode<Label>("%MineralCountLabel");
		mineralIconRect = GetNode<TextureRect>("%MineralIcon");
		titleLabel = GetNode<Label>("%Title");

		randomExplorButton.Pressed += OnRandomExplorButtonPressed;
		gradientSearchButton.Pressed += OnGradientSearchButtonPressed;
		returnToBaseButton.Pressed += OnReturnToBaseButtonPressed;
		stopExplorbutton.Pressed += OnStopExplorButtonPressed;
		trackRobotButton.Pressed += OnTrackRobotButtonPressed;
		dropResourcesButton.Pressed += OnDropResourcesButtonPressed;
		analyseSampleButton.Pressed += OnAnalyseSampleButtonPressed;
		toggleSoundGeigerButton.Pressed += () =>
		{
			if (AudioHelpers.geigerActive)
			{
				AudioHelpers.StopGeigerCounter();
				toggleSoundGeigerButton.Icon = mutedGeigerTexture; // Show the muted icon
			}
			else
			{
				AudioHelpers.StartGeigerCounter(initialAnomalyValue: selectedBuildingComponent.GetAnomalyReadingAtCurrentPos());
				toggleSoundGeigerButton.Icon = unmutedGeigerTexture; // Show the unmuted icon
			}
		};
		if (SettingManager.Instance.IsTrackingRobot)
		{
			trackRobotButton.Text = "Stop tracking";
		}
		else
		{
			trackRobotButton.Text = "Track Robot";
		}
		explorModeOptionsButton.ItemSelected += OnOptionsButtonItemSelected;
		explorModeOptionsButton.GetPopup().AboutToPopup += OnExplorationModePopupOpened;
		explorModeOptionsButton.GetPopup().PopupHide += OnExplorationModePopupClosed;
		startExplorButton.Pressed += OnStartExplorButtonSelected;
		customPathButton.Pressed += OnCustomPathButtonPressed;


		if (selectedBuildingComponent.BuildingResource.IsAerial)
		{
			titleLabel.Text = "Selected Drone";
			placeBridgeButton.Hide();
			analyseSampleButton.Hide();
			fragmentSampleContainer.Hide();
			liftRobotButton.Show();
			if (IsInstanceValid(resourcesCarriedContainer))
			{
				resourcesCarriedContainer.Hide();
			}
			if (selectedBuildingComponent.IsLifting)
			{
				groundRobotBelowUav = selectedBuildingComponent.AttachedRobot;
				ChangeStateMultiPurposeButton(MultiPurposeButtonState.DropRobot);
			}
			else
			{
				ChangeStateMultiPurposeButton(MultiPurposeButtonState.LiftRobot);
			}
			placeAntennaButton.Hide();
		}
		else
		{
			titleLabel.Text = "Selected Rover";
			placeBridgeButton.Show();
			analyseSampleButton.Show();
			fragmentSampleContainer.Show();
			liftRobotButton.Hide();
			if (IsInstanceValid(resourcesCarriedContainer))
			{
				resourcesCarriedContainer.Show();
			}
			placeBridgeButton.Pressed += OnPlaceBridgeButtonPressed;
			placeAntennaButton.Pressed += OnPlaceAntennaButtonPressed;
		}

		UpdateResourceLabel();
	}

	private void InitializeFragmentSampleSelector()
	{
		fragmentSampleContainer = new VBoxContainer
		{
			Name = "FragmentSampleAvailability",
			CustomMinimumSize = new Vector2(220f, 0f)
		};

		Container buttonParent = analyseSampleButton.GetParent<Container>();
		buttonParent.AddChild(fragmentSampleContainer);
		buttonParent.MoveChild(fragmentSampleContainer, analyseSampleButton.GetIndex() + 1);
	}

	private int previousSampleCount = -1;

	private void RefreshNearbySampleAvailability()
	{
		if (!GodotObject.IsInstanceValid(selectedBuildingComponent) ||
			selectedBuildingComponent.BuildingResource.IsAerial)
		{
			nearbyFragmentSamples.Clear();
			selectedFragmentSample = null;
			if (IsInstanceValid(analyseSampleButton)) analyseSampleButton.Disabled = true;
			previousSampleCount = 0;
			return;
		}

		Vector2I? previousSelection = selectedFragmentSample;
		IReadOnlyList<Vector2I> positions = selectedBuildingComponent.gridManager
			.GetSampleLocationsAroundPosition(selectedBuildingComponent.GetGridCellPosition());

		nearbyFragmentSamples.Clear();
		foreach (Vector2I position in positions)
		{
			nearbyFragmentSamples.Add(baseLevel?.GetFragmentAnalysisStatus(position) ??
				new FragmentSampleAvailability
				{
					Position = position,
					Status = FragmentSampleAnalysisStatus.Available
				});
		}

		int selectedIndex = previousSelection.HasValue
			? nearbyFragmentSamples.FindIndex(sample => sample.Position == previousSelection.Value)
			: -1;
		if (selectedIndex < 0 && nearbyFragmentSamples.Count > 0) selectedIndex = 0;
		selectedFragmentSample = selectedIndex >= 0 ? nearbyFragmentSamples[selectedIndex].Position : null;

		int newCount = nearbyFragmentSamples.Count;
		if (newCount != previousSampleCount)
		{
			previousSampleCount = newCount;
			if (newCount > 0)
			{
				string statusText = GetFragmentStatusText(nearbyFragmentSamples[selectedIndex >= 0 ? selectedIndex : 0]);
				GameUI.PushMessage(
					$"ROVER: {newCount} fragment{(newCount == 1 ? "" : "s")} in range — {statusText.ToLowerInvariant()}",
					"cyan", false, selectedBuildingComponent);
			}
		}

		analyseSampleButton.Disabled = !CanAnalyseSelectedSample();
	}

	private void OnFragmentAnalysisStatusChanged(Vector2I fragmentPosition)
	{
		RefreshNearbySampleAvailability();
	}

	private static string GetFragmentStatusText(FragmentSampleAvailability sample)
	{
		string status = sample.Status switch
		{
			FragmentSampleAnalysisStatus.Analysing => "ANALYSING",
			FragmentSampleAnalysisStatus.PreviouslyAnalysed => "PREVIOUSLY ANALYSED",
			FragmentSampleAnalysisStatus.Completed => "COMPLETED",
			FragmentSampleAnalysisStatus.Solved => "SOLVED",
			_ => "NOT YET ANALYSED"
		};
		return sample.IsRestored ? $"{status} · RESTORED" : status;
	}

	private bool CanAnalyseSelectedSample()
	{
		if (!selectedFragmentSample.HasValue) return false;
		int index = nearbyFragmentSamples.FindIndex(
			sample => sample.Position == selectedFragmentSample.Value);
		return index >= 0 && nearbyFragmentSamples[index].Status != FragmentSampleAnalysisStatus.Analysing;
	}

	private void OnGroundRobotBelowUav(BuildingComponent groundRobot)
	{
		if (GodotObject.IsInstanceValid(selectedBuildingComponent) &&
			selectedBuildingComponent.BuildingResource.IsAerial &&
			!selectedBuildingComponent.IsLifting)
		{
			groundRobotBelowUav = groundRobot;
		}
	}

	private void OnNoGroundRobotBelowUav()
	{
		if (GodotObject.IsInstanceValid(selectedBuildingComponent) &&
			selectedBuildingComponent.BuildingResource.IsAerial &&
			!selectedBuildingComponent.IsLifting)
		{
			groundRobotBelowUav = null;
		}
	}

	private void OnNoMoreRobotSelected(BuildingComponent component)
	{
		if (component != selectedBuildingComponent) return;
		// Stop Geiger counter when robot is deselected
		AudioHelpers.StopGeigerCounter();
		DisconnectSignals();
		QueueFree();
	}

	private void OnBuildingStuck(BuildingComponent component)
	{
	}

	private void OnBuildingUnStuck(BuildingComponent component)
	{
	}

	private void OnAllRobotsStopped()
	{
	}

	private void OnRandomExplorButtonPressed()
	{
		selectedBuildingComponent.EnableRandomMode();
	}

	private void OnGradientSearchButtonPressed()
	{
		selectedBuildingComponent.EnableGradientSearchMode();
	}

	private void OnReturnToBaseButtonPressed()
	{
		selectedBuildingComponent.EnableReturnToBase();
	}


	private void OnOptionsButtonItemSelected(long index)
	{
		if (!selectedBuildingComponent.BuildingResource.IsAerial)
		{
			if(index == 0)
			{
				currentexplorMode = ExplorMode.Gradient;
			}
			else if (index == 1)
			{
				currentexplorMode = ExplorMode.ReturnToBase;
			}
		}
		else if (selectedBuildingComponent.BuildingResource.IsAerial)
		{
			if (index == 0)
			{
				currentexplorMode = ExplorMode.Random;
			}
			else if (index == 1)
			{
				currentexplorMode = ExplorMode.Gradient;
			}
			else if (index == 2)
			{
				currentexplorMode = ExplorMode.ReturnToBase;
			}
		}
		GameEvents.EmitExplorationModeSelected(selectedBuildingComponent, currentexplorMode.ToString());
	}

	private void OnStartExplorButtonSelected()
	{
		GameEvents.EmitExplorationStarted(selectedBuildingComponent, currentexplorMode.ToString());
		if (currentexplorMode == ExplorMode.None && selectedBuildingComponent.BuildingResource.IsAerial)
		{
			//assume it's random
			currentexplorMode = ExplorMode.Random;
			OnRandomExplorButtonPressed();
		}
		else if (currentexplorMode == ExplorMode.Random) OnRandomExplorButtonPressed();
		else if (currentexplorMode == ExplorMode.Gradient) OnGradientSearchButtonPressed();
		else if (currentexplorMode == ExplorMode.ReturnToBase) OnReturnToBaseButtonPressed();
	}

	private void OnStopExplorButtonPressed()
	{
		currentexplorMode = ExplorMode.None;
		selectedBuildingComponent.StopAnyAutomatedMovementMode();
	}

	private void OnCustomPathButtonPressed()
	{
		GameEvents.EmitCustomPathRequested(selectedBuildingComponent);
	}

	private void OnTrackRobotButtonPressed()
	{
		if (trackRobotButton.Text == "Stop tracking")
		{
			SettingManager.EmitStopTrackingRobot();
			trackRobotButton.Text = "Track Robot";
		}
		else
		{
			SettingManager.EmitTrackingRobot(selectedBuildingComponent);
			trackRobotButton.Text = "Stop tracking";
		}
	}

	private void OnDropResourcesButtonPressed()
	{
		selectedBuildingComponent.TryDropResourcesAtBase();
	}

	private void OnPlaceAntennaButtonPressed()
	{
		GameEvents.EmitPlaceAntennaButtonPressed(selectedBuildingComponent, antennaBuildingResource);
	}

	private void SetAnomalySignal()
	{
		selectedBuildingComponent.NewAnomalyReading += OnNewAnomalyReading;
		int initialAnomaly = selectedBuildingComponent.GetAnomalyReadingAtCurrentPos();
		UpdateAnomalyDisplay(initialAnomaly);
		
		// Keep the geiger counter muted until the player explicitly enables it.
		AudioHelpers.StopGeigerCounter();
		toggleSoundGeigerButton.Icon = mutedGeigerTexture;
	}

	private void SetBatterySignal()
	{
		selectedBuildingComponent.BatteryChange += OnBatteryChange;
		UpdateBatteryDisplay(selectedBuildingComponent.Battery);
	}

	private void SetResourceSignal()
	{
		UpdateResourceLabel();
	}

	private void UpdateResourceLabel()
	{
		if (!IsInstanceValid(resourceLabel) || selectedBuildingComponent == null)
		{
			return;
		}

		int carriedCount = selectedBuildingComponent.resourceCollected.Count;
		int woodCount = 0;
		int mineralCount = 0;

		foreach (string resourceType in selectedBuildingComponent.resourceCollected)
		{
			if (resourceType == "wood")
			{
				woodCount++;
			}
			else
			{
				mineralCount++;
			}
		}

		if (IsInstanceValid(woodCountLabel))
		{
			woodCountLabel.Text = woodCount.ToString();
		}
		if (IsInstanceValid(woodIconRect))
		{
			woodIconRect.Texture = woodIconTexture;
		}
		if (IsInstanceValid(mineralCountLabel))
		{
			mineralCountLabel.Text = mineralCount.ToString();
		}
		if (IsInstanceValid(mineralIconRect))
		{
			mineralIconRect.Texture = mineralIconTexture;
		}
	}

	public void OnBatteryChange(int value)
	{
		if (IsInstanceValid(batteryLabel))
		{
			UpdateBatteryDisplay(value);
		}
	}

	private void UpdateBatteryDisplay(int value)
	{
		if (!IsInstanceValid(batteryLabel))
		{
			return;
		}

		int maxBattery = selectedBuildingComponent?.BuildingResource?.BatteryMax ?? 100;
		int batteryPercent = Mathf.Clamp((int)Math.Round((value / (float)maxBattery) * 100f), 0, 100);
		int segments = 10;
		int filledSegments = Mathf.Clamp((int)Math.Round(batteryPercent / 10f), 0, segments);

		char[] bar = new char[segments];
		Array.Fill(bar, '·');
		for (int i = 0; i < filledSegments; i++)
		{
			bar[i] = '█';
		}

		batteryLabel.Text = $"BAT [{new string(bar)}]\n{value} moves left";
	}

	public void HideUI()
	{
		// Stop Geiger counter when UI is hidden
		AudioHelpers.StopGeigerCounter();
		Visible = false;
	}

	public void OnNewAnomalyReading(int value)
	{
		UpdateAnomalyDisplay(value);

		// Don't refresh minimap here - OnBuildingMovedForMinimap already handles it
		// This was causing double-refresh on every move
		
		// Update Geiger counter with new reading
		AudioHelpers.UpdateAnomalyReading(value);
	}

	private void UpdateAnomalyDisplay(int value)
	{
		if (!IsInstanceValid(gravAnomValueLabel))
		{
			return;
		}

		anomalyHistory.Enqueue(value);
		while (anomalyHistory.Count > MaxAnomalyHistoryEntries)
		{
			anomalyHistory.Dequeue();
		}

		char[] bar = new char[AnomalyBarLength];
		Array.Fill(bar, '·');

		var historyValues = anomalyHistory.ToArray();
		int currentIndex = MapAnomalyToBarIndex(value);
		bar[currentIndex] = '█';

		for (int i = 0; i < historyValues.Length; i++)
		{
			int historyValue = historyValues[i];
			int historyIndex = MapAnomalyToBarIndex(historyValue);
			if (historyIndex == currentIndex)
			{
				continue;
			}

			char marker = i == historyValues.Length - 1 ? '▒' : i == historyValues.Length - 2 ? '░' : '·';
			if (bar[historyIndex] == '·')
			{
				bar[historyIndex] = marker;
			}
		}

		string trend = GetTrendIndicator(historyValues);
		gravAnomValueLabel.Text = $"ANOM [{new string(bar)}] {trend} {value}/{(int)MaxAnomalyValuePossible}";
	}

	private int MapAnomalyToBarIndex(int anomalyValue)
	{
		return Mathf.Clamp((int)Math.Round((anomalyValue / MaxAnomalyValuePossible) * (AnomalyBarLength - 1)), 0, AnomalyBarLength - 1);
	}

	private string GetTrendIndicator(IReadOnlyList<int> historyValues)
	{
		if (historyValues.Count < 2)
		{
			return "→";
		}

		int firstCount = Math.Max(1, historyValues.Count / 2);
		int firstSum = 0;
		int secondSum = 0;

		for (int i = 0; i < firstCount; i++)
		{
			firstSum += historyValues[i];
		}

		for (int i = historyValues.Count - firstCount; i < historyValues.Count; i++)
		{
			secondSum += historyValues[i];
		}

		double firstAverage = firstSum / (double)firstCount;
		double secondAverage = secondSum / (double)firstCount;
		double delta = secondAverage - firstAverage;

		if (delta > 20)
		{
			return "▲";
		}
		if (delta < -20)
		{
			return "▼";
		}
		return "→";
	}

	private void OnBuildingMovedForMinimap(BuildingComponent movedBuilding)
	{
		// Only update if it's the selected robot that moved
		if (movedBuilding != selectedBuildingComponent) return;

		if (IsInstanceValid(miniMapController))
		{
			miniMapController.SetRobotCell(selectedBuildingComponent.GetGridCellPosition());
		}
		RefreshNearbySampleAvailability();
	}

	public void OnResourceCarriedCountChanged(int carriedResourceCount)
	{
		UpdateResourceLabel();
	}

	private void DisconnectSignals()
	{
		batteryTutorialTarget?.Dispose();
		batteryTutorialTarget = null;
		explorationModeTutorialTarget?.Dispose();
		explorationModeTutorialTarget = null;
		startExplorationTutorialTarget?.Dispose();
		startExplorationTutorialTarget = null;
		dropResourcesTutorialTarget?.Dispose();
		dropResourcesTutorialTarget = null;
		resourcesCarriedTutorialTarget?.Dispose();
		resourcesCarriedTutorialTarget = null;
		anomalyRadarTutorialTarget?.Dispose();
		anomalyRadarTutorialTarget = null;
		anomalyIndicatorTutorialTarget?.Dispose();
		anomalyIndicatorTutorialTarget = null;
		placeBridgeTutorialTarget?.Dispose();
		placeBridgeTutorialTarget = null;
		liftRobotTutorialTarget?.Dispose();
		liftRobotTutorialTarget = null;
		customPathTutorialTarget?.Dispose();
		customPathTutorialTarget = null;
		analyseSampleTutorialTarget?.Dispose();
		analyseSampleTutorialTarget = null;
		placeAntennaTutorialTarget?.Dispose();
		placeAntennaTutorialTarget = null;
		if (signalsDisconnected) return;
		signalsDisconnected = true;
		// Safely disconnect signals before the object is freed - check connection first
		if (randomExplorButton.IsConnected("pressed", Callable.From(OnRandomExplorButtonPressed)))
		{
			randomExplorButton.Pressed -= OnRandomExplorButtonPressed;
		}
		if (stopExplorbutton.IsConnected("pressed", Callable.From(OnStopExplorButtonPressed)))
		{
			stopExplorbutton.Pressed -= OnStopExplorButtonPressed;
		}
		if (trackRobotButton.IsConnected("pressed", Callable.From(OnTrackRobotButtonPressed)))
		{
			trackRobotButton.Pressed -= OnTrackRobotButtonPressed;
		}
		if (IsInstanceValid(placeBridgeButton) && placeBridgeButton.IsConnected("pressed", Callable.From(OnPlaceBridgeButtonPressed)))
		{
			placeBridgeButton.Pressed -= OnPlaceBridgeButtonPressed;
		}
		if (IsInstanceValid(liftRobotButton) && liftRobotButton.IsConnected("pressed", Callable.From(OnLiftRobotButtonPressed)))
		{
			liftRobotButton.Pressed -= OnLiftRobotButtonPressed;
		}
		if (IsInstanceValid(liftRobotButton) && liftRobotButton.IsConnected("pressed", Callable.From(OnDropRobotButtonPressed)))
		{
			liftRobotButton.Pressed -= OnDropRobotButtonPressed;
		}
		if (gradientSearchButton.IsConnected("pressed", Callable.From(OnGradientSearchButtonPressed)))
		{
			gradientSearchButton.Pressed -= OnGradientSearchButtonPressed;
		}
		if (returnToBaseButton.IsConnected("pressed", Callable.From(OnReturnToBaseButtonPressed)))
		{
			returnToBaseButton.Pressed -= OnReturnToBaseButtonPressed;
		}
		if (explorModeOptionsButton.IsConnected("item_selected", Callable.From<long>(OnOptionsButtonItemSelected)))
		{
			explorModeOptionsButton.ItemSelected -= OnOptionsButtonItemSelected;
		}
		if (IsInstanceValid(explorModeOptionsButton))
		{
			PopupMenu explorationPopup = explorModeOptionsButton.GetPopup();
			explorationPopup.AboutToPopup -= OnExplorationModePopupOpened;
			explorationPopup.PopupHide -= OnExplorationModePopupClosed;
		}
		if (startExplorButton.IsConnected("pressed", Callable.From(OnStartExplorButtonSelected)))
		{
			startExplorButton.Pressed -= OnStartExplorButtonSelected;
		}
		if (customPathButton.IsConnected("pressed", Callable.From(OnCustomPathButtonPressed)))
		{
			customPathButton.Pressed -= OnCustomPathButtonPressed;
		}
		if(placeAntennaButton.IsConnected("pressed", Callable.From(OnPlaceAntennaButtonPressed)))
		{
			placeAntennaButton.Pressed -= OnPlaceAntennaButtonPressed;
		}
		if(dropResourcesButton.IsConnected("pressed", Callable.From(OnDropResourcesButtonPressed)))
		{
			dropResourcesButton.Pressed -= OnDropResourcesButtonPressed;
		}
		if(analyseSampleButton.IsConnected("pressed", Callable.From(OnAnalyseSampleButtonPressed)))
		{
			analyseSampleButton.Pressed -= OnAnalyseSampleButtonPressed;
		}

		if (baseLevel != null)
		{
			baseLevel.FragmentAnalysisStatusChanged -= OnFragmentAnalysisStatusChanged;
		}
		if (selectedBuildingComponent != null)
		{
			selectedBuildingComponent.ModeChanged -= OnModeChanged;
			selectedBuildingComponent.NewAnomalyReading -= OnNewAnomalyReading;
			selectedBuildingComponent.BatteryChange -= OnBatteryChange;
		}

		if (GameEvents.Instance != null)
		{
			DisconnectGameEvent(GameEvents.SignalName.NoMoreRobotSelected, Callable.From<BuildingComponent>(OnNoMoreRobotSelected));
			DisconnectGameEvent(GameEvents.SignalName.BuildingStuck, Callable.From<BuildingComponent>(OnBuildingStuck));
			DisconnectGameEvent(GameEvents.SignalName.BuildingUnStuck, Callable.From<BuildingComponent>(OnBuildingUnStuck));
			DisconnectGameEvent(GameEvents.SignalName.AllRobotStopped, Callable.From(OnAllRobotsStopped));
			DisconnectGameEvent(GameEvents.SignalName.CarriedResourceCountChanged, Callable.From<int>(OnResourceCarriedCountChanged));
			DisconnectGameEvent(GameEvents.SignalName.GroundRobotBelowUav, Callable.From<BuildingComponent>(OnGroundRobotBelowUav));
			DisconnectGameEvent(GameEvents.SignalName.NoGroundRobotBelowUav, Callable.From(OnNoGroundRobotBelowUav));
		}
		
		// Disconnect minimap building moved event
		if (GameEvents.Instance != null && GameEvents.Instance.IsConnected(GameEvents.SignalName.BuildingMoved, Callable.From<BuildingComponent>(OnBuildingMovedForMinimap)))
		{
			GameEvents.Instance.Disconnect(GameEvents.SignalName.BuildingMoved, Callable.From<BuildingComponent>(OnBuildingMovedForMinimap));
		}
	}

	private static void DisconnectGameEvent(StringName signal, Callable callable)
	{
		if (GameEvents.Instance.IsConnected(signal, callable))
			GameEvents.Instance.Disconnect(signal, callable);
	}

	public override void _ExitTree()
	{
		SetExplorationPopupCursorOverride(false);
		DisconnectSignals();
	}

	private void OnExplorationModePopupOpened()
	{
		SetExplorationPopupCursorOverride(true);
	}

	private void OnExplorationModePopupClosed()
	{
		SetExplorationPopupCursorOverride(false);
	}

	private void SetExplorationPopupCursorOverride(bool enabled)
	{
		GetNodeOrNull<Cursor>("/root/Cursor")?.SetPopupCursorOverride(enabled);
	}

	private void OnAnalyseSampleButtonPressed()
	{
		if (selectedBuildingComponent == null || selectedBuildingComponent.BuildingResource.IsAerial)
		{
			return;
		}
		RefreshNearbySampleAvailability();
		if (!selectedFragmentSample.HasValue)
		{
			GameUI.PushMessage("No sample around to analyse", "red", true);
			return;
		}
		GameEvents.EmitFragmentAnalysisRequested(
			selectedFragmentSample.Value,
			selectedBuildingComponent,
			FragmentAnalysisActionOrigin.Player);
		//selectedBuildingComponent.AnalyseSample();
	}
	private void OnPlaceBridgeButtonPressed()
	{
		GameEvents.EmitPlaceBridgeButtonPressed(selectedBuildingComponent, bridgeBuildingResource);
	}

	private void OnLiftRobotButtonPressed()
	{
		if (selectedBuildingComponent == null || !selectedBuildingComponent.BuildingResource.IsAerial)
		{
			return;
		}

		var dronePos = selectedBuildingComponent.GetGridCellPosition();
		var groundPos = dronePos + Vector2I.Down;
		var robotUnderDrone = selectedBuildingComponent.gridManager.GetRobotAtPosition(groundPos);

		if (robotUnderDrone == null || robotUnderDrone.BuildingResource.IsAerial)
		{
			selectedBuildingComponent.PulseGrappleNoTarget();
			return;
		}

		groundRobotBelowUav = robotUnderDrone;
		selectedBuildingComponent.AttachToRobot(groundRobotBelowUav);
		groundRobotBelowUav.AttachToRobot(selectedBuildingComponent);
		GameEvents.EmitLiftRobotButtonPressed(selectedBuildingComponent, groundRobotBelowUav);
		ChangeStateMultiPurposeButton(MultiPurposeButtonState.DropRobot);
	}

	private void OnDropRobotButtonPressed()
	{
		if (!GodotObject.IsInstanceValid(selectedBuildingComponent) ||
			!selectedBuildingComponent.BuildingResource.IsAerial)
		{
			return;
		}

		BuildingComponent groundRobot = selectedBuildingComponent.AttachedRobot;
		if (!GodotObject.IsInstanceValid(groundRobot))
		{
			groundRobot = groundRobotBelowUav;
		}

		if (!GodotObject.IsInstanceValid(groundRobot))
		{
			selectedBuildingComponent.DetachRobot();
			groundRobotBelowUav = null;
			ChangeStateMultiPurposeButton(MultiPurposeButtonState.LiftRobot);
			return;
		}

		selectedBuildingComponent.DetachRobot();
		groundRobot.DetachRobot();
		ChangeStateMultiPurposeButton(MultiPurposeButtonState.LiftRobot);
		GameEvents.EmitDropRobotButtonPressed(selectedBuildingComponent, groundRobot);
		groundRobotBelowUav = null;
	}

	private void ChangeStateMultiPurposeButton(MultiPurposeButtonState state)
	{
		currentButtonState = state;
		if (liftRobotButton.IsConnected("pressed", Callable.From(OnLiftRobotButtonPressed)))
			liftRobotButton.Pressed -= OnLiftRobotButtonPressed;
		if (liftRobotButton.IsConnected("pressed", Callable.From(OnDropRobotButtonPressed)))
			liftRobotButton.Pressed -= OnDropRobotButtonPressed;

		switch (state)
		{
			case MultiPurposeButtonState.Placebridge:
				liftRobotButton.Text = "Place Bridge";
				liftRobotButton.Disabled = false;
				break;
			case MultiPurposeButtonState.LiftRobot:
				liftRobotButton.Text = "Lift Robot";
				liftRobotButton.Disabled = false;
				liftRobotButton.Pressed += OnLiftRobotButtonPressed;
				break;
			case MultiPurposeButtonState.DropRobot:
				liftRobotButton.Text = "Drop Robot";
				liftRobotButton.Disabled = false;
				liftRobotButton.Pressed += OnDropRobotButtonPressed;
				break;
		}
	}

	private void OnModeChanged(string mode)
	{
	}


}
