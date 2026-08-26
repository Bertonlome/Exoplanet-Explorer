using System;
using System.Collections.Generic;
using Godot;

public partial class FragmentCanvas : Control, IFragmentObservationSource
{
    [Export]
    private FragmentGenerationSettings generationSettings = new();

    [Export]
    private FragmentRockSettings rockSettings = new();

    [Signal]
    public delegate void PuzzleStateChangedEventHandler(bool filterCombinationCorrect, bool rotationCorrect);

    public enum FilterType
    {
        Polarization,
        Spectral,
        Surface,
        Electromagnetic,
        Resonance,
        XRay
    }

    private Texture2D _rockTexture;
    private float _displayRotationDegrees;
    private bool _polarizationEnabled;
    private bool _spectralEnabled;
    private bool _surfaceEnabled;
    private bool _electromagneticEnabled;
    private bool _resonanceEnabled;
    private bool _xRayEnabled;
    private int _polarizationLevel = 3;
    private int _spectralLevel = 3;
    private int _surfaceLevel = 3;
    private Vector2I _fragmentPosition;
    private Vector2I _monolithPosition;
    private FragmentGlyphType _glyphType;
    private float _viewZoom = 1f;
    private Vector2 _viewPan;
    private bool _isViewDragging;
    private ulong _observationRevision;
    private List<FragmentObservablePrimitive> _observablePrimitiveCollector;
    private int _nextObservablePrimitiveId;
	private bool _isViewNavigationActive;
	private float _viewNavigationElapsed;
	private float _viewNavigationDuration;
	private Vector2 _navigationStartPan;
	private Vector2 _navigationTargetPan;
	private float _navigationStartZoom;
	private float _navigationTargetZoom;
	private Rect2 _navigationTargetBounds;
	private sealed class RegionRotation
	{
		public int RegionId;
		public Rect2 SelectionBounds;
		public Vector2 PivotNormalized;
		public float Degrees;
		public readonly HashSet<FragmentLine> Lines = new();
	}
	private readonly List<RegionRotation> _regionRotations = new();

    public FragmentPuzzle Puzzle { get; private set; }
    public float ViewZoom => _viewZoom;
    public Vector2 ViewPan => _viewPan;
	public Vector2 ObservableSampleSize => GetVirtualCanvasSize();

    public event Action<float, Vector2, FragmentAnalysisActionOrigin> ViewChanged;
	public event Action ViewNavigationCompleted;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float DisplayRotationDegrees
    {
        get => _displayRotationDegrees;
        set
        {
            _displayRotationDegrees = Mathf.Wrap(value, -180f, 180f);
            _observationRevision++;
            QueueRedraw();
            EmitPuzzleStateChanged();
        }
    }

    public override void _Ready()
    {
        generationSettings ??= new FragmentGenerationSettings();
        rockSettings ??= new FragmentRockSettings();
		ProcessMode = ProcessModeEnum.Always;
    }

	public override void _Process(double delta)
	{
		if (!_isViewNavigationActive) return;
		_viewNavigationElapsed += (float)delta;
		float linearProgress = Mathf.Clamp(
			_viewNavigationElapsed / MathF.Max(_viewNavigationDuration, 0.001f),
			0f,
			1f);
		float easedProgress = linearProgress < 0.5f
			? 4f * linearProgress * linearProgress * linearProgress
			: 1f - MathF.Pow(-2f * linearProgress + 2f, 3f) * 0.5f;
		ApplyNavigationProgress(easedProgress);
		if (linearProgress >= 1f) CompleteViewNavigation();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			_observationRevision++;
			if (_isViewNavigationActive)
				CalculateFocusedView(
					_navigationTargetBounds,
					out _navigationTargetZoom,
					out _navigationTargetPan);
		}
	}

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton) return;

        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
                _isViewDragging = true;
            else
                _isViewDragging = false;

            AcceptEvent();
            return;
        }

        if (!mouseButton.Pressed) return;

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        {
            ZoomViewAt(mouseButton.Position, MathF.Max(generationSettings.ViewZoomFactor, 1.01f));
            AcceptEvent();
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        {
            ZoomViewAt(mouseButton.Position, 1f / MathF.Max(generationSettings.ViewZoomFactor, 1.01f));
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!IsVisibleInTree())
        {
            _isViewDragging = false;
            return;
        }

        if (_isViewDragging && inputEvent is InputEventMouseMotion mouseMotion)
        {
			CancelViewNavigation();
            // Camera-style grab: the sampled fragment follows the mouse movement.
            _viewPan += mouseMotion.Relative;
            ClampViewPan();
            NotifyViewChanged(FragmentAnalysisActionOrigin.Player);
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isViewDragging &&
            inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            !mouseButton.Pressed)
        {
            _isViewDragging = false;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo) return;

        float panStep = MathF.Max(generationSettings.ViewPanStep, 1f);
        Vector2 panDelta = keyEvent.Keycode switch
        {
            Key.Left => Vector2.Right * panStep,
            Key.Right => Vector2.Left * panStep,
            Key.Up => Vector2.Down * panStep,
            Key.Down => Vector2.Up * panStep,
            _ => Vector2.Zero
        };

        if (panDelta == Vector2.Zero) return;
		CancelViewNavigation();
        _viewPan += panDelta;
        ClampViewPan();
        NotifyViewChanged(FragmentAnalysisActionOrigin.Player);
        QueueRedraw();
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        DrawRockBackground();

        float surfaceQuality = GetProcessingQuality(
            _surfaceEnabled,
            _surfaceLevel,
            Puzzle?.CorrectSurfaceEnabled ?? false,
            Puzzle?.CorrectSurfaceLevel ?? 3);

        if (surfaceQuality > 0f)
        {
            Color contrastOverlay = Colors.Black;
            contrastOverlay.A = Mathf.Clamp(
                generationSettings.SurfaceBackgroundDarkening * surfaceQuality,
                0f,
                0.9f);
            DrawRect(new Rect2(Vector2.Zero, Size), contrastOverlay, true);
        }
        else if (surfaceQuality < 0f)
        {
            Color washout = rockSettings.LightColor;
            washout.A = Mathf.Clamp(-surfaceQuality * 0.2f, 0f, 0.35f);
            DrawRect(new Rect2(Vector2.Zero, Size), washout, true);
        }

        if (Puzzle == null) return;

        float processingScore = GetProcessingReconstructionScore();
        float scanScore = GetScanReconstructionScore();
        float reconstructionQuality = processingScore * scanScore;

        DrawMineralVeins(reconstructionQuality);

        foreach (FragmentLine line in Puzzle.Lines)
            DrawPuzzleLine(line, reconstructionQuality, surfaceQuality);
    }

    public void SetFilter(FilterType filter, bool enabled)
    {
        switch (filter)
        {
            case FilterType.Polarization: _polarizationEnabled = enabled; break;
            case FilterType.Spectral: _spectralEnabled = enabled; break;
            case FilterType.Surface: _surfaceEnabled = enabled; break;
            case FilterType.Electromagnetic: _electromagneticEnabled = enabled; break;
            case FilterType.Resonance: _resonanceEnabled = enabled; break;
            case FilterType.XRay: _xRayEnabled = enabled; break;
        }

        _observationRevision++;
        QueueRedraw();
        EmitPuzzleStateChanged();
    }

    public void SetProcessingLevel(FilterType filter, int level)
    {
        int clampedLevel = Mathf.Clamp(level, 1, 5);
        switch (filter)
        {
            case FilterType.Polarization: _polarizationLevel = clampedLevel; break;
            case FilterType.Spectral: _spectralLevel = clampedLevel; break;
            case FilterType.Surface: _surfaceLevel = clampedLevel; break;
            default: return;
        }

        _observationRevision++;
        QueueRedraw();
        EmitPuzzleStateChanged();
    }

	public void SetProcessingConfiguration(FragmentAnalysisControlState configuration)
	{
		if (configuration == null) return;
		_polarizationEnabled = configuration.PolarizationEnabled;
		_polarizationLevel = Mathf.Clamp(configuration.PolarizationLevel, 1, 5);
		_spectralEnabled = configuration.SpectralEnabled;
		_spectralLevel = Mathf.Clamp(configuration.SpectralLevel, 1, 5);
		_surfaceEnabled = configuration.SurfaceEnabled;
		_surfaceLevel = Mathf.Clamp(configuration.SurfaceLevel, 1, 5);
		_electromagneticEnabled = configuration.ElectromagneticEnabled;
		_resonanceEnabled = configuration.ResonanceEnabled;
		_xRayEnabled = configuration.XRayEnabled;
		_observationRevision++;
		QueueRedraw();
		EmitPuzzleStateChanged();
	}

    // Kept as a compatibility alias for existing callers.
    public void SetLayer(FilterType filter, bool enabled) => SetFilter(filter, enabled);

    public void SetPuzzleRotationDegrees(float degrees) => DisplayRotationDegrees = degrees;

	public float GetRegionRotationDegrees(int regionId) =>
		_regionRotations.Find(rotation => rotation.RegionId == regionId)?.Degrees ?? 0f;

	public void SetRegionRotationDegrees(
		int regionId,
		Rect2 normalizedBounds,
		Vector2 pivotNormalized,
		float degrees)
	{
		if (Puzzle == null || regionId < 0) return;
		RegionRotation rotation = _regionRotations.Find(candidate => candidate.RegionId == regionId);
		if (rotation == null)
		{
			rotation = new RegionRotation
			{
				RegionId = regionId,
				SelectionBounds = normalizedBounds,
				PivotNormalized = pivotNormalized.Clamp(Vector2.Zero, Vector2.One)
			};
			foreach (FragmentLine line in Puzzle.Lines)
			{
				// A rendered stroke acquires one orientation owner. A later overlapping R# may
				// capture other strokes, but cannot rotate geometry already committed to R1.
				if (_regionRotations.Exists(existing => existing.Lines.Contains(line))) continue;
				Vector2 center = line.HasCustomRotationCenter ? line.RotationCenter : Puzzle.FigureCenter;
				Vector2 start = TransformPuzzlePointToVirtual(line.Start, center, line);
				Vector2 end = TransformPuzzlePointToVirtual(line.End, center, line);
				Vector2 size = GetVirtualCanvasSize();
				Vector2 normalizedStart = start / size;
				Vector2 normalizedEnd = end / size;
				if (SegmentIntersectsRect(normalizedStart, normalizedEnd, normalizedBounds))
					rotation.Lines.Add(line);
			}
			_regionRotations.Add(rotation);
		}
		rotation.SelectionBounds = normalizedBounds;
		rotation.PivotNormalized = pivotNormalized.Clamp(Vector2.Zero, Vector2.One);
		rotation.Degrees = Mathf.Wrap(degrees, -180f, 180f);
		_observationRevision++;
		QueueRedraw();
		EmitPuzzleStateChanged();
	}

    public void SetSpatialContext(
        Vector2I fragmentPosition,
        Vector2I monolithPosition,
        FragmentGlyphType glyphType = FragmentGlyphType.Hominid)
    {
        _fragmentPosition = fragmentPosition;
        _monolithPosition = monolithPosition;
        _glyphType = glyphType;
    }

    public bool IsCorrectFilterCombination()
    {
        return Puzzle != null && Puzzle.IsCorrectFilterCombination(
            _electromagneticEnabled,
            _resonanceEnabled,
            _xRayEnabled);
    }

    public bool IsCorrectProcessingCombination()
    {
        return Puzzle != null && Puzzle.IsCorrectProcessingCombination(
            _polarizationEnabled,
            _polarizationLevel,
            _spectralEnabled,
            _spectralLevel,
            _surfaceEnabled,
            _surfaceLevel);
    }

    public bool IsAtCorrectRotation()
    {
        if (Puzzle == null) return false;
		if (RotationMatches(DisplayRotationDegrees)) return true;
		int signalCount = 0;
		foreach (FragmentLine line in Puzzle.Lines)
			if (line.Role == FragmentLineRole.Signal) signalCount++;
		int requiredMembership = Math.Max(1, Mathf.CeilToInt(signalCount * 0.6f));
		foreach (RegionRotation rotation in _regionRotations)
		{
			int membership = 0;
			foreach (FragmentLine line in rotation.Lines)
				if (line.Role == FragmentLineRole.Signal) membership++;
			if (membership >= requiredMembership &&
				RotationMatches(DisplayRotationDegrees + rotation.Degrees)) return true;
		}
		return false;

		bool RotationMatches(float degrees)
		{
			float difference = Mathf.Abs(Mathf.RadToDeg(Mathf.AngleDifference(
				Mathf.DegToRad(degrees),
				Mathf.DegToRad(Puzzle.CorrectRotationDegrees))));
			return difference <= generationSettings.CorrectRotationToleranceDegrees;
		}
    }

    public bool IsPuzzleSolved() =>
        IsCorrectFilterCombination() && IsCorrectProcessingCombination() && IsAtCorrectRotation();

    public void GenerateFragment()
    {
        ulong sampleSeed = generationSettings.RandomizeSeedOnReload
            ? GD.Randi()
            : generationSettings.Seed;

        GenerateFragmentFromSeed(sampleSeed);
    }

    public void GenerateFragmentFromSeed(ulong sampleSeed)
    {
        Puzzle = FragmentPuzzleGenerator.Generate(
            generationSettings,
            rockSettings,
            GetVirtualCanvasSize(),
            sampleSeed,
            _fragmentPosition,
            _monolithPosition,
            _glyphType);
		_regionRotations.Clear();
        _displayRotationDegrees = Puzzle.InitialRotationDegrees;
        _viewZoom = 1f;
        _viewPan = Vector2.Zero;
        _observationRevision++;
        GenerateRockTexture(unchecked((int)sampleSeed));
        QueueRedraw();
        EmitPuzzleStateChanged();
    }

    public void RestoreView(
        float zoom,
        Vector2 pan,
        FragmentAnalysisActionOrigin origin = FragmentAnalysisActionOrigin.Restore)
    {
        float minimumZoom = GetMinimumViewZoom();
        float maximumZoom = MathF.Max(generationSettings.MaximumViewZoom, minimumZoom);
        _viewZoom = Mathf.Clamp(zoom, minimumZoom, maximumZoom);
        _viewPan = pan;
        ClampViewPan();
        NotifyViewChanged(origin);
        QueueRedraw();
    }

    public FragmentObservableScan CaptureObservableScan()
    {
        if (Puzzle == null)
        {
            return new FragmentObservableScan
            {
                Revision = _observationRevision,
                SampleSize = GetVirtualCanvasSize(),
				RotationPivotNormalized = new Vector2(0.5f, 0.5f)
            };
        }

		List<FragmentObservablePrimitive> primitives = new();
		_observablePrimitiveCollector = primitives;
		_nextObservablePrimitiveId = 1;
		try
		{
			float surfaceQuality = GetProcessingQuality(
				_surfaceEnabled,
				_surfaceLevel,
				Puzzle.CorrectSurfaceEnabled,
				Puzzle.CorrectSurfaceLevel);
			float reconstructionQuality =
				GetProcessingReconstructionScore() * GetScanReconstructionScore();
			DrawMineralVeins(reconstructionQuality);
			foreach (FragmentLine line in Puzzle.Lines)
				DrawPuzzleLine(line, reconstructionQuality, surfaceQuality);
		}
		finally
		{
			_observablePrimitiveCollector = null;
		}

		return new FragmentObservableScan
		{
			Revision = _observationRevision,
			SampleSize = GetVirtualCanvasSize(),
			RotationPivotNormalized = new Vector2(
				Puzzle.FigureCenter.X / MathF.Max(Puzzle.ReferenceSize.X, 1f),
				Puzzle.FigureCenter.Y / MathF.Max(Puzzle.ReferenceSize.Y, 1f)),
			Primitives = primitives
		};
    }

    private void DrawPuzzleLine(FragmentLine line, float reconstructionQuality, float surfaceQuality)
    {
        float polarizationQuality = GetProcessingQuality(
            _polarizationEnabled,
            _polarizationLevel,
            Puzzle.CorrectPolarizationEnabled,
            Puzzle.CorrectPolarizationLevel);
        float spectralQuality = GetProcessingQuality(
            _spectralEnabled,
            _spectralLevel,
            Puzzle.CorrectSpectralEnabled,
            Puzzle.CorrectSpectralLevel);

        if (line.Role == FragmentLineRole.Distractor)
        {
            FragmentDistractorGlyph distractorGlyph = Puzzle.GetDistractorGlyph(line.DistractorGlyphType);
            if (distractorGlyph == null)
            {
                DrawNoiseDistractorLine(
                    line,
                    reconstructionQuality,
                    surfaceQuality,
                    polarizationQuality);
                return;
            }

            float distractorProcessingScore = GetProcessingReconstructionScore(distractorGlyph);
            float distractorScanScore = GetScanReconstructionScore(distractorGlyph);
            float distractorReconstructionQuality = distractorProcessingScore * distractorScanScore;
            float distractorSurfaceQuality = GetProcessingQuality(
                _surfaceEnabled,
                _surfaceLevel,
                distractorGlyph.CorrectSurfaceEnabled,
                distractorGlyph.CorrectSurfaceLevel);
            float distractorPolarizationQuality = GetProcessingQuality(
                _polarizationEnabled,
                _polarizationLevel,
                distractorGlyph.CorrectPolarizationEnabled,
                distractorGlyph.CorrectPolarizationLevel);
            float distractorSpectralQuality = GetProcessingQuality(
                _spectralEnabled,
                _spectralLevel,
                distractorGlyph.CorrectSpectralEnabled,
                distractorGlyph.CorrectSpectralLevel);
            DrawReconstructedLine(
                line,
                distractorReconstructionQuality,
                distractorSurfaceQuality,
                distractorPolarizationQuality,
                distractorSpectralQuality,
                generationSettings.DistractorInactiveOpacityMultiplier);
            return;
        }

        DrawReconstructedLine(
            line,
            reconstructionQuality,
            surfaceQuality,
            polarizationQuality,
            spectralQuality);
    }

    private void DrawReconstructedLine(
        FragmentLine line,
        float reconstructionQuality,
        float surfaceQuality,
        float polarizationQuality,
        float spectralQuality,
        float inactiveOpacityMultiplier = 1f)
    {
        DrawInactiveLine(
            line,
            reconstructionQuality,
            surfaceQuality,
            polarizationQuality,
            inactiveOpacityMultiplier);

        float revealStrength = GetLineRevealStrength(line, reconstructionQuality);
        if (revealStrength <= 0f) return;

        Color color = line.Color;
        color.A *= revealStrength;
        float width = line.Width;
        float detrimentalStrength = Mathf.Max(
            Mathf.Max(-polarizationQuality, -spectralQuality),
            -surfaceQuality);

        if (detrimentalStrength > 0f)
        {
            color.A *= Mathf.Lerp(
                1f,
                generationSettings.DetrimentalSignalOpacity,
                Mathf.Clamp(detrimentalStrength, 0f, 1f));
        }

        if (surfaceQuality > 0f)
        {
            color = color.Lerp(
                generationSettings.SurfaceSignalColor,
                Mathf.Clamp(generationSettings.SurfaceSignalColorStrength * surfaceQuality, 0f, 1f));
        }

        if (spectralQuality > 0f && line.IsImportant)
        {
            width *= Mathf.Lerp(
                1f,
                MathF.Max(generationSettings.SignalEnhancementWidthMultiplier, 1f),
                spectralQuality);
            color = color.Lerp(
                generationSettings.SignalEnhancementColor,
                Mathf.Clamp(generationSettings.SignalEnhancementColorStrength * spectralQuality, 0f, 1f));
        }

        DrawLinePattern(
            line,
            color,
            width,
            Mathf.Max(polarizationQuality, reconstructionQuality));
    }

    private void DrawNoiseDistractorLine(
        FragmentLine line,
        float reconstructionQuality,
        float surfaceQuality,
        float polarizationQuality)
    {
        DrawInactiveLine(line, reconstructionQuality, surfaceQuality, polarizationQuality);
        bool channelSelected = line.Channel switch
        {
            FragmentScanChannel.Electromagnetic => _electromagneticEnabled,
            FragmentScanChannel.Resonance => _resonanceEnabled,
            FragmentScanChannel.XRay => _xRayEnabled,
            _ => false
        };
        if (!channelSelected) return;

        float wrongSettingVisibility = (1f - reconstructionQuality) * 0.75f;
        float solutionVisibility = line.RevealedInCorrectCombination
            ? generationSettings.DistractorOpacityAtFullReconstruction * reconstructionQuality
            : 0f;
        float visibility = Mathf.Clamp(wrongSettingVisibility + solutionVisibility, 0f, 1f);
        if (visibility <= 0f) return;

        Color color = line.Color;
        color.A *= visibility;
        if (surfaceQuality > 0f)
        {
            color = color.Lerp(
                generationSettings.SurfaceSignalColor,
                generationSettings.SurfaceSignalColorStrength * surfaceQuality * 0.35f);
        }

        DrawLinePattern(line, color, line.Width, 0f);
    }

    private void DrawInactiveLine(
        FragmentLine line,
        float reconstructionQuality,
        float surfaceQuality,
        float polarizationQuality,
        float opacityMultiplier = 1f)
    {
        float opacity = Mathf.Clamp(generationSettings.InactiveOpacity, 0f, 1f);
        opacity *= MathF.Max(opacityMultiplier, 0f);
        opacity *= Mathf.Lerp(
            1f,
            Mathf.Clamp(generationSettings.NoiseOpacityAtFullReconstruction, 0f, 1f),
            reconstructionQuality);
        if (surfaceQuality > 0f)
        {
            opacity *= Mathf.Lerp(
                1f,
                Mathf.Clamp(generationSettings.SurfaceNoiseOpacityMultiplier, 0f, 1f),
                surfaceQuality);
        }
        else if (surfaceQuality < 0f)
        {
            opacity *= Mathf.Lerp(
                1f,
                MathF.Max(generationSettings.DetrimentalNoiseMultiplier, 1f),
                -surfaceQuality);
        }

        Color fractureColor = rockSettings.FractureColor;
        Color depositColor = rockSettings.DepositColor;
        fractureColor.A *= opacity;
        depositColor.A *= opacity;

        DrawLinePattern(line, fractureColor, rockSettings.FractureWidth, polarizationQuality);
        DrawLinePattern(line, depositColor, rockSettings.DepositWidth, polarizationQuality);
    }

    private void DrawLinePattern(FragmentLine line, Color color, float width, float reconstructionQuality)
    {
        Vector2 rotationCenter = line.HasCustomRotationCenter
            ? line.RotationCenter
            : Puzzle.FigureCenter;
        Vector2 start = TransformPuzzlePoint(line.Start, rotationCenter, line);
        Vector2 end = TransformPuzzlePoint(line.End, rotationCenter, line);

        if (reconstructionQuality >= 1f)
        {
            DrawClippedLine(start, end, color, width);
            return;
        }

        Vector2 delta = end - start;
        for (int intervalIndex = 0; intervalIndex < line.VisibleIntervals.Count; intervalIndex++)
        {
            if (reconstructionQuality < 0f && intervalIndex % 2 == 1) continue;
            Vector2 interval = line.VisibleIntervals[intervalIndex];
            DrawClippedLine(
                start + delta * interval.X,
                start + delta * interval.Y,
                color,
                width);
        }

        if (reconstructionQuality > 0f)
        {
            Color reconstructionColor = color;
            reconstructionColor.A *= reconstructionQuality;
            DrawClippedLine(start, end, reconstructionColor, width);
        }
    }

    private void DrawMineralVeins(float reconstructionQuality)
    {
        float surfaceQuality = GetProcessingQuality(
            _surfaceEnabled,
            _surfaceLevel,
            Puzzle.CorrectSurfaceEnabled,
            Puzzle.CorrectSurfaceLevel);
        float surfaceNoiseMultiplier = surfaceQuality >= 0f
            ? Mathf.Lerp(1f, generationSettings.SurfaceNoiseOpacityMultiplier, surfaceQuality)
            : Mathf.Lerp(1f, MathF.Max(generationSettings.DetrimentalNoiseMultiplier, 1f), -surfaceQuality);
        surfaceNoiseMultiplier *= Mathf.Lerp(
            1f,
            Mathf.Clamp(generationSettings.NoiseOpacityAtFullReconstruction, 0f, 1f),
            reconstructionQuality);
        Vector2 virtualCanvasSize = GetVirtualCanvasSize();

        foreach (FragmentVein vein in Puzzle.Veins)
        {
            Color fractureColor = rockSettings.FractureColor;
            Color depositColor = rockSettings.DepositColor;
            fractureColor.A *= vein.Opacity * surfaceNoiseMultiplier;
            depositColor.A *= vein.Opacity * surfaceNoiseMultiplier;

            for (int i = 0; i < vein.NormalizedPoints.Length - 1; i++)
            {
				Vector2 start = ApplyRenderTransform(vein.NormalizedPoints[i] * virtualCanvasSize);
				Vector2 end = ApplyRenderTransform(vein.NormalizedPoints[i + 1] * virtualCanvasSize);
                DrawClippedLine(start, end, fractureColor, rockSettings.FractureWidth);
                DrawClippedLine(start, end, depositColor, rockSettings.DepositWidth);
            }
        }
    }

    private float GetLineRevealStrength(FragmentLine line, float reconstructionQuality)
    {
        float transitionWidth = MathF.Max(generationSettings.RevealTransitionWidth, 0.01f);
        float start = line.RevealThreshold - transitionWidth;
        float progress = Mathf.Clamp(
            (reconstructionQuality - start) / transitionWidth,
            0f,
            1f);
        return progress * progress * (3f - 2f * progress);
    }

    private float GetProcessingReconstructionScore()
    {
        if (Puzzle == null) return 0f;

        float polarization = GetProcessorMatchScore(
            _polarizationEnabled,
            _polarizationLevel,
            Puzzle.CorrectPolarizationEnabled,
            Puzzle.CorrectPolarizationLevel);
        float spectral = GetProcessorMatchScore(
            _spectralEnabled,
            _spectralLevel,
            Puzzle.CorrectSpectralEnabled,
            Puzzle.CorrectSpectralLevel);
        float surface = GetProcessorMatchScore(
            _surfaceEnabled,
            _surfaceLevel,
            Puzzle.CorrectSurfaceEnabled,
            Puzzle.CorrectSurfaceLevel);
        return (polarization + spectral + surface) / 3f;
    }

    private float GetProcessingReconstructionScore(FragmentDistractorGlyph filterKey)
    {
        float polarization = GetProcessorMatchScore(
            _polarizationEnabled,
            _polarizationLevel,
            filterKey.CorrectPolarizationEnabled,
            filterKey.CorrectPolarizationLevel);
        float spectral = GetProcessorMatchScore(
            _spectralEnabled,
            _spectralLevel,
            filterKey.CorrectSpectralEnabled,
            filterKey.CorrectSpectralLevel);
        float surface = GetProcessorMatchScore(
            _surfaceEnabled,
            _surfaceLevel,
            filterKey.CorrectSurfaceEnabled,
            filterKey.CorrectSurfaceLevel);
        return (polarization + spectral + surface) / 3f;
    }

    private float GetProcessorMatchScore(bool enabled, int level, bool correctEnabled, int correctLevel)
    {
        if (!correctEnabled) return enabled ? 0f : 1f;
        if (!enabled)
            return Mathf.Clamp(generationSettings.RequiredProcessorBypassScore, 0f, 1f);

        int distance = Mathf.Abs(level - correctLevel);
        return distance switch
        {
            0 => 1f,
            1 => Mathf.Clamp(generationSettings.OneStepEffectStrength, 0f, 1f),
            2 => Mathf.Clamp(generationSettings.TwoStepMatchScore, 0f, 1f),
            _ => 0f
        };
    }

    private float GetScanReconstructionScore()
    {
        if (Puzzle == null) return 0f;

        return GetScanReconstructionScore(
            Puzzle.CorrectElectromagneticEnabled,
            Puzzle.CorrectResonanceEnabled,
            Puzzle.CorrectXRayEnabled);
    }

    private float GetScanReconstructionScore(FragmentDistractorGlyph filterKey)
    {
        return GetScanReconstructionScore(
            filterKey.CorrectElectromagneticEnabled,
            filterKey.CorrectResonanceEnabled,
            filterKey.CorrectXRayEnabled);
    }

    private float GetScanReconstructionScore(
        bool correctElectromagnetic,
        bool correctResonance,
        bool correctXRay)
    {

        int requiredCount = 0;
        int correctSelections = 0;
        int extraSelections = 0;
        ScoreScanChannel(
            correctElectromagnetic,
            _electromagneticEnabled,
            ref requiredCount,
            ref correctSelections,
            ref extraSelections);
        ScoreScanChannel(
            correctResonance,
            _resonanceEnabled,
            ref requiredCount,
            ref correctSelections,
            ref extraSelections);
        ScoreScanChannel(
            correctXRay,
            _xRayEnabled,
            ref requiredCount,
            ref correctSelections,
            ref extraSelections);

        float requiredScore = requiredCount > 0
            ? (float)correctSelections / requiredCount
            : 1f;
        float penalty = extraSelections * Mathf.Clamp(
            generationSettings.IncorrectScanChannelPenalty,
            0f,
            1f);
        return Mathf.Clamp(requiredScore - penalty, 0f, 1f);
    }

    private static void ScoreScanChannel(
        bool required,
        bool selected,
        ref int requiredCount,
        ref int correctSelections,
        ref int extraSelections)
    {
        if (required)
        {
            requiredCount++;
            if (selected) correctSelections++;
        }
        else if (selected)
        {
            extraSelections++;
        }
    }

    private float GetProcessingQuality(bool enabled, int level, bool correctEnabled, int correctLevel)
    {
        if (!enabled) return 0f;
        if (!correctEnabled) return generationSettings.DetrimentalEffectStrength;

        int distance = Mathf.Abs(level - correctLevel);
        return distance switch
        {
            0 => 1f,
            1 => Mathf.Clamp(generationSettings.OneStepEffectStrength, 0f, 1f),
            2 => 0f,
            _ => Mathf.Clamp(generationSettings.DetrimentalEffectStrength, -1f, 0f)
        };
    }

    private Vector2 TransformPuzzlePoint(
		Vector2 point,
		Vector2 rotationCenter,
		FragmentLine line)
	{
		return ApplyRenderTransform(TransformPuzzlePointToVirtual(point, rotationCenter, line));
	}

	private Vector2 TransformPuzzlePointToVirtual(
		Vector2 point,
		Vector2 rotationCenter,
		FragmentLine line)
    {
        Vector2 referenceSize = Puzzle.ReferenceSize;
        Vector2 rotated = rotationCenter +
            (point - rotationCenter).Rotated(Mathf.DegToRad(DisplayRotationDegrees));
        Vector2 virtualCanvasSize = GetVirtualCanvasSize();
        Vector2 scaled = new(
            rotated.X * virtualCanvasSize.X / MathF.Max(referenceSize.X, 1f),
            rotated.Y * virtualCanvasSize.Y / MathF.Max(referenceSize.Y, 1f));
		foreach (RegionRotation regionRotation in _regionRotations)
		{
			if (!regionRotation.Lines.Contains(line) ||
				MathF.Abs(regionRotation.Degrees) <= 0.0001f) continue;
			Vector2 pivot = regionRotation.PivotNormalized * virtualCanvasSize;
			scaled = pivot + (scaled - pivot).Rotated(Mathf.DegToRad(regionRotation.Degrees));
		}
		return scaled;
    }

	private static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Rect2 rectangle)
	{
		Vector2 delta = end - start;
		float minimum = 0f;
		float maximum = 1f;
		return ClipRegionSegment(-delta.X, start.X - rectangle.Position.X, ref minimum, ref maximum) &&
			ClipRegionSegment(delta.X, rectangle.End.X - start.X, ref minimum, ref maximum) &&
			ClipRegionSegment(-delta.Y, start.Y - rectangle.Position.Y, ref minimum, ref maximum) &&
			ClipRegionSegment(delta.Y, rectangle.End.Y - start.Y, ref minimum, ref maximum);
	}

	private static bool ClipRegionSegment(
		float direction,
		float distance,
		ref float minimum,
		ref float maximum)
	{
		if (Mathf.IsZeroApprox(direction)) return distance >= 0f;
		float ratio = distance / direction;
		if (direction < 0f)
		{
			if (ratio > maximum) return false;
			if (ratio > minimum) minimum = ratio;
		}
		else
		{
			if (ratio < minimum) return false;
			if (ratio < maximum) maximum = ratio;
		}
		return true;
	}

	private Vector2 ApplyRenderTransform(Vector2 point)
	{
		return _observablePrimitiveCollector != null ? point : ApplyViewTransform(point);
	}

    private Vector2 ApplyViewTransform(Vector2 point)
    {
        Vector2 viewportCenter = Size * 0.5f;
        Vector2 virtualCanvasCenter = GetVirtualCanvasSize() * 0.5f;
        return viewportCenter + _viewPan + (point - virtualCanvasCenter) * _viewZoom;
    }

    private Rect2 GetViewRect()
    {
        Vector2 viewportCenter = Size * 0.5f;
        Vector2 virtualCanvasSize = GetVirtualCanvasSize();
        return new Rect2(
            viewportCenter + _viewPan - virtualCanvasSize * _viewZoom * 0.5f,
            virtualCanvasSize * _viewZoom);
    }

    private void DrawRockBackground()
    {
        Rect2 canvasRect = new(Vector2.Zero, Size);
        DrawRect(canvasRect, rockSettings.DarkColor, true);
        if (_rockTexture == null) return;

        Rect2 viewRect = GetViewRect();
        Rect2 visibleRect = viewRect.Intersection(canvasRect);
        if (visibleRect.Size.X <= 0f || visibleRect.Size.Y <= 0f) return;

        Vector2 textureSize = _rockTexture.GetSize();
        Vector2 sourcePosition = (visibleRect.Position - viewRect.Position) / viewRect.Size * textureSize;
        Vector2 sourceSize = visibleRect.Size / viewRect.Size * textureSize;
        DrawTextureRectRegion(
            _rockTexture,
            visibleRect,
            new Rect2(sourcePosition, sourceSize));
    }

	public void ZoomViewAt(
		Vector2 localPosition,
		float factor,
		FragmentAnalysisActionOrigin origin = FragmentAnalysisActionOrigin.Player)
    {
		if (origin == FragmentAnalysisActionOrigin.Player) CancelViewNavigation();
        float minimumZoom = GetMinimumViewZoom();
        float maximumZoom = MathF.Max(generationSettings.MaximumViewZoom, minimumZoom);
        float newZoom = Mathf.Clamp(_viewZoom * factor, minimumZoom, maximumZoom);
        if (Mathf.IsEqualApprox(newZoom, _viewZoom)) return;

        Vector2 center = Size * 0.5f;
        float ratio = newZoom / _viewZoom;
        _viewPan = localPosition - center - (localPosition - center - _viewPan) * ratio;
        _viewZoom = newZoom;
        ClampViewPan();
		NotifyViewChanged(origin);
        QueueRedraw();
    }

	public void PanViewBy(
		Vector2 delta,
		FragmentAnalysisActionOrigin origin = FragmentAnalysisActionOrigin.Player)
	{
		if (origin == FragmentAnalysisActionOrigin.Player) CancelViewNavigation();
		_viewPan += delta;
		ClampViewPan();
		NotifyViewChanged(origin);
		QueueRedraw();
	}

	public void FocusNormalizedPoint(
		Vector2 normalizedPoint,
		FragmentAnalysisActionOrigin origin = FragmentAnalysisActionOrigin.Rover)
	{
		CancelViewNavigation();
		Vector2 virtualPoint = normalizedPoint * GetVirtualCanvasSize();
		Vector2 virtualCenter = GetVirtualCanvasSize() * 0.5f;
		_viewPan = -(virtualPoint - virtualCenter) * _viewZoom;
		ClampViewPan();
		NotifyViewChanged(origin);
		QueueRedraw();
	}

	public bool IsNormalizedPointVisible(Vector2 normalizedPoint, float margin = 8f)
	{
		Vector2 virtualSize = GetVirtualCanvasSize();
		Vector2 virtualPoint = normalizedPoint * virtualSize;
		Vector2 viewportPoint = Size * 0.5f +
			(virtualPoint - virtualSize * 0.5f) * _viewZoom + _viewPan;
		float safeMargin = Mathf.Clamp(margin, 0f, MathF.Min(Size.X, Size.Y) * 0.45f);
		return new Rect2(
			new Vector2(safeMargin, safeMargin),
			new Vector2(
				MathF.Max(Size.X - safeMargin * 2f, 0f),
				MathF.Max(Size.Y - safeMargin * 2f, 0f))).HasPoint(viewportPoint);
	}

	public void FocusNormalizedRect(
		Rect2 normalizedBounds,
		FragmentAnalysisActionOrigin origin = FragmentAnalysisActionOrigin.Rover)
	{
		CancelViewNavigation();
		Vector2 virtualSize = GetVirtualCanvasSize();
		Vector2 targetSize = normalizedBounds.Size * virtualSize;
		if (targetSize.X > 0.001f && targetSize.Y > 0.001f)
		{
			float minimumZoom = GetMinimumViewZoom();
			float maximumZoom = MathF.Max(generationSettings.MaximumViewZoom, minimumZoom);
			float fitZoom = MathF.Min(
				Size.X / (targetSize.X * 1.2f),
				Size.Y / (targetSize.Y * 1.2f));
			_viewZoom = Mathf.Clamp(fitZoom, minimumZoom, maximumZoom);
		}
		Vector2 virtualPoint = normalizedBounds.GetCenter() * virtualSize;
		_viewPan = -(virtualPoint - virtualSize * 0.5f) * _viewZoom;
		ClampViewPan();
		NotifyViewChanged(origin);
		QueueRedraw();
	}

	public void NavigateToNormalizedRect(Rect2 normalizedBounds, float durationSeconds)
	{
		CancelViewNavigation();
		// A comparison/overlay can consume the mouse release after the canvas saw the press.
		// Never let that stale drag state silently cancel a newly requested Rover tween.
		_isViewDragging = false;
		CalculateFocusedView(normalizedBounds, out _navigationTargetZoom, out _navigationTargetPan);
		_navigationTargetBounds = normalizedBounds;
		_navigationStartZoom = _viewZoom;
		_navigationStartPan = _viewPan;
		if (Mathf.IsEqualApprox(_navigationStartZoom, _navigationTargetZoom) &&
			_navigationStartPan.IsEqualApprox(_navigationTargetPan))
		{
			CompleteViewNavigation();
			return;
		}
		float duration = MathF.Max(durationSeconds, 0.01f);
		_viewNavigationElapsed = 0f;
		_viewNavigationDuration = duration;
		_isViewNavigationActive = true;
	}

	public void CancelViewNavigation()
	{
		_isViewNavigationActive = false;
		_viewNavigationElapsed = 0f;
	}

	private void ApplyNavigationProgress(float progress)
	{
		_viewZoom = Mathf.Lerp(_navigationStartZoom, _navigationTargetZoom, progress);
		_viewPan = _navigationStartPan.Lerp(_navigationTargetPan, progress);
		ClampViewPan();
		NotifyViewChanged(FragmentAnalysisActionOrigin.Rover);
		QueueRedraw();
	}

	private void CompleteViewNavigation()
	{
		_isViewNavigationActive = false;
		_viewNavigationElapsed = 0f;
		_viewZoom = _navigationTargetZoom;
		_viewPan = _navigationTargetPan;
		ClampViewPan();
		NotifyViewChanged(FragmentAnalysisActionOrigin.Rover);
		QueueRedraw();
		ViewNavigationCompleted?.Invoke();
	}

	private void CalculateFocusedView(
		Rect2 normalizedBounds,
		out float targetZoom,
		out Vector2 targetPan)
	{
		Vector2 virtualSize = GetVirtualCanvasSize();
		Vector2 targetSize = normalizedBounds.Size * virtualSize;
		float minimumZoom = GetMinimumViewZoom();
		float maximumZoom = MathF.Max(generationSettings.MaximumViewZoom, minimumZoom);
		float fitZoom = targetSize.X > 0.001f && targetSize.Y > 0.001f
			? MathF.Min(Size.X / (targetSize.X * 1.2f), Size.Y / (targetSize.Y * 1.2f))
			: _viewZoom;
		targetZoom = Mathf.Clamp(fitZoom, minimumZoom, maximumZoom);
		Vector2 virtualPoint = normalizedBounds.GetCenter() * virtualSize;
		targetPan = -(virtualPoint - virtualSize * 0.5f) * targetZoom;
		Vector2 scaledCanvasSize = virtualSize * targetZoom;
		Vector2 maximumPan = new(
			MathF.Max((scaledCanvasSize.X - Size.X) * 0.5f, 0f),
			MathF.Max((scaledCanvasSize.Y - Size.Y) * 0.5f, 0f));
		targetPan = new Vector2(
			Mathf.Clamp(targetPan.X, -maximumPan.X, maximumPan.X),
			Mathf.Clamp(targetPan.Y, -maximumPan.Y, maximumPan.Y));
	}


    private void NotifyViewChanged(FragmentAnalysisActionOrigin origin)
    {
        ViewChanged?.Invoke(_viewZoom, _viewPan, origin);
    }

    private void ClampViewPan()
    {
        Vector2 scaledCanvasSize = GetVirtualCanvasSize() * _viewZoom;
        Vector2 maximumPan = new(
            MathF.Max((scaledCanvasSize.X - Size.X) * 0.5f, 0f),
            MathF.Max((scaledCanvasSize.Y - Size.Y) * 0.5f, 0f));
        _viewPan = new Vector2(
            Mathf.Clamp(_viewPan.X, -maximumPan.X, maximumPan.X),
            Mathf.Clamp(_viewPan.Y, -maximumPan.Y, maximumPan.Y));
    }

    private void GenerateRockTexture(int seed)
    {
        int resolution = Mathf.Clamp(
            Mathf.RoundToInt(rockSettings.Resolution * GetCanvasSizeMultiplier()),
            32,
            2048);
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
                float crystal = 1f - Mathf.Abs(cellularNoise.GetNoise2D(x, y));
                value += (crystal - 0.5f) * rockSettings.CellularStrength;
                value = Mathf.Clamp(value, 0f, 1f);
                image.SetPixel(x, y, rockSettings.DarkColor.Lerp(rockSettings.LightColor, value));
            }
        }

        _rockTexture = ImageTexture.CreateFromImage(image);
    }

    private float GetCanvasSizeMultiplier() =>
        MathF.Max(generationSettings.CanvasSizeMultiplier, 1f);

    private Vector2 GetVirtualCanvasSize() =>
        Size * GetCanvasSizeMultiplier();

    private float GetMinimumViewZoom()
    {
        Vector2 virtualCanvasSize = GetVirtualCanvasSize();
        float fitZoom = MathF.Max(
            Size.X / MathF.Max(virtualCanvasSize.X, 1f),
            Size.Y / MathF.Max(virtualCanvasSize.Y, 1f));
        return MathF.Max(generationSettings.MinimumViewZoom, fitZoom);
    }

    private void DrawClippedLine(Vector2 start, Vector2 end, Color color, float width)
    {
		Vector2 boundsSize = _observablePrimitiveCollector != null
			? GetVirtualCanvasSize()
			: Size;
		if (!TryClipLineToBounds(
			start,
			end,
			width,
			boundsSize,
			out Vector2 clippedStart,
			out Vector2 clippedEnd))
		{
			return;
		}

		if (_observablePrimitiveCollector == null)
		{
			DrawLine(clippedStart, clippedEnd, color, width);
			return;
		}

		if (color.A <= 0.001f || clippedStart.DistanceSquaredTo(clippedEnd) <= 0.01f) return;
		Vector2 safeBounds = new(
			MathF.Max(boundsSize.X, 1f),
			MathF.Max(boundsSize.Y, 1f));
		_observablePrimitiveCollector.Add(new FragmentObservablePrimitive
		{
			Id = _nextObservablePrimitiveId++,
			Start = clippedStart / safeBounds,
			End = clippedEnd / safeBounds,
			Color = color,
			Width = width,
			Intensity = Mathf.Clamp(color.A, 0f, 1f)
		});
    }

	private static bool TryClipLineToBounds(
        Vector2 start,
        Vector2 end,
        float width,
        Vector2 boundsSize,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        float inset = Mathf.Max(width * 0.5f, 0f);
        float left = inset;
        float top = inset;
		float right = boundsSize.X - inset;
		float bottom = boundsSize.Y - inset;

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

    private void EmitPuzzleStateChanged()
    {
        if (!IsInsideTree() || Puzzle == null) return;
        EmitSignal(
            SignalName.PuzzleStateChanged,
            IsCorrectFilterCombination() && IsCorrectProcessingCombination(),
            IsAtCorrectRotation());
    }
}
