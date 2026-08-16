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
    View
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
}

public sealed class FragmentAnalysisChange
{
    public FragmentAnalysisControlState Previous { get; init; }
    public FragmentAnalysisControlState Current { get; init; }
    public FragmentAnalysisParameter Parameter { get; init; }
    public FragmentAnalysisActionOrigin Origin { get; init; }
}

public interface IFragmentAnalysisCommandSink
{
    event Action<FragmentAnalysisChange> AnalysisChanged;
    FragmentAnalysisControlState CaptureControlState();
    void DispatchAnalysisCommand(FragmentAnalysisCommand command);
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
	public FragmentObservableScan Scan { get; init; }
	public List<FragmentDetectedFeature> Features { get; init; } = new();
}

public sealed class FragmentDetectedStructure
{
    public int Id { get; init; }
    public FragmentAnnotationProvenance Provenance { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
    public List<int> FeatureIds { get; init; } = new();
}

public sealed class FragmentSignalMetrics
{
    public float SignalToNoise { get; init; }
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

public sealed class FragmentOrientationHypothesis
{
    public int Id { get; init; }
    public float AxisDegrees { get; init; }
    public float Confidence { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
}

public sealed class FragmentArrowCandidate
{
    public int Id { get; init; }
    public Vector2 Tail { get; init; }
    public Vector2 Tip { get; init; }
    public float Confidence { get; init; }
    public FragmentAnnotationDisposition Disposition { get; set; }
    public List<int> FeatureIds { get; init; } = new();
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
    public List<FragmentProcessingHistoryEntry> PreviousConfigurations { get; } = new();
    public List<FragmentOrientationHypothesis> OrientationHypotheses { get; } = new();
    public int? AcceptedOrientationId { get; set; }
    public List<FragmentArrowCandidate> ArrowCandidates { get; } = new();
    public int? AcceptedArrowId { get; set; }
    public Vector2? AcceptedWorldDirection { get; set; }

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
			ActiveCropRegionId = ActiveCropRegionId,
            AcceptedOrientationId = AcceptedOrientationId,
            AcceptedArrowId = AcceptedArrowId,
            AcceptedWorldDirection = AcceptedWorldDirection
        };

        foreach ((FragmentAutonomyCapability capability, FragmentAutonomyMode mode) in CapabilityOverrides)
            clone.CapabilityOverrides[capability] = mode;
        foreach ((FragmentAutonomyCapability capability, float reliability) in YellowReliability)
            clone.YellowReliability[capability] = reliability;
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
				Scan = lockedView.Scan == null ? null : new FragmentObservableScan
				{
					Revision = lockedView.Scan.Revision,
					SampleSize = lockedView.Scan.SampleSize,
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
                Provenance = structure.Provenance,
                Disposition = structure.Disposition,
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
					SignalToNoise = entry.Metrics.SignalToNoise
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
                Disposition = hypothesis.Disposition
            });
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
                FeatureIds = new List<int>(candidate.FeatureIds)
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

public sealed class FragmentAutonomyTruthLine
{
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public FragmentScanChannel Channel { get; init; }
    public bool IsImportant { get; init; }
}

public sealed class FragmentAutonomyTruth
{
    public FragmentGlyphType GlyphType { get; init; }
    public float CorrectRotationDegrees { get; init; }
    public bool CorrectPolarizationEnabled { get; init; }
    public int CorrectPolarizationLevel { get; init; }
    public bool CorrectSpectralEnabled { get; init; }
    public int CorrectSpectralLevel { get; init; }
    public bool CorrectSurfaceEnabled { get; init; }
    public int CorrectSurfaceLevel { get; init; }
    public bool CorrectElectromagneticEnabled { get; init; }
    public bool CorrectResonanceEnabled { get; init; }
    public bool CorrectXRayEnabled { get; init; }
    public Vector2 MonolithDirection { get; init; }
    public IReadOnlyList<FragmentAutonomyTruthLine> SignalLines { get; init; } =
        Array.Empty<FragmentAutonomyTruthLine>();

    public static FragmentAutonomyTruth FromPuzzle(FragmentPuzzle puzzle)
    {
        if (puzzle == null) return null;

        List<FragmentAutonomyTruthLine> signalLines = new();
        foreach (FragmentLine line in puzzle.Lines)
        {
            if (line.Role != FragmentLineRole.Signal) continue;
            signalLines.Add(new FragmentAutonomyTruthLine
            {
                Start = line.Start,
                End = line.End,
                Channel = line.Channel,
                IsImportant = line.IsImportant
            });
        }

        return new FragmentAutonomyTruth
        {
            GlyphType = puzzle.GlyphType,
            CorrectRotationDegrees = puzzle.CorrectRotationDegrees,
            CorrectPolarizationEnabled = puzzle.CorrectPolarizationEnabled,
            CorrectPolarizationLevel = puzzle.CorrectPolarizationLevel,
            CorrectSpectralEnabled = puzzle.CorrectSpectralEnabled,
            CorrectSpectralLevel = puzzle.CorrectSpectralLevel,
            CorrectSurfaceEnabled = puzzle.CorrectSurfaceEnabled,
            CorrectSurfaceLevel = puzzle.CorrectSurfaceLevel,
            CorrectElectromagneticEnabled = puzzle.CorrectElectromagneticEnabled,
            CorrectResonanceEnabled = puzzle.CorrectResonanceEnabled,
            CorrectXRayEnabled = puzzle.CorrectXRayEnabled,
            MonolithDirection = puzzle.MonolithDirection,
            SignalLines = signalLines
        };
    }
}
