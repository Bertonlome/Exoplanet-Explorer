using Godot;

public sealed class FragmentAnalysisState
{
    public ulong PuzzleSeed { get; init; }
    public FragmentGlyphType GlyphType { get; init; }

    public bool PolarizationEnabled { get; init; }
    public bool SpectralEnabled { get; init; }
    public bool SurfaceEnabled { get; init; }
    public bool ElectromagneticEnabled { get; init; }
    public bool ResonanceEnabled { get; init; }
    public bool XRayEnabled { get; init; }

    public int PolarizationLevel { get; init; }
    public int SpectralLevel { get; init; }
    public int SurfaceLevel { get; init; }

    public float RotationDegrees { get; init; }
    public float ViewZoom { get; init; } = 1f;
    public Vector2 ViewPan { get; init; }
    public bool WasSolved { get; init; }
    public bool WasEverSolved { get; init; }
	public bool WasCompleted { get; init; }
    public FragmentAnalysisActionOrigin InitiationOrigin { get; init; }
    public FragmentAutonomyState RoverState { get; init; }
}
