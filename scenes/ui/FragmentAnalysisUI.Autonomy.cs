using System;
using System.Collections.Generic;
using Godot;

public partial class FragmentAnalysisUI
{
    [Export]
    private FragmentAutonomySettings autonomySettings = new();

    private sealed class CapabilityControlRow
    {
        public OptionButton Allocation { get; init; }
        public SpinBox Reliability { get; init; }
        public Label EffectiveMode { get; init; }
    }

    private FragmentAnalysisRover fragmentAnalysisRover;
    private FragmentRoverOverlay fragmentRoverOverlay;
	private FragmentRegionSequenceView regionSequenceView;
    private PanelContainer roverPanel;
    private Button roverPanelToggleButton;
	private Button comparisonOpenButton;
    private Label roverCompactStatusLabel;
    private Label fragmentLifecycleLabel;
    private CheckButton autonomyOffButton;
    private CheckButton autonomySupporterButton;
    private CheckButton autonomyPerformerButton;
    private Label roverActivityLabel;
    private Label roverCurrentActionLabel;
    private Label roverNextActionLabel;
    private Label roverTargetLabel;
    private Label roverResultLabel;
    private Label roverLocksLabel;
    private Button roverPauseButton;
    private Button scanFeaturesButton;
    private CheckButton showFeatureOverlayButton;
    private Label selectedFeatureLabel;
	private Button historyBackButton;
	private Button historyForwardButton;
	private OptionButton featureSelector;
    private Button acceptFeatureButton;
    private Button dismissFeatureButton;
    private Button restoreFeatureButton;
	private Button groupRegionsButton;
	private CheckButton showRegionOverlayButton;
	private Button addRegionButton;
	private Label selectedRegionLabel;
	private OptionButton regionSelector;
	private Button acceptRegionButton;
	private Button dismissRegionButton;
	private Button restoreRegionButton;
	private Label navigationIntentLabel;
	private Button navigateToRegionButton;
	private Button cancelNavigationButton;
	private Label targetMetricsLabel;
	private Label processingEffectLabel;
	private OptionButton processingHistorySelector;
	private Button restoreProcessingConfigurationButton;
	private CheckButton bookmarkProcessingConfigurationButton;
	private Button processingHistorySectionButton;
	private Button candidateRegionSectionButton;
	private Button regionSequenceSectionButton;
	private Button featureSensingSectionButton;
	private Control processingHistoryActions;
	private Control candidateRegionActions;
	private Control candidateRegionEdits;
	private Control navigationActions;
	private Control regionSequenceActions;
	private Control featureSensingActions;
	private Control featureEdits;
	private CheckButton regionSequenceButton;
	private Button previousRegionPairButton;
	private Button nextRegionPairButton;
	private Label regionSequenceLabel;
    private Button autonomyAdvancedButton;
    private ScrollContainer capabilityOverridesScroll;
    private VBoxContainer capabilityOverridesContainer;
    private ConfirmationDialog reloadConfirmationDialog;
    private ButtonGroup autonomyModeButtonGroup;
    private readonly Dictionary<FragmentAutonomyCapability, CapabilityControlRow> capabilityControlRows = new();
    private FragmentAnalysisControlState lastControlState;
    private bool autonomySignalsConnected;
    private bool isApplyingAnalysisCommand;
    private bool isSyncingAutonomyUi;
	private bool isSyncingFeatureSelector;
	private bool isSyncingRegionSelector;
	private bool isSyncingProcessingHistory;
	private bool isProcessingHistorySectionExpanded;
	private bool isCandidateRegionSectionExpanded;
	private bool isRegionSequenceSectionExpanded;
	private bool isFeatureSensingSectionExpanded;
	private bool workflowFeatureStage;
	private bool isCompactHeader;

    public event Action<FragmentAnalysisChange> AnalysisChanged;

	private void InitializeAutonomyNodes()
	{
        autonomySettings ??= new FragmentAutonomySettings();
		targetMetricsLabel = GetNode<Label>("%TargetMetricsLabel");
		processingEffectLabel = GetNode<Label>("%ProcessingEffectLabel");

        fragmentAnalysisRover = new FragmentAnalysisRover { Name = "FragmentAnalysisRover" };
        fragmentAnalysisRover.Configure(autonomySettings);
        AddChild(fragmentAnalysisRover);

        PanelContainer analysisFrame = fragmentCanvas.GetParent<PanelContainer>();
        Container originalAnalysisParent = analysisFrame.GetParent<Container>();
        int originalAnalysisIndex = analysisFrame.GetIndex();
        HBoxContainer analysisWorkspace = new()
        {
            Name = "AnalysisWorkspace",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        analysisWorkspace.AddThemeConstantOverride("separation", 12);
        originalAnalysisParent.AddChild(analysisWorkspace);
        originalAnalysisParent.MoveChild(analysisWorkspace, originalAnalysisIndex);
        analysisFrame.Reparent(analysisWorkspace);
        analysisFrame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        analysisFrame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		regionSequenceView = new FragmentRegionSequenceView
		{
			Name = "FragmentRegionSequenceView",
			Visible = false,
			ZIndex = 1,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		analysisFrame.AddChild(regionSequenceView);
		regionSequenceView.SetFeatureColors(
			autonomySettings.RoverFeatureColor,
			autonomySettings.AcceptedRoverFeatureColor,
			autonomySettings.PlayerFeatureColor,
			autonomySettings.PendingFeatureColor);
		regionSequenceView.RegionSelected += OnSequenceRegionSelected;
		regionSequenceView.RegionActionRequested += OnSequenceRegionActionRequested;
		regionSequenceView.RegionLockRequested += OnSequenceRegionLockRequested;
		regionSequenceView.ExitRequested += OnSequenceExitRequested;

        fragmentRoverOverlay = new FragmentRoverOverlay
        {
            Name = "FragmentRoverOverlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
			ZIndex = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        analysisFrame.AddChild(fragmentRoverOverlay);
		fragmentRoverOverlay.SetFeatureColors(
			autonomySettings.RoverFeatureColor,
			autonomySettings.AcceptedRoverFeatureColor,
			autonomySettings.PlayerFeatureColor,
			autonomySettings.PendingFeatureColor);
		fragmentRoverOverlay.SetCandidateRegionColor(autonomySettings.CandidateRegionColor);
		fragmentRoverOverlay.SetNavigationTargetColor(autonomySettings.NavigationTargetColor);

        CreateRoverPanel(analysisWorkspace);
        CreateCompactHeaderControls();

        reloadConfirmationDialog = new ConfirmationDialog
        {
            Name = "ReloadConfirmationDialog",
            Title = "Reload Fragment Analysis",
            DialogText = "Reloading creates a new puzzle and clears Rover annotations, history, " +
                "hypotheses, and accepted direction for the current sample. Continue?",
            OkButtonText = "RELOAD"
        };
        AddChild(reloadConfirmationDialog);

        autonomyModeButtonGroup = new ButtonGroup { AllowUnpress = false };
        autonomyOffButton.ButtonGroup = autonomyModeButtonGroup;
        autonomySupporterButton.ButtonGroup = autonomyModeButtonGroup;
        autonomyPerformerButton.ButtonGroup = autonomyModeButtonGroup;
    }

    private void CreateCompactHeaderControls()
    {
        HBoxContainer header = quitButton.GetParent<HBoxContainer>();
        int quitIndex = quitButton.GetIndex();

        fragmentLifecycleLabel = new Label
        {
            Name = "FragmentLifecycleLabel",
            Text = "SAMPLE: ANALYSING",
            CustomMinimumSize = new Vector2(190, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.AddChild(fragmentLifecycleLabel);
        header.MoveChild(fragmentLifecycleLabel, quitIndex);

        roverCompactStatusLabel = new Label
        {
            Name = "RoverCompactStatusLabel",
            Text = "ROVER: OFF / OFF",
            CustomMinimumSize = new Vector2(190, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.AddChild(roverCompactStatusLabel);
        header.MoveChild(roverCompactStatusLabel, quitIndex + 1);

        roverPanelToggleButton = new Button
        {
            Name = "RoverPanelToggleButton",
            Text = "HIDE ROVER",
            TooltipText = "Show or hide the Rover autonomy panel."
        };
        header.AddChild(roverPanelToggleButton);
        header.MoveChild(roverPanelToggleButton, quitIndex + 2);

		comparisonOpenButton = new Button
		{
			Name = "ComparisonOpenButton",
			Text = "COMPARE REGIONS",
			TooltipText = "Open the side-by-side accepted-region comparison.",
			Disabled = true
		};
		header.AddChild(comparisonOpenButton);
		header.MoveChild(comparisonOpenButton, quitIndex + 3);
    }

    private void UpdateFragmentLifecycleLabel(bool restored, bool solved)
    {
        if (!IsInstanceValid(fragmentLifecycleLabel)) return;
		string openedBy = initiationOrigin == FragmentAnalysisActionOrigin.Rover
			? "ROVER"
			: "PLAYER";
		fragmentLifecycleLabel.Text = "SAMPLE: ACTIVE" +
			$" · {openedBy}" +
            (restored ? " · RESTORED" : string.Empty) +
            (solved ? " · SOLVED" : string.Empty);
		fragmentLifecycleLabel.TooltipText = initiationOrigin == FragmentAnalysisActionOrigin.Rover
			? "Analysis opened after the player approved a Rover proposal."
			: "Analysis opened using the player's Analyse Sample button.";
    }

    private void CreateRoverPanel(HBoxContainer analysisWorkspace)
    {
		PackedScene panelScene = GD.Load<PackedScene>(
			"res://scenes/ui/FragmentAutonomyPanel.tscn");
		roverPanel = panelScene.Instantiate<PanelContainer>();
		analysisWorkspace.AddChild(roverPanel);

		autonomyOffButton = roverPanel.GetNode<CheckButton>("%AutonomyOffButton");
		autonomySupporterButton = roverPanel.GetNode<CheckButton>("%AutonomySupporterButton");
		autonomyPerformerButton = roverPanel.GetNode<CheckButton>("%AutonomyPerformerButton");
		roverActivityLabel = roverPanel.GetNode<Label>("%RoverActivityLabel");
		roverCurrentActionLabel = roverPanel.GetNode<Label>("%RoverCurrentActionLabel");
		roverNextActionLabel = roverPanel.GetNode<Label>("%RoverNextActionLabel");
		roverTargetLabel = roverPanel.GetNode<Label>("%RoverTargetLabel");
		roverResultLabel = roverPanel.GetNode<Label>("%RoverResultLabel");
		roverLocksLabel = roverPanel.GetNode<Label>("%RoverLocksLabel");
		historyBackButton = roverPanel.GetNode<Button>("%HistoryBackButton");
		historyForwardButton = roverPanel.GetNode<Button>("%HistoryForwardButton");
		processingHistorySelector = roverPanel.GetNode<OptionButton>("%ProcessingHistorySelector");
		restoreProcessingConfigurationButton = roverPanel.GetNode<Button>("%RestoreProcessingConfigurationButton");
		bookmarkProcessingConfigurationButton = roverPanel.GetNode<CheckButton>("%BookmarkProcessingConfigurationButton");
		processingHistorySectionButton = roverPanel.GetNode<Button>("%ProcessingHistoryTitle");
		candidateRegionSectionButton = roverPanel.GetNode<Button>("%RegionTitle");
		regionSequenceSectionButton = roverPanel.GetNode<Button>("%SequenceTitle");
		featureSensingSectionButton = roverPanel.GetNode<Button>("%FeatureTitle");
		processingHistoryActions = restoreProcessingConfigurationButton.GetParent<Control>();
		groupRegionsButton = roverPanel.GetNode<Button>("%GroupRegionsButton");
		showRegionOverlayButton = roverPanel.GetNode<CheckButton>("%ShowRegionOverlayButton");
		selectedRegionLabel = roverPanel.GetNode<Label>("%SelectedRegionLabel");
		regionSelector = roverPanel.GetNode<OptionButton>("%RegionSelector");
		acceptRegionButton = roverPanel.GetNode<Button>("%AcceptRegionButton");
		dismissRegionButton = roverPanel.GetNode<Button>("%DismissRegionButton");
		restoreRegionButton = roverPanel.GetNode<Button>("%RestoreRegionButton");
		addRegionButton = roverPanel.GetNode<Button>("%AddRegionButton");
		candidateRegionActions = groupRegionsButton.GetParent<Control>();
		candidateRegionEdits = acceptRegionButton.GetParent<Control>();
		navigationIntentLabel = roverPanel.GetNode<Label>("%NavigationIntentLabel");
		navigateToRegionButton = roverPanel.GetNode<Button>("%NavigateToRegionButton");
		cancelNavigationButton = roverPanel.GetNode<Button>("%CancelNavigationButton");
		navigationActions = navigateToRegionButton.GetParent<Control>();
		regionSequenceLabel = roverPanel.GetNode<Label>("%RegionSequenceLabel");
		previousRegionPairButton = roverPanel.GetNode<Button>("%PreviousRegionPairButton");
		regionSequenceButton = roverPanel.GetNode<CheckButton>("%RegionSequenceButton");
		nextRegionPairButton = roverPanel.GetNode<Button>("%NextRegionPairButton");
		regionSequenceActions = regionSequenceButton.GetParent<Control>();
		scanFeaturesButton = roverPanel.GetNode<Button>("%ScanFeaturesButton");
		showFeatureOverlayButton = roverPanel.GetNode<CheckButton>("%ShowFeatureOverlayButton");
		selectedFeatureLabel = roverPanel.GetNode<Label>("%SelectedFeatureLabel");
		featureSelector = roverPanel.GetNode<OptionButton>("%FeatureSelector");
		acceptFeatureButton = roverPanel.GetNode<Button>("%AcceptFeatureButton");
		dismissFeatureButton = roverPanel.GetNode<Button>("%DismissFeatureButton");
		restoreFeatureButton = roverPanel.GetNode<Button>("%RestoreFeatureButton");
		featureSensingActions = scanFeaturesButton.GetParent<Control>();
		featureEdits = acceptFeatureButton.GetParent<Control>();
		autonomyAdvancedButton = roverPanel.GetNode<Button>("%AutonomyAdvancedButton");
		capabilityOverridesScroll = roverPanel.GetNode<ScrollContainer>("%CapabilityOverridesScroll");
		capabilityOverridesContainer = roverPanel.GetNode<VBoxContainer>("%CapabilityOverridesContainer");
		roverPauseButton = roverPanel.GetNode<Button>("%RoverPauseButton");
	}

    private void InitializeAutonomy(FragmentAutonomyState restoredState)
    {
        lastControlState = CaptureControlState();
        ConnectAutonomySignals();
        fragmentAnalysisRover.Initialize(
            fragmentCanvas,
            this,
            FragmentAutonomyTruth.FromPuzzle(fragmentCanvas.Puzzle),
            restoredState);
        fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		InitializeWorkflowSections();
		UpdateFeatureOverlayView();
        BuildCapabilityOverrideControls();
        RefreshAutonomyUi();
		RefreshFeatureControls();
		RefreshRegionControls();
		RefreshRegionSequence();
    }

    private void ConnectAutonomySignals()
    {
        if (autonomySignalsConnected) return;
        autonomySignalsConnected = true;

        fragmentCanvas.ViewChanged += OnCanvasViewChanged;
        fragmentAnalysisRover.StatusChanged += OnRoverStatusChanged;
        fragmentAnalysisRover.AllocationChanged += RefreshAutonomyUi;
		fragmentAnalysisRover.HistoryChanged += RefreshHistoryButtons;
		fragmentAnalysisRover.ProcessingHistoryChanged += RefreshProcessingHistoryControls;
		fragmentAnalysisRover.FeaturesChanged += OnFeaturesChanged;
		fragmentAnalysisRover.FeatureFocusRequested += OnFeatureFocusRequested;
		fragmentAnalysisRover.RegionsChanged += OnRegionsChanged;
		fragmentAnalysisRover.RegionFocusRequested += OnRegionFocusRequested;
		fragmentAnalysisRover.NavigationTargetChanged += OnNavigationTargetChanged;
		fragmentAnalysisRover.NavigationTargetCleared += OnNavigationTargetCleared;
		fragmentAnalysisRover.NavigationExecutionRequested += OnNavigationExecutionRequested;
		fragmentAnalysisRover.NavigationCancellationRequested += OnNavigationCancellationRequested;
		fragmentAnalysisRover.RegionReviewCompleted += OnRegionReviewCompleted;
		fragmentAnalysisRover.MetricsChanged += OnMetricsChanged;
		fragmentCanvas.Resized += OnFragmentCanvasResized;
		fragmentCanvas.ViewNavigationCompleted += OnViewNavigationCompleted;
		fragmentRoverOverlay.FeatureSelected += OnFeatureSelected;
		fragmentRoverOverlay.PanRequested += OnFeatureOverlayPanRequested;
		fragmentRoverOverlay.ZoomRequested += OnFeatureOverlayZoomRequested;
		fragmentRoverOverlay.RegionSelected += OnRegionSelected;
		fragmentRoverOverlay.RegionDrawn += OnRegionDrawn;
		fragmentRoverOverlay.RegionResized += OnRegionResized;
        roverPanelToggleButton.Pressed += OnRoverPanelTogglePressed;
		comparisonOpenButton.Pressed += OnComparisonOpenPressed;
        autonomyOffButton.Toggled += OnAutonomyOffToggled;
        autonomySupporterButton.Toggled += OnAutonomySupporterToggled;
        autonomyPerformerButton.Toggled += OnAutonomyPerformerToggled;
        roverPauseButton.Pressed += OnRoverPausePressed;
        autonomyAdvancedButton.Pressed += OnAutonomyAdvancedPressed;
        reloadConfirmationDialog.Confirmed += OnReloadConfirmed;
		scanFeaturesButton.Pressed += OnScanFeaturesPressed;
		showFeatureOverlayButton.Toggled += OnFeatureOverlayToggled;
		featureSelector.ItemSelected += OnFeatureSelectorItemSelected;
		acceptFeatureButton.Pressed += OnAcceptFeaturePressed;
		dismissFeatureButton.Pressed += OnDismissFeaturePressed;
		restoreFeatureButton.Pressed += OnRestoreFeaturePressed;
		historyBackButton.Pressed += OnHistoryBackPressed;
		historyForwardButton.Pressed += OnHistoryForwardPressed;
		processingHistorySelector.ItemSelected += OnProcessingHistorySelected;
		restoreProcessingConfigurationButton.Pressed += OnRestoreProcessingConfigurationPressed;
		bookmarkProcessingConfigurationButton.Toggled += OnProcessingBookmarkToggled;
		processingHistorySectionButton.Pressed += OnProcessingHistorySectionPressed;
		candidateRegionSectionButton.Pressed += OnCandidateRegionSectionPressed;
		regionSequenceSectionButton.Pressed += OnRegionSequenceSectionPressed;
		featureSensingSectionButton.Pressed += OnFeatureSensingSectionPressed;
		groupRegionsButton.Pressed += OnGroupRegionsPressed;
		showRegionOverlayButton.Toggled += OnRegionOverlayToggled;
		regionSelector.ItemSelected += OnRegionSelectorItemSelected;
		acceptRegionButton.Pressed += OnAcceptRegionPressed;
		dismissRegionButton.Pressed += OnDismissRegionPressed;
		restoreRegionButton.Pressed += OnRestoreRegionPressed;
		addRegionButton.Pressed += OnAddRegionPressed;
		navigateToRegionButton.Pressed += OnNavigateToRegionPressed;
		cancelNavigationButton.Pressed += OnCancelNavigationPressed;
		regionSequenceButton.Toggled += OnRegionSequenceToggled;
		previousRegionPairButton.Pressed += OnPreviousRegionPairPressed;
		nextRegionPairButton.Pressed += OnNextRegionPairPressed;
		AnalysisChanged += OnRegionSequenceAnalysisChanged;
    }

    private void DisconnectAutonomySignals()
    {
        if (!autonomySignalsConnected) return;
        autonomySignalsConnected = false;

        fragmentCanvas.ViewChanged -= OnCanvasViewChanged;
        fragmentAnalysisRover.StatusChanged -= OnRoverStatusChanged;
        fragmentAnalysisRover.AllocationChanged -= RefreshAutonomyUi;
		fragmentAnalysisRover.HistoryChanged -= RefreshHistoryButtons;
		fragmentAnalysisRover.ProcessingHistoryChanged -= RefreshProcessingHistoryControls;
		fragmentAnalysisRover.FeaturesChanged -= OnFeaturesChanged;
		fragmentAnalysisRover.FeatureFocusRequested -= OnFeatureFocusRequested;
		fragmentAnalysisRover.RegionsChanged -= OnRegionsChanged;
		fragmentAnalysisRover.RegionFocusRequested -= OnRegionFocusRequested;
		fragmentAnalysisRover.NavigationTargetChanged -= OnNavigationTargetChanged;
		fragmentAnalysisRover.NavigationTargetCleared -= OnNavigationTargetCleared;
		fragmentAnalysisRover.NavigationExecutionRequested -= OnNavigationExecutionRequested;
		fragmentAnalysisRover.NavigationCancellationRequested -= OnNavigationCancellationRequested;
		fragmentAnalysisRover.RegionReviewCompleted -= OnRegionReviewCompleted;
		fragmentAnalysisRover.MetricsChanged -= OnMetricsChanged;
		fragmentCanvas.Resized -= OnFragmentCanvasResized;
		fragmentCanvas.ViewNavigationCompleted -= OnViewNavigationCompleted;
		fragmentRoverOverlay.FeatureSelected -= OnFeatureSelected;
		fragmentRoverOverlay.PanRequested -= OnFeatureOverlayPanRequested;
		fragmentRoverOverlay.ZoomRequested -= OnFeatureOverlayZoomRequested;
		fragmentRoverOverlay.RegionSelected -= OnRegionSelected;
		fragmentRoverOverlay.RegionDrawn -= OnRegionDrawn;
		fragmentRoverOverlay.RegionResized -= OnRegionResized;
        roverPanelToggleButton.Pressed -= OnRoverPanelTogglePressed;
		comparisonOpenButton.Pressed -= OnComparisonOpenPressed;
        autonomyOffButton.Toggled -= OnAutonomyOffToggled;
        autonomySupporterButton.Toggled -= OnAutonomySupporterToggled;
        autonomyPerformerButton.Toggled -= OnAutonomyPerformerToggled;
        roverPauseButton.Pressed -= OnRoverPausePressed;
        autonomyAdvancedButton.Pressed -= OnAutonomyAdvancedPressed;
        reloadConfirmationDialog.Confirmed -= OnReloadConfirmed;
		scanFeaturesButton.Pressed -= OnScanFeaturesPressed;
		showFeatureOverlayButton.Toggled -= OnFeatureOverlayToggled;
		featureSelector.ItemSelected -= OnFeatureSelectorItemSelected;
		acceptFeatureButton.Pressed -= OnAcceptFeaturePressed;
		dismissFeatureButton.Pressed -= OnDismissFeaturePressed;
		restoreFeatureButton.Pressed -= OnRestoreFeaturePressed;
		historyBackButton.Pressed -= OnHistoryBackPressed;
		historyForwardButton.Pressed -= OnHistoryForwardPressed;
		processingHistorySelector.ItemSelected -= OnProcessingHistorySelected;
		restoreProcessingConfigurationButton.Pressed -= OnRestoreProcessingConfigurationPressed;
		bookmarkProcessingConfigurationButton.Toggled -= OnProcessingBookmarkToggled;
		processingHistorySectionButton.Pressed -= OnProcessingHistorySectionPressed;
		candidateRegionSectionButton.Pressed -= OnCandidateRegionSectionPressed;
		regionSequenceSectionButton.Pressed -= OnRegionSequenceSectionPressed;
		featureSensingSectionButton.Pressed -= OnFeatureSensingSectionPressed;
		groupRegionsButton.Pressed -= OnGroupRegionsPressed;
		showRegionOverlayButton.Toggled -= OnRegionOverlayToggled;
		regionSelector.ItemSelected -= OnRegionSelectorItemSelected;
		acceptRegionButton.Pressed -= OnAcceptRegionPressed;
		dismissRegionButton.Pressed -= OnDismissRegionPressed;
		restoreRegionButton.Pressed -= OnRestoreRegionPressed;
		addRegionButton.Pressed -= OnAddRegionPressed;
		navigateToRegionButton.Pressed -= OnNavigateToRegionPressed;
		cancelNavigationButton.Pressed -= OnCancelNavigationPressed;
		regionSequenceButton.Toggled -= OnRegionSequenceToggled;
		previousRegionPairButton.Pressed -= OnPreviousRegionPairPressed;
		nextRegionPairButton.Pressed -= OnNextRegionPairPressed;
		AnalysisChanged -= OnRegionSequenceAnalysisChanged;
        fragmentAnalysisRover.Shutdown();
    }

    public FragmentAnalysisControlState CaptureControlState()
    {
        return new FragmentAnalysisControlState
        {
            PolarizationEnabled = polarizationButton.ButtonPressed,
            PolarizationLevel = Mathf.RoundToInt(polarizationSlider.Value),
            SpectralEnabled = spectralButton.ButtonPressed,
            SpectralLevel = Mathf.RoundToInt(spectralSlider.Value),
            SurfaceEnabled = surfaceButton.ButtonPressed,
            SurfaceLevel = Mathf.RoundToInt(surfaceSlider.Value),
            ElectromagneticEnabled = electromagneticButton.ButtonPressed,
            ResonanceEnabled = resonanceButton.ButtonPressed,
            XRayEnabled = xRayButton.ButtonPressed,
            RotationDegrees = fragmentCanvas.DisplayRotationDegrees,
            ViewZoom = fragmentCanvas.ViewZoom,
            ViewPan = fragmentCanvas.ViewPan
        };
    }

    public void DispatchAnalysisCommand(FragmentAnalysisCommand command)
    {
        if (command == null || isApplyingAnalysisCommand) return;

        FragmentAnalysisControlState previous = lastControlState ?? CaptureControlState();
        isApplyingAnalysisCommand = true;
        try
        {
            switch (command.Parameter)
            {
                case FragmentAnalysisParameter.PolarizationEnabled:
                    polarizationButton.ButtonPressed = command.BoolValue;
                    fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Polarization, command.BoolValue);
                    break;
                case FragmentAnalysisParameter.PolarizationLevel:
                    polarizationSlider.Value = Mathf.Clamp(command.IntValue, 1, 5);
                    fragmentCanvas.SetProcessingLevel(
                        FragmentCanvas.FilterType.Polarization,
                        command.IntValue);
                    break;
                case FragmentAnalysisParameter.SpectralEnabled:
                    spectralButton.ButtonPressed = command.BoolValue;
                    fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Spectral, command.BoolValue);
                    break;
                case FragmentAnalysisParameter.SpectralLevel:
                    spectralSlider.Value = Mathf.Clamp(command.IntValue, 1, 5);
                    fragmentCanvas.SetProcessingLevel(FragmentCanvas.FilterType.Spectral, command.IntValue);
                    break;
                case FragmentAnalysisParameter.SurfaceEnabled:
                    surfaceButton.ButtonPressed = command.BoolValue;
                    fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Surface, command.BoolValue);
                    break;
                case FragmentAnalysisParameter.SurfaceLevel:
                    surfaceSlider.Value = Mathf.Clamp(command.IntValue, 1, 5);
                    fragmentCanvas.SetProcessingLevel(FragmentCanvas.FilterType.Surface, command.IntValue);
                    break;
                case FragmentAnalysisParameter.ElectromagneticEnabled:
                    electromagneticButton.ButtonPressed = command.BoolValue;
                    fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Electromagnetic, command.BoolValue);
                    break;
                case FragmentAnalysisParameter.ResonanceEnabled:
                    resonanceButton.ButtonPressed = command.BoolValue;
                    fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Resonance, command.BoolValue);
                    break;
                case FragmentAnalysisParameter.XRayEnabled:
                    xRayButton.ButtonPressed = command.BoolValue;
                    fragmentCanvas.SetFilter(FragmentCanvas.FilterType.XRay, command.BoolValue);
                    break;
                case FragmentAnalysisParameter.Rotation:
                    fragmentCanvas.SetPuzzleRotationDegrees(command.FloatValue);
                    break;
                default:
                    return;
            }

            UpdateProcessingLabels();
            UpdateRotationLabel();
        }
        finally
        {
            isApplyingAnalysisCommand = false;
        }

        FragmentAnalysisControlState current = CaptureControlState();
        lastControlState = current;
        AnalysisChanged?.Invoke(new FragmentAnalysisChange
        {
            Previous = previous,
            Current = current,
            Parameter = command.Parameter,
            Origin = command.Origin
        });
    }

    private void DispatchToggle(FragmentAnalysisParameter parameter, bool enabled)
    {
        if (isApplyingAnalysisCommand) return;
        DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
            parameter,
            enabled,
            FragmentAnalysisActionOrigin.Player));
    }

    private void DispatchLevel(FragmentAnalysisParameter parameter, int level)
    {
        if (isApplyingAnalysisCommand) return;
        DispatchAnalysisCommand(FragmentAnalysisCommand.Level(
            parameter,
            level,
            FragmentAnalysisActionOrigin.Player));
    }

	private void OnCanvasViewChanged(
        float zoom,
        Vector2 pan,
        FragmentAnalysisActionOrigin origin)
    {
		UpdateFeatureOverlayView();
		if (origin == FragmentAnalysisActionOrigin.Player)
			fragmentAnalysisRover?.OverrideNavigationByPlayer();
        FragmentAnalysisControlState previous = lastControlState ?? CaptureControlState();
        FragmentAnalysisControlState current = CaptureControlState();
        lastControlState = current;
        AnalysisChanged?.Invoke(new FragmentAnalysisChange
        {
            Previous = previous,
            Current = current,
            Parameter = FragmentAnalysisParameter.View,
            Origin = origin
        });
    }

	private void OnFragmentCanvasResized()
	{
		UpdateFeatureOverlayView();
		fragmentAnalysisRover?.RefreshDetectedFeatures(true);
	}

	private void UpdateFeatureOverlayView()
	{
		if (!IsInstanceValid(fragmentCanvas) || !IsInstanceValid(fragmentRoverOverlay)) return;
		fragmentRoverOverlay.SetView(
			fragmentCanvas.ObservableSampleSize,
			fragmentCanvas.ViewZoom,
			fragmentCanvas.ViewPan);
	}

	private void OnFeaturesChanged()
	{
		fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		UpdateFeatureOverlayView();
		RefreshFeatureControls();
		RefreshRegionSequence();
	}

	private void OnScanFeaturesPressed()
	{
		fragmentAnalysisRover.RefreshDetectedFeatures(true, recordHistory: true);
	}

	private void OnFeatureOverlayToggled(bool visible)
	{
		fragmentRoverOverlay.SetShowFeatures(visible);
	}

	private void OnFeatureSelected(int featureId)
	{
		fragmentAnalysisRover.OverrideNavigationByPlayer();
		fragmentAnalysisRover.ApplyFeatureEdit(FragmentFeatureEditAction.Select, featureId);
	}

	private void OnFeatureFocusRequested(int featureId)
	{
		if (fragmentAnalysisRover?.IsNavigationInProgress == true) return;
		FragmentDetectedFeature feature = fragmentAnalysisRover.State?.DetectedFeatures.Find(
			candidate => candidate.Id == featureId);
		if (feature == null) return;
		fragmentCanvas.FocusNormalizedPoint(GetFeatureCenter(feature));
	}

	private static Vector2 GetFeatureCenter(FragmentDetectedFeature feature)
	{
		if (feature.Segments.Count == 0) return (feature.Start + feature.End) * 0.5f;
		Vector2 sum = Vector2.Zero;
		foreach (FragmentFeatureSegment segment in feature.Segments)
			sum += (segment.Start + segment.End) * 0.5f;
		return sum / feature.Segments.Count;
	}

	private void OnFeatureOverlayPanRequested(Vector2 delta)
	{
		fragmentCanvas.PanViewBy(delta);
	}

	private void OnFeatureOverlayZoomRequested(Vector2 position, float factor)
	{
		fragmentCanvas.ZoomViewAt(position, factor);
	}

	private void OnFeatureSelectorItemSelected(long index)
	{
		if (isSyncingFeatureSelector || index < 0 || index >= featureSelector.ItemCount) return;
		fragmentAnalysisRover.OverrideNavigationByPlayer();
		fragmentAnalysisRover.ApplyFeatureEdit(
			FragmentFeatureEditAction.Select,
			featureSelector.GetItemId((int)index));
	}

	private void OnAcceptFeaturePressed()
	{
		ApplySelectedFeatureEdit(FragmentFeatureEditAction.Accept);
	}

	private void OnDismissFeaturePressed()
	{
		ApplySelectedFeatureEdit(FragmentFeatureEditAction.Dismiss);
	}

	private void OnRestoreFeaturePressed()
	{
		ApplySelectedFeatureEdit(FragmentFeatureEditAction.Restore);
	}

	private void ApplySelectedFeatureEdit(FragmentFeatureEditAction action)
	{
		if (fragmentAnalysisRover.State?.SelectedFeatureId is not int featureId) return;
		fragmentAnalysisRover.ApplyFeatureEdit(action, featureId);
	}

	private void RefreshFeatureControls()
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		bool includeRoverFeatures = AreRoverFeaturesVisible();
		isSyncingFeatureSelector = true;
		featureSelector.Clear();
		int selectedIndex = -1;
		if (state != null)
		{
			foreach (FragmentDetectedFeature feature in state.DetectedFeatures)
			{
				if (!includeRoverFeatures &&
					feature.Provenance == FragmentAnnotationProvenance.Rover)
				{
					continue;
				}
				string provenance = feature.Provenance == FragmentAnnotationProvenance.Rover
					? "ROVER"
					: "PLAYER";
				int segmentCount = Math.Max(feature.Segments.Count, 1);
				featureSelector.AddItem(
					$"F{feature.Id} · {provenance} · " +
					$"{feature.Disposition.ToString().ToUpperInvariant()} · {segmentCount} STROKES",
					feature.Id);
				if (state.SelectedFeatureId == feature.Id)
					selectedIndex = featureSelector.ItemCount - 1;
			}
		}
		if (selectedIndex >= 0) featureSelector.Select(selectedIndex);
		featureSelector.Visible = isFeatureSensingSectionExpanded && featureSelector.ItemCount > 0;
		isSyncingFeatureSelector = false;

		FragmentDetectedFeature selected = state?.SelectedFeatureId is int selectedId
			? state.DetectedFeatures.Find(feature => feature.Id == selectedId)
			: null;
		if (selected?.Provenance == FragmentAnnotationProvenance.Rover && !includeRoverFeatures)
			selected = null;
		if (selected == null)
		{
			selectedFeatureLabel.Text = "FEATURE: None selected";
			acceptFeatureButton.Disabled = true;
			dismissFeatureButton.Disabled = true;
			restoreFeatureButton.Disabled = true;
			return;
		}

		selectedFeatureLabel.Text =
			$"FEATURE {selected.Id}: {selected.Provenance.ToString().ToUpperInvariant()} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()} · " +
			$"CONF {selected.Confidence:0.00}";
		acceptFeatureButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Accepted;
		dismissFeatureButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		restoreFeatureButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Proposed;
	}

	private bool AreRoverFeaturesVisible()
	{
		return fragmentAnalysisRover?.State != null &&
			fragmentAnalysisRover.State.GlobalMode != FragmentAutonomyMode.Off &&
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.SenseSampleFeatures) != FragmentAutonomyMode.Off;
	}

	private void OnRegionsChanged()
	{
		if (workflowFeatureStage && fragmentAnalysisRover.State.CandidateRegions.Exists(region =>
			region.Disposition == FragmentAnnotationDisposition.Proposed))
		{
			workflowFeatureStage = false;
			SetFeatureSensingSectionExpanded(false);
			SetCandidateRegionSectionExpanded(true);
		}
		fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		RefreshRegionControls();
		RefreshRegionSequence();
	}

	private void OnRegionFocusRequested(int regionId)
	{
		FragmentCandidateRegion region = fragmentAnalysisRover.State?.CandidateRegions.Find(
			candidate => candidate.Id == regionId);
		if (region == null) return;
		if (fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.NavigateSample) == FragmentAutonomyMode.Off)
		{
			fragmentCanvas.FocusNormalizedRect(
				region.NormalizedBounds, FragmentAnalysisActionOrigin.Player);
			return;
		}
		fragmentAnalysisRover.ProposeNavigationTarget(regionId);
	}

	private void OnNavigationTargetChanged(Rect2 bounds, int regionId, bool active)
	{
		fragmentRoverOverlay.SetNavigationTarget(bounds, regionId, active);
		navigationIntentLabel.Text = active
			? $"NAVIGATION: Moving to R{regionId}"
			: $"NAVIGATION: Next target R{regionId}";
		RefreshNavigationControls();
	}

	private void OnNavigationTargetCleared()
	{
		fragmentRoverOverlay.SetNavigationTarget(null, null, false);
		navigationIntentLabel.Text = "NAVIGATION: Select a region";
		RefreshNavigationControls();
	}

	private void OnNavigationExecutionRequested(Rect2 bounds)
	{
		if (regionSequenceButton.ButtonPressed)
			regionSequenceButton.ButtonPressed = false;
		fragmentCanvas.NavigateToNormalizedRect(
			bounds, fragmentAnalysisRover.Settings.NavigationDurationSeconds);
		RefreshNavigationControls();
	}

	private void OnNavigationCancellationRequested()
	{
		fragmentCanvas.CancelViewNavigation();
	}

	private void OnViewNavigationCompleted()
	{
		fragmentAnalysisRover.NotifyNavigationCompleted();
	}

	private void OnNavigateToRegionPressed()
	{
		fragmentAnalysisRover.ApproveNavigation();
	}

	private void OnCancelNavigationPressed()
	{
		fragmentCanvas.CancelViewNavigation();
		fragmentAnalysisRover.CancelNavigationByPlayer();
	}

	private void RefreshNavigationControls()
	{
		bool hasTarget = fragmentAnalysisRover?.NavigationTargetRegionId.HasValue == true;
		navigateToRegionButton.Disabled = !hasTarget || fragmentAnalysisRover.IsNavigationInProgress;
		cancelNavigationButton.Disabled = !hasTarget;
		navigateToRegionButton.Text = fragmentAnalysisRover?.GetEffectiveMode(
			FragmentAutonomyCapability.NavigateSample) == FragmentAutonomyMode.Performer
			? "GO NOW"
			: "GO";
	}

	private void OnRegionReviewCompleted(int acceptedRegionCount)
	{
		workflowFeatureStage = true;
		SetCandidateRegionSectionExpanded(false);
		SetFeatureSensingSectionExpanded(true);
		bool showComparison = acceptedRegionCount >= 2 && !regionSequenceButton.Disabled;
		regionSequenceButton.ButtonPressed = showComparison;
		RefreshFeatureControls();
		if (featureSelector.Visible)
			featureSelector.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void OnMetricsChanged(FragmentSignalMeasurementReport report)
	{
		if (report == null)
		{
			targetMetricsLabel.Text = "SELECTED REGION SIGNAL / NOISE: Rover measurement off";
			processingEffectLabel.Text = "MEASURED CHANGE: UNAVAILABLE";
			return;
		}
		targetMetricsLabel.Text = report.Target == null
			? "SELECTED REGION SIGNAL / NOISE: Select a region"
			: FormatMetrics(
				$"SELECTED REGION R{report.TargetRegionId}",
				report.Target,
				report.PreviousTarget);
		FragmentProcessingHistoryEntry latest = null;
		IReadOnlyList<FragmentProcessingHistoryEntry> history = fragmentAnalysisRover.ProcessingHistory;
		for (int index = history.Count - 1; index >= 0; index--)
		{
			if (fragmentAnalysisRover.ActiveProcessingHistorySequence.HasValue &&
				history[index].Sequence != fragmentAnalysisRover.ActiveProcessingHistorySequence.Value)
				continue;
			if (!fragmentAnalysisRover.ActiveProcessingHistorySequence.HasValue &&
				history[index].TargetRegionId != report.TargetRegionId) continue;
			latest = history[index];
			break;
		}
		processingEffectLabel.Text = latest == null
			? "MEASURED CHANGE: BASELINE"
			: latest.Effect switch
			{
				FragmentProcessingEffect.Improved => $"MEASURED CHANGE: IMPROVED {latest.Delta:+0.00;-0.00;0.00}",
				FragmentProcessingEffect.Degraded => $"MEASURED CHANGE: DEGRADED {latest.Delta:+0.00;-0.00;0.00}",
				FragmentProcessingEffect.LittleChange => "MEASURED CHANGE: LITTLE CHANGE",
				_ => "MEASURED CHANGE: BASELINE"
			};
		RefreshProcessingHistoryControls();
	}

	private static string FormatMetrics(
		string title,
		FragmentSignalMetrics current,
		FragmentSignalMetrics previous)
	{
		return $"{title} · SIGNAL / NOISE: " +
			FormatMetric(current.SignalToNoise, previous?.SignalToNoise);
	}

	private static string FormatMetric(float value, float? previous)
	{
		string delta = previous.HasValue
			? $" · Δ {value - previous.Value:+0.00;-0.00;0.00}"
			: string.Empty;
		return $"{value:0.00}{delta}";
	}

	private void OnRegionSelected(int regionId)
	{
		fragmentAnalysisRover.ApplyRegionEdit(FragmentRegionEditAction.Select, regionId);
	}

	private void OnRegionDrawn(Rect2 normalizedBounds)
	{
		addRegionButton.Text = "DRAW REGION";
		fragmentAnalysisRover.AddPlayerRegion(normalizedBounds);
	}

	private void OnRegionResized(int regionId, Rect2 normalizedBounds)
	{
		fragmentAnalysisRover.ResizeRegion(regionId, normalizedBounds);
	}

	private void OnGroupRegionsPressed()
	{
		fragmentAnalysisRover.RefreshCandidateRegions(true);
	}

	private void OnRegionOverlayToggled(bool visible)
	{
		fragmentRoverOverlay.SetShowRegions(visible);
	}

	private void OnAddRegionPressed()
	{
		bool armed = addRegionButton.Text == "CANCEL DRAW";
		if (!armed)
		{
			showRegionOverlayButton.ButtonPressed = true;
			fragmentRoverOverlay.SetShowRegions(true);
		}
		fragmentRoverOverlay.SetRegionDrawingArmed(!armed);
		addRegionButton.Text = armed ? "DRAW REGION" : "CANCEL DRAW";
	}

	private void OnRegionSelectorItemSelected(long index)
	{
		if (isSyncingRegionSelector || index < 0 || index >= regionSelector.ItemCount) return;
		fragmentAnalysisRover.ApplyRegionEdit(
			FragmentRegionEditAction.Select,
			regionSelector.GetItemId((int)index));
	}

	private void OnAcceptRegionPressed() => ApplySelectedRegionEdit(FragmentRegionEditAction.Accept);
	private void OnDismissRegionPressed() => ApplySelectedRegionEdit(FragmentRegionEditAction.Dismiss);
	private void OnRestoreRegionPressed() => ApplySelectedRegionEdit(FragmentRegionEditAction.Restore);

	private void ApplySelectedRegionEdit(FragmentRegionEditAction action)
	{
		if (fragmentAnalysisRover.State?.SelectedRegionId is int regionId)
			fragmentAnalysisRover.ApplyRegionEdit(
				action,
				regionId,
				applyCropOnAccept: !regionSequenceView.Visible);
	}

	private void RefreshRegionControls()
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		bool includeRover = AreRoverRegionsVisible();
		isSyncingRegionSelector = true;
		regionSelector.Clear();
		int selectedIndex = -1;
		if (state != null)
		{
			foreach (FragmentCandidateRegion region in state.CandidateRegions)
			{
				if (!includeRover && region.Provenance == FragmentAnnotationProvenance.Rover) continue;
				string provenance = region.Provenance.ToString().ToUpperInvariant();
				regionSelector.AddItem(
					$"R{region.Id} · {provenance} · {region.Disposition.ToString().ToUpperInvariant()} · " +
					$"{region.FeatureIds.Count} FEATURES",
					region.Id);
				if (state.SelectedRegionId == region.Id) selectedIndex = regionSelector.ItemCount - 1;
			}
		}
		if (selectedIndex >= 0) regionSelector.Select(selectedIndex);
		regionSelector.Visible = isCandidateRegionSectionExpanded && regionSelector.ItemCount > 0;
		isSyncingRegionSelector = false;

		FragmentCandidateRegion selected = state?.SelectedRegionId is int selectedId
			? state.CandidateRegions.Find(region => region.Id == selectedId)
			: null;
		if (selected?.Provenance == FragmentAnnotationProvenance.Rover && !includeRover) selected = null;
		if (selected == null)
		{
			selectedRegionLabel.Text = "REGION: None selected";
			acceptRegionButton.Disabled = true;
			dismissRegionButton.Disabled = true;
			restoreRegionButton.Disabled = true;
			return;
		}
		selectedRegionLabel.Text =
			$"REGION {selected.Id}: {selected.Provenance.ToString().ToUpperInvariant()} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()} · CONF {selected.Confidence:0.00}";
		acceptRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Accepted;
		dismissRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		restoreRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Proposed;
	}

	private bool AreRoverRegionsVisible()
	{
		return fragmentAnalysisRover?.State != null &&
			fragmentAnalysisRover.State.GlobalMode != FragmentAutonomyMode.Off &&
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.InterpretSignalRegions) != FragmentAutonomyMode.Off;
	}

	private void OnRegionSequenceToggled(bool visible)
	{
		regionSequenceView.Visible = visible && regionSequenceView.RegionCount >= 2;
		fragmentRoverOverlay.Visible = !regionSequenceView.Visible;
		comparisonOpenButton.Disabled = regionSequenceView.RegionCount < 2 || regionSequenceView.Visible;
		RefreshRegionSequenceControls();
	}

	private void OnComparisonOpenPressed()
	{
		if (regionSequenceView.RegionCount >= 2)
			regionSequenceButton.ButtonPressed = true;
	}

	private void OnSequenceExitRequested()
	{
		regionSequenceButton.ButtonPressed = false;
	}

	private void OnPreviousRegionPairPressed()
	{
		regionSequenceView.PreviousPage();
		RefreshRegionSequenceControls();
	}

	private void OnNextRegionPairPressed()
	{
		regionSequenceView.NextPage();
		RefreshRegionSequenceControls();
	}

	private void OnRegionSequenceAnalysisChanged(FragmentAnalysisChange change)
	{
		if (change?.Parameter != FragmentAnalysisParameter.View) RefreshRegionSequence();
	}

	private void RefreshRegionSequence()
	{
		if (!IsInstanceValid(regionSequenceView) || fragmentAnalysisRover?.State == null) return;
		List<FragmentCandidateRegion> visibleRegions = new();
		List<FragmentDetectedFeature> visibleFeatures = new();
		bool includeRover = AreRoverRegionsVisible();
		bool includeRoverFeatures = AreRoverFeaturesVisible();
		foreach (FragmentCandidateRegion region in fragmentAnalysisRover.State.CandidateRegions)
		{
			if (region.Provenance == FragmentAnnotationProvenance.Rover && !includeRover) continue;
			visibleRegions.Add(region);
		}
		foreach (FragmentDetectedFeature feature in fragmentAnalysisRover.State.DetectedFeatures)
		{
			if (feature.Provenance == FragmentAnnotationProvenance.Rover && !includeRoverFeatures) continue;
			visibleFeatures.Add(feature);
		}
		regionSequenceView.SetContent(
			fragmentCanvas.CaptureObservableScan(),
			visibleRegions,
			visibleFeatures,
			fragmentAnalysisRover.State.LockedRegionViews,
			fragmentAnalysisRover.State.SelectedFeatureId,
			fragmentAnalysisRover.State.SelectedRegionId);
		bool available = regionSequenceView.RegionCount >= 2;
		regionSequenceButton.Disabled = !available;
		comparisonOpenButton.Disabled = !available || regionSequenceView.Visible;
		if (!available)
		{
			regionSequenceButton.ButtonPressed = false;
			regionSequenceView.Visible = false;
			fragmentRoverOverlay.Visible = true;
		}
		RefreshRegionSequenceControls();
	}

	private void RefreshRegionSequenceControls()
	{
		regionSequenceLabel.Text = regionSequenceView.PageText;
		previousRegionPairButton.Disabled = !regionSequenceView.Visible || !regionSequenceView.CanGoPrevious;
		nextRegionPairButton.Disabled = !regionSequenceView.Visible || !regionSequenceView.CanGoNext;
	}

	private void OnSequenceRegionSelected(int regionId)
	{
		fragmentAnalysisRover.ApplyRegionEdit(FragmentRegionEditAction.Select, regionId);
	}

	private void OnSequenceRegionActionRequested(int regionId, FragmentRegionEditAction action)
	{
		fragmentAnalysisRover.ApplyRegionEdit(action, regionId, applyCropOnAccept: false);
	}

	private void OnSequenceRegionLockRequested(int regionId)
	{
		fragmentAnalysisRover.ToggleRegionViewLock(regionId);
	}

    private void UpdateProcessingLabels()
    {
        polarizationValueLabel.Text =
            $"POLARIZATION LEVEL: {Mathf.RoundToInt(polarizationSlider.Value)}";
        spectralValueLabel.Text =
            $"SPECTRAL LEVEL: {Mathf.RoundToInt(spectralSlider.Value)}";
        surfaceValueLabel.Text =
            $"SURFACE LEVEL: {Mathf.RoundToInt(surfaceSlider.Value)}";
    }

    private void ShowReloadConfirmation()
    {
        reloadConfirmationDialog.PopupCentered();
    }

    private void OnReloadConfirmed()
    {
        fragmentCanvas.GenerateFragment();
        wasEverSolved = false;
        isRestoredSession = false;
        UpdateFragmentLifecycleLabel(false, false);
        fragmentAnalysisRover.ResetForPuzzle(
            FragmentAutonomyTruth.FromPuzzle(fragmentCanvas.Puzzle));
        fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
        lastControlState = CaptureControlState();
        UpdateRotationLabel();
        RefreshAutonomyUi();
    }

    private void OnAutonomyOffToggled(bool pressed)
    {
        if (!pressed || isSyncingAutonomyUi) return;
        fragmentAnalysisRover.SetMode(FragmentAutonomyMode.Off);
    }

    private void OnAutonomySupporterToggled(bool pressed)
    {
        if (!pressed || isSyncingAutonomyUi) return;
        fragmentAnalysisRover.SetMode(FragmentAutonomyMode.Supporter);
    }

    private void OnAutonomyPerformerToggled(bool pressed)
    {
        if (!pressed || isSyncingAutonomyUi) return;
        fragmentAnalysisRover.SetMode(FragmentAutonomyMode.Performer);
    }

    private void OnRoverPausePressed()
    {
        fragmentAnalysisRover.SetPaused(!fragmentAnalysisRover.State.IsPaused);
        RefreshAutonomyUi();
    }

    private void OnAutonomyAdvancedPressed()
    {
        capabilityOverridesScroll.Visible = !capabilityOverridesScroll.Visible;
        autonomyAdvancedButton.Text = capabilityOverridesScroll.Visible
            ? "HIDE TASK ALLOCATION"
            : "TASK ALLOCATION";
    }

    private void OnRoverPanelTogglePressed()
    {
        roverPanel.Visible = !roverPanel.Visible;
		UpdateResponsiveHeader(true);
    }

	private void UpdateResponsiveHeader(bool force = false)
	{
		if (!IsInstanceValid(roverPanelToggleButton)) return;
		bool compact = GetViewport().GetVisibleRect().Size.X < 2200f;
		if (!force && compact == isCompactHeader) return;
		isCompactHeader = compact;
		fragmentLifecycleLabel.Visible = !compact;
		roverCompactStatusLabel.CustomMinimumSize = compact
			? Vector2.Zero
			: new Vector2(190f, 0f);
		roverPanelToggleButton.Text = compact
			? (roverPanel.Visible ? "ROVER ◀" : "ROVER ▶")
			: (roverPanel.Visible ? "HIDE ROVER" : "SHOW ROVER");
		comparisonOpenButton.Text = compact ? "COMPARE" : "COMPARE REGIONS";
	}

	private void OnRoverStatusChanged(FragmentRoverActionStatus status)
    {
        if (status == null) return;
        roverActivityLabel.Text = $"STATUS: {status.Activity.ToString().ToUpperInvariant()}";
        roverCurrentActionLabel.Text = $"CURRENT: {status.CurrentAction}";
        roverNextActionLabel.Text = $"NEXT: {status.NextAction}";
        roverTargetLabel.Text = $"TARGET: {status.CurrentTarget}";
        roverResultLabel.Text = $"RESULT: {status.MeasuredResult}";
        roverLocksLabel.Text = $"LOCKED: {status.LockedParameters}";
        roverCompactStatusLabel.Text =
            $"ROVER: {fragmentAnalysisRover.State?.GlobalMode.ToString().ToUpperInvariant() ?? "OFF"} / " +
            status.Activity.ToString().ToUpperInvariant();
		RefreshHistoryButtons();
	}

	private void OnHistoryBackPressed()
	{
		fragmentAnalysisRover.UndoLastAction();
	}

	private void InitializeWorkflowSections()
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		bool regionReviewComplete = state != null &&
			state.CandidateRegions.Exists(region =>
				region.Disposition == FragmentAnnotationDisposition.Accepted) &&
			!state.CandidateRegions.Exists(region =>
				region.Disposition == FragmentAnnotationDisposition.Proposed);
		workflowFeatureStage = regionReviewComplete;
		SetProcessingHistorySectionExpanded(false);
		SetCandidateRegionSectionExpanded(!regionReviewComplete);
		SetRegionSequenceSectionExpanded(false);
		SetFeatureSensingSectionExpanded(regionReviewComplete);
	}

	private void OnProcessingHistorySectionPressed() =>
		SetProcessingHistorySectionExpanded(!isProcessingHistorySectionExpanded);

	private void OnCandidateRegionSectionPressed() =>
		SetCandidateRegionSectionExpanded(!isCandidateRegionSectionExpanded);

	private void OnRegionSequenceSectionPressed() =>
		SetRegionSequenceSectionExpanded(!isRegionSequenceSectionExpanded);

	private void OnFeatureSensingSectionPressed() =>
		SetFeatureSensingSectionExpanded(!isFeatureSensingSectionExpanded);

	private void SetProcessingHistorySectionExpanded(bool expanded)
	{
		isProcessingHistorySectionExpanded = expanded;
		processingHistorySectionButton.Text =
			expanded ? "▼ TESTED CONFIGURATIONS" : "▶ TESTED CONFIGURATIONS";
		processingHistorySelector.Visible = expanded && processingHistorySelector.ItemCount > 0;
		processingHistoryActions.Visible = expanded;
	}

	private void SetCandidateRegionSectionExpanded(bool expanded)
	{
		isCandidateRegionSectionExpanded = expanded;
		candidateRegionSectionButton.Text = expanded ? "▼ CANDIDATE REGIONS" : "▶ CANDIDATE REGIONS";
		candidateRegionActions.Visible = expanded;
		selectedRegionLabel.Visible = expanded;
		regionSelector.Visible = expanded && regionSelector.ItemCount > 0;
		candidateRegionEdits.Visible = expanded;
		navigationIntentLabel.Visible = expanded;
		navigationActions.Visible = expanded;
	}

	private void SetRegionSequenceSectionExpanded(bool expanded)
	{
		isRegionSequenceSectionExpanded = expanded;
		regionSequenceSectionButton.Text = expanded ? "▼ REGION SEQUENCE" : "▶ REGION SEQUENCE";
		regionSequenceLabel.Visible = expanded;
		regionSequenceActions.Visible = expanded;
	}

	private void SetFeatureSensingSectionExpanded(bool expanded)
	{
		isFeatureSensingSectionExpanded = expanded;
		featureSensingSectionButton.Text = expanded ? "▼ FEATURE SENSING" : "▶ FEATURE SENSING";
		featureSensingActions.Visible = expanded;
		selectedFeatureLabel.Visible = expanded;
		featureSelector.Visible = expanded && featureSelector.ItemCount > 0;
		featureEdits.Visible = expanded;
	}

	private void OnHistoryForwardPressed()
	{
		fragmentAnalysisRover.RedoLastAction();
	}

	private void RefreshHistoryButtons()
	{
		historyBackButton.Disabled = fragmentAnalysisRover == null || !fragmentAnalysisRover.CanUndo;
		historyForwardButton.Disabled = fragmentAnalysisRover == null || !fragmentAnalysisRover.CanRedo;
	}

	private void RefreshProcessingHistoryControls()
	{
		if (!IsInstanceValid(processingHistorySelector) || fragmentAnalysisRover == null) return;
		int selectedSequence = processingHistorySelector.Selected >= 0
			? processingHistorySelector.GetItemId(processingHistorySelector.Selected)
			: -1;
		isSyncingProcessingHistory = true;
		processingHistorySelector.Clear();
		int selectedIndex = -1;
		IReadOnlyList<FragmentProcessingHistoryEntry> history = fragmentAnalysisRover.ProcessingHistory;
		for (int index = 0; index < history.Count; index++)
		{
			FragmentProcessingHistoryEntry entry = history[index];
			string marker = entry.IsBookmarked ? "★ " : string.Empty;
			string effect = entry.Effect switch
			{
				FragmentProcessingEffect.Improved => "improved",
				FragmentProcessingEffect.Degraded => "degraded",
				FragmentProcessingEffect.LittleChange => "little change",
				_ => "baseline"
			};
			processingHistorySelector.AddItem(
				$"{marker}#{entry.Sequence} · R{entry.TargetRegionId} · S/N {entry.Metrics.SignalToNoise:0.00} · {effect}",
				entry.Sequence);
			if (entry.Sequence == selectedSequence) selectedIndex = index;
		}
		if (selectedIndex < 0 && processingHistorySelector.ItemCount > 0)
			selectedIndex = processingHistorySelector.ItemCount - 1;
		if (selectedIndex >= 0) processingHistorySelector.Select(selectedIndex);
		isSyncingProcessingHistory = false;
		processingHistorySelector.Visible = isProcessingHistorySectionExpanded &&
			processingHistorySelector.ItemCount > 0;
		RefreshSelectedProcessingHistoryEntry();
	}

	private void OnProcessingHistorySelected(long index)
	{
		if (isSyncingProcessingHistory) return;
		RefreshSelectedProcessingHistoryEntry();
	}

	private void RefreshSelectedProcessingHistoryEntry()
	{
		bool selected = processingHistorySelector.Selected >= 0 &&
			processingHistorySelector.Selected < processingHistorySelector.ItemCount;
		restoreProcessingConfigurationButton.Disabled = !selected;
		bookmarkProcessingConfigurationButton.Disabled = !selected;
		isSyncingProcessingHistory = true;
		bool bookmarked = false;
		if (selected)
		{
			int sequence = processingHistorySelector.GetItemId(processingHistorySelector.Selected);
			foreach (FragmentProcessingHistoryEntry entry in fragmentAnalysisRover.ProcessingHistory)
				if (entry.Sequence == sequence) bookmarked = entry.IsBookmarked;
		}
		bookmarkProcessingConfigurationButton.ButtonPressed = bookmarked;
		isSyncingProcessingHistory = false;
	}

	private void OnRestoreProcessingConfigurationPressed()
	{
		if (processingHistorySelector.Selected < 0) return;
		fragmentAnalysisRover.RestoreProcessingConfiguration(
			processingHistorySelector.GetItemId(processingHistorySelector.Selected));
	}

	private void OnProcessingBookmarkToggled(bool bookmarked)
	{
		if (isSyncingProcessingHistory || processingHistorySelector.Selected < 0) return;
		fragmentAnalysisRover.SetProcessingConfigurationBookmarked(
			processingHistorySelector.GetItemId(processingHistorySelector.Selected),
			bookmarked);
	}

    private void BuildCapabilityOverrideControls()
    {
        capabilityControlRows.Clear();
        foreach (Node child in capabilityOverridesContainer.GetChildren())
            child.QueueFree();

        foreach (FragmentAutonomyCapability capability in FragmentAutonomyCapabilityCatalog.All)
        {
            VBoxContainer row = new();
            Label title = new()
            {
                Text = FragmentAutonomyCapabilityCatalog.GetDisplayName(capability),
                TooltipText = "Override the global allocation for this task."
            };
            HBoxContainer controls = new();
            OptionButton allocation = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = "Use the global mode or override this task."
            };
            allocation.AddItem("GLOBAL", 0);
            allocation.AddItem("OFF", 1);
            allocation.AddItem("SUPPORTER", 2);
            allocation.AddItem("PERFORMER", 3);

            FragmentCapabilityRating supportRating =
                FragmentAutonomyCapabilityCatalog.GetSupportRating(capability);
            FragmentCapabilityRating performerRating =
                FragmentAutonomyCapabilityCatalog.GetPerformerRating(capability);
            allocation.SetItemDisabled(2, supportRating == FragmentCapabilityRating.Red);
            allocation.SetItemDisabled(3, performerRating == FragmentCapabilityRating.Red);

            SpinBox reliability = new()
            {
                MinValue = 0,
                MaxValue = 1,
                Step = 0.05,
                CustomMinimumSize = new Vector2(82, 0),
                TooltipText = "Reliability used when this task has a Yellow rating."
            };
            Label effectiveMode = new()
            {
                TooltipText = "The effective mode after applying capability restrictions."
            };

            controls.AddChild(allocation);
            controls.AddChild(reliability);
            row.AddChild(title);
            row.AddChild(controls);
            row.AddChild(effectiveMode);
            capabilityOverridesContainer.AddChild(row);

            capabilityControlRows[capability] = new CapabilityControlRow
            {
                Allocation = allocation,
                Reliability = reliability,
                EffectiveMode = effectiveMode
            };

            allocation.ItemSelected += selectedIndex =>
                OnCapabilityAllocationSelected(capability, selectedIndex);
            reliability.ValueChanged += value =>
                OnCapabilityReliabilityChanged(capability, value);
        }
    }

    private void OnCapabilityAllocationSelected(
        FragmentAutonomyCapability capability,
        long selectedIndex)
    {
        if (isSyncingAutonomyUi) return;
        FragmentAutonomyMode? mode = selectedIndex switch
        {
            1 => FragmentAutonomyMode.Off,
            2 => FragmentAutonomyMode.Supporter,
            3 => FragmentAutonomyMode.Performer,
            _ => null
        };
        fragmentAnalysisRover.SetCapabilityOverride(capability, mode);
    }

    private void OnCapabilityReliabilityChanged(
        FragmentAutonomyCapability capability,
        double value)
    {
        if (isSyncingAutonomyUi) return;
        fragmentAnalysisRover.SetYellowReliability(capability, (float)value);
    }

    private void RefreshAutonomyUi()
    {
        if (fragmentAnalysisRover?.State == null) return;

        isSyncingAutonomyUi = true;
        try
        {
            FragmentAutonomyState state = fragmentAnalysisRover.State;
            autonomyOffButton.ButtonPressed = state.GlobalMode == FragmentAutonomyMode.Off;
            autonomySupporterButton.ButtonPressed = state.GlobalMode == FragmentAutonomyMode.Supporter;
            autonomyPerformerButton.ButtonPressed = state.GlobalMode == FragmentAutonomyMode.Performer;
            roverPauseButton.Disabled = state.GlobalMode == FragmentAutonomyMode.Off;
            roverPauseButton.Text = state.IsPaused ? "RESUME" : "PAUSE";
            scanFeaturesButton.Disabled = state.IsPaused || fragmentAnalysisRover.GetEffectiveMode(
                FragmentAutonomyCapability.SenseSampleFeatures) == FragmentAutonomyMode.Off;
			fragmentRoverOverlay.SetShowRoverFeatures(AreRoverFeaturesVisible());
			groupRegionsButton.Disabled = state.IsPaused || fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.InterpretSignalRegions) == FragmentAutonomyMode.Off;
			addRegionButton.Disabled = false;
			fragmentRoverOverlay.SetShowRoverRegions(AreRoverRegionsVisible());

            foreach ((FragmentAutonomyCapability capability, CapabilityControlRow row) in capabilityControlRows)
            {
                row.Allocation.Selected = state.CapabilityOverrides.TryGetValue(
                    capability,
                    out FragmentAutonomyMode overrideMode)
                    ? (int)overrideMode + 1
                    : 0;
                row.Reliability.Value = fragmentAnalysisRover.GetYellowReliability(capability);
                FragmentAutonomyMode effectiveMode = fragmentAnalysisRover.GetEffectiveMode(capability);
                FragmentCapabilityRating rating = effectiveMode switch
                {
                    FragmentAutonomyMode.Supporter =>
                        FragmentAutonomyCapabilityCatalog.GetSupportRating(capability),
                    FragmentAutonomyMode.Performer =>
                        FragmentAutonomyCapabilityCatalog.GetPerformerRating(capability),
                    _ => FragmentCapabilityRating.Red
                };
                row.EffectiveMode.Text = effectiveMode == FragmentAutonomyMode.Off
                    ? "Effective: OFF"
                    : $"Effective: {effectiveMode.ToString().ToUpperInvariant()} · {rating.ToString().ToUpperInvariant()}";
            }
        }
        finally
        {
            isSyncingAutonomyUi = false;
        }

        OnRoverStatusChanged(fragmentAnalysisRover.Status);
		RefreshFeatureControls();
		RefreshRegionControls();
		RefreshRegionSequence();
		RefreshNavigationControls();
		OnMetricsChanged(fragmentAnalysisRover.MeasurementReport);
    }
}
