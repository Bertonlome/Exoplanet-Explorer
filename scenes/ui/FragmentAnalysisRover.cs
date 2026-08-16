using System;
using System.Collections.Generic;
using Godot;

public partial class FragmentAnalysisRover : Node
{
    [Export]
    private FragmentAutonomySettings settings = new();

    private IFragmentObservationSource observationSource;
    private IFragmentAnalysisCommandSink commandSink;
    private FragmentAutonomyTruth truth;
	private FragmentRoverActionStatus status = new() { Activity = FragmentRoverActivity.Off };
	private ulong lastFeatureRevision = ulong.MaxValue;
	private readonly List<FragmentActionHistoryEntry> actionHistory = new();
	private int actionHistoryIndex = -1;
	private float navigationPreviewRemaining = -1f;
	private readonly List<int> featureReviewRegionIds = new();
	private bool isAcceptedRegionFeatureReviewActive;
	private float measurementDelayRemaining = -1f;
	private ulong lastMeasurementRevision = ulong.MaxValue;
	private int? lastMeasurementTargetRegionId;
	private Rect2? lastMeasurementTargetBounds;
	private int processingHistorySequence;
	private FragmentAnalysisActionOrigin pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
	private string pendingProcessingAction;
	private bool suppressProcessingHistory;

    public FragmentAutonomySettings Settings => settings;
    public FragmentAutonomyState State { get; private set; }
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

    public event Action<FragmentRoverActionStatus> StatusChanged;
    public event Action AllocationChanged;
	public event Action HistoryChanged;
	public event Action FeaturesChanged;
	public event Action<int> FeatureFocusRequested;
	public event Action RegionsChanged;
	public event Action<int> RegionFocusRequested;
	public event Action<Rect2, int, bool> NavigationTargetChanged;
	public event Action NavigationTargetCleared;
	public event Action<Rect2> NavigationExecutionRequested;
	public event Action NavigationCancellationRequested;
	public event Action<int> RegionReviewCompleted;
	public event Action<FragmentSignalMeasurementReport> MetricsChanged;
	public event Action ProcessingHistoryChanged;

    public void Configure(FragmentAutonomySettings configuredSettings)
    {
        settings = configuredSettings ?? new FragmentAutonomySettings();
    }

    public void Initialize(
        IFragmentObservationSource observationSource,
        IFragmentAnalysisCommandSink commandSink,
        FragmentAutonomyTruth truth,
        FragmentAutonomyState restoredState = null)
    {
        Shutdown();
        settings ??= new FragmentAutonomySettings();
        this.observationSource = observationSource;
        this.commandSink = commandSink;
        this.truth = truth;
        State = restoredState?.Clone() ?? FragmentAutonomyState.CreateDefault(settings);
		State.DetectedFeatures.RemoveAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Player);
		if (State.SelectedFeatureId.HasValue &&
			!State.DetectedFeatures.Exists(feature => feature.Id == State.SelectedFeatureId.Value))
		{
			State.SelectedFeatureId = null;
		}
        EnsureReliabilityDefaults();
		processingHistorySequence = 0;
		foreach (FragmentProcessingHistoryEntry entry in State.PreviousConfigurations)
			processingHistorySequence = Math.Max(processingHistorySequence, entry.Sequence);
        commandSink.AnalysisChanged += OnAnalysisChanged;
        RefreshIdleStatus();
		RefreshDetectedFeatures();
		RefreshSignalMetrics(true);
		ResetActionHistory();
    }

    public void Shutdown()
    {
        if (commandSink != null)
            commandSink.AnalysisChanged -= OnAnalysisChanged;

        observationSource = null;
        commandSink = null;
        truth = null;
		lastFeatureRevision = ulong.MaxValue;
		actionHistory.Clear();
		actionHistoryIndex = -1;
		featureReviewRegionIds.Clear();
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
		ClearNavigationTarget(false);
    }

    public void ResetForPuzzle(FragmentAutonomyTruth newTruth)
    {
		ClearNavigationTarget(true);
		featureReviewRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;
        FragmentAutonomyState previous = State ?? FragmentAutonomyState.CreateDefault(settings);
        FragmentAutonomyState reset = FragmentAutonomyState.CreateDefault(settings);
        reset.GlobalMode = previous.GlobalMode;

        foreach ((FragmentAutonomyCapability capability, FragmentAutonomyMode mode) in previous.CapabilityOverrides)
            reset.CapabilityOverrides[capability] = mode;
        foreach ((FragmentAutonomyCapability capability, float reliability) in previous.YellowReliability)
            reset.YellowReliability[capability] = reliability;

        State = reset;
        truth = newTruth;
		lastFeatureRevision = ulong.MaxValue;
		processingHistorySequence = 0;
		RefreshDetectedFeatures();
		RefreshSignalMetrics(true);
		ResetActionHistory();
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

		ClearNavigationTarget(true);
        State.GlobalMode = mode;
        if (mode == FragmentAutonomyMode.Off)
            State.IsPaused = false;
        RefreshIdleStatus();
        AllocationChanged?.Invoke();
		RefreshDetectedFeatures(true);
		RefreshSignalMetrics(true);
		RecordAction($"MODE: {mode.ToString().ToUpperInvariant()}");
    }

    public void SetCapabilityOverride(
        FragmentAutonomyCapability capability,
        FragmentAutonomyMode? mode)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
		if (capability == FragmentAutonomyCapability.NavigateSample)
			ClearNavigationTarget(true);
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
    }

    public void SetYellowReliability(FragmentAutonomyCapability capability, float reliability)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
        State.YellowReliability[capability] = Mathf.Clamp(reliability, 0f, 1f);
		RecordAction($"RELIABILITY: {FragmentAutonomyCapabilityCatalog.GetDisplayName(capability)}");
        AllocationChanged?.Invoke();
    }

    public void SetPaused(bool paused)
    {
        if (State == null) State = FragmentAutonomyState.CreateDefault(settings);
        State.IsPaused = State.GlobalMode != FragmentAutonomyMode.Off && paused;
		if (State.IsPaused) ClearNavigationTarget(true);
        RefreshIdleStatus();
		if (!State.IsPaused) RefreshDetectedFeatures(true);
		RecordAction(State.IsPaused ? "PAUSE" : "RESUME");
    }

    public override void _ExitTree()
    {
        Shutdown();
    }

	public override void _Process(double delta)
	{
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
			LockedParameters = "None"
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
        if (change == null || change.Origin != FragmentAnalysisActionOrigin.Player) return;
		if (change.Parameter == FragmentAnalysisParameter.View) return;

        bool wasActive = status.Activity == FragmentRoverActivity.Planning ||
            status.Activity == FragmentRoverActivity.AwaitingApproval ||
            status.Activity == FragmentRoverActivity.Executing;
        if (wasActive)
		{
            State.IsPaused = true;
			ClearNavigationTarget(true);
		}

        status = new FragmentRoverActionStatus
        {
            Activity = wasActive ? FragmentRoverActivity.Overridden : GetIdleActivity(),
            CurrentAction = $"PLAYER: {GetParameterDisplayName(change.Parameter)}",
            NextAction = wasActive ? "Paused after player override" : "No capability implemented yet",
            CurrentTarget = "None",
            MeasuredResult = "No measurement",
            LockedParameters = "None"
        };
        StatusChanged?.Invoke(status);
		RefreshDetectedFeatures();
		pendingMeasurementOrigin = change.Origin;
		pendingProcessingAction = $"PROCESSING: {GetParameterDisplayName(change.Parameter)}";
		ScheduleSignalMeasurement();
    }

	public void RefreshDetectedFeatures(bool force = false, bool recordHistory = false)
	{
		if (State == null || observationSource == null || State.IsPaused ||
			GetEffectiveMode(FragmentAutonomyCapability.SenseSampleFeatures) == FragmentAutonomyMode.Off)
		{
			return;
		}

		FragmentObservableScan scan = observationSource.CaptureObservableScan();
		if (scan == null || (!force && scan.Revision == lastFeatureRevision)) return;
		lastFeatureRevision = scan.Revision;

		IReadOnlyList<FragmentDetectedFeature> detected =
			FragmentFeatureDetector.DetectFeatures(scan);
		List<FragmentDetectedFeature> previousRoverFeatures = State.DetectedFeatures.FindAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Rover);
		List<FragmentDetectedFeature> playerFeatures = State.DetectedFeatures.FindAll(
			feature => feature.Provenance == FragmentAnnotationProvenance.Player);
		int nextId = GetNextFeatureId();

		List<int> matchedPreviousIds = new();
		State.DetectedFeatures.Clear();
		State.DetectedFeatures.AddRange(playerFeatures);
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
			if (matchedPreviousIds.Contains(previous.Id) ||
				previous.Disposition == FragmentAnnotationDisposition.Proposed)
			{
				continue;
			}
			State.DetectedFeatures.Add(previous);
		}
		ApplyActiveCropToFeatures();

		if (State.SelectedFeatureId.HasValue &&
			!State.DetectedFeatures.Exists(feature => feature.Id == State.SelectedFeatureId.Value))
		{
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
		if (!IsNavigationInProgress)
		{
			status = new FragmentRoverActionStatus
			{
				Activity = State.IsPaused ? FragmentRoverActivity.Paused : FragmentRoverActivity.Idle,
				CurrentAction = $"Detected {roverCount} observable features",
				NextAction = "Review the selected feature, then accept or dismiss it",
				CurrentTarget = "Whole virtual scan",
				MeasuredResult = $"{roverCount} candidate features",
				LockedParameters = "None"
			};
		}
		if (recordHistory) RecordAction($"SCAN: {roverCount} feature groups");
		if (!IsNavigationInProgress) StatusChanged?.Invoke(status);
		FeaturesChanged?.Invoke();
		if (!IsNavigationInProgress) RequestSelectedFeatureFocus();
	}

	public void RefreshCandidateRegions(bool force = false)
	{
		if (State == null || State.IsPaused ||
			GetEffectiveMode(FragmentAutonomyCapability.InterpretSignalRegions) == FragmentAutonomyMode.Off)
		{
			return;
		}
		featureReviewRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;

		IReadOnlyList<FragmentCandidateRegion> detected =
			FragmentRegionDetector.GroupCandidateRegions(State.DetectedFeatures);
		List<FragmentCandidateRegion> previousRover = State.CandidateRegions.FindAll(
			region => region.Provenance == FragmentAnnotationProvenance.Rover);
		List<FragmentCandidateRegion> playerRegions = State.CandidateRegions.FindAll(
			region => region.Provenance == FragmentAnnotationProvenance.Player);
		List<int> matchedIds = new();
		int nextId = GetNextRegionId();

		State.CandidateRegions.Clear();
		State.CandidateRegions.AddRange(playerRegions);
		foreach (FragmentCandidateRegion candidate in detected)
		{
			FragmentCandidateRegion previous = FindBestPreviousRegion(previousRover, matchedIds, candidate);
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
		State.SelectedRegionId ??= FindFirstProposedRegionId();

		int roverCount = State.CandidateRegions.FindAll(
			region => region.Provenance == FragmentAnnotationProvenance.Rover).Count;
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = $"Grouped {roverCount} candidate regions",
			NextAction = "Review, accept, dismiss, or draw a region",
			CurrentTarget = "Visible feature clusters",
			MeasuredResult = $"{roverCount} candidate regions",
			LockedParameters = "None"
		};
		if (force) RecordAction($"GROUP: {roverCount} regions");
		StatusChanged?.Invoke(status);
		RegionsChanged?.Invoke();
		RefreshSignalMetrics(true);
		RequestSelectedRegionFocus();
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
		if (acceptedRegions.Count == 1)
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
		State.CandidateRegions.Add(new FragmentCandidateRegion
		{
			Id = id,
			NormalizedBounds = bounds,
			Confidence = 1f,
			Provenance = FragmentAnnotationProvenance.Player,
			Disposition = FragmentAnnotationDisposition.Accepted,
			FeatureIds = FindFeaturesInRegion(bounds)
		});
		State.SelectedRegionId = id;
		State.ActiveCropRegionId = id;
		ApplyRegionCrop();
		PublishRegionEditStatus("Added player region", id);
		RegionFocusRequested?.Invoke(id);
	}

	public void ResizeRegion(int regionId, Rect2 normalizedBounds)
	{
		FragmentCandidateRegion region = State?.CandidateRegions.Find(candidate => candidate.Id == regionId);
		if (region == null || normalizedBounds.Size.X < 0.01f || normalizedBounds.Size.Y < 0.01f) return;
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
			PublishRegionEditStatus("Unlocked rendering for region", regionId);
			return;
		}

		FragmentObservableScan currentScan = observationSource.CaptureObservableScan();
		if (currentScan == null) return;
		FragmentLockedRegionView lockedView = new()
		{
			RegionId = regionId,
			NormalizedBounds = region.NormalizedBounds,
			Scan = CloneObservableScan(currentScan)
		};
		foreach (FragmentDetectedFeature feature in State.DetectedFeatures)
		{
			if (feature.Disposition == FragmentAnnotationDisposition.Dismissed) continue;
			lockedView.Features.Add(CloneDetectedFeature(feature));
		}
		State.LockedRegionViews.Add(lockedView);
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
		Disposition = feature.Disposition
	};

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
		status = new FragmentRoverActionStatus
		{
			Activity = GetIdleActivity(),
			CurrentAction = action,
			NextAction = "Continue candidate-region review",
			CurrentTarget = $"Region {regionId}",
			MeasuredResult = $"{State.CandidateRegions.Count} stored regions",
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

	private int? FindFirstProposedRegionId() => State?.CandidateRegions.Find(region =>
		region.Provenance == FragmentAnnotationProvenance.Rover &&
		region.Disposition == FragmentAnnotationDisposition.Proposed)?.Id;

	private int? FindNextProposedRegionId(int afterId)
	{
		if (State == null || State.CandidateRegions.Count == 0) return null;
		int start = State.CandidateRegions.FindIndex(region => region.Id == afterId);
		for (int offset = 1; offset <= State.CandidateRegions.Count; offset++)
		{
			FragmentCandidateRegion region = State.CandidateRegions[
				(start + offset + State.CandidateRegions.Count) % State.CandidateRegions.Count];
			if (region.Provenance == FragmentAnnotationProvenance.Rover &&
				region.Disposition == FragmentAnnotationDisposition.Proposed) return region.Id;
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

		FragmentDetectedFeature feature = State.DetectedFeatures.Find(
			candidate => candidate.Id == featureId);
		if (feature == null) return;
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
			best = feature;
			bestDistanceSquared = distanceSquared;
		}
		return best;
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
		return State?.DetectedFeatures.Find(feature =>
			feature.Provenance == FragmentAnnotationProvenance.Rover &&
			feature.Disposition == FragmentAnnotationDisposition.Proposed &&
			IsFeatureInActiveReviewRegions(feature))?.Id;
	}

	private int? FindNextProposedFeatureId(int afterFeatureId)
	{
		if (State == null) return null;
		int start = State.DetectedFeatures.FindIndex(feature => feature.Id == afterFeatureId);
		for (int offset = 1; offset <= State.DetectedFeatures.Count; offset++)
		{
			FragmentDetectedFeature feature = State.DetectedFeatures[
				(start + offset + State.DetectedFeatures.Count) % State.DetectedFeatures.Count];
			if (feature.Provenance == FragmentAnnotationProvenance.Rover &&
				feature.Disposition == FragmentAnnotationDisposition.Proposed &&
				IsFeatureInActiveReviewRegions(feature))
			{
				return feature.Id;
			}
		}
		return null;
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
		measurementDelayRemaining = MathF.Max(settings?.MeasurementDebounceSeconds ?? 0.12f, 0f);
		if (measurementDelayRemaining <= 0f)
		{
			measurementDelayRemaining = -1f;
			RefreshSignalMetrics();
		}
	}

	public void RefreshSignalMetrics(bool force = false)
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
				targetFeatureIds)
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
		RecordProcessingMeasurement(pendingMeasurementOrigin);
		pendingMeasurementOrigin = FragmentAnalysisActionOrigin.System;
		MetricsChanged?.Invoke(MeasurementReport);
		if (!string.IsNullOrEmpty(pendingProcessingAction))
		{
			string action = pendingProcessingAction;
			pendingProcessingAction = null;
			RecordAction(action);
		}
	}

	public void RestoreProcessingConfiguration(int sequence)
	{
		FragmentProcessingHistoryEntry entry = State?.PreviousConfigurations.Find(
			candidate => candidate.Sequence == sequence);
		if (entry?.Configuration == null || entry.Metrics == null) return;

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
			LockedParameters = "None"
		};
		MetricsChanged?.Invoke(MeasurementReport);
		StatusChanged?.Invoke(status);
		RecordAction($"RESTORE CONFIG #{entry.Sequence}");
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
		metrics == null ? null : new FragmentSignalMetrics { SignalToNoise = metrics.SignalToNoise };

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
		ClearNavigationTarget(true);
		featureReviewRegionIds.Clear();
		isAcceptedRegionFeatureReviewActive = false;
		State = entry.State.Clone();
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
			LockedParameters = "None"
		};
		AllocationChanged?.Invoke();
		FeaturesChanged?.Invoke();
		RegionsChanged?.Invoke();
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
            LockedParameters = "None"
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
            FragmentAnalysisParameter.PolarizationEnabled => "Polarization toggle changed",
            FragmentAnalysisParameter.PolarizationLevel => "Polarization level changed",
            FragmentAnalysisParameter.SpectralEnabled => "Spectral toggle changed",
            FragmentAnalysisParameter.SpectralLevel => "Spectral level changed",
            FragmentAnalysisParameter.SurfaceEnabled => "Surface toggle changed",
            FragmentAnalysisParameter.SurfaceLevel => "Surface level changed",
            FragmentAnalysisParameter.ElectromagneticEnabled => "Electromagnetic channel changed",
            FragmentAnalysisParameter.ResonanceEnabled => "Resonance channel changed",
            FragmentAnalysisParameter.XRayEnabled => "X-Ray channel changed",
            FragmentAnalysisParameter.Rotation => "Rotation changed",
            FragmentAnalysisParameter.View => "View changed",
            _ => "Analysis changed"
        };
    }
}
