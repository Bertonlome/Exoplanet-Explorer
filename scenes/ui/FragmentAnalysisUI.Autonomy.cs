using System;
using System.Collections.Generic;
using Godot;
using Game.UI;

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
	private Label processingSearchPlanLabel;
	private Button processingSearchStartButton;
	private Button processingSearchApplyButton;
	private Button processingSearchSkipButton;
	private Button processingSearchBackButton;
	private Button processingSearchForwardButton;
	private TextureButton polarizationLockButton;
	private TextureButton spectralLockButton;
	private TextureButton surfaceLockButton;
	private TextureButton electromagneticLockButton;
	private TextureButton resonanceLockButton;
	private TextureButton xRayLockButton;
	private Button processingHistorySectionButton;
	private Button candidateRegionSectionButton;
	private Button regionSequenceSectionButton;
	private Button featureSensingSectionButton;
	private Button structureSectionButton;
	private Button fragmentOverviewSectionButton;
	private Control fragmentOverviewContent;
	private TextureRect fragmentOverviewTexture;
	private Label fragmentOverviewCaption;
	private Button orientationSectionButton;
	private Control orientationRegionControls;
	private Button previousOrientationRegionButton;
	private Label orientationRegionLabel;
	private Button nextOrientationRegionButton;
	private Control orientationActions;
	private Button estimateOrientationButton;
	private CheckButton showOrientationOverlayButton;
	private Label selectedOrientationLabel;
	private OptionButton orientationSelector;
	private Label orientationEvidenceLabel;
	private Control orientationEdits;
	private Button previousOrientationButton;
	private Label orientationStepLabel;
	private Button acceptOrientationButton;
	private Button nextOrientationButton;
	private Button quitOrientationViewButton;
	private Button correctionSectionButton;
	private Control correctionActions;
	private Button proposeCorrectionButton;
	private Label correctionLabel;
	private Control correctionEditor;
	private SpinBox correctionDegreesSpinBox;
	private Label correctionDirectionLabel;
	private Control correctionEdits;
	private Button acceptCorrectionButton;
	private Button rejectCorrectionButton;
	private Button arrowSectionButton;
	private Control arrowActions;
	private Button detectArrowsButton;
	private CheckButton showArrowOverlayButton;
	private Label arrowLabel;
	private OptionButton arrowSelector;
	private Control arrowNavigation;
	private Button previousArrowButton;
	private Label arrowStepLabel;
	private Button nextArrowButton;
	private Control arrowManual;
	private CheckButton drawArrowButton;
	private Control arrowEdits;
	private Button acceptArrowButton;
	private Button rejectArrowButton;
	private Button restoreArrowButton;
	private Button directionSectionButton;
	private Control directionActions;
	private Button mapDirectionButton;
	private Label directionStatusLabel;
	private FragmentDirectionInset directionInset;
	private Button scanStructuresButton;
	private CheckButton showStructureOverlayButton;
	private Label selectedStructureLabel;
	private OptionButton structureSelector;
	private Button newStructureButton;
	private CheckButton editStructureButton;
	private Button mergeStructureButton;
	private Button acceptStructureButton;
	private Button dismissStructureButton;
	private Button restoreStructureButton;
	private Control processingHistoryActions;
	private Control candidateRegionActions;
	private Control candidateRegionEdits;
	private Button regionViewLockButton;
	private Control navigationActions;
	private Control regionSequenceActions;
	private Control featureSensingActions;
	private Control featureEdits;
	private Control structureActions;
	private Control structureMembershipEdits;
	private Control structureDispositionEdits;
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
	private bool isProcessingHistoryDirty;
	private bool isCandidateRegionSectionExpanded;
	private bool isRegionSequenceSectionExpanded;
	private bool isFeatureSensingSectionExpanded;
	private bool isStructureSectionExpanded;
	private bool isFragmentOverviewSectionExpanded;
	private bool isOrientationSectionExpanded;
	private bool isCorrectionSectionExpanded;
	private bool isArrowSectionExpanded;
	private bool isSyncingArrowSelector;
	private bool isDirectionSectionExpanded;
	private bool isSyncingCorrection;
	private bool isSyncingOrientationSelector;
	private bool isSyncingStructureSelector;
	private int? mergeTargetStructureId;
	private bool workflowFeatureStage;
	private bool isCompactHeader;
	private bool isRegionSequenceRefreshPending;

    public event Action<FragmentAnalysisChange> AnalysisChanged;

	private void InitializeAutonomyNodes()
	{
        autonomySettings ??= new FragmentAutonomySettings();
		if (!FragmentDirectionMapper.ValidateCoordinateContract(out string directionError))
			GD.PushError($"Fragment direction coordinate contract failed: {directionError}");
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
		regionSequenceView.PageChanged += OnRegionSequencePageChanged;
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
		fragmentRoverOverlay.SetStructureColor(autonomySettings.StructureColor);
		fragmentRoverOverlay.SetOrientationColors(
			autonomySettings.OrientationColor,
			autonomySettings.OrientationReferenceColor,
			autonomySettings.OrientationGhostColor);
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
		processingSearchPlanLabel = GetNode<Label>("%ProcessingSearchPlanLabel");
		processingSearchStartButton = GetNode<Button>("%ProcessingSearchStartButton");
		processingSearchApplyButton = GetNode<Button>("%ProcessingSearchApplyButton");
		processingSearchSkipButton = GetNode<Button>("%ProcessingSearchSkipButton");
		processingSearchBackButton = GetNode<Button>("%ProcessingSearchBackButton");
		processingSearchForwardButton = GetNode<Button>("%ProcessingSearchForwardButton");
		polarizationLockButton = GetNode<TextureButton>("%PolarizationLockButton");
		spectralLockButton = GetNode<TextureButton>("%SpectralLockButton");
		surfaceLockButton = GetNode<TextureButton>("%SurfaceLockButton");
		electromagneticLockButton = GetNode<TextureButton>("%ElectromagneticLockButton");
		resonanceLockButton = GetNode<TextureButton>("%ResonanceLockButton");
		xRayLockButton = GetNode<TextureButton>("%XRayLockButton");
		processingHistorySectionButton = roverPanel.GetNode<Button>("%ProcessingHistoryTitle");
		candidateRegionSectionButton = roverPanel.GetNode<Button>("%RegionTitle");
		regionSequenceSectionButton = roverPanel.GetNode<Button>("%SequenceTitle");
		featureSensingSectionButton = roverPanel.GetNode<Button>("%FeatureTitle");
		structureSectionButton = roverPanel.GetNode<Button>("%StructureTitle");
		fragmentOverviewSectionButton = roverPanel.GetNode<Button>("%FragmentOverviewTitle");
		fragmentOverviewContent = roverPanel.GetNode<Control>("%FragmentOverviewContent");
		fragmentOverviewTexture = roverPanel.GetNode<TextureRect>("%FragmentOverviewTexture");
		fragmentOverviewCaption = roverPanel.GetNode<Label>("%FragmentOverviewCaption");
		orientationSectionButton = roverPanel.GetNode<Button>("%OrientationTitle");
		orientationRegionControls = roverPanel.GetNode<Control>("%OrientationRegionControls");
		previousOrientationRegionButton = roverPanel.GetNode<Button>("%PreviousOrientationRegionButton");
		orientationRegionLabel = roverPanel.GetNode<Label>("%OrientationRegionLabel");
		nextOrientationRegionButton = roverPanel.GetNode<Button>("%NextOrientationRegionButton");
		estimateOrientationButton = roverPanel.GetNode<Button>("%EstimateOrientationButton");
		showOrientationOverlayButton = roverPanel.GetNode<CheckButton>("%ShowOrientationOverlayButton");
		selectedOrientationLabel = roverPanel.GetNode<Label>("%SelectedOrientationLabel");
		orientationSelector = roverPanel.GetNode<OptionButton>("%OrientationSelector");
		orientationEvidenceLabel = roverPanel.GetNode<Label>("%OrientationEvidenceLabel");
		previousOrientationButton = roverPanel.GetNode<Button>("%PreviousOrientationButton");
		orientationStepLabel = roverPanel.GetNode<Label>("%OrientationStepLabel");
		acceptOrientationButton = roverPanel.GetNode<Button>("%AcceptOrientationButton");
		nextOrientationButton = roverPanel.GetNode<Button>("%NextOrientationButton");
		quitOrientationViewButton = roverPanel.GetNode<Button>("%QuitOrientationViewButton");
		orientationActions = estimateOrientationButton.GetParent<Control>();
		orientationEdits = acceptOrientationButton.GetParent<Control>();
		correctionSectionButton = roverPanel.GetNode<Button>("%CorrectionTitle");
		correctionActions = roverPanel.GetNode<Control>("%CorrectionActions");
		proposeCorrectionButton = roverPanel.GetNode<Button>("%ProposeCorrectionButton");
		correctionLabel = roverPanel.GetNode<Label>("%CorrectionLabel");
		correctionEditor = roverPanel.GetNode<Control>("%CorrectionEditor");
		correctionDegreesSpinBox = roverPanel.GetNode<SpinBox>("%CorrectionDegreesSpinBox");
		correctionDirectionLabel = roverPanel.GetNode<Label>("%CorrectionDirectionLabel");
		correctionEdits = roverPanel.GetNode<Control>("%CorrectionEdits");
		acceptCorrectionButton = roverPanel.GetNode<Button>("%AcceptCorrectionButton");
		rejectCorrectionButton = roverPanel.GetNode<Button>("%RejectCorrectionButton");
		arrowSectionButton = roverPanel.GetNode<Button>("%ArrowTitle");
		arrowActions = roverPanel.GetNode<Control>("%ArrowActions");
		detectArrowsButton = roverPanel.GetNode<Button>("%DetectArrowsButton");
		showArrowOverlayButton = roverPanel.GetNode<CheckButton>("%ShowArrowOverlayButton");
		arrowLabel = roverPanel.GetNode<Label>("%ArrowLabel");
		arrowSelector = roverPanel.GetNode<OptionButton>("%ArrowSelector");
		arrowNavigation = roverPanel.GetNode<Control>("%ArrowNavigation");
		previousArrowButton = roverPanel.GetNode<Button>("%PreviousArrowButton");
		arrowStepLabel = roverPanel.GetNode<Label>("%ArrowStepLabel");
		nextArrowButton = roverPanel.GetNode<Button>("%NextArrowButton");
		arrowManual = roverPanel.GetNode<Control>("%ArrowManual");
		drawArrowButton = roverPanel.GetNode<CheckButton>("%DrawArrowButton");
		arrowEdits = roverPanel.GetNode<Control>("%ArrowEdits");
		acceptArrowButton = roverPanel.GetNode<Button>("%AcceptArrowButton");
		rejectArrowButton = roverPanel.GetNode<Button>("%RejectArrowButton");
		restoreArrowButton = roverPanel.GetNode<Button>("%RestoreArrowButton");
		directionSectionButton = roverPanel.GetNode<Button>("%DirectionTitle");
		directionActions = roverPanel.GetNode<Control>("%DirectionActions");
		mapDirectionButton = roverPanel.GetNode<Button>("%MapDirectionButton");
		directionStatusLabel = roverPanel.GetNode<Label>("%DirectionStatusLabel");
		directionInset = roverPanel.GetNode<FragmentDirectionInset>("%DirectionInset");
		processingHistoryActions = restoreProcessingConfigurationButton.GetParent<Control>();
		groupRegionsButton = roverPanel.GetNode<Button>("%GroupRegionsButton");
		showRegionOverlayButton = roverPanel.GetNode<CheckButton>("%ShowRegionOverlayButton");
		selectedRegionLabel = roverPanel.GetNode<Label>("%SelectedRegionLabel");
		regionSelector = roverPanel.GetNode<OptionButton>("%RegionSelector");
		acceptRegionButton = roverPanel.GetNode<Button>("%AcceptRegionButton");
		dismissRegionButton = roverPanel.GetNode<Button>("%DismissRegionButton");
		restoreRegionButton = roverPanel.GetNode<Button>("%RestoreRegionButton");
		addRegionButton = roverPanel.GetNode<Button>("%AddRegionButton");
		regionViewLockButton = roverPanel.GetNode<Button>("%RegionViewLockButton");
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
		scanStructuresButton = roverPanel.GetNode<Button>("%ScanStructuresButton");
		showStructureOverlayButton = roverPanel.GetNode<CheckButton>("%ShowStructureOverlayButton");
		selectedStructureLabel = roverPanel.GetNode<Label>("%SelectedStructureLabel");
		structureSelector = roverPanel.GetNode<OptionButton>("%StructureSelector");
		newStructureButton = roverPanel.GetNode<Button>("%NewStructureButton");
		editStructureButton = roverPanel.GetNode<CheckButton>("%EditStructureButton");
		mergeStructureButton = roverPanel.GetNode<Button>("%MergeStructureButton");
		acceptStructureButton = roverPanel.GetNode<Button>("%AcceptStructureButton");
		dismissStructureButton = roverPanel.GetNode<Button>("%DismissStructureButton");
		restoreStructureButton = roverPanel.GetNode<Button>("%RestoreStructureButton");
		if (scanStructuresButton == null || newStructureButton == null ||
			acceptStructureButton == null)
			throw new InvalidOperationException(
				"FragmentAutonomyPanel.tscn is missing the checkpoint 4.1 structure controls; " +
				"rescan/reimport the scene after updating it.");
		structureActions = scanStructuresButton.GetParent<Control>();
		structureMembershipEdits = newStructureButton.GetParent<Control>();
		structureDispositionEdits = acceptStructureButton.GetParent<Control>();
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
		RefreshStructureControls();
		RefreshOrientationControls();
		RefreshRotationCorrectionControls();
		RefreshArrowControls();
		RefreshDirectionControls();
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
		fragmentAnalysisRover.ProcessingSearchChanged += RefreshProcessingSearchControls;
		fragmentAnalysisRover.FeaturesChanged += OnFeaturesChanged;
		fragmentAnalysisRover.FeatureFocusRequested += OnFeatureFocusRequested;
		fragmentAnalysisRover.RegionsChanged += OnRegionsChanged;
		fragmentAnalysisRover.StructuresChanged += OnStructuresChanged;
		fragmentAnalysisRover.ArrowCandidatesChanged += OnArrowCandidatesChanged;
		fragmentAnalysisRover.DirectionInterpretationChanged += OnDirectionInterpretationChanged;
		fragmentAnalysisRover.OrientationsChanged += OnOrientationsChanged;
		fragmentAnalysisRover.RotationCorrectionChanged += OnRotationCorrectionChanged;
		fragmentAnalysisRover.RotationCorrectionApplied += OnRotationCorrectionApplied;
		fragmentAnalysisRover.RotationExecutionChanged += OnRotationExecutionChanged;
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
		fragmentRoverOverlay.StructureFeatureToggled += OnStructureFeatureToggled;
		fragmentRoverOverlay.StructureEditingCancelled += OnStructureEditingCancelled;
		fragmentRoverOverlay.ArrowDrawn += OnArrowDrawn;
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
		processingSearchStartButton.Pressed += OnProcessingSearchStartPressed;
		processingSearchApplyButton.Pressed += OnProcessingSearchApplyPressed;
		processingSearchSkipButton.Pressed += OnProcessingSearchSkipPressed;
		processingSearchBackButton.Pressed += OnProcessingSearchBackPressed;
		processingSearchForwardButton.Pressed += OnProcessingSearchForwardPressed;
		polarizationLockButton.Toggled += OnPolarizationLockToggled;
		spectralLockButton.Toggled += OnSpectralLockToggled;
		surfaceLockButton.Toggled += OnSurfaceLockToggled;
		electromagneticLockButton.Toggled += OnElectromagneticLockToggled;
		resonanceLockButton.Toggled += OnResonanceLockToggled;
		xRayLockButton.Toggled += OnXRayLockToggled;
		processingHistorySectionButton.Pressed += OnProcessingHistorySectionPressed;
		candidateRegionSectionButton.Pressed += OnCandidateRegionSectionPressed;
		regionSequenceSectionButton.Pressed += OnRegionSequenceSectionPressed;
		featureSensingSectionButton.Pressed += OnFeatureSensingSectionPressed;
		structureSectionButton.Pressed += OnStructureSectionPressed;
		fragmentOverviewSectionButton.Pressed += OnFragmentOverviewSectionPressed;
		orientationSectionButton.Pressed += OnOrientationSectionPressed;
		correctionSectionButton.Pressed += OnCorrectionSectionPressed;
		arrowSectionButton.Pressed += OnArrowSectionPressed;
		directionSectionButton.Pressed += OnDirectionSectionPressed;
		groupRegionsButton.Pressed += OnGroupRegionsPressed;
		showRegionOverlayButton.Toggled += OnRegionOverlayToggled;
		regionSelector.ItemSelected += OnRegionSelectorItemSelected;
		acceptRegionButton.Pressed += OnAcceptRegionPressed;
		dismissRegionButton.Pressed += OnDismissRegionPressed;
		restoreRegionButton.Pressed += OnRestoreRegionPressed;
		addRegionButton.Pressed += OnAddRegionPressed;
		regionViewLockButton.Pressed += OnRegionViewLockPressed;
		navigateToRegionButton.Pressed += OnNavigateToRegionPressed;
		cancelNavigationButton.Pressed += OnCancelNavigationPressed;
		regionSequenceButton.Toggled += OnRegionSequenceToggled;
		previousRegionPairButton.Pressed += OnPreviousRegionPairPressed;
		nextRegionPairButton.Pressed += OnNextRegionPairPressed;
		scanStructuresButton.Pressed += OnScanStructuresPressed;
		showStructureOverlayButton.Toggled += OnStructureOverlayToggled;
		structureSelector.ItemSelected += OnStructureSelectorItemSelected;
		newStructureButton.Pressed += OnNewStructurePressed;
		editStructureButton.Toggled += OnEditStructureToggled;
		mergeStructureButton.Pressed += OnMergeStructurePressed;
		acceptStructureButton.Pressed += OnAcceptStructurePressed;
		dismissStructureButton.Pressed += OnDismissStructurePressed;
		restoreStructureButton.Pressed += OnRestoreStructurePressed;
		estimateOrientationButton.Pressed += OnEstimateOrientationPressed;
		previousOrientationRegionButton.Pressed += OnPreviousOrientationRegionPressed;
		nextOrientationRegionButton.Pressed += OnNextOrientationRegionPressed;
		quitOrientationViewButton.Pressed += OnQuitOrientationViewPressed;
		showOrientationOverlayButton.Toggled += OnOrientationOverlayToggled;
		orientationSelector.ItemSelected += OnOrientationSelectorItemSelected;
		previousOrientationButton.Pressed += OnPreviousOrientationPressed;
		acceptOrientationButton.Pressed += OnAcceptOrientationPressed;
		nextOrientationButton.Pressed += OnNextOrientationPressed;
		proposeCorrectionButton.Pressed += OnProposeCorrectionPressed;
		correctionDegreesSpinBox.ValueChanged += OnCorrectionDegreesChanged;
		acceptCorrectionButton.Pressed += OnAcceptCorrectionPressed;
		rejectCorrectionButton.Pressed += OnRejectCorrectionPressed;
		detectArrowsButton.Pressed += OnDetectArrowsPressed;
		showArrowOverlayButton.Toggled += OnArrowOverlayToggled;
		arrowSelector.ItemSelected += OnArrowSelectorItemSelected;
		previousArrowButton.Pressed += OnPreviousArrowPressed;
		nextArrowButton.Pressed += OnNextArrowPressed;
		drawArrowButton.Toggled += OnDrawArrowToggled;
		acceptArrowButton.Pressed += OnAcceptArrowPressed;
		rejectArrowButton.Pressed += OnRejectArrowPressed;
		restoreArrowButton.Pressed += OnRestoreArrowPressed;
		mapDirectionButton.Pressed += OnMapDirectionPressed;
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
		fragmentAnalysisRover.ProcessingSearchChanged -= RefreshProcessingSearchControls;
		fragmentAnalysisRover.FeaturesChanged -= OnFeaturesChanged;
		fragmentAnalysisRover.FeatureFocusRequested -= OnFeatureFocusRequested;
		fragmentAnalysisRover.RegionsChanged -= OnRegionsChanged;
		fragmentAnalysisRover.StructuresChanged -= OnStructuresChanged;
		fragmentAnalysisRover.ArrowCandidatesChanged -= OnArrowCandidatesChanged;
		fragmentAnalysisRover.DirectionInterpretationChanged -= OnDirectionInterpretationChanged;
		fragmentAnalysisRover.OrientationsChanged -= OnOrientationsChanged;
		fragmentAnalysisRover.RotationCorrectionChanged -= OnRotationCorrectionChanged;
		fragmentAnalysisRover.RotationCorrectionApplied -= OnRotationCorrectionApplied;
		fragmentAnalysisRover.RotationExecutionChanged -= OnRotationExecutionChanged;
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
		fragmentRoverOverlay.StructureFeatureToggled -= OnStructureFeatureToggled;
		fragmentRoverOverlay.StructureEditingCancelled -= OnStructureEditingCancelled;
		fragmentRoverOverlay.ArrowDrawn -= OnArrowDrawn;
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
		processingSearchStartButton.Pressed -= OnProcessingSearchStartPressed;
		processingSearchApplyButton.Pressed -= OnProcessingSearchApplyPressed;
		processingSearchSkipButton.Pressed -= OnProcessingSearchSkipPressed;
		processingSearchBackButton.Pressed -= OnProcessingSearchBackPressed;
		processingSearchForwardButton.Pressed -= OnProcessingSearchForwardPressed;
		polarizationLockButton.Toggled -= OnPolarizationLockToggled;
		spectralLockButton.Toggled -= OnSpectralLockToggled;
		surfaceLockButton.Toggled -= OnSurfaceLockToggled;
		electromagneticLockButton.Toggled -= OnElectromagneticLockToggled;
		resonanceLockButton.Toggled -= OnResonanceLockToggled;
		xRayLockButton.Toggled -= OnXRayLockToggled;
		processingHistorySectionButton.Pressed -= OnProcessingHistorySectionPressed;
		candidateRegionSectionButton.Pressed -= OnCandidateRegionSectionPressed;
		regionSequenceSectionButton.Pressed -= OnRegionSequenceSectionPressed;
		featureSensingSectionButton.Pressed -= OnFeatureSensingSectionPressed;
		structureSectionButton.Pressed -= OnStructureSectionPressed;
		fragmentOverviewSectionButton.Pressed -= OnFragmentOverviewSectionPressed;
		orientationSectionButton.Pressed -= OnOrientationSectionPressed;
		correctionSectionButton.Pressed -= OnCorrectionSectionPressed;
		arrowSectionButton.Pressed -= OnArrowSectionPressed;
		directionSectionButton.Pressed -= OnDirectionSectionPressed;
		groupRegionsButton.Pressed -= OnGroupRegionsPressed;
		showRegionOverlayButton.Toggled -= OnRegionOverlayToggled;
		regionSelector.ItemSelected -= OnRegionSelectorItemSelected;
		acceptRegionButton.Pressed -= OnAcceptRegionPressed;
		dismissRegionButton.Pressed -= OnDismissRegionPressed;
		restoreRegionButton.Pressed -= OnRestoreRegionPressed;
		addRegionButton.Pressed -= OnAddRegionPressed;
		regionViewLockButton.Pressed -= OnRegionViewLockPressed;
		navigateToRegionButton.Pressed -= OnNavigateToRegionPressed;
		cancelNavigationButton.Pressed -= OnCancelNavigationPressed;
		regionSequenceButton.Toggled -= OnRegionSequenceToggled;
		previousRegionPairButton.Pressed -= OnPreviousRegionPairPressed;
		nextRegionPairButton.Pressed -= OnNextRegionPairPressed;
		scanStructuresButton.Pressed -= OnScanStructuresPressed;
		showStructureOverlayButton.Toggled -= OnStructureOverlayToggled;
		structureSelector.ItemSelected -= OnStructureSelectorItemSelected;
		newStructureButton.Pressed -= OnNewStructurePressed;
		editStructureButton.Toggled -= OnEditStructureToggled;
		mergeStructureButton.Pressed -= OnMergeStructurePressed;
		acceptStructureButton.Pressed -= OnAcceptStructurePressed;
		dismissStructureButton.Pressed -= OnDismissStructurePressed;
		restoreStructureButton.Pressed -= OnRestoreStructurePressed;
		estimateOrientationButton.Pressed -= OnEstimateOrientationPressed;
		previousOrientationRegionButton.Pressed -= OnPreviousOrientationRegionPressed;
		nextOrientationRegionButton.Pressed -= OnNextOrientationRegionPressed;
		quitOrientationViewButton.Pressed -= OnQuitOrientationViewPressed;
		showOrientationOverlayButton.Toggled -= OnOrientationOverlayToggled;
		orientationSelector.ItemSelected -= OnOrientationSelectorItemSelected;
		previousOrientationButton.Pressed -= OnPreviousOrientationPressed;
		acceptOrientationButton.Pressed -= OnAcceptOrientationPressed;
		nextOrientationButton.Pressed -= OnNextOrientationPressed;
		proposeCorrectionButton.Pressed -= OnProposeCorrectionPressed;
		correctionDegreesSpinBox.ValueChanged -= OnCorrectionDegreesChanged;
		acceptCorrectionButton.Pressed -= OnAcceptCorrectionPressed;
		rejectCorrectionButton.Pressed -= OnRejectCorrectionPressed;
		detectArrowsButton.Pressed -= OnDetectArrowsPressed;
		showArrowOverlayButton.Toggled -= OnArrowOverlayToggled;
		arrowSelector.ItemSelected -= OnArrowSelectorItemSelected;
		previousArrowButton.Pressed -= OnPreviousArrowPressed;
		nextArrowButton.Pressed -= OnNextArrowPressed;
		drawArrowButton.Toggled -= OnDrawArrowToggled;
		acceptArrowButton.Pressed -= OnAcceptArrowPressed;
		rejectArrowButton.Pressed -= OnRejectArrowPressed;
		restoreArrowButton.Pressed -= OnRestoreArrowPressed;
		mapDirectionButton.Pressed -= OnMapDirectionPressed;
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
		RefreshStructureControls();
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
		if (fragmentAnalysisRover?.IsNavigationInProgress == true ||
			fragmentAnalysisRover?.IsProcessingSearchRunning == true) return;
		FragmentDetectedFeature feature = fragmentAnalysisRover.State?.DetectedFeatures.Find(
			candidate => candidate.Id == featureId);
		if (feature == null) return;
		Vector2 center = GetFeatureCenter(feature);
		if (!fragmentCanvas.IsNormalizedPointVisible(center))
			fragmentCanvas.FocusNormalizedPoint(center);
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
		bool canEditOnCurrentPage = fragmentAnalysisRover.CanEditFeatureOnCurrentReviewPage(selected.Id);
		acceptFeatureButton.Disabled = !canEditOnCurrentPage ||
			selected.Disposition == FragmentAnnotationDisposition.Accepted;
		dismissFeatureButton.Disabled = !canEditOnCurrentPage ||
			selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		restoreFeatureButton.Disabled = !canEditOnCurrentPage ||
			selected.Disposition == FragmentAnnotationDisposition.Proposed;
	}

	private bool AreRoverFeaturesVisible()
	{
		return fragmentAnalysisRover?.State != null &&
			fragmentAnalysisRover.State.GlobalMode != FragmentAutonomyMode.Off &&
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.SenseSampleFeatures) != FragmentAutonomyMode.Off;
	}

	private void OnStructuresChanged()
	{
		fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		RefreshStructureControls();
		RefreshOrientationControls();
		RefreshRotationCorrectionControls();
		RefreshOrientationPresentation();
	}

	private void OnOrientationsChanged()
	{
		fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		RefreshOrientationControls();
		RefreshRotationCorrectionControls();
		RefreshOrientationPresentation(true);
	}

	private void OnRotationCorrectionChanged()
	{
		fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		RefreshRotationCorrectionControls();
		RefreshOrientationPresentation(true);
	}

	private void OnRotationCorrectionApplied(float targetRotation)
	{
		SetOrientationSectionExpanded(false);
		if (IsInstanceValid(regionSequenceButton) && regionSequenceButton.ButtonPressed)
			regionSequenceButton.ButtonPressed = false;
		RefreshOrientationPresentation();
	}

	private void OnRotationExecutionChanged()
	{
		if (fragmentAnalysisRover.IsRotationExecuting)
		{
			if (IsInstanceValid(regionSequenceButton) && regionSequenceButton.ButtonPressed)
				regionSequenceButton.ButtonPressed = false;
			RefreshOrientationPresentation();
		}
		RefreshRotationCorrectionControls();
	}

	private void OnEstimateOrientationPressed() =>
		fragmentAnalysisRover.EstimateOrientationHypotheses(true);

	private void OnPreviousOrientationRegionPressed() => SelectRelativeOrientationRegion(-1);
	private void OnNextOrientationRegionPressed() => SelectRelativeOrientationRegion(1);

	private void SelectRelativeOrientationRegion(int offset)
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		if (state == null) return;
		List<FragmentCandidateRegion> regions = GetOrientationRegions(state);
		if (regions.Count == 0) return;
		int index = regions.FindIndex(region => region.Id == state.SelectedRegionId);
		if (index < 0) index = offset < 0 ? regions.Count - 1 : 0;
		else index = (index + offset + regions.Count) % regions.Count;
		fragmentAnalysisRover.ApplyRegionEdit(
			FragmentRegionEditAction.Select,
			regions[index].Id,
			applyCropOnAccept: false);
		fragmentAnalysisRover.EstimateOrientationHypotheses(true);
	}

	private void OnQuitOrientationViewPressed()
	{
		SetOrientationSectionExpanded(false);
		if (IsInstanceValid(regionSequenceButton) && regionSequenceButton.ButtonPressed)
			regionSequenceButton.ButtonPressed = false;
		RefreshOrientationPresentation();
	}

	private void OnOrientationOverlayToggled(bool visible)
	{
		fragmentRoverOverlay.SetShowOrientations(visible &&
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.InterpretUprightOrientation) != FragmentAutonomyMode.Off);
		if (visible) fragmentRoverOverlay.RestartOrientationPreviewAnimation();
	}

	private void OnOrientationSelectorItemSelected(long index)
	{
		if (isSyncingOrientationSelector || index < 0 || index >= orientationSelector.ItemCount) return;
		fragmentAnalysisRover.ApplyOrientationEdit(
			FragmentOrientationEditAction.Select,
			orientationSelector.GetItemId((int)index));
		fragmentRoverOverlay.RestartOrientationPreviewAnimation();
	}

	private void OnAcceptOrientationPressed() =>
		ApplySelectedOrientationEdit(FragmentOrientationEditAction.Accept);

	private void OnProposeCorrectionPressed() =>
		fragmentAnalysisRover.ProposeRotationCorrection();

	private void OnCorrectionDegreesChanged(double value)
	{
		if (isSyncingCorrection) return;
		fragmentAnalysisRover.AdjustRotationCorrection((float)value);
	}

	private void OnAcceptCorrectionPressed()
	{
		if (fragmentAnalysisRover.IsRotationInProgress)
			fragmentAnalysisRover.CancelRotationCorrectionExecution();
		else
			fragmentAnalysisRover.ApplyRotationCorrectionEdit(
				FragmentRotationCorrectionEditAction.Accept);
	}

	private void OnRejectCorrectionPressed() =>
		fragmentAnalysisRover.ApplyRotationCorrectionEdit(
			FragmentRotationCorrectionEditAction.Reject);

	private void OnPreviousOrientationPressed() => SelectRelativeOrientation(-1);
	private void OnNextOrientationPressed() => SelectRelativeOrientation(1);

	private void SelectRelativeOrientation(int offset)
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		if (state == null || state.OrientationHypotheses.Count == 0) return;
		int index = state.OrientationHypotheses.FindIndex(hypothesis =>
			hypothesis.Id == state.SelectedOrientationId);
		if (index < 0) index = 0;
		else index = (index + offset + state.OrientationHypotheses.Count) %
			state.OrientationHypotheses.Count;
		fragmentAnalysisRover.ApplyOrientationEdit(
			FragmentOrientationEditAction.Select,
			state.OrientationHypotheses[index].Id);
	}

	private void ApplySelectedOrientationEdit(FragmentOrientationEditAction action)
	{
		if (fragmentAnalysisRover.State?.SelectedOrientationId is int hypothesisId)
			fragmentAnalysisRover.ApplyOrientationEdit(action, hypothesisId);
	}

	private void RefreshOrientationControls()
	{
		if (!IsInstanceValid(orientationSelector) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		RefreshOrientationRegionControls(state);
		int selectedId = state.SelectedOrientationId ?? -1;
		isSyncingOrientationSelector = true;
		orientationSelector.Clear();
		int selectedIndex = -1;
		foreach (FragmentOrientationHypothesis hypothesis in state.OrientationHypotheses)
		{
			string disposition = FormatOrientationDisposition(hypothesis.Disposition);
			orientationSelector.AddItem(
				$"H{hypothesis.Id} · {hypothesis.AxisDegrees:+0.0;-0.0;0.0}° · " +
				$"CONF {hypothesis.Confidence:0.00} · {disposition}",
				hypothesis.Id);
			if (hypothesis.Id == selectedId) selectedIndex = orientationSelector.ItemCount - 1;
		}
		if (selectedIndex >= 0) orientationSelector.Select(selectedIndex);
		orientationSelector.Visible = false;
		isSyncingOrientationSelector = false;

		FragmentOrientationHypothesis selected = state.OrientationHypotheses.Find(hypothesis =>
			hypothesis.Id == selectedId);
		FragmentCandidateRegion source = state.SelectedRegionId is int regionId
			? state.CandidateRegions.Find(region =>
				region.Id == regionId &&
				region.Disposition != FragmentAnnotationDisposition.Dismissed)
			: null;
		FragmentAutonomyMode mode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.InterpretUprightOrientation);
		fragmentRoverOverlay.SetShowOrientations(
			showOrientationOverlayButton.ButtonPressed && mode != FragmentAutonomyMode.Off);
		estimateOrientationButton.Disabled = state.IsPaused || mode == FragmentAutonomyMode.Off ||
			source == null;
		if (selected == null)
		{
			selectedOrientationLabel.Text = source == null
					? "ORIENTATION: Select a comparison region"
				: "ORIENTATION: No hypotheses estimated";
			orientationEvidenceLabel.Text =
				"EVIDENCE: Visible line directions only; upright polarity requires player review.";
			acceptOrientationButton.Disabled = true;
			previousOrientationButton.Disabled = true;
			nextOrientationButton.Disabled = true;
			orientationStepLabel.Text = "H—";
			return;
		}
		selectedOrientationLabel.Text =
			$"H{selected.Id}: AXIS {selected.AxisDegrees:+0.0;-0.0;0.0}° · " +
			$"CONF {selected.Confidence:0.00} · " +
			FormatOrientationDisposition(selected.Disposition);
		orientationEvidenceLabel.Text = $"EVIDENCE: {selected.Evidence}";
		orientationStepLabel.Text = $"H{selected.Id}";
		bool hasAlternatives = state.OrientationHypotheses.Count > 1;
		previousOrientationButton.Disabled = !hasAlternatives;
		nextOrientationButton.Disabled = !hasAlternatives;
		acceptOrientationButton.Disabled =
			selected.Disposition == FragmentAnnotationDisposition.Accepted;
	}

	private void RefreshRotationCorrectionControls()
	{
		if (!IsInstanceValid(correctionSectionButton) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		FragmentOrientationHypothesis accepted = state.AcceptedOrientationId is int orientationId
			? state.OrientationHypotheses.Find(hypothesis =>
				hypothesis.Id == orientationId &&
				hypothesis.Disposition == FragmentAnnotationDisposition.Accepted)
			: null;
		FragmentRotationCorrection correction = state.RotationCorrection;
		FragmentAutonomyMode mode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.DecideRotationCorrection);
		FragmentAutonomyMode rotateMode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.Rotate);
		proposeCorrectionButton.Disabled = state.IsPaused || mode == FragmentAutonomyMode.Off ||
			accepted == null || fragmentAnalysisRover.IsRotationInProgress;
		proposeCorrectionButton.Text = correction == null
			? "PROPOSE CORRECTION"
			: "RECALCULATE CORRECTION";
		acceptCorrectionButton.Text = fragmentAnalysisRover.IsRotationInProgress
			? "CANCEL ROTATION"
			: "START ROTATION";

		isSyncingCorrection = true;
		try
		{
			if (correction == null)
			{
				correctionLabel.Text = accepted == null
					? "ORIENTATION ERROR: Accept an H# first"
					: $"ORIENTATION ERROR: H{accepted.Id} ready for calculation";
				correctionDegreesSpinBox.Value = 0;
				correctionDegreesSpinBox.Editable = false;
				correctionDirectionLabel.Text = "NONE";
				acceptCorrectionButton.Disabled = true;
				rejectCorrectionButton.Disabled = true;
				return;
			}

			string direction = FormatCorrectionDirection(correction.ProposedDegrees);
			string disposition = correction.Disposition switch
			{
				FragmentAnnotationDisposition.Accepted => " · APPLYING",
				FragmentAnnotationDisposition.Dismissed => " · REJECTED",
				_ => correction.IsPlayerAdjusted ? " · PLAYER ADJUSTED" : " · ROVER PROPOSAL"
			};
			if (fragmentAnalysisRover.IsRotationPreviewActive)
				disposition = " · PREVIEWING";
			else if (fragmentAnalysisRover.IsRotationExecuting)
				disposition =
					$" · ROTATING {fragmentAnalysisRover.RotationExecutionProgress * 100f:0}%";
			correctionLabel.Text =
				$"ORIENTATION ERROR: {MathF.Abs(correction.ProposedDegrees):0.0}° {direction}" +
				$" · H{correction.SourceOrientationId}{disposition}";
			correctionDegreesSpinBox.Value = correction.ProposedDegrees;
			correctionDegreesSpinBox.Editable =
				correction.Disposition == FragmentAnnotationDisposition.Proposed;
			correctionDirectionLabel.Text = direction;
			acceptCorrectionButton.Disabled = state.IsPaused ||
				rotateMode != FragmentAutonomyMode.Performer ||
				(correction.Disposition != FragmentAnnotationDisposition.Proposed &&
				 !fragmentAnalysisRover.IsRotationInProgress);
			rejectCorrectionButton.Disabled = state.IsPaused ||
				correction.Disposition == FragmentAnnotationDisposition.Dismissed;
		}
		finally
		{
			isSyncingCorrection = false;
		}
	}

	private static string FormatCorrectionDirection(float degrees) =>
		degrees > 0.01f ? "CW" : degrees < -0.01f ? "CCW" : "NONE";

	private void OnArrowCandidatesChanged()
	{
		fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
		RefreshArrowControls();
	}

	private void OnDetectArrowsPressed() =>
		fragmentAnalysisRover.RefreshArrowCandidates(true);

	private void OnArrowOverlayToggled(bool visible) =>
		fragmentRoverOverlay.SetShowArrows(visible);

	private void OnArrowSelectorItemSelected(long index)
	{
		if (isSyncingArrowSelector || index < 0 || index >= arrowSelector.ItemCount) return;
		fragmentAnalysisRover.ApplyArrowEdit(
			FragmentArrowEditAction.Select,
			arrowSelector.GetItemId((int)index));
	}

	private void OnPreviousArrowPressed() => SelectRelativeArrow(-1);
	private void OnNextArrowPressed() => SelectRelativeArrow(1);

	private void SelectRelativeArrow(int offset)
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		if (state == null || state.ArrowCandidates.Count == 0) return;
		int index = state.ArrowCandidates.FindIndex(candidate =>
			candidate.Id == state.SelectedArrowId);
		if (index < 0) index = 0;
		else index = (index + offset + state.ArrowCandidates.Count) % state.ArrowCandidates.Count;
		fragmentAnalysisRover.ApplyArrowEdit(
			FragmentArrowEditAction.Select,
			state.ArrowCandidates[index].Id);
	}

	private void OnDrawArrowToggled(bool armed) =>
		fragmentRoverOverlay.SetArrowDrawingArmed(armed);

	private void OnArrowDrawn(Vector2 tail, Vector2 tip)
	{
		drawArrowButton.SetPressedNoSignal(false);
		fragmentAnalysisRover.DefinePlayerArrow(tail, tip);
	}

	private void OnAcceptArrowPressed() =>
		ApplySelectedArrowEdit(FragmentArrowEditAction.Accept);
	private void OnRejectArrowPressed() =>
		ApplySelectedArrowEdit(FragmentArrowEditAction.Reject);
	private void OnRestoreArrowPressed() =>
		ApplySelectedArrowEdit(FragmentArrowEditAction.Restore);

	private void ApplySelectedArrowEdit(FragmentArrowEditAction action)
	{
		if (fragmentAnalysisRover.State?.SelectedArrowId is int id)
			fragmentAnalysisRover.ApplyArrowEdit(action, id);
	}

	private void RefreshArrowControls()
	{
		if (!IsInstanceValid(arrowSelector) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		int selectedId = state.SelectedArrowId ?? -1;
		isSyncingArrowSelector = true;
		arrowSelector.Clear();
		int selectedIndex = -1;
		foreach (FragmentArrowCandidate candidate in state.ArrowCandidates)
		{
			string source = candidate.IsPlayerDefined ? "PLAYER" : "ROVER";
			arrowSelector.AddItem(
				$"A{candidate.Id} · {source} · {candidate.Disposition.ToString().ToUpperInvariant()}",
				candidate.Id);
			if (candidate.Id == selectedId) selectedIndex = arrowSelector.ItemCount - 1;
		}
		if (selectedIndex >= 0) arrowSelector.Select(selectedIndex);
		arrowSelector.Visible = isArrowSectionExpanded && arrowSelector.ItemCount > 0;
		isSyncingArrowSelector = false;

		FragmentArrowCandidate selected = state.ArrowCandidates.Find(candidate =>
			candidate.Id == selectedId);
		FragmentAutonomyMode mode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.SenseDirectionalArrow);
		detectArrowsButton.Disabled = state.IsPaused || mode == FragmentAutonomyMode.Off;
		fragmentRoverOverlay.SetShowArrows(showArrowOverlayButton.ButtonPressed);
		bool hasAlternatives = state.ArrowCandidates.Count > 1;
		previousArrowButton.Disabled = !hasAlternatives;
		nextArrowButton.Disabled = !hasAlternatives;
		if (selected == null)
		{
			arrowLabel.Text = "ARROW: No candidates; detect geometry or draw tail-to-tip";
			arrowStepLabel.Text = "A—";
			acceptArrowButton.Disabled = true;
			rejectArrowButton.Disabled = true;
			restoreArrowButton.Disabled = true;
			return;
		}
		string sourceLabel = selected.IsPlayerDefined ? "PLAYER-DRAWN" : "GEOMETRY-ONLY ROVER";
		arrowLabel.Text = $"A{selected.Id}: {sourceLabel} · CONF {selected.Confidence:0.00} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()}\nEVIDENCE: {selected.Evidence}";
		arrowStepLabel.Text = $"A{selected.Id}";
		acceptArrowButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Accepted;
		rejectArrowButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		restoreArrowButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Proposed;
	}

	private void OnDirectionInterpretationChanged()
	{
		RefreshDirectionControls();
		if (fragmentAnalysisRover.State?.DirectionInterpretation != null)
			SetDirectionSectionExpanded(true);
	}

	private void OnMapDirectionPressed() =>
		fragmentAnalysisRover.ComputeDirectionInterpretation(true);

	private void RefreshDirectionControls()
	{
		if (!IsInstanceValid(directionInset) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		FragmentDirectionInterpretation mapped = state.DirectionInterpretation;
		directionInset.SetDirection(mapped);
		directionStatusLabel.Text = mapped == null
			? "BEARING: Accept one A# and one H#, then map the direction"
			: FragmentDirectionMapper.FormatBearing(mapped) +
				$"\nSOURCE: A{mapped.SourceArrowId} + H{mapped.SourceOrientationId}" +
				"\nMINIMAP: BEARING RAY ADDED AT FRAGMENT LOCATION";
		bool hasAcceptedArrow = state.AcceptedArrowId is int arrowId &&
			state.ArrowCandidates.Exists(candidate =>
				candidate.Id == arrowId &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted);
		bool hasAcceptedOrientation = state.AcceptedOrientationId is int orientationId &&
			state.OrientationHypotheses.Exists(candidate =>
				candidate.Id == orientationId &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted);
		FragmentAutonomyMode mode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.InterpretMonolithDirection);
		mapDirectionButton.Disabled = state.IsPaused || mode == FragmentAutonomyMode.Off ||
			!hasAcceptedArrow || !hasAcceptedOrientation;
		mapDirectionButton.Text = mapped == null ? "MAP TO WORLD" : "RECOMPUTE BEARING";
		GameUI.Instance?.SetFragmentBearing(
			fragmentPosition,
			mapped?.WorldGridDirection,
			mapped?.CompassLabel);
	}

	private void RefreshOrientationRegionControls(FragmentAutonomyState state)
	{
		List<FragmentCandidateRegion> regions = GetOrientationRegions(state);
		FragmentCandidateRegion selected = regions.Find(region =>
			region.Id == state.SelectedRegionId);
		orientationRegionLabel.Text = selected == null
			? "REGION: —"
			: $"R{selected.Id} · " +
				(fragmentAnalysisRover.IsRegionViewLocked(selected.Id) ? "LOCKED" : "LIVE");
		bool hasAlternatives = regions.Count > 1;
		previousOrientationRegionButton.Disabled = !hasAlternatives;
		nextOrientationRegionButton.Disabled = !hasAlternatives;
	}

	private static List<FragmentCandidateRegion> GetOrientationRegions(FragmentAutonomyState state)
	{
		List<FragmentCandidateRegion> regions = state.CandidateRegions.FindAll(region =>
			region.Disposition != FragmentAnnotationDisposition.Dismissed);
		regions.Sort((first, second) => first.Id.CompareTo(second.Id));
		return regions;
	}

	private static string FormatOrientationDisposition(FragmentAnnotationDisposition disposition) =>
		disposition switch
		{
			FragmentAnnotationDisposition.Accepted => "PLAYER ACCEPTED",
			FragmentAnnotationDisposition.Dismissed => "REJECTED",
			_ => "CANDIDATE"
		};

	private void OnScanStructuresPressed() =>
		fragmentAnalysisRover.RefreshStructures(true);

	private void OnStructureOverlayToggled(bool visible)
	{
		if (!visible) editStructureButton.ButtonPressed = false;
		fragmentRoverOverlay.SetShowStructures(visible);
	}

	private void OnStructureSelectorItemSelected(long index)
	{
		if (isSyncingStructureSelector || index < 0 || index >= structureSelector.ItemCount) return;
		int selectedId = structureSelector.GetItemId((int)index);
		if (mergeTargetStructureId is int targetId && selectedId != targetId)
		{
			mergeTargetStructureId = null;
			mergeStructureButton.Text = "MERGE";
			fragmentAnalysisRover.MergeStructures(targetId, selectedId);
			return;
		}
		fragmentAnalysisRover.ApplyStructureEdit(FragmentStructureEditAction.Select, selectedId);
	}

	private void OnNewStructurePressed()
	{
		mergeTargetStructureId = null;
		mergeStructureButton.Text = "MERGE";
		int id = fragmentAnalysisRover.AddPlayerStructure();
		if (id < 0) return;
		showStructureOverlayButton.ButtonPressed = true;
		showFeatureOverlayButton.ButtonPressed = true;
		editStructureButton.ButtonPressed = true;
	}

	private void OnEditStructureToggled(bool editing)
	{
		if (isSyncingStructureSelector) return;
		if (editing)
		{
			if (regionSequenceButton.ButtonPressed)
				regionSequenceButton.ButtonPressed = false;
			mergeTargetStructureId = null;
			mergeStructureButton.Text = "MERGE";
			showStructureOverlayButton.ButtonPressed = true;
			showFeatureOverlayButton.ButtonPressed = true;
			fragmentRoverOverlay.SetRegionDrawingArmed(false);
			addRegionButton.Text = "DRAW REGION";
		}
		fragmentRoverOverlay.SetStructureEditing(editing);
		RefreshStructureControls();
	}

	private void OnMergeStructurePressed()
	{
		if (mergeTargetStructureId.HasValue)
		{
			mergeTargetStructureId = null;
			mergeStructureButton.Text = "MERGE";
			return;
		}
		if (fragmentAnalysisRover.State?.SelectedStructureId is not int structureId) return;
		mergeTargetStructureId = structureId;
		mergeStructureButton.Text = "CANCEL";
		editStructureButton.ButtonPressed = false;
		selectedStructureLabel.Text = $"STRUCTURE {structureId}: Choose another structure to merge";
	}

	private void OnStructureFeatureToggled(int featureId) =>
		fragmentAnalysisRover.ToggleSelectedStructureFeature(featureId);

	private void OnStructureEditingCancelled()
	{
		isSyncingStructureSelector = true;
		editStructureButton.ButtonPressed = false;
		isSyncingStructureSelector = false;
		RefreshStructureControls();
	}

	private void OnAcceptStructurePressed() =>
		ApplySelectedStructureEdit(FragmentStructureEditAction.Accept);

	private void OnDismissStructurePressed()
	{
		editStructureButton.ButtonPressed = false;
		ApplySelectedStructureEdit(FragmentStructureEditAction.Dismiss);
	}

	private void OnRestoreStructurePressed() =>
		ApplySelectedStructureEdit(FragmentStructureEditAction.Restore);

	private void ApplySelectedStructureEdit(FragmentStructureEditAction action)
	{
		if (fragmentAnalysisRover.State?.SelectedStructureId is int structureId)
			fragmentAnalysisRover.ApplyStructureEdit(action, structureId);
	}

	private void RefreshStructureControls()
	{
		if (!IsInstanceValid(structureSelector) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		bool includeRover = AreRoverStructuresVisible();
		int selectedId = state.SelectedStructureId ?? -1;
		isSyncingStructureSelector = true;
		structureSelector.Clear();
		int selectedIndex = -1;
		foreach (FragmentDetectedStructure structure in state.DetectedStructures)
		{
			if (!includeRover && structure.Provenance == FragmentAnnotationProvenance.Rover) continue;
			string edited = structure.IsPlayerEdited ? " · EDITED" : "";
			structureSelector.AddItem(
				$"S{structure.Id} · {structure.Provenance.ToString().ToUpperInvariant()} · " +
				$"{FragmentCandidateValidityPolicy.DescribeStructureDisposition(structure.Disposition)} · " +
				$"{structure.FeatureIds.Count} FEATURES{edited}",
				structure.Id);
			if (structure.Id == selectedId) selectedIndex = structureSelector.ItemCount - 1;
		}
		if (selectedIndex >= 0) structureSelector.Select(selectedIndex);
		structureSelector.Visible = isStructureSectionExpanded && structureSelector.ItemCount > 0;

		FragmentDetectedStructure selected = state.DetectedStructures.Find(structure =>
			structure.Id == selectedId &&
			(includeRover || structure.Provenance != FragmentAnnotationProvenance.Rover));
		bool canEdit = selected != null &&
			selected.Disposition != FragmentAnnotationDisposition.Dismissed;
		if (!canEdit && editStructureButton.ButtonPressed)
		{
			editStructureButton.ButtonPressed = false;
			fragmentRoverOverlay.SetStructureEditing(false);
		}
		editStructureButton.Disabled = !canEdit;
		int activeCount = state.DetectedStructures.FindAll(structure =>
			structure.Disposition != FragmentAnnotationDisposition.Dismissed &&
			(includeRover || structure.Provenance != FragmentAnnotationProvenance.Rover)).Count;
		mergeStructureButton.Disabled = !canEdit || activeCount < 2;
		newStructureButton.Disabled = state.IsPaused;
		scanStructuresButton.Disabled = state.IsPaused ||
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.SenseReconstructedStructures) == FragmentAutonomyMode.Off;

		if (selected == null)
		{
			selectedStructureLabel.Text = "STRUCTURE: None selected";
			acceptStructureButton.Disabled = true;
			dismissStructureButton.Disabled = true;
			restoreStructureButton.Disabled = true;
		}
		else
		{
			selectedStructureLabel.Text =
				$"STRUCTURE {selected.Id}: " +
				$"{selected.Provenance.ToString().ToUpperInvariant()} · " +
				$"{FragmentCandidateValidityPolicy.DescribeStructureDisposition(selected.Disposition)} · " +
				$"{selected.FeatureIds.Count} FEATURES · CONF {selected.Confidence:0.00}";
			acceptStructureButton.Disabled =
				selected.Disposition == FragmentAnnotationDisposition.Accepted ||
				selected.FeatureIds.Count == 0;
			dismissStructureButton.Disabled =
				selected.Disposition == FragmentAnnotationDisposition.Dismissed;
			restoreStructureButton.Disabled =
				selected.Disposition == FragmentAnnotationDisposition.Proposed;
		}
		isSyncingStructureSelector = false;
	}

	private bool AreRoverStructuresVisible()
	{
		return fragmentAnalysisRover?.State != null &&
			fragmentAnalysisRover.State.GlobalMode != FragmentAutonomyMode.Off &&
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.SenseReconstructedStructures) != FragmentAutonomyMode.Off;
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
		RefreshOrientationControls();
		RefreshRegionSequence();
		RefreshProcessingSearchControls();
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
		if (report.Target?.IsComplete == false)
		{
			targetMetricsLabel.Text =
				$"SELECTED REGION R{report.TargetRegionId} · S/N MEASUREMENT SAFETY LIMIT";
			processingEffectLabel.Text = "MEASURED CHANGE: ABORTED — SEARCH PAUSED";
			RefreshProcessingSearchControls();
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
		RefreshProcessingSearchControls();
	}

	private void RefreshOrientationPresentation(bool restartCuePreview = false)
	{
		if (!IsInstanceValid(fragmentRoverOverlay) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		FragmentDetectedStructure structure = state.OrientationSourceStructure;
		FragmentOrientationHypothesis hypothesis = state.SelectedOrientationId is int hypothesisId
			? state.OrientationHypotheses.Find(candidate => candidate.Id == hypothesisId)
			: null;
		bool isolate = isOrientationSectionExpanded && structure != null &&
			!fragmentAnalysisRover.IsRotationExecuting;
		fragmentRoverOverlay.SetOrientationIsolation(isolate);
		fragmentRoverOverlay.Visible = !IsInstanceValid(regionSequenceView) ||
			!regionSequenceView.Visible;
		if (IsInstanceValid(regionSequenceView))
			regionSequenceView.SetOrientationIsolation(
				isolate,
				state.OrientationSourceView?.RegionId,
				structure,
				hypothesis,
				state.RotationCorrection,
				state.OrientationSourceView?.Features ?? state.DetectedFeatures,
				autonomySettings.StructureColor);
		if (restartCuePreview && isolate && state.SelectedOrientationId.HasValue)
		{
			fragmentRoverOverlay.RestartOrientationPreviewAnimation();
			if (IsInstanceValid(regionSequenceView))
				regionSequenceView.RestartOrientationPreviewAnimation();
		}
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
			editStructureButton.ButtonPressed = false;
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
	private void OnRegionViewLockPressed()
	{
		if (fragmentAnalysisRover.State?.SelectedRegionId is int regionId)
			fragmentAnalysisRover.ToggleRegionViewLock(regionId);
	}

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
			regionViewLockButton.Disabled = true;
			regionViewLockButton.Text = "LOCK";
			return;
		}
		bool locked = fragmentAnalysisRover.IsRegionViewLocked(selected.Id);
		selectedRegionLabel.Text =
			$"REGION {selected.Id}: {selected.Provenance.ToString().ToUpperInvariant()} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()} · CONF {selected.Confidence:0.00}" +
			(locked ? " · VIEW LOCKED" : string.Empty);
		acceptRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Accepted;
		dismissRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		restoreRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Proposed;
		regionViewLockButton.Disabled =
			selected.Disposition != FragmentAnnotationDisposition.Accepted;
		regionViewLockButton.Text = locked ? "UNLOCK" : "LOCK";
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
		fragmentAnalysisRover.SetFeatureReviewPriority(
			regionSequenceView.Visible ? regionSequenceView.DisplayedRegionIds : Array.Empty<int>());
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
		if (change?.Parameter != FragmentAnalysisParameter.View)
			isRegionSequenceRefreshPending = true;
	}

	private void RefreshRegionSequence()
	{
		isRegionSequenceRefreshPending = false;
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
			fragmentAnalysisRover.State.DetectedStructures,
			fragmentAnalysisRover.State.LockedRegionViews,
			fragmentAnalysisRover.State.SelectedFeatureId,
			fragmentAnalysisRover.State.SelectedRegionId);
		fragmentAnalysisRover.SetFeatureReviewPriority(
			regionSequenceView.Visible ? regionSequenceView.DisplayedRegionIds : Array.Empty<int>());
		if (regionSequenceView.Visible &&
			fragmentAnalysisRover.State.SelectedRegionId is int selectedRegionId)
			regionSequenceView.EnsureRegionVisible(selectedRegionId);
		RefreshOrientationPresentation();
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

	private void OnRegionSequencePageChanged()
	{
		fragmentAnalysisRover.SetFeatureReviewPriority(
			regionSequenceView.Visible ? regionSequenceView.DisplayedRegionIds : Array.Empty<int>());
		RefreshFeatureControls();
		RefreshRegionSequenceControls();
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
		rotationValueLabel.Visible = !compact;
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
		string currentAction = FragmentCandidateValidityPolicy.GuardPlayerFacingCopy(
			status.CurrentAction);
		string nextAction = FragmentCandidateValidityPolicy.GuardPlayerFacingCopy(
			status.NextAction);
		string currentTarget = FragmentCandidateValidityPolicy.GuardPlayerFacingCopy(
			status.CurrentTarget);
		string measuredResult = FragmentCandidateValidityPolicy.GuardPlayerFacingCopy(
			status.MeasuredResult);
        roverActivityLabel.Text = $"STATUS: {status.Activity.ToString().ToUpperInvariant()}";
        roverCurrentActionLabel.Text = $"CURRENT: {currentAction}";
        roverNextActionLabel.Text = $"NEXT: {nextAction}";
        roverTargetLabel.Text = $"TARGET: {currentTarget}";
        roverResultLabel.Text = $"RESULT: {measuredResult}";
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
		SetStructureSectionExpanded(false);
		SetFragmentOverviewSectionExpanded(false);
		SetOrientationSectionExpanded(false);
		SetCorrectionSectionExpanded(false);
		SetArrowSectionExpanded(false);
		SetDirectionSectionExpanded(false);
	}

	private void OnProcessingHistorySectionPressed() =>
		SetProcessingHistorySectionExpanded(!isProcessingHistorySectionExpanded);

	private void OnCandidateRegionSectionPressed() =>
		SetCandidateRegionSectionExpanded(!isCandidateRegionSectionExpanded);

	private void OnRegionSequenceSectionPressed() =>
		SetRegionSequenceSectionExpanded(!isRegionSequenceSectionExpanded);

	private void OnFeatureSensingSectionPressed() =>
		SetFeatureSensingSectionExpanded(!isFeatureSensingSectionExpanded);

	private void OnStructureSectionPressed() =>
		SetStructureSectionExpanded(!isStructureSectionExpanded);

	private void OnFragmentOverviewSectionPressed() =>
		SetFragmentOverviewSectionExpanded(!isFragmentOverviewSectionExpanded);

	private void OnOrientationSectionPressed() =>
		SetOrientationSectionExpanded(!isOrientationSectionExpanded);

	private void OnCorrectionSectionPressed() =>
		SetCorrectionSectionExpanded(!isCorrectionSectionExpanded);

	private void OnArrowSectionPressed() =>
		SetArrowSectionExpanded(!isArrowSectionExpanded);

	private void OnDirectionSectionPressed() =>
		SetDirectionSectionExpanded(!isDirectionSectionExpanded);

	private void SetFragmentOverviewSectionExpanded(bool expanded)
	{
		isFragmentOverviewSectionExpanded = expanded;
		fragmentOverviewSectionButton.Text = expanded
			? "▼ SCANNED FRAGMENT"
			: "▶ SCANNED FRAGMENT";
		fragmentOverviewContent.Visible = expanded;
	}

	private void SetFragmentOverviewTexture(Texture2D texture)
	{
		if (!IsInstanceValid(fragmentOverviewTexture)) return;
		fragmentOverviewTexture.Texture = texture;
		fragmentOverviewTexture.Visible = texture != null;
		fragmentOverviewCaption.Text = texture == null
			? "SCANNED FRAGMENT IMAGE UNAVAILABLE"
			: "VISUAL RECORD OF THE SCANNED FRAGMENT";
	}

	private void SetOrientationSectionExpanded(bool expanded)
	{
		isOrientationSectionExpanded = expanded;
		orientationSectionButton.Text = expanded
			? "▼ ORIENTATION"
			: "▶ ORIENTATION";
		orientationRegionControls.Visible = expanded;
		orientationActions.Visible = expanded;
		quitOrientationViewButton.Visible = expanded;
		selectedOrientationLabel.Visible = expanded;
		orientationSelector.Visible = false;
		orientationEvidenceLabel.Visible = expanded;
		orientationEdits.Visible = expanded;
		SetCorrectionSectionExpanded(expanded);
		if (expanded && fragmentAnalysisRover?.State?.OrientationHypotheses.Count == 0)
			fragmentAnalysisRover.EstimateOrientationHypotheses(true);
		RefreshOrientationPresentation(expanded);
	}

	private void SetCorrectionSectionExpanded(bool expanded)
	{
		isCorrectionSectionExpanded = expanded;
		correctionSectionButton.Text = expanded
			? "▼ ROTATION CORRECTION"
			: "▶ ROTATION CORRECTION";
		correctionActions.Visible = expanded;
		correctionLabel.Visible = expanded;
		correctionEditor.Visible = expanded;
		correctionEdits.Visible = expanded;
		RefreshRotationCorrectionControls();
		if (expanded && fragmentAnalysisRover?.State?.RotationCorrection != null)
			RefreshOrientationPresentation(true);
	}

	private void SetArrowSectionExpanded(bool expanded)
	{
		isArrowSectionExpanded = expanded;
		arrowSectionButton.Text = expanded
			? "▼ DIRECTIONAL ARROW"
			: "▶ DIRECTIONAL ARROW";
		arrowActions.Visible = expanded;
		arrowLabel.Visible = expanded;
		arrowSelector.Visible = expanded && arrowSelector.ItemCount > 0;
		arrowNavigation.Visible = expanded;
		arrowManual.Visible = expanded;
		arrowEdits.Visible = expanded;
		if (!expanded)
		{
			drawArrowButton.SetPressedNoSignal(false);
			fragmentRoverOverlay.SetArrowDrawingArmed(false);
		}
		RefreshArrowControls();
	}

	private void SetDirectionSectionExpanded(bool expanded)
	{
		isDirectionSectionExpanded = expanded;
		directionSectionButton.Text = expanded
			? "▼ WORLD DIRECTION"
			: "▶ WORLD DIRECTION";
		directionActions.Visible = expanded;
		directionStatusLabel.Visible = expanded;
		directionInset.Visible = expanded;
		RefreshDirectionControls();
	}

	private void SetProcessingHistorySectionExpanded(bool expanded)
	{
		isProcessingHistorySectionExpanded = expanded;
		processingHistorySectionButton.Text =
			expanded ? "▼ TESTED CONFIGURATIONS" : "▶ TESTED CONFIGURATIONS";
		if (expanded && isProcessingHistoryDirty)
			RefreshProcessingHistoryControls();
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
		regionViewLockButton.Visible = expanded;
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

	private void SetStructureSectionExpanded(bool expanded)
	{
		isStructureSectionExpanded = expanded;
		structureSectionButton.Text = expanded
			? "▼ RECONSTRUCTED STRUCTURES"
			: "▶ RECONSTRUCTED STRUCTURES";
		structureActions.Visible = expanded;
		selectedStructureLabel.Visible = expanded;
		structureSelector.Visible = expanded && structureSelector.ItemCount > 0;
		structureMembershipEdits.Visible = expanded;
		structureDispositionEdits.Visible = expanded;
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
		if (!isProcessingHistorySectionExpanded)
		{
			isProcessingHistoryDirty = true;
			processingHistorySelector.Visible = false;
			return;
		}
		isProcessingHistoryDirty = false;
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

	private void OnProcessingSearchStartPressed()
	{
		if (fragmentAnalysisRover.IsProcessingSearchRunning)
			fragmentAnalysisRover.StopProcessingSearch();
		else
			fragmentAnalysisRover.StartProcessingSearch();
		RefreshProcessingSearchControls();
	}

	private void OnProcessingSearchApplyPressed() =>
		fragmentAnalysisRover.ApproveProcessingAdjustment();

	private void OnProcessingSearchSkipPressed() =>
		fragmentAnalysisRover.SkipProcessingAdjustment();

	private void OnProcessingSearchBackPressed() => fragmentAnalysisRover.SearchBack();

	private void OnProcessingSearchForwardPressed() => fragmentAnalysisRover.SearchForward();

	private void OnPolarizationLockToggled(bool locked) =>
		SetProcessingLock(FragmentAnalysisParameter.PolarizationEnabled, locked);

	private void OnSpectralLockToggled(bool locked) =>
		SetProcessingLock(FragmentAnalysisParameter.SpectralEnabled, locked);

	private void OnSurfaceLockToggled(bool locked) =>
		SetProcessingLock(FragmentAnalysisParameter.SurfaceEnabled, locked);

	private void OnElectromagneticLockToggled(bool locked) =>
		SetProcessingLock(FragmentAnalysisParameter.ElectromagneticEnabled, locked);

	private void OnResonanceLockToggled(bool locked) =>
		SetProcessingLock(FragmentAnalysisParameter.ResonanceEnabled, locked);

	private void OnXRayLockToggled(bool locked) =>
		SetProcessingLock(FragmentAnalysisParameter.XRayEnabled, locked);

	private void SetProcessingLock(FragmentAnalysisParameter parameter, bool locked)
	{
		if (isSyncingAutonomyUi) return;
		fragmentAnalysisRover.SetProcessingParameterLocked(parameter, locked);
	}

	private void RefreshProcessingSearchControls()
	{
		if (!IsInstanceValid(processingSearchPlanLabel) || fragmentAnalysisRover?.State == null) return;
		FragmentProcessingAdjustment proposal = fragmentAnalysisRover.PendingProcessingAdjustment;
		FragmentAutonomyMode decisionMode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.DecideProcessingConfiguration);
		bool paused = fragmentAnalysisRover.State.IsPaused;
		bool running = fragmentAnalysisRover.IsProcessingSearchRunning;
		bool hasProposal = proposal != null;
		bool supporter = decisionMode == FragmentAutonomyMode.Supporter ||
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.AdjustProcessingParameters) != FragmentAutonomyMode.Performer;

		string intervalProgress = running
			? $"TEST {fragmentAnalysisRover.ContinuousProcessingSearchSteps}/" +
			  $"{fragmentAnalysisRover.ContinuousProcessingSearchStepLimit} · "
			: "";
		processingSearchPlanLabel.Text = hasProposal
			? $"{intervalProgress}PLAN: {proposal.ParameterName} · " +
			  $"{proposal.PreviousValue} → {proposal.ProposedValue}"
			: running && paused
				? $"{intervalProgress}{fragmentAnalysisRover.Status?.CurrentAction ?? "SEARCH PAUSED"} · " +
				  "PRESS RESUME TO CONTINUE"
				: running ? $"{intervalProgress}Measuring or finding the next candidate" :
				  "PLAN: Select a measured region, then start search";
		processingSearchPlanLabel.TooltipText = processingSearchPlanLabel.Text;
		processingSearchStartButton.Text = running ? "STOP" : "START";
		processingSearchStartButton.Disabled = decisionMode == FragmentAutonomyMode.Off ||
			fragmentAnalysisRover.State.SelectedRegionId == null;
		processingSearchApplyButton.Disabled = !hasProposal || paused || !supporter;
		processingSearchApplyButton.Visible = supporter;
		processingSearchSkipButton.Disabled = !hasProposal || paused;
		processingSearchBackButton.Disabled = !fragmentAnalysisRover.CanSearchBack;
		processingSearchForwardButton.Disabled = !fragmentAnalysisRover.CanSearchForward;

		isSyncingAutonomyUi = true;
		try
		{
			SetLockButtonState(polarizationLockButton,
				fragmentAnalysisRover.IsProcessingParameterLocked(
					FragmentAnalysisParameter.PolarizationEnabled),
				"Polarization enabled state and level");
			SetLockButtonState(spectralLockButton,
				fragmentAnalysisRover.IsProcessingParameterLocked(
					FragmentAnalysisParameter.SpectralEnabled),
				"Spectral enabled state and level");
			SetLockButtonState(surfaceLockButton,
				fragmentAnalysisRover.IsProcessingParameterLocked(
					FragmentAnalysisParameter.SurfaceEnabled),
				"Surface Topography enabled state and level");
			SetLockButtonState(electromagneticLockButton,
				fragmentAnalysisRover.IsProcessingParameterLocked(
					FragmentAnalysisParameter.ElectromagneticEnabled),
				"Electromagnetic channel");
			SetLockButtonState(resonanceLockButton,
				fragmentAnalysisRover.IsProcessingParameterLocked(
					FragmentAnalysisParameter.ResonanceEnabled),
				"Resonance channel");
			SetLockButtonState(xRayLockButton,
				fragmentAnalysisRover.IsProcessingParameterLocked(
					FragmentAnalysisParameter.XRayEnabled),
				"X-Ray channel");
		}
		finally
		{
			isSyncingAutonomyUi = false;
		}
	}

	private static void SetLockButtonState(TextureButton button, bool locked, string parameterName)
	{
		button.ButtonPressed = locked;
		button.TooltipText = locked
			? $"Locked: Rover search cannot change {parameterName}. Click to unlock."
			: $"Unlocked: Rover search may change {parameterName}. Click to lock.";
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
			fragmentRoverOverlay.SetShowRoverStructures(AreRoverStructuresVisible());

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
		RefreshStructureControls();
		RefreshOrientationControls();
		RefreshRotationCorrectionControls();
		RefreshArrowControls();
		RefreshDirectionControls();
		RefreshRegionSequence();
		RefreshNavigationControls();
		OnMetricsChanged(fragmentAnalysisRover.MeasurementReport);
		RefreshProcessingSearchControls();
    }
}
