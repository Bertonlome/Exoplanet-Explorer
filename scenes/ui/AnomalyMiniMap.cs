using Godot;
using System;
using System.Collections.Generic;

public partial class AnomalyMiniMap : Node3D
{
    // === Exports ===
    [Export] public MultiMeshInstance3D Bars;        // assign in inspector
    [Export] public MeshInstance3D RobotMarker;      // assign in inspector
    [Export] public Camera3D Cam;                    // assign in inspector
    [Export] public DirectionalLight3D Light;        // assign in inspector (optional)
    [Export] public int GridW = 64;                  // Display resolution (reduce for robot window mode)
    [Export] public int GridH = 64;                  // Display resolution (reduce for robot window mode)
    [Export] public float CellSize = 0.15f;          // spacing in minimap world units (wider bars)
    [Export] public float HeightScale = 0.01f;       // height multiplier for better visibility
    [Export] public float MaxBarHeight = 1.0f;       // Maximum bar height in world units (tallest bar)
    [Export] public float MaxValue = 500f;
    [Export] public float Gamma = 0.6f;
    [Export] public bool UsePerspective = true;      // false = ortho
    [Export] public float OrbitSensitivity = 0.01f;
    [Export] public float ZoomSensitivity = 0.12f;
    [Export] public float MinZoomFactor = 0.35f;
    [Export] public float MaxZoomFactor = 3.0f;

    private MultiMesh _mm;
    private Vector3 _cameraTarget;
    private float _cameraDistance;
    private float _initialCameraDistance;
    private float _initialOrthographicSize;
    private float _cameraYaw;
    private float _cameraPitch;
    private Node3D _orientationIndicators;
    private float _robotMarkerStemHeight;

    // Cached “view window” in map tile coords
    private Rect2I _window;            // which map area is shown
    private Vector2I _mapSize;         // full map width/height (tiles)
    private Vector2I _robotCell;       // robot tile for marker
    private float[,] _grid;            // GridW×GridH downsample
    private bool _initialized = false;
    private Mode _currentMode = Mode.FullMap; // Track current mode
    
    // Change detection
    private float[,] _lastGrid;        // Previous grid state for comparison
    private int _refreshCount = 0;     // Track number of refreshes

    public override void _Ready()
    {
        // Don't initialize here if we're going to set custom grid size
        // The Initialize method will call SetupMultiMesh
        // But if no one calls InitFullMap, we should still initialize with defaults
    }
    
    public void SetupMultiMesh()
    {
        // Allow re-initialization to recreate MultiMesh with new grid size
        if (_initialized)
        {
            // Clean up old MultiMesh
            if (_mm != null)
            {
                _mm = null;
            }
        }
        
        //GD.Print($"Setting up MultiMesh with {GridW}x{GridH} = {GridW * GridH} instances");
        
        // Setup MultiMesh with a box/cube - bars are thicker now (95% of cell size)
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            InstanceCount = GridW * GridH,
            Mesh = new BoxMesh { Size = new Vector3(CellSize * 0.95f, 1f, CellSize * 0.95f) } // Y=1; we'll scale
        };
        
        // Create a material with proper shading for 3D depth
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
            VertexColorUseAsAlbedo = true, // Use the colors we set
            Metallic = 0.0f,
            Roughness = 0.7f
        };
        
        Bars.Multimesh = _mm;
        Bars.MaterialOverride = material;
        
        // Add a light if not provided in the scene
        if (Light == null)
        {
            Light = new DirectionalLight3D();
            AddChild(Light);
            Light.GlobalPosition = new Vector3(5, 10, 5);
            Light.LookAt(Vector3.Zero, Vector3.Up);
            Light.LightEnergy = 1.0f;
        }

        ConfigureCamera();
        SetupOrientationIndicators();
        SetupRobotMarker();
        _grid = new float[GridW, GridH];
        // pre-place instances (XZ positions) once
        PreplaceInstances();
        
        _initialized = true;
    }

    private void ConfigureCamera()
    {
        // === Camera Tweaking Guide ===
        // Position: Cam.GlobalPosition = new Vector3(X, Y, Z)
        //   - X: left(-) to right(+) - adjust horizontal viewing angle
        //   - Y: height - higher values = bird's eye view, lower = side view
        //   - Z: distance from center - larger = further back
        // Orientation: Cam.LookAt(target, up_vector)
        //   - target: point to look at (usually center of histogram)
        //   - up_vector: usually Vector3.Up for standard orientation
        // Zoom (Perspective): Cam.Fov (field of view)
        //   - Lower FOV (e.g., 20-30) = zoomed in, narrow view
        //   - Higher FOV (e.g., 50-70) = zoomed out, wide angle
        // Zoom (Orthographic): Cam.Size
        //   - Smaller = zoomed in, larger = zoomed out
        
        if (UsePerspective)
        {
            Cam.Projection = Camera3D.ProjectionType.Perspective;
            // Place camera at an angle to better see height variations
            var extentX = GridW * CellSize * 0.5f;
            var extentZ = GridH * CellSize * 0.5f;
            var maxExtent = MathF.Max(extentX, extentZ);
            GlobalPosition = Vector3.Zero;
            
            // Pull camera back further to see the whole scene
            // Position at a good angle with more distance
            _cameraTarget = new Vector3(-0.25f, 0.85f, 0);
            Cam.GlobalPosition = new Vector3(maxExtent * 2.5f, maxExtent * 3.0f, maxExtent * 4.0f);
            Cam.Fov = 35.0f; // Wider field of view
        }
        else
        {
            Cam.Projection = Camera3D.ProjectionType.Orthogonal;
            var half = MathF.Max(GridW, GridH) * CellSize * 0.55f;
            Cam.Size = half * 2.0f;
            _cameraTarget = Vector3.Zero;
            Cam.GlobalPosition = new Vector3(0, 10f, 0);
        }

        InitializeCameraOrbit();
    }

    private void InitializeCameraOrbit()
    {
        var offset = Cam.GlobalPosition - _cameraTarget;
        _cameraDistance = MathF.Max(offset.Length(), 0.01f);
        _initialCameraDistance = _cameraDistance;
        _initialOrthographicSize = Cam.Size;
        _cameraYaw = MathF.Atan2(offset.X, offset.Z);
        _cameraPitch = MathF.Asin(Mathf.Clamp(offset.Y / _cameraDistance, -1f, 1f));
        _cameraPitch = Mathf.Clamp(_cameraPitch, Mathf.DegToRad(10f), Mathf.DegToRad(85f));
        ApplyCameraOrbit();
    }

    public void OrbitCamera(Vector2 dragDelta)
    {
        if (Cam == null) return;

        _cameraYaw -= dragDelta.X * OrbitSensitivity;
        _cameraPitch = Mathf.Clamp(
            _cameraPitch - dragDelta.Y * OrbitSensitivity,
            Mathf.DegToRad(10f),
            Mathf.DegToRad(85f));

        ApplyCameraOrbit();
    }

    public void ZoomCamera(float wheelSteps)
    {
        if (Cam == null || Mathf.IsZeroApprox(wheelSteps)) return;

        float zoomBase = Mathf.Clamp(1f - ZoomSensitivity, 0.01f, 0.99f);
        float zoomMultiplier = MathF.Pow(zoomBase, wheelSteps);

        if (Cam.Projection == Camera3D.ProjectionType.Orthogonal)
        {
            Cam.Size = Mathf.Clamp(
                Cam.Size * zoomMultiplier,
                _initialOrthographicSize * MinZoomFactor,
                _initialOrthographicSize * MaxZoomFactor);
            return;
        }

        _cameraDistance = Mathf.Clamp(
            _cameraDistance * zoomMultiplier,
            _initialCameraDistance * MinZoomFactor,
            _initialCameraDistance * MaxZoomFactor);
        ApplyCameraOrbit();
    }

    private void ApplyCameraOrbit()
    {
        float horizontalDistance = _cameraDistance * MathF.Cos(_cameraPitch);
        var offset = new Vector3(
            horizontalDistance * MathF.Sin(_cameraYaw),
            _cameraDistance * MathF.Sin(_cameraPitch),
            horizontalDistance * MathF.Cos(_cameraYaw));

        Cam.GlobalPosition = _cameraTarget + offset;
        Cam.LookAt(_cameraTarget, Vector3.Up);
    }

    private void SetupOrientationIndicators()
    {
        if (IsInstanceValid(_orientationIndicators))
        {
            _orientationIndicators.QueueFree();
        }

        _orientationIndicators = new Node3D { Name = "OrientationIndicators" };
        AddChild(_orientationIndicators);

        // Keep the compass spacing proportional to the graph dimensions.
        float extentX = GridW * CellSize * .7f;
        float extentZ = GridH * CellSize * .7f;
        float arrowLength = CellSize * 1.8f;
        float arrowWidth = CellSize * 1.2f;
        float shaftWidth = CellSize * 0.16f;
        float edgeGap = CellSize * 0.95f;
        float labelGap = CellSize * 1.5f;
        float arrowY = 0.04f;

        var north = new Vector3(0, 0, -1);
        var south = new Vector3(0, 0, 1);
        var east = new Vector3(1, 0, 0);
        var west = new Vector3(-1, 0, 0);

        var northCenter = new Vector3(0, arrowY, -(extentZ + edgeGap));
        var southCenter = new Vector3(0, arrowY, extentZ + edgeGap);
        var eastCenter = new Vector3(extentX + edgeGap, arrowY, 0);
        var westCenter = new Vector3(-(extentX + edgeGap), arrowY, 0);

        var arrowMesh = new ImmediateMesh();
        var arrowMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        arrowMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, arrowMaterial);
        var arrowStart = new Vector3(0, arrowY, 0);
        AddArrow(arrowMesh, arrowStart, northCenter, north, arrowLength, arrowWidth, shaftWidth, new Color(1f, 0.25f, 0.2f));
        AddArrow(arrowMesh, arrowStart, southCenter, south, arrowLength, arrowWidth, shaftWidth, Colors.White);
        AddArrow(arrowMesh, arrowStart, eastCenter, east, arrowLength, arrowWidth, shaftWidth, Colors.White);
        AddArrow(arrowMesh, arrowStart, westCenter, west, arrowLength, arrowWidth, shaftWidth, Colors.White);
        arrowMesh.SurfaceEnd();

        _orientationIndicators.AddChild(new MeshInstance3D { Mesh = arrowMesh });

        AddGroundDirectionLabel("N", northCenter + north * (arrowLength * 0.5f + labelGap), new Color(1f, 0.25f, 0.2f));
        AddGroundDirectionLabel("S", southCenter + south * (arrowLength * 0.5f + labelGap), Colors.White);
        AddGroundDirectionLabel("E", eastCenter + east * (arrowLength * 0.5f + labelGap), Colors.White);
        AddGroundDirectionLabel("W", westCenter + west * (arrowLength * 0.5f + labelGap), Colors.White);
    }

    private static void AddArrow(
        ImmediateMesh mesh,
        Vector3 start,
        Vector3 headCenter,
        Vector3 direction,
        float headLength,
        float headWidth,
        float shaftWidth,
        Color color)
    {
        var perpendicular = new Vector3(-direction.Z, 0, direction.X);
        var tip = headCenter + direction * (headLength * 0.5f);
        var headBase = headCenter - direction * (headLength * 0.5f);
        var shaftOffset = perpendicular * (shaftWidth * 0.5f);

        // Shaft: two triangles forming a rectangle from graph center to head.
        AddColoredVertex(mesh, start + shaftOffset, color);
        AddColoredVertex(mesh, start - shaftOffset, color);
        AddColoredVertex(mesh, headBase - shaftOffset, color);

        AddColoredVertex(mesh, start + shaftOffset, color);
        AddColoredVertex(mesh, headBase - shaftOffset, color);
        AddColoredVertex(mesh, headBase + shaftOffset, color);

        // Arrowhead.
        AddColoredVertex(mesh, tip, color);
        AddColoredVertex(mesh, headBase + perpendicular * (headWidth * 0.5f), color);
        AddColoredVertex(mesh, headBase - perpendicular * (headWidth * 0.5f), color);
    }

    private static void AddColoredVertex(ImmediateMesh mesh, Vector3 vertex, Color color)
    {
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(vertex);
    }

    private void AddGroundDirectionLabel(string text, Vector3 position, Color color)
    {
        var label = new Label3D
        {
            Text = text,
            Position = new Vector3(position.X, 0.05f, position.Z),
            RotationDegrees = new Vector3(-90f, 0, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            FixedSize = false,
            DoubleSided = true,
            NoDepthTest = false,
            FontSize = 128,
            PixelSize = CellSize / 30f,
            OutlineSize = 6,
            Modulate = color,
            OutlineModulate = new Color(0.02f, 0.03f, 0.08f, 1f)
        };

        _orientationIndicators.AddChild(label);
    }

    private void SetupRobotMarker()
    {
        if (RobotMarker == null) return;

        foreach (var child in RobotMarker.GetChildren())
        {
            child.QueueFree();
        }

        _robotMarkerStemHeight = MaxBarHeight + CellSize * 2.0f;
        float stemWidth = CellSize * 0.16f;
        float aircraftThickness = CellSize * 0.14f;

        var markerMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.White,
            EmissionEnabled = true,
            Emission = Colors.White,
            EmissionEnergyMultiplier = 2.5f
        };

        RobotMarker.Mesh = new BoxMesh
        {
            Size = new Vector3(stemWidth, _robotMarkerStemHeight, stemWidth)
        };
        RobotMarker.MaterialOverride = markerMaterial;
        RobotMarker.Scale = Vector3.One;

        var aircraft = new Node3D
        {
            Name = "OwnshipAircraft",
            Position = Vector3.Up * (_robotMarkerStemHeight * 0.5f + aircraftThickness * 0.5f)
        };
        RobotMarker.AddChild(aircraft);

        // A simple top-down aircraft silhouette: fuselage, main wings and tail.
        AddAircraftPart(
            aircraft,
            new Vector3(CellSize * 0.28f, aircraftThickness, CellSize * 1.5f),
            Vector3.Zero,
            markerMaterial);
        AddAircraftPart(
            aircraft,
            new Vector3(CellSize * 1.4f, aircraftThickness, CellSize * 0.28f),
            new Vector3(0, 0, -CellSize * 0.12f),
            markerMaterial);
        AddAircraftPart(
            aircraft,
            new Vector3(CellSize * 0.7f, aircraftThickness, CellSize * 0.22f),
            new Vector3(0, 0, CellSize * 0.55f),
            markerMaterial);
    }

    private static void AddAircraftPart(
        Node3D aircraft,
        Vector3 size,
        Vector3 position,
        Material material)
    {
        var part = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = material
        };
        aircraft.AddChild(part);
    }

    private void PreplaceInstances()
    {
        int idx = 0;
        for (int gy = 0; gy < GridH; gy++)
        for (int gx = 0; gx < GridW; gx++, idx++)
        {
            float x = (gx - GridW * 0.5f + 0.5f) * CellSize;
            float z = (gy - GridH * 0.5f + 0.5f) * CellSize;
            // basis with unit height; we'll overwrite Y scale each update
            var basis = new Basis(
                new Vector3(CellSize * 0.95f, 0, 0),
                new Vector3(0, 1f, 0),
                new Vector3(0, 0, CellSize * 0.95f)
            );
            var xform = new Transform3D(basis, new Vector3(x, 0.5f, z)); // temp Y=0.5
            _mm.SetInstanceTransform(idx, xform);
            _mm.SetInstanceColor(idx, new Color(0, 0, 0, 0)); // init invisible
        }
    }

    // === Public API ===
    public void InitFullMap(Vector2I mapSize)
    {
        _mapSize = mapSize;
        _window = new Rect2I(Vector2I.Zero, mapSize);
        
        // Initialize the MultiMesh now that grid size is set
        SetupMultiMesh();
        
        // Initialize change detection grid
        _lastGrid = new float[GridW, GridH];
        
        UpdateRobotMarker(); // place off-grid initially
    }

    public void SetRobotCell(Vector2I robotCell)
    {
        _robotCell = robotCell;
        UpdateRobotMarker();
    }

    public enum Mode { FullMap, RobotWindow }

    public void SetMode(Mode mode, Vector2I windowSizeTiles)
    {
        _currentMode = mode; // Track current mode
        
        if (mode == Mode.FullMap)
        {
            _window = new Rect2I(Vector2I.Zero, _mapSize);
            //GD.Print($"SetMode: FullMap - window covers entire map: {_window}");
        }
        else
        {
            // Center window on robot without clamping - allow negative coordinates
            var origin = new Vector2I(
                _robotCell.X - windowSizeTiles.X / 2,
                _robotCell.Y - windowSizeTiles.Y / 2
            );
            _window = new Rect2I(origin, windowSizeTiles);
            
            //GD.Print($"SetMode: RobotWindow - robot at {_robotCell}, window size {windowSizeTiles}, calculated origin {origin}, final window: {_window}");
        }
        
        //GD.Print($"3D Histogram Mode: {mode} | Window: {_window.Size.X}x{_window.Size.Y} tiles ({_window.Size.X * _window.Size.Y} map tiles) → {GridW}x{GridH} bars ({GridW * GridH} bars)");
    }

    /// <summary>
    /// Update bars from an anomaly sampler. Provide either a dictionary lookup or a function.
    /// </summary>
    public void Refresh(Func<Vector2I, float> sampleAnomaly)
    {
        _refreshCount++;
        //GD.Print($"Refresh #{_refreshCount} called - Window: pos={_window.Position}, size={_window.Size}, robot={_robotCell}, mode={_currentMode}");
        
        // Downsample the current window to GridW×GridH
        DownsampleWindow(sampleAnomaly, _window, _grid);
        
        // Check if the grid values have actually changed
        bool hasChanged = HasGridChanged();
        //GD.Print($"Grid comparison: HAS {(hasChanged ? "" : "NOT ")}CHANGED since last refresh");
        
        // Copy current grid to last grid for next comparison
        CopyGridToLast();
        
        // Push to MultiMesh
        ApplyGridToBars(_grid);
        // Move robot marker within the current window
        UpdateRobotMarker();
    }

    // === Core helpers ===
    private void DownsampleWindow(Func<Vector2I, float> sample, Rect2I win, float[,] outGrid)
    {
        float sx = (float)win.Size.X / GridW;
        float sy = (float)win.Size.Y / GridH;
        
        int sampledCells = 0;
        float minSampled = float.MaxValue;
        float maxSampled = float.MinValue;

        for (int gy = 0; gy < GridH; gy++)
        {
            for (int gx = 0; gx < GridW; gx++)
            {
                int x0 = win.Position.X + (int)(gx * sx);
                int x1 = win.Position.X + Math.Min((int)((gx + 1) * sx), win.Size.X);
                int y0 = win.Position.Y + (int)(gy * sy);
                int y1 = win.Position.Y + Math.Min((int)((gy + 1) * sy), win.Size.Y);

                if (x1 <= x0) x1 = x0 + 1;
                if (y1 <= y0) y1 = y0 + 1;

                // Max-pooling keeps peaks visible; switch to average if you prefer
                float m = 0f;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float v = sample(new Vector2I(x, y));
                    if (v > m) m = v;
                    sampledCells++;
                }
                outGrid[gx, gy] = m;
                
                if (m < minSampled) minSampled = m;
                if (m > maxSampled) maxSampled = m;
            }
        }
        
        //GD.Print($"Downsampled {sampledCells} cells, value range: {minSampled:F2} to {maxSampled:F2}");
    }

    private void ApplyGridToBars(float[,] grid)
    {
        // First pass: find the maximum value in the current grid
        float maxGridValue = 0f;
        for (int gy = 0; gy < GridH; gy++)
        for (int gx = 0; gx < GridW; gx++)
        {
            float v = grid[gx, gy];
            if (v > maxGridValue) maxGridValue = v;
        }
        
        // Prevent division by zero
        if (maxGridValue < 0.001f) maxGridValue = 1f;
        
        int idx = 0;
        int visibleBars = 0;
        float maxHeight = 0f;
        float minHeight = float.MaxValue;
        
        // Second pass: apply normalized heights but keep absolute color gradient
        for (int gy = 0; gy < GridH; gy++)
        for (int gx = 0; gx < GridW; gx++, idx++)
        {
            float v = grid[gx, gy];
            
            // Color based on ABSOLUTE anomaly value (for gradient)
            float nColor = Mathf.Pow(Mathf.Clamp(v / MaxValue, 0f, 1f), Gamma);
            
            // Height NORMALIZED to current max (tallest bar = MaxBarHeight)
            float normalizedHeight = (v / maxGridValue) * MaxBarHeight;
            float h = MathF.Max(normalizedHeight, 0.01f);

            // Track statistics
            if (v > 0.01f) visibleBars++;
            if (h > maxHeight) maxHeight = h;
            if (h < minHeight) minHeight = h;

            // Get current transform and rebuild with correct height
            var xf = _mm.GetInstanceTransform(idx);
            
            // Create new basis with correct X, Z dimensions and Y height
            var basis = new Basis(
                new Vector3(CellSize * 0.95f, 0, 0),
                new Vector3(0, h, 0),
                new Vector3(0, 0, CellSize * 0.95f)
            );
            
            var origin = xf.Origin;
            origin.Y = h * 0.5f;

            _mm.SetInstanceTransform(idx, new Transform3D(basis, origin));
            _mm.SetInstanceColor(idx, HeatColor(nColor)); // Use absolute value for color
        }
        
        //GD.Print($"Bars painted: {visibleBars}/{GridW * GridH} | Height range: {minHeight:F2} to {maxHeight:F2} units | Max grid value: {maxGridValue:F2}");
    }

    private void UpdateRobotMarker()
    {
        if (RobotMarker == null) return;
        if (_window.Size == Vector2I.Zero) { RobotMarker.Visible = false; return; }

        // In RobotWindow mode, robot is ALWAYS centered at (0, 0)
        if (_currentMode == Mode.RobotWindow)
        {
            RobotMarker.Visible = true;
            RobotMarker.GlobalTransform = new Transform3D(
                Basis.Identity,
                new Vector3(0, _robotMarkerStemHeight * 0.5f, 0));
            return;
        }

        // FullMap mode: calculate position based on robot location in map
        // If robot is outside current window, hide
        if (_robotCell.X < _window.Position.X || _robotCell.Y < _window.Position.Y ||
            _robotCell.X >= _window.End.X     || _robotCell.Y >= _window.End.Y)
        {
            RobotMarker.Visible = false;
            return;
        }

        // Map robot to local grid position
        float gx = ((float)(_robotCell.X - _window.Position.X) / _window.Size.X) * GridW - 0.5f;
        float gy = ((float)(_robotCell.Y - _window.Position.Y) / _window.Size.Y) * GridH - 0.5f;

        float x = (gx - GridW * 0.5f + 0.5f) * CellSize;
        float z = (gy - GridH * 0.5f + 0.5f) * CellSize;

        RobotMarker.Visible = true;
        RobotMarker.GlobalTransform = new Transform3D(
            Basis.Identity,
            new Vector3(x, _robotMarkerStemHeight * 0.5f, z));
    }

    private static Color HeatColor(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        if (t < 0.33f) { float k = t / 0.33f; return new Color(0, 0.2f + 0.8f*k, 1, 1); }
        if (t < 0.66f) { float k = (t - 0.33f) / 0.33f; return new Color(k, 1, 1 - 0.5f*k, 1); }
        { float k = (t - 0.66f) / 0.34f; return new Color(1, 1 - 0.7f*k, 0.5f - 0.5f*k, 1); }
    }
    
    // === Change Detection ===
    private bool HasGridChanged()
    {
        if (_lastGrid == null) return true; // First run always counts as changed
        
        const float epsilon = 0.001f; // Threshold for considering values different
        
        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                if (Mathf.Abs(_grid[x, y] - _lastGrid[x, y]) > epsilon)
                {
                    return true; // Found a difference
                }
            }
        }
        
        return false; // No significant changes
    }
    
    private void CopyGridToLast()
    {
        if (_lastGrid == null)
        {
            _lastGrid = new float[GridW, GridH];
        }
        
        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                _lastGrid[x, y] = _grid[x, y];
            }
        }
    }
}
