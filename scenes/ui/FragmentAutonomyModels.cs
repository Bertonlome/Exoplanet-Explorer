using System;
using System.Collections.Generic;
using Godot;

public enum FragmentAutonomyMode
{
    Off,
    Supporter,
    Performer
}

public enum FragmentCapabilityRating
{
    Green,
    Yellow,
    Orange,
    Red
}

public enum FragmentAutonomyCapability
{
    SenseSampleAvailability,
    DecideToInitiateAnalysis,
    InitiateAnalysis,
    SenseSampleFeatures,
    InterpretSignalRegions,
    DecideWhereToInspect,
    NavigateSample,
    SenseProcessingChanges,
    InterpretProcessingEffects,
    DecideProcessingConfiguration,
    AdjustProcessingParameters,
    SenseReconstructedStructures,
    InterpretGlyphIdentity,
    DecideCandidateValidity,
    InterpretUprightOrientation,
    DecideRotationCorrection,
    Rotate,
    SenseDirectionalArrow,
    InterpretMonolithDirection
}

public enum FragmentAnalysisActionOrigin
{
    Player,
    Rover,
    Restore,
    System
}

public enum FragmentAnalysisParameter
{
    None,
    PolarizationEnabled,
    PolarizationLevel,
    SpectralEnabled,
    SpectralLevel,
    SurfaceEnabled,
    SurfaceLevel,
    ElectromagneticEnabled,
    ResonanceEnabled,
    XRayEnabled,
    Rotation,
    View,
	Configuration
}

public enum FragmentAutonomousWorkflowStage
{
	Inactive,
	SearchingRegions,
	AwaitingRegionReview,
	SearchingRegionFeatures,
	AwaitingFeatureReview,
	AwaitingRegionChoice,
	AwaitingStructureReview,
	AwaitingOrientationReview,
	WaitingForRotation,
	AwaitingArrowReview,
	AwaitingPlayerArrow,
	Complete
}

public enum FragmentRoverActivity
{
    Off,
    Idle,
    Planning,
    AwaitingApproval,
    Executing,
    Paused,
    Overridden,
    WaitingForPlayer
}

public enum FragmentAnnotationDisposition
{
    Proposed,
    Accepted,
    Dismissed
}

public enum FragmentAnnotationProvenance
{
    Rover,
    Player
}

public enum FragmentFeatureEditAction
{
    Select,
    Accept,
    Dismiss,
	Restore
}

public enum FragmentRegionEditAction
{
	Select,
	Accept,
	Dismiss,
	Restore
}

public enum FragmentStructureEditAction
{
	Select,
	Accept,
	Dismiss,
	Restore
}

public enum FragmentOrientationEditAction
{
	Select,
	Accept,
	Reject,
	Restore
}

public enum FragmentRotationCorrectionEditAction
{
	Accept,
	Reject
}

public enum FragmentArrowEditAction
{
	Select,
	Accept,
	Reject,
	Restore
}

public enum FragmentSampleAnalysisStatus
{
    Available,
    Analysing,
    PreviouslyAnalysed,
    Solved
}

public sealed class FragmentSampleAvailability
{
    public Vector2I Position { get; init; }
    public FragmentSampleAnalysisStatus Status { get; init; }
    public bool IsRestored { get; init; }
}

public sealed class FragmentAnalysisProposal
{
    public Vector2I Position { get; init; }
    public FragmentSampleAnalysisStatus Status { get; init; }
    public FragmentRoverActivity Activity { get; init; }
    public string Reason { get; init; } = "Sample available in analysis range";
}

public static class FragmentAutonomyCapabilityCatalog
{
    public static readonly FragmentAutonomyCapability[] All =
        (FragmentAutonomyCapability[])Enum.GetValues(typeof(FragmentAutonomyCapability));

    public static string GetDisplayName(FragmentAutonomyCapability capability)
    {
        return capability switch
        {
            FragmentAutonomyCapability.SenseSampleAvailability => "1.1 Sense sample availability",
            FragmentAutonomyCapability.DecideToInitiateAnalysis => "1.2 Decide to initiate analysis",
            FragmentAutonomyCapability.InitiateAnalysis => "1.3 Initiate analysis",
            FragmentAutonomyCapability.SenseSampleFeatures => "2.1 Sense sample features",
            FragmentAutonomyCapability.InterpretSignalRegions => "2.2 Interpret signal regions",
            FragmentAutonomyCapability.DecideWhereToInspect => "2.3 Decide where to inspect",
            FragmentAutonomyCapability.NavigateSample => "2.4 Navigate sample",
            FragmentAutonomyCapability.SenseProcessingChanges => "3.1 Sense processing changes",
            FragmentAutonomyCapability.InterpretProcessingEffects => "3.2 Interpret processing effects",
            FragmentAutonomyCapability.DecideProcessingConfiguration => "3.3 Decide processing configuration",
            FragmentAutonomyCapability.AdjustProcessingParameters => "3.4 Adjust processing parameters",
            FragmentAutonomyCapability.SenseReconstructedStructures => "4.1 Sense reconstructed structures",
            FragmentAutonomyCapability.InterpretGlyphIdentity => "4.2 Interpret glyph identity",
            FragmentAutonomyCapability.DecideCandidateValidity => "4.3 Decide candidate validity",
            FragmentAutonomyCapability.InterpretUprightOrientation => "5.1 Interpret upright orientation",
            FragmentAutonomyCapability.DecideRotationCorrection => "5.2 Decide rotation correction",
            FragmentAutonomyCapability.Rotate => "5.3 Rotate",
            FragmentAutonomyCapability.SenseDirectionalArrow => "6.1 Sense directional arrow",
            FragmentAutonomyCapability.InterpretMonolithDirection => "6.2 Interpret monolith direction",
            _ => capability.ToString()
        };
    }

    public static FragmentCapabilityRating GetSupportRating(FragmentAutonomyCapability capability)
    {
        return capability switch
        {
            FragmentAutonomyCapability.SenseSampleAvailability => FragmentCapabilityRating.Orange,
            FragmentAutonomyCapability.DecideToInitiateAnalysis => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.InitiateAnalysis => FragmentCapabilityRating.Red,
            FragmentAutonomyCapability.SenseSampleFeatures => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.InterpretSignalRegions => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.DecideWhereToInspect => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.NavigateSample => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.SenseProcessingChanges => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.InterpretProcessingEffects => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.DecideProcessingConfiguration => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.AdjustProcessingParameters => FragmentCapabilityRating.Red,
            FragmentAutonomyCapability.SenseReconstructedStructures => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.InterpretGlyphIdentity => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.DecideCandidateValidity => FragmentCapabilityRating.Red,
            FragmentAutonomyCapability.InterpretUprightOrientation => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.DecideRotationCorrection => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.Rotate => FragmentCapabilityRating.Red,
            FragmentAutonomyCapability.SenseDirectionalArrow => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.InterpretMonolithDirection => FragmentCapabilityRating.Green,
            _ => FragmentCapabilityRating.Red
        };
    }

    public static FragmentCapabilityRating GetPerformerRating(FragmentAutonomyCapability capability)
    {
        return capability switch
        {
            FragmentAutonomyCapability.SenseSampleAvailability => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.DecideToInitiateAnalysis => FragmentCapabilityRating.Orange,
            FragmentAutonomyCapability.InitiateAnalysis => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.SenseSampleFeatures => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.InterpretSignalRegions => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.DecideWhereToInspect => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.NavigateSample => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.SenseProcessingChanges => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.InterpretProcessingEffects => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.DecideProcessingConfiguration => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.AdjustProcessingParameters => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.SenseReconstructedStructures => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.InterpretGlyphIdentity => FragmentCapabilityRating.Red,
            FragmentAutonomyCapability.DecideCandidateValidity => FragmentCapabilityRating.Red,
            FragmentAutonomyCapability.InterpretUprightOrientation => FragmentCapabilityRating.Yellow,
            FragmentAutonomyCapability.DecideRotationCorrection => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.Rotate => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.SenseDirectionalArrow => FragmentCapabilityRating.Green,
            FragmentAutonomyCapability.InterpretMonolithDirection => FragmentCapabilityRating.Green,
            _ => FragmentCapabilityRating.Red
        };
    }
}

public sealed class FragmentAnalysisControlState
{
    public bool PolarizationEnabled { get; init; }
    public int PolarizationLevel { get; init; }
    public bool SpectralEnabled { get; init; }
    public int SpectralLevel { get; init; }
    public bool SurfaceEnabled { get; init; }
    public int SurfaceLevel { get; init; }
    public bool ElectromagneticEnabled { get; init; }
    public bool ResonanceEnabled { get; init; }
    public bool XRayEnabled { get; init; }
    public float RotationDegrees { get; init; }
    public float ViewZoom { get; init; } = 1f;
    public Vector2 ViewPan { get; init; }
}

public sealed class FragmentAnalysisCommand
{
    public FragmentAnalysisParameter Parameter { get; init; }
    public FragmentAnalysisActionOrigin Origin { get; init; }
    public bool BoolValue { get; init; }
    public int IntValue { get; init; }
    public float FloatValue { get; init; }
	public int? RegionId { get; init; }
	public Rect2 RegionBounds { get; init; }
	public Vector2 RotationPivotNormalized { get; init; } = new(0.5f, 0.5f);

    public static FragmentAnalysisCommand Toggle(
        FragmentAnalysisParameter parameter,
        bool value,
        FragmentAnalysisActionOrigin origin) => new()
        {
            Parameter = parameter,
            BoolValue = value,
            Origin = origin
        };

    public static FragmentAnalysisCommand Level(
        FragmentAnalysisParameter parameter,
        int value,
        FragmentAnalysisActionOrigin origin) => new()
        {
            Parameter = parameter,
            IntValue = value,
            Origin = origin
        };

    public static FragmentAnalysisCommand Rotation(
        float value,
        FragmentAnalysisActionOrigin origin) => new()
        {
            Parameter = FragmentAnalysisParameter.Rotation,
            FloatValue = value,
            Origin = origin
        };

	public static FragmentAnalysisCommand RegionRotation(
		int regionId,
		Rect2 regionBounds,
		Vector2 pivotNormalized,
		float value,
		FragmentAnalysisActionOrigin origin) => new()
		{
			Parameter = FragmentAnalysisParameter.Rotation,
			FloatValue = value,
			Origin = origin,
			RegionId = regionId,
			RegionBounds = regionBounds,
			RotationPivotNormalized = pivotNormalized
		};
}

public sealed class FragmentAnalysisChange
{
    public FragmentAnalysisControlState Previous { get; init; }
    public FragmentAnalysisControlState Current { get; init; }
    public FragmentAnalysisParameter Parameter { get; init; }
    public FragmentAnalysisActionOrigin Origin { get; init; }
	public int? RegionId { get; init; }
}

public interface IFragmentAnalysisCommandSink
{
    event Action<FragmentAnalysisChange> AnalysisChanged;
    FragmentAnalysisControlState CaptureControlState();
	float CaptureRegionRotationDegrees(int regionId);
    void DispatchAnalysisCommand(FragmentAnalysisCommand command);
	void DispatchAnalysisConfiguration(
		FragmentAnalysisControlState configuration,
		FragmentAnalysisActionOrigin origin);
}

public interface IFragmentObservationSource
{
    FragmentObservableScan CaptureObservableScan();
}

public sealed class FragmentObservablePrimitive
{
    public int Id { get; init; }
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public Color Color { get; init; }
    public float Width { get; init; }
    public float Intensity { get; init; }
}

public sealed class FragmentObservableScan
{
    public ulong Revision { get; init; }
    public Vector2 SampleSize { get; init; }
	/// <summary>Neutral renderer transform pivot in sample-normalized coordinates.</summary>
	public Vector2 RotationPivotNormalized { get; init; } = new(0.5f, 0.5f);
    public IReadOnlyList<FragmentObservablePrimitive> Primitives { get; init; } =
        Array.Empty<FragmentObservablePrimitive>();
}

public sealed class FragmentDetectedFeature
{
    public int Id { get; init; }
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
	public List<FragmentFeatureSegment> Segments { get; init; } = new();
    public float Confidence { get; init; }
    public FragmentAnnotationProvenance Provenance { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
}

public sealed class FragmentFeatureSegment
{
	public Vector2 Start { get; init; }
	public Vector2 End { get; init; }
}

public sealed class FragmentCandidateRegion
{
    public int Id { get; init; }
    public Rect2 NormalizedBounds { get; set; }
    public float Confidence { get; init; }
    public FragmentAnnotationProvenance Provenance { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
    public List<int> FeatureIds { get; init; } = new();
}

public sealed class FragmentLockedRegionView
{
	public int RegionId { get; init; }
	public Rect2 NormalizedBounds { get; init; }
	public float RotationDegrees { get; init; }
	public FragmentObservableScan Scan { get; init; }
	public List<FragmentDetectedFeature> Features { get; init; } = new();
}

public sealed class FragmentDetectedStructure
{
    public int Id { get; init; }
	public float Confidence { get; init; }
    public FragmentAnnotationProvenance Provenance { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
	public bool IsPlayerEdited { get; set; }
    public List<int> FeatureIds { get; init; } = new();
}

public sealed class FragmentSignalMetrics
{
    public float SignalToNoise { get; init; }
	public bool IsComplete { get; init; } = true;
	public int ComparisonCount { get; init; }
}

public sealed class FragmentSignalMeasurementReport
{
	public ulong Revision { get; init; }
	public int? TargetRegionId { get; init; }
	public FragmentSignalMetrics Target { get; init; }
	public FragmentSignalMetrics PreviousTarget { get; init; }
}

public enum FragmentProcessingEffect
{
	Initial,
	Improved,
	Degraded,
	LittleChange
}

public sealed class FragmentProcessingHistoryEntry
{
    public int Sequence { get; init; }
    public FragmentAnalysisControlState Configuration { get; init; }
    public FragmentSignalMetrics Metrics { get; init; }
    public FragmentAnalysisActionOrigin Origin { get; init; }
	public int? TargetRegionId { get; init; }
	public FragmentProcessingEffect Effect { get; init; }
	public float Delta { get; init; }
	public bool IsBookmarked { get; set; }
}

public sealed class FragmentProcessingAdjustment
{
	public FragmentAnalysisParameter Parameter { get; init; }
	public bool BoolValue { get; init; }
	public int IntValue { get; init; }
	public string ParameterName { get; init; } = "None";
	public string PreviousValue { get; init; } = "";
	public string ProposedValue { get; init; } = "";
	public string Rationale { get; init; } = "Explore an untested neighbouring configuration";
	public string ConfigurationKey { get; init; } = "";
	public bool IsBacktrack { get; init; }

	public FragmentAnalysisCommand ToCommand(FragmentAnalysisActionOrigin origin) =>
		Parameter switch
		{
			FragmentAnalysisParameter.PolarizationEnabled or
			FragmentAnalysisParameter.SpectralEnabled or
			FragmentAnalysisParameter.SurfaceEnabled or
			FragmentAnalysisParameter.ElectromagneticEnabled or
			FragmentAnalysisParameter.ResonanceEnabled or
			FragmentAnalysisParameter.XRayEnabled =>
				FragmentAnalysisCommand.Toggle(Parameter, BoolValue, origin),
			_ => FragmentAnalysisCommand.Level(Parameter, IntValue, origin)
		};
}

public sealed class FragmentOrientationHypothesis
{
    public int Id { get; init; }
    public float AxisDegrees { get; init; }
    public float Confidence { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
	public int SourceStructureId { get; init; }
	public ulong GeometrySignature { get; init; }
	public bool IsPolarityAmbiguous { get; init; }
	public string Evidence { get; init; } = "Geometric line-axis estimate";
}

public sealed class FragmentRotationCorrection
{
	public int RegionId { get; set; } = -1;
	public int SourceOrientationId { get; init; }
	public float SourceRotationDegrees { get; init; }
	public float RoverDegrees { get; init; }
	public float ProposedDegrees { get; set; }
	public bool IsPlayerAdjusted { get; set; }
	public FragmentAnnotationDisposition Disposition { get; set; } =
		FragmentAnnotationDisposition.Proposed;
}

public sealed class FragmentArrowCandidate
{
    public int Id { get; init; }
    public Vector2 Tail { get; init; }
    public Vector2 Tip { get; init; }
    public float Confidence { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
    public List<int> FeatureIds { get; init; } = new();
	public FragmentAnnotationProvenance Provenance { get; init; } =
		FragmentAnnotationProvenance.Rover;
	public bool IsPlayerDefined { get; init; }
	public string Evidence { get; init; } = "Geometric shaft/head candidate";
	public int RegionId { get; set; } = -1;
}

public sealed class FragmentDirectionInterpretation
{
	public int RegionId { get; init; } = -1;
	public int SourceArrowId { get; init; }
	public int SourceOrientationId { get; init; }
	public Vector2 ScanDirection { get; init; }
	public Vector2 UprightDirection { get; init; }
	public Vector2 WorldGridDirection { get; init; }
	public float UprightCorrectionDegrees { get; init; }
	public float BearingDegrees { get; init; }
	public string CompassLabel { get; init; } = "—";
}

public sealed class FragmentRegionRotationState
{
	public int RegionId { get; init; }
	public Rect2 RegionBounds { get; set; }
	public Vector2 PivotNormalized { get; set; }
	public float Degrees { get; set; }
}

public sealed class FragmentRoverActionStatus
{
    public FragmentRoverActivity Activity { get; init; }
    public string CurrentAction { get; init; } = "None";
    public string NextAction { get; init; } = "None";
    public string CurrentTarget { get; init; } = "None";
    public string MeasuredResult { get; init; } = "No measurement";
    public string LockedParameters { get; init; } = "None";
}

public sealed class FragmentAutonomyState
{
    public FragmentAutonomyMode GlobalMode { get; set; } = FragmentAutonomyMode.Off;
    public bool IsPaused { get; set; }
    public Dictionary<FragmentAutonomyCapability, FragmentAutonomyMode> CapabilityOverrides { get; } = new();
    public Dictionary<FragmentAutonomyCapability, float> YellowReliability { get; } = new();
    public List<FragmentDetectedFeature> DetectedFeatures { get; } = new();
    public int? SelectedFeatureId { get; set; }
	public List<string> RecentActions { get; } = new();
    public List<FragmentCandidateRegion> CandidateRegions { get; } = new();
	public int? SelectedRegionId { get; set; }
	public int? ActiveCropRegionId { get; set; }
	public List<FragmentLockedRegionView> LockedRegionViews { get; } = new();
    public List<FragmentDetectedStructure> DetectedStructures { get; } = new();
	public int? SelectedStructureId { get; set; }
    public List<FragmentProcessingHistoryEntry> PreviousConfigurations { get; } = new();
	public List<FragmentAnalysisParameter> LockedProcessingParameters { get; } = new();
	public List<string> RejectedProcessingConfigurations { get; } = new();
	public bool IsProcessingSearchActive { get; set; }
	public List<FragmentOrientationHypothesis> OrientationHypotheses { get; } = new();
	public int? SelectedOrientationId { get; set; }
	public int? AcceptedOrientationId { get; set; }
	public FragmentLockedRegionView OrientationSourceView { get; set; }
	public FragmentDetectedStructure OrientationSourceStructure { get; set; }
	public FragmentRotationCorrection RotationCorrection { get; set; }
	public List<FragmentRegionRotationState> RegionRotations { get; } = new();
    public List<FragmentArrowCandidate> ArrowCandidates { get; } = new();
	public int? SelectedArrowId { get; set; }
    public int? AcceptedArrowId { get; set; }
    public Vector2? AcceptedWorldDirection { get; set; }
	public FragmentDirectionInterpretation DirectionInterpretation { get; set; }

    public static FragmentAutonomyState CreateDefault(FragmentAutonomySettings settings)
    {
        FragmentAutonomyState state = new()
        {
            GlobalMode = settings?.DefaultMode ?? FragmentAutonomyMode.Off
        };

        float reliability = settings?.DefaultYellowReliability ?? 0.5f;
        foreach (FragmentAutonomyCapability capability in FragmentAutonomyCapabilityCatalog.All)
            state.YellowReliability[capability] = reliability;

        return state;
    }

    public FragmentAutonomyState Clone()
    {
        FragmentAutonomyState clone = new()
        {
            GlobalMode = GlobalMode,
            IsPaused = IsPaused,
            SelectedFeatureId = SelectedFeatureId,
			SelectedRegionId = SelectedRegionId,
			SelectedStructureId = SelectedStructureId,
            ActiveCropRegionId = ActiveCropRegionId,
			IsProcessingSearchActive = IsProcessingSearchActive,
			SelectedOrientationId = SelectedOrientationId,
			AcceptedOrientationId = AcceptedOrientationId,
			RotationCorrection = RotationCorrection == null ? null : new FragmentRotationCorrection
			{
				RegionId = RotationCorrection.RegionId,
				SourceOrientationId = RotationCorrection.SourceOrientationId,
				SourceRotationDegrees = RotationCorrection.SourceRotationDegrees,
				RoverDegrees = RotationCorrection.RoverDegrees,
				ProposedDegrees = RotationCorrection.ProposedDegrees,
				IsPlayerAdjusted = RotationCorrection.IsPlayerAdjusted,
				Disposition = RotationCorrection.Disposition
			},
			SelectedArrowId = SelectedArrowId,
            AcceptedArrowId = AcceptedArrowId,
			AcceptedWorldDirection = AcceptedWorldDirection,
			DirectionInterpretation = DirectionInterpretation == null
				? null
				: new FragmentDirectionInterpretation
				{
					RegionId = DirectionInterpretation.RegionId,
					SourceArrowId = DirectionInterpretation.SourceArrowId,
					SourceOrientationId = DirectionInterpretation.SourceOrientationId,
					ScanDirection = DirectionInterpretation.ScanDirection,
					UprightDirection = DirectionInterpretation.UprightDirection,
					WorldGridDirection = DirectionInterpretation.WorldGridDirection,
					UprightCorrectionDegrees = DirectionInterpretation.UprightCorrectionDegrees,
					BearingDegrees = DirectionInterpretation.BearingDegrees,
					CompassLabel = DirectionInterpretation.CompassLabel
				}
        };
		foreach (FragmentRegionRotationState rotation in RegionRotations)
			clone.RegionRotations.Add(new FragmentRegionRotationState
			{
				RegionId = rotation.RegionId,
				RegionBounds = rotation.RegionBounds,
				PivotNormalized = rotation.PivotNormalized,
				Degrees = rotation.Degrees
			});

        foreach ((FragmentAutonomyCapability capability, FragmentAutonomyMode mode) in CapabilityOverrides)
            clone.CapabilityOverrides[capability] = mode;
        foreach ((FragmentAutonomyCapability capability, float reliability) in YellowReliability)
            clone.YellowReliability[capability] = reliability;
		clone.LockedProcessingParameters.AddRange(LockedProcessingParameters);
		clone.RejectedProcessingConfigurations.AddRange(RejectedProcessingConfigurations);
		clone.RecentActions.AddRange(RecentActions);
        foreach (FragmentDetectedFeature feature in DetectedFeatures)
        {
            clone.DetectedFeatures.Add(new FragmentDetectedFeature
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
            });
        }
        foreach (FragmentCandidateRegion region in CandidateRegions)
        {
            clone.CandidateRegions.Add(new FragmentCandidateRegion
            {
                Id = region.Id,
                NormalizedBounds = region.NormalizedBounds,
                Confidence = region.Confidence,
                Provenance = region.Provenance,
                Disposition = region.Disposition,
                FeatureIds = new List<int>(region.FeatureIds)
            });
        }
		foreach (FragmentLockedRegionView lockedView in LockedRegionViews)
		{
			FragmentLockedRegionView lockedClone = new()
			{
				RegionId = lockedView.RegionId,
				NormalizedBounds = lockedView.NormalizedBounds,
				RotationDegrees = lockedView.RotationDegrees,
				Scan = lockedView.Scan == null ? null : new FragmentObservableScan
				{
					Revision = lockedView.Scan.Revision,
					SampleSize = lockedView.Scan.SampleSize,
					RotationPivotNormalized = lockedView.Scan.RotationPivotNormalized,
					Primitives = CloneObservablePrimitives(lockedView.Scan.Primitives)
				}
			};
			foreach (FragmentDetectedFeature feature in lockedView.Features)
				lockedClone.Features.Add(CloneFeature(feature));
			clone.LockedRegionViews.Add(lockedClone);
		}
        foreach (FragmentDetectedStructure structure in DetectedStructures)
        {
            clone.DetectedStructures.Add(new FragmentDetectedStructure
            {
                Id = structure.Id,
				Confidence = structure.Confidence,
                Provenance = structure.Provenance,
                Disposition = structure.Disposition,
				IsPlayerEdited = structure.IsPlayerEdited,
                FeatureIds = new List<int>(structure.FeatureIds)
            });
        }
        foreach (FragmentProcessingHistoryEntry entry in PreviousConfigurations)
        {
            clone.PreviousConfigurations.Add(new FragmentProcessingHistoryEntry
            {
                Sequence = entry.Sequence,
				Configuration = entry.Configuration == null ? null : new FragmentAnalysisControlState
				{
					PolarizationEnabled = entry.Configuration.PolarizationEnabled,
					PolarizationLevel = entry.Configuration.PolarizationLevel,
					SpectralEnabled = entry.Configuration.SpectralEnabled,
					SpectralLevel = entry.Configuration.SpectralLevel,
					SurfaceEnabled = entry.Configuration.SurfaceEnabled,
					SurfaceLevel = entry.Configuration.SurfaceLevel,
					ElectromagneticEnabled = entry.Configuration.ElectromagneticEnabled,
					ResonanceEnabled = entry.Configuration.ResonanceEnabled,
					XRayEnabled = entry.Configuration.XRayEnabled,
					RotationDegrees = entry.Configuration.RotationDegrees,
					ViewZoom = entry.Configuration.ViewZoom,
					ViewPan = entry.Configuration.ViewPan
				},
				Metrics = entry.Metrics == null ? null : new FragmentSignalMetrics
				{
					SignalToNoise = entry.Metrics.SignalToNoise,
					IsComplete = entry.Metrics.IsComplete,
					ComparisonCount = entry.Metrics.ComparisonCount
				},
				Origin = entry.Origin,
				TargetRegionId = entry.TargetRegionId,
				Effect = entry.Effect,
				Delta = entry.Delta,
				IsBookmarked = entry.IsBookmarked
            });
        }
		foreach (FragmentOrientationHypothesis hypothesis in OrientationHypotheses)
        {
            clone.OrientationHypotheses.Add(new FragmentOrientationHypothesis
            {
                Id = hypothesis.Id,
                AxisDegrees = hypothesis.AxisDegrees,
                Confidence = hypothesis.Confidence,
				Disposition = hypothesis.Disposition,
				SourceStructureId = hypothesis.SourceStructureId,
				GeometrySignature = hypothesis.GeometrySignature,
				IsPolarityAmbiguous = hypothesis.IsPolarityAmbiguous,
				Evidence = hypothesis.Evidence
			});
		}
		if (OrientationSourceView != null)
		{
			clone.OrientationSourceView = new FragmentLockedRegionView
			{
				RegionId = OrientationSourceView.RegionId,
				NormalizedBounds = OrientationSourceView.NormalizedBounds,
				RotationDegrees = OrientationSourceView.RotationDegrees,
				Scan = OrientationSourceView.Scan == null ? null : new FragmentObservableScan
				{
					Revision = OrientationSourceView.Scan.Revision,
					SampleSize = OrientationSourceView.Scan.SampleSize,
					RotationPivotNormalized = OrientationSourceView.Scan.RotationPivotNormalized,
					Primitives = CloneObservablePrimitives(OrientationSourceView.Scan.Primitives)
				}
			};
			foreach (FragmentDetectedFeature feature in OrientationSourceView.Features)
				clone.OrientationSourceView.Features.Add(CloneFeature(feature));
		}
		if (OrientationSourceStructure != null)
		{
			clone.OrientationSourceStructure = new FragmentDetectedStructure
			{
				Id = OrientationSourceStructure.Id,
				Confidence = OrientationSourceStructure.Confidence,
				Provenance = OrientationSourceStructure.Provenance,
				Disposition = OrientationSourceStructure.Disposition,
				IsPlayerEdited = OrientationSourceStructure.IsPlayerEdited,
				FeatureIds = new List<int>(OrientationSourceStructure.FeatureIds)
			};
		}
		foreach (FragmentArrowCandidate candidate in ArrowCandidates)
        {
            clone.ArrowCandidates.Add(new FragmentArrowCandidate
            {
                Id = candidate.Id,
                Tail = candidate.Tail,
                Tip = candidate.Tip,
                Confidence = candidate.Confidence,
                Disposition = candidate.Disposition,
				FeatureIds = new List<int>(candidate.FeatureIds),
				Provenance = candidate.Provenance,
				IsPlayerDefined = candidate.IsPlayerDefined,
				Evidence = candidate.Evidence,
				RegionId = candidate.RegionId
            });
        }

        return clone;
    }

	private static IReadOnlyList<FragmentObservablePrimitive> CloneObservablePrimitives(
		IReadOnlyList<FragmentObservablePrimitive> source)
	{
		List<FragmentObservablePrimitive> clone = new();
		if (source == null) return clone;
		foreach (FragmentObservablePrimitive primitive in source)
		{
			clone.Add(new FragmentObservablePrimitive
			{
				Id = primitive.Id,
				Start = primitive.Start,
				End = primitive.End,
				Color = primitive.Color,
				Width = primitive.Width,
				Intensity = primitive.Intensity
			});
		}
		return clone;
	}

	private static FragmentDetectedFeature CloneFeature(FragmentDetectedFeature feature) => new()
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
}
