using Godot;
using System.Collections.Generic;

public partial class FragmentCanvas : Control
{
	[Export]
	private FragmentGenerationSettings generationSettings = new();
	[Export]
	private FragmentRockSettings rockSettings = new();

	public enum FilterType
	{
		Polarization,
		Spectral,
		Surface,
		Electromagnetic,
		Resonance
	}
    private List<FragmentLine> _lines = new();
    private readonly List<Vector2[]> _veins = new();
    private Texture2D _rockTexture;

    private bool _layer1 = true;
    private bool _layer2 = false;
    private bool _layer3 = false;
    private bool _layer4 = false;
    private bool _layer5 = false;

    public override void _Ready()
    {
        GenerateFragment();
    }

    public override void _Draw()
    {
        if (_rockTexture != null)
            DrawTextureRect(_rockTexture, new Rect2(Vector2.Zero, Size), false);
        else
            DrawRect(new Rect2(Vector2.Zero, Size), rockSettings.DarkColor, true);

        DrawMineralVeins();

        foreach (FragmentLine line in _lines)
        {
            bool visible =
                (line.Layer == 0 && _layer1) ||
                (line.Layer == 1 && _layer2) ||
                (line.Layer == 2 && _layer3) ||
                (line.Layer == 3 && _layer4) ||
                (line.Layer == 4 && _layer5);

            if (!visible)
                continue;

            if (TryClipLineToCanvas(line.Start, line.End, line.Width, out Vector2 start, out Vector2 end))
                DrawLine(start, end, line.Color, line.Width);
        }
    }

    public void SetLayer(FilterType layer, bool enabled)
    {
        switch (layer)
        {
            case FilterType.Polarization: _layer1 = enabled; break;
            case FilterType.Spectral: _layer2 = enabled; break;
            case FilterType.Surface: _layer3 = enabled; break;
            case FilterType.Electromagnetic: _layer4 = enabled; break;
            case FilterType.Resonance: _layer5 = enabled; break;
        }

        QueueRedraw();
    }

    public void GenerateFragment()
    {
        ulong sampleSeed = generationSettings.RandomizeSeedOnReload
            ? GD.Randi()
            : generationSettings.Seed;

        RandomNumberGenerator rng = new();
        rng.Seed = sampleSeed;

        GenerateRockTexture(unchecked((int)sampleSeed));
        GenerateVeins(sampleSeed);

        _lines.Clear();

        float minimumX = Mathf.Min(generationSettings.StartMinimumX, generationSettings.StartMaximumX) * Size.X;
        float maximumX = Mathf.Max(generationSettings.StartMinimumX, generationSettings.StartMaximumX) * Size.X;
        float minimumY = Mathf.Min(generationSettings.StartMinimumY, generationSettings.StartMaximumY) * Size.Y;
        float maximumY = Mathf.Max(generationSettings.StartMinimumY, generationSettings.StartMaximumY) * Size.Y;

        for (int i = 0; i < generationSettings.LineCount; i++)
        {
            Vector2 start = new(
                rng.RandfRange(minimumX, maximumX),
                rng.RandfRange(minimumY, maximumY)
            );

            Vector2 end = start + new Vector2(
                rng.RandfRange(generationSettings.MinimumOffset.X, generationSettings.MaximumOffset.X),
                rng.RandfRange(generationSettings.MinimumOffset.Y, generationSettings.MaximumOffset.Y)
            );

            _lines.Add(new FragmentLine
            {
                Start = start,
                End = end,
                Layer = rng.RandiRange(0, 4),
                Color = generationSettings.LineColor,
                Width = generationSettings.LineWidth
            });
        }

        QueueRedraw();
    }

    private void GenerateRockTexture(int seed)
    {
        int resolution = Mathf.Clamp(rockSettings.Resolution, 32, 1024);

        FastNoiseLite largeNoise = new()
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = rockSettings.LargeNoiseFrequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = rockSettings.LargeNoiseOctaves
        };

        FastNoiseLite fineNoise = new()
        {
            Seed = seed + 1234,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = rockSettings.FineNoiseFrequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = rockSettings.FineNoiseOctaves
        };

        FastNoiseLite cellularNoise = new()
        {
            Seed = seed + 5678,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            Frequency = rockSettings.CellularFrequency
        };

        Image image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float value = rockSettings.BaseBrightness;
                value += largeNoise.GetNoise2D(x, y) * rockSettings.LargeNoiseStrength;
                value += fineNoise.GetNoise2D(x, y) * rockSettings.FineNoiseStrength;

                // Accentuate cellular borders while keeping them subordinate to the broad rock pattern.
                float crystal = 1f - Mathf.Abs(cellularNoise.GetNoise2D(x, y));
                value += (crystal - 0.5f) * rockSettings.CellularStrength;
                value = Mathf.Clamp(value, 0f, 1f);

                image.SetPixel(x, y, rockSettings.DarkColor.Lerp(rockSettings.LightColor, value));
            }
        }

        _rockTexture = ImageTexture.CreateFromImage(image);
    }

    private void GenerateVeins(ulong seed)
    {
        RandomNumberGenerator rng = new() { Seed = seed ^ 0x9E3779B97F4A7C15UL };
        _veins.Clear();

        float resolution = Mathf.Clamp(rockSettings.Resolution, 32, 1024);
        float minimumStep = Mathf.Min(rockSettings.MinimumStepLength, rockSettings.MaximumStepLength);
        float maximumStep = Mathf.Max(rockSettings.MinimumStepLength, rockSettings.MaximumStepLength);

        for (int veinIndex = 0; veinIndex < rockSettings.VeinCount; veinIndex++)
        {
            Vector2[] points = new Vector2[rockSettings.PointsPerVein];
            Vector2 point = new(rng.RandfRange(0f, resolution), rng.RandfRange(0f, resolution));
            float angle = rng.RandfRange(0f, Mathf.Tau);

            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                points[pointIndex] = point;
                angle += rng.RandfRange(-rockSettings.MaximumTurn, rockSettings.MaximumTurn);
                point += Vector2.FromAngle(angle) * rng.RandfRange(minimumStep, maximumStep);
            }

            _veins.Add(points);
        }
    }

    private void DrawMineralVeins()
    {
        float resolution = Mathf.Clamp(rockSettings.Resolution, 32, 1024);
        Vector2 scale = Size / resolution;

        foreach (Vector2[] vein in _veins)
        {
            for (int i = 0; i < vein.Length - 1; i++)
            {
                Vector2 originalStart = vein[i] * scale;
                Vector2 originalEnd = vein[i + 1] * scale;
                if (!TryClipLineToCanvas(
                    originalStart,
                    originalEnd,
                    rockSettings.FractureWidth,
                    out Vector2 start,
                    out Vector2 end))
                {
                    continue;
                }

                DrawLine(start, end, rockSettings.FractureColor, rockSettings.FractureWidth);
                DrawLine(start, end, rockSettings.DepositColor, rockSettings.DepositWidth);
            }
        }
    }

    private bool TryClipLineToCanvas(
        Vector2 start,
        Vector2 end,
        float width,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        float inset = Mathf.Max(width * 0.5f, 0f);
        float left = inset;
        float top = inset;
        float right = Size.X - inset;
        float bottom = Size.Y - inset;

        clippedStart = start;
        clippedEnd = end;
        if (right < left || bottom < top) return false;

        Vector2 delta = end - start;
        float minimumT = 0f;
        float maximumT = 1f;

        if (!ClipTest(-delta.X, start.X - left, ref minimumT, ref maximumT) ||
            !ClipTest(delta.X, right - start.X, ref minimumT, ref maximumT) ||
            !ClipTest(-delta.Y, start.Y - top, ref minimumT, ref maximumT) ||
            !ClipTest(delta.Y, bottom - start.Y, ref minimumT, ref maximumT))
        {
            return false;
        }

        clippedStart = start + delta * minimumT;
        clippedEnd = start + delta * maximumT;
        return true;
    }

    private static bool ClipTest(float direction, float distance, ref float minimumT, ref float maximumT)
    {
        if (Mathf.IsZeroApprox(direction)) return distance >= 0f;

        float ratio = distance / direction;
        if (direction < 0f)
        {
            if (ratio > maximumT) return false;
            minimumT = Mathf.Max(minimumT, ratio);
        }
        else
        {
            if (ratio < minimumT) return false;
            maximumT = Mathf.Min(maximumT, ratio);
        }

        return true;
    }
}

public class FragmentLine
{
    public Vector2 Start;
    public Vector2 End;

    public int Layer;

    public Color Color;
    public float Width;
}
