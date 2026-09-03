using System;
using System.Collections.Generic;
using Godot;

public partial class FragmentAnalysisRover : Node
{
	// This is an absolute per-interval safety ceiling, not a search-space limit. Searches may
	// continue after an explicit resume, but must yield to the player before cumulative analysis
	// and UI work reaches the freeze range observed during long uninterrupted runs.
	private const int HardMaximumContinuousSearchSteps = 40;

	private sealed class AutonomousConfigurationCandidate
	{
		public FragmentAnalysisControlState Controls { get; init; }
		public string Key { get; init; }
	}

    [Export]
    private FragmentAutonomySettings settings = new();

    private IFragmentObservationSource observationSource;
    private IFragmentAnalysisCommandSink commandSink;
	private FragmentRoverActionStatus status = new() { Activity = FragmentRoverActivity.Off };
	private ulong lastFeatureRevision = ulong.MaxValue;
	private readonly List<FragmentActionHistoryEntry> actionHistory = new();
	private int actionHistoryIndex = -1;
	private float navigationPreviewRemaining = -1f;
	private readonly List<int> featureReviewRegionIds = new();
	private readonly List<int> featureReviewPriorityRegionIds = new();
	private readonly List<int> structureReviewPriorityRegionIds = new();
	private bool isAcceptedRegionFeatureReviewActive;
	private float measurementDelayRemaining = -1f;
	private ulong lastMeasurementRevision = ulong.MaxValue;
	private int? lastMeasurementTargetRegionId;
	private Rect2? lastMeasurementTargetBounds;
	private int processingHistorySequence;
	private FragmentAnalysisActionOrigin pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
	private string pendingProcessingAction;
	private bool suppressProcessingHistory;
	private FragmentProcessingAdjustment pendingProcessingAdjustment;
	private float processingPreviewRemaining = -1f;
	private bool awaitingSearchMeasurement;
	private float processingActionWatchdogRemaining = -1f;
	private int continuousSearchSteps;
	private bool isPlanningProcessingAdjustment;
	private bool isApplyingProcessingAdjustment;
	private bool isRefreshingSignalMetrics;
	private readonly Dictionary<string, int> processingTransitionCounts = new();
	private float rotationPreviewRemaining = -1f;
	private float rotationTweenElapsed;
	private float rotationTweenDuration;
	private float rotationStartDegrees;
	private float rotationTargetDegrees;
	private float rotationDeltaDegrees;
	private bool isRotationTweenActive;
	private bool rotationPlayerRequested;
	private bool preserveOrientationAcrossKnownRotation;
	private int rotationSourceRegionId = -1;
	private Rect2 rotationSourceRegionBounds;
	private Vector2 rotationSourcePivotNormalized = new(0.5f, 0.5f);
	private FragmentAutonomousWorkflowStage autonomousWorkflowStage =
		FragmentAutonomousWorkflowStage.Inactive;
	private readonly List<AutonomousConfigurationCandidate> autonomousConfigurations = new();
	private readonly List<int> autonomousRegionIds = new();
	private readonly Dictionary<int, FragmentAnalysisControlState> autonomousRegionBestConfigurations = new();
	private readonly List<Rect2> autonomousExcludedRegionBounds = new();
	private float autonomousStepRemaining = -1f;
	private int autonomousConfigurationIndex;
	private int autonomousTestsCompleted;
	private int autonomousRegionIndex;
	private int autonomousTargetRegionId = -1;
	private float autonomousBestScore = float.NegativeInfinity;
	private int autonomousBestDenseRegionCount;
	private int autonomousReachableRegionConfigurationCount;
	private int autonomousBestFeatureCount;
	private FragmentAnalysisControlState autonomousBestConfiguration;
	private FragmentAnalysisControlState autonomousAppliedConfiguration;

	public FragmentAutonomySettings Settings => settings;
	public FragmentAutonomyState State { get; private set; }
	public bool IsRotationPreviewActive => rotationPreviewRemaining >= 0f;
	public bool IsRotationExecuting => isRotationTweenActive;
	public bool IsRotationInProgress => IsRotationPreviewActive || IsRotationExecuting;
	public float RotationExecutionProgress => isRotationTweenActive && rotationTweenDuration > 0f
		? Mathf.Clamp(rotationTweenElapsed / rotationTweenDuration, 0f, 1f)
		: IsRotationPreviewActive ? 0f : 1f;
	public float RotationExecutionTargetDegrees => rotationTargetDegrees;
    public FragmentRoverActionStatus Status => status;
	public bool CanUndo => actionHistoryIndex > 0;
	public bool CanRedo => actionHistoryIndex >= 0 && actionHistoryIndex < actionHistory.Count - 1;
	public int? NavigationTargetRegionId { get; private set; }
	public Rect2? NavigationTargetBounds { get; private set; }
	public bool IsNavigationInProgress { get; private set; }
	public FragmentSignalMeasurementReport MeasurementReport { get; private set; }
	public int? ActiveProcessingHistorySequence { get; private set; }
	public IReadOnlyList<FragmentProcessingHistoryEntry> ProcessingHistory =>
		State?.PreviousConfigurations ?? (IReadOnlyList<FragmentProcessingHistoryEntry>)Array.Empty<FragmentProcessingHistoryEntry>();
	public FragmentProcessingAdjustment PendingProcessingAdjustment => pendingProcessingAdjustment;
	public bool IsProcessingSearchRunning => State?.IsProcessingSearchActive == true;
	public int ContinuousProcessingSearchSteps => continuousSearchSteps;
	public int ContinuousProcessingSearchStepLimit =>
		Math.Clamp(
			settings?.MaximumContinuousSearchSteps ?? HardMaximumContinuousSearchSteps,
			1,
			HardMaximumContinuousSearchSteps);
	public FragmentAutonomousWorkflowStage AutonomousWorkflowStage => autonomousWorkflowStage;
	public bool IsAutonomousWorkflowActive =>
		autonomousWorkflowStage != FragmentAutonomousWorkflowStage.Inactive &&
		autonomousWorkflowStage != FragmentAutonomousWorkflowStage.Complete;
	public bool IsAutonomousWorkflowWaitingForPlayer => autonomousWorkflowStage is
		FragmentAutonomousWorkflowStage.AwaitingRegionReview or
		FragmentAutonomousWorkflowStage.AwaitingFeatureReview or
		FragmentAutonomousWorkflowStage.AwaitingRegionChoice or
		FragmentAutonomousWorkflowStage.AwaitingStructureReview or
		FragmentAutonomousWorkflowStage.AwaitingOrientationReview or
		FragmentAutonomousWorkflowStage.AwaitingArrowReview or
		FragmentAutonomousWorkflowStage.AwaitingPlayerArrow or
		FragmentAutonomousWorkflowStage.Complete;
	public bool IsAutonomousRegionFeatureScopeActive => autonomousWorkflowStage is
		FragmentAutonomousWorkflowStage.SearchingRegionFeatures or
		FragmentAutonomousWorkflowStage.AwaitingFeatureReview;
	public bool CanSearchBack => GetProcessingHistoryIndex() > 0;
	public bool CanSearchForward
	{
		get
		{
			int index = GetProcessingHistoryIndex();
			return index >= 0 && index < ProcessingHistory.Count - 1;
		}
	}

    public event Action<FragmentRoverActionStatus> StatusChanged;
    public event Action AllocationChanged;
	public event Action HistoryChanged;
	public event Action FeaturesChanged;
	public event Action<int> FeatureFocusRequested;
	public event Action RegionsChanged;
	public event Action StructuresChanged;
	public event Action ArrowCandidatesChanged;
	public event Action DirectionInterpretationChanged;
	public event Action OrientationsChanged;
	public event Action RotationCorrectionChanged;
	public event Action<float> RotationCorrectionApplied;
	public event Action RotationExecutionChanged;
	public event Action<int> RegionFocusRequested;
	public event Action<Rect2, int, bool> NavigationTargetChanged;
	public event Action NavigationTargetCleared;
	public event Action<Rect2> NavigationExecutionRequested;
	public event Action NavigationCancellationRequested;
	public event Action<int> RegionReviewCompleted;
	public event Action<FragmentSignalMeasurementReport> MetricsChanged;
	public event Action ProcessingHistoryChanged;
	public event Action ProcessingSearchChanged;
	public event Action<FragmentAutonomousWorkflowStage> AutonomousWorkflowChanged;

    public void Configure(FragmentAutonomySettings configuredSettings)
    {
        settings = configuredSettings ?? new FragmentAutonomySettings();
    }

    public void Initialize(
        IFragmentObservationSource observationSource,
        IFragmentAnalysisCommandSink commandSink,
        FragmentAutonomyState restoredState = null)
    {
        Shutdown();
        settings ??= new FragmentAutonomySettings();
        this.observationSource = observationSource;
        this.commandSink = commandSink;
		ResetAutonomousWorkflowTransient();
        State = restoredState?.Clone() ?? FragmentAutonomyState.CreateDefault(settings);
		State.DetectedFeatures.RemoveAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Player);
		if (State.SelectedFeatureId.HasValue &&
			!State.DetectedFeatures.Exists(feature => feature.Id == State.SelectedFeatureId.Value))
		{
			State.SelectedFeatureId = null;
		}
		if (State.SelectedStructureId.HasValue &&
			!State.DetectedStructures.Exists(
				structure => structure.Id == State.SelectedStructureId.Value))
			State.SelectedStructureId = null;
		foreach (FragmentArrowCandidate arrow in State.ArrowCandidates)
			if (arrow.RegionId < 0) arrow.RegionId = ResolveArrowRegionId(arrow);
        EnsureReliabilityDefaults();
		foreach (FragmentRegionRotationState rotation in State.RegionRotations)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.RegionRotation(
				rotation.RegionId,
				rotation.RegionBounds,
				rotation.PivotNormalized,
				rotation.Degrees,
				FragmentAnalysisActionOrigin.Restore));
		processingHistorySequence = 0;
		foreach (FragmentProcessingHistoryEntry entry in State.PreviousConfigurations)
			processingHistorySequence = Math.Max(processingHistorySequence, entry.Sequence);
        commandSink.AnalysisChanged += OnAnalysisChanged;
        RefreshIdleStatus();
		// A restored session already owns stable reviewed F#/S#/H#/A# identities. Support mode is
		// deliberately human-initiated: its first Feature scan happens only from SCAN FEATURES.
		if (State.GlobalMode != FragmentAutonomyMode.Supporter &&
			(restoredState == null || State.DetectedFeatures.Count == 0))
			RefreshDetectedFeatures();
		RefreshSignalMetrics(true);
		ResetActionHistory();
		ValidateDirectionInterpretation();
		if (State.IsProcessingSearchActive && !State.IsPaused)
			PlanNextProcessingAdjustment();
    }

    public void Shutdown()
    {
        if (commandSink != null)
            commandSink.AnalysisChanged -= OnAnalysisChanged;

        observationSource = null;
        commandSink = null;
		lastFeatureRevision = ulong.MaxValue;
		actionHistory.Clear();
		actionHistoryIndex = -1;
		featureReviewRegionIds.Clear();
		featureReviewPriorityRegionIds.Clear();
		structureReviewPriorityRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;
		measurementDelayRemaining = -1f;
		lastMeasurementRevision = ulong.MaxValue;
		lastMeasurementTargetRegionId = null;
		lastMeasurementTargetBounds = null;
		MeasurementReport = null;
		ActiveProcessingHistorySequence = null;
		processingHistorySequence = 0;
		pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
		pendingProcessingAction = null;
		suppressProcessingHistory = false;
		pendingProcessingAdjustment = null;
		processingPreviewRemaining = -1f;
		awaitingSearchMeasurement = false;
		processingActionWatchdogRemaining = -1f;
		continuousSearchSteps = 0;
		isPlanningProcessingAdjustment = false;
		isApplyingProcessingAdjustment = false;
		isRefreshingSignalMetrics = false;
		processingTransitionCounts.Clear();
		preserveOrientationAcrossKnownRotation = false;
		rotationSourceRegionId = -1;
		rotationSourceRegionBounds = default;
		rotationSourcePivotNormalized = new Vector2(0.5f, 0.5f);
		ResetAutonomousWorkflowTransient();
		ResetRotationExecutionState();
		ClearNavigationTarget(false);
    }

	public void ResetForPuzzle()
	{
		CancelRotationExecution("New puzzle loaded", false, false);
		ClearNavigationTarget(true);
		CancelPendingProcessingAdjustment();
		featureReviewRegionIds.Clear();
		featureReviewPriorityRegionIds.Clear();
		structureReviewPriorityRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;
		ResetAutonomousWorkflowTransient();
        FragmentAutonomyState previous = State ?? FragmentAutonomyState.CreateDefault(settings);
        FragmentAutonomyState reset = FragmentAutonomyState.CreateDefault(settings);
        reset.GlobalMode = previous.GlobalMode;

        foreach ((FragmentAutonomyCapability capability, FragmentAutonomyMode mode) in previous.CapabilityOverrides)
            reset.CapabilityOverrides[capability] = mode;
        foreach ((FragmentAutonomyCapability capability, float reliability) in previous.YellowReliability)
            reset.YellowReliability[capability] = reliability;

        State = reset;
		lastFeatureRevision = ulong.MaxValue;
		processingHistorySequence = 0;
		RefreshDetectedFeatures();
		RefreshSignalMetrics(true);
		ResetActionHistory();
		InvalidateDirectionInterpretation();
		DirectionInterpretationChanged?.Invoke();
        RefreshIdleStatus("New puzzle loaded; Rover analysis state cleared");
        AllocationChanged?.Invoke();
    }

    public FragmentAutonomyState CaptureState()
    {
        return (State ?? FragmentAutonomyState.CreateDefault(settings)).Clone();
    }

    public FragmentAutonomyMode GetEffectiveMode(FragmentAutonomyCapability capability)
    {
        FragmentAutonomyMode requestedMode = State == null
            ? settings?.DefaultMode ?? FragmentAutonomyMode.Off
            : State.CapabilityOverrides.TryGetValue(capability, out FragmentAutonomyMode mode)
            ? mode
            : State.GlobalMode;

        if (requestedMode == FragmentAutonomyMode.Performer &&
            FragmentAutonomyCapabilityCatalog.GetPerformerRating(capability) == FragmentCapabilityRating.Red)
        {
            requestedMode = FragmentAutonomyMode.Supporter;
        }

        if (requestedMode == FragmentAutonomyMode.Supporter &&
            FragmentAutonomyCapabilityCatalog.GetSupportRating(capability) == FragmentCapabilityRating.Red)
        {
            requestedMode = FragmentAutonomyMode.Off;
        }

        return requestedMode;
    }

    public float GetYellowReliability(FragmentAutonomyCapability capability)
    {
        if (State != null && State.YellowReliability.TryGetValue(capability, out float reliability))
            return Mathf.Clamp(reliability, 0f, 1f);
        return Mathf.Clamp(settings?.DefaultYellowReliability ?? 0.5f, 0f, 1f);
    }

    public void SetMode(FragmentAutonomyMode mode)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
        if (State.GlobalMode == mode) return;

		CancelRotationExecution("Autonomy allocation changed", true, false);
		ClearNavigationTarget(true);
		CancelPendingProcessingAdjustment();
		if (mode != FragmentAutonomyMode.Performer)
			StopAutonomousWorkflow(recordHistory: false);
        State.GlobalMode = mode;
		if (mode == FragmentAutonomyMode.Off)
		{
            State.IsPaused = false;
			State.IsProcessingSearchActive = false;
		}
        RefreshIdleStatus();
        AllocationChanged?.Invoke();
		RefreshDetectedFeatures(true);
		RefreshSignalMetrics(true);
		RecordAction($"MODE: {mode.ToString().ToUpperInvariant()}");
		TryAutoMapDirection();
		if (State.IsProcessingSearchActive && !State.IsPaused)
			PlanNextProcessingAdjustment();
		ProcessingSearchChanged?.Invoke();
    }

    public void SetCapabilityOverride(
        FragmentAutonomyCapability capability,
        FragmentAutonomyMode? mode)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
		if (capability == FragmentAutonomyCapability.NavigateSample)
			ClearNavigationTarget(true);
		if (capability == FragmentAutonomyCapability.Rotate)
			CancelRotationExecution("Rotation allocation changed", true, false);
		if (capability == FragmentAutonomyCapability.DecideProcessingConfiguration ||
			capability == FragmentAutonomyCapability.AdjustProcessingParameters)
			CancelPendingProcessingAdjustment();
		if (mode.HasValue)
            State.CapabilityOverrides[capability] = mode.Value;
        else
            State.CapabilityOverrides.Remove(capability);

        AllocationChanged?.Invoke();
		if (capability == FragmentAutonomyCapability.SenseSampleFeatures)
			RefreshDetectedFeatures(true);
		if (capability == FragmentAutonomyCapability.SenseProcessingChanges)
			RefreshSignalMetrics(true);
		RecordAction($"ALLOCATION: {FragmentAutonomyCapabilityCatalog.GetDisplayName(capability)}");
		if (State.IsProcessingSearchActive && !State.IsPaused &&
			(capability == FragmentAutonomyCapability.DecideProcessingConfiguration ||
			 capability == FragmentAutonomyCapability.AdjustProcessingParameters))
			PlanNextProcessingAdjustment();
		if (capability == FragmentAutonomyCapability.InterpretMonolithDirection)
			TryAutoMapDirection();
    }

    public void SetYellowReliability(FragmentAutonomyCapability capability, float reliability)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
        State.YellowReliability[capability] = Mathf.Clamp(reliability, 0f, 1f);
		if (capability == FragmentAutonomyCapability.InterpretUprightOrientation)
			InvalidateOrientationHypotheses();
		RecordAction($"RELIABILITY: {FragmentAutonomyCapabilityCatalog.GetDisplayName(capability)}");
        AllocationChanged?.Invoke();
    }

    public void SetPaused(bool paused)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
		State.IsPaused = State.GlobalMode != FragmentAutonomyMode.Off && paused;
		if (State.IsPaused)
		{
			CancelRotationExecution("Paused by player", true, true);
			ClearNavigationTarget(true);
			CancelPendingProcessingAdjustment();
		}
        RefreshIdleStatus();
		if (!State.IsPaused)
		{
			continuousSearchSteps = 0;
			RefreshDetectedFeatures(true);
			if (State.IsProcessingSearchActive) PlanNextProcessingAdjustment();
			if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.WaitingForRotation &&
				!IsRotationInProgress && State.RotationCorrection != null)
				ApplyApprovedRotationCorrection(State.RotationCorrection);
		}
		RecordAction(State.IsPaused ? "PAUSE" : "RESUME");
		ProcessingSearchChanged?.Invoke();
    }

    public override void _ExitTree()
    {
        Shutdown();
    }

	public override void _Process(double delta)
	{
		ProcessRotationExecution((float)delta);
		if (navigationPreviewRemaining >= 0f && State?.IsPaused != true &&
			GetEffectiveMode(FragmentAutonomyCapability.NavigateSample) == FragmentAutonomyMode.Performer)
		{
			navigationPreviewRemaining -= (float)delta;
			if (navigationPreviewRemaining <= 0f) BeginNavigation();
		}
		if (measurementDelayRemaining >= 0f)
		{
			measurementDelayRemaining -= (float)delta;
			if (measurementDelayRemaining <= 0f)
			{
				measurementDelayRemaining = -1f;
				RefreshSignalMetrics();
			}
		}
		if (processingPreviewRemaining >= 0f && State?.IsPaused != true &&
			pendingProcessingAdjustment != null &&
			GetEffectiveMode(FragmentAutonomyCapability.DecideProcessingConfiguration) ==
				FragmentAutonomyMode.Performer &&
			GetEffectiveMode(FragmentAutonomyCapability.AdjustProcessingParameters) ==
				FragmentAutonomyMode.Performer)
		{
			processingPreviewRemaining -= (float)delta;
			if (processingPreviewRemaining <= 0f) ApplyPendingProcessingAdjustment();
		}
		if (awaitingSearchMeasurement && processingActionWatchdogRemaining >= 0f)
		{
			processingActionWatchdogRemaining -= (float)delta;
			if (processingActionWatchdogRemaining <= 0f)
				PauseProcessingSearchForSafety("Measurement timeout");
		}
		ProcessAutonomousWorkflow((float)delta);
	}

	public void ProposeNavigationTarget(int regionId)
	{
		FragmentCandidateRegion region = State?.CandidateRegions.Find(candidate =>
			candidate.Id == regionId && candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
		FragmentAutonomyMode mode = GetEffectiveMode(FragmentAutonomyCapability.NavigateSample);
		if (region == null || State.IsPaused || mode == FragmentAutonomyMode.Off) return;

		if (NavigationTargetRegionId.HasValue) NavigationCancellationRequested?.Invoke();
		NavigationTargetRegionId = regionId;
		NavigationTargetBounds = region.NormalizedBounds;
		IsNavigationInProgress = false;
		navigationPreviewRemaining = mode == FragmentAutonomyMode.Performer
			? MathF.Max(settings.ActionPreviewSeconds, 0f)
			: -1f;
		status = new FragmentRoverActionStatus
		{
			Activity = mode == FragmentAutonomyMode.Supporter
				? FragmentRoverActivity.AwaitingApproval
				: FragmentRoverActivity.Planning,
			CurrentAction = $"Navigation target preview: Region {regionId}",
			NextAction = mode == FragmentAutonomyMode.Supporter
				? "Select GO or choose another target"
				: $"Navigate after {navigationPreviewRemaining:0.0}s preview",
			CurrentTarget = $"Region {regionId}",
			MeasuredResult = "Destination bounded to the analysis canvas",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		NavigationTargetChanged?.Invoke(region.NormalizedBounds, regionId, false);
		StatusChanged?.Invoke(status);
		if (mode == FragmentAutonomyMode.Performer && navigationPreviewRemaining <= 0f)
			BeginNavigation();
	}

	public void ApproveNavigation()
	{
		if (!NavigationTargetBounds.HasValue || State?.IsPaused == true) return;
		BeginNavigation();
	}

	public void NotifyNavigationCompleted()
	{
		if (!IsNavigationInProgress || !NavigationTargetRegionId.HasValue) return;
		int regionId = NavigationTargetRegionId.Value;
		IsNavigationInProgress = false;
		navigationPreviewRemaining = -1f;
		NavigationTargetRegionId = null;
		NavigationTargetBounds = null;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"Navigation completed: Region {regionId}",
			NextAction = "Inspect the selected region",
			CurrentTarget = $"Region {regionId}",
			MeasuredResult = "Target reached within view bounds",
			LockedParameters = "None"
		};
		NavigationTargetCleared?.Invoke();
		StatusChanged?.Invoke(status);
	}

	public void OverrideNavigationByPlayer()
	{
		if (!NavigationTargetRegionId.HasValue && !IsNavigationInProgress) return;
		ClearNavigationTarget(true);
		if (State.GlobalMode != FragmentAutonomyMode.Off) State.IsPaused = true;
		status = new FragmentRoverActionStatus
		{
			Activity = FragmentRoverActivity.Overridden,
			CurrentAction = "OVERRIDDEN BY PLAYER",
			NextAction = "Resume autonomy explicitly",
			CurrentTarget = "Player-controlled view",
			MeasuredResult = "Autonomous navigation cancelled in place",
			LockedParameters = "None"
		};
		AllocationChanged?.Invoke();
		StatusChanged?.Invoke(status);
	}

	public void CancelNavigationByPlayer() => OverrideNavigationByPlayer();

	private void BeginNavigation()
	{
		if (!NavigationTargetBounds.HasValue || !NavigationTargetRegionId.HasValue) return;
		navigationPreviewRemaining = -1f;
		IsNavigationInProgress = true;
		status = new FragmentRoverActionStatus
		{
			Activity = FragmentRoverActivity.Executing,
			CurrentAction = $"Navigating to Region {NavigationTargetRegionId.Value}",
			NextAction = "Inspect the selected region",
			CurrentTarget = $"Region {NavigationTargetRegionId.Value}",
			MeasuredResult = "Pan and zoom easing within shared bounds",
			LockedParameters = "None"
		};
		NavigationTargetChanged?.Invoke(
			NavigationTargetBounds.Value, NavigationTargetRegionId.Value, true);
		StatusChanged?.Invoke(status);
		NavigationExecutionRequested?.Invoke(NavigationTargetBounds.Value);
	}

	private void ClearNavigationTarget(bool requestCancellation)
	{
		bool hadTarget = NavigationTargetRegionId.HasValue || IsNavigationInProgress;
		navigationPreviewRemaining = -1f;
		NavigationTargetRegionId = null;
		NavigationTargetBounds = null;
		IsNavigationInProgress = false;
		if (!hadTarget) return;
		if (requestCancellation) NavigationCancellationRequested?.Invoke();
		NavigationTargetCleared?.Invoke();
	}

    private void EnsureReliabilityDefaults()
    {
        foreach (FragmentAutonomyCapability capability in FragmentAutonomyCapabilityCatalog.All)
        {
            if (!State.YellowReliability.ContainsKey(capability))
                State.YellowReliability[capability] = settings.DefaultYellowReliability;
        }
    }

    private void OnAnalysisChanged(FragmentAnalysisChange change)
    {
		if (change == null) return;
		if (change.Parameter == FragmentAnalysisParameter.View) return;
		if (change.Origin == FragmentAnalysisActionOrigin.Restore) return;
		bool wasRotationActive = IsRotationInProgress;
		float annotationRotationDelta = 0f;
		if (change.Parameter == FragmentAnalysisParameter.Rotation)
		{
			float previousRotation = change.Previous?.RotationDegrees ??
				change.Current?.RotationDegrees ?? 0f;
			float currentRotation = change.Current?.RotationDegrees ?? previousRotation;
			annotationRotationDelta = Mathf.Wrap(
				wasRotationActive && change.Origin != FragmentAnalysisActionOrigin.Rover
					? currentRotation - rotationStartDegrees
					: currentRotation - previousRotation,
				-180f,
				180f);
		}
		if (change.Parameter == FragmentAnalysisParameter.Rotation &&
			change.Origin == FragmentAnalysisActionOrigin.Rover &&
			IsRotationInProgress)
		{
			// Tween frames are already bounded and supervised here. Re-running feature detection and
			// signal measurement on every frame would both hitch and invalidate the active proposal.
			return;
		}
		if (change.Parameter == FragmentAnalysisParameter.Rotation &&
			change.Origin != FragmentAnalysisActionOrigin.Rover &&
			IsRotationInProgress)
			CancelRotationExecution("Player rotation override", true, false);
		if (change.Parameter == FragmentAnalysisParameter.Rotation &&
			State?.RotationCorrection != null)
		{
			State.RotationCorrection = null;
			RotationCorrectionChanged?.Invoke();
		}
		if (change.Parameter == FragmentAnalysisParameter.Rotation)
			InvalidateDirectionInterpretation();

		if (change.Origin == FragmentAnalysisActionOrigin.Player)
		{
			bool wasActive = wasRotationActive ||
				status.Activity == FragmentRoverActivity.Planning ||
				status.Activity == FragmentRoverActivity.AwaitingApproval ||
				status.Activity == FragmentRoverActivity.Executing ||
				State.IsProcessingSearchActive;
			if (wasActive)
			{
				State.IsPaused = true;
				ClearNavigationTarget(true);
				CancelPendingProcessingAdjustment();
			}

			status = new FragmentRoverActionStatus
			{
				Activity = wasActive ? FragmentRoverActivity.Overridden : GetIdleActivity(),
				CurrentAction = $"PLAYER: {GetParameterDisplayName(change.Parameter)}",
				NextAction = wasActive ? "Paused after player override" : "Continue manual analysis",
				CurrentTarget = GetProcessingTargetName(),
				MeasuredResult = "Waiting for updated S/N",
				LockedParameters = GetLockedProcessingParameterNames()
			};
			StatusChanged?.Invoke(status);
			if (wasActive) AllocationChanged?.Invoke();
			ProcessingSearchChanged?.Invoke();
		}
		else if (change.Origin == FragmentAnalysisActionOrigin.Rover)
		{
			status = new FragmentRoverActionStatus
			{
				Activity = FragmentRoverActivity.Executing,
				CurrentAction = $"TESTING: {GetParameterDisplayName(change.Parameter)}",
				NextAction = "Measure S/N, then plan one next step",
				CurrentTarget = GetProcessingTargetName(),
				MeasuredResult = "Measurement pending",
				LockedParameters = GetLockedProcessingParameterNames()
			};
			StatusChanged?.Invoke(status);
		}
		if (MathF.Abs(annotationRotationDelta) > 0.0001f)
		{
			FragmentObservableScan changedScan = observationSource?.CaptureObservableScan();
			TransformLiveAnnotationsForRotation(
				annotationRotationDelta,
				changedScan?.SampleSize ?? Vector2.One,
				changedScan?.RotationPivotNormalized ?? new Vector2(0.5f, 0.5f));
			FeaturesChanged?.Invoke();
			RegionsChanged?.Invoke();
			StructuresChanged?.Invoke();
			ComputeDirectionInterpretation(false, playerRequested: true);
			preserveOrientationAcrossKnownRotation = true;
		}
		bool autonomousConfigurationTest =
			change.Parameter == FragmentAnalysisParameter.Configuration &&
			autonomousWorkflowStage is
				FragmentAutonomousWorkflowStage.SearchingRegions or
				FragmentAutonomousWorkflowStage.SearchingRegionFeatures;
		RefreshDetectedFeatures(
			force: change.Parameter == FragmentAnalysisParameter.Rotation ||
				change.Parameter == FragmentAnalysisParameter.Configuration,
			retainUnmatchedReviewed: change.Parameter != FragmentAnalysisParameter.Rotation,
			requestSelectedFeatureFocus: change.Parameter != FragmentAnalysisParameter.Rotation && !autonomousConfigurationTest);
		preserveOrientationAcrossKnownRotation = false;
		if (autonomousConfigurationTest)
		{
			measurementDelayRemaining = -1f;
			pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
			pendingProcessingAction = null;
			return;
		}
		pendingMeasurementOrigin = change.Origin;
		if (change.Origin != FragmentAnalysisActionOrigin.Rover ||
			string.IsNullOrEmpty(pendingProcessingAction))
			pendingProcessingAction = $"PROCESSING: {GetParameterDisplayName(change.Parameter)}";
		ScheduleSignalMeasurement();
    }

	public void RefreshDetectedFeatures(
		bool force = false,
		bool recordHistory = false,
		bool retainUnmatchedReviewed = true,
		bool requestSelectedFeatureFocus = true,
		bool playerRequested = false,
		bool scopePlayerScanToRegions = true)
	{
		FragmentAutonomyMode sensingMode = GetEffectiveMode(
			FragmentAutonomyCapability.SenseSampleFeatures);
		if (State == null || observationSource == null || (State.IsPaused && !force) ||
			(!playerRequested && sensingMode != FragmentAutonomyMode.Performer))
		{
			return;
		}

		FragmentObservableScan scan = observationSource.CaptureObservableScan();
		if (scan == null || (!force && scan.Revision == lastFeatureRevision)) return;
		lastFeatureRevision = scan.Revision;

		IReadOnlyList<FragmentDetectedFeature> rawDetected =
			FragmentFeatureDetector.DetectFeatures(scan);
		List<FragmentCandidateRegion> scanRegions = playerRequested && scopePlayerScanToRegions
			? State.CandidateRegions.FindAll(region =>
				region.Disposition != FragmentAnnotationDisposition.Dismissed)
			: new List<FragmentCandidateRegion>();
		bool isRegionScopedPlayerScan = scanRegions.Count > 0;
		List<FragmentDetectedFeature> detected = new();
		foreach (FragmentDetectedFeature candidate in rawDetected)
		{
			if (isRegionScopedPlayerScan && !scanRegions.Exists(region =>
				DoesFeatureIntersectRegion(candidate, region.NormalizedBounds))) continue;
			detected.Add(candidate);
		}
		List<FragmentDetectedFeature> previousRoverFeatures = State.DetectedFeatures.FindAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Rover && !feature.IsInferred);
		List<FragmentDetectedFeature> playerFeatures = State.DetectedFeatures.FindAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Player);
		List<FragmentDetectedFeature> inferredFeatures = State.DetectedFeatures.FindAll(
			feature => feature.IsInferred);
		int nextId = GetNextFeatureId();

		List<int> matchedPreviousIds = new();
		State.DetectedFeatures.Clear();
		State.DetectedFeatures.AddRange(playerFeatures);
		State.DetectedFeatures.AddRange(inferredFeatures);
		foreach (FragmentDetectedFeature candidate in detected)
		{
			FragmentDetectedFeature previous = FindBestPreviousFeature(
				previousRoverFeatures,
				matchedPreviousIds,
				candidate);
			if (previous != null) matchedPreviousIds.Add(previous.Id);
			State.DetectedFeatures.Add(new FragmentDetectedFeature
			{
				Id = previous?.Id ?? nextId++,
				Start = candidate.Start,
				End = candidate.End,
				Segments = candidate.Segments.ConvertAll(segment => new FragmentFeatureSegment
				{
					Start = segment.Start,
					End = segment.End
				}),
				Confidence = candidate.Confidence,
				Provenance = FragmentAnnotationProvenance.Rover,
				Disposition = previous?.Disposition ?? FragmentAnnotationDisposition.Proposed
			});
		}
		foreach (FragmentDetectedFeature previous in previousRoverFeatures)
		{
			bool protectedByStructure = State.DetectedStructures.Exists(structure =>
				(structure.IsPlayerEdited ||
				 structure.Disposition == FragmentAnnotationDisposition.Accepted) &&
				structure.FeatureIds.Contains(previous.Id));
			if (!retainUnmatchedReviewed || matchedPreviousIds.Contains(previous.Id) ||
				(previous.Disposition == FragmentAnnotationDisposition.Proposed && !protectedByStructure))
			{
				continue;
			}
			State.DetectedFeatures.Add(previous);
		}
		PruneMissingStructureMembers();
		ApplyActiveCropToFeatures();
		InvalidateOrientationIfGeometryChanged();

		if (State.SelectedFeatureId.HasValue &&
			!State.DetectedFeatures.Exists(feature => feature.Id == State.SelectedFeatureId.Value))
		{
			State.SelectedFeatureId = null;
		}
		if (IsAutonomousRegionFeatureScopeActive &&
			State.SelectedFeatureId is int scopedSelectedId)
		{
			FragmentDetectedFeature scopedSelected = State.DetectedFeatures.Find(feature =>
				feature.Id == scopedSelectedId);
			if (scopedSelected == null ||
				!IsFeatureInRegions(scopedSelected, featureReviewPriorityRegionIds))
				State.SelectedFeatureId = null;
		}
		if (!State.SelectedFeatureId.HasValue ||
			State.DetectedFeatures.Find(feature => feature.Id == State.SelectedFeatureId.Value)
				?.Disposition != FragmentAnnotationDisposition.Proposed)
		{
			State.SelectedFeatureId = FindFirstProposedFeatureId();
		}

		int roverCount = State.DetectedFeatures.FindAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Rover).Count;
		if (!IsNavigationInProgress && !awaitingSearchMeasurement)
		{
			status = new FragmentRoverActionStatus
			{
				Activity = State.IsPaused ? FragmentRoverActivity.Paused : FragmentRoverActivity.Idle,
				CurrentAction = $"Detected {roverCount} observable features",
				NextAction = "Review the selected feature, then accept or dismiss it",
				CurrentTarget = isRegionScopedPlayerScan
					? scanRegions.Count == 1
						? $"Region {scanRegions[0].Id}"
						: $"{scanRegions.Count} regions of interest"
					: "Whole virtual scan",
				MeasuredResult = $"{roverCount} candidate features",
				LockedParameters = "None"
			};
		}
		if (recordHistory) RecordAction($"SCAN: {roverCount} feature groups");
		if (!IsNavigationInProgress && !awaitingSearchMeasurement) StatusChanged?.Invoke(status);
		FeaturesChanged?.Invoke();
		if (!IsNavigationInProgress && requestSelectedFeatureFocus) RequestSelectedFeatureFocus();
	}

	public void RefreshCandidateRegions(
		bool force = false,
		bool publish = true,
		bool playerRequested = false)
	{
		if (State == null || State.IsPaused ||
			GetEffectiveMode(FragmentAutonomyCapability.InterpretSignalRegions) == FragmentAutonomyMode.Off)
		{
			return;
		}
		featureReviewRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;
		if (!State.DetectedFeatures.Exists(feature =>
			feature.Provenance == FragmentAnnotationProvenance.Rover &&
			!feature.IsInferred &&
			feature.Disposition != FragmentAnnotationDisposition.Dismissed))
		{
			// GENERATE REGIONS is itself an explicit analysis request. Bootstrap the hidden
			// prerequisite from the whole sample instead of requiring a separate Feature scan.
			RefreshDetectedFeatures(
				force: true,
				recordHistory: false,
				requestSelectedFeatureFocus: false,
				playerRequested: playerRequested,
				scopePlayerScanToRegions: false);
		}

		IReadOnlyList<FragmentCandidateRegion> detected =
			FragmentRegionDetector.GroupCandidateRegions(State.DetectedFeatures);
		List<FragmentCandidateRegion> existingRetainedRegions = State.CandidateRegions.FindAll(
			region => region.Disposition != FragmentAnnotationDisposition.Dismissed);
		List<FragmentCandidateRegion> previousRover = State.CandidateRegions.FindAll(
			region => region.Provenance == FragmentAnnotationProvenance.Rover);
		List<FragmentCandidateRegion> replaceableRover = previousRover.FindAll(
			region => region.Disposition == FragmentAnnotationDisposition.Proposed);
		List<FragmentCandidateRegion> playerRegions = State.CandidateRegions.FindAll(
			region => region.Provenance == FragmentAnnotationProvenance.Player);
		List<int> matchedIds = new();
		int nextId = GetNextRegionId();

		State.CandidateRegions.Clear();
		State.CandidateRegions.AddRange(playerRegions);
		foreach (FragmentCandidateRegion candidate in detected)
		{
			FragmentCandidateRegion previous = FindBestPreviousRegion(
				replaceableRover, matchedIds, candidate);
			if (OverlapsExistingRegionByMoreThanHalf(
				candidate.NormalizedBounds,
				existingRetainedRegions,
				previous?.Id)) continue;
			if (previous != null) matchedIds.Add(previous.Id);
			State.CandidateRegions.Add(new FragmentCandidateRegion
			{
				Id = previous?.Id ?? nextId++,
				NormalizedBounds = candidate.NormalizedBounds,
				Confidence = candidate.Confidence,
				Provenance = FragmentAnnotationProvenance.Rover,
				Disposition = previous?.Disposition ?? FragmentAnnotationDisposition.Proposed,
				FeatureIds = new List<int>(candidate.FeatureIds)
			});
		}
		foreach (FragmentCandidateRegion previous in previousRover)
		{
			if (matchedIds.Contains(previous.Id) ||
				previous.Disposition == FragmentAnnotationDisposition.Proposed) continue;
			State.CandidateRegions.Add(previous);
		}

		if (State.SelectedRegionId.HasValue &&
			!State.CandidateRegions.Exists(region => region.Id == State.SelectedRegionId.Value))
			State.SelectedRegionId = null;
		FragmentCandidateRegion selectedRegion = State.SelectedRegionId is int selectedRegionId
			? State.CandidateRegions.Find(region => region.Id == selectedRegionId)
			: null;
		if (selectedRegion == null ||
			(selectedRegion.Provenance == FragmentAnnotationProvenance.Rover &&
			 selectedRegion.Disposition == FragmentAnnotationDisposition.Proposed))
			State.SelectedRegionId = FindFirstProposedRegionId();

		if (!publish) return;
		int roverCount = State.CandidateRegions.FindAll(
			region => region.Provenance == FragmentAnnotationProvenance.Rover).Count;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"Generated {roverCount} regions of interest",
			NextAction = "Review, accept, dismiss, or draw a region",
			CurrentTarget = "Visible feature clusters",
			MeasuredResult = $"{roverCount} regions of interest",
			LockedParameters = "None"
		};
		if (force) RecordAction($"GROUP: {roverCount} regions");
		StatusChanged?.Invoke(status);
		RegionsChanged?.Invoke();
		RefreshSignalMetrics(true);
		RequestSelectedRegionFocus();
	}

	public void RefreshStructures(
		bool recordHistory = false,
		bool playerRequested = false)
	{
		if (State == null ||
			(!playerRequested && (State.IsPaused ||
			 GetEffectiveMode(FragmentAutonomyCapability.SenseReconstructedStructures) ==
				FragmentAutonomyMode.Off)))
			return;

		InvalidateOrientationHypotheses();
		if (settings?.EnableStructureGapCompletion != false &&
			!State.DetectedFeatures.Exists(feature => feature.IsInferred))
		{
			IReadOnlyList<FragmentDetectedFeature> inferred =
				FragmentStructureDetector.InferCompletionFeatures(
					State.DetectedFeatures,
					settings?.StructureConnectionDistance ?? 0.025f,
					settings?.MaximumStructureCompletionGap ?? 0.12f,
					settings?.MinimumStructureCompletionAlignment ?? 0.35f,
					settings?.MaximumInferredStructureFeatures ?? 4);
			int nextFeatureId = GetNextFeatureId();
			foreach (FragmentDetectedFeature completion in inferred)
				State.DetectedFeatures.Add(new FragmentDetectedFeature
				{
					Id = nextFeatureId++,
					Start = completion.Start,
					End = completion.End,
					Confidence = completion.Confidence,
					Provenance = completion.Provenance,
					Disposition = completion.Disposition,
					IsInferred = true
				});
			if (inferred.Count > 0) FeaturesChanged?.Invoke();
		}
		IReadOnlyList<FragmentDetectedStructure> detected =
			FragmentStructureDetector.DetectStructures(
				State.DetectedFeatures,
				settings?.StructureConnectionDistance ?? 0.025f,
				settings?.MinimumStructureFeatureCount ?? 2,
				settings?.MaximumStructureFeatureCount ?? 256);
		List<FragmentDetectedStructure> replaceable = State.DetectedStructures.FindAll(structure =>
			structure.Provenance == FragmentAnnotationProvenance.Rover &&
			structure.Disposition == FragmentAnnotationDisposition.Proposed &&
			!structure.IsPlayerEdited);
		List<FragmentDetectedStructure> protectedStructures = State.DetectedStructures.FindAll(structure =>
			!replaceable.Contains(structure));
		List<int> matchedIds = new();
		List<FragmentDetectedStructure> refreshed = new(protectedStructures);
		int nextId = GetNextStructureId();

		foreach (FragmentDetectedStructure candidate in detected)
		{
			if (protectedStructures.Exists(existing =>
				MembershipSimilarity(existing.FeatureIds, candidate.FeatureIds) >= 0.8f))
				continue;
			FragmentDetectedStructure previous = null;
			float bestSimilarity = 0.5f;
			foreach (FragmentDetectedStructure existing in replaceable)
			{
				if (matchedIds.Contains(existing.Id)) continue;
				float similarity = MembershipSimilarity(existing.FeatureIds, candidate.FeatureIds);
				if (similarity <= bestSimilarity) continue;
				bestSimilarity = similarity;
				previous = existing;
			}
			if (previous != null) matchedIds.Add(previous.Id);
			refreshed.Add(new FragmentDetectedStructure
			{
				Id = previous?.Id ?? nextId++,
				Confidence = candidate.Confidence,
				Provenance = FragmentAnnotationProvenance.Rover,
				Disposition = FragmentAnnotationDisposition.Proposed,
				FeatureIds = new List<int>(candidate.FeatureIds)
			});
		}

		State.DetectedStructures.Clear();
		State.DetectedStructures.AddRange(refreshed);
		State.DetectedStructures.Sort((first, second) => first.Id.CompareTo(second.Id));
		if (State.SelectedStructureId.HasValue && !State.DetectedStructures.Exists(
			structure => structure.Id == State.SelectedStructureId.Value))
			State.SelectedStructureId = null;
		State.SelectedStructureId ??= State.DetectedStructures.Find(structure =>
			structure.Disposition == FragmentAnnotationDisposition.Proposed)?.Id;

		int proposalCount = State.DetectedStructures.FindAll(structure =>
			structure.Disposition == FragmentAnnotationDisposition.Proposed).Count;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"Detected {proposalCount} reconstructed structures",
			NextAction = "Review, edit, accept, dismiss, or create a structure",
			CurrentTarget = "Observable feature graph",
			MeasuredResult = $"{State.DetectedStructures.Count} stored structures",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		if (recordHistory) RecordAction($"STRUCTURE SCAN: {proposalCount} proposals");
		StatusChanged?.Invoke(status);
		StructuresChanged?.Invoke();
	}

	public void ApplyStructureEdit(FragmentStructureEditAction action, int structureId)
	{
		PruneStructureReviewPriority();
		FragmentDetectedStructure structure = State?.DetectedStructures.Find(
			candidate => candidate.Id == structureId);
		if (structure == null) return;
		// While side-by-side review is active, no structure action may silently target an
		// annotation rendered on another carousel page.
		if (structureReviewPriorityRegionIds.Count > 0 &&
			!IsStructureInRegions(structure, structureReviewPriorityRegionIds)) return;
		if (action == FragmentStructureEditAction.Select)
		{
			if (State.SelectedStructureId != structureId) InvalidateOrientationHypotheses();
			State.SelectedStructureId = structureId;
			PublishStructureEditStatus("Player selected candidate structure", structureId);
			return;
		}
		structure.Disposition = action switch
		{
			FragmentStructureEditAction.Accept => FragmentAnnotationDisposition.Accepted,
			FragmentStructureEditAction.Dismiss => FragmentAnnotationDisposition.Dismissed,
			FragmentStructureEditAction.Restore => FragmentAnnotationDisposition.Proposed,
			_ => structure.Disposition
		};
		if (action == FragmentStructureEditAction.Dismiss)
			InvalidateOrientationHypotheses();
		bool completesAutonomousStructureGate =
			action == FragmentStructureEditAction.Accept &&
			autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingStructureReview;
		State.SelectedStructureId = action == FragmentStructureEditAction.Restore ||
			completesAutonomousStructureGate
			? structureId
			: FindNextProposedStructureId(structureId);
		PublishStructureEditStatus(
			FragmentCandidateValidityPolicy.DescribePlayerStructureAction(action),
			structureId);
		if (completesAutonomousStructureGate)
			ContinueAutonomousWorkflow();
	}

	public bool BeginStructureEditing(int structureId)
	{
		PruneStructureReviewPriority();
		FragmentDetectedStructure structure = State?.DetectedStructures.Find(candidate =>
			candidate.Id == structureId &&
			candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
		if (structure == null ||
			(structureReviewPriorityRegionIds.Count > 0 &&
			 !IsStructureInRegions(structure, structureReviewPriorityRegionIds))) return false;

		State.SelectedStructureId = structureId;
		structure.Disposition = FragmentAnnotationDisposition.Proposed;
		InvalidateOrientationHypotheses();
		PublishStructureEditStatus("Editing draft structure", structureId);
		return true;
	}

	public void RefreshArrowCandidates(
		bool recordHistory = false,
		bool playerRequested = false,
		bool autonomousTargetOnly = false)
	{
		if (State == null || observationSource == null ||
			(!playerRequested && (State.IsPaused ||
			 GetEffectiveMode(FragmentAutonomyCapability.SenseDirectionalArrow) ==
				FragmentAutonomyMode.Off))) return;
		if (autonomousTargetOnly || IsAutonomousArrowStage())
		{
			RefreshAutonomousArrowCandidate(recordHistory);
			return;
		}

		FragmentObservableScan scan = observationSource.CaptureObservableScan();
		IReadOnlyList<FragmentArrowCandidate> detected =
			FragmentArrowDetector.DetectCandidates(
				State.DetectedFeatures,
				State.DetectedStructures,
				scan?.SampleSize ?? Vector2.One);
		List<FragmentArrowCandidate> retained = State.ArrowCandidates.FindAll(candidate =>
			candidate.Provenance == FragmentAnnotationProvenance.Player ||
			candidate.Disposition != FragmentAnnotationDisposition.Proposed);
		int nextId = GetNextArrowId();
		foreach (FragmentArrowCandidate candidate in detected)
		{
			int regionId = ResolveArrowRegionId(candidate);
			// A rejected geometric direction stays rejected across rescans instead of returning
			// immediately under a fresh identifier.
			if (retained.Exists(existing => existing.RegionId == regionId &&
				SameArrowGeometry(existing, candidate))) continue;
			retained.Add(new FragmentArrowCandidate
			{
				Id = nextId++,
				Tail = candidate.Tail,
				Tip = candidate.Tip,
				Confidence = candidate.Confidence,
				Disposition = FragmentAnnotationDisposition.Proposed,
				FeatureIds = new List<int>(candidate.FeatureIds),
				Provenance = FragmentAnnotationProvenance.Rover,
				Evidence = candidate.Evidence,
				RegionId = regionId
			});
		}
		State.ArrowCandidates.Clear();
		State.ArrowCandidates.AddRange(retained);
		State.ArrowCandidates.Sort((first, second) => first.Id.CompareTo(second.Id));
		if (State.SelectedArrowId.HasValue && !State.ArrowCandidates.Exists(candidate =>
			candidate.Id == State.SelectedArrowId.Value &&
			(candidate.RegionId < 0 || candidate.RegionId == State.SelectedRegionId)))
			State.SelectedArrowId = null;
		State.SelectedArrowId ??= State.ArrowCandidates.Find(candidate =>
			candidate.Disposition == FragmentAnnotationDisposition.Proposed &&
			(candidate.RegionId < 0 || candidate.RegionId == State.SelectedRegionId))?.Id;

		int proposals = State.ArrowCandidates.FindAll(candidate =>
			candidate.Disposition == FragmentAnnotationDisposition.Proposed).Count;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"Detected {proposals} geometric arrow candidates",
			NextAction = "Review, accept, reject, or use manual Arrow drawing",
			CurrentTarget = "Visible Arrow geometry",
			MeasuredResult = $"{State.ArrowCandidates.Count} stored arrow candidates",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		if (recordHistory) RecordAction($"ARROW SCAN: {proposals} proposals");
		StatusChanged?.Invoke(status);
		ArrowCandidatesChanged?.Invoke();
	}

	private bool IsAutonomousArrowStage() =>
		State?.GlobalMode == FragmentAutonomyMode.Performer &&
		autonomousTargetRegionId >= 0 &&
		autonomousWorkflowStage is
			FragmentAutonomousWorkflowStage.AwaitingArrowReview or
			FragmentAutonomousWorkflowStage.AwaitingPlayerArrow;

	private void RefreshAutonomousArrowCandidate(bool recordHistory)
	{
		FragmentCandidateRegion targetRegion = State.CandidateRegions.Find(region =>
			region.Id == autonomousTargetRegionId &&
			region.Disposition == FragmentAnnotationDisposition.Accepted);
		FragmentDetectedStructure targetStructure = State.SelectedStructureId is int structureId
			? State.DetectedStructures.Find(structure =>
				structure.Id == structureId &&
				structure.Disposition == FragmentAnnotationDisposition.Accepted &&
				targetRegion != null &&
				StructureTouchesRegion(structure, targetRegion))
			: null;

		bool continuingReview = autonomousWorkflowStage is
			FragmentAutonomousWorkflowStage.AwaitingArrowReview or
			FragmentAutonomousWorkflowStage.AwaitingPlayerArrow;
		List<FragmentArrowCandidate> previous = continuingReview
			? State.ArrowCandidates.FindAll(candidate =>
				candidate.RegionId == autonomousTargetRegionId)
			: new List<FragmentArrowCandidate>();
		int nextId = GetNextArrowId();
		FragmentArrowCandidate soleCandidate = previous.Find(candidate =>
			candidate.IsPlayerDefined &&
			candidate.Disposition != FragmentAnnotationDisposition.Dismissed);

		if (soleCandidate == null && targetRegion != null && targetStructure != null)
		{
			List<FragmentDetectedFeature> scopedFeatures = new();
			foreach (int featureId in targetStructure.FeatureIds)
			{
				FragmentDetectedFeature feature = State.DetectedFeatures.Find(candidate =>
					candidate.Id == featureId &&
					candidate.Disposition != FragmentAnnotationDisposition.Dismissed &&
					IsFeatureInRegion(candidate, targetRegion));
				if (feature != null) scopedFeatures.Add(feature);
			}
			IReadOnlyList<FragmentArrowCandidate> detected =
				FragmentArrowDetector.DetectCandidates(
					scopedFeatures,
					new List<FragmentDetectedStructure> { targetStructure },
					observationSource.CaptureObservableScan()?.SampleSize ?? Vector2.One);
			if (detected.Count > 0)
			{
				FragmentArrowCandidate best = detected[0];
				soleCandidate = previous.Find(candidate => SameArrowGeometry(candidate, best));
				soleCandidate ??= new FragmentArrowCandidate
				{
					Id = nextId,
					Tail = best.Tail,
					Tip = best.Tip,
					Confidence = best.Confidence,
					Disposition = FragmentAnnotationDisposition.Proposed,
					FeatureIds = new List<int>(best.FeatureIds),
					Provenance = FragmentAnnotationProvenance.Rover,
					Evidence = best.Evidence,
					RegionId = autonomousTargetRegionId
				};
			}
		}

		State.ArrowCandidates.Clear();
		if (soleCandidate != null)
		{
			soleCandidate.RegionId = autonomousTargetRegionId;
			State.ArrowCandidates.Add(soleCandidate);
		}
		bool acceptedStillPresent = soleCandidate != null &&
			soleCandidate.Disposition == FragmentAnnotationDisposition.Accepted &&
			State.AcceptedArrowId == soleCandidate.Id;
		if (!acceptedStillPresent)
		{
			State.AcceptedArrowId = null;
			InvalidateDirectionInterpretation();
		}
		State.SelectedArrowId = soleCandidate != null &&
			soleCandidate.Disposition != FragmentAnnotationDisposition.Dismissed
			? soleCandidate.Id
			: null;

		int proposals = soleCandidate?.Disposition == FragmentAnnotationDisposition.Proposed ? 1 : 0;
		string structureTarget = targetStructure == null
			? $"the validated Structure in R{autonomousTargetRegionId}"
			: $"Structure S{targetStructure.Id}";
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = proposals == 1
				? $"Detected one arrow for {structureTarget}"
				: $"No arrow detected for {structureTarget}",
			NextAction = proposals == 1
				? "Review the single Arrow proposal"
				: "Use manual Arrow drawing in the selected Region",
			CurrentTarget = $"Region {autonomousTargetRegionId}",
			MeasuredResult = proposals == 1 ? "One scoped Arrow candidate" : "No scoped Arrow candidate",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		if (recordHistory)
			RecordAction($"ARROW SCAN: {proposals} proposal for R{autonomousTargetRegionId}");
		StatusChanged?.Invoke(status);
		ArrowCandidatesChanged?.Invoke();
	}

	public void ApplyArrowEdit(FragmentArrowEditAction action, int arrowId)
	{
		FragmentArrowCandidate candidate = State?.ArrowCandidates.Find(arrow => arrow.Id == arrowId);
		if (candidate == null) return;
		if (IsAutonomousArrowStage() && candidate.RegionId != autonomousTargetRegionId) return;
		if (action == FragmentArrowEditAction.Select)
		{
			State.SelectedArrowId = arrowId;
			PublishArrowEditStatus("Selected", candidate);
			return;
		}
		if (action == FragmentArrowEditAction.Accept)
		{
			foreach (FragmentArrowCandidate previous in State.ArrowCandidates)
				if (previous.Id != arrowId &&
					previous.RegionId == candidate.RegionId &&
					previous.Disposition == FragmentAnnotationDisposition.Accepted)
					previous.Disposition = FragmentAnnotationDisposition.Proposed;
			candidate.Disposition = FragmentAnnotationDisposition.Accepted;
			State.AcceptedArrowId = arrowId;
			State.SelectedArrowId = arrowId;
			InvalidateDirectionInterpretation();
		}
		else if (action == FragmentArrowEditAction.Reject)
		{
			candidate.Disposition = FragmentAnnotationDisposition.Dismissed;
			if (State.AcceptedArrowId == arrowId) State.AcceptedArrowId = null;
			State.SelectedArrowId = FindNextProposedArrowId(arrowId);
			InvalidateDirectionInterpretation();
		}
		else if (action == FragmentArrowEditAction.Restore)
		{
			candidate.Disposition = FragmentAnnotationDisposition.Proposed;
			State.SelectedArrowId = arrowId;
		}
		PublishArrowEditStatus(action.ToString().ToUpperInvariant(), candidate);
		if (action == FragmentArrowEditAction.Accept)
		{
			ComputeDirectionInterpretation(true, playerRequested: true);
		if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingArrowReview)
			{
				if (State.DirectionInterpretation != null)
					CompleteAutonomousWorkflow();
				else
				{
					SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingPlayerArrow);
					PublishAutonomousStatus(
						"Accepted arrow could not produce a bearing",
						"Use manual Arrow drawing for a replacement",
						$"Region {autonomousTargetRegionId}",
						"Valid manual Arrow geometry required",
						FragmentRoverActivity.WaitingForPlayer);
				}
			}
		}
		else if (action == FragmentArrowEditAction.Reject &&
			autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingArrowReview &&
			!State.SelectedArrowId.HasValue)
		{
			SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingPlayerArrow);
			PublishAutonomousStatus(
				"No accepted arrow candidate remains",
				"Draw one arrow from tail to tip",
				$"Region {autonomousTargetRegionId}",
				"Player geometry required",
				FragmentRoverActivity.WaitingForPlayer);
			}
	}

	public void ComputeDirectionInterpretation(
		bool recordHistory = true,
		bool playerRequested = false)
	{
		if (State == null || observationSource == null || commandSink == null ||
			(State.IsPaused && !playerRequested)) return;
		if (!playerRequested &&
			GetEffectiveMode(FragmentAutonomyCapability.InterpretMonolithDirection) ==
				FragmentAutonomyMode.Off) return;
		FragmentArrowCandidate arrow = State.AcceptedArrowId is int arrowId
			? State.ArrowCandidates.Find(candidate =>
				candidate.Id == arrowId &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted)
			: null;
		if (arrow == null)
		{
			InvalidateDirectionInterpretation();
			return;
		}
		Vector2 sampleSize = observationSource.CaptureObservableScan()?.SampleSize ?? Vector2.One;
		FragmentDirectionInterpretation mapped = FragmentDirectionMapper.Map(arrow, sampleSize);
		if (mapped == null)
		{
			InvalidateDirectionInterpretation();
			return;
		}
		State.DirectionInterpretation = mapped;
		State.AcceptedWorldDirection = mapped.WorldGridDirection;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = "Mapped accepted arrow into the world grid frame",
			NextAction = "Compare the analyzer inset with the persistent minimap ray",
			CurrentTarget = $"Accepted arrow A{mapped.SourceArrowId}",
			MeasuredResult = FragmentDirectionMapper.FormatBearing(mapped),
			LockedParameters = GetLockedProcessingParameterNames()
		};
		if (recordHistory)
			RecordAction($"WORLD BEARING: {FragmentDirectionMapper.FormatBearing(mapped)}");
		StatusChanged?.Invoke(status);
		DirectionInterpretationChanged?.Invoke();
	}

	private void TryAutoMapDirection()
	{
		if (GetEffectiveMode(FragmentAutonomyCapability.InterpretMonolithDirection) ==
			FragmentAutonomyMode.Performer)
			ComputeDirectionInterpretation(true);
	}

	private void ValidateDirectionInterpretation()
	{
		FragmentDirectionInterpretation mapped = State?.DirectionInterpretation;
		if (mapped == null) return;
		bool valid = State.AcceptedArrowId == mapped.SourceArrowId &&
			State.ArrowCandidates.Exists(candidate =>
				candidate.Id == mapped.SourceArrowId &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted);
		if (!valid) InvalidateDirectionInterpretation();
	}

	private void InvalidateDirectionInterpretation()
	{
		if (State == null) return;
		bool changed = State.DirectionInterpretation != null || State.AcceptedWorldDirection.HasValue;
		State.DirectionInterpretation = null;
		State.AcceptedWorldDirection = null;
		if (changed) DirectionInterpretationChanged?.Invoke();
	}

	public int DefinePlayerArrow(Vector2 tail, Vector2 tip)
	{
		if (State == null) return -1;
		tail = new Vector2(Mathf.Clamp(tail.X, 0f, 1f), Mathf.Clamp(tail.Y, 0f, 1f));
		tip = new Vector2(Mathf.Clamp(tip.X, 0f, 1f), Mathf.Clamp(tip.Y, 0f, 1f));
		if (tail.DistanceTo(tip) < 0.015f) return -1;
		int id = GetNextArrowId();
		bool autonomousTarget = IsAutonomousArrowStage();
		if (autonomousTarget)
		{
			// A manual replacement belongs to the already chosen Region and supersedes the Rover's
			// sole proposal; autonomous analysis never accumulates cross-Region arrows.
			State.ArrowCandidates.Clear();
			State.SelectedArrowId = null;
			State.AcceptedArrowId = null;
			InvalidateDirectionInterpretation();
		}
		FragmentArrowCandidate candidate = new()
		{
			Id = id,
			Tail = tail,
			Tip = tip,
			Confidence = 1f,
			Disposition = FragmentAnnotationDisposition.Proposed,
			Provenance = FragmentAnnotationProvenance.Player,
			IsPlayerDefined = true,
			Evidence = "PLAYER-DRAWN ARROW",
			RegionId = autonomousTarget
				? autonomousTargetRegionId
				: ResolveArrowRegionId(tail, tip, null)
		};
		State.ArrowCandidates.Add(candidate);
		State.SelectedArrowId = id;
		InvalidateDirectionInterpretation();
		PublishArrowEditStatus("Drew", candidate);
		if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingPlayerArrow)
		{
			SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingArrowReview);
			PublishAutonomousStatus(
				$"Player arrow A{id} is ready",
				"Accept an Arrow to calculate and publish world bearing",
				$"Region {candidate.RegionId}",
				"Player-drawn Arrow",
				FragmentRoverActivity.WaitingForPlayer);
		}
		return id;
	}

	private void PublishArrowEditStatus(string action, FragmentArrowCandidate candidate)
	{
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"{action} arrow A{candidate.Id}",
			NextAction = "Continue directional-arrow review",
			CurrentTarget = $"Arrow candidate {candidate.Id}",
			MeasuredResult = candidate.IsPlayerDefined
				? "Player-drawn Arrow"
				: "Rover-detected Arrow",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		RecordAction($"{action} A{candidate.Id}");
		StatusChanged?.Invoke(status);
		ArrowCandidatesChanged?.Invoke();
	}

	private int? FindNextProposedArrowId(int afterArrowId)
	{
		if (State == null || State.ArrowCandidates.Count == 0) return null;
		FragmentArrowCandidate current = State.ArrowCandidates.Find(candidate =>
			candidate.Id == afterArrowId);
		int regionId = current?.RegionId ?? State.SelectedRegionId ?? -1;
		int start = State.ArrowCandidates.FindIndex(candidate => candidate.Id == afterArrowId);
		for (int offset = 1; offset <= State.ArrowCandidates.Count; offset++)
		{
			FragmentArrowCandidate candidate = State.ArrowCandidates[
				(start + offset + State.ArrowCandidates.Count) % State.ArrowCandidates.Count];
			if (candidate.Disposition == FragmentAnnotationDisposition.Proposed &&
				(candidate.RegionId < 0 || candidate.RegionId == regionId)) return candidate.Id;
		}
		return null;
	}

	private int GetNextArrowId()
	{
		int highest = 0;
		if (State != null)
			foreach (FragmentArrowCandidate candidate in State.ArrowCandidates)
				highest = Math.Max(highest, candidate.Id);
		return highest + 1;
	}

	private static bool SameArrowGeometry(
		FragmentArrowCandidate first,
		FragmentArrowCandidate second) =>
		first.Tail.DistanceSquaredTo(second.Tail) < 0.0004f &&
		first.Tip.DistanceSquaredTo(second.Tip) < 0.0004f;

	private int ResolveArrowRegionId(FragmentArrowCandidate arrow) =>
		ResolveArrowRegionId(arrow.Tail, arrow.Tip, arrow.FeatureIds);

	private int ResolveArrowRegionId(
		Vector2 tail,
		Vector2 tip,
		IReadOnlyList<int> featureIds)
	{
		if (State == null) return -1;
		Vector2 center = (tail + tip) * 0.5f;
		FragmentCandidateRegion best = null;
		foreach (FragmentCandidateRegion region in State.CandidateRegions)
		{
			if (region.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
			bool sharesFeature = false;
			if (featureIds != null)
				for (int index = 0; index < featureIds.Count && !sharesFeature; index++)
					sharesFeature = region.FeatureIds.Contains(featureIds[index]);
			if (!sharesFeature && !region.NormalizedBounds.HasPoint(center)) continue;
			if (best == null || region.Id == State.SelectedRegionId ||
				region.NormalizedBounds.Size.X * region.NormalizedBounds.Size.Y <
				best.NormalizedBounds.Size.X * best.NormalizedBounds.Size.Y) best = region;
		}
		return best?.Id ?? State.SelectedRegionId ?? -1;
	}

	private int? FindNextProposedStructureId(int afterStructureId)
	{
		if (State == null || State.DetectedStructures.Count == 0) return null;
		int? visible = FindNextMatching(structure =>
			IsStructureInRegions(structure, structureReviewPriorityRegionIds));
		if (structureReviewPriorityRegionIds.Count > 0) return visible;
		return FindNextMatching(_ => true);

		int? FindNextMatching(Func<FragmentDetectedStructure, bool> additionalPredicate)
		{
		int start = State.DetectedStructures.FindIndex(structure =>
			structure.Id == afterStructureId);
		for (int offset = 1; offset <= State.DetectedStructures.Count; offset++)
		{
			FragmentDetectedStructure candidate = State.DetectedStructures[
				(start + offset + State.DetectedStructures.Count) % State.DetectedStructures.Count];
			if (candidate.Disposition == FragmentAnnotationDisposition.Proposed &&
				additionalPredicate(candidate))
				return candidate.Id;
		}
		return null;
		}
	}

	public int AddPlayerStructure(int? initialFeatureId = null)
	{
		if (State == null) return -1;
		InvalidateOrientationHypotheses();
		List<int> membership = new();
		if (initialFeatureId is int featureId && State.DetectedFeatures.Exists(feature =>
			feature.Id == featureId &&
			feature.Disposition != FragmentAnnotationDisposition.Dismissed))
			membership.Add(featureId);
		int id = GetNextStructureId();
		State.DetectedStructures.Add(new FragmentDetectedStructure
		{
			Id = id,
			Confidence = 1f,
			Provenance = FragmentAnnotationProvenance.Player,
			Disposition = FragmentAnnotationDisposition.Proposed,
			IsPlayerEdited = true,
			FeatureIds = membership
		});
		State.SelectedStructureId = id;
		PublishStructureEditStatus("Created structure", id);
		return id;
	}

	public void ToggleSelectedStructureFeature(int featureId)
	{
		FragmentDetectedStructure structure = State?.SelectedStructureId is int structureId
			? State.DetectedStructures.Find(candidate => candidate.Id == structureId)
			: null;
		FragmentDetectedFeature feature = State?.DetectedFeatures.Find(candidate =>
			candidate.Id == featureId &&
			candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
		if (structure == null || feature == null ||
			structure.Disposition == FragmentAnnotationDisposition.Dismissed) return;
		string action;
		if (structure.FeatureIds.Remove(featureId)) action = "Excluded";
		else
		{
			structure.FeatureIds.Add(featureId);
			structure.FeatureIds.Sort();
			action = "Included";
		}
		structure.IsPlayerEdited = true;
		structure.Disposition = FragmentAnnotationDisposition.Proposed;
		InvalidateOrientationHypotheses();
		PublishStructureEditStatus($"{action} F{featureId} from", structure.Id);
	}

	public void RemoveSelectedStructureFeature(int featureId)
	{
		FragmentDetectedStructure structure = State?.SelectedStructureId is int structureId
			? State.DetectedStructures.Find(candidate => candidate.Id == structureId)
			: null;
		if (structure == null ||
			structure.Disposition == FragmentAnnotationDisposition.Dismissed ||
			!structure.FeatureIds.Remove(featureId)) return;
		FragmentDetectedFeature removedFeature = State.DetectedFeatures.Find(candidate =>
			candidate.Id == featureId);
		if (removedFeature != null)
			removedFeature.Disposition = FragmentAnnotationDisposition.Dismissed;
		foreach (FragmentDetectedStructure candidate in State.DetectedStructures)
			if (candidate.Id != structure.Id) candidate.FeatureIds.Remove(featureId);
		structure.IsPlayerEdited = true;
		structure.Disposition = FragmentAnnotationDisposition.Proposed;
		InvalidateOrientationHypotheses();
		if (State.SelectedFeatureId == featureId) State.SelectedFeatureId = null;
		FeaturesChanged?.Invoke();
		PublishStructureEditStatus($"Removed F{featureId} from", structure.Id);
	}

	public int AddPlayerStrokeToSelectedStructure(Vector2 start, Vector2 end)
	{
		FragmentDetectedStructure structure = State?.SelectedStructureId is int structureId
			? State.DetectedStructures.Find(candidate => candidate.Id == structureId)
			: null;
		if (structure == null ||
			structure.Disposition == FragmentAnnotationDisposition.Dismissed ||
			start.DistanceSquaredTo(end) < 0.000025f) return -1;
		start = start.Clamp(Vector2.Zero, Vector2.One);
		end = end.Clamp(Vector2.Zero, Vector2.One);
		int featureId = GetNextFeatureId();
		State.DetectedFeatures.Add(new FragmentDetectedFeature
		{
			Id = featureId,
			Start = start,
			End = end,
			Confidence = 1f,
			Provenance = FragmentAnnotationProvenance.Player,
			Disposition = FragmentAnnotationDisposition.Accepted
		});
		structure.FeatureIds.Add(featureId);
		structure.FeatureIds.Sort();
		structure.IsPlayerEdited = true;
		structure.Disposition = FragmentAnnotationDisposition.Proposed;
		if (State.SelectedRegionId is int regionId)
		{
			FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
				candidate.Id == regionId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
			if (region != null)
			{
				if (!region.FeatureIds.Contains(featureId)) region.FeatureIds.Add(featureId);
				if (!IsRegionViewLocked(region.Id))
				{
					Rect2 strokeBounds = new(start, Vector2.Zero);
					strokeBounds = strokeBounds.Expand(end).Grow(0.012f);
					region.NormalizedBounds = region.NormalizedBounds.Merge(strokeBounds);
				}
			}
		}
		State.SelectedFeatureId = featureId;
		InvalidateOrientationHypotheses();
		FeaturesChanged?.Invoke();
		RegionsChanged?.Invoke();
		PublishStructureEditStatus($"Drew F{featureId} in", structure.Id);
		return featureId;
	}

	public void MergeStructures(int targetStructureId, int sourceStructureId)
	{
		if (State == null || targetStructureId == sourceStructureId) return;
		FragmentDetectedStructure target = State.DetectedStructures.Find(structure =>
			structure.Id == targetStructureId &&
			structure.Disposition != FragmentAnnotationDisposition.Dismissed);
		FragmentDetectedStructure source = State.DetectedStructures.Find(structure =>
			structure.Id == sourceStructureId &&
			structure.Disposition != FragmentAnnotationDisposition.Dismissed);
		if (target == null || source == null) return;
		InvalidateOrientationHypotheses();
		foreach (int featureId in source.FeatureIds)
			if (!target.FeatureIds.Contains(featureId)) target.FeatureIds.Add(featureId);
		target.FeatureIds.Sort();
		target.IsPlayerEdited = true;
		target.Disposition = FragmentAnnotationDisposition.Proposed;
		source.Disposition = FragmentAnnotationDisposition.Dismissed;
		source.IsPlayerEdited = true;
		State.SelectedStructureId = target.Id;
		PublishStructureEditStatus($"Merged S{source.Id} into", target.Id);
	}

	public void EstimateOrientationHypotheses(
		bool recordHistory = true,
		bool playerRequested = false)
	{
		if (State == null || observationSource == null ||
			(!playerRequested && (State.IsPaused ||
			 GetEffectiveMode(FragmentAutonomyCapability.InterpretUprightOrientation) ==
				FragmentAutonomyMode.Off)))
			return;
		FragmentCandidateRegion region = State.SelectedRegionId is int regionId
			? State.CandidateRegions.Find(candidate =>
				candidate.Id == regionId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed)
			: null;
		if (region == null) return;
		FragmentLockedRegionView locked = State.LockedRegionViews.Find(view =>
			view.RegionId == region.Id);
		FragmentObservableScan scan = locked?.Scan ?? observationSource.CaptureObservableScan();
		if (scan == null) return;
		IReadOnlyList<FragmentDetectedFeature> availableFeatures =
			locked?.Features ?? State.DetectedFeatures;
		List<FragmentDetectedFeature> regionFeatures = new();
		foreach (FragmentDetectedFeature feature in availableFeatures)
		{
			if (feature == null || feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				continue;
			if (!region.FeatureIds.Contains(feature.Id) &&
				!region.NormalizedBounds.HasPoint(GetFeatureCenter(feature))) continue;
			regionFeatures.Add(CloneDetectedFeature(feature));
		}
		// A locked view is a stable visual reference, but it must not hide later player edits.
		// Merge the live region geometry over that snapshot so added/custom strokes and updated
		// Features are always available to the orientation reconstruction.
		if (locked != null)
			foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
			{
				if (feature == null ||
					(!region.FeatureIds.Contains(feature.Id) &&
					 !region.NormalizedBounds.HasPoint(GetFeatureCenter(feature)))) continue;
				int existingIndex = regionFeatures.FindIndex(candidate => candidate.Id == feature.Id);
				if (feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				{
					if (existingIndex >= 0) regionFeatures.RemoveAt(existingIndex);
					continue;
				}
				FragmentDetectedFeature current = CloneDetectedFeature(feature);
				if (existingIndex >= 0) regionFeatures[existingIndex] = current;
				else regionFeatures.Add(current);
			}
		if (regionFeatures.Count == 0) return;
		IReadOnlyList<FragmentDetectedStructure> structures =
			FragmentStructureDetector.DetectStructures(
				regionFeatures,
				settings?.StructureConnectionDistance ?? 0.025f,
				1,
				settings?.MaximumStructureFeatureCount ?? 256);
		FragmentDetectedStructure structure = null;
		foreach (FragmentDetectedStructure candidate in structures)
			if (structure == null || candidate.FeatureIds.Count > structure.FeatureIds.Count ||
				(candidate.FeatureIds.Count == structure.FeatureIds.Count &&
				 candidate.Confidence > structure.Confidence))
				structure = candidate;
		structure ??= new FragmentDetectedStructure
		{
			Id = 0,
			Confidence = 0.5f,
			Provenance = FragmentAnnotationProvenance.Rover,
			Disposition = FragmentAnnotationDisposition.Proposed,
			FeatureIds = regionFeatures.ConvertAll(feature => feature.Id)
		};
		FragmentDetectedStructure selectedStructure = State.SelectedStructureId is int selectedStructureId
			? State.DetectedStructures.Find(candidate =>
				candidate.Id == selectedStructureId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed)
			: null;
		if (selectedStructure == null || !selectedStructure.FeatureIds.Exists(featureId =>
			regionFeatures.Exists(feature => feature.Id == featureId)))
		{
			selectedStructure = null;
			int bestPriority = int.MinValue;
			foreach (FragmentDetectedStructure candidate in State.DetectedStructures)
			{
				if (candidate.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
				int visibleCount = candidate.FeatureIds.FindAll(featureId =>
					regionFeatures.Exists(feature => feature.Id == featureId)).Count;
				if (visibleCount == 0) continue;
				int priority = visibleCount +
					(candidate.Disposition == FragmentAnnotationDisposition.Accepted ? 10000 : 0) +
					(candidate.IsPlayerEdited ? 1000 : 0);
				if (priority <= bestPriority) continue;
				bestPriority = priority;
				selectedStructure = candidate;
			}
		}
		List<int> selectedVisibleFeatureIds = selectedStructure == null
			? new List<int>()
			: selectedStructure.FeatureIds.FindAll(featureId =>
				regionFeatures.Exists(feature => feature.Id == featureId));
		if (selectedStructure != null && selectedVisibleFeatureIds.Count > 0)
			structure = new FragmentDetectedStructure
		{
			Id = selectedStructure.Id,
			Confidence = selectedStructure.Confidence,
			Provenance = selectedStructure.Provenance,
			Disposition = selectedStructure.Disposition,
			IsPlayerEdited = selectedStructure.IsPlayerEdited,
			FeatureIds = selectedVisibleFeatureIds
		};
		ulong signature = FragmentOrientationEstimator.ComputeGeometrySignature(
			structure, regionFeatures);
		bool sameGeometry = State.OrientationHypotheses.Count > 0 &&
			State.OrientationSourceView?.RegionId == region.Id &&
			State.OrientationHypotheses[0].SourceStructureId == structure.Id &&
			State.OrientationHypotheses[0].GeometrySignature == signature;
		if (!sameGeometry)
		{
			State.OrientationSourceView = new FragmentLockedRegionView
			{
				RegionId = region.Id,
				NormalizedBounds = region.NormalizedBounds,
				RotationDegrees = locked?.RotationDegrees ??
					commandSink?.CaptureRegionRotationDegrees(region.Id) ?? 0f,
				Scan = CloneObservableScan(scan)
			};
			foreach (FragmentDetectedFeature feature in regionFeatures)
				State.OrientationSourceView.Features.Add(CloneDetectedFeature(feature));
			State.OrientationSourceStructure = new FragmentDetectedStructure
			{
				Id = structure.Id,
				Confidence = structure.Confidence,
				Provenance = structure.Provenance,
				Disposition = structure.Disposition,
				IsPlayerEdited = structure.IsPlayerEdited,
				FeatureIds = new List<int>(structure.FeatureIds)
			};
			State.OrientationHypotheses.Clear();
			State.OrientationHypotheses.AddRange(
				FragmentOrientationEstimator.EstimateHypotheses(
					structure,
					regionFeatures,
					scan.SampleSize,
					GetYellowReliability(
						FragmentAutonomyCapability.InterpretUprightOrientation),
					8));
			State.AcceptedOrientationId = null;
			State.SelectedOrientationId = State.OrientationHypotheses.Count > 0
				? State.OrientationHypotheses[0].Id
				: null;
		}
		else if (!State.SelectedOrientationId.HasValue)
		{
			State.SelectedOrientationId = State.OrientationHypotheses.Find(hypothesis =>
				hypothesis.Disposition != FragmentAnnotationDisposition.Dismissed)?.Id ??
				State.OrientationHypotheses[0].Id;
		}
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = sameGeometry
				? "Reviewed existing Rotation alternatives"
				: $"Estimated {State.OrientationHypotheses.Count} Rotation alternatives",
			NextAction = "Player reviews, accepts, or rejects a possible Rotation",
			CurrentTarget = $"Region {region.Id}" + (locked == null ? " · LIVE" : " · LOCKED"),
			MeasuredResult = locked == null
				? "Rotation uses the region's current visible reconstruction"
				: "Rotation uses the region's retained locked reconstruction",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		if (recordHistory && !sameGeometry)
			RecordAction($"ROTATION: {State.OrientationHypotheses.Count} alternatives for R{region.Id}");
		StatusChanged?.Invoke(status);
		OrientationsChanged?.Invoke();
	}

	public void ApplyOrientationEdit(FragmentOrientationEditAction action, int hypothesisId)
	{
		FragmentOrientationHypothesis hypothesis = State?.OrientationHypotheses.Find(
			candidate => candidate.Id == hypothesisId);
		if (hypothesis == null) return;
		State.SelectedOrientationId = hypothesisId;
		if (action == FragmentOrientationEditAction.Select)
		{
			OrientationsChanged?.Invoke();
			return;
		}
		string result;
		switch (action)
		{
			case FragmentOrientationEditAction.Accept:
				foreach (FragmentOrientationHypothesis other in State.OrientationHypotheses)
					if (other.Id != hypothesisId &&
						other.Disposition == FragmentAnnotationDisposition.Accepted)
						other.Disposition = FragmentAnnotationDisposition.Proposed;
				hypothesis.Disposition = FragmentAnnotationDisposition.Accepted;
				State.AcceptedOrientationId = hypothesisId;
				result = "Player accepted Rotation alternative";
				break;
			case FragmentOrientationEditAction.Reject:
				hypothesis.Disposition = FragmentAnnotationDisposition.Dismissed;
				if (State.AcceptedOrientationId == hypothesisId) State.AcceptedOrientationId = null;
				State.SelectedOrientationId = State.OrientationHypotheses.Find(candidate =>
					candidate.Disposition == FragmentAnnotationDisposition.Proposed)?.Id ?? hypothesisId;
				result = "Player rejected Rotation alternative";
				break;
			case FragmentOrientationEditAction.Restore:
				hypothesis.Disposition = FragmentAnnotationDisposition.Proposed;
				result = "Player restored Rotation alternative";
				break;
			default:
				return;
		}
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = result,
			NextAction = State.AcceptedOrientationId.HasValue
				? "Use the accepted Rotation for supervised correction"
				: "Continue Rotation review",
			CurrentTarget = $"Rotation ROT{hypothesisId}",
			MeasuredResult = $"Axis {hypothesis.AxisDegrees:+0.0;-0.0;0.0}°",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		RecordAction($"{result} ROT{hypothesisId}");
		if (action == FragmentOrientationEditAction.Accept ||
			(action == FragmentOrientationEditAction.Reject &&
			 State.RotationCorrection?.SourceOrientationId == hypothesisId))
		{
			State.RotationCorrection = null;
			RotationCorrectionChanged?.Invoke();
		}
		StatusChanged?.Invoke(status);
		OrientationsChanged?.Invoke();
		if (action == FragmentOrientationEditAction.Accept &&
			autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingOrientationReview)
			SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.WaitingForRotation);
		if (action == FragmentOrientationEditAction.Accept &&
			State.GlobalMode == FragmentAutonomyMode.Performer && !State.IsPaused &&
			GetEffectiveMode(FragmentAutonomyCapability.DecideRotationCorrection) ==
				FragmentAutonomyMode.Performer &&
			GetEffectiveMode(FragmentAutonomyCapability.Rotate) == FragmentAutonomyMode.Performer)
		{
			ProposeRotationCorrection();
			if (State.RotationCorrection != null)
				ApplyApprovedRotationCorrection(State.RotationCorrection);
		}
	}

	public void ProposeRotationCorrection(bool playerRequested = false)
	{
		if (State == null || commandSink == null ||
			(!playerRequested && (State.IsPaused ||
			 GetEffectiveMode(FragmentAutonomyCapability.DecideRotationCorrection) ==
				FragmentAutonomyMode.Off)))
			return;
		FragmentOrientationHypothesis accepted = State.AcceptedOrientationId is int orientationId
			? State.OrientationHypotheses.Find(hypothesis =>
				hypothesis.Id == orientationId &&
				hypothesis.Disposition == FragmentAnnotationDisposition.Accepted)
			: null;
		if (accepted == null) return;
		int sourceRegionId = State.OrientationSourceView?.RegionId ?? -1;
		if (sourceRegionId < 0) return;
		float currentRotation = commandSink.CaptureRegionRotationDegrees(sourceRegionId);
		float sourceRotation = State.OrientationSourceView?.RotationDegrees ?? currentRotation;
		float correction = FragmentOrientationEstimator.CalculateCorrection(
			accepted,
			currentRotation,
			sourceRotation);
		State.RotationCorrection = new FragmentRotationCorrection
		{
			RegionId = sourceRegionId,
			SourceOrientationId = accepted.Id,
			SourceRotationDegrees = sourceRotation,
			RoverDegrees = correction,
			ProposedDegrees = correction,
			Disposition = FragmentAnnotationDisposition.Proposed
		};
		PublishRotationCorrectionStatus("Proposed rotation correction");
	}

	public void AdjustRotationCorrection(float degrees)
	{
		FragmentRotationCorrection correction = State?.RotationCorrection;
		if (correction == null ||
			correction.Disposition != FragmentAnnotationDisposition.Proposed)
			return;
		if (IsRotationInProgress)
			CancelRotationExecution("Player edited the rotation proposal", true, true);
		correction.ProposedDegrees = Mathf.Wrap(degrees, -180f, 180f);
		correction.IsPlayerAdjusted =
			MathF.Abs(Mathf.Wrap(correction.ProposedDegrees - correction.RoverDegrees, -180f, 180f)) >
			0.01f;
		correction.Disposition = FragmentAnnotationDisposition.Proposed;
		PublishRotationCorrectionStatus("Player adjusted rotation correction");
	}

	public void ApplyRotationCorrectionEdit(FragmentRotationCorrectionEditAction action)
	{
		FragmentRotationCorrection correction = State?.RotationCorrection;
		if (correction == null) return;
		if (action == FragmentRotationCorrectionEditAction.Accept)
		{
			ApplyApprovedRotationCorrection(correction);
			return;
		}
		if (IsRotationInProgress)
			CancelRotationExecution("Rotation proposal rejected by player", true, true);
		correction.Disposition = FragmentAnnotationDisposition.Dismissed;
		PublishRotationCorrectionStatus("Player rejected rotation correction");
	}

	private void ApplyApprovedRotationCorrection(
		FragmentRotationCorrection correction,
		bool playerRequested = false)
	{
		if (commandSink == null ||
			(!playerRequested && (State?.IsPaused == true ||
			 GetEffectiveMode(FragmentAutonomyCapability.Rotate) != FragmentAutonomyMode.Performer)))
			return;
		if (IsRotationInProgress)
		{
			CancelRotationExecution("Rotation cancelled by player", true, true);
			return;
		}
		if (State.IsProcessingSearchActive || pendingProcessingAdjustment != null)
		{
			State.IsProcessingSearchActive = false;
			CancelPendingProcessingAdjustment();
			ProcessingSearchChanged?.Invoke();
		}
		// The accepted H# keeps its private orientation snapshot, but the comparison lock is a
		// display reference and must not survive into the live rotation. Release only the source
		// region's lock before scheduling the preview/tween; unrelated player locks remain intact.
		bool releasedOrientationSourceLock = ReleaseOrientationSourceRegionLock();
		FragmentCandidateRegion sourceRegion = State.CandidateRegions.Find(region =>
			region.Id == correction.RegionId &&
			region.Disposition != FragmentAnnotationDisposition.Dismissed);
		if (sourceRegion == null) return;
		rotationPlayerRequested = playerRequested;
		rotationSourceRegionId = sourceRegion.Id;
		rotationSourceRegionBounds = sourceRegion.NormalizedBounds;
		rotationSourcePivotNormalized = GetOrientationSourcePivotNormalized(sourceRegion);
		rotationStartDegrees = commandSink.CaptureRegionRotationDegrees(rotationSourceRegionId);
		rotationDeltaDegrees = Mathf.Wrap(correction.ProposedDegrees, -180f, 180f);
		rotationTargetDegrees = Mathf.Wrap(
			rotationStartDegrees + rotationDeltaDegrees,
			-180f,
			180f);
		rotationTweenDuration = Mathf.Clamp(settings?.RotationDurationSeconds ?? 0.6f, 0.1f, 5f);
		rotationTweenElapsed = 0f;
		rotationPreviewRemaining = Mathf.Clamp(settings?.ActionPreviewSeconds ?? 1f, 0f, 5f);
		isRotationTweenActive = false;
		string direction = GetRotationDirection(rotationDeltaDegrees);
		status = new FragmentRoverActionStatus
		{
			Activity = FragmentRoverActivity.Planning,
			CurrentAction = $"Previewing ROTATE {direction} {MathF.Abs(rotationDeltaDegrees):0.0}°",
			NextAction = rotationPreviewRemaining > 0f
				? $"Execute after {rotationPreviewRemaining:0.0}s preview"
				: "Execute approved rotation",
			CurrentTarget = $"Rotation ROT{correction.SourceOrientationId}",
			MeasuredResult =
				$"DISPLAY {rotationStartDegrees:+0.0;-0.0;0.0}° → {rotationTargetDegrees:+0.0;-0.0;0.0}°",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		RecordAction($"ROTATION PREVIEW: ROT{correction.SourceOrientationId} · " +
			$"{rotationDeltaDegrees:+0.0;-0.0;0.0}°");
		StatusChanged?.Invoke(status);
		RotationExecutionChanged?.Invoke();
		// RotationExecutionChanged closes side-by-side first. Refreshing the unlocked region after
		// that avoids a carousel-selection callback invalidating the just-accepted H#.
		if (releasedOrientationSourceLock) RegionsChanged?.Invoke();
		if (rotationPreviewRemaining <= 0f) BeginRotationTween();
	}

	public bool ExecuteAcceptedOrientationCorrectionFromPlayer()
	{
		if (State?.AcceptedOrientationId == null || IsRotationInProgress) return false;
		ProposeRotationCorrection(playerRequested: true);
		FragmentRotationCorrection correction = State.RotationCorrection;
		if (correction == null) return false;
		ApplyApprovedRotationCorrection(correction, playerRequested: true);
		return IsRotationInProgress;
	}

	private bool ReleaseOrientationSourceRegionLock()
	{
		if (State?.OrientationSourceView?.RegionId is not int sourceRegionId) return false;
		return State.LockedRegionViews.RemoveAll(view => view.RegionId == sourceRegionId) > 0;
	}

	private Vector2 GetOrientationSourcePivotNormalized(FragmentCandidateRegion region)
	{
		FragmentDetectedStructure structure = State?.OrientationSourceStructure;
		IReadOnlyList<FragmentDetectedFeature> features = State?.OrientationSourceView?.Features;
		if (structure == null || features == null) return region.NormalizedBounds.GetCenter();
		Vector2 sum = Vector2.Zero;
		int count = 0;
		foreach (int featureId in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = null;
			for (int index = 0; index < features.Count; index++)
				if (features[index].Id == featureId)
				{
					feature = features[index];
					break;
				}
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
			{
				sum += feature.Start + feature.End;
				count += 2;
			}
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
				{
					sum += segment.Start + segment.End;
					count += 2;
				}
		}
		return count > 0
			? (sum / count).Clamp(Vector2.Zero, Vector2.One)
			: region.NormalizedBounds.GetCenter();
	}

	public void CancelRotationCorrectionExecution() =>
		CancelRotationExecution("Rotation cancelled by player", true, true);

	private void ProcessRotationExecution(float delta)
	{
		if ((State?.IsPaused == true && !rotationPlayerRequested) || commandSink == null) return;
		if (rotationPreviewRemaining >= 0f)
		{
			rotationPreviewRemaining -= MathF.Max(delta, 0f);
			RotationExecutionChanged?.Invoke();
			if (rotationPreviewRemaining <= 0f) BeginRotationTween();
			return;
		}
		if (!isRotationTweenActive) return;
		rotationTweenElapsed = MathF.Min(
			rotationTweenElapsed + MathF.Max(delta, 0f),
			rotationTweenDuration);
		float progress = rotationTweenDuration <= 0f
			? 1f
			: Mathf.Clamp(rotationTweenElapsed / rotationTweenDuration, 0f, 1f);
		float eased = Mathf.SmoothStep(0f, 1f, progress);
		float displayed = Mathf.Wrap(
			rotationStartDegrees + rotationDeltaDegrees * eased,
			-180f,
			180f);
		commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.RegionRotation(
			rotationSourceRegionId,
			rotationSourceRegionBounds,
			rotationSourcePivotNormalized,
			displayed,
			FragmentAnalysisActionOrigin.Rover));
		RotationExecutionChanged?.Invoke();
		if (progress >= 1f) CompleteRotationExecution();
	}

	private void BeginRotationTween()
	{
		rotationPreviewRemaining = -1f;
		isRotationTweenActive = true;
		rotationTweenElapsed = 0f;
		status = new FragmentRoverActionStatus
		{
			Activity = FragmentRoverActivity.Executing,
			CurrentAction = $"ROTATE {GetRotationDirection(rotationDeltaDegrees)} " +
				$"{MathF.Abs(rotationDeltaDegrees):0.0}°",
			NextAction = "Player may edit, cancel, or use manual rotation controls",
			CurrentTarget = $"DISPLAY {rotationTargetDegrees:+0.0;-0.0;0.0}°",
			MeasuredResult = "ROTATION 0%",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		StatusChanged?.Invoke(status);
		RotationExecutionChanged?.Invoke();
	}

	private void CompleteRotationExecution()
	{
		FragmentRotationCorrection correction = State?.RotationCorrection;
		int sourceOrientationId = correction?.SourceOrientationId ?? 0;
		float appliedDelta = rotationDeltaDegrees;
		float finalTarget = rotationTargetDegrees;
		int completedRegionId = rotationSourceRegionId;
		Rect2 completedRegionBounds = rotationSourceRegionBounds;
		Vector2 completedPivot = rotationSourcePivotNormalized;
		bool playerAdjusted = correction?.IsPlayerAdjusted == true;
		ResetRotationExecutionState();
		if (correction != null)
			correction.Disposition = FragmentAnnotationDisposition.Accepted;
		FragmentObservableScan completedScan = observationSource?.CaptureObservableScan();
		// Rover tween notifications intentionally skip annotation work to keep animation smooth.
		// Reconcile retained/player geometry exactly once at completion, in sample-pixel space.
		TransformLiveAnnotationsForRotation(
			appliedDelta,
			completedScan?.SampleSize ?? Vector2.One,
			completedPivot,
			completedRegionId);
		FragmentRegionRotationState storedRotation = State.RegionRotations.Find(rotation =>
			rotation.RegionId == completedRegionId);
		if (storedRotation == null)
		{
			storedRotation = new FragmentRegionRotationState
			{
				RegionId = completedRegionId,
				// Preserve the pre-rotation selection mask so a reopened canvas identifies the
				// same puzzle strokes before replaying this region-local transform.
				RegionBounds = completedRegionBounds
			};
			State.RegionRotations.Add(storedRotation);
		}
		storedRotation.PivotNormalized = completedPivot;
		storedRotation.Degrees = finalTarget;
		preserveOrientationAcrossKnownRotation = true;
		RefreshDetectedFeatures(
			true,
			retainUnmatchedReviewed: false,
			requestSelectedFeatureFocus: false);
		preserveOrientationAcrossKnownRotation = false;
		RegionsChanged?.Invoke();
		StructuresChanged?.Invoke();
		RefreshSignalMetrics(true);
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = "Completed player-approved rotation",
			NextAction = "Review the rotated fragment or propose another orientation",
			CurrentTarget = sourceOrientationId > 0
				? $"Rotation ROT{sourceOrientationId}"
				: "Displayed fragment",
			MeasuredResult =
				$"{MathF.Abs(appliedDelta):0.0}° {GetRotationDirection(appliedDelta)} · " +
				$"DISPLAY {finalTarget:+0.0;-0.0;0.0}°" +
				(playerAdjusted ? " · PLAYER ADJUSTED" : string.Empty),
			LockedParameters = GetLockedProcessingParameterNames()
		};
		RecordAction($"ROTATION COMPLETE: {appliedDelta:+0.0;-0.0;0.0}°" +
			$" → {finalTarget:+0.0;-0.0;0.0}°");
		StatusChanged?.Invoke(status);
		RotationExecutionChanged?.Invoke();
		RotationCorrectionApplied?.Invoke(finalTarget);
		BeginAutonomousArrowReview();
	}

	private void CancelRotationExecution(string reason, bool preserveCorrection, bool recordHistory)
	{
		if (!IsRotationInProgress) return;
		if (isRotationTweenActive && rotationSourceRegionId >= 0 && commandSink != null)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.RegionRotation(
				rotationSourceRegionId,
				rotationSourceRegionBounds,
				rotationSourcePivotNormalized,
				rotationStartDegrees,
				FragmentAnalysisActionOrigin.Rover));
		ResetRotationExecutionState();
		if (State?.RotationCorrection != null)
		{
			if (preserveCorrection)
				State.RotationCorrection.Disposition = FragmentAnnotationDisposition.Proposed;
			else
				State.RotationCorrection = null;
		}
		status = new FragmentRoverActionStatus
		{
			Activity = FragmentRoverActivity.Overridden,
			CurrentAction = reason,
			NextAction = preserveCorrection
				? "Edit or restart the retained proposal"
				: "Estimate another rotation correction",
			CurrentTarget = "Displayed fragment",
			MeasuredResult = $"Stopped at {commandSink?.CaptureControlState().RotationDegrees ?? 0f:+0.0;-0.0;0.0}°",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		if (recordHistory) RecordAction($"ROTATION CANCELLED: {reason}");
		StatusChanged?.Invoke(status);
		RotationExecutionChanged?.Invoke();
		RotationCorrectionChanged?.Invoke();
	}

	private void ResetRotationExecutionState()
	{
		rotationPreviewRemaining = -1f;
		rotationTweenElapsed = 0f;
		rotationTweenDuration = 0f;
		isRotationTweenActive = false;
		rotationPlayerRequested = false;
		rotationSourceRegionId = -1;
		rotationSourceRegionBounds = default;
		rotationSourcePivotNormalized = new Vector2(0.5f, 0.5f);
	}

	private static string GetRotationDirection(float degrees) =>
		degrees > 0.01f ? "CW" : degrees < -0.01f ? "CCW" : "NONE";

	private void TransformLiveAnnotationsForRotation(
		float degrees,
		Vector2 sampleSize,
		Vector2 rotationPivotNormalized,
		int targetRegionId = -1)
	{
		if (State == null || MathF.Abs(degrees) <= 0.0001f) return;
		Vector2 safeSize = new(
			MathF.Max(sampleSize.X, 1f),
			MathF.Max(sampleSize.Y, 1f));
		Vector2 renderPivot = rotationPivotNormalized.Clamp(Vector2.Zero, Vector2.One) * safeSize;
		float radians = Mathf.DegToRad(degrees);
		List<FragmentDetectedFeature> originalFeatures = new(State.DetectedFeatures);
		Dictionary<int, List<int>> regionFeatureIds = new();
		Dictionary<int, Vector2> regionPivots = new();
		foreach (FragmentCandidateRegion region in State.CandidateRegions)
		{
			List<int> featureIds = new();
			foreach (FragmentDetectedFeature feature in originalFeatures)
			{
				if (feature.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
				if (region.FeatureIds.Contains(feature.Id) ||
					region.NormalizedBounds.HasPoint(GetFeatureCenter(feature)))
					featureIds.Add(feature.Id);
			}
			regionFeatureIds[region.Id] = featureIds;
			// A global manual rotation changes every visible glyph by the same angle, but each
			// Region remains an independent local frame. Reusing the selected/orientation source
			// pivot here made every other Region orbit that glyph instead of rotating in place.
			FragmentRegionRotationState committedRotation = State.RegionRotations.Find(rotation =>
				rotation.RegionId == region.Id);
			Vector2 regionPivotNormalized = targetRegionId == region.Id
				? rotationPivotNormalized
				: committedRotation?.PivotNormalized ?? region.NormalizedBounds.GetCenter();
			regionPivots[region.Id] =
				regionPivotNormalized.Clamp(Vector2.Zero, Vector2.One) * safeSize;
		}

		Dictionary<int, int> featureRegionIds = new();
		foreach (FragmentDetectedFeature feature in originalFeatures)
		{
			FragmentCandidateRegion owner = FindBestRegion(feature.Id, GetFeatureCenter(feature));
			if (owner != null) featureRegionIds[feature.Id] = owner.Id;
		}
		Dictionary<int, int> arrowRegionIds = new();
		foreach (FragmentArrowCandidate arrow in State.ArrowCandidates)
		{
			FragmentCandidateRegion owner = FindBestArrowRegion(arrow);
			if (owner != null) arrowRegionIds[arrow.Id] = owner.Id;
		}

		List<FragmentDetectedFeature> transformed = new(State.DetectedFeatures.Count);
		foreach (FragmentDetectedFeature feature in originalFeatures)
		{
			bool shouldTransform = targetRegionId < 0 ||
				(featureRegionIds.TryGetValue(feature.Id, out int ownerRegionId) &&
				 ownerRegionId == targetRegionId);
			if (!shouldTransform)
			{
				transformed.Add(CloneDetectedFeature(feature));
				continue;
			}
			Vector2 pivot = featureRegionIds.TryGetValue(feature.Id, out int regionId)
				? regionPivots[regionId]
				: renderPivot;
			List<FragmentFeatureSegment> segments = new(feature.Segments.Count);
			foreach (FragmentFeatureSegment segment in feature.Segments)
				segments.Add(new FragmentFeatureSegment
				{
					Start = TransformPoint(segment.Start, pivot),
					End = TransformPoint(segment.End, pivot)
				});
			transformed.Add(new FragmentDetectedFeature
			{
				Id = feature.Id,
				Start = TransformPoint(feature.Start, pivot),
				End = TransformPoint(feature.End, pivot),
				Segments = segments,
				Confidence = feature.Confidence,
				Provenance = feature.Provenance,
				Disposition = feature.Disposition,
				IsInferred = feature.IsInferred
			});
		}
		State.DetectedFeatures.Clear();
		State.DetectedFeatures.AddRange(transformed);

		List<FragmentArrowCandidate> transformedArrows = new(State.ArrowCandidates.Count);
		foreach (FragmentArrowCandidate candidate in State.ArrowCandidates)
		{
			bool shouldTransform = targetRegionId < 0 ||
				(arrowRegionIds.TryGetValue(candidate.Id, out int ownerRegionId) &&
				 ownerRegionId == targetRegionId);
			Vector2 pivot = arrowRegionIds.TryGetValue(candidate.Id, out int regionId)
				? regionPivots[regionId]
				: renderPivot;
			transformedArrows.Add(new FragmentArrowCandidate
			{
				Id = candidate.Id,
				Tail = shouldTransform ? TransformPoint(candidate.Tail, pivot) : candidate.Tail,
				Tip = shouldTransform ? TransformPoint(candidate.Tip, pivot) : candidate.Tip,
				Confidence = candidate.Confidence,
				Disposition = candidate.Disposition,
				FeatureIds = new List<int>(candidate.FeatureIds),
				Provenance = candidate.Provenance,
				IsPlayerDefined = candidate.IsPlayerDefined,
				Evidence = candidate.Evidence,
				RegionId = candidate.RegionId
			});
		}
		State.ArrowCandidates.Clear();
		State.ArrowCandidates.AddRange(transformedArrows);

		foreach (FragmentCandidateRegion region in State.CandidateRegions)
		{
			if (targetRegionId >= 0 && region.Id != targetRegionId) continue;
			Rect2 bounds = region.NormalizedBounds;
			Rect2 transformedBounds = new();
			bool hasTransformedPoint = false;
			Vector2 pivot = regionPivots[region.Id];
			foreach (int featureId in regionFeatureIds[region.Id])
			{
				FragmentDetectedFeature member = State.DetectedFeatures.Find(feature =>
					feature.Id == featureId);
				if (member == null) continue;
				if (member.Segments.Count == 0)
				{
					AddRegionPoint(member.Start);
					AddRegionPoint(member.End);
				}
				else
					foreach (FragmentFeatureSegment segment in member.Segments)
					{
						AddRegionPoint(segment.Start);
						AddRegionPoint(segment.End);
					}
			}
			foreach ((int arrowId, int ownerRegionId) in arrowRegionIds)
				if (ownerRegionId == region.Id)
				{
					FragmentArrowCandidate arrow = State.ArrowCandidates.Find(candidate =>
						candidate.Id == arrowId);
					if (arrow == null) continue;
					AddRegionPoint(arrow.Tail);
					AddRegionPoint(arrow.Tip);
				}
			// Fit a new axis-aligned box from transformed content. Rotating the previous AABB on every
			// tween frame compounds empty corner extents and progressively distorts the Region bounds.
			if (!hasTransformedPoint)
				foreach (Vector2 corner in GetRectCorners(bounds))
					AddRegionPoint(TransformPoint(corner, pivot));
			if (hasTransformedPoint)
			{
				const float margin = 0.015f;
				region.NormalizedBounds = new Rect2(
					transformedBounds.Position - new Vector2(margin, margin),
					transformedBounds.Size + new Vector2(margin * 2f, margin * 2f));
			}
			else region.NormalizedBounds = bounds;

			void AddRegionPoint(Vector2 point)
			{
				if (!hasTransformedPoint)
				{
					transformedBounds = new Rect2(point, Vector2.Zero);
					hasTransformedPoint = true;
				}
				else transformedBounds = transformedBounds.Expand(point);
			}
		}

		FragmentCandidateRegion FindBestRegion(int featureId, Vector2 center)
		{
			foreach (FragmentRegionRotationState committed in State.RegionRotations)
			{
				FragmentCandidateRegion owner = State.CandidateRegions.Find(region =>
					region.Id == committed.RegionId &&
					region.Disposition != FragmentAnnotationDisposition.Dismissed &&
					(region.FeatureIds.Contains(featureId) ||
					 region.NormalizedBounds.HasPoint(center)));
				if (owner != null) return owner;
			}
			FragmentCandidateRegion best = null;
			foreach (FragmentCandidateRegion region in State.CandidateRegions)
			{
				if (!regionFeatureIds[region.Id].Contains(featureId) &&
					!region.NormalizedBounds.HasPoint(center)) continue;
				if (best == null || region.Id == State.SelectedRegionId ||
					(region.Disposition == FragmentAnnotationDisposition.Accepted &&
					 best.Disposition != FragmentAnnotationDisposition.Accepted) ||
					RectArea(region.NormalizedBounds) < RectArea(best.NormalizedBounds))
					best = region;
			}
			return best;
		}

		FragmentCandidateRegion FindBestArrowRegion(FragmentArrowCandidate arrow)
		{
			if (arrow.RegionId >= 0)
			{
				FragmentCandidateRegion assigned = State.CandidateRegions.Find(region =>
					region.Id == arrow.RegionId &&
					region.Disposition != FragmentAnnotationDisposition.Dismissed);
				if (assigned != null) return assigned;
			}
			Vector2 center = (arrow.Tail + arrow.Tip) * 0.5f;
			FragmentCandidateRegion best = null;
			foreach (FragmentCandidateRegion region in State.CandidateRegions)
			{
				bool sharesFeature = arrow.FeatureIds.Exists(
					regionFeatureIds[region.Id].Contains);
				if (!sharesFeature && !region.NormalizedBounds.HasPoint(center)) continue;
				if (best == null || region.Id == State.SelectedRegionId ||
					RectArea(region.NormalizedBounds) < RectArea(best.NormalizedBounds))
					best = region;
			}
			return best;
		}

		Vector2 TransformPoint(Vector2 normalized, Vector2 pivot)
		{
			Vector2 pixels = normalized * safeSize;
			return (pivot + (pixels - pivot).Rotated(radians)) / safeSize;
		}

		static Vector2[] GetRectCorners(Rect2 rectangle)
		{
			return new[]
			{
				rectangle.Position,
				new Vector2(rectangle.End.X, rectangle.Position.Y),
				rectangle.End,
				new Vector2(rectangle.Position.X, rectangle.End.Y)
			};
		}

		static float RectArea(Rect2 rectangle) => rectangle.Size.X * rectangle.Size.Y;
	}

	private void PublishRotationCorrectionStatus(string action)
	{
		FragmentRotationCorrection correction = State.RotationCorrection;
		string direction = correction.ProposedDegrees > 0.01f
			? "CW"
			: correction.ProposedDegrees < -0.01f ? "CCW" : "NONE";
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = action,
			NextAction = correction.Disposition == FragmentAnnotationDisposition.Accepted
				? "Execute the approved target in the Rotate step"
				: correction.Disposition == FragmentAnnotationDisposition.Dismissed
					? "Choose or propose another correction"
					: "Adjust, accept, or reject the proposal",
			CurrentTarget = $"Rotation ROT{correction.SourceOrientationId}",
			MeasuredResult = $"{MathF.Abs(correction.ProposedDegrees):0.0}° {direction}" +
				(correction.IsPlayerAdjusted ? " · PLAYER ADJUSTED" : string.Empty),
			LockedParameters = GetLockedProcessingParameterNames()
		};
		RecordAction($"CORRECTION: {action} · {correction.ProposedDegrees:+0.0;-0.0;0.0}°");
		StatusChanged?.Invoke(status);
		RotationCorrectionChanged?.Invoke();
	}

	private void InvalidateOrientationIfGeometryChanged()
	{
		if (State?.OrientationHypotheses.Count <= 0) return;
		if (preserveOrientationAcrossKnownRotation)
		{
			preserveOrientationAcrossKnownRotation = false;
			return;
		}
		if (State.OrientationSourceView is FragmentLockedRegionView sourceView &&
			State.LockedRegionViews.Exists(view => view.RegionId == sourceView.RegionId))
			return;
		// A live region follows the current filters, so any observable-geometry revision makes its
		// retained orientation snapshot stale. A genuinely locked comparison region is handled above.
		InvalidateOrientationHypotheses();
	}

	private void InvalidateOrientationHypotheses()
	{
		if (State == null || (State.OrientationHypotheses.Count == 0 &&
			!State.SelectedOrientationId.HasValue && !State.AcceptedOrientationId.HasValue &&
			State.OrientationSourceView == null && State.OrientationSourceStructure == null)) return;
		State.OrientationHypotheses.Clear();
		State.SelectedOrientationId = null;
		State.AcceptedOrientationId = null;
		State.OrientationSourceView = null;
		State.OrientationSourceStructure = null;
		State.RotationCorrection = null;
		OrientationsChanged?.Invoke();
		RotationCorrectionChanged?.Invoke();
	}

	private void PublishStructureEditStatus(string action, int structureId)
	{
		FragmentDetectedStructure structure = State.DetectedStructures.Find(candidate =>
			candidate.Id == structureId);
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = action,
			NextAction = "Continue reconstructed-structure review",
			CurrentTarget = $"Structure {structureId}",
			MeasuredResult = $"{structure?.FeatureIds.Count ?? 0} member features",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		RecordAction($"{action} S{structureId}");
		StatusChanged?.Invoke(status);
		StructuresChanged?.Invoke();
	}

	private int GetNextStructureId()
	{
		int highest = 0;
		if (State != null)
			foreach (FragmentDetectedStructure structure in State.DetectedStructures)
				highest = Math.Max(highest, structure.Id);
		return highest + 1;
	}

	private static float MembershipSimilarity(IReadOnlyList<int> first, IReadOnlyList<int> second)
	{
		if (first == null || second == null || first.Count == 0 || second.Count == 0) return 0f;
		int intersection = 0;
		foreach (int id in first)
			if (ContainsId(second, id)) intersection++;
		int union = first.Count + second.Count - intersection;
		return union == 0 ? 0f : (float)intersection / union;
	}

	private static bool ContainsId(IReadOnlyList<int> ids, int target)
	{
		for (int index = 0; index < ids.Count; index++)
			if (ids[index] == target) return true;
		return false;
	}

	public void ApplyRegionEdit(
		FragmentRegionEditAction action,
		int regionId,
		bool applyCropOnAccept = true)
	{
		FragmentCandidateRegion region = State?.CandidateRegions.Find(candidate => candidate.Id == regionId);
		if (region == null) return;
		int? nextRegionToReview = null;
		bool regionReviewCompleted = false;
		if (action == FragmentRegionEditAction.Select)
		{
			if (State.SelectedRegionId != regionId) InvalidateOrientationHypotheses();
			State.SelectedRegionId = regionId;
			PublishRegionEditStatus("Selected region", regionId);
			return;
		}
		region.Disposition = action switch
		{
			FragmentRegionEditAction.Accept => FragmentAnnotationDisposition.Accepted,
			FragmentRegionEditAction.Dismiss => FragmentAnnotationDisposition.Dismissed,
			FragmentRegionEditAction.Restore => region.Provenance == FragmentAnnotationProvenance.Player
				? FragmentAnnotationDisposition.Accepted
				: FragmentAnnotationDisposition.Proposed,
			_ => region.Disposition
		};
		if (action == FragmentRegionEditAction.Accept)
		{
			if (applyCropOnAccept)
			{
				State.ActiveCropRegionId = regionId;
				ApplyRegionCrop();
			}
			nextRegionToReview = FindNextProposedRegionId(regionId);
			State.SelectedRegionId = nextRegionToReview ?? regionId;
			regionReviewCompleted = !nextRegionToReview.HasValue;
		}
		else if (action == FragmentRegionEditAction.Dismiss)
		{
			State.LockedRegionViews.RemoveAll(view => view.RegionId == regionId);
			if (NavigationTargetRegionId == regionId) ClearNavigationTarget(true);
			if (State.ActiveCropRegionId == regionId)
			{
				State.ActiveCropRegionId = State.CandidateRegions.Find(candidate =>
					candidate.Id != regionId &&
					candidate.Disposition == FragmentAnnotationDisposition.Accepted)?.Id;
			}
			DismissFeaturesInside(region.NormalizedBounds);
			State.SelectedRegionId = FindNextProposedRegionId(regionId);
			nextRegionToReview = State.SelectedRegionId;
			regionReviewCompleted = !nextRegionToReview.HasValue;
		}
		else
		{
			if (action == FragmentRegionEditAction.Restore)
			{
				featureReviewRegionIds.Clear();
				isAcceptedRegionFeatureReviewActive = false;
			}
			State.SelectedRegionId = regionId;
		}
		PublishRegionEditStatus($"{action} region", regionId);
		if (regionReviewCompleted &&
			(action == FragmentRegionEditAction.Accept || action == FragmentRegionEditAction.Dismiss))
		{
			CompleteRegionReview();
		}
		else if (action == FragmentRegionEditAction.Accept)
		{
			if (nextRegionToReview.HasValue)
				RegionFocusRequested?.Invoke(nextRegionToReview.Value);
			else if (NavigationTargetRegionId == regionId)
				ClearNavigationTarget(true);
		}
		else
		{
			RequestSelectedRegionFocus();
		}
	}

	private void CompleteRegionReview()
	{
		List<FragmentCandidateRegion> acceptedRegions = State.CandidateRegions.FindAll(region =>
			region.Disposition == FragmentAnnotationDisposition.Accepted);
		acceptedRegions.Sort((first, second) => first.Id.CompareTo(second.Id));
		featureReviewRegionIds.Clear();
		featureReviewRegionIds.AddRange(acceptedRegions.ConvertAll(region => region.Id));
		isAcceptedRegionFeatureReviewActive = true;
		State.SelectedFeatureId = FindFirstProposedFeatureInRegions(
			acceptedRegions, out int? featureRegionId);
		State.SelectedRegionId = featureRegionId ??
			(acceptedRegions.Count > 0 ? acceptedRegions[0].Id : null);
		if (NavigationTargetRegionId.HasValue) ClearNavigationTarget(true);

		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = "Candidate-region review completed",
			NextAction = State.SelectedFeatureId.HasValue
				? "Accept or dismiss the selected feature"
				: "No proposed features remain",
			CurrentTarget = State.SelectedFeatureId is int featureId
				? $"Feature {featureId}"
				: acceptedRegions.Count > 0 ? $"Region {acceptedRegions[0].Id}" : "Whole fragment",
			MeasuredResult = $"{acceptedRegions.Count} accepted regions",
			LockedParameters = "None"
		};
		FeaturesChanged?.Invoke();
		RegionsChanged?.Invoke();
		RefreshSignalMetrics(true);
		RegionReviewCompleted?.Invoke(acceptedRegions.Count);
		StatusChanged?.Invoke(status);
		UpdateCurrentHistorySnapshot();
		if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingRegionReview &&
			acceptedRegions.Count > 0)
			BeginAutonomousFeatureSearch();
		if (acceptedRegions.Count == 1 &&
			autonomousWorkflowStage != FragmentAutonomousWorkflowStage.SearchingRegionFeatures)
			RegionFocusRequested?.Invoke(acceptedRegions[0].Id);
	}

	private int? FindFirstProposedFeatureInRegions(
		IReadOnlyList<FragmentCandidateRegion> acceptedRegions,
		out int? containingRegionId)
	{
		containingRegionId = null;
		foreach (FragmentCandidateRegion region in acceptedRegions)
		{
			FragmentDetectedFeature feature = State.DetectedFeatures.Find(candidate =>
				candidate.Disposition == FragmentAnnotationDisposition.Proposed &&
				(region.FeatureIds.Contains(candidate.Id) ||
					region.NormalizedBounds.HasPoint(GetFeatureCenter(candidate))));
			if (feature != null)
			{
				containingRegionId = region.Id;
				return feature.Id;
			}
		}
		return null;
	}

	public void AddPlayerRegion(Rect2 normalizedBounds)
	{
		if (State == null || normalizedBounds.Size.X < 0.01f || normalizedBounds.Size.Y < 0.01f) return;
		Rect2 bounds = new(
			normalizedBounds.Position.Clamp(Vector2.Zero, Vector2.One),
			normalizedBounds.End.Clamp(Vector2.Zero, Vector2.One) -
				normalizedBounds.Position.Clamp(Vector2.Zero, Vector2.One));
		int id = GetNextRegionId();
		List<int> featureIds = FindFeaturesInRegion(bounds);
		// Drawing a replacement region is an explicit request to review that area again. Features
		// dismissed by an earlier region crop/dismissal must therefore become actionable in the new
		// scope instead of leaving every feature button disabled.
		foreach (int featureId in featureIds)
		{
			FragmentDetectedFeature feature = State.DetectedFeatures.Find(candidate =>
				candidate.Id == featureId);
			if (feature?.Provenance == FragmentAnnotationProvenance.Rover &&
				feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				feature.Disposition = FragmentAnnotationDisposition.Proposed;
		}
		bool requiresReview = autonomousWorkflowStage ==
			FragmentAutonomousWorkflowStage.AwaitingRegionReview;
		State.CandidateRegions.Add(new FragmentCandidateRegion
		{
			Id = id,
			NormalizedBounds = bounds,
			Confidence = 1f,
			Provenance = FragmentAnnotationProvenance.Player,
			Disposition = requiresReview
				? FragmentAnnotationDisposition.Proposed
				: FragmentAnnotationDisposition.Accepted,
			FeatureIds = featureIds
		});
		State.SelectedRegionId = id;
		if (requiresReview)
		{
			PublishRegionEditStatus("Added player region for review", id);
			FeaturesChanged?.Invoke();
			RegionsChanged?.Invoke();
			RegionFocusRequested?.Invoke(id);
			return;
		}
		State.ActiveCropRegionId = id;
		featureReviewRegionIds.RemoveAll(regionId => !IsRetainedRegion(regionId));
		if (!featureReviewRegionIds.Contains(id)) featureReviewRegionIds.Add(id);
		isAcceptedRegionFeatureReviewActive = true;
		featureReviewPriorityRegionIds.Clear();
		featureReviewPriorityRegionIds.Add(id);
		ApplyRegionCrop();
		State.SelectedFeatureId = State.DetectedFeatures.Find(feature =>
			feature.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsFeatureInRegions(feature, featureReviewPriorityRegionIds))?.Id;
		AlignSelectedRegionToFeature(State.SelectedFeatureId);
		PublishRegionEditStatus("Added player region", id);
		FeaturesChanged?.Invoke();
		RegionFocusRequested?.Invoke(id);
	}

	public void ResizeRegion(int regionId, Rect2 normalizedBounds)
	{
		FragmentCandidateRegion region = State?.CandidateRegions.Find(candidate => candidate.Id == regionId);
		if (region == null || IsRegionViewLocked(regionId) ||
			normalizedBounds.Size.X < 0.01f || normalizedBounds.Size.Y < 0.01f) return;
		Vector2 start = normalizedBounds.Position.Clamp(Vector2.Zero, Vector2.One);
		Vector2 end = normalizedBounds.End.Clamp(Vector2.Zero, Vector2.One);
		region.NormalizedBounds = new Rect2(start, end - start);
		State.LockedRegionViews.RemoveAll(view => view.RegionId == regionId);
		region.FeatureIds.Clear();
		region.FeatureIds.AddRange(FindFeaturesInRegion(region.NormalizedBounds));
		if (State.ActiveCropRegionId.HasValue)
			ApplyRegionCrop();
		PublishRegionEditStatus("Resized region", regionId);
		RegionFocusRequested?.Invoke(regionId);
	}

	public void DeleteRegion(int regionId)
	{
		FragmentCandidateRegion region = State?.CandidateRegions.Find(candidate =>
			candidate.Id == regionId &&
			candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
		if (region == null) return;

		region.Disposition = FragmentAnnotationDisposition.Dismissed;
		State.LockedRegionViews.RemoveAll(view => view.RegionId == regionId);
		featureReviewRegionIds.Remove(regionId);
		featureReviewPriorityRegionIds.Remove(regionId);
		structureReviewPriorityRegionIds.Remove(regionId);
		autonomousRegionIds.Remove(regionId);
		autonomousRegionBestConfigurations.Remove(regionId);

		if (NavigationTargetRegionId == regionId) ClearNavigationTarget(true);
		if (State.ActiveCropRegionId == regionId)
		{
			State.ActiveCropRegionId = State.CandidateRegions.Find(candidate =>
				candidate.Id != regionId &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted)?.Id;
		}
		if (State.SelectedRegionId == regionId)
		{
			State.SelectedRegionId = State.CandidateRegions.Find(candidate =>
				candidate.Id != regionId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed)?.Id;
		}

		bool arrowsChanged = false;
		foreach (FragmentArrowCandidate arrow in State.ArrowCandidates)
		{
			if (arrow.RegionId != regionId ||
				arrow.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
			arrow.Disposition = FragmentAnnotationDisposition.Dismissed;
			arrowsChanged = true;
		}
		if (State.SelectedArrowId is int selectedArrowId &&
			State.ArrowCandidates.Find(candidate => candidate.Id == selectedArrowId)?.RegionId == regionId)
			State.SelectedArrowId = null;
		if (State.AcceptedArrowId is int acceptedArrowId &&
			State.ArrowCandidates.Find(candidate => candidate.Id == acceptedArrowId)?.RegionId == regionId)
			State.AcceptedArrowId = null;

		if (State.OrientationSourceView?.RegionId == regionId)
			InvalidateOrientationHypotheses();
		if (State.DirectionInterpretation?.RegionId == regionId)
			InvalidateDirectionInterpretation();

		bool completesRegionReview =
			autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingRegionReview &&
			!State.CandidateRegions.Exists(candidate =>
				candidate.Disposition == FragmentAnnotationDisposition.Proposed);
		PublishRegionEditStatus("Deleted region", regionId);
		if (arrowsChanged) ArrowCandidatesChanged?.Invoke();
		if (completesRegionReview) CompleteRegionReview();
	}

	public bool IsRegionViewLocked(int regionId) =>
		State?.LockedRegionViews.Exists(view => view.RegionId == regionId) == true;

	public void ToggleRegionViewLock(int regionId)
	{
		if (State == null || observationSource == null) return;
		FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
			candidate.Id == regionId &&
			candidate.Disposition == FragmentAnnotationDisposition.Accepted);
		if (region == null) return;
		int existingIndex = State.LockedRegionViews.FindIndex(view => view.RegionId == regionId);
		if (existingIndex >= 0)
		{
			State.LockedRegionViews.RemoveAt(existingIndex);
			if (State.OrientationSourceView?.RegionId == regionId)
				InvalidateOrientationHypotheses();
			PublishRegionEditStatus("Unlocked rendering for region", regionId);
			return;
		}

		FragmentObservableScan currentScan = observationSource.CaptureObservableScan();
		if (currentScan == null) return;
		FragmentLockedRegionView lockedView = new()
		{
			RegionId = regionId,
			NormalizedBounds = region.NormalizedBounds,
			RotationDegrees = commandSink?.CaptureRegionRotationDegrees(regionId) ?? 0f,
			Scan = CloneObservableScan(currentScan)
		};
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
		{
			if (feature.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
			lockedView.Features.Add(CloneDetectedFeature(feature));
		}
		State.LockedRegionViews.Add(lockedView);
		if (State.OrientationSourceView?.RegionId == regionId)
			InvalidateOrientationHypotheses();
		PublishRegionEditStatus("Locked rendering for region", regionId);
	}

	private static FragmentObservableScan CloneObservableScan(FragmentObservableScan source)
	{
		List<FragmentObservablePrimitive> primitives = new();
		foreach (FragmentObservablePrimitive primitive in source.Primitives)
		{
			primitives.Add(new FragmentObservablePrimitive
			{
				Id = primitive.Id,
				Start = primitive.Start,
				End = primitive.End,
				Color = primitive.Color,
				Width = primitive.Width,
				Intensity = primitive.Intensity
			});
		}
		return new FragmentObservableScan
		{
			Revision = source.Revision,
			SampleSize = source.SampleSize,
			RotationPivotNormalized = source.RotationPivotNormalized,
			Primitives = primitives
		};
	}

	private static FragmentDetectedFeature CloneDetectedFeature(FragmentDetectedFeature feature) => new()
	{
		Id = feature.Id,
		Start = feature.Start,
		End = feature.End,
		Segments = feature.Segments.ConvertAll(segment => new FragmentFeatureSegment
		{
			Start = segment.Start,
			End = segment.End
		}),
		Confidence = feature.Confidence,
		Provenance = feature.Provenance,
		Disposition = feature.Disposition,
		IsInferred = feature.IsInferred
	};

	private void PruneMissingStructureMembers()
	{
		HashSet<int> availableFeatureIds = new();
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
			if (feature.Disposition != FragmentAnnotationDisposition.Dismissed)
				availableFeatureIds.Add(feature.Id);
		foreach (FragmentDetectedStructure structure in State.DetectedStructures)
			structure.FeatureIds.RemoveAll(featureId => !availableFeatureIds.Contains(featureId));
		if (State.SelectedStructureId is int selectedId &&
			State.DetectedStructures.Find(structure => structure.Id == selectedId)?.FeatureIds.Count == 0)
			State.SelectedStructureId = null;
	}

	private void ApplyRegionCrop()
	{
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
		{
			if (!IsFeatureInsideRetainedRegion(feature))
				feature.Disposition = FragmentAnnotationDisposition.Dismissed;
		}
		SelectFirstVisibleFeatureIfNeeded();
		FeaturesChanged?.Invoke();
	}

	private void ApplyActiveCropToFeatures()
	{
		if (State?.ActiveCropRegionId is not int regionId) return;
		FragmentCandidateRegion crop = State.CandidateRegions.Find(region =>
			region.Id == regionId && region.Disposition != FragmentAnnotationDisposition.Dismissed);
		if (crop == null)
		{
			State.ActiveCropRegionId = null;
			return;
		}
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
			if (!IsFeatureInsideRetainedRegion(feature))
				feature.Disposition = FragmentAnnotationDisposition.Dismissed;
		SelectFirstVisibleFeatureIfNeeded();
	}

	private bool IsFeatureInsideRetainedRegion(FragmentDetectedFeature feature)
	{
		Vector2 center = GetFeatureCenter(feature);
		return State.CandidateRegions.Exists(region =>
			region.Disposition != FragmentAnnotationDisposition.Dismissed &&
			region.NormalizedBounds.HasPoint(center));
	}

	private void DismissFeaturesInside(Rect2 bounds)
	{
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
		{
			if (bounds.HasPoint(GetFeatureCenter(feature)))
				feature.Disposition = FragmentAnnotationDisposition.Dismissed;
		}
		SelectFirstVisibleFeatureIfNeeded();
		FeaturesChanged?.Invoke();
	}

	private void SelectFirstVisibleFeatureIfNeeded()
	{
		FragmentDetectedFeature selected = State.SelectedFeatureId is int selectedId
			? State.DetectedFeatures.Find(feature => feature.Id == selectedId)
			: null;
		if (selected?.Disposition != FragmentAnnotationDisposition.Dismissed) return;
		State.SelectedFeatureId = FindFirstProposedFeatureId();
	}

	private void PublishRegionEditStatus(string action, int regionId)
	{
		int retainedRegionCount = State.CandidateRegions.FindAll(candidate =>
			candidate.Disposition != FragmentAnnotationDisposition.Dismissed).Count;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = action,
			NextAction = "Continue candidate-region review",
			CurrentTarget = $"Region {regionId}",
			MeasuredResult = $"{retainedRegionCount} retained regions",
			LockedParameters = "None"
		};
		RecordAction($"{action} R{regionId}");
		StatusChanged?.Invoke(status);
		RegionsChanged?.Invoke();
		RefreshSignalMetrics(true);
	}

	private List<int> FindFeaturesInRegion(Rect2 bounds)
	{
		List<int> ids = new();
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
			if (bounds.HasPoint(GetFeatureCenter(feature))) ids.Add(feature.Id);
		return ids;
	}

	private int GetNextRegionId()
	{
		int highest = 0;
		if (State != null)
			foreach (FragmentCandidateRegion region in State.CandidateRegions)
				highest = Math.Max(highest, region.Id);
		return highest + 1;
	}

	private static FragmentCandidateRegion FindBestPreviousRegion(
		List<FragmentCandidateRegion> previous,
		List<int> matchedIds,
		FragmentCandidateRegion candidate)
	{
		FragmentCandidateRegion best = null;
		float bestDistance = 0.01f;
		Vector2 center = candidate.NormalizedBounds.GetCenter();
		foreach (FragmentCandidateRegion region in previous)
		{
			if (matchedIds.Contains(region.Id)) continue;
			float distance = region.NormalizedBounds.GetCenter().DistanceSquaredTo(center);
			if (distance >= bestDistance) continue;
			bestDistance = distance;
			best = region;
		}
		return best;
	}

	private static bool OverlapsExistingRegionByMoreThanHalf(
		Rect2 candidateBounds,
		IReadOnlyList<FragmentCandidateRegion> existingRegions,
		int? replacedRegionId = null)
	{
		float candidateArea = MathF.Max(
			candidateBounds.Size.X * candidateBounds.Size.Y, 0.000001f);
		foreach (FragmentCandidateRegion existing in existingRegions)
		{
			if (existing == null || existing.Id == replacedRegionId ||
				existing.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
			Rect2 intersection = candidateBounds.Intersection(existing.NormalizedBounds);
			float intersectionArea = MathF.Max(intersection.Size.X, 0f) *
				MathF.Max(intersection.Size.Y, 0f);
			if (intersectionArea / candidateArea > 0.5f) return true;
		}
		return false;
	}

	private int? FindFirstProposedRegionId() => State?.CandidateRegions.Find(region =>
		region.Disposition == FragmentAnnotationDisposition.Proposed)?.Id;

	private int? FindNextProposedRegionId(int afterId)
	{
		if (State == null || State.CandidateRegions.Count == 0) return null;
		int start = State.CandidateRegions.FindIndex(region => region.Id == afterId);
		for (int offset = 1; offset <= State.CandidateRegions.Count; offset++)
		{
			FragmentCandidateRegion region = State.CandidateRegions[
				(start + offset + State.CandidateRegions.Count) % State.CandidateRegions.Count];
			if (region.Disposition == FragmentAnnotationDisposition.Proposed) return region.Id;
		}
		return null;
	}

	private void RequestSelectedRegionFocus()
	{
		if (State?.SelectedRegionId is int regionId) RegionFocusRequested?.Invoke(regionId);
	}


	public void ApplyFeatureEdit(
		FragmentFeatureEditAction action,
		int featureId)
	{
		if (State == null) return;

		PruneFeatureReviewPriority();
		FragmentDetectedFeature feature = State.DetectedFeatures.Find(
			candidate => candidate.Id == featureId);
		if (feature == null) return;
		bool strictAutonomousRegionReview = IsAutonomousRegionFeatureScopeActive;
		if ((action != FragmentFeatureEditAction.Select || strictAutonomousRegionReview) &&
			featureReviewPriorityRegionIds.Count > 0 &&
			!IsFeatureInRegions(feature, featureReviewPriorityRegionIds)) return;
		switch (action)
		{
			case FragmentFeatureEditAction.Select:
				State.SelectedFeatureId = featureId;
				AlignSelectedRegionToFeature(featureId);
				PublishFeatureEditStatus("Selected feature", featureId);
				return;
			case FragmentFeatureEditAction.Accept:
				feature.Disposition = FragmentAnnotationDisposition.Accepted;
				break;
			case FragmentFeatureEditAction.Dismiss:
				feature.Disposition = FragmentAnnotationDisposition.Dismissed;
				bool changedStructure = false;
				foreach (FragmentDetectedStructure structure in State.DetectedStructures)
				{
					if (!structure.FeatureIds.Remove(featureId)) continue;
					structure.IsPlayerEdited = true;
					changedStructure = true;
				}
				if (changedStructure) InvalidateOrientationHypotheses();
				break;
			case FragmentFeatureEditAction.Restore:
				feature.Disposition = FragmentAnnotationDisposition.Proposed;
				break;
			default:
				return;
		}
		State.SelectedFeatureId = action == FragmentFeatureEditAction.Restore
			? featureId
			: FindNextProposedFeatureId(featureId);
		AlignSelectedRegionToFeature(State.SelectedFeatureId);
		PublishFeatureEditStatus($"{action} feature", featureId);
		RequestSelectedFeatureFocus();
		if (action != FragmentFeatureEditAction.Restore)
			AdvanceAutonomousFeatureReviewIfComplete();
	}

	private void PublishFeatureEditStatus(string action, int featureId)
	{
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = action,
			NextAction = "Continue feature review",
			CurrentTarget = $"Feature {featureId}",
			MeasuredResult = $"{State.DetectedFeatures.Count} stored features",
			LockedParameters = "None"
		};
		RecordAction($"{action} F{featureId}");
		StatusChanged?.Invoke(status);
		FeaturesChanged?.Invoke();
		StructuresChanged?.Invoke();
		OrientationsChanged?.Invoke();
		if (isAcceptedRegionFeatureReviewActive)
		{
			RegionsChanged?.Invoke();
			RefreshSignalMetrics(true);
		}
	}

	private void AlignSelectedRegionToFeature(int? featureId)
	{
		if (!isAcceptedRegionFeatureReviewActive || !featureId.HasValue) return;
		FragmentDetectedFeature feature = State.DetectedFeatures.Find(candidate =>
			candidate.Id == featureId.Value);
		if (feature == null) return;
		if (IsAutonomousRegionFeatureScopeActive &&
			IsRetainedRegion(autonomousTargetRegionId) &&
			IsFeatureInRegions(feature, featureReviewPriorityRegionIds))
		{
			State.SelectedRegionId = autonomousTargetRegionId;
			return;
		}
		Vector2 center = GetFeatureCenter(feature);
		FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
			featureReviewRegionIds.Contains(candidate.Id) &&
			candidate.Disposition == FragmentAnnotationDisposition.Accepted &&
			(candidate.FeatureIds.Contains(feature.Id) ||
				candidate.NormalizedBounds.HasPoint(center)));
		if (region != null) State.SelectedRegionId = region.Id;
	}

	private int GetNextFeatureId()
	{
		int highest = 0;
		if (State != null)
		{
			foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
				highest = Math.Max(highest, feature.Id);
		}
		return highest + 1;
	}

	private static FragmentDetectedFeature FindBestPreviousFeature(
		List<FragmentDetectedFeature> previous,
		List<int> matchedIds,
		FragmentDetectedFeature candidate)
	{
		FragmentDetectedFeature best = null;
		float bestDistanceSquared = 0.0016f;
		Vector2 candidateCenter = GetFeatureCenter(candidate);
		foreach (FragmentDetectedFeature feature in previous)
		{
			if (matchedIds.Contains(feature.Id)) continue;
			float distanceSquared = GetFeatureCenter(feature).DistanceSquaredTo(candidateCenter);
			if (distanceSquared >= bestDistanceSquared) continue;
			float previousLength = GetFeatureLength(feature);
			float candidateLength = GetFeatureLength(candidate);
			float shorter = MathF.Min(previousLength, candidateLength);
			float longer = MathF.Max(previousLength, candidateLength);
			if (shorter <= 0.0001f || longer / shorter > 2.5f) continue;
			Vector2 previousAxis = GetFeatureAxis(feature);
			Vector2 candidateAxis = GetFeatureAxis(candidate);
			if (previousAxis.LengthSquared() > 0.0001f &&
				candidateAxis.LengthSquared() > 0.0001f &&
				MathF.Abs(previousAxis.Dot(candidateAxis)) < 0.7f)
				continue;
			best = feature;
			bestDistanceSquared = distanceSquared;
		}
		return best;
	}

	private static float GetFeatureLength(FragmentDetectedFeature feature)
	{
		if (feature.Segments == null || feature.Segments.Count == 0)
			return feature.Start.DistanceTo(feature.End);
		float length = 0f;
		foreach (FragmentFeatureSegment segment in feature.Segments)
			length += segment.Start.DistanceTo(segment.End);
		return length;
	}

	private static Vector2 GetFeatureAxis(FragmentDetectedFeature feature)
	{
		Vector2 axis = feature.End - feature.Start;
		float longestSquared = axis.LengthSquared();
		if (feature.Segments != null)
			foreach (FragmentFeatureSegment segment in feature.Segments)
			{
				Vector2 candidate = segment.End - segment.Start;
				if (candidate.LengthSquared() <= longestSquared) continue;
				axis = candidate;
				longestSquared = candidate.LengthSquared();
			}
		return longestSquared > 0.0001f ? axis.Normalized() : Vector2.Zero;
	}

	private static Vector2 GetFeatureCenter(FragmentDetectedFeature feature)
	{
		if (feature.Segments.Count == 0) return (feature.Start + feature.End) * 0.5f;
		Vector2 sum = Vector2.Zero;
		foreach (FragmentFeatureSegment segment in feature.Segments)
			sum += (segment.Start + segment.End) * 0.5f;
		return sum / feature.Segments.Count;
	}

	private int? FindFirstProposedFeatureId()
	{
		int? priority = State?.DetectedFeatures.Find(feature =>
			feature.Provenance == FragmentAnnotationProvenance.Rover &&
			feature.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsFeatureInActiveReviewRegions(feature) &&
			IsFeatureInRegions(feature, featureReviewPriorityRegionIds))?.Id;
		if (priority.HasValue) return priority;
		if (IsAutonomousRegionFeatureScopeActive)
			return null;
		return State?.DetectedFeatures.Find(feature =>
			feature.Provenance == FragmentAnnotationProvenance.Rover &&
			feature.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsFeatureInActiveReviewRegions(feature))?.Id;
	}

	private int? FindNextProposedFeatureId(int afterFeatureId)
	{
		if (State == null) return null;
		int? priority = FindNextMatching(feature =>
			IsFeatureInRegions(feature, featureReviewPriorityRegionIds));
		if (priority.HasValue) return priority;
		if (IsAutonomousRegionFeatureScopeActive)
			return null;
		return FindNextMatching(_ => true);

		int? FindNextMatching(Func<FragmentDetectedFeature, bool> additionalPredicate)
		{
		int start = State.DetectedFeatures.FindIndex(feature => feature.Id == afterFeatureId);
		for (int offset = 1; offset <= State.DetectedFeatures.Count; offset++)
		{
			FragmentDetectedFeature feature = State.DetectedFeatures[
				(start + offset + State.DetectedFeatures.Count) % State.DetectedFeatures.Count];
			if (feature.Provenance == FragmentAnnotationProvenance.Rover &&
				feature.Disposition == FragmentAnnotationDisposition.Proposed &&
				IsFeatureInActiveReviewRegions(feature) &&
				additionalPredicate(feature))
			{
				return feature.Id;
			}
		}
		return null;
		}
	}

	public void SetFeatureReviewPriority(IReadOnlyList<int> displayedRegionIds)
	{
		featureReviewPriorityRegionIds.Clear();
		if ((autonomousWorkflowStage is
			FragmentAutonomousWorkflowStage.SearchingRegionFeatures or
			FragmentAutonomousWorkflowStage.AwaitingFeatureReview) &&
			IsRetainedRegion(autonomousTargetRegionId))
		{
			featureReviewPriorityRegionIds.Add(autonomousTargetRegionId);
		}
		else if (displayedRegionIds != null)
			foreach (int id in displayedRegionIds)
				if (!featureReviewPriorityRegionIds.Contains(id))
					featureReviewPriorityRegionIds.Add(id);
		if (featureReviewPriorityRegionIds.Count == 0) return;
		FragmentDetectedFeature selected = State?.SelectedFeatureId is int selectedId
			? State.DetectedFeatures.Find(feature => feature.Id == selectedId)
			: null;
		if (selected != null && IsFeatureInRegions(selected, featureReviewPriorityRegionIds)) return;
		int? nextFeatureId = FindFirstProposedFeatureId();
		if (State?.SelectedFeatureId == nextFeatureId) return;
		State.SelectedFeatureId = nextFeatureId;
		AlignSelectedRegionToFeature(State.SelectedFeatureId);
		FeaturesChanged?.Invoke();
	}

	public bool CanEditFeatureOnCurrentReviewPage(int featureId)
	{
		PruneFeatureReviewPriority();
		if (featureReviewPriorityRegionIds.Count == 0) return true;
		FragmentDetectedFeature feature = State?.DetectedFeatures.Find(candidate =>
			candidate.Id == featureId);
		return feature != null && IsFeatureInRegions(feature, featureReviewPriorityRegionIds);
	}

	public void SetStructureReviewPriority(IReadOnlyList<int> displayedRegionIds)
	{
		structureReviewPriorityRegionIds.Clear();
		if (displayedRegionIds != null)
			foreach (int id in displayedRegionIds)
				if (!structureReviewPriorityRegionIds.Contains(id) && IsRetainedRegion(id))
					structureReviewPriorityRegionIds.Add(id);
		if (structureReviewPriorityRegionIds.Count == 0) return;

		FragmentDetectedStructure selected = State?.SelectedStructureId is int selectedId
			? State.DetectedStructures.Find(structure => structure.Id == selectedId)
			: null;
		if (selected != null &&
			IsStructureInRegions(selected, structureReviewPriorityRegionIds)) return;

		FragmentDetectedStructure next = State?.DetectedStructures.Find(structure =>
			structure.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsStructureInRegions(structure, structureReviewPriorityRegionIds));
		next ??= State?.DetectedStructures.Find(structure =>
			structure.Disposition == FragmentAnnotationDisposition.Accepted &&
			IsStructureInRegions(structure, structureReviewPriorityRegionIds));
		int? nextId = next?.Id;
		if (State?.SelectedStructureId == nextId) return;
		InvalidateOrientationHypotheses();
		State.SelectedStructureId = nextId;
		if (next != null &&
			FindVisibleRegionForStructure(next, structureReviewPriorityRegionIds) is int regionId)
			State.SelectedRegionId = regionId;
		StructuresChanged?.Invoke();
	}

	public bool CanEditStructureOnCurrentReviewPage(int structureId)
	{
		PruneStructureReviewPriority();
		if (structureReviewPriorityRegionIds.Count == 0) return true;
		FragmentDetectedStructure structure = State?.DetectedStructures.Find(candidate =>
			candidate.Id == structureId);
		return structure != null &&
			IsStructureInRegions(structure, structureReviewPriorityRegionIds);
	}

	private void PruneFeatureReviewPriority()
	{
		if (State == null)
		{
			featureReviewPriorityRegionIds.Clear();
			return;
		}
		featureReviewPriorityRegionIds.RemoveAll(regionId => !IsRetainedRegion(regionId));
	}

	private void PruneStructureReviewPriority()
	{
		if (State == null)
		{
			structureReviewPriorityRegionIds.Clear();
			return;
		}
		structureReviewPriorityRegionIds.RemoveAll(regionId => !IsRetainedRegion(regionId));
	}

	private bool IsRetainedRegion(int regionId) => State?.CandidateRegions.Exists(region =>
		region.Id == regionId &&
		region.Disposition != FragmentAnnotationDisposition.Dismissed) == true;

	private bool IsFeatureInRegions(
		FragmentDetectedFeature feature,
		IReadOnlyList<int> regionIds)
	{
		if (regionIds == null || regionIds.Count == 0) return false;
		Vector2 center = GetFeatureCenter(feature);
		return State.CandidateRegions.Exists(region =>
			ContainsId(regionIds, region.Id) &&
			region.Disposition != FragmentAnnotationDisposition.Dismissed &&
			(region.FeatureIds.Contains(feature.Id) || region.NormalizedBounds.HasPoint(center)));
	}

	private bool IsStructureInRegions(
		FragmentDetectedStructure structure,
		IReadOnlyList<int> regionIds) =>
		FindVisibleRegionForStructure(structure, regionIds).HasValue;

	private int? FindVisibleRegionForStructure(
		FragmentDetectedStructure structure,
		IReadOnlyList<int> regionIds)
	{
		if (State == null || structure == null || regionIds == null || regionIds.Count == 0 ||
			structure.Disposition == FragmentAnnotationDisposition.Dismissed) return null;
		foreach (int regionId in regionIds)
		{
			FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
				candidate.Id == regionId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
			if (region == null) continue;
			FragmentLockedRegionView locked = State.LockedRegionViews.Find(view =>
				view.RegionId == region.Id);
			Rect2 bounds = locked?.NormalizedBounds ?? region.NormalizedBounds;
			IReadOnlyList<FragmentDetectedFeature> renderedFeatures =
				locked?.Features ?? State.DetectedFeatures;
			foreach (int featureId in structure.FeatureIds)
			{
				FragmentDetectedFeature feature = null;
				for (int index = 0; index < renderedFeatures.Count; index++)
					if (renderedFeatures[index].Id == featureId &&
						renderedFeatures[index].Disposition != FragmentAnnotationDisposition.Dismissed)
					{
						feature = renderedFeatures[index];
						break;
					}
				if (feature != null && DoesFeatureIntersectRegion(feature, bounds)) return region.Id;
			}
		}
		return null;
	}

	private static bool DoesFeatureIntersectRegion(
		FragmentDetectedFeature feature,
		Rect2 bounds)
	{
		if (feature.Segments == null || feature.Segments.Count == 0)
			return DoesSegmentIntersectRegion(feature.Start, feature.End, bounds);
		foreach (FragmentFeatureSegment segment in feature.Segments)
			if (DoesSegmentIntersectRegion(segment.Start, segment.End, bounds)) return true;
		return false;
	}

	private static bool DoesSegmentIntersectRegion(Vector2 start, Vector2 end, Rect2 bounds)
	{
		Vector2 delta = end - start;
		float minimum = 0f;
		float maximum = 1f;
		return ClipStructureSegment(-delta.X, start.X - bounds.Position.X, ref minimum, ref maximum) &&
			ClipStructureSegment(delta.X, bounds.End.X - start.X, ref minimum, ref maximum) &&
			ClipStructureSegment(-delta.Y, start.Y - bounds.Position.Y, ref minimum, ref maximum) &&
			ClipStructureSegment(delta.Y, bounds.End.Y - start.Y, ref minimum, ref maximum);
	}

	private static bool ClipStructureSegment(
		float direction,
		float distance,
		ref float minimum,
		ref float maximum)
	{
		if (Mathf.IsZeroApprox(direction)) return distance >= 0f;
		float ratio = distance / direction;
		if (direction < 0f)
		{
			if (ratio > maximum) return false;
			if (ratio > minimum) minimum = ratio;
		}
		else
		{
			if (ratio < minimum) return false;
			if (ratio < maximum) maximum = ratio;
		}
		return true;
	}

	private bool IsFeatureInActiveReviewRegions(FragmentDetectedFeature feature)
	{
		if (!isAcceptedRegionFeatureReviewActive) return true;
		if (featureReviewRegionIds.Count == 0) return false;
		Vector2 center = GetFeatureCenter(feature);
		return State.CandidateRegions.Exists(region =>
			featureReviewRegionIds.Contains(region.Id) &&
			region.Disposition == FragmentAnnotationDisposition.Accepted &&
			(region.FeatureIds.Contains(feature.Id) || region.NormalizedBounds.HasPoint(center)));
	}

	private void RequestSelectedFeatureFocus()
	{
		if (State?.SelectedFeatureId is int featureId)
			FeatureFocusRequested?.Invoke(featureId);
	}

	private void ScheduleSignalMeasurement()
	{
		float minimumDelay = MathF.Max(settings?.MinimumAutonomousStepDelaySeconds ?? 0.05f, 0.01f);
		measurementDelayRemaining = MathF.Max(
			settings?.MeasurementDebounceSeconds ?? 0.12f,
			minimumDelay);
	}

	public void RefreshSignalMetrics(bool force = false)
	{
		if (isRefreshingSignalMetrics) return;
		isRefreshingSignalMetrics = true;
		try
		{
			RefreshSignalMetricsCore(force);
		}
		finally
		{
			isRefreshingSignalMetrics = false;
		}
	}

	private void RefreshSignalMetricsCore(bool force)
	{
		if (State == null || observationSource == null ||
			GetEffectiveMode(FragmentAutonomyCapability.SenseProcessingChanges) == FragmentAutonomyMode.Off)
		{
			if (MeasurementReport != null)
			{
				MeasurementReport = null;
				MetricsChanged?.Invoke(null);
			}
			if (!string.IsNullOrEmpty(pendingProcessingAction))
			{
				string action = pendingProcessingAction;
				pendingProcessingAction = null;
				pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
				RecordAction(action);
			}
			return;
		}

		FragmentObservableScan scan = observationSource.CaptureObservableScan();
		if (scan == null) return;
		FragmentCandidateRegion targetRegion = State.SelectedRegionId is int regionId
			? State.CandidateRegions.Find(region =>
				region.Id == regionId && region.Disposition != FragmentAnnotationDisposition.Dismissed)
			: null;
		int? targetRegionId = targetRegion?.Id;
		Rect2? targetBounds = targetRegion?.NormalizedBounds;
		bool searchTargetChanged = targetRegionId != lastMeasurementTargetRegionId ||
			!Nullable.Equals(targetBounds, lastMeasurementTargetBounds);
		if (!force && scan.Revision == lastMeasurementRevision &&
			targetRegionId == lastMeasurementTargetRegionId &&
			Nullable.Equals(targetBounds, lastMeasurementTargetBounds)) return;

		FragmentSignalMeasurementReport previous = MeasurementReport;
		IReadOnlyList<int> targetFeatureIds = targetRegion != null
			? targetRegion.FeatureIds
			: Array.Empty<int>();
		FragmentSignalMetrics target = targetBounds.HasValue
			? FragmentSignalMeasurer.Measure(
				scan,
				targetBounds.Value,
				State.DetectedFeatures,
				targetFeatureIds,
				settings?.MaximumMeasurementComparisons ?? 500000)
			: null;
		MeasurementReport = new FragmentSignalMeasurementReport
		{
			Revision = scan.Revision,
			TargetRegionId = targetRegionId,
			Target = target,
			PreviousTarget = previous != null && previous.TargetRegionId == targetRegionId
				? previous.Target
				: null
		};
		lastMeasurementRevision = scan.Revision;
		lastMeasurementTargetRegionId = targetRegionId;
		lastMeasurementTargetBounds = targetBounds;
		if (target?.IsComplete == false)
		{
			pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
			pendingProcessingAction = null;
			MetricsChanged?.Invoke(MeasurementReport);
			PauseProcessingSearchForSafety(
				$"S/N measurement exceeded {target.ComparisonCount:N0} comparisons");
			return;
		}
		RecordProcessingMeasurement(pendingMeasurementOrigin);
		pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
		MetricsChanged?.Invoke(MeasurementReport);
		if (!string.IsNullOrEmpty(pendingProcessingAction))
		{
			string action = pendingProcessingAction;
			pendingProcessingAction = null;
			RecordAction(action);
		}
		if (awaitingSearchMeasurement)
		{
			awaitingSearchMeasurement = false;
			processingActionWatchdogRemaining = -1f;
			if (State.IsProcessingSearchActive && !State.IsPaused)
				PlanNextProcessingAdjustment();
		}
		else if (searchTargetChanged && State.IsProcessingSearchActive && !State.IsPaused)
		{
			PlanNextProcessingAdjustment();
		}
	}

	public void StartAutonomousWorkflow()
	{
		if (State == null || commandSink == null) return;
		if (State.GlobalMode != FragmentAutonomyMode.Performer)
		{
			PublishAutonomousStatus(
				"Autonomous workflow is not allocated",
				"Select PERFORMER, then press PLAY ROVER",
				"Current fragment",
				"No autonomous search started",
				FragmentRoverActivity.WaitingForPlayer);
			return;
		}
		if (IsAutonomousWorkflowActive)
		{
			if (State.IsPaused) SetPaused(false);
			return;
		}

		StopProcessingSearch();
		ResetAutonomousWorkflowTransient();
		autonomousExcludedRegionBounds.Clear();
		State.IsPaused = false;
		State.CandidateRegions.RemoveAll(region =>
			region.Provenance == FragmentAnnotationProvenance.Rover &&
			region.Disposition == FragmentAnnotationDisposition.Proposed);
		if (State.SelectedRegionId is int selectedRegionId &&
			!State.CandidateRegions.Exists(region => region.Id == selectedRegionId))
			State.SelectedRegionId = null;
		autonomousWorkflowStage = FragmentAutonomousWorkflowStage.SearchingRegions;
		BuildAutonomousRegionConfigurations();
		RecordAction("AUTONOMOUS WORKFLOW: START");
		RegionsChanged?.Invoke();
		AutonomousWorkflowChanged?.Invoke(autonomousWorkflowStage);
		AllocationChanged?.Invoke();
		ApplyNextAutonomousRegionConfiguration();
	}

	public bool TryStartAutonomousFeatureSearch()
	{
		if (State == null || commandSink == null ||
			State.GlobalMode != FragmentAutonomyMode.Performer) return false;
		if (State.CandidateRegions.Exists(region =>
			region.Disposition == FragmentAnnotationDisposition.Proposed))
		{
			PublishAutonomousStatus(
				"Region review is not complete",
				"Accept or dismiss every proposed Region before scanning Features",
				"Regions of interest",
				"Feature search is waiting",
				FragmentRoverActivity.WaitingForPlayer);
			return true;
		}
		StopProcessingSearch();
		autonomousRegionBestConfigurations.Clear();
		BeginAutonomousFeatureSearch();
		return true;
	}

	public void FindAnotherAutonomousRegionSet()
	{
		if (State == null || commandSink == null ||
			State.GlobalMode != FragmentAutonomyMode.Performer) return;
		autonomousExcludedRegionBounds.Clear();
		foreach (FragmentCandidateRegion region in State.CandidateRegions)
		{
			if (region.Disposition != FragmentAnnotationDisposition.Dismissed)
			{
				autonomousExcludedRegionBounds.Add(region.NormalizedBounds);
				region.Disposition = FragmentAnnotationDisposition.Dismissed;
			}
		}
		State.LockedRegionViews.Clear();
		State.SelectedRegionId = null;
		State.ActiveCropRegionId = null;
		StopProcessingSearch();
		ResetAutonomousWorkflowTransient();
		State.IsPaused = false;
		autonomousWorkflowStage = FragmentAutonomousWorkflowStage.SearchingRegions;
		BuildAutonomousRegionConfigurations();
		RecordAction("AUTONOMOUS WORKFLOW: FIND ANOTHER REGION SET");
		RegionsChanged?.Invoke();
		AutonomousWorkflowChanged?.Invoke(autonomousWorkflowStage);
		AllocationChanged?.Invoke();
		ApplyNextAutonomousRegionConfiguration();
	}

	public void StopAutonomousWorkflow(bool recordHistory = true)
	{
		if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.Inactive) return;
		ResetAutonomousWorkflowTransient();
		if (State != null) State.IsPaused = false;
		if (recordHistory) RecordAction("AUTONOMOUS WORKFLOW: STOP");
		RefreshIdleStatus("Autonomous workflow stopped");
		AutonomousWorkflowChanged?.Invoke(autonomousWorkflowStage);
		AllocationChanged?.Invoke();
	}

	public void ContinueAutonomousWorkflow()
	{
		if (State == null || State.GlobalMode != FragmentAutonomyMode.Performer) return;
		if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingRegionChoice)
		{
			FragmentCandidateRegion chosen = State.SelectedRegionId is int regionId
				? State.CandidateRegions.Find(region =>
					region.Id == regionId &&
					region.Disposition == FragmentAnnotationDisposition.Accepted)
				: null;
			if (chosen == null)
			{
				PublishAutonomousStatus(
					"Select one accepted region",
					"Choose a Region in REGIONS OF INTEREST, then confirm",
					"No semantic region selected",
					"Player decision required",
					FragmentRoverActivity.WaitingForPlayer);
				return;
			}

			autonomousTargetRegionId = chosen.Id;
			if (autonomousRegionBestConfigurations.TryGetValue(chosen.Id, out var best))
				ApplyAutonomousConfiguration(best);
			State.LockedRegionViews.RemoveAll(view => view.RegionId == chosen.Id);
			State.SelectedRegionId = chosen.Id;
			State.ActiveCropRegionId = chosen.Id;
			featureReviewPriorityRegionIds.Clear();
			structureReviewPriorityRegionIds.Clear();
			RefreshStructures(true);
			FragmentDetectedStructure structure = FindBestStructureForRegion(chosen);
			State.SelectedStructureId = structure?.Id;
			SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingStructureReview);
			PublishAutonomousStatus(
				$"Reconstructed structure for R{chosen.Id}",
				"Edit if needed, then select VALIDATE & CONTINUE",
				$"Region {chosen.Id}",
				structure == null
					? "No non-empty structure; draw/edit one before continuing"
					: $"Selected S{structure.Id} with {structure.FeatureIds.Count} features",
				FragmentRoverActivity.WaitingForPlayer);
			StructuresChanged?.Invoke();
			return;
		}

		if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.AwaitingStructureReview)
		{
			FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
				candidate.Id == autonomousTargetRegionId &&
				candidate.Disposition == FragmentAnnotationDisposition.Accepted);
			FragmentDetectedStructure structure = State.SelectedStructureId is int structureId
				? State.DetectedStructures.Find(candidate =>
					candidate.Id == structureId &&
					candidate.Disposition != FragmentAnnotationDisposition.Dismissed &&
					candidate.FeatureIds.Count > 0)
				: null;
			if (region == null || structure == null || !StructureTouchesRegion(structure, region))
			{
				PublishAutonomousStatus(
					"A non-empty structure in the chosen region is required",
					"Use EDIT STRUCTURE, then validate again",
					region == null ? "Chosen region unavailable" : $"Region {region.Id}",
					"Structure validation not complete",
					FragmentRoverActivity.WaitingForPlayer);
				return;
			}

			if (structure.Disposition != FragmentAnnotationDisposition.Accepted)
				ApplyStructureEdit(FragmentStructureEditAction.Accept, structure.Id);
			EstimateOrientationHypotheses(true);
			SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingOrientationReview);
			PublishAutonomousStatus(
				$"Estimated Rotation alternatives for R{region.Id}",
				"Compare each possible Rotation with the scanned fragment and accept one",
				$"Region {region.Id} · Structure S{structure.Id}",
				$"{State.OrientationHypotheses.Count} observable Rotation alternatives",
				FragmentRoverActivity.WaitingForPlayer);
		}
	}

	private void ProcessAutonomousWorkflow(float delta)
	{
		if (State == null || State.IsPaused ||
			State.GlobalMode != FragmentAutonomyMode.Performer) return;
		if (autonomousWorkflowStage != FragmentAutonomousWorkflowStage.SearchingRegions &&
			autonomousWorkflowStage != FragmentAutonomousWorkflowStage.SearchingRegionFeatures)
			return;
		if (autonomousStepRemaining < 0f) return;
		autonomousStepRemaining -= MathF.Max(delta, 0f);
		int catchUpSteps = 0;
		while (autonomousStepRemaining <= 0f && catchUpSteps++ < 8 &&
			autonomousWorkflowStage is FragmentAutonomousWorkflowStage.SearchingRegions or
				FragmentAutonomousWorkflowStage.SearchingRegionFeatures)
		{
			float overdue = -autonomousStepRemaining;
			if (autonomousWorkflowStage == FragmentAutonomousWorkflowStage.SearchingRegions)
				EvaluateAutonomousRegionConfiguration();
			else
				EvaluateAutonomousFeatureConfiguration();
			if (autonomousStepRemaining >= 0f) autonomousStepRemaining -= overdue;
		}
	}

	private void BuildAutonomousRegionConfigurations()
	{
		autonomousConfigurations.Clear();
		autonomousConfigurationIndex = 0;
		autonomousTestsCompleted = 0;
		autonomousBestScore = float.NegativeInfinity;
		autonomousBestDenseRegionCount = 0;
		autonomousBestConfiguration = null;
		FragmentAnalysisControlState current = commandSink.CaptureControlState();
		List<int> searchableBits = new();
		if (!IsProcessingParameterLocked(FragmentAnalysisParameter.PolarizationEnabled))
			searchableBits.Add(0);
		if (!IsProcessingParameterLocked(FragmentAnalysisParameter.SpectralEnabled))
			searchableBits.Add(1);
		if (!IsProcessingParameterLocked(FragmentAnalysisParameter.SurfaceEnabled))
			searchableBits.Add(2);
		if (!IsProcessingParameterLocked(FragmentAnalysisParameter.ElectromagneticEnabled))
			searchableBits.Add(3);
		if (!IsProcessingParameterLocked(FragmentAnalysisParameter.ResonanceEnabled))
			searchableBits.Add(4);
		if (!IsProcessingParameterLocked(FragmentAnalysisParameter.XRayEnabled))
			searchableBits.Add(5);
		autonomousReachableRegionConfigurationCount = 1 << searchableBits.Count;

		int startMask = GetConfigurationMask(current);
		Queue<int> frontier = new();
		HashSet<int> visitedMasks = new() { startMask };
		HashSet<string> configurationKeys = new();
		frontier.Enqueue(startMask);
		int maximum = Math.Clamp(settings?.MaximumRegionSearchTests ?? 64, 1, 64);
		while (frontier.Count > 0 && autonomousConfigurations.Count < maximum)
		{
			int mask = frontier.Dequeue();
			FragmentAnalysisControlState controls = CreateConfiguration(
				current, mask, current.PolarizationLevel,
				current.SpectralLevel, current.SurfaceLevel);
			string key = GetAutonomousConfigurationKey(controls);
			if (configurationKeys.Add(key))
				autonomousConfigurations.Add(new AutonomousConfigurationCandidate
				{
					Controls = controls,
					Key = key
				});
			foreach (int bit in searchableBits)
			{
				int neighbor = mask ^ (1 << bit);
				if (visitedMasks.Add(neighbor)) frontier.Enqueue(neighbor);
			}
		}
	}

	private void ApplyNextAutonomousRegionConfiguration()
	{
		if (autonomousConfigurationIndex >= autonomousConfigurations.Count)
		{
			FinishAutonomousRegionSearch();
			return;
		}
		AutonomousConfigurationCandidate candidate =
			autonomousConfigurations[autonomousConfigurationIndex++];
		ApplyAutonomousConfiguration(candidate.Controls);
		float budget = MathF.Max(settings?.RegionSearchBudgetSeconds ?? 5f, 0.1f);
		autonomousStepRemaining = budget / Math.Max(autonomousConfigurations.Count, 1);
		PublishAutonomousStatus(
			$"BFS ON/OFF configuration {autonomousConfigurationIndex}/" +
				$"{autonomousConfigurations.Count}",
			$"Score distinct compact dense regions (up to " +
				$"{Math.Clamp(settings?.MaximumScoredDenseRegionCount ?? 5, 1, 5)})",
			"Whole fragment",
			$"Best: {autonomousBestDenseRegionCount} dense regions · " +
				$"{autonomousReachableRegionConfigurationCount}/64 reachable combinations",
			FragmentRoverActivity.Executing);
	}

	private void EvaluateAutonomousRegionConfiguration()
	{
		IReadOnlyList<FragmentCandidateRegion> regions =
			FragmentRegionDetector.GroupCandidateRegions(State.DetectedFeatures);
		(float score, int denseCount) = ScoreAutonomousRegions(regions);
		autonomousTestsCompleted++;
		if (score > autonomousBestScore)
		{
			autonomousBestScore = score;
			autonomousBestDenseRegionCount = denseCount;
			autonomousBestConfiguration = CloneControlState(autonomousAppliedConfiguration);
		}
		if (autonomousTestsCompleted >= autonomousConfigurations.Count)
		{
			FinishAutonomousRegionSearch();
			return;
		}
		ApplyNextAutonomousRegionConfiguration();
	}

	private void FinishAutonomousRegionSearch()
	{
		if (autonomousBestConfiguration != null)
			ApplyAutonomousConfiguration(autonomousBestConfiguration);
		RefreshCandidateRegions(true, false);
		PruneAutonomousRegionProposals();
		autonomousStepRemaining = -1f;
		SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingRegionReview);
		int proposed = State.CandidateRegions.FindAll(region =>
			region.Disposition == FragmentAnnotationDisposition.Proposed).Count;
		PublishAutonomousStatus(
			$"Region search paused with {proposed} proposals",
			"Accept, dismiss, resize, or draw regions until no proposal remains",
			"Dense observable feature clusters",
			$"Best result after {autonomousTestsCompleted} tests: " +
				$"{autonomousBestDenseRegionCount} dense regions",
			FragmentRoverActivity.WaitingForPlayer);
		RecordAction($"AUTONOMOUS REGION SEARCH: {proposed} proposals after " +
			$"{autonomousTestsCompleted} BFS tests");
		RegionsChanged?.Invoke();
		RefreshSignalMetrics(true);
	}

	private (float Score, int DenseCount) ScoreAutonomousRegions(
		IReadOnlyList<FragmentCandidateRegion> regions)
	{
		if (regions == null) return (0f, 0);
		List<(FragmentCandidateRegion Region, float Quality)> denseRegions = new();
		foreach (FragmentCandidateRegion region in regions)
		{
			if (IsExcludedAutonomousRegion(region.NormalizedBounds)) continue;
			int count = Math.Max(region.FeatureIds?.Count ?? 0, 0);
			float area = MathF.Max(region.NormalizedBounds.Size.X *
				region.NormalizedBounds.Size.Y, 0.0025f);
			float density = count / area;
			bool isDense = count >= Math.Max(settings?.MinimumDenseRegionFeatureCount ?? 3, 1) &&
				density >= MathF.Max(settings?.MinimumDenseRegionDensity ?? 18f, 1f);
			if (!isDense) continue;
			denseRegions.Add((region,
				MathF.Min(density, 1000f) + count * 12f - area * 20f));
		}
		denseRegions.Sort((first, second) => second.Quality.CompareTo(first.Quality));
		int dense = Math.Min(denseRegions.Count,
			Math.Clamp(settings?.MaximumScoredDenseRegionCount ?? 5, 1, 5));
		float score = dense * 100000f;
		for (int index = 0; index < dense; index++)
		{
			score += denseRegions[index].Quality;
			for (int other = 0; other < index; other++)
				score += denseRegions[index].Region.NormalizedBounds.GetCenter().DistanceTo(
					denseRegions[other].Region.NormalizedBounds.GetCenter()) * 100f;
		}
		return (score, dense);
	}

	private bool IsExcludedAutonomousRegion(Rect2 bounds)
	{
		Vector2 center = bounds.GetCenter();
		foreach (Rect2 excluded in autonomousExcludedRegionBounds)
		{
			if (excluded.HasPoint(center)) return true;
			Rect2 overlap = excluded.Intersection(bounds);
			float overlapArea = MathF.Max(overlap.Size.X, 0f) * MathF.Max(overlap.Size.Y, 0f);
			float smallerArea = MathF.Min(
				MathF.Max(excluded.Size.X * excluded.Size.Y, 0.0001f),
				MathF.Max(bounds.Size.X * bounds.Size.Y, 0.0001f));
			if (overlapArea / smallerArea >= 0.5f) return true;
		}
		return false;
	}

	private void PruneAutonomousRegionProposals()
	{
		List<(FragmentCandidateRegion Region, float Density)> ranked = new();
		foreach (FragmentCandidateRegion region in State.CandidateRegions)
		{
			if (region.Provenance != FragmentAnnotationProvenance.Rover ||
				region.Disposition != FragmentAnnotationDisposition.Proposed) continue;
			float area = MathF.Max(region.NormalizedBounds.Size.X *
				region.NormalizedBounds.Size.Y, 0.0025f);
			ranked.Add((region, region.FeatureIds.Count / area));
		}
		if (ranked.Count == 0) return;
		ranked.Sort((first, second) => second.Density.CompareTo(first.Density));
		float threshold = MathF.Max(
			settings?.MinimumDenseRegionDensity ?? 18f,
			ranked[0].Density * 0.5f);
		int maximumKeep = Math.Min(
			Math.Clamp(settings?.MaximumScoredDenseRegionCount ?? 5, 1, 5), ranked.Count);
		HashSet<int> retained = new();
		for (int index = 0; index < maximumKeep; index++)
			if (ranked[index].Region.FeatureIds.Count >=
					Math.Max(settings?.MinimumDenseRegionFeatureCount ?? 3, 1) &&
				ranked[index].Density >= threshold)
				retained.Add(ranked[index].Region.Id);
		if (retained.Count == 0) retained.Add(ranked[0].Region.Id);
		State.CandidateRegions.RemoveAll(region =>
			region.Provenance == FragmentAnnotationProvenance.Rover &&
			region.Disposition == FragmentAnnotationDisposition.Proposed &&
			!retained.Contains(region.Id));
		State.SelectedRegionId = State.CandidateRegions.Find(region =>
			region.Disposition == FragmentAnnotationDisposition.Proposed)?.Id;
	}

	private void BeginAutonomousFeatureSearch()
	{
		// Region acceptance is a human-gated action and may leave the shared pause flag set by a
		// preceding player override. The next autonomous phase is an explicit resume boundary.
		State.IsPaused = false;
		autonomousRegionIds.Clear();
		foreach (FragmentCandidateRegion region in State.CandidateRegions)
			if (region.Disposition == FragmentAnnotationDisposition.Accepted)
				autonomousRegionIds.Add(region.Id);
		autonomousRegionIds.Sort();
		if (autonomousRegionIds.Count == 0)
		{
			SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingRegionReview);
			PublishAutonomousStatus(
				"No accepted regions remain",
				"Draw or restore a region and accept it",
				"Whole fragment",
				"Feature search cannot start",
				FragmentRoverActivity.WaitingForPlayer);
			return;
		}
		autonomousRegionIndex = 0;
		StartAutonomousFeatureSearchForCurrentRegion();
	}

	private void StartAutonomousFeatureSearchForCurrentRegion()
	{
		if (autonomousRegionIndex >= autonomousRegionIds.Count)
		{
			EnterAutonomousRegionChoice();
			return;
		}
		autonomousTargetRegionId = autonomousRegionIds[autonomousRegionIndex];
		State.IsPaused = false;
		State.SelectedRegionId = autonomousTargetRegionId;
		featureReviewPriorityRegionIds.Clear();
		featureReviewPriorityRegionIds.Add(autonomousTargetRegionId);
		BuildAutonomousFeatureConfigurations();
		SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.SearchingRegionFeatures);
		RegionsChanged?.Invoke();
		ApplyNextAutonomousFeatureConfiguration();
	}

	private void BuildAutonomousFeatureConfigurations()
	{
		autonomousConfigurations.Clear();
		autonomousConfigurationIndex = 0;
		autonomousTestsCompleted = 0;
		autonomousBestScore = float.NegativeInfinity;
		autonomousBestFeatureCount = -1;
		autonomousBestConfiguration = null;
		FragmentAnalysisControlState current = commandSink.CaptureControlState();
		int maximum = Math.Clamp(
			settings?.MaximumFeatureSearchTestsPerRegion ?? 144, 1, 400);
		System.Random random = CreateAutonomousRandom(0x7F31 + autonomousTargetRegionId * 97);
		HashSet<string> keys = new();
		AddCandidate(current);
		int attempts = 0;
		while (autonomousConfigurations.Count < maximum && attempts++ < maximum * 20)
		{
			int mask = random.Next(64);
			FragmentAnalysisControlState candidate = CreateConfiguration(
				current,
				mask,
				random.Next(1, 6),
				random.Next(1, 6),
				random.Next(1, 6));
			AddCandidate(candidate);
		}

		void AddCandidate(FragmentAnalysisControlState controls)
		{
			string key = GetAutonomousConfigurationKey(controls);
			if (!keys.Add(key)) return;
			autonomousConfigurations.Add(new AutonomousConfigurationCandidate
			{
				Controls = controls,
				Key = key
			});
		}
	}

	private void ApplyNextAutonomousFeatureConfiguration()
	{
		if (autonomousConfigurationIndex >= autonomousConfigurations.Count)
		{
			FinishAutonomousFeatureSearch();
			return;
		}
		AutonomousConfigurationCandidate candidate =
			autonomousConfigurations[autonomousConfigurationIndex++];
		float budget = MathF.Max(settings?.FeatureSearchBudgetSeconds ?? 5f, 0.1f);
		autonomousStepRemaining = budget / Math.Max(autonomousConfigurations.Count, 1);
		// Arm progress before dispatch: configuration application emits synchronous UI/model events,
		// so no callback can observe a SearchingRegionFeatures stage with an unarmed timer.
		ApplyAutonomousConfiguration(candidate.Controls);
		PublishAutonomousStatus(
			$"Optimizing Features for Region {autonomousTargetRegionId}: " +
				$"{autonomousConfigurationIndex}/{autonomousConfigurations.Count}",
			"Retain the configuration with the most visible regional features",
			$"Region {autonomousTargetRegionId}",
			$"Best visible feature count: {Math.Max(autonomousBestFeatureCount, 0)}",
			FragmentRoverActivity.Executing);
	}

	private void EvaluateAutonomousFeatureConfiguration()
	{
		FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
			candidate.Id == autonomousTargetRegionId &&
			candidate.Disposition == FragmentAnnotationDisposition.Accepted);
		if (region == null)
		{
			EnterAutonomousRegionChoice();
			return;
		}
		int count = CountFeaturesInRegion(region);
		autonomousTestsCompleted++;
		if (count > autonomousBestFeatureCount)
		{
			autonomousBestFeatureCount = count;
			autonomousBestScore = count;
			autonomousBestConfiguration = CloneControlState(autonomousAppliedConfiguration);
		}
		if (autonomousTestsCompleted >= autonomousConfigurations.Count)
			FinishAutonomousFeatureSearch();
		else
			ApplyNextAutonomousFeatureConfiguration();
	}

	private void FinishAutonomousFeatureSearch()
	{
		if (autonomousBestConfiguration != null)
			ApplyAutonomousConfiguration(autonomousBestConfiguration);
		autonomousRegionBestConfigurations[autonomousTargetRegionId] =
			CloneControlState(autonomousBestConfiguration ?? commandSink.CaptureControlState());
		FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
			candidate.Id == autonomousTargetRegionId);
		if (region == null)
		{
			EnterAutonomousRegionChoice();
			return;
		}
		region.FeatureIds.Clear();
		region.FeatureIds.AddRange(FindFeaturesInRegion(region.NormalizedBounds));
		State.SelectedFeatureId = State.DetectedFeatures.Find(feature =>
			feature.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsFeatureInRegion(feature, region))?.Id;
		autonomousStepRemaining = -1f;
		SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingFeatureReview);
		PublishAutonomousStatus(
			$"Feature search for Region {region.Id} is ready for validation",
			State.SelectedFeatureId.HasValue
				? "Accept or dismiss each proposed Feature"
				: "No unresolved Feature remains; advancing",
			$"Region {region.Id}",
			$"Best of {autonomousTestsCompleted} tests: {autonomousBestFeatureCount} features",
			FragmentRoverActivity.WaitingForPlayer);
		FeaturesChanged?.Invoke();
		RegionsChanged?.Invoke();
		if (!State.SelectedFeatureId.HasValue)
			AdvanceAutonomousFeatureReviewIfComplete();
	}

	private void AdvanceAutonomousFeatureReviewIfComplete()
	{
		if (autonomousWorkflowStage != FragmentAutonomousWorkflowStage.AwaitingFeatureReview)
			return;
		FragmentCandidateRegion region = State.CandidateRegions.Find(candidate =>
			candidate.Id == autonomousTargetRegionId);
		if (region == null) return;
		bool unresolved = State.DetectedFeatures.Exists(feature =>
			feature.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsFeatureInRegion(feature, region));
		if (unresolved) return;
		if (!IsRegionViewLocked(region.Id)) ToggleRegionViewLock(region.Id);
		autonomousRegionIndex++;
		StartAutonomousFeatureSearchForCurrentRegion();
	}

	private void EnterAutonomousRegionChoice()
	{
		featureReviewPriorityRegionIds.Clear();
		structureReviewPriorityRegionIds.Clear();
		bool retainPlayerSelection = State.SelectedRegionId is int selectedRegionId &&
			autonomousRegionIds.Contains(selectedRegionId);
		if (!retainPlayerSelection)
			State.SelectedRegionId = autonomousRegionIds.Count > 0
				? autonomousRegionIds[0]
				: null;
		SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.AwaitingRegionChoice);
		PublishAutonomousStatus(
			"All accepted regions have been feature-reviewed",
			"Compare the fragment reference, select one Region, then confirm",
			"Accepted regions",
			$"{autonomousRegionIds.Count} reviewed regions available",
			FragmentRoverActivity.WaitingForPlayer);
		RegionsChanged?.Invoke();
	}

	private void BeginAutonomousArrowReview()
	{
		if (State == null || State.GlobalMode != FragmentAutonomyMode.Performer ||
			autonomousTargetRegionId < 0) return;
		RefreshArrowCandidates(true, autonomousTargetOnly: true);
		bool hasCandidate = State.ArrowCandidates.Exists(candidate =>
			candidate.Disposition == FragmentAnnotationDisposition.Proposed &&
			candidate.RegionId == autonomousTargetRegionId);
		SetAutonomousWorkflowStage(hasCandidate
			? FragmentAutonomousWorkflowStage.AwaitingArrowReview
			: FragmentAutonomousWorkflowStage.AwaitingPlayerArrow);
		PublishAutonomousStatus(
			hasCandidate
				? $"Detected arrow candidates in R{autonomousTargetRegionId}"
				: $"No arrow candidate detected in R{autonomousTargetRegionId}",
			hasCandidate
				? "Accept or reject the proposed Arrow"
				: "Draw one arrow from tail to tip",
			$"Region {autonomousTargetRegionId}",
			hasCandidate ? "Player arrow validation required" : "Player geometry required",
			FragmentRoverActivity.WaitingForPlayer);
	}

	private void CompleteAutonomousWorkflow()
	{
		State.IsAnalysisCompleted = true;
		SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage.Complete);
		PublishAutonomousStatus(
			"Autonomous fragment analysis complete",
			"Review the minimap bearing or edit retained analysis",
			$"Region {autonomousTargetRegionId}",
			State.DirectionInterpretation == null
				? "Arrow accepted; bearing unavailable"
				: "World bearing and minimap ray published",
			FragmentRoverActivity.Completed);
		RecordAction("AUTONOMOUS WORKFLOW: COMPLETE");
	}

	private void ApplyAutonomousConfiguration(FragmentAnalysisControlState configuration)
	{
		if (configuration == null || commandSink == null) return;
		autonomousAppliedConfiguration = CloneControlState(configuration);
		commandSink.DispatchAnalysisConfiguration(
			autonomousAppliedConfiguration,
			FragmentAnalysisActionOrigin.Rover);
	}

	private FragmentAnalysisControlState CreateConfiguration(
		FragmentAnalysisControlState baseline,
		int mask,
		int polarizationLevel,
		int spectralLevel,
		int surfaceLevel)
	{
		bool polarizationLocked = IsProcessingParameterLocked(
			FragmentAnalysisParameter.PolarizationEnabled);
		bool spectralLocked = IsProcessingParameterLocked(FragmentAnalysisParameter.SpectralEnabled);
		bool surfaceLocked = IsProcessingParameterLocked(FragmentAnalysisParameter.SurfaceEnabled);
		bool electromagneticLocked = IsProcessingParameterLocked(
			FragmentAnalysisParameter.ElectromagneticEnabled);
		bool resonanceLocked = IsProcessingParameterLocked(FragmentAnalysisParameter.ResonanceEnabled);
		bool xrayLocked = IsProcessingParameterLocked(FragmentAnalysisParameter.XRayEnabled);
		return new FragmentAnalysisControlState
		{
			PolarizationEnabled = polarizationLocked
				? baseline.PolarizationEnabled : (mask & 1) != 0,
			PolarizationLevel = polarizationLocked
				? baseline.PolarizationLevel : polarizationLevel,
			SpectralEnabled = spectralLocked ? baseline.SpectralEnabled : (mask & 2) != 0,
			SpectralLevel = spectralLocked ? baseline.SpectralLevel : spectralLevel,
			SurfaceEnabled = surfaceLocked ? baseline.SurfaceEnabled : (mask & 4) != 0,
			SurfaceLevel = surfaceLocked ? baseline.SurfaceLevel : surfaceLevel,
			ElectromagneticEnabled = electromagneticLocked
				? baseline.ElectromagneticEnabled : (mask & 8) != 0,
			ResonanceEnabled = resonanceLocked
				? baseline.ResonanceEnabled : (mask & 16) != 0,
			XRayEnabled = xrayLocked ? baseline.XRayEnabled : (mask & 32) != 0,
			RotationDegrees = baseline.RotationDegrees,
			ViewZoom = baseline.ViewZoom,
			ViewPan = baseline.ViewPan
		};
	}

	private System.Random CreateAutonomousRandom(int salt)
	{
		ulong revision = observationSource?.CaptureObservableScan()?.Revision ?? 0;
		return new System.Random(unchecked((int)(revision ^ (uint)salt)));
	}

	private static void Shuffle<T>(List<T> values, System.Random random)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int other = random.Next(index + 1);
			(values[index], values[other]) = (values[other], values[index]);
		}
	}

	private static string GetAutonomousConfigurationKey(FragmentAnalysisControlState controls) =>
		$"{(controls.PolarizationEnabled ? controls.PolarizationLevel : 0)}-" +
		$"{(controls.SpectralEnabled ? controls.SpectralLevel : 0)}-" +
		$"{(controls.SurfaceEnabled ? controls.SurfaceLevel : 0)}-" +
		$"{(controls.ElectromagneticEnabled ? 1 : 0)}" +
		$"{(controls.ResonanceEnabled ? 1 : 0)}{(controls.XRayEnabled ? 1 : 0)}";

	private static int GetConfigurationMask(FragmentAnalysisControlState controls)
	{
		int mask = 0;
		if (controls.PolarizationEnabled) mask |= 1;
		if (controls.SpectralEnabled) mask |= 2;
		if (controls.SurfaceEnabled) mask |= 4;
		if (controls.ElectromagneticEnabled) mask |= 8;
		if (controls.ResonanceEnabled) mask |= 16;
		if (controls.XRayEnabled) mask |= 32;
		return mask;
	}

	private int CountFeaturesInRegion(FragmentCandidateRegion region) =>
		State.DetectedFeatures.FindAll(feature =>
			feature.Disposition != FragmentAnnotationDisposition.Dismissed &&
			IsFeatureInRegion(feature, region)).Count;

	private static bool IsFeatureInRegion(
		FragmentDetectedFeature feature,
		FragmentCandidateRegion region) =>
		region.FeatureIds.Contains(feature.Id) ||
		region.NormalizedBounds.HasPoint(GetFeatureCenter(feature));

	private FragmentDetectedStructure FindBestStructureForRegion(FragmentCandidateRegion region)
	{
		FragmentDetectedStructure best = null;
		int bestCount = 0;
		foreach (FragmentDetectedStructure structure in State.DetectedStructures)
		{
			if (structure.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
			int count = CountStructureFeaturesInRegion(structure, region);
			if (count <= bestCount) continue;
			best = structure;
			bestCount = count;
		}
		return best;
	}

	private bool StructureTouchesRegion(
		FragmentDetectedStructure structure,
		FragmentCandidateRegion region) =>
		CountStructureFeaturesInRegion(structure, region) > 0;

	private int CountStructureFeaturesInRegion(
		FragmentDetectedStructure structure,
		FragmentCandidateRegion region)
	{
		int count = 0;
		foreach (int featureId in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = State.DetectedFeatures.Find(candidate =>
				candidate.Id == featureId &&
				candidate.Disposition != FragmentAnnotationDisposition.Dismissed);
			if (feature != null && IsFeatureInRegion(feature, region)) count++;
		}
		return count;
	}

	private void SetAutonomousWorkflowStage(FragmentAutonomousWorkflowStage stage)
	{
		if (autonomousWorkflowStage == stage) return;
		autonomousWorkflowStage = stage;
		AutonomousWorkflowChanged?.Invoke(stage);
		AllocationChanged?.Invoke();
	}

	private void PublishAutonomousStatus(
		string current,
		string next,
		string target,
		string result,
		FragmentRoverActivity activity)
	{
		status = new FragmentRoverActionStatus
		{
			Activity = activity,
			CurrentAction = current,
			NextAction = next,
			CurrentTarget = target,
			MeasuredResult = result,
			LockedParameters = GetLockedProcessingParameterNames()
		};
		StatusChanged?.Invoke(status);
		AutonomousWorkflowChanged?.Invoke(autonomousWorkflowStage);
	}

	private void ResetAutonomousWorkflowTransient()
	{
		autonomousWorkflowStage = FragmentAutonomousWorkflowStage.Inactive;
		autonomousConfigurations.Clear();
		autonomousRegionIds.Clear();
		autonomousRegionBestConfigurations.Clear();
		autonomousStepRemaining = -1f;
		autonomousConfigurationIndex = 0;
		autonomousTestsCompleted = 0;
		autonomousRegionIndex = 0;
		autonomousTargetRegionId = -1;
		autonomousBestScore = float.NegativeInfinity;
		autonomousBestDenseRegionCount = 0;
		autonomousReachableRegionConfigurationCount = 0;
		autonomousBestFeatureCount = 0;
		autonomousBestConfiguration = null;
		autonomousAppliedConfiguration = null;
	}

	public void StartProcessingSearch()
	{
		if (State == null || commandSink == null) return;
		if (GetEffectiveMode(FragmentAutonomyCapability.DecideProcessingConfiguration) ==
			FragmentAutonomyMode.Off)
		{
			PublishProcessingSearchStatus("Configuration search is not allocated", "Select Support or Perform");
			return;
		}
		if (State.SelectedRegionId == null || MeasurementReport?.Target == null)
		{
			PublishProcessingSearchStatus("Select a measured region before searching", "Select or group a region");
			return;
		}

		State.IsProcessingSearchActive = true;
		State.IsPaused = false;
		continuousSearchSteps = 0;
		processingTransitionCounts.Clear();
		RecordAction("CONFIG SEARCH: START");
		PlanNextProcessingAdjustment();
		AllocationChanged?.Invoke();
	}

	public void StopProcessingSearch()
	{
		if (State == null || !State.IsProcessingSearchActive) return;
		State.IsProcessingSearchActive = false;
		processingTransitionCounts.Clear();
		CancelPendingProcessingAdjustment();
		RefreshIdleStatus("Configuration search stopped");
		RecordAction("CONFIG SEARCH: STOP");
		ProcessingSearchChanged?.Invoke();
	}

	public void ApproveProcessingAdjustment()
	{
		if (pendingProcessingAdjustment == null || State?.IsPaused == true) return;
		ApplyPendingProcessingAdjustment();
	}

	public void SkipProcessingAdjustment()
	{
		if (pendingProcessingAdjustment == null || State == null) return;
		string skipped = $"R{State.SelectedRegionId}:{pendingProcessingAdjustment.ConfigurationKey}";
		if (!string.IsNullOrEmpty(skipped) && !State.RejectedProcessingConfigurations.Contains(skipped))
			State.RejectedProcessingConfigurations.Add(skipped);
		string parameterName = pendingProcessingAdjustment.ParameterName;
		CancelPendingProcessingAdjustment();
		RecordAction($"CONFIG SEARCH: SKIP {parameterName}");
		PlanNextProcessingAdjustment();
	}

	public bool IsProcessingParameterLocked(FragmentAnalysisParameter parameter) =>
		State?.LockedProcessingParameters.Contains(GetLockKey(parameter)) == true;

	public void SetProcessingParameterLocked(FragmentAnalysisParameter parameter, bool locked)
	{
		if (State == null) return;
		FragmentAnalysisParameter key = GetLockKey(parameter);
		bool currentlyLocked = State.LockedProcessingParameters.Contains(key);
		if (currentlyLocked == locked) return;
		if (locked) State.LockedProcessingParameters.Add(key);
		else State.LockedProcessingParameters.Remove(key);
		CancelPendingProcessingAdjustment();
		RecordAction($"CONFIG LOCK: {GetParameterDisplayName(key)} {(locked ? "ON" : "OFF")}");
		if (State.IsProcessingSearchActive && !State.IsPaused) PlanNextProcessingAdjustment();
		else RefreshIdleStatus();
		ProcessingSearchChanged?.Invoke();
	}

	public void SearchBack()
	{
		int index = GetProcessingHistoryIndex();
		if (index <= 0) return;
		PauseSearchForHistoryNavigation();
		RestoreProcessingConfiguration(ProcessingHistory[index - 1].Sequence);
	}

	public void SearchForward()
	{
		int index = GetProcessingHistoryIndex();
		if (index < 0 || index >= ProcessingHistory.Count - 1) return;
		PauseSearchForHistoryNavigation();
		RestoreProcessingConfiguration(ProcessingHistory[index + 1].Sequence);
	}

	private void PauseSearchForHistoryNavigation()
	{
		CancelPendingProcessingAdjustment();
		if (State?.IsProcessingSearchActive == true) State.IsPaused = true;
		AllocationChanged?.Invoke();
		ProcessingSearchChanged?.Invoke();
	}

	private void PlanNextProcessingAdjustment()
	{
		if (isPlanningProcessingAdjustment) return;
		isPlanningProcessingAdjustment = true;
		long started = System.Diagnostics.Stopwatch.GetTimestamp();
		try
		{
			PlanNextProcessingAdjustmentCore();
		}
		finally
		{
			isPlanningProcessingAdjustment = false;
		}
		double elapsedMilliseconds =
			(System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 /
			System.Diagnostics.Stopwatch.Frequency;
		if (State?.IsProcessingSearchActive == true && !State.IsPaused &&
			elapsedMilliseconds > Math.Max(settings?.PlannerTimeBudgetMilliseconds ?? 25, 1))
		{
			PauseProcessingSearchForSafety(
				$"Planner exceeded {settings.PlannerTimeBudgetMilliseconds} ms budget");
		}
	}

	private void PlanNextProcessingAdjustmentCore()
	{
		CancelPendingProcessingAdjustment();
		if (State == null || !State.IsProcessingSearchActive || State.IsPaused || commandSink == null)
			return;
		FragmentAutonomyMode mode = GetEffectiveMode(
			FragmentAutonomyCapability.DecideProcessingConfiguration);
		if (mode == FragmentAutonomyMode.Off) return;
		if (State.SelectedRegionId == null || MeasurementReport?.Target == null)
		{
			PublishProcessingSearchStatus("Configuration search needs a measured region", "Select a region");
			return;
		}

		pendingProcessingAdjustment = FragmentConfigurationSearch.PlanNextAdjustment(
			commandSink.CaptureControlState(),
			ProcessingHistory,
			State.RejectedProcessingConfigurations,
			State.LockedProcessingParameters,
			State.SelectedRegionId,
			settings?.ProcessingEffectThreshold ?? 0.02f);
		if (pendingProcessingAdjustment == null)
		{
			State.IsProcessingSearchActive = false;
			PublishProcessingSearchStatus(
				"Configuration search complete",
				"Unlock a parameter or inspect tested configurations");
			ProcessingSearchChanged?.Invoke();
			return;
		}

		bool performerCanApply = mode == FragmentAutonomyMode.Performer &&
			GetEffectiveMode(FragmentAutonomyCapability.AdjustProcessingParameters) ==
				FragmentAutonomyMode.Performer;
		processingPreviewRemaining = performerCanApply
			? MathF.Max(
				settings?.ActionPreviewSeconds ?? 1f,
				MathF.Max(settings?.MinimumAutonomousStepDelaySeconds ?? 0.05f, 0.01f))
			: -1f;
		status = new FragmentRoverActionStatus
		{
			Activity = performerCanApply
				? FragmentRoverActivity.Planning
				: FragmentRoverActivity.AwaitingApproval,
			CurrentAction = pendingProcessingAdjustment.IsBacktrack
				? "Planning a measured-branch backtrack"
				: "Planning one processing test",
			NextAction = FormatProcessingAdjustment(pendingProcessingAdjustment) +
				(performerCanApply ? $" in {processingPreviewRemaining:0.0}s" : " — press APPLY"),
			CurrentTarget = GetProcessingTargetName(),
			MeasuredResult = $"Current S/N {MeasurementReport.Target.SignalToNoise:0.00}",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		StatusChanged?.Invoke(status);
		ProcessingSearchChanged?.Invoke();
		if (performerCanApply && processingPreviewRemaining <= 0f)
			ApplyPendingProcessingAdjustment();
	}

	private void ApplyPendingProcessingAdjustment()
	{
		if (isApplyingProcessingAdjustment || pendingProcessingAdjustment == null ||
			commandSink == null || State?.IsPaused == true) return;
		int maximumSteps = ContinuousProcessingSearchStepLimit;
		if (continuousSearchSteps >= maximumSteps)
		{
			PauseProcessingSearchForSafety($"Safety rest after {maximumSteps} continuous tests");
			return;
		}

		isApplyingProcessingAdjustment = true;
		try
		{
			FragmentProcessingAdjustment adjustment = pendingProcessingAdjustment;
			if (IsProcessingParameterLocked(adjustment.Parameter))
			{
				CancelPendingProcessingAdjustment();
				PlanNextProcessingAdjustment();
				return;
			}
			string transition =
				$"{FragmentConfigurationSearch.GetConfigurationKey(commandSink.CaptureControlState())}" +
				$">{adjustment.ConfigurationKey}";
			processingTransitionCounts.TryGetValue(transition, out int transitionCount);
			transitionCount++;
			processingTransitionCounts[transition] = transitionCount;
			int maximumRepeats = Math.Max(settings?.MaximumRepeatedSearchTransition ?? 2, 1);
			if (transitionCount > maximumRepeats)
			{
				PauseProcessingSearchForSafety("Repeated configuration transition detected");
				return;
			}
			pendingProcessingAdjustment = null;
			processingPreviewRemaining = -1f;
			awaitingSearchMeasurement = true;
			processingActionWatchdogRemaining = MathF.Max(
				settings?.ProcessingActionTimeoutSeconds ?? 5f,
				0.5f);
			continuousSearchSteps++;
			pendingProcessingAction = $"ROVER TEST: {FormatProcessingAdjustment(adjustment)}";
			commandSink.DispatchAnalysisCommand(adjustment.ToCommand(FragmentAnalysisActionOrigin.Rover));
			ProcessingSearchChanged?.Invoke();
		}
		finally
		{
			isApplyingProcessingAdjustment = false;
		}
	}

	private void CancelPendingProcessingAdjustment()
	{
		pendingProcessingAdjustment = null;
		processingPreviewRemaining = -1f;
		awaitingSearchMeasurement = false;
		processingActionWatchdogRemaining = -1f;
		ProcessingSearchChanged?.Invoke();
	}

	private void PauseProcessingSearchForSafety(string reason)
	{
		if (State == null) return;
		CancelPendingProcessingAdjustment();
		measurementDelayRemaining = -1f;
		State.IsPaused = true;
		status = new FragmentRoverActionStatus
		{
			Activity = FragmentRoverActivity.Paused,
			CurrentAction = $"SAFETY PAUSE: {reason}",
			NextAction = "Review the current result, then press RESUME to continue",
			CurrentTarget = GetProcessingTargetName(),
			MeasuredResult = MeasurementReport?.Target == null
				? "No completed measurement"
				: MeasurementReport.Target.IsComplete
					? $"Last completed S/N {MeasurementReport.Target.SignalToNoise:0.00}"
					: "S/N measurement aborted before completion",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		StatusChanged?.Invoke(status);
		AllocationChanged?.Invoke();
		ProcessingSearchChanged?.Invoke();
	}

	private int GetProcessingHistoryIndex()
	{
		if (ProcessingHistory.Count == 0) return -1;
		if (ActiveProcessingHistorySequence is int sequence)
			for (int index = 0; index < ProcessingHistory.Count; index++)
				if (ProcessingHistory[index].Sequence == sequence) return index;
		return ProcessingHistory.Count - 1;
	}

	private static FragmentAnalysisParameter GetLockKey(FragmentAnalysisParameter parameter) => parameter switch
	{
		FragmentAnalysisParameter.PolarizationLevel => FragmentAnalysisParameter.PolarizationEnabled,
		FragmentAnalysisParameter.SpectralLevel => FragmentAnalysisParameter.SpectralEnabled,
		FragmentAnalysisParameter.SurfaceLevel => FragmentAnalysisParameter.SurfaceEnabled,
		_ => parameter
	};

	private string GetLockedProcessingParameterNames()
	{
		if (State?.LockedProcessingParameters.Count is not > 0) return "None";
		List<string> names = new();
		foreach (FragmentAnalysisParameter parameter in State.LockedProcessingParameters)
			names.Add(GetParameterDisplayName(parameter));
		return string.Join(", ", names);
	}

	private string GetProcessingTargetName() => State?.SelectedRegionId is int regionId
		? $"Region {regionId}"
		: "No region";

	private static string FormatProcessingAdjustment(FragmentProcessingAdjustment adjustment) =>
		adjustment == null
			? "No planned adjustment"
			: $"{adjustment.ParameterName}: {adjustment.PreviousValue} → {adjustment.ProposedValue}";

	private void PublishProcessingSearchStatus(string current, string next)
	{
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = current,
			NextAction = next,
			CurrentTarget = GetProcessingTargetName(),
			MeasuredResult = MeasurementReport?.Target == null
				? "No regional measurement"
				: $"Current S/N {MeasurementReport.Target.SignalToNoise:0.00}",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		StatusChanged?.Invoke(status);
	}

	public void RestoreProcessingConfiguration(int sequence)
	{
		FragmentProcessingHistoryEntry entry = State?.PreviousConfigurations.Find(
			candidate => candidate.Sequence == sequence);
		if (entry?.Configuration == null || entry.Metrics == null) return;
		CancelPendingProcessingAdjustment();
		if (State.IsProcessingSearchActive) State.IsPaused = true;

		suppressProcessingHistory = true;
		if (entry.TargetRegionId is int targetRegionId &&
			State.CandidateRegions.Exists(region => region.Id == targetRegionId))
			State.SelectedRegionId = targetRegionId;
		RestoreControlState(entry.Configuration);
		suppressProcessingHistory = false;
		FragmentObservableScan scan = observationSource?.CaptureObservableScan();
		MeasurementReport = new FragmentSignalMeasurementReport
		{
			Revision = scan?.Revision ?? 0,
			TargetRegionId = entry.TargetRegionId,
			Target = CloneMetrics(entry.Metrics),
			PreviousTarget = null
		};
		lastMeasurementRevision = MeasurementReport.Revision;
		lastMeasurementTargetRegionId = entry.TargetRegionId;
		lastMeasurementTargetBounds = State.CandidateRegions.Find(
			region => region.Id == entry.TargetRegionId)?.NormalizedBounds;
		ActiveProcessingHistorySequence = entry.Sequence;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"RESTORED TEST #{entry.Sequence}",
			NextAction = "Inspect or test another configuration",
			CurrentTarget = entry.TargetRegionId is int regionId ? $"Region {regionId}" : "No region",
			MeasuredResult = $"Stored S/N {entry.Metrics.SignalToNoise:0.00} ({FormatEffect(entry)})",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		MetricsChanged?.Invoke(MeasurementReport);
		StatusChanged?.Invoke(status);
		RecordAction($"RESTORE CONFIG #{entry.Sequence}");
		ProcessingSearchChanged?.Invoke();
	}

	public void SetProcessingConfigurationBookmarked(int sequence, bool bookmarked)
	{
		FragmentProcessingHistoryEntry entry = State?.PreviousConfigurations.Find(
			candidate => candidate.Sequence == sequence);
		if (entry == null || entry.IsBookmarked == bookmarked) return;
		entry.IsBookmarked = bookmarked;
		ProcessingHistoryChanged?.Invoke();
		UpdateCurrentHistorySnapshot();
	}

	private void RecordProcessingMeasurement(FragmentAnalysisActionOrigin origin)
	{
		if (suppressProcessingHistory || MeasurementReport?.Target == null || commandSink == null) return;
		FragmentAnalysisControlState configuration = commandSink.CaptureControlState();
		FragmentProcessingHistoryEntry last = State.PreviousConfigurations.Count > 0
			? State.PreviousConfigurations[^1]
			: null;
		if (last != null && last.TargetRegionId == MeasurementReport.TargetRegionId &&
			SameConfiguration(last.Configuration, configuration))
		{
			ActiveProcessingHistorySequence = last.Sequence;
			return;
		}

		FragmentProcessingHistoryEntry previousForTarget = State.PreviousConfigurations.FindLast(
			entry => entry.TargetRegionId == MeasurementReport.TargetRegionId);
		float delta = previousForTarget == null
			? 0f
			: MeasurementReport.Target.SignalToNoise - previousForTarget.Metrics.SignalToNoise;
		float threshold = MathF.Max(settings?.ProcessingEffectThreshold ?? 0.02f, 0f);
		FragmentProcessingEffect effect = previousForTarget == null
			? FragmentProcessingEffect.Initial
			: delta >= threshold ? FragmentProcessingEffect.Improved
			: delta <= -threshold ? FragmentProcessingEffect.Degraded
			: FragmentProcessingEffect.LittleChange;
		FragmentProcessingHistoryEntry recorded = new()
		{
			Sequence = ++processingHistorySequence,
			Configuration = CloneControlState(configuration),
			Metrics = CloneMetrics(MeasurementReport.Target),
			Origin = origin,
			TargetRegionId = MeasurementReport.TargetRegionId,
			Effect = effect,
			Delta = delta
		};
		State.PreviousConfigurations.Add(recorded);
		ActiveProcessingHistorySequence = recorded.Sequence;

		int limit = Math.Max(settings?.MaximumHistoryEntries ?? 256, 1);
		while (State.PreviousConfigurations.Count > limit)
		{
			int pruneIndex = State.PreviousConfigurations.FindIndex(entry => !entry.IsBookmarked);
			if (pruneIndex < 0) break;
			State.PreviousConfigurations.RemoveAt(pruneIndex);
		}
		ProcessingHistoryChanged?.Invoke();
		ProcessingSearchChanged?.Invoke();
	}

	private static string FormatEffect(FragmentProcessingHistoryEntry entry) => entry.Effect switch
	{
		FragmentProcessingEffect.Improved => $"measured improvement {entry.Delta:+0.00;-0.00;0.00}",
		FragmentProcessingEffect.Degraded => $"measured degradation {entry.Delta:+0.00;-0.00;0.00}",
		FragmentProcessingEffect.LittleChange => "little measured change",
		_ => "baseline"
	};

	private static bool SameConfiguration(
		FragmentAnalysisControlState first,
		FragmentAnalysisControlState second)
	{
		if (first == null || second == null) return false;
		return first.PolarizationEnabled == second.PolarizationEnabled &&
			first.PolarizationLevel == second.PolarizationLevel &&
			first.SpectralEnabled == second.SpectralEnabled &&
			first.SpectralLevel == second.SpectralLevel &&
			first.SurfaceEnabled == second.SurfaceEnabled &&
			first.SurfaceLevel == second.SurfaceLevel &&
			first.ElectromagneticEnabled == second.ElectromagneticEnabled &&
			first.ResonanceEnabled == second.ResonanceEnabled &&
			first.XRayEnabled == second.XRayEnabled &&
			Mathf.IsEqualApprox(first.RotationDegrees, second.RotationDegrees);
	}

	private static FragmentAnalysisControlState CloneControlState(FragmentAnalysisControlState state) =>
		state == null ? null : new FragmentAnalysisControlState
		{
			PolarizationEnabled = state.PolarizationEnabled,
			PolarizationLevel = state.PolarizationLevel,
			SpectralEnabled = state.SpectralEnabled,
			SpectralLevel = state.SpectralLevel,
			SurfaceEnabled = state.SurfaceEnabled,
			SurfaceLevel = state.SurfaceLevel,
			ElectromagneticEnabled = state.ElectromagneticEnabled,
			ResonanceEnabled = state.ResonanceEnabled,
			XRayEnabled = state.XRayEnabled,
			RotationDegrees = state.RotationDegrees,
			ViewZoom = state.ViewZoom,
			ViewPan = state.ViewPan
		};

	private static FragmentSignalMetrics CloneMetrics(FragmentSignalMetrics metrics) =>
		metrics == null ? null : new FragmentSignalMetrics
		{
			SignalToNoise = metrics.SignalToNoise,
			IsComplete = metrics.IsComplete,
			ComparisonCount = metrics.ComparisonCount
		};

	private void RecordAction(string action)
	{
		if (State == null || string.IsNullOrWhiteSpace(action)) return;
		State.RecentActions.Add(action);
		while (State.RecentActions.Count > 5)
			State.RecentActions.RemoveAt(0);

		if (actionHistoryIndex < actionHistory.Count - 1)
			actionHistory.RemoveRange(
				actionHistoryIndex + 1,
				actionHistory.Count - actionHistoryIndex - 1);
		actionHistory.Add(CaptureHistoryEntry(action));
		while (actionHistory.Count > 6)
			actionHistory.RemoveAt(0);
		actionHistoryIndex = actionHistory.Count - 1;
		HistoryChanged?.Invoke();
	}

	public void UndoLastAction()
	{
		if (!CanUndo) return;
		actionHistoryIndex--;
		RestoreHistoryEntry(actionHistory[actionHistoryIndex], "UNDO");
	}

	public void RedoLastAction()
	{
		if (!CanRedo) return;
		actionHistoryIndex++;
		RestoreHistoryEntry(actionHistory[actionHistoryIndex], "REDO");
	}

	private void ResetActionHistory()
	{
		actionHistory.Clear();
		actionHistory.Add(CaptureHistoryEntry("Initial analysis state"));
		actionHistoryIndex = 0;
	}

	private FragmentActionHistoryEntry CaptureHistoryEntry(string action) => new()
	{
		Action = action,
		State = State.Clone(),
		Controls = CloneControlState(commandSink?.CaptureControlState()),
		Measurement = CloneMeasurementReport(MeasurementReport),
		ActiveProcessingSequence = ActiveProcessingHistorySequence
	};

	private void RestoreHistoryEntry(FragmentActionHistoryEntry entry, string direction)
	{
		if (entry == null) return;
		bool stoppedAutonomousWorkflow = autonomousWorkflowStage != FragmentAutonomousWorkflowStage.Inactive;
		ResetAutonomousWorkflowTransient();
		if(stoppedAutonomousWorkflow)
			AutonomousWorkflowChanged?.Invoke(AutonomousWorkflowStage);
		CancelPendingProcessingAdjustment();
		ClearNavigationTarget(true);
		featureReviewRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;
		State = entry.State.Clone();
		if (State.IsProcessingSearchActive) State.IsPaused = true;
		ActiveProcessingHistorySequence = entry.ActiveProcessingSequence;
		RestoreFeatureReviewScope();
		RestoreControlState(entry.Controls);
		FragmentObservableScan currentScan = observationSource?.CaptureObservableScan();
		if (currentScan != null) lastFeatureRevision = currentScan.Revision;
		lastMeasurementRevision = ulong.MaxValue;
		lastMeasurementTargetRegionId = null;
		lastMeasurementTargetBounds = null;
		MeasurementReport = CloneMeasurementReport(entry.Measurement);
		if (MeasurementReport == null) RefreshSignalMetrics(true);
		else
		{
			lastMeasurementRevision = MeasurementReport.Revision;
			lastMeasurementTargetRegionId = MeasurementReport.TargetRegionId;
			lastMeasurementTargetBounds = State.CandidateRegions.Find(
				region => region.Id == MeasurementReport.TargetRegionId)?.NormalizedBounds;
			MetricsChanged?.Invoke(MeasurementReport);
		}
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"{direction}: {entry.Action}",
			NextAction = CanRedo ? "Forward reapplies the next action" : "Continue analysis",
			CurrentTarget = State.SelectedRegionId is int regionId
				? $"Region {regionId}"
				: State.SelectedFeatureId is int featureId ? $"Feature {featureId}" : "Current fragment",
			MeasuredResult = $"History {actionHistoryIndex + 1}/{actionHistory.Count}",
			LockedParameters = GetLockedProcessingParameterNames()
		};
		AllocationChanged?.Invoke();
		FeaturesChanged?.Invoke();
		RegionsChanged?.Invoke();
		StructuresChanged?.Invoke();
		OrientationsChanged?.Invoke();
		if (isAcceptedRegionFeatureReviewActive)
		{
			RegionReviewCompleted?.Invoke(featureReviewRegionIds.Count);
			if (featureReviewRegionIds.Count == 1)
				RegionFocusRequested?.Invoke(featureReviewRegionIds[0]);
		}
		StatusChanged?.Invoke(status);
		HistoryChanged?.Invoke();
		ProcessingHistoryChanged?.Invoke();
	}

	private static FragmentSignalMeasurementReport CloneMeasurementReport(
		FragmentSignalMeasurementReport report) => report == null ? null : new FragmentSignalMeasurementReport
		{
			Revision = report.Revision,
			TargetRegionId = report.TargetRegionId,
			Target = CloneMetrics(report.Target),
			PreviousTarget = CloneMetrics(report.PreviousTarget)
		};

	private void UpdateCurrentHistorySnapshot()
	{
		if (actionHistoryIndex < 0 || actionHistoryIndex >= actionHistory.Count) return;
		string action = actionHistory[actionHistoryIndex].Action;
		actionHistory[actionHistoryIndex] = CaptureHistoryEntry(action);
	}

	private void RestoreFeatureReviewScope()
	{
		if (State.CandidateRegions.Count == 0 || State.CandidateRegions.Exists(region =>
			region.Disposition == FragmentAnnotationDisposition.Proposed)) return;
		isAcceptedRegionFeatureReviewActive = true;
		foreach (FragmentCandidateRegion region in State.CandidateRegions)
			if (region.Disposition == FragmentAnnotationDisposition.Accepted)
				featureReviewRegionIds.Add(region.Id);
		featureReviewRegionIds.Sort();
	}

	private void RestoreControlState(FragmentAnalysisControlState target)
	{
		if (target == null || commandSink == null) return;
		FragmentAnalysisControlState current = commandSink.CaptureControlState();
		if (current.PolarizationEnabled != target.PolarizationEnabled)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
				FragmentAnalysisParameter.PolarizationEnabled, target.PolarizationEnabled,
				FragmentAnalysisActionOrigin.Restore));
		if (current.PolarizationLevel != target.PolarizationLevel)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Level(
				FragmentAnalysisParameter.PolarizationLevel, target.PolarizationLevel,
				FragmentAnalysisActionOrigin.Restore));
		if (current.SpectralEnabled != target.SpectralEnabled)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
				FragmentAnalysisParameter.SpectralEnabled, target.SpectralEnabled,
				FragmentAnalysisActionOrigin.Restore));
		if (current.SpectralLevel != target.SpectralLevel)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Level(
				FragmentAnalysisParameter.SpectralLevel, target.SpectralLevel,
				FragmentAnalysisActionOrigin.Restore));
		if (current.SurfaceEnabled != target.SurfaceEnabled)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
				FragmentAnalysisParameter.SurfaceEnabled, target.SurfaceEnabled,
				FragmentAnalysisActionOrigin.Restore));
		if (current.SurfaceLevel != target.SurfaceLevel)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Level(
				FragmentAnalysisParameter.SurfaceLevel, target.SurfaceLevel,
				FragmentAnalysisActionOrigin.Restore));
		if (current.ElectromagneticEnabled != target.ElectromagneticEnabled)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
				FragmentAnalysisParameter.ElectromagneticEnabled, target.ElectromagneticEnabled,
				FragmentAnalysisActionOrigin.Restore));
		if (current.ResonanceEnabled != target.ResonanceEnabled)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
				FragmentAnalysisParameter.ResonanceEnabled, target.ResonanceEnabled,
				FragmentAnalysisActionOrigin.Restore));
		if (current.XRayEnabled != target.XRayEnabled)
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Toggle(
				FragmentAnalysisParameter.XRayEnabled, target.XRayEnabled,
				FragmentAnalysisActionOrigin.Restore));
		if (!Mathf.IsEqualApprox(current.RotationDegrees, target.RotationDegrees))
			commandSink.DispatchAnalysisCommand(FragmentAnalysisCommand.Rotation(
				target.RotationDegrees, FragmentAnalysisActionOrigin.Restore));
	}

	private sealed class FragmentActionHistoryEntry
	{
		public string Action { get; init; }
		public FragmentAutonomyState State { get; init; }
		public FragmentAnalysisControlState Controls { get; init; }
		public FragmentSignalMeasurementReport Measurement { get; init; }
		public int? ActiveProcessingSequence { get; init; }
	}

    private void RefreshIdleStatus(string currentAction = null)
    {
        FragmentRoverActivity activity = GetIdleActivity();
        string current = currentAction ?? activity switch
        {
            FragmentRoverActivity.Off => "Manual analysis",
            FragmentRoverActivity.Paused => "Autonomy paused",
            _ => "Fragment analysis active"
        };

        status = new FragmentRoverActionStatus
        {
            Activity = activity,
            CurrentAction = current,
            NextAction = activity == FragmentRoverActivity.Off
                ? "Select Supporter or Performer"
				: "Await player analysis action",
			CurrentTarget = activity == FragmentRoverActivity.Off ? "None" : "Current fragment",
            MeasuredResult = "No measurement",
			LockedParameters = GetLockedProcessingParameterNames()
        };
        StatusChanged?.Invoke(status);
    }

    private FragmentRoverActivity GetIdleActivity()
    {
        if (State == null || State.GlobalMode == FragmentAutonomyMode.Off)
            return FragmentRoverActivity.Off;
        return State.IsPaused ? FragmentRoverActivity.Paused : FragmentRoverActivity.Idle;
    }

    private static string GetParameterDisplayName(FragmentAnalysisParameter parameter)
    {
        return parameter switch
        {
			FragmentAnalysisParameter.PolarizationEnabled => "Polarization",
			FragmentAnalysisParameter.PolarizationLevel => "Polarization level",
			FragmentAnalysisParameter.SpectralEnabled => "Spectral",
			FragmentAnalysisParameter.SpectralLevel => "Spectral level",
			FragmentAnalysisParameter.SurfaceEnabled => "Surface topography",
			FragmentAnalysisParameter.SurfaceLevel => "Surface level",
			FragmentAnalysisParameter.ElectromagneticEnabled => "Electromagnetic channel",
			FragmentAnalysisParameter.ResonanceEnabled => "Resonance channel",
			FragmentAnalysisParameter.XRayEnabled => "X-Ray channel",
			FragmentAnalysisParameter.Rotation => "Rotation",
			FragmentAnalysisParameter.View => "View",
			FragmentAnalysisParameter.Configuration => "Processing configuration",
            _ => "Analysis changed"
        };
    }
}
