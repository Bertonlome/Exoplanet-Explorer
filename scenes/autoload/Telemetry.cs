using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;

namespace Game.Autoload;

public partial class Telemetry : Node
{
    public static Telemetry Instance { get; private set; }
    public static bool EnabledStatic { get; private set; } = false;

    private const double DisplayRefreshIntervalSeconds = 0.25;
    private const int MaxDisplayedScopes = 8;

    public bool Enabled = false;

    private Dictionary<string, double> currentFrameTimes = new();
    private Dictionary<string, double> windowScopeTotals = new();
    private double windowElapsedSeconds = 0.0;
    private int windowFrameCount = 0;
    private string lastDisplayText = "Telemetry enabled\nCollecting samples...";

    private Label displayLabel;
    private CanvasLayer canvasLayer;
    private bool lastF11State = false;
    private bool wasEnabledLastFrame = false;

    public override void _Ready()
    {
        Instance = this;
        // Build a minimal overlay
        canvasLayer = new CanvasLayer();
        AddChild(canvasLayer);

        displayLabel = new Label();
        displayLabel.Position = new Vector2(16, 16);
        displayLabel.Modulate = Colors.White;
        displayLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        displayLabel.AddThemeConstantOverride("outline_size", 4);
        displayLabel.Visible = false;
        canvasLayer.AddChild(displayLabel);
    }

    public override void _Process(double delta)
    {
        // Toggle with F11 (edge detect)
        var f11 = Input.IsKeyPressed(Key.F11);
        if (f11 && !lastF11State)
        {
            Enabled = !Enabled;
            EnabledStatic = Enabled;
        }
        lastF11State = f11;

        if (!Enabled)
        {
            if (wasEnabledLastFrame)
            {
                currentFrameTimes.Clear();
                windowScopeTotals.Clear();
                windowElapsedSeconds = 0.0;
                windowFrameCount = 0;
                lastDisplayText = "Telemetry enabled\nCollecting samples...";
                wasEnabledLastFrame = false;
            }

            if (displayLabel.Visible) displayLabel.Visible = false;
            return;
        }

        if (!wasEnabledLastFrame)
        {
            currentFrameTimes.Clear();
            windowScopeTotals.Clear();
            windowElapsedSeconds = 0.0;
            windowFrameCount = 0;
            lastDisplayText = "Telemetry enabled\nCollecting samples...";
            wasEnabledLastFrame = true;
        }

        displayLabel.Visible = true;

        windowElapsedSeconds += delta;
        windowFrameCount++;

        foreach (var kv in currentFrameTimes)
        {
            if (!windowScopeTotals.ContainsKey(kv.Key))
            {
                windowScopeTotals[kv.Key] = 0.0;
            }

            windowScopeTotals[kv.Key] += kv.Value;
        }

        currentFrameTimes.Clear();

        if (windowElapsedSeconds >= DisplayRefreshIntervalSeconds)
        {
            var averageFrameMs = windowElapsedSeconds > 0.0
                ? (windowElapsedSeconds * 1000.0) / Math.Max(1, windowFrameCount)
                : 0.0;
            var averageFps = windowElapsedSeconds > 0.0
                ? windowFrameCount / windowElapsedSeconds
                : 0.0;
            var text = $"FPS: {averageFps:0}  Avg frame ms: {averageFrameMs:0.0}  Window: {windowElapsedSeconds:0.00}s\n";

            var top = windowScopeTotals
                .OrderByDescending(kv => kv.Value)
                .Take(MaxDisplayedScopes);
            foreach (var kv in top)
            {
                var averageScopeMs = kv.Value / Math.Max(1, windowFrameCount);
                text += $"{kv.Key}: {averageScopeMs:0.00} ms/frame\n";
            }

            lastDisplayText = text;
            windowScopeTotals.Clear();
            windowElapsedSeconds = 0.0;
            windowFrameCount = 0;
        }

        displayLabel.Text = lastDisplayText;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable Scope(string name)
    {
        if (!EnabledStatic || Instance == null) return NullScope.Instance;
        return new ScopeTimer(name);
    }

    private class ScopeTimer : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _sw;

        public ScopeTimer(string name)
        {
            _name = name;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();
            var ms = _sw.Elapsed.TotalMilliseconds;
            if (!Instance.currentFrameTimes.ContainsKey(_name)) Instance.currentFrameTimes[_name] = 0.0;
            Instance.currentFrameTimes[_name] += ms;
        }
    }

    private class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new NullScope();
        public void Dispose() { }
    }
}
