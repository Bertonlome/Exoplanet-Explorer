using Godot;

[GlobalClass]
public partial class FragmentGenerationSettings : Resource
{
    [ExportGroup("Lines")]
    [Export(PropertyHint.Range, "1,500,1")]
    public int LineCount { get; set; } = 40;

    [Export(PropertyHint.Range, "0.5,20,0.5")]
    public float LineWidth { get; set; } = 2f;

    [Export]
    public Color LineColor { get; set; } = new(0.3f, 0.9f, 1f);

    [Export]
    public Color DistractorColor { get; set; } = new(0.75f, 0.48f, 0.22f);

    [ExportGroup("Feature Composition")]
    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float SignalFraction { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ImportantSignalFraction { get; set; } = 0.18f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SolutionLockedSignalFraction { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CorrectCombinationDistractorFraction { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ElectromagneticChannelFraction { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,0.9,0.01")]
    public float XRayChannelFraction { get; set; } = 0.33f;

    [ExportGroup("True Pattern")]
    [Export]
    public Vector2 PatternCenter { get; set; } = new(0.5f, 0.5f);

    [Export]
    public bool RandomizePatternPosition { get; set; } = true;

    [Export]
    public Vector2 PatternCenterMinimum { get; set; } = Vector2.Zero;

    [Export]
    public Vector2 PatternCenterMaximum { get; set; } = Vector2.One;

    [Export(PropertyHint.Range, "0.05,0.45,0.01")]
    public float PatternRadius { get; set; } = 0.22f;

    [Export]
    public Vector2 PatternAspect { get; set; } = new(0.38f, 0.18f);

    [Export(PropertyHint.Range, "0.1,0.9,0.01")]
    public float InnerRectangleScale { get; set; } = 0.48f;

    [Export(PropertyHint.Range, "0.2,0.9,0.01")]
    public float LongLegLengthMultiplier { get; set; } = 0.72f;

    [Export(PropertyHint.Range, "0.1,0.9,0.01")]
    public float BranchPosition { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.05,0.5,0.01")]
    public float BranchLengthMultiplier { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "0.05,0.5,0.01")]
    public float BranchDropLengthMultiplier { get; set; } = 0.22f;

    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float DirectionArrowLengthMultiplier { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "0.05,0.6,0.01")]
    public float DirectionArrowHeadMultiplier { get; set; } = 0.2f;

    [ExportGroup("Inactive Layers")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float InactiveOpacity { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "0,0.95,0.01")]
    public float InactiveErasedFraction { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "1,30,1")]
    public int InactiveErasureSections { get; set; } = 6;

    [ExportGroup("Filter Effects")]
    [Export(PropertyHint.Range, "1,8,0.1")]
    public float SignalEnhancementWidthMultiplier { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SignalEnhancementColorStrength { get; set; } = 0.55f;

    [Export]
    public Color SignalEnhancementColor { get; set; } = new(1f, 0.88f, 0.3f);

    [Export(PropertyHint.Range, "0,0.9,0.01")]
    public float SurfaceBackgroundDarkening { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SurfaceNoiseOpacityMultiplier { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SurfaceSignalColorStrength { get; set; } = 0.35f;

    [Export]
    public Color SurfaceSignalColor { get; set; } = Colors.White;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float OneStepEffectStrength { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "-1,0,0.01")]
    public float DetrimentalEffectStrength { get; set; } = -0.65f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DetrimentalSignalOpacity { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "1,4,0.1")]
    public float DetrimentalNoiseMultiplier { get; set; } = 1.8f;

    [ExportGroup("Progressive Reconstruction")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TwoStepMatchScore { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float RequiredProcessorBypassScore { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float IncorrectScanChannelPenalty { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SignalRevealThresholdMinimum { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SignalRevealThresholdMaximum { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ImportantSignalRevealThresholdMinimum { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ImportantSignalRevealThresholdMaximum { get; set; } = 0.88f;

    [Export(PropertyHint.Range, "0.01,0.5,0.01")]
    public float RevealTransitionWidth { get; set; } = 0.18f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float NoiseOpacityAtFullReconstruction { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DistractorOpacityAtFullReconstruction { get; set; } = 0.25f;

    [ExportGroup("Distractor Glyphs")]
    [Export(PropertyHint.Range, "0.2,1,0.05")]
    public float DistractorGlyphScale { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "1,4,0.1")]
    public float DistractorInactiveOpacityMultiplier { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.5,1.2,0.05")]
    public float RealDecoyGlyphScale { get; set; } = 0.9f;

    [ExportGroup("Viewport Navigation")]
    [Export(PropertyHint.Range, "1,4,0.25")]
    public float CanvasSizeMultiplier { get; set; } = 2f;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float MinimumViewZoom { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "1,10,0.1")]
    public float MaximumViewZoom { get; set; } = 4f;

    [Export(PropertyHint.Range, "1.01,2,0.01")]
    public float ViewZoomFactor { get; set; } = 1.15f;

    [Export(PropertyHint.Range, "1,200,1")]
    public float ViewPanStep { get; set; } = 40f;

    [ExportGroup("Correct Processing Combination")]
    [Export]
    public bool RandomizeCorrectProcessingCombination { get; set; } = true;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ProcessingEnabledProbability { get; set; } = 0.75f;

    [Export]
    public bool AllowNoProcessingSolution { get; set; } = false;

    [Export]
    public bool CorrectPolarizationEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "1,5,1")]
    public int CorrectPolarizationLevel { get; set; } = 3;

    [Export]
    public bool CorrectSpectralEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "1,5,1")]
    public int CorrectSpectralLevel { get; set; } = 3;

    [Export]
    public bool CorrectSurfaceEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "1,5,1")]
    public int CorrectSurfaceLevel { get; set; } = 3;

    [ExportGroup("Placement")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float StartMinimumX { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float StartMaximumX { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float StartMinimumY { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float StartMaximumY { get; set; } = 0.5f;

    [ExportGroup("Line Offset")]
    [Export]
    public Vector2 MinimumOffset { get; set; } = new(-70f, -70f);

    [Export]
    public Vector2 MaximumOffset { get; set; } = new(70f, 70f);

    [ExportGroup("Correct Scan Combination")]
    [Export]
    public bool RandomizeCorrectChannelCombination { get; set; } = true;

    [Export]
    public bool AllowNoChannelsSolution { get; set; } = false;

    [Export]
    public bool AllowAllChannelsSolution { get; set; } = false;

    [Export]
    public bool CorrectElectromagneticEnabled { get; set; } = true;

    [Export]
    public bool CorrectResonanceEnabled { get; set; } = true;

    [Export]
    public bool CorrectXRayEnabled { get; set; } = false;

    [ExportGroup("Rotation")]
    [Export]
    public bool RandomizeInitialRotation { get; set; } = true;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float InitialRotationDegrees { get; set; } = 0f;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float InitialRotationMinimumDegrees { get; set; } = -180f;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float InitialRotationMaximumDegrees { get; set; } = 180f;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float CorrectRotationDegrees { get; set; } = 0f;

    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float CorrectRotationToleranceDegrees { get; set; } = 3f;

    [ExportGroup("Randomness")]
    [Export]
    public bool RandomizeSeedOnReload { get; set; } = true;

    [Export]
    public ulong Seed { get; set; } = 12345;
}
