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

    [ExportGroup("Randomness")]
    [Export]
    public bool RandomizeSeedOnReload { get; set; } = true;

    [Export]
    public ulong Seed { get; set; } = 12345;
}
