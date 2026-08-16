using Godot;

[GlobalClass]
public partial class FragmentAutonomySettings : Resource
{
    [ExportGroup("Allocation")]
    [Export]
    public FragmentAutonomyMode DefaultMode { get; set; } = FragmentAutonomyMode.Off;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float DefaultYellowReliability { get; set; } = 0.5f;

    [ExportGroup("Timing")]
    [Export(PropertyHint.Range, "0,5,0.1")]
    public float ActionPreviewSeconds { get; set; } = 1f;

    [Export(PropertyHint.Range, "0.1,5,0.1")]
    public float NavigationDurationSeconds { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "0.1,5,0.1")]
    public float RotationDurationSeconds { get; set; } = 0.6f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float MeasurementDebounceSeconds { get; set; } = 0.12f;

    [ExportGroup("History")]
    [Export(PropertyHint.Range, "1,2048,1")]
    public int MaximumHistoryEntries { get; set; } = 256;

	[Export(PropertyHint.Range, "0,0.25,0.005")]
	public float ProcessingEffectThreshold { get; set; } = 0.02f;

    [ExportGroup("Overlay Colours")]
    [Export]
	public Color RoverFeatureColor { get; set; } = new(1f, 0.15f, 0.75f, 0.95f);

	[Export]
	public Color AcceptedRoverFeatureColor { get; set; } = new(1f, 0.72f, 0.1f, 0.98f);

	[Export]
	public Color PendingFeatureColor { get; set; } = new(0.15f, 0.95f, 1f, 1f);

    [Export]
    public Color PlayerFeatureColor { get; set; } = new(0.25f, 1f, 0.45f, 0.9f);

    [Export]
    public Color CandidateRegionColor { get; set; } = new(1f, 0.7f, 0.15f, 0.35f);

    [Export]
    public Color StructureColor { get; set; } = new(1f, 0.2f, 0.85f, 0.9f);

    [Export]
    public Color NavigationTargetColor { get; set; } = new(1f, 1f, 1f, 0.9f);
}
