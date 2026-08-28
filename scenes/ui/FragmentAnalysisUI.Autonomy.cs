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
	private ScrollContainer roverPanelScroll;
	private PanelContainer initialModeOverlay;
	private Button initialManualButton;
	private Button initialSupportButton;
	private Button initialAutonomousButton;
	private bool initialModeSignalsConnected;
    private Button roverPauseButton;
	private Window autonomousWorkflowPopup;
	private Control autonomousWorkflowPrompt;
	private Label autonomousWorkflowPromptLabel;
	private Control autonomousRegionReviewActions;
	private Button autonomousRegionReviewButton;
	private Button autonomousAddRegionButton;
	private Button autonomousFindAnotherRegionButton;
	private Control autonomousArrowActions;
	private Button autonomousReviewArrowButton;
	private Button autonomousDrawArrowButton;
	private Control autonomousStructureActions;
	private Button autonomousEditStructureButton;
	private Button autonomousValidateStructureButton;
	private Button autonomousWorkflowContinueButton;
	private bool isSubmittingPlayerArrow;
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
	private Control orientationFragmentReference;
	private TextureRect orientationFragmentTexture;
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
	private bool suppressSectionAutoScroll;
	private bool isNavigatingHistory;
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
	private FragmentAutonomousWorkflowStage lastPresentedAutonomousWorkflowStage = (FragmentAutonomousWorkflowStage)(-1);
	private bool isSyncingOrientationSelector;
	private bool isSyncingStructureSelector;
	private bool workflowFeatureStage;
	private bool isCompactHeader;
	private bool isRegionSequenceRefreshPending;

    public event Action<FragmentAnalysisChange> AnalysisChanged;

	private void InitializeAutonomyNodes()
	{
        autonomySettings ??= new FragmentAutonomySettings();
		initialModeOverlay = GetNode<PanelContainer>("%InitialModeOverlay");
		initialManualButton = GetNode<Button>("%InitialManualButton");
		initialSupportButton = GetNode<Button>("%InitialSupportButton");
		initialAutonomousButton = GetNode<Button>("%InitialAutonomousButton");
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
        // Mode button group is set up inside CreateCompactHeaderControls after the buttons are created.
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

        // roverCompactStatusLabel replaced by inline mode buttons in the header.
        roverCompactStatusLabel = null;

        // Autonomy mode buttons moved from panel to top bar.
		autonomyModeButtonGroup = new ButtonGroup { AllowUnpress = false };

		autonomyOffButton = new CheckButton
		{
			Name = "AutonomyOffButton",
			Text = "MANUAL",
			TooltipText = "Manual analysis only.",
			ButtonPressed = true,
			ButtonGroup = autonomyModeButtonGroup
		};
		autonomySupporterButton = new CheckButton
		{
			Name = "AutonomySupporterButton",
			Text = "SUPPORT",
			TooltipText = "The player acts and the Rover provides support.",
			ButtonGroup = autonomyModeButtonGroup
		};
		autonomyPerformerButton = new CheckButton
		{
			Name = "AutonomyPerformerButton",
			Text = "AUTONOMOUS",
			TooltipText = "The Rover acts where allowed and the player supervises.",
			ButtonGroup = autonomyModeButtonGroup
		};
		header.AddChild(autonomyOffButton);
		header.MoveChild(autonomyOffButton, quitIndex + 1);
		header.AddChild(autonomySupporterButton);
		header.MoveChild(autonomySupporterButton, quitIndex + 2);
		header.AddChild(autonomyPerformerButton);
		header.MoveChild(autonomyPerformerButton, quitIndex + 3);

        // Task allocation button in the header.
        autonomyAdvancedButton = new Button
        {
            Name = "AutonomyAdvancedButtonHeader",
            Text = "TASK ALLOC",
            TooltipText = "Set per-task allocation overrides and Yellow reliability."
        };
        header.AddChild(autonomyAdvancedButton);
        header.MoveChild(autonomyAdvancedButton, quitIndex + 4);

        roverPanelToggleButton = new Button
        {
            Name = "RoverPanelToggleButton",
            Text = "ROVER MENU<=",
            TooltipText = "Show or hide the Rover autonomy panel."
        };
        header.AddChild(roverPanelToggleButton);
        header.MoveChild(roverPanelToggleButton, quitIndex + 5);

		// COMPARE REGIONS button is now inside the candidate-regions section in the rover panel,
		// placed next to GENERATE REGIONS. Create it here and move it programmatically after the
		// panel is visible.
		comparisonOpenButton = new Button
		{
			Name = "ComparisonOpenButton",
			Text = "COMPARE",
			TooltipText = "Open the side-by-side accepted-region comparison.",
			Disabled = true
		};
		// Add as sibling of groupRegionsButton inside candidateRegionActions.
		if (IsInstanceValid(candidateRegionActions))
		{
			candidateRegionActions.AddChild(comparisonOpenButton);
		}
		else
		{
			// Fallback: add to header if panel actions not yet available.
			header.AddChild(comparisonOpenButton);
			header.MoveChild(comparisonOpenButton, quitIndex + 6);
		}
    }

    private void UpdateFragmentLifecycleLabel(bool restored, bool solved)
    {
        if (!IsInstanceValid(fragmentLifecycleLabel)) return;
		string openedBy = initiationOrigin == FragmentAnalysisActionOrigin.Rover
			? "ROVER"
			: "PLAYER";
		bool completed = fragmentAnalysisRover?.State?.IsAnalysisCompleted == true;
		fragmentLifecycleLabel.Text = (completed ? "SAMPLE: COMPLETED" : "SAMPLE: ACTIVE") +
			$" · {openedBy}" +
            (restored ? " · RESTORED" : string.Empty) +
            (solved ? " · SOLVED" : string.Empty);
		fragmentLifecycleLabel.TooltipText = initiationOrigin == FragmentAnalysisActionOrigin.Rover
			? "Analysis was initiated by a Rover workflow."
			: "Analysis opened using the player's Analyse Sample button.";
    }

    private void CreateRoverPanel(HBoxContainer analysisWorkspace)
    {
		PackedScene panelScene = GD.Load<PackedScene>(
			"res://scenes/ui/FragmentAutonomyPanel.tscn");
		roverPanel = panelScene.Instantiate<PanelContainer>();
		analysisWorkspace.AddChild(roverPanel);

		// Mode buttons are now in the top header bar; panel no longer contains them.
		autonomyOffButton = null;
		autonomySupporterButton = null;
		autonomyPerformerButton = null;

		roverActivityLabel = roverPanel.GetNode<Label>("%RoverActivityLabel");
		roverCurrentActionLabel = roverPanel.GetNode<Label>("%RoverCurrentActionLabel");
		roverNextActionLabel = roverPanel.GetNode<Label>("%RoverNextActionLabel");
		roverTargetLabel = null; // removed from panel
		roverPanelScroll = roverPanel.GetNode<ScrollContainer>("Margin/PanelScroll");
		historyBackButton = roverPanel.GetNode<Button>("%HistoryBackButton");
		historyForwardButton = roverPanel.GetNode<Button>("%HistoryForwardButton");
		roverPauseButton = roverPanel.GetNode<Button>("%RoverWorkflowPlayPauseButton");
		CreateAutonomousWorkflowPopup();

		// TESTED CONFIGURATIONS section removed; null out references.
		processingHistorySelector = null;
		restoreProcessingConfigurationButton = null;
		bookmarkProcessingConfigurationButton = null;
		processingHistorySectionButton = null;
		processingHistoryActions = null;

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

		candidateRegionSectionButton = roverPanel.GetNode<Button>("%RegionTitle");

		// REGION SEQUENCE section removed; null out references.
		regionSequenceSectionButton = null;
		regionSequenceLabel = null;
		previousRegionPairButton = null;
		regionSequenceButton = null;
		nextRegionPairButton = null;
		regionSequenceActions = null;

		featureSensingSectionButton = roverPanel.GetNode<Button>("%FeatureTitle");
		structureSectionButton = roverPanel.GetNode<Button>("%StructureTitle");

		// Keep the scanned-fragment reference inside the Rover's human-decision prompt.
		fragmentOverviewSectionButton = null;
		fragmentOverviewContent = null;

		orientationSectionButton = roverPanel.GetNode<Button>("%OrientationTitle");
		CreateOrientationFragmentReference(
			roverPanel.GetNode<VBoxContainer>("Margin/PanelScroll/Content"));
		orientationRegionControls = roverPanel.GetNode<Control>("%OrientationRegionControls");
		previousOrientationRegionButton = roverPanel.GetNode<Button>("%PreviousOrientationRegionButton");
		orientationRegionLabel = roverPanel.GetNode<Label>("%OrientationRegionLabel");
		nextOrientationRegionButton = roverPanel.GetNode<Button>("%NextOrientationRegionButton");
		estimateOrientationButton = roverPanel.GetNode<Button>("%EstimateOrientationButton");
		showOrientationOverlayButton = roverPanel.GetNode<CheckButton>("%ShowOrientationOverlayButton");
		selectedOrientationLabel = roverPanel.GetNode<Label>("%SelectedOrientationLabel");
		orientationSelector = roverPanel.GetNode<OptionButton>("%OrientationSelector");
		previousOrientationButton = roverPanel.GetNode<Button>("%PreviousOrientationButton");
		orientationStepLabel = roverPanel.GetNode<Label>("%OrientationStepLabel");
		acceptOrientationButton = roverPanel.GetNode<Button>("%AcceptOrientationButton");
		nextOrientationButton = roverPanel.GetNode<Button>("%NextOrientationButton");
		quitOrientationViewButton = roverPanel.GetNode<Button>("%QuitOrientationViewButton");
		orientationActions = estimateOrientationButton.GetParent<Control>();
		orientationEdits = acceptOrientationButton.GetParent<Control>();

		// Correction section is now merged into ORIENTATION; create controls programmatically.
		correctionSectionButton = null;
		CreateCorrectionControls(roverPanel.GetNode<VBoxContainer>("Margin/PanelScroll/Content"), orientationEdits);

		// ARROW & DIRECTION section (merged); ArrowTitle is now the shared section header.
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
		restoreArrowButton = null; // removed
		// WORLD DIRECTION merged into ARROW section; DirectionActions/Status/Inset are still present.
		directionSectionButton = null; // merged; ArrowTitle covers both
		directionActions = roverPanel.GetNode<Control>("%DirectionActions");
		mapDirectionButton = roverPanel.GetNode<Button>("%MapDirectionButton");
		directionStatusLabel = roverPanel.GetNode<Label>("%DirectionStatusLabel");
		directionInset = roverPanel.GetNode<FragmentDirectionInset>("%DirectionInset");

		groupRegionsButton = roverPanel.GetNode<Button>("%GroupRegionsButton");
		showRegionOverlayButton = roverPanel.GetNode<CheckButton>("%ShowRegionOverlayButton");
		selectedRegionLabel = roverPanel.GetNode<Label>("%SelectedRegionLabel");
		regionSelector = roverPanel.GetNode<OptionButton>("%RegionSelector");
		acceptRegionButton = roverPanel.GetNode<Button>("%AcceptRegionButton");
		dismissRegionButton = roverPanel.GetNode<Button>("%DismissRegionButton");
		restoreRegionButton = null; // removed
		addRegionButton = roverPanel.GetNode<Button>("%AddRegionButton");
		regionViewLockButton = roverPanel.GetNode<Button>("%RegionViewLockButton");
		candidateRegionActions = groupRegionsButton.GetParent<Control>();
		candidateRegionEdits = acceptRegionButton.GetParent<Control>();
		navigationIntentLabel = roverPanel.GetNode<Label>("%NavigationIntentLabel");
		navigateToRegionButton = roverPanel.GetNode<Button>("%NavigateToRegionButton");
		cancelNavigationButton = roverPanel.GetNode<Button>("%CancelNavigationButton");
		navigationActions = navigateToRegionButton.GetParent<Control>();
		scanFeaturesButton = roverPanel.GetNode<Button>("%ScanFeaturesButton");
		showFeatureOverlayButton = roverPanel.GetNode<CheckButton>("%ShowFeatureOverlayButton");
		selectedFeatureLabel = roverPanel.GetNode<Label>("%SelectedFeatureLabel");
		featureSelector = roverPanel.GetNode<OptionButton>("%FeatureSelector");
		acceptFeatureButton = roverPanel.GetNode<Button>("%AcceptFeatureButton");
		dismissFeatureButton = roverPanel.GetNode<Button>("%DismissFeatureButton");
		restoreFeatureButton = null; // removed
		featureSensingActions = scanFeaturesButton.GetParent<Control>();
		featureEdits = acceptFeatureButton.GetParent<Control>();
		scanStructuresButton = roverPanel.GetNode<Button>("%ScanStructuresButton");
		showStructureOverlayButton = roverPanel.GetNode<CheckButton>("%ShowStructureOverlayButton");
		selectedStructureLabel = roverPanel.GetNode<Label>("%SelectedStructureLabel");
		structureSelector = roverPanel.GetNode<OptionButton>("%StructureSelector");
		acceptStructureButton = roverPanel.GetNode<Button>("%AcceptStructureButton");
		dismissStructureButton = roverPanel.GetNode<Button>("%DismissStructureButton");
		restoreStructureButton = null; // removed
		if (scanStructuresButton == null || acceptStructureButton == null)
			throw new InvalidOperationException(
				"FragmentAutonomyPanel.tscn is missing the checkpoint 4.1 structure controls; " +
				"rescan/reimport the scene after updating it.");
		structureActions = scanStructuresButton.GetParent<Control>();
		structureDispositionEdits = acceptStructureButton.GetParent<Control>();

		// Task allocation trigger is created in the top header bar.
		autonomyAdvancedButton = null;
		capabilityOverridesScroll = roverPanel.GetNode<ScrollContainer>("%CapabilityOverridesScroll");
		capabilityOverridesContainer = roverPanel.GetNode<VBoxContainer>("%CapabilityOverridesContainer");

		// Bottom PAUSE button removed with the action row.
		//roverPauseButton = null;

		// Create rotation controls inside the orientation section.
		CreateOrientationRotationControls(roverPanel.GetNode<VBoxContainer>("Margin/PanelScroll/Content"));
	}

	private void CreateAutonomousWorkflowPopup()
	{
		autonomousWorkflowPopup = new Window
		{
			Name = "AutonomousWorkflowPopup",
			Title = "ROVER REQUEST — HUMAN DECISION REQUIRED",
			Size = new Vector2I(480, 220),
			MinSize = new Vector2I(420, 180),
			Transient = true,
			Exclusive = false,
			AlwaysOnTop = true,
			Unresizable = true,
			Visible = false
		};
		AddChild(autonomousWorkflowPopup);

		MarginContainer margin = new MarginContainer { Name = "PromptMargin" };
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		autonomousWorkflowPopup.AddChild(margin);
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		VBoxContainer prompt = new VBoxContainer { Name = "AutonomousWorkflowPrompt" };
		prompt.AddThemeConstantOverride("separation", 8);
		margin.AddChild(prompt);
		autonomousWorkflowPrompt = prompt;

		fragmentOverviewTexture = CreateFragmentThumbnail();
		autonomousWorkflowPromptLabel = new Label
		{
			Name = "AutonomousWorkflowPromptLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Text = "ROVER WAITING FOR PLAYER"
		};
		autonomousWorkflowPromptLabel.AddThemeColorOverride(
			"font_color", new Color(0.25f, 1f, 0.45f));
		prompt.AddChild(autonomousWorkflowPromptLabel);

		HBoxContainer regionActions = new HBoxContainer
		{
			Name = "AutonomousRegionReviewActions",
			Visible = false
		};
		regionActions.AddThemeConstantOverride("separation", 8);
		autonomousRegionReviewButton = new Button
		{
			Name = "AutonomousRegionReviewButton",
			Text = "REVIEW FIRST REGION",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		autonomousAddRegionButton = new Button
		{
			Name = "AutonomousAddRegionButton",
			Text = "ADD REGION",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		regionActions.AddChild(autonomousRegionReviewButton);
		regionActions.AddChild(autonomousAddRegionButton);
		prompt.AddChild(regionActions);
		autonomousRegionReviewActions = regionActions;

		autonomousWorkflowContinueButton = new Button
		{
			Name = "AutonomousWorkflowContinueButton",
			Text = "CONTINUE",
			Visible = false
		};
		prompt.AddChild(autonomousWorkflowContinueButton);
		autonomousFindAnotherRegionButton = new Button
		{
			Name = "AutonomousFindAnotherRegionButton",
			Text = "FIND ANOTHER REGION",
			Visible = false
		};
		prompt.AddChild(autonomousFindAnotherRegionButton);

		HBoxContainer arrowActions = new HBoxContainer
		{
			Name = "AutonomousArrowActions",
			Visible = false
		};
		arrowActions.AddThemeConstantOverride("separation", 8);
		autonomousReviewArrowButton = new Button
		{
			Name = "AutonomousReviewArrowButton",
			Text = "REVIEW ARROW",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		autonomousDrawArrowButton = new Button
		{
			Name = "AutonomousDrawArrowButton",
			Text = "DRAW ARROW",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		arrowActions.AddChild(autonomousReviewArrowButton);
		arrowActions.AddChild(autonomousDrawArrowButton);
		prompt.AddChild(arrowActions);
		autonomousArrowActions = arrowActions;

		HBoxContainer structureActions = new HBoxContainer
		{
			Name = "AutonomousStructureActions",
			Visible = false
		};
		structureActions.AddThemeConstantOverride("separation", 8);
		autonomousEditStructureButton = new Button
		{
			Name = "AutonomousEditStructureButton",
			Text = "EDIT STRUCTURE",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		autonomousValidateStructureButton = new Button
		{
			Name = "AutonomousValidateStructureButton",
			Text = "VALIDATE & CONTINUE",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		structureActions.AddChild(autonomousEditStructureButton);
		structureActions.AddChild(autonomousValidateStructureButton);
		prompt.AddChild(structureActions);
		autonomousStructureActions = structureActions;
	}

	/// <summary>Creates the scanned-monolith reference inside the human-decision prompt.</summary>
	private TextureRect CreateFragmentThumbnail()
	{
		VBoxContainer prompt = autonomousWorkflowPrompt as VBoxContainer;
		if (prompt == null)
		{
			GD.PushError("AutonomousWorkflowPrompt must be a VBoxContainer.");
			return null;
		}
		fragmentOverviewCaption = new Label
		{
			Name = "FragmentOverviewCaption",
			Text = "SCANNED MONOLITH REFERENCE",
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false
		};
		PanelContainer frame = new PanelContainer
		{
			Name = "FragmentThumbnailFrame",
			CustomMinimumSize = new Vector2(0, 112),
			Visible = false
		};
		TextureRect tex = new TextureRect
		{
			Name = "FragmentOverviewTexture",
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		frame.AddChild(tex);
		prompt.AddChild(fragmentOverviewCaption);
		prompt.AddChild(frame);
		prompt.MoveChild(fragmentOverviewCaption, 0);
		prompt.MoveChild(frame, 1);
		return tex;
	}

	private void CreateOrientationFragmentReference(VBoxContainer content)
	{
		VBoxContainer reference = new VBoxContainer
		{
			Name = "OrientationFragmentReference",
			Visible = false
		};
		Label caption = new Label
		{
			Text = "SCANNED MONOLITH REFERENCE",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		PanelContainer frame = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, 112)
		};
		orientationFragmentTexture = new TextureRect
		{
			Name = "OrientationFragmentTexture",
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		frame.AddChild(orientationFragmentTexture);
		reference.AddChild(caption);
		reference.AddChild(frame);
		content.AddChild(reference);
		content.MoveChild(reference, orientationSectionButton.GetIndex() + 1);
		orientationFragmentReference = reference;
	}

	/// <summary>Creates correction controls and inserts them after the orientation H# edits row.</summary>
	private void CreateCorrectionControls(VBoxContainer content, Control afterNode)
	{
		int insertAt = afterNode.GetIndex() + 1;

		correctionLabel = new Label
		{
			Name = "CorrectionLabel",
			Text = "ORIENTATION ERROR: Accept an orientation hypothesis first",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Visible = false
		};
		content.AddChild(correctionLabel);
		content.MoveChild(correctionLabel, insertAt++);

		correctionEditor = new HBoxContainer { Name = "CorrectionEditor", Visible = false };
		Label degreesLbl = new Label { Text = "SIGNED DEGREES", VerticalAlignment = VerticalAlignment.Center };
		correctionDegreesSpinBox = new SpinBox
		{
			Name = "CorrectionDegreesSpinBox",
			MinValue = -180, MaxValue = 180,
			Suffix = "°",
			CustomMinimumSize = new Vector2(110, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		correctionDirectionLabel = new Label
		{
			Name = "CorrectionDirectionLabel",
			Text = "NONE",
			CustomMinimumSize = new Vector2(48, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		correctionEditor.AddChild(degreesLbl);
		correctionEditor.AddChild(correctionDegreesSpinBox);
		correctionEditor.AddChild(correctionDirectionLabel);
		content.AddChild(correctionEditor);
		content.MoveChild(correctionEditor, insertAt++);

		correctionEdits = new HBoxContainer
		{
			Name = "CorrectionEdits",
			Visible = false,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		acceptCorrectionButton = new Button
		{
			Name = "AcceptCorrectionButton",
			Text = "START ROTATION",
			Disabled = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			TooltipText = "Preview and execute this signed correction. Use again to cancel at current angle."
		};
		rejectCorrectionButton = new Button
		{
			Name = "RejectCorrectionButton",
			Text = "REJECT",
			Disabled = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		correctionEdits.AddChild(acceptCorrectionButton);
		correctionEdits.AddChild(rejectCorrectionButton);
		content.AddChild(correctionEdits);
		content.MoveChild(correctionEdits, insertAt);

		// correctionActions / proposeCorrectionButton are no longer exposed;
		// corrections are auto-proposed after H# acceptance.
		correctionActions = correctionEdits;
		proposeCorrectionButton = null;
	}

	/// <summary>Creates manual rotation buttons at the bottom of the orientation section.</summary>
	private void CreateOrientationRotationControls(VBoxContainer content)
	{
		// Place after the correction edits row (which is the last thing in the orientation area).
		HBoxContainer rotRow = new HBoxContainer
		{
			Name = "OrientationRotationRow",
			Visible = false,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		rotRow.AddThemeConstantOverride("separation", 4);

		rotateCounterClockwiseButton = new Button
		{
			Name = "RotateCounterClockwiseButton",
			Text = "CCW -10°",
			TooltipText = "Rotate the reconstruction 10 degrees counter-clockwise."
		};
		rotationValueLabel = new Label
		{
			Name = "RotationValueLabel",
			Text = "ROTATION: 0°",
			CustomMinimumSize = new Vector2(130, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		fineRotationSpinBox = new SpinBox
		{
			Name = "FineRotationSpinBox",
			MinValue = -180, MaxValue = 180,
			Suffix = "°",
			CustomMinimumSize = new Vector2(92, 0),
			TooltipText = "Set the rotation precisely in 1-degree steps."
		};
		rotateClockwiseButton = new Button
		{
			Name = "RotateClockwiseButton",
			Text = "CW +10°",
			TooltipText = "Rotate the reconstruction 10 degrees clockwise."
		};

		rotRow.AddChild(rotateCounterClockwiseButton);
		rotRow.AddChild(rotationValueLabel);
		rotRow.AddChild(fineRotationSpinBox);
		rotRow.AddChild(rotateClockwiseButton);

		// Insert after correctionEdits inside the content VBox.
		int insertAt = correctionEdits != null
			? correctionEdits.GetIndex() + 1
			: content.GetChildCount();
		content.AddChild(rotRow);
		content.MoveChild(rotRow, insertAt);
	}

    private void InitializeAutonomy(FragmentAutonomyState restoredState)
    {
        lastControlState = CaptureControlState();
        ConnectAutonomySignals();
        fragmentAnalysisRover.Initialize(
            fragmentCanvas,
            this,
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
		fragmentAnalysisRover.AutonomousWorkflowChanged += OnAutonomousWorkflowChanged;
		autonomousWorkflowPopup.CloseRequested += OnAutonomousWorkflowPopupCloseRequested;
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
		fragmentRoverOverlay.RegionLockRequested += OnOverlayRegionLockRequested;
		fragmentRoverOverlay.StructureEditRequested += OnStructureEditRequested;
		fragmentRoverOverlay.StructureFeatureToggled += OnStructureFeatureToggled;
		fragmentRoverOverlay.StructureFeatureRemoved += OnStructureFeatureRemoved;
		fragmentRoverOverlay.StructureStrokeDrawn += OnStructureStrokeDrawn;
		fragmentRoverOverlay.StructureEditingCancelled += OnStructureEditingCancelled;
		fragmentRoverOverlay.ArrowDrawn += OnArrowDrawn;
        roverPanelToggleButton.Pressed += OnRoverPanelTogglePressed;
		comparisonOpenButton.Pressed += OnComparisonOpenPressed;
        autonomyOffButton.Toggled += OnAutonomyOffToggled;
        autonomySupporterButton.Toggled += OnAutonomySupporterToggled;
        autonomyPerformerButton.Toggled += OnAutonomyPerformerToggled;
        if (roverPauseButton != null) roverPauseButton.Pressed += OnRoverPausePressed;
		autonomyAdvancedButton.Pressed += OnAutonomyAdvancedPressed;
		autonomousRegionReviewButton.Pressed += OnAutonomousRegionReviewPressed;
		autonomousAddRegionButton.Pressed += OnAutonomousAddRegionPressed;
		autonomousWorkflowContinueButton.Pressed += OnAutonomousWorkflowContinuePressed;
		autonomousFindAnotherRegionButton.Pressed += OnAutonomousFindAnotherRegionPressed;
		autonomousReviewArrowButton.Pressed += OnAutonomousReviewArrowPressed;
		autonomousDrawArrowButton.Pressed += OnAutonomousDrawArrowPressed;
		autonomousEditStructureButton.Pressed += OnAutonomousEditStructurePressed;
		autonomousValidateStructureButton.Pressed += OnAutonomousValidateStructurePressed;
        reloadConfirmationDialog.Confirmed += OnReloadConfirmed;
		scanFeaturesButton.Pressed += OnScanFeaturesPressed;
		showFeatureOverlayButton.Toggled += OnFeatureOverlayToggled;
		featureSelector.ItemSelected += OnFeatureSelectorItemSelected;
		acceptFeatureButton.Pressed += OnAcceptFeaturePressed;
		dismissFeatureButton.Pressed += OnDismissFeaturePressed;
		if (restoreFeatureButton != null) restoreFeatureButton.Pressed += OnRestoreFeaturePressed;
		historyBackButton.Pressed += OnHistoryBackPressed;
		historyForwardButton.Pressed += OnHistoryForwardPressed;
		if (processingHistorySelector != null) processingHistorySelector.ItemSelected += OnProcessingHistorySelected;
		if (restoreProcessingConfigurationButton != null) restoreProcessingConfigurationButton.Pressed += OnRestoreProcessingConfigurationPressed;
		if (bookmarkProcessingConfigurationButton != null) bookmarkProcessingConfigurationButton.Toggled += OnProcessingBookmarkToggled;
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
		if (processingHistorySectionButton != null) processingHistorySectionButton.Pressed += OnProcessingHistorySectionPressed;
		candidateRegionSectionButton.Pressed += OnCandidateRegionSectionPressed;
		if (regionSequenceSectionButton != null) regionSequenceSectionButton.Pressed += OnRegionSequenceSectionPressed;
		featureSensingSectionButton.Pressed += OnFeatureSensingSectionPressed;
		structureSectionButton.Pressed += OnStructureSectionPressed;
		if (fragmentOverviewSectionButton != null) fragmentOverviewSectionButton.Pressed += OnFragmentOverviewSectionPressed;
		orientationSectionButton.Pressed += OnOrientationSectionPressed;
		if (correctionSectionButton != null) correctionSectionButton.Pressed += OnCorrectionSectionPressed;
		arrowSectionButton.Pressed += OnArrowSectionPressed;
		if (directionSectionButton != null) directionSectionButton.Pressed += OnDirectionSectionPressed;
		groupRegionsButton.Pressed += OnGroupRegionsPressed;
		showRegionOverlayButton.Toggled += OnRegionOverlayToggled;
		regionSelector.ItemSelected += OnRegionSelectorItemSelected;
		acceptRegionButton.Pressed += OnAcceptRegionPressed;
		dismissRegionButton.Pressed += OnDismissRegionPressed;
		if (restoreRegionButton != null) restoreRegionButton.Pressed += OnRestoreRegionPressed;
		addRegionButton.Pressed += OnAddRegionPressed;
		regionViewLockButton.Pressed += OnRegionViewLockPressed;
		navigateToRegionButton.Pressed += OnNavigateToRegionPressed;
		cancelNavigationButton.Pressed += OnCancelNavigationPressed;
		if (regionSequenceButton != null) regionSequenceButton.Toggled += OnRegionSequenceToggled;
		if (previousRegionPairButton != null) previousRegionPairButton.Pressed += OnPreviousRegionPairPressed;
		if (nextRegionPairButton != null) nextRegionPairButton.Pressed += OnNextRegionPairPressed;
		scanStructuresButton.Pressed += OnScanStructuresPressed;
		showStructureOverlayButton.Toggled += OnStructureOverlayToggled;
		structureSelector.ItemSelected += OnStructureSelectorItemSelected;
		acceptStructureButton.Pressed += OnAcceptStructurePressed;
		dismissStructureButton.Pressed += OnDismissStructurePressed;
		if (restoreStructureButton != null) restoreStructureButton.Pressed += OnRestoreStructurePressed;
		estimateOrientationButton.Pressed += OnEstimateOrientationPressed;
		previousOrientationRegionButton.Pressed += OnPreviousOrientationRegionPressed;
		nextOrientationRegionButton.Pressed += OnNextOrientationRegionPressed;
		quitOrientationViewButton.Pressed += OnQuitOrientationViewPressed;
		showOrientationOverlayButton.Toggled += OnOrientationOverlayToggled;
		orientationSelector.ItemSelected += OnOrientationSelectorItemSelected;
		previousOrientationButton.Pressed += OnPreviousOrientationPressed;
		acceptOrientationButton.Pressed += OnAcceptOrientationPressed;
		nextOrientationButton.Pressed += OnNextOrientationPressed;
		if (proposeCorrectionButton != null) proposeCorrectionButton.Pressed += OnProposeCorrectionPressed;
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
		if (restoreArrowButton != null) restoreArrowButton.Pressed += OnRestoreArrowPressed;
		mapDirectionButton.Pressed += OnMapDirectionPressed;
		// Rotation controls created programmatically; connect signals here.
		rotateCounterClockwiseButton.Pressed += OnRotateCounterClockwisePressed;
		rotateClockwiseButton.Pressed += OnRotateClockwisePressed;
		fineRotationSpinBox.ValueChanged += OnFineRotationChanged;
		AnalysisChanged += OnRegionSequenceAnalysisChanged;
    }

    private void DisconnectAutonomySignals()
    {
        if (!autonomySignalsConnected) return;
        autonomySignalsConnected = false;
		SetAutonomousWorkflowPopupCursor(false);

        fragmentCanvas.ViewChanged -= OnCanvasViewChanged;
        fragmentAnalysisRover.StatusChanged -= OnRoverStatusChanged;
        fragmentAnalysisRover.AllocationChanged -= RefreshAutonomyUi;
		fragmentAnalysisRover.HistoryChanged -= RefreshHistoryButtons;
		fragmentAnalysisRover.ProcessingHistoryChanged -= RefreshProcessingHistoryControls;
		fragmentAnalysisRover.ProcessingSearchChanged -= RefreshProcessingSearchControls;
		fragmentAnalysisRover.AutonomousWorkflowChanged -= OnAutonomousWorkflowChanged;
		if (IsInstanceValid(autonomousWorkflowPopup))
			autonomousWorkflowPopup.CloseRequested -= OnAutonomousWorkflowPopupCloseRequested;
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
		fragmentRoverOverlay.RegionLockRequested -= OnOverlayRegionLockRequested;
		fragmentRoverOverlay.StructureEditRequested -= OnStructureEditRequested;
		fragmentRoverOverlay.StructureFeatureToggled -= OnStructureFeatureToggled;
		fragmentRoverOverlay.StructureFeatureRemoved -= OnStructureFeatureRemoved;
		fragmentRoverOverlay.StructureStrokeDrawn -= OnStructureStrokeDrawn;
		fragmentRoverOverlay.StructureEditingCancelled -= OnStructureEditingCancelled;
		fragmentRoverOverlay.ArrowDrawn -= OnArrowDrawn;
        roverPanelToggleButton.Pressed -= OnRoverPanelTogglePressed;
		comparisonOpenButton.Pressed -= OnComparisonOpenPressed;
        autonomyOffButton.Toggled -= OnAutonomyOffToggled;
        autonomySupporterButton.Toggled -= OnAutonomySupporterToggled;
        autonomyPerformerButton.Toggled -= OnAutonomyPerformerToggled;
		if (roverPauseButton != null) roverPauseButton.Pressed -= OnRoverPausePressed;
		autonomousRegionReviewButton.Pressed -= OnAutonomousRegionReviewPressed;
		autonomousAddRegionButton.Pressed -= OnAutonomousAddRegionPressed;
		autonomousWorkflowContinueButton.Pressed -= OnAutonomousWorkflowContinuePressed;
		autonomousFindAnotherRegionButton.Pressed -= OnAutonomousFindAnotherRegionPressed;
		autonomousReviewArrowButton.Pressed -= OnAutonomousReviewArrowPressed;
		autonomousDrawArrowButton.Pressed -= OnAutonomousDrawArrowPressed;
		autonomousEditStructureButton.Pressed -= OnAutonomousEditStructurePressed;
		autonomousValidateStructureButton.Pressed -= OnAutonomousValidateStructurePressed;
        autonomyAdvancedButton.Pressed -= OnAutonomyAdvancedPressed;
        reloadConfirmationDialog.Confirmed -= OnReloadConfirmed;
		scanFeaturesButton.Pressed -= OnScanFeaturesPressed;
		showFeatureOverlayButton.Toggled -= OnFeatureOverlayToggled;
		featureSelector.ItemSelected -= OnFeatureSelectorItemSelected;
		acceptFeatureButton.Pressed -= OnAcceptFeaturePressed;
		dismissFeatureButton.Pressed -= OnDismissFeaturePressed;
		if (restoreFeatureButton != null) restoreFeatureButton.Pressed -= OnRestoreFeaturePressed;
		historyBackButton.Pressed -= OnHistoryBackPressed;
		historyForwardButton.Pressed -= OnHistoryForwardPressed;
		if (processingHistorySelector != null) processingHistorySelector.ItemSelected -= OnProcessingHistorySelected;
		if (restoreProcessingConfigurationButton != null) restoreProcessingConfigurationButton.Pressed -= OnRestoreProcessingConfigurationPressed;
		if (bookmarkProcessingConfigurationButton != null) bookmarkProcessingConfigurationButton.Toggled -= OnProcessingBookmarkToggled;
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
		if (processingHistorySectionButton != null) processingHistorySectionButton.Pressed -= OnProcessingHistorySectionPressed;
		candidateRegionSectionButton.Pressed -= OnCandidateRegionSectionPressed;
		if (regionSequenceSectionButton != null) regionSequenceSectionButton.Pressed -= OnRegionSequenceSectionPressed;
		featureSensingSectionButton.Pressed -= OnFeatureSensingSectionPressed;
		structureSectionButton.Pressed -= OnStructureSectionPressed;
		if (fragmentOverviewSectionButton != null) fragmentOverviewSectionButton.Pressed -= OnFragmentOverviewSectionPressed;
		orientationSectionButton.Pressed -= OnOrientationSectionPressed;
		if (correctionSectionButton != null) correctionSectionButton.Pressed -= OnCorrectionSectionPressed;
		arrowSectionButton.Pressed -= OnArrowSectionPressed;
		if (directionSectionButton != null) directionSectionButton.Pressed -= OnDirectionSectionPressed;
		groupRegionsButton.Pressed -= OnGroupRegionsPressed;
		showRegionOverlayButton.Toggled -= OnRegionOverlayToggled;
		regionSelector.ItemSelected -= OnRegionSelectorItemSelected;
		acceptRegionButton.Pressed -= OnAcceptRegionPressed;
		dismissRegionButton.Pressed -= OnDismissRegionPressed;
		if (restoreRegionButton != null) restoreRegionButton.Pressed -= OnRestoreRegionPressed;
		addRegionButton.Pressed -= OnAddRegionPressed;
		regionViewLockButton.Pressed -= OnRegionViewLockPressed;
		navigateToRegionButton.Pressed -= OnNavigateToRegionPressed;
		cancelNavigationButton.Pressed -= OnCancelNavigationPressed;
		if (regionSequenceButton != null) regionSequenceButton.Toggled -= OnRegionSequenceToggled;
		if (previousRegionPairButton != null) previousRegionPairButton.Pressed -= OnPreviousRegionPairPressed;
		if (nextRegionPairButton != null) nextRegionPairButton.Pressed -= OnNextRegionPairPressed;
		scanStructuresButton.Pressed -= OnScanStructuresPressed;
		showStructureOverlayButton.Toggled -= OnStructureOverlayToggled;
		structureSelector.ItemSelected -= OnStructureSelectorItemSelected;
		acceptStructureButton.Pressed -= OnAcceptStructurePressed;
		dismissStructureButton.Pressed -= OnDismissStructurePressed;
		if (restoreStructureButton != null) restoreStructureButton.Pressed -= OnRestoreStructurePressed;
		estimateOrientationButton.Pressed -= OnEstimateOrientationPressed;
		previousOrientationRegionButton.Pressed -= OnPreviousOrientationRegionPressed;
		nextOrientationRegionButton.Pressed -= OnNextOrientationRegionPressed;
		quitOrientationViewButton.Pressed -= OnQuitOrientationViewPressed;
		showOrientationOverlayButton.Toggled -= OnOrientationOverlayToggled;
		orientationSelector.ItemSelected -= OnOrientationSelectorItemSelected;
		previousOrientationButton.Pressed -= OnPreviousOrientationPressed;
		acceptOrientationButton.Pressed -= OnAcceptOrientationPressed;
		nextOrientationButton.Pressed -= OnNextOrientationPressed;
		if (proposeCorrectionButton != null) proposeCorrectionButton.Pressed -= OnProposeCorrectionPressed;
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
		if (restoreArrowButton != null) restoreArrowButton.Pressed -= OnRestoreArrowPressed;
		mapDirectionButton.Pressed -= OnMapDirectionPressed;
		rotateCounterClockwiseButton.Pressed -= OnRotateCounterClockwisePressed;
		rotateClockwiseButton.Pressed -= OnRotateClockwisePressed;
		fineRotationSpinBox.ValueChanged -= OnFineRotationChanged;
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
		if (command.Origin != FragmentAnalysisActionOrigin.Restore &&
			command.Parameter != FragmentAnalysisParameter.View)
			CancelStructureEditingForAction();

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
					if (command.RegionId is int regionId)
						fragmentCanvas.SetRegionRotationDegrees(
							regionId,
							command.RegionBounds,
							command.RotationPivotNormalized,
							command.FloatValue);
					else
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
			Origin = command.Origin,
			RegionId = command.RegionId
        });
    }
public void DispatchAnalysisConfiguration(
		FragmentAnalysisControlState configuration,
		FragmentAnalysisActionOrigin origin)
	{
		if (configuration == null || isApplyingAnalysisCommand) return;
		if (origin != FragmentAnalysisActionOrigin.Restore)
			CancelStructureEditingForAction();

		FragmentAnalysisControlState previous = lastControlState ?? CaptureControlState();
		isApplyingAnalysisCommand = true;
		try
		{
			polarizationButton.ButtonPressed = configuration.PolarizationEnabled;
			spectralButton.ButtonPressed = configuration.SpectralEnabled;
			surfaceButton.ButtonPressed = configuration.SurfaceEnabled;
			electromagneticButton.ButtonPressed = configuration.ElectromagneticEnabled;
			resonanceButton.ButtonPressed = configuration.ResonanceEnabled;
			xRayButton.ButtonPressed = configuration.XRayEnabled;
			polarizationSlider.Value = Mathf.Clamp(configuration.PolarizationLevel, 1, 5);
			spectralSlider.Value = Mathf.Clamp(configuration.SpectralLevel, 1, 5);
			surfaceSlider.Value = Mathf.Clamp(configuration.SurfaceLevel, 1, 5);

			fragmentCanvas.SetProcessingConfiguration(configuration);
			UpdateProcessingLabels();
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
			Parameter = FragmentAnalysisParameter.Configuration,
			Origin = origin
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
		CancelStructureEditingForAction();
		if (fragmentAnalysisRover.TryStartAutonomousFeatureSearch()) return;
		fragmentAnalysisRover.RefreshDetectedFeatures(
			true,
			recordHistory: true,
			playerRequested: true);
	}

	private void OnFeatureOverlayToggled(bool visible)
	{
		fragmentRoverOverlay.SetShowFeatures(visible);
		regionSequenceView.SetShowFeatures(visible);
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
			if (IsInstanceValid(restoreFeatureButton))
				restoreFeatureButton.Disabled = true;
			return;
		}

		selectedFeatureLabel.Text =
			$"FEATURE {selected.Id}: {selected.Provenance.ToString().ToUpperInvariant()} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()}";
		bool canEditOnCurrentPage = fragmentAnalysisRover.CanEditFeatureOnCurrentReviewPage(selected.Id);
		acceptFeatureButton.Disabled = !canEditOnCurrentPage ||
			selected.Disposition == FragmentAnnotationDisposition.Accepted;
		dismissFeatureButton.Disabled = !canEditOnCurrentPage ||
			selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		if (IsInstanceValid(restoreFeatureButton))
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
		CancelStructureEditingForAction();
		SetOrientationSectionExpanded(false);
		if (regionSequenceView.Visible)
			OnSequenceExitRequested();
		RefreshOrientationPresentation();
		// Folding comparison/orientation UI can resize the analyzer. Re-sample on the next frame so
		// normalized overlays are derived from the final canvas aspect ratio, not the pre-fold layout.
		Callable.From(ReconcilePostRotationGeometry).CallDeferred();
	}

	private void ReconcilePostRotationGeometry()
	{
		if (!IsInstanceValid(fragmentCanvas) || fragmentAnalysisRover?.State == null) return;
		UpdateFeatureOverlayView();
		fragmentAnalysisRover.RefreshDetectedFeatures(
			force: true,
			retainUnmatchedReviewed: false,
			requestSelectedFeatureFocus: false);
		if (fragmentAnalysisRover.AutonomousWorkflowStage is
			FragmentAutonomousWorkflowStage.AwaitingArrowReview or
			FragmentAutonomousWorkflowStage.AwaitingPlayerArrow)
			fragmentAnalysisRover.RefreshArrowCandidates(false);
	}

	private void OnRotationExecutionChanged()
	{
		if (fragmentAnalysisRover.IsRotationInProgress)
		{
			CancelStructureEditingForAction();
			if (regionSequenceView.Visible)
				OnSequenceExitRequested();
			RefreshOrientationPresentation();
		}
		RefreshRotationCorrectionControls();
	}

	private void OnEstimateOrientationPressed()
	{
		CancelStructureEditingForAction();
		fragmentAnalysisRover.EstimateOrientationHypotheses(true);
	}

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
		if (regionSequenceView.Visible)
			OnSequenceExitRequested();
		RefreshOrientationPresentation();
	}

	private void OnOrientationOverlayToggled(bool visible)
	{
		bool enabled = visible && fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.InterpretUprightOrientation) != FragmentAutonomyMode.Off;
		fragmentRoverOverlay.SetShowOrientations(enabled);
		RefreshOrientationPresentation(enabled);
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
				$"H{hypothesis.Id} · {hypothesis.AxisDegrees:+0.0;-0.0;0.0}° · {disposition}",
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
			acceptOrientationButton.Disabled = true;
			previousOrientationButton.Disabled = true;
			nextOrientationButton.Disabled = true;
			orientationStepLabel.Text = "H—";
			return;
		}
		selectedOrientationLabel.Text =
			$"H{selected.Id}: AXIS {selected.AxisDegrees:+0.0;-0.0;0.0}° · " +
			FormatOrientationDisposition(selected.Disposition);
		orientationStepLabel.Text = $"H{selected.Id}";
		bool hasAlternatives = state.OrientationHypotheses.Count > 1;
		previousOrientationButton.Disabled = !hasAlternatives;
		nextOrientationButton.Disabled = !hasAlternatives;
		acceptOrientationButton.Disabled =
			selected.Disposition == FragmentAnnotationDisposition.Accepted;
	}

	private void RefreshRotationCorrectionControls()
	{
		// correctionSectionButton is now null (section merged into ORIENTATION); guard accordingly.
		if (fragmentAnalysisRover?.State == null || acceptCorrectionButton == null) return;
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
		if (proposeCorrectionButton != null)
			proposeCorrectionButton.Disabled = state.IsPaused || mode == FragmentAutonomyMode.Off ||
				accepted == null || fragmentAnalysisRover.IsRotationInProgress;
		if (proposeCorrectionButton != null)
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
					? "ORIENTATION ERROR: Accept an orientation hypothesis first"
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
				$" · R{correction.RegionId} · H{correction.SourceOrientationId}{disposition}";
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

	private void OnDetectArrowsPressed()
	{
		CancelStructureEditingForAction();
		fragmentAnalysisRover.RefreshArrowCandidates(true);
	}

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
		if (state == null) return;
		List<FragmentArrowCandidate> regional = state.ArrowCandidates.FindAll(candidate =>
			candidate.RegionId < 0 || candidate.RegionId == state.SelectedRegionId);
		if (regional.Count == 0) return;
		int index = regional.FindIndex(candidate =>
			candidate.Id == state.SelectedArrowId);
		if (index < 0) index = 0;
		else index = (index + offset + regional.Count) % regional.Count;
		fragmentAnalysisRover.ApplyArrowEdit(
			FragmentArrowEditAction.Select,
			regional[index].Id);
	}

	private void OnDrawArrowToggled(bool armed)
	{
		if (armed) CancelStructureEditingForAction();
		fragmentRoverOverlay.SetArrowDrawingArmed(armed);
	}

	private void OnArrowDrawn(Vector2 tail, Vector2 tip)
	{
		drawArrowButton.SetPressedNoSignal(false);
		isSubmittingPlayerArrow = true;
		try
		{
			fragmentAnalysisRover.DefinePlayerArrow(tail, tip);
		}
		finally
		{
			isSubmittingPlayerArrow = false;
		}
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
			if (candidate.RegionId >= 0 && candidate.RegionId != state.SelectedRegionId) continue;
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
			candidate.Id == selectedId &&
			(candidate.RegionId < 0 || candidate.RegionId == state.SelectedRegionId));
		FragmentAutonomyMode mode = fragmentAnalysisRover.GetEffectiveMode(
			FragmentAutonomyCapability.SenseDirectionalArrow);
		detectArrowsButton.Disabled = state.IsPaused || mode == FragmentAutonomyMode.Off;
		fragmentRoverOverlay.SetShowArrows(showArrowOverlayButton.ButtonPressed);
		bool hasAlternatives = arrowSelector.ItemCount > 1;
		previousArrowButton.Disabled = !hasAlternatives;
		nextArrowButton.Disabled = !hasAlternatives;
		if (selected == null)
		{
			arrowLabel.Text = "ARROW: No candidates; detect geometry or draw tail-to-tip";
			arrowStepLabel.Text = "A—";
			acceptArrowButton.Disabled = true;
			rejectArrowButton.Disabled = true;
			if (IsInstanceValid(restoreArrowButton))
				restoreArrowButton.Disabled = true;
			return;
		}
		string sourceLabel = selected.IsPlayerDefined ? "PLAYER-DRAWN" : "GEOMETRY-ONLY ROVER";
		arrowLabel.Text = $"A{selected.Id}: {sourceLabel} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()}\nEVIDENCE: {selected.Evidence}";
		arrowStepLabel.Text = $"A{selected.Id}";
		acceptArrowButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Accepted;
		rejectArrowButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		if (IsInstanceValid(restoreArrowButton))
			restoreArrowButton.Disabled =
				selected.Disposition == FragmentAnnotationDisposition.Proposed;
	}

	private void OnDirectionInterpretationChanged()
	{
		RefreshDirectionControls();
		if (fragmentAnalysisRover.State?.DirectionInterpretation != null)
			SetDirectionSectionExpanded(true);
	}

	private void OnMapDirectionPressed()
	{
		CancelStructureEditingForAction();
		fragmentAnalysisRover.ComputeDirectionInterpretation(true, playerRequested: true);
	}

	private void RefreshDirectionControls()
	{
		if (!IsInstanceValid(directionInset) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		FragmentDirectionInterpretation mapped = state.DirectionInterpretation;
		directionInset.SetDirection(mapped);
		bool hasAcceptedArrow = state.AcceptedArrowId is int arrowId &&
			state.ArrowCandidates.Exists(candidate =>
				candidate.Id == arrowId &&
				(candidate.RegionId < 0 || candidate.RegionId == state.SelectedRegionId) &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted);
		directionStatusLabel.Text = mapped == null
			? "BEARING: Accept one Arrow; screen up is north"
			: "MINIMAP: BEARING RAY ADDED AT FRAGMENT LOCATION";
		mapDirectionButton.Disabled = !hasAcceptedArrow;
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

	private void OnScanStructuresPressed()
	{
		CancelStructureEditingForAction();
		fragmentAnalysisRover.RefreshStructures(true);
	}

	private void OnStructureOverlayToggled(bool visible)
	{
		if (!visible)
		{
			fragmentRoverOverlay.SetStructureEditing(false);
			OnStructureEditingCancelled();
		}
		fragmentRoverOverlay.SetShowStructures(visible);
		regionSequenceView.SetShowStructures(visible);
	}

	private void OnStructureSelectorItemSelected(long index)
	{
		if (isSyncingStructureSelector || index < 0 || index >= structureSelector.ItemCount) return;
		int selectedId = structureSelector.GetItemId((int)index);
		fragmentRoverOverlay.SetStructureEditing(false);
		fragmentAnalysisRover.ApplyStructureEdit(FragmentStructureEditAction.Select, selectedId);
	}

	private void OnStructureEditRequested(int regionId, int structureId)
	{
		if (fragmentRoverOverlay.IsEditingStructure(regionId, structureId))
		{
			CancelStructureEditingForAction();
			return;
		}
		if (regionSequenceView.Visible) OnSequenceExitRequested();
		fragmentAnalysisRover.ApplyRegionEdit(FragmentRegionEditAction.Select, regionId);
		fragmentAnalysisRover.ApplyStructureEdit(FragmentStructureEditAction.Select, structureId);
		showStructureOverlayButton.ButtonPressed = true;
		showFeatureOverlayButton.ButtonPressed = true;
		fragmentRoverOverlay.SetRegionDrawingArmed(false);
		addRegionButton.Text = "DRAW REGION";
		fragmentRoverOverlay.SetStructureEditing(true, structureId, regionId);
		SetStructureSectionExpanded(true);
		RefreshStructureControls();
	}

	private void OnStructureFeatureToggled(int featureId) =>
		PreserveViewDuringStructureEdit(() =>
			fragmentAnalysisRover.ToggleSelectedStructureFeature(featureId));

	private void OnStructureFeatureRemoved(int featureId) =>
		PreserveViewDuringStructureEdit(() =>
			fragmentAnalysisRover.RemoveSelectedStructureFeature(featureId));

	private void OnStructureStrokeDrawn(Vector2 start, Vector2 end) =>
		PreserveViewDuringStructureEdit(() =>
			fragmentAnalysisRover.AddPlayerStrokeToSelectedStructure(start, end));

	private void PreserveViewDuringStructureEdit(Action edit)
	{
		float retainedZoom = fragmentCanvas.ViewZoom;
		Vector2 retainedPan = fragmentCanvas.ViewPan;
		edit?.Invoke();
		if (!Mathf.IsEqualApprox(fragmentCanvas.ViewZoom, retainedZoom) ||
			fragmentCanvas.ViewPan.DistanceSquaredTo(retainedPan) > 0.0001f)
			fragmentCanvas.RestoreView(
				retainedZoom,
				retainedPan,
				FragmentAnalysisActionOrigin.System);
	}

	private void OnStructureEditingCancelled()
	{
		RefreshStructureControls();
	}

	private void CancelStructureEditingForAction()
	{
		if (!IsInstanceValid(fragmentRoverOverlay) || !fragmentRoverOverlay.IsStructureEditing) return;
		fragmentRoverOverlay.SetStructureEditing(false);
		RefreshStructureControls();
	}

	private void OnAcceptStructurePressed() =>
		ApplySelectedStructureEdit(FragmentStructureEditAction.Accept);

	private void OnDismissStructurePressed()
	{
		fragmentRoverOverlay.SetStructureEditing(false);
		ApplySelectedStructureEdit(FragmentStructureEditAction.Dismiss);
	}

	private void OnRestoreStructurePressed() =>
		ApplySelectedStructureEdit(FragmentStructureEditAction.Restore);

	private void ApplySelectedStructureEdit(FragmentStructureEditAction action)
	{
		if (fragmentAnalysisRover.State?.SelectedStructureId is int structureId &&
			fragmentAnalysisRover.CanEditStructureOnCurrentReviewPage(structureId))
			fragmentAnalysisRover.ApplyStructureEdit(action, structureId);
	}

	private void RefreshStructureControls()
	{
		if (!IsInstanceValid(structureSelector) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomyState state = fragmentAnalysisRover.State;
		bool autonomousStructureGate = fragmentAnalysisRover.AutonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.AwaitingStructureReview;
		acceptStructureButton.Text = autonomousStructureGate
			? "VALIDATE & CONTINUE"
			: "ACCEPT";
		bool includeRover = AreRoverStructuresVisible();
		int selectedId = state.SelectedStructureId ?? -1;
		isSyncingStructureSelector = true;
		structureSelector.Clear();
		int selectedIndex = -1;
		foreach (FragmentDetectedStructure structure in state.DetectedStructures)
		{
			if (!includeRover && structure.Provenance == FragmentAnnotationProvenance.Rover) continue;
			if (regionSequenceView.Visible &&
				!fragmentAnalysisRover.CanEditStructureOnCurrentReviewPage(structure.Id)) continue;
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
		scanStructuresButton.Disabled = state.IsPaused ||
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.SenseReconstructedStructures) == FragmentAutonomyMode.Off;

		if (selected == null)
		{
			selectedStructureLabel.Text = "STRUCTURE: None selected";
			acceptStructureButton.Disabled = true;
			dismissStructureButton.Disabled = true;
			if (IsInstanceValid(restoreStructureButton))
				restoreStructureButton.Disabled = true;
		}
		else
		{
			bool canEditOnCurrentPage =
				fragmentAnalysisRover.CanEditStructureOnCurrentReviewPage(selected.Id);
			selectedStructureLabel.Text =
				$"STRUCTURE {selected.Id}: " +
				$"{selected.Provenance.ToString().ToUpperInvariant()} · " +
				$"{FragmentCandidateValidityPolicy.DescribeStructureDisposition(selected.Disposition)} · " +
				$"{selected.FeatureIds.Count} FEATURES" +
				(fragmentRoverOverlay.IsStructureEditing
					? "\nEDIT: click to add/select · Delete removes · drag draws a stroke"
					: string.Empty);
			acceptStructureButton.Disabled =
				!canEditOnCurrentPage ||
				(!autonomousStructureGate &&
				 selected.Disposition == FragmentAnnotationDisposition.Accepted) ||
				selected.FeatureIds.Count == 0;
			dismissStructureButton.Disabled =
				!canEditOnCurrentPage ||
				selected.Disposition == FragmentAnnotationDisposition.Dismissed;
			if (IsInstanceValid(restoreStructureButton))
				restoreStructureButton.Disabled =
					!canEditOnCurrentPage ||
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
		RefreshArrowControls();
		RefreshOrientationControls();
		RefreshRegionSequence();
		RefreshProcessingSearchControls();
	}

	private void OnRegionFocusRequested(int regionId)
	{
		if (isNavigatingHistory) return;
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
		if (regionSequenceView.Visible)
			OnSequenceExitRequested();
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
		CancelStructureEditingForAction();
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
		RefreshRegionSequence();
		if (!isNavigatingHistory && acceptedRegionCount >= 2 && regionSequenceView.RegionCount >= 2)
			OnComparisonOpenPressed();
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
		bool orientationVisible = showOrientationOverlayButton.ButtonPressed &&
			fragmentAnalysisRover.GetEffectiveMode(
				FragmentAutonomyCapability.InterpretUprightOrientation) != FragmentAutonomyMode.Off;
		bool isolate = isOrientationSectionExpanded && orientationVisible && structure != null &&
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
		CancelStructureEditingForAction();
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
			CancelStructureEditingForAction();
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

	private void OnAcceptRegionPressed()
	{
		CancelStructureEditingForAction();
		ApplySelectedRegionEdit(FragmentRegionEditAction.Accept);
	}
	private void OnDismissRegionPressed()
	{
		CancelStructureEditingForAction();
		ApplySelectedRegionEdit(FragmentRegionEditAction.Dismiss);
	}
	private void OnRestoreRegionPressed() => ApplySelectedRegionEdit(FragmentRegionEditAction.Restore);
	private void OnRegionViewLockPressed()
	{
		CancelStructureEditingForAction();
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
			selectedRegionLabel.Text = "REGION OF INTEREST: None selected";
			acceptRegionButton.Disabled = true;
			dismissRegionButton.Disabled = true;
			if (restoreRegionButton != null) restoreRegionButton.Disabled = true;
			regionViewLockButton.Disabled = true;
			regionViewLockButton.Text = "LOCK";
			return;
		}
		bool locked = fragmentAnalysisRover.IsRegionViewLocked(selected.Id);
		selectedRegionLabel.Text =
			$"REGION OF INTEREST {selected.Id}: {selected.Provenance.ToString().ToUpperInvariant()} · " +
			$"{selected.Disposition.ToString().ToUpperInvariant()}" +
			(locked ? " · VIEW LOCKED" : string.Empty);
		acceptRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Accepted;
		dismissRegionButton.Disabled = selected.Disposition == FragmentAnnotationDisposition.Dismissed;
		if (restoreRegionButton != null)
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
		fragmentAnalysisRover.SetStructureReviewPriority(
			regionSequenceView.Visible ? regionSequenceView.DisplayedRegionIds : Array.Empty<int>());
		RefreshStructureControls();
		if (comparisonOpenButton != null)
			comparisonOpenButton.Disabled = regionSequenceView.RegionCount < 2 || regionSequenceView.Visible;
		RefreshRegionSequenceControls();
	}

	private void OnComparisonOpenPressed()
	{
		if (regionSequenceView.RegionCount >= 2)
		{
			regionSequenceView.Visible = true;
			fragmentRoverOverlay.Visible = false;
			comparisonOpenButton.Disabled = true;
			if (regionSequenceButton != null) regionSequenceButton.SetPressedNoSignal(true);
			fragmentAnalysisRover.SetFeatureReviewPriority(regionSequenceView.DisplayedRegionIds);
			fragmentAnalysisRover.SetStructureReviewPriority(regionSequenceView.DisplayedRegionIds);
			RefreshStructureControls();
			RefreshRegionSequenceControls();
		}
	}

	private void OnSequenceExitRequested()
	{
		regionSequenceView.Visible = false;
		fragmentRoverOverlay.Visible = true;
		comparisonOpenButton.Disabled = regionSequenceView.RegionCount < 2;
		if (regionSequenceButton != null) regionSequenceButton.SetPressedNoSignal(false);
		fragmentAnalysisRover.SetFeatureReviewPriority(Array.Empty<int>());
		fragmentAnalysisRover.SetStructureReviewPriority(Array.Empty<int>());
		RefreshStructureControls();
		RefreshRegionSequenceControls();
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
		fragmentAnalysisRover.SetStructureReviewPriority(
			regionSequenceView.Visible ? regionSequenceView.DisplayedRegionIds : Array.Empty<int>());
		if (regionSequenceView.Visible &&
			fragmentAnalysisRover.State.SelectedRegionId is int selectedRegionId)
			regionSequenceView.EnsureRegionVisible(selectedRegionId);
		RefreshOrientationPresentation();
		bool available = regionSequenceView.RegionCount >= 2;
		if (regionSequenceButton != null) regionSequenceButton.Disabled = !available;
		if (comparisonOpenButton != null) comparisonOpenButton.Disabled = !available || regionSequenceView.Visible;
		if (!available)
		{
			if (regionSequenceButton != null) regionSequenceButton.ButtonPressed = false;
			regionSequenceView.Visible = false;
			fragmentRoverOverlay.Visible = true;
			fragmentAnalysisRover.SetFeatureReviewPriority(Array.Empty<int>());
			fragmentAnalysisRover.SetStructureReviewPriority(Array.Empty<int>());
		}
		RefreshRegionSequenceControls();
	}

	private void RefreshRegionSequenceControls()
	{
		if (regionSequenceLabel != null) regionSequenceLabel.Text = regionSequenceView.PageText;
		if (previousRegionPairButton != null)
			previousRegionPairButton.Disabled = !regionSequenceView.Visible || !regionSequenceView.CanGoPrevious;
		if (nextRegionPairButton != null)
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
		fragmentAnalysisRover.SetStructureReviewPriority(
			regionSequenceView.Visible ? regionSequenceView.DisplayedRegionIds : Array.Empty<int>());
		RefreshFeatureControls();
		RefreshStructureControls();
		RefreshRegionSequenceControls();
	}

	private void OnSequenceRegionActionRequested(int regionId, FragmentRegionEditAction action)
	{
		fragmentAnalysisRover.ApplyRegionEdit(action, regionId, applyCropOnAccept: false);
	}

	private void OnSequenceRegionLockRequested(int regionId)
	{
		CancelStructureEditingForAction();
		fragmentAnalysisRover.ToggleRegionViewLock(regionId);
	}

	private void OnOverlayRegionLockRequested(int regionId)
	{
		CancelStructureEditingForAction();
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
        fragmentAnalysisRover.ResetForPuzzle();
        fragmentRoverOverlay.SetState(fragmentAnalysisRover.State);
        lastControlState = CaptureControlState();
        UpdateRotationLabel();
        RefreshAutonomyUi();
    }

    private void OnAutonomyOffToggled(bool pressed)
    {
        if (!pressed || isSyncingAutonomyUi) return;
		CancelStructureEditingForAction();
        fragmentAnalysisRover.SetMode(FragmentAutonomyMode.Off);
    }

    private void OnAutonomySupporterToggled(bool pressed)
    {
        if (!pressed || isSyncingAutonomyUi) return;
		CancelStructureEditingForAction();
        fragmentAnalysisRover.SetMode(FragmentAutonomyMode.Supporter);
    }

    private void OnAutonomyPerformerToggled(bool pressed)
    {
        if (!pressed || isSyncingAutonomyUi) return;
		CancelStructureEditingForAction();
        fragmentAnalysisRover.SetMode(FragmentAutonomyMode.Performer);
		StartPerformerWorkflowImmediately();
    }

	private void StartPerformerWorkflowImmediately()
	{
		if (regionSequenceView.Visible) OnSequenceExitRequested();
		fragmentCanvas.RestoreView(0f, Vector2.Zero, FragmentAnalysisActionOrigin.Rover);
		fragmentAnalysisRover.StartAutonomousWorkflow();
	}

	private void OnRoverPausePressed()
	{
		if (fragmentAnalysisRover.IsAutonomousWorkflowWaitingForPlayer)
		{
			ShowAutonomousWorkflowPopup();
			return;
		}
		if (!fragmentAnalysisRover.IsAutonomousWorkflowActive)
			fragmentAnalysisRover.StartAutonomousWorkflow();
		else if (!fragmentAnalysisRover.IsAutonomousWorkflowWaitingForPlayer)
			fragmentAnalysisRover.SetPaused(!fragmentAnalysisRover.State.IsPaused);
        RefreshAutonomyUi();
    }

	private void OnAutonomousWorkflowContinuePressed()
	{
		if (fragmentAnalysisRover.AutonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.Complete)
		{
			HideAutonomousWorkflowPopup();
			return;
		}
		if (fragmentAnalysisRover.AutonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.AwaitingFeatureReview)
		{
			HideAutonomousWorkflowPopup();
			return;
		}
		CancelStructureEditingForAction();
		fragmentAnalysisRover.ContinueAutonomousWorkflow();
	}

	private void OnAutonomousRegionReviewPressed()
	{
		FragmentCandidateRegion first = null;
		foreach (FragmentCandidateRegion region in fragmentAnalysisRover.State.CandidateRegions)
		{
			if (region.Disposition != FragmentAnnotationDisposition.Proposed) continue;
			if (first == null || region.Id < first.Id) first = region;
		}
		if (first == null) return;
		SetCandidateRegionSectionExpanded(true);
		showRegionOverlayButton.ButtonPressed = true;
		fragmentAnalysisRover.ApplyRegionEdit(FragmentRegionEditAction.Select, first.Id);
		fragmentCanvas.FocusNormalizedRect(
			first.NormalizedBounds,
			FragmentAnalysisActionOrigin.Rover);
		HideAutonomousWorkflowPopup();
	}

	private void OnAutonomousAddRegionPressed()
	{
		HideAutonomousWorkflowPopup();
		SetCandidateRegionSectionExpanded(true);
		if (addRegionButton.Text != "CANCEL DRAW") OnAddRegionPressed();
	}

	private void OnAutonomousFindAnotherRegionPressed()
	{
		HideAutonomousWorkflowPopup();
		fragmentAnalysisRover.FindAnotherAutonomousRegionSet();
	}

	private void OnAutonomousReviewArrowPressed()
	{
		HideAutonomousWorkflowPopup();
		SetArrowSectionExpanded(true);
		showArrowOverlayButton.SetPressedNoSignal(true);
		fragmentRoverOverlay.SetShowArrows(true);
	}

	private void OnAutonomousDrawArrowPressed()
	{
		HideAutonomousWorkflowPopup();
		SetArrowSectionExpanded(true);
		showArrowOverlayButton.SetPressedNoSignal(true);
		fragmentRoverOverlay.SetShowArrows(true);
		drawArrowButton.ButtonPressed = true;
	}

	private void OnAutonomousEditStructurePressed()
	{
		FragmentAutonomyState state = fragmentAnalysisRover?.State;
		if (state?.SelectedRegionId is not int regionId ||
			state.SelectedStructureId is not int structureId) return;
		HideAutonomousWorkflowPopup();
		OnStructureEditRequested(regionId, structureId);
	}

	private void OnAutonomousValidateStructurePressed()
	{
		HideAutonomousWorkflowPopup();
		CancelStructureEditingForAction();
		ApplySelectedStructureEdit(FragmentStructureEditAction.Accept);
	}

	private void OnAutonomousWorkflowChanged(FragmentAutonomousWorkflowStage stage)
	{
		if (!IsInstanceValid(regionSequenceView)) return;
		if (lastPresentedAutonomousWorkflowStage == stage)
		{
			RefreshAutonomousWorkflowUi();
			return;
		}
		lastPresentedAutonomousWorkflowStage = stage;
		switch (stage)
		{
			case FragmentAutonomousWorkflowStage.SearchingRegions:
			case FragmentAutonomousWorkflowStage.AwaitingRegionReview:
				if (regionSequenceView.Visible) OnSequenceExitRequested();
				SetCandidateRegionSectionExpanded(true);
				break;
			case FragmentAutonomousWorkflowStage.SearchingRegionFeatures:
			case FragmentAutonomousWorkflowStage.AwaitingFeatureReview:
				RefreshRegionSequence();
				if (!regionSequenceView.Visible && regionSequenceView.RegionCount >= 2)
					OnComparisonOpenPressed();
				if (fragmentAnalysisRover.State?.SelectedRegionId is int featureRegionId)
				{
					if (regionSequenceView.RegionCount >= 2)
						regionSequenceView.EnsureRegionVisible(featureRegionId);
					else
					{
						FragmentCandidateRegion featureRegion =
							fragmentAnalysisRover.State.CandidateRegions.Find(candidate =>
								candidate.Id == featureRegionId &&
								candidate.Disposition == FragmentAnnotationDisposition.Accepted);
						if (featureRegion != null)
							fragmentCanvas.FocusNormalizedRect(
								featureRegion.NormalizedBounds,
								FragmentAnalysisActionOrigin.Rover);
					}
				}
				SetFeatureSensingSectionExpanded(true);
				break;
			case FragmentAutonomousWorkflowStage.AwaitingRegionChoice:
				RefreshRegionSequence();
				if (!regionSequenceView.Visible && regionSequenceView.RegionCount >= 2)
					OnComparisonOpenPressed();
				SetCandidateRegionSectionExpanded(true);
				break;
			case FragmentAutonomousWorkflowStage.AwaitingStructureReview:
				if (regionSequenceView.Visible) OnSequenceExitRequested();
				SetCandidateRegionSectionExpanded(false);
				SetFeatureSensingSectionExpanded(false);
				SetOrientationSectionExpanded(false);
				SetArrowSectionExpanded(false);
				capabilityOverridesScroll.Visible = false;
				autonomyAdvancedButton.Text = "TASK ALLOCATION";
				if (fragmentAnalysisRover.State?.SelectedRegionId is int structureRegionId)
				{
					FragmentCandidateRegion region = fragmentAnalysisRover.State.CandidateRegions.Find(
						candidate => candidate.Id == structureRegionId);
					if (region != null)
						fragmentCanvas.FocusNormalizedRect(
							region.NormalizedBounds, FragmentAnalysisActionOrigin.Rover);
				}
				SetStructureSectionExpanded(true);
				break;
			case FragmentAutonomousWorkflowStage.AwaitingOrientationReview:
				if (regionSequenceView.Visible) OnSequenceExitRequested();
				// Enter through the same path as ESTIMATE AXES, then make the first observable
				// hypothesis the active H1 presentation before the human-gate popup appears.
				if (fragmentAnalysisRover.State.OrientationHypotheses.Count == 0)
					OnEstimateOrientationPressed();
				FragmentOrientationHypothesis firstOrientation =
					fragmentAnalysisRover.State.OrientationHypotheses.Count > 0
						? fragmentAnalysisRover.State.OrientationHypotheses[0]
						: null;
				if (firstOrientation != null &&
					fragmentAnalysisRover.State.SelectedOrientationId != firstOrientation.Id)
					fragmentAnalysisRover.ApplyOrientationEdit(
						FragmentOrientationEditAction.Select,
						firstOrientation.Id);
				showOrientationOverlayButton.SetPressedNoSignal(true);
				fragmentRoverOverlay.SetShowOrientations(true);
				SetOrientationSectionExpanded(true);
				RefreshOrientationPresentation(true);
				break;
			case FragmentAutonomousWorkflowStage.WaitingForRotation:
				SetOrientationSectionExpanded(true);
				break;
			case FragmentAutonomousWorkflowStage.AwaitingArrowReview:
			case FragmentAutonomousWorkflowStage.AwaitingPlayerArrow:
				if (regionSequenceView.Visible) OnSequenceExitRequested();
				SetCandidateRegionSectionExpanded(false);
				SetFeatureSensingSectionExpanded(false);
				SetStructureSectionExpanded(false);
				SetOrientationSectionExpanded(false);
				capabilityOverridesScroll.Visible = false;
				autonomyAdvancedButton.Text = "TASK ALLOCATION";
				showArrowOverlayButton.SetPressedNoSignal(true);
				fragmentRoverOverlay.SetShowArrows(true);
				SetArrowSectionExpanded(true);
				if (stage == FragmentAutonomousWorkflowStage.AwaitingPlayerArrow)
					drawArrowButton.ButtonPressed = true;
				break;
			case FragmentAutonomousWorkflowStage.Complete:
				CancelStructureEditingForAction();
				UpdateFragmentLifecycleLabel(isRestoredSession, wasEverSolved);
				break;
		}
		RefreshAutonomousWorkflowUi();
		if (fragmentAnalysisRover.IsAutonomousWorkflowWaitingForPlayer &&
			!isSubmittingPlayerArrow)
			ShowAutonomousWorkflowPopup();
	}

	private void ShowAutonomousWorkflowPopup()
	{
		if (!IsInstanceValid(autonomousWorkflowPopup) ||
			!fragmentAnalysisRover.IsAutonomousWorkflowWaitingForPlayer) return;
		bool showReference = fragmentAnalysisRover.AutonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.AwaitingRegionChoice;
		bool regionReview = fragmentAnalysisRover.AutonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.AwaitingRegionReview;
		bool structureReview = fragmentAnalysisRover.AutonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.AwaitingStructureReview;
		autonomousWorkflowPopup.PopupCentered(
			showReference
				? new Vector2I(500, 430)
				: regionReview ? new Vector2I(540, 270)
				: structureReview ? new Vector2I(540, 250)
				: new Vector2I(500, 210));
		SetAutonomousWorkflowPopupCursor(true);
	}

	private void OnAutonomousWorkflowPopupCloseRequested()
	{
		HideAutonomousWorkflowPopup();
	}

	private void HideAutonomousWorkflowPopup()
	{
		if (IsInstanceValid(autonomousWorkflowPopup)) autonomousWorkflowPopup.Hide();
		SetAutonomousWorkflowPopupCursor(false);
	}

	private void SetAutonomousWorkflowPopupCursor(bool popupVisible)
	{
		GetNodeOrNull<Game.Autoload.Cursor>("/root/Cursor")
			?.SetPopupCursorOverride(popupVisible);
	}

	private void RefreshAutonomousWorkflowUi()
	{
		if (!IsInstanceValid(roverPauseButton) || fragmentAnalysisRover?.State == null) return;
		FragmentAutonomousWorkflowStage stage = fragmentAnalysisRover.AutonomousWorkflowStage;
		bool performer = fragmentAnalysisRover.State.GlobalMode == FragmentAutonomyMode.Performer;
		bool waiting = fragmentAnalysisRover.IsAutonomousWorkflowWaitingForPlayer;
		roverPauseButton.Disabled = !performer;
		roverPauseButton.Text = stage == FragmentAutonomousWorkflowStage.Complete
			? "SHOW COMPLETION"
			: !fragmentAnalysisRover.IsAutonomousWorkflowActive
			? "PLAY ROVER"
			: waiting
				? "SHOW ROVER REQUEST"
				: fragmentAnalysisRover.State.IsPaused ? "RESUME" : "PAUSE";

		string prompt = stage switch
		{
			FragmentAutonomousWorkflowStage.AwaitingRegionReview =>
				"ROVER: Validate or refine every proposed region of interest.\n" +
				"Tip: double-click a region to resize it.",
			FragmentAutonomousWorkflowStage.AwaitingFeatureReview =>
				"ROVER: Validate or dismiss each visible Feature in the focused Region.",
			FragmentAutonomousWorkflowStage.AwaitingRegionChoice =>
				"ROVER: Compare the scanned fragment reference and select the meaningful Region.",
			FragmentAutonomousWorkflowStage.AwaitingStructureReview =>
				"ROVER: Review or edit the reconstruction, then validate it to continue.",
			FragmentAutonomousWorkflowStage.AwaitingOrientationReview =>
				"ROVER: Compare the scanned fragment reference with each orientation hypothesis and accept one.",
			FragmentAutonomousWorkflowStage.AwaitingArrowReview =>
				"ROVER: Accept or reject the proposed directional Arrow.",
			FragmentAutonomousWorkflowStage.AwaitingPlayerArrow =>
				"ROVER: No arrow was detected. Draw one from tail to tip.",
			FragmentAutonomousWorkflowStage.Complete =>
				"ANALYSIS COMPLETED\nWorld bearing was added to the minimap.",
			_ => string.Empty
		};
		autonomousWorkflowPromptLabel.Text = prompt;
		bool regionReview = stage == FragmentAutonomousWorkflowStage.AwaitingRegionReview;
		autonomousRegionReviewActions.Visible = regionReview;
		if (regionReview)
			autonomousRegionReviewButton.Disabled =
				!fragmentAnalysisRover.State.CandidateRegions.Exists(region =>
					region.Disposition == FragmentAnnotationDisposition.Proposed);
		if (prompt.Length == 0 && IsInstanceValid(autonomousWorkflowPopup))
		{
			autonomousWorkflowPopup.Hide();
			SetAutonomousWorkflowPopupCursor(false);
		}
		bool continueVisible = stage is
			FragmentAutonomousWorkflowStage.AwaitingFeatureReview or
			FragmentAutonomousWorkflowStage.AwaitingRegionChoice or
			FragmentAutonomousWorkflowStage.Complete;
		autonomousWorkflowContinueButton.Visible = continueVisible;
		autonomousWorkflowContinueButton.Text =
			stage == FragmentAutonomousWorkflowStage.AwaitingFeatureReview
				? "CONTINUE"
				: stage == FragmentAutonomousWorkflowStage.Complete
					? "ACKNOWLEDGE"
					: "USE SELECTED REGION";
		autonomousFindAnotherRegionButton.Visible =
			stage == FragmentAutonomousWorkflowStage.AwaitingRegionChoice;
		bool arrowReview = stage == FragmentAutonomousWorkflowStage.AwaitingArrowReview;
		bool playerArrow = stage == FragmentAutonomousWorkflowStage.AwaitingPlayerArrow;
		autonomousArrowActions.Visible = arrowReview || playerArrow;
		autonomousReviewArrowButton.Visible = arrowReview;
		autonomousDrawArrowButton.Visible = playerArrow;
		bool structureReview = stage == FragmentAutonomousWorkflowStage.AwaitingStructureReview;
		autonomousStructureActions.Visible = structureReview;
		FragmentDetectedStructure selectedStructure =
			fragmentAnalysisRover.State.SelectedStructureId is int selectedStructureId
				? fragmentAnalysisRover.State.DetectedStructures.Find(candidate =>
					candidate.Id == selectedStructureId)
				: null;
		bool validStructure = selectedStructure != null &&
			selectedStructure.Disposition != FragmentAnnotationDisposition.Dismissed &&
			selectedStructure.FeatureIds.Count > 0;
		autonomousEditStructureButton.Disabled = !structureReview || !validStructure;
		autonomousValidateStructureButton.Disabled = !structureReview || !validStructure;

		bool showReference = stage == FragmentAutonomousWorkflowStage.AwaitingRegionChoice;
		if (IsInstanceValid(fragmentOverviewTexture))
		{
			Control frame = fragmentOverviewTexture.GetParent<Control>();
			if (IsInstanceValid(frame))
				frame.Visible = showReference && fragmentOverviewTexture.Texture != null;
		}
		if (IsInstanceValid(fragmentOverviewCaption))
			fragmentOverviewCaption.Visible = showReference &&
				fragmentOverviewTexture?.Texture != null;
	}

    /// <summary>Shows the hidden, scene-authored mode selector for first-time openings.</summary>
    internal void ShowInitialModeDialog()
    {
        if (!IsInstanceValid(initialModeOverlay)) return;
        if (!initialModeSignalsConnected)
        {
            initialModeSignalsConnected = true;
            initialManualButton.Pressed += () => CloseInitialModeDialog(FragmentAutonomyMode.Off);
            initialSupportButton.Pressed += () =>
                CloseInitialModeDialog(FragmentAutonomyMode.Supporter);
            initialAutonomousButton.Pressed += () =>
                CloseInitialModeDialog(FragmentAutonomyMode.Performer);
        }
        initialModeOverlay.Show();
        initialManualButton.GrabFocus();
    }

    private void CloseInitialModeDialog(FragmentAutonomyMode mode)
    {
        initialModeOverlay.Hide();
        fragmentAnalysisRover.SetMode(mode);
		if (mode == FragmentAutonomyMode.Performer)
			StartPerformerWorkflowImmediately();
        RefreshAutonomyUi();
    }

    private void OnAutonomyAdvancedPressed()
    {
        capabilityOverridesScroll.Visible = !capabilityOverridesScroll.Visible;
        autonomyAdvancedButton.Text = capabilityOverridesScroll.Visible
            ? "HIDE TASK ALLOCATION"
            : "TASK ALLOCATION";
		if (capabilityOverridesScroll.Visible)
			ScrollRoverSectionIntoView(capabilityOverridesScroll);
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
		if (rotationValueLabel != null) rotationValueLabel.Visible = !compact;
		roverPanelToggleButton.Text = compact
			? (roverPanel.Visible ? "ROVER ◀" : "ROVER ▶")
			: (roverPanel.Visible ? "ROVER MENU<=" : "ROVER MENU=>");
		if (comparisonOpenButton != null)
			comparisonOpenButton.Text = "COMPARE";
	}

	private void OnRoverStatusChanged(FragmentRoverActionStatus status)
    {
        if (status == null) return;
		string currentAction = FragmentCandidateValidityPolicy.GuardPlayerFacingCopy(
			status.CurrentAction);
		string nextAction = FragmentCandidateValidityPolicy.GuardPlayerFacingCopy(
			status.NextAction);
		string activity = status.Activity.ToString().ToUpperInvariant();
		// Suppress IDLE status; show nothing when the rover has no active task.
		if (activity == "IDLE") activity = string.Empty;
        roverActivityLabel.Text = activity.Length > 0 ? $"STATUS: {activity}" : string.Empty;
        roverCurrentActionLabel.Text = $"CURRENT: {currentAction}";
        roverNextActionLabel.Text = $"NEXT: {nextAction}";
		RefreshHistoryButtons();
	}

	private void OnHistoryBackPressed()
	{
		NavigateHistory(fragmentAnalysisRover.UndoLastAction);
	}

	private void NavigateHistory(Action historyAction)
	{
		bool comparisonWasVisible = regionSequenceView?.Visible == true;
		suppressSectionAutoScroll = true;
		isNavigatingHistory = true;
		try
		{
			historyAction?.Invoke();
			if (!comparisonWasVisible && regionSequenceView.Visible)
				OnSequenceExitRequested();
			else if (comparisonWasVisible && !regionSequenceView.Visible &&
				regionSequenceView.RegionCount >= 2)
				OnComparisonOpenPressed();
		}
		finally
		{
			isNavigatingHistory = false;
			suppressSectionAutoScroll = false;
		}
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

	private void OnCandidateRegionSectionPressed()
	{
		CancelStructureEditingForAction();
		SetCandidateRegionSectionExpanded(!isCandidateRegionSectionExpanded);
	}

	private void OnRegionSequenceSectionPressed() =>
		SetRegionSequenceSectionExpanded(!isRegionSequenceSectionExpanded);

	private void OnFeatureSensingSectionPressed()
	{
		CancelStructureEditingForAction();
		SetFeatureSensingSectionExpanded(!isFeatureSensingSectionExpanded);
	}

	private void OnStructureSectionPressed()
	{
		CancelStructureEditingForAction();
		SetStructureSectionExpanded(!isStructureSectionExpanded);
	}

	private void OnFragmentOverviewSectionPressed() =>
		SetFragmentOverviewSectionExpanded(!isFragmentOverviewSectionExpanded);

	private void OnOrientationSectionPressed()
	{
		CancelStructureEditingForAction();
		SetOrientationSectionExpanded(!isOrientationSectionExpanded);
	}

	private void OnCorrectionSectionPressed() =>
		SetCorrectionSectionExpanded(!isCorrectionSectionExpanded);

	private void OnArrowSectionPressed()
	{
		CancelStructureEditingForAction();
		SetArrowSectionExpanded(!isArrowSectionExpanded);
	}

	private void OnDirectionSectionPressed() =>
		SetDirectionSectionExpanded(!isDirectionSectionExpanded);

	private void SetFragmentOverviewSectionExpanded(bool expanded)
	{
		isFragmentOverviewSectionExpanded = expanded;
		if (fragmentOverviewSectionButton != null)
			fragmentOverviewSectionButton.Text = expanded ? "▼ SCANNED FRAGMENT" : "▶ SCANNED FRAGMENT";
		if (fragmentOverviewContent != null)
			fragmentOverviewContent.Visible = expanded;
		if (expanded) ScrollRoverSectionIntoView(fragmentOverviewSectionButton);
	}

	private void SetFragmentOverviewTexture(Texture2D texture)
	{
		if (!IsInstanceValid(fragmentOverviewTexture)) return;
		fragmentOverviewTexture.Texture = texture;
		if (IsInstanceValid(orientationFragmentTexture))
			orientationFragmentTexture.Texture = texture;
		Control frame = fragmentOverviewTexture.GetParent<Control>();
		if (IsInstanceValid(frame)) frame.Visible = texture != null;
		if (fragmentOverviewCaption != null)
			fragmentOverviewCaption.Text = texture == null
				? "SCANNED FRAGMENT IMAGE UNAVAILABLE"
				: "VISUAL RECORD OF THE SCANNED FRAGMENT";
	}

	private void SetOrientationSectionExpanded(bool expanded)
	{
		isOrientationSectionExpanded = expanded;
		orientationSectionButton.Text = expanded ? "▼ ORIENTATION" : "▶ ORIENTATION";
		if (IsInstanceValid(orientationFragmentReference))
			orientationFragmentReference.Visible = expanded &&
				orientationFragmentTexture?.Texture != null;
		orientationRegionControls.Visible = expanded;
		orientationActions.Visible = expanded;
		quitOrientationViewButton.Visible = expanded;
		selectedOrientationLabel.Visible = expanded;
		orientationSelector.Visible = false;
		orientationEdits.Visible = expanded;
		if (expanded)
		{
			bool canPreview = fragmentAnalysisRover?.State != null &&
				fragmentAnalysisRover.GetEffectiveMode(
					FragmentAutonomyCapability.InterpretUprightOrientation) != FragmentAutonomyMode.Off;
			showOrientationOverlayButton.SetPressedNoSignal(canPreview);
			fragmentRoverOverlay.SetShowOrientations(canPreview);
		}
		else
			HideOrientationPreviewAfterRotation();
		SetCorrectionSectionExpanded(expanded);
		// Manual rotation row follows the orientation section.
		Control rotRow = rotateCounterClockwiseButton?.GetParent<Control>();
		if (rotRow != null) rotRow.Visible = expanded;
		if (expanded && fragmentAnalysisRover?.State?.OrientationHypotheses.Count == 0)
			fragmentAnalysisRover.EstimateOrientationHypotheses(true);
		RefreshOrientationPresentation(expanded);
		if (expanded) ScrollRoverSectionIntoView(orientationSectionButton);
	}

	private void HideOrientationPreviewAfterRotation()
	{
		if (IsInstanceValid(showOrientationOverlayButton))
			showOrientationOverlayButton.SetPressedNoSignal(false);
		if (IsInstanceValid(fragmentRoverOverlay))
		{
			fragmentRoverOverlay.SetShowOrientations(false);
			fragmentRoverOverlay.SetOrientationIsolation(false);
		}
		if (IsInstanceValid(regionSequenceView))
			regionSequenceView.SetOrientationIsolation(
				false,
				null,
				null,
				null,
				null,
				Array.Empty<FragmentDetectedFeature>(),
				autonomySettings.StructureColor);
	}

	private void SetCorrectionSectionExpanded(bool expanded)
	{
		isCorrectionSectionExpanded = expanded;
		if (correctionSectionButton != null)
			correctionSectionButton.Text = expanded ? "▼ ROTATION CORRECTION" : "▶ ROTATION CORRECTION";
		if (correctionActions != null) correctionActions.Visible = expanded;
		if (correctionLabel != null) correctionLabel.Visible = expanded;
		if (correctionEditor != null) correctionEditor.Visible = expanded;
		if (correctionEdits != null) correctionEdits.Visible = expanded;
		RefreshRotationCorrectionControls();
		if (expanded && fragmentAnalysisRover?.State?.RotationCorrection != null)
			RefreshOrientationPresentation(true);
		if (expanded) ScrollRoverSectionIntoView(correctionSectionButton ?? orientationSectionButton);
	}

	private void SetArrowSectionExpanded(bool expanded)
	{
		isArrowSectionExpanded = expanded;
		arrowSectionButton.Text = expanded ? "▼ ARROW & DIRECTION" : "▶ ARROW & DIRECTION";
		arrowActions.Visible = expanded;
		arrowLabel.Visible = expanded;
		arrowSelector.Visible = expanded && arrowSelector.ItemCount > 0;
		arrowNavigation.Visible = expanded;
		arrowManual.Visible = expanded;
		arrowEdits.Visible = expanded;
		// Direction content is now merged into this section.
		directionActions.Visible = expanded;
		directionStatusLabel.Visible = expanded;
		directionInset.Visible = expanded;
		if (!expanded)
		{
			drawArrowButton.SetPressedNoSignal(false);
			fragmentRoverOverlay.SetArrowDrawingArmed(false);
		}
		RefreshArrowControls();
		RefreshDirectionControls();
		if (expanded) ScrollRoverSectionIntoView(arrowSectionButton);
	}

	private void SetDirectionSectionExpanded(bool expanded)
	{
		isDirectionSectionExpanded = expanded;
		// Direction section is merged into Arrow; directionSectionButton is null.
		if (directionSectionButton != null)
			directionSectionButton.Text = expanded ? "▼ WORLD DIRECTION" : "▶ WORLD DIRECTION";
		directionActions.Visible = expanded;
		directionStatusLabel.Visible = expanded;
		directionInset.Visible = expanded;
		RefreshDirectionControls();
		if (expanded) ScrollRoverSectionIntoView(directionSectionButton ?? arrowSectionButton);
	}

	private void SetProcessingHistorySectionExpanded(bool expanded)
	{
		isProcessingHistorySectionExpanded = expanded;
		if (processingHistorySectionButton != null)
			processingHistorySectionButton.Text =
				expanded ? "▼ TESTED CONFIGURATIONS" : "▶ TESTED CONFIGURATIONS";
		if (expanded && isProcessingHistoryDirty)
			RefreshProcessingHistoryControls();
		if (processingHistorySelector != null)
			processingHistorySelector.Visible = expanded && processingHistorySelector.ItemCount > 0;
		if (processingHistoryActions != null)
			processingHistoryActions.Visible = expanded;
		if (expanded) ScrollRoverSectionIntoView(processingHistorySectionButton);
	}

	private void SetCandidateRegionSectionExpanded(bool expanded)
	{
		isCandidateRegionSectionExpanded = expanded;
		candidateRegionSectionButton.Text = expanded
			? "▼ REGIONS OF INTEREST"
			: "▶ REGIONS OF INTEREST";
		candidateRegionActions.Visible = expanded;
		selectedRegionLabel.Visible = expanded;
		regionSelector.Visible = expanded && regionSelector.ItemCount > 0;
		candidateRegionEdits.Visible = expanded;
		regionViewLockButton.Visible = expanded;
		navigationIntentLabel.Visible = expanded;
		navigationActions.Visible = expanded;
		if (expanded) ScrollRoverSectionIntoView(candidateRegionSectionButton);
	}

	private void SetRegionSequenceSectionExpanded(bool expanded)
	{
		isRegionSequenceSectionExpanded = expanded;
		if (regionSequenceSectionButton != null)
			regionSequenceSectionButton.Text = expanded ? "▼ REGION SEQUENCE" : "▶ REGION SEQUENCE";
		if (regionSequenceLabel != null) regionSequenceLabel.Visible = expanded;
		if (regionSequenceActions != null) regionSequenceActions.Visible = expanded;
		if (expanded) ScrollRoverSectionIntoView(regionSequenceSectionButton);
	}

	private void SetFeatureSensingSectionExpanded(bool expanded)
	{
		isFeatureSensingSectionExpanded = expanded;
		featureSensingSectionButton.Text = expanded ? "▼ FEATURE SENSING" : "▶ FEATURE SENSING";
		featureSensingActions.Visible = expanded;
		selectedFeatureLabel.Visible = expanded;
		featureSelector.Visible = expanded && featureSelector.ItemCount > 0;
		featureEdits.Visible = expanded;
		if (expanded) ScrollRoverSectionIntoView(featureSensingSectionButton);
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
		structureDispositionEdits.Visible = expanded;
		if (expanded) ScrollRoverSectionIntoView(structureSectionButton);
	}

	private void ScrollRoverSectionIntoView(Control sectionButton)
	{
		if (suppressSectionAutoScroll ||
			!IsInstanceValid(roverPanelScroll) || !IsInstanceValid(sectionButton)) return;
		CallDeferred(nameof(QueueRoverSectionScroll), GetPathTo(sectionButton));
	}

	private void QueueRoverSectionScroll(NodePath sectionPath)
	{
		// Visibility changes do not settle container sizes until the next layout pass. A second
		// deferred call makes the lowest section scroll against its final expanded height.
		CallDeferred(nameof(ApplyRoverSectionScroll), sectionPath);
	}

	private void ApplyRoverSectionScroll(NodePath sectionPath)
	{
		if (!IsInstanceValid(roverPanelScroll)) return;
		Control section = GetNodeOrNull<Control>(sectionPath);
		if (!IsInstanceValid(section) || !section.IsVisibleInTree()) return;
		Control lastVisibleControl = section;
		if (section.GetParent() is Container content)
		{
			int sectionIndex = section.GetIndex();
			for (int index = sectionIndex + 1; index < content.GetChildCount(); index++)
			{
				Node child = content.GetChild(index);
				if (child is HSeparator) break;
				if (child is Control control && control.IsVisibleInTree())
					lastVisibleControl = control;
			}
		}
		roverPanelScroll.EnsureControlVisible(lastVisibleControl);
		float viewportHeight = MathF.Max(roverPanelScroll.Size.Y, 1f);
		float sectionTop = section.Position.Y;
		float sectionBottom = lastVisibleControl.Position.Y + lastVisibleControl.Size.Y;
		int desired = Mathf.FloorToInt(MathF.Max(
			sectionTop - 4f,
			sectionBottom - viewportHeight + 12f));
		roverPanelScroll.ScrollVertical = Math.Max(0, desired);
	}

	private void OnHistoryForwardPressed()
	{
		NavigateHistory(fragmentAnalysisRover.RedoLastAction);
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
            if (roverPauseButton != null)
            {
                roverPauseButton.Disabled = state.GlobalMode == FragmentAutonomyMode.Off;
                roverPauseButton.Text = state.IsPaused ? "RESUME" : "PAUSE";
            }
			scanFeaturesButton.Disabled = state.GlobalMode != FragmentAutonomyMode.Supporter &&
				(state.IsPaused || fragmentAnalysisRover.GetEffectiveMode(
					FragmentAutonomyCapability.SenseSampleFeatures) == FragmentAutonomyMode.Off);
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
		RefreshAutonomousWorkflowUi();
    }
}
