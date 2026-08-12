using Godot;

[GlobalClass]
public partial class FragmentRockSettings : Resource
{
    [ExportGroup("Texture")]
    [Export(PropertyHint.Range, "32,1024,1")]
    public int Resolution { get; set; } = 256;

    [Export]
    public Color DarkColor { get; set; } = new(0.035f, 0.055f, 0.06f);

    [Export]
    public Color LightColor { get; set; } = new(0.34f, 0.40f, 0.39f);

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BaseBrightness { get; set; } = 0.5f;

    [ExportGroup("Large Mineral Patches")]
    [Export(PropertyHint.Range, "0.001,0.1,0.001")]
    public float LargeNoiseFrequency { get; set; } = 0.015f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int LargeNoiseOctaves { get; set; } = 4;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float LargeNoiseStrength { get; set; } = 0.28f;

    [ExportGroup("Fine Grain")]
    [Export(PropertyHint.Range, "0.005,0.5,0.005")]
    public float FineNoiseFrequency { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int FineNoiseOctaves { get; set; } = 3;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float FineNoiseStrength { get; set; } = 0.1f;

    [ExportGroup("Crystal Grain")]
    [Export(PropertyHint.Range, "0.001,0.5,0.001")]
    public float CellularFrequency { get; set; } = 0.045f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CellularStrength { get; set; } = 0.12f;

    [ExportGroup("Veins")]
    [Export(PropertyHint.Range, "0,50,1")]
    public int VeinCount { get; set; } = 12;

    [Export(PropertyHint.Range, "2,50,1")]
    public int PointsPerVein { get; set; } = 12;

    [Export(PropertyHint.Range, "1,64,0.5")]
    public float MinimumStepLength { get; set; } = 8f;

    [Export(PropertyHint.Range, "1,64,0.5")]
    public float MaximumStepLength { get; set; } = 20f;

    [Export(PropertyHint.Range, "0,3.14,0.01")]
    public float MaximumTurn { get; set; } = 0.35f;

    [Export]
    public Color FractureColor { get; set; } = new(0.01f, 0.02f, 0.02f, 0.65f);

    [Export(PropertyHint.Range, "0.5,20,0.5")]
    public float FractureWidth { get; set; } = 4f;

    [Export]
    public Color DepositColor { get; set; } = new(0.55f, 0.68f, 0.64f, 0.5f);

    [Export(PropertyHint.Range, "0.5,10,0.5")]
    public float DepositWidth { get; set; } = 1.5f;
}
