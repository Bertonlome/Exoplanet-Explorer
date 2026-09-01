using System.Collections.Generic;
using Godot;

public enum FragmentLineRole
{
    Signal,
    Distractor
}

public enum FragmentScanChannel
{
    Electromagnetic,
    Resonance,
    XRay
}

public enum FragmentGlyphType
{
    Hominid,
    Key,
    Television
}

public enum FragmentDistractorGlyphType
{
    None,
    Trident,
    DiamondEye,
    AngularSpiral,
    HominidDecoy,
    KeyDecoy,
    TelevisionDecoy
}

public sealed class FragmentPuzzle
{
    public ulong Seed { get; init; }
    public Vector2 ReferenceSize { get; init; }
    public Vector2I FragmentPosition { get; init; }
    public Vector2I MonolithPosition { get; init; }
    public Vector2 MonolithDirection { get; init; }
    public FragmentGlyphType GlyphType { get; init; }
    public Vector2 FigureCenter { get; init; }
    public float InitialRotationDegrees { get; init; }
    public float CorrectRotationDegrees { get; init; }
    public bool CorrectPolarizationEnabled { get; init; }
    public bool CorrectSpectralEnabled { get; init; }
    public bool CorrectSurfaceEnabled { get; init; }
    public int CorrectPolarizationLevel { get; init; }
    public int CorrectSpectralLevel { get; init; }
    public int CorrectSurfaceLevel { get; init; }
    public bool CorrectElectromagneticEnabled { get; init; }
    public bool CorrectResonanceEnabled { get; init; }
    public bool CorrectXRayEnabled { get; init; }
    public List<FragmentLine> Lines { get; } = new();
    public List<FragmentVein> Veins { get; } = new();
    public List<Vector2> ImportantPoints { get; } = new();
    public List<FragmentDistractorGlyph> DistractorGlyphs { get; } = new();

    public FragmentDistractorGlyph GetDistractorGlyph(FragmentDistractorGlyphType glyphType)
    {
        return DistractorGlyphs.Find(glyph => glyph.GlyphType == glyphType);
    }

    public bool IsCorrectFilterCombination(bool electromagnetic, bool resonance, bool xRay)
    {
        return electromagnetic == CorrectElectromagneticEnabled &&
            resonance == CorrectResonanceEnabled &&
            xRay == CorrectXRayEnabled;
    }

    public bool IsCorrectProcessingCombination(
        bool polarizationEnabled,
        int polarizationLevel,
        bool spectralEnabled,
        int spectralLevel,
        bool surfaceEnabled,
        int surfaceLevel)
    {
        return IsProcessorCorrect(
                polarizationEnabled,
                polarizationLevel,
                CorrectPolarizationEnabled,
                CorrectPolarizationLevel) &&
            IsProcessorCorrect(
                spectralEnabled,
                spectralLevel,
                CorrectSpectralEnabled,
                CorrectSpectralLevel) &&
            IsProcessorCorrect(
                surfaceEnabled,
                surfaceLevel,
                CorrectSurfaceEnabled,
                CorrectSurfaceLevel);
    }

    private static bool IsProcessorCorrect(bool enabled, int level, bool correctEnabled, int correctLevel)
    {
        return enabled == correctEnabled && (!correctEnabled || level == correctLevel);
    }
}

public sealed class FragmentDistractorGlyph
{
    public FragmentDistractorGlyphType GlyphType { get; init; }
    public bool CorrectPolarizationEnabled { get; init; }
    public bool CorrectSpectralEnabled { get; init; }
    public bool CorrectSurfaceEnabled { get; init; }
    public int CorrectPolarizationLevel { get; init; }
    public int CorrectSpectralLevel { get; init; }
    public int CorrectSurfaceLevel { get; init; }
    public bool CorrectElectromagneticEnabled { get; init; }
    public bool CorrectResonanceEnabled { get; init; }
    public bool CorrectXRayEnabled { get; init; }
}

public sealed class FragmentLine
{
    public Vector2 Start;
    public Vector2 End;
    public FragmentLineRole Role;
    public FragmentDistractorGlyphType DistractorGlyphType;
    public bool HasCustomRotationCenter;
    public Vector2 RotationCenter;
    public FragmentScanChannel Channel;
    public bool IsImportant;
    public bool RequiresCorrectCombination;
    public bool RevealedInCorrectCombination;
    public Color Color;
    public float Width;
    public float RevealThreshold;
    public List<Vector2> VisibleIntervals = new();
}

public sealed class FragmentVein
{
    public Vector2[] NormalizedPoints = System.Array.Empty<Vector2>();
    public float Opacity;
}
