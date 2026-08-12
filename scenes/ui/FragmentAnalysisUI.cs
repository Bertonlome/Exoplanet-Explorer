using Godot;
using System;
using Game;
using Game.UI;

public partial class FragmentAnalysisUI : CanvasLayer
{
	private const float RotationStepDegrees = 10f;

	private FragmentCanvas fragmentCanvas;
	private Button quitButton;
	private Button reloadButton;
	private Button rotateCounterClockwiseButton;
	private Button rotateClockwiseButton;
	private Label rotationValueLabel;
	private CheckButton polarizationButton;
	private CheckButton spectralButton;
	private CheckButton surfaceButton;
	private CheckButton electromagneticButton;
	private CheckButton resonanceButton;
	private CheckButton xRayButton;
	private HSlider polarizationSlider;
	private HSlider spectralSlider;
	private HSlider surfaceSlider;
	private Label polarizationValueLabel;
	private Label spectralValueLabel;
	private Label surfaceValueLabel;
	private Vector2I fragmentPosition;
	private bool isClosing;
	private MonolithFragment monolithFragment;
	private MonolithFragment.Variant fragmentVariant;

	public event Action<Vector2I, FragmentAnalysisState> StateSaved;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		fragmentCanvas = GetNode<FragmentCanvas>("%FragmentCanvas");
		quitButton = GetNode<Button>("%QuitButton");
		reloadButton = GetNode<Button>("%ReloadButton");
		rotateCounterClockwiseButton = GetNode<Button>("%RotateCounterClockwiseButton");
		rotateClockwiseButton = GetNode<Button>("%RotateClockwiseButton");
		rotationValueLabel = GetNode<Label>("%RotationValueLabel");
		polarizationButton = GetNode<CheckButton>("%PolarizationButton");
		spectralButton = GetNode<CheckButton>("%SpectralButton");
		surfaceButton = GetNode<CheckButton>("%SurfaceButton");
		electromagneticButton = GetNode<CheckButton>("%ElectromagneticButton");
		resonanceButton = GetNode<CheckButton>("%ResonanceButton");
		xRayButton = GetNode<CheckButton>("%XRayButton");
		polarizationSlider = GetNode<HSlider>("%PolarizationSlider");
		spectralSlider = GetNode<HSlider>("%SpectralSlider");
		surfaceSlider = GetNode<HSlider>("%SurfaceSlider");
		polarizationValueLabel = GetNode<Label>("%PolarizationValueLabel");
		spectralValueLabel = GetNode<Label>("%SpectralValueLabel");
		surfaceValueLabel = GetNode<Label>("%SurfaceValueLabel");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void SetupUI(
		Vector2I fragmentPosition, Vector2I monolithPosition, FragmentAnalysisState savedState = null)
	{
		this.fragmentPosition = fragmentPosition;
		Visible = true;
		GameUI.Instance?.SetMinimapInputEnabled(false);
		GameUI.Instance?.SetWorldCameraInputEnabled(false);
		monolithFragment = this.TryGetNodeAtPosition<MonolithFragment>(fragmentPosition);
		fragmentVariant = monolithFragment?.currentVariant ?? MonolithFragment.Variant.Hominid;
		FragmentGlyphType glyphType = savedState?.GlyphType ?? GetGlyphType(fragmentVariant);
		fragmentCanvas.SetSpatialContext(fragmentPosition, monolithPosition, glyphType);

		if (savedState == null)
		{
			fragmentCanvas.GenerateFragment();
		}
		else
		{
			RestoreState(savedState);
		}

		quitButton.Pressed += HideUI;
		reloadButton.Pressed += OnReloadPressed;
		rotateCounterClockwiseButton.Pressed += OnRotateCounterClockwisePressed;
		rotateClockwiseButton.Pressed += OnRotateClockwisePressed;
		fragmentCanvas.PuzzleStateChanged += OnPuzzleStateChanged;
		polarizationButton.Toggled += OnPolarizationToggled;
		spectralButton.Toggled += OnSpectralToggled;
		surfaceButton.Toggled += OnSurfaceToggled;
		electromagneticButton.Toggled += OnElectromagneticToggled;
		resonanceButton.Toggled += OnResonanceToggled;
		xRayButton.Toggled += OnXRayToggled;
		polarizationSlider.ValueChanged += OnPolarizationLevelChanged;
		spectralSlider.ValueChanged += OnSpectralLevelChanged;
		surfaceSlider.ValueChanged += OnSurfaceLevelChanged;
		SyncFilterState();
	}

	public void HideUI()
	{
		if (isClosing) return;
		isClosing = true;
		StateSaved?.Invoke(fragmentPosition, CaptureState());
		Visible = false;
		GameUI.Instance?.SetMinimapInputEnabled(true);
		GameUI.Instance?.SetWorldCameraInputEnabled(true);
		DisconnectSignals();
		QueueFree();
	}

	public override void _ExitTree()
	{
		GameUI.Instance?.SetMinimapInputEnabled(true);
		GameUI.Instance?.SetWorldCameraInputEnabled(true);
	}

	private void DisconnectSignals()
	{
		quitButton.Pressed -= HideUI;
		reloadButton.Pressed -= OnReloadPressed;
		rotateCounterClockwiseButton.Pressed -= OnRotateCounterClockwisePressed;
		rotateClockwiseButton.Pressed -= OnRotateClockwisePressed;
		fragmentCanvas.PuzzleStateChanged -= OnPuzzleStateChanged;
		polarizationButton.Toggled -= OnPolarizationToggled;
		spectralButton.Toggled -= OnSpectralToggled;
		surfaceButton.Toggled -= OnSurfaceToggled;
		electromagneticButton.Toggled -= OnElectromagneticToggled;
		resonanceButton.Toggled -= OnResonanceToggled;
		xRayButton.Toggled -= OnXRayToggled;
		polarizationSlider.ValueChanged -= OnPolarizationLevelChanged;
		spectralSlider.ValueChanged -= OnSpectralLevelChanged;
		surfaceSlider.ValueChanged -= OnSurfaceLevelChanged;
	}

	private void OnReloadPressed()
	{
		fragmentCanvas.GenerateFragment();
		UpdateRotationLabel();
	}

	private void OnRotateCounterClockwisePressed()
	{
		fragmentCanvas.SetPuzzleRotationDegrees(
			fragmentCanvas.DisplayRotationDegrees - RotationStepDegrees);
	}

	private void OnRotateClockwisePressed()
	{
		fragmentCanvas.SetPuzzleRotationDegrees(
			fragmentCanvas.DisplayRotationDegrees + RotationStepDegrees);
	}

	private void OnPuzzleStateChanged(bool filterCombinationCorrect, bool rotationCorrect)
	{
		UpdateRotationLabel();
	}

	private void UpdateRotationLabel()
	{
		rotationValueLabel.Text = $"ROTATION: {Mathf.RoundToInt(fragmentCanvas.DisplayRotationDegrees)}°";
	}

	private void SyncFilterState()
	{
		OnPolarizationToggled(polarizationButton.ButtonPressed);
		OnSpectralToggled(spectralButton.ButtonPressed);
		OnSurfaceToggled(surfaceButton.ButtonPressed);
		OnElectromagneticToggled(electromagneticButton.ButtonPressed);
		OnResonanceToggled(resonanceButton.ButtonPressed);
		OnXRayToggled(xRayButton.ButtonPressed);
		OnPolarizationLevelChanged(polarizationSlider.Value);
		OnSpectralLevelChanged(spectralSlider.Value);
		OnSurfaceLevelChanged(surfaceSlider.Value);
		UpdateRotationLabel();
	}

	private void RestoreState(FragmentAnalysisState state)
	{
		fragmentCanvas.GenerateFragmentFromSeed(state.PuzzleSeed);

		polarizationButton.ButtonPressed = state.PolarizationEnabled;
		spectralButton.ButtonPressed = state.SpectralEnabled;
		surfaceButton.ButtonPressed = state.SurfaceEnabled;
		electromagneticButton.ButtonPressed = state.ElectromagneticEnabled;
		resonanceButton.ButtonPressed = state.ResonanceEnabled;
		xRayButton.ButtonPressed = state.XRayEnabled;

		polarizationSlider.Value = state.PolarizationLevel;
		spectralSlider.Value = state.SpectralLevel;
		surfaceSlider.Value = state.SurfaceLevel;

		fragmentCanvas.SetPuzzleRotationDegrees(state.RotationDegrees);
		fragmentCanvas.RestoreView(state.ViewZoom, state.ViewPan);
	}

	private FragmentAnalysisState CaptureState()
	{
		return new FragmentAnalysisState
		{
			PuzzleSeed = fragmentCanvas.Puzzle.Seed,
			GlyphType = fragmentCanvas.Puzzle.GlyphType,
			PolarizationEnabled = polarizationButton.ButtonPressed,
			SpectralEnabled = spectralButton.ButtonPressed,
			SurfaceEnabled = surfaceButton.ButtonPressed,
			ElectromagneticEnabled = electromagneticButton.ButtonPressed,
			ResonanceEnabled = resonanceButton.ButtonPressed,
			XRayEnabled = xRayButton.ButtonPressed,
			PolarizationLevel = Mathf.RoundToInt(polarizationSlider.Value),
			SpectralLevel = Mathf.RoundToInt(spectralSlider.Value),
			SurfaceLevel = Mathf.RoundToInt(surfaceSlider.Value),
			RotationDegrees = fragmentCanvas.DisplayRotationDegrees,
			ViewZoom = fragmentCanvas.ViewZoom,
			ViewPan = fragmentCanvas.ViewPan,
			WasSolved = fragmentCanvas.IsPuzzleSolved()
		};
	}

	private static FragmentGlyphType GetGlyphType(MonolithFragment.Variant variant)
	{
		return variant switch
		{
			MonolithFragment.Variant.Chip => FragmentGlyphType.Key,
			MonolithFragment.Variant.Television => FragmentGlyphType.Television,
			_ => FragmentGlyphType.Hominid
		};
	}

	private void OnPolarizationToggled(bool enabled) => fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Polarization, enabled);
	private void OnSpectralToggled(bool enabled) => fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Spectral, enabled);
	private void OnSurfaceToggled(bool enabled) => fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Surface, enabled);
	private void OnElectromagneticToggled(bool enabled) => fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Electromagnetic, enabled);
	private void OnResonanceToggled(bool enabled) => fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Resonance, enabled);
	private void OnXRayToggled(bool enabled) => fragmentCanvas.SetFilter(FragmentCanvas.FilterType.XRay, enabled);

	private void OnPolarizationLevelChanged(double value)
	{
		int level = Mathf.RoundToInt(value);
		polarizationValueLabel.Text = $"POLARIZATION LEVEL: {level}";
		fragmentCanvas.SetProcessingLevel(FragmentCanvas.FilterType.Polarization, level);
	}

	private void OnSpectralLevelChanged(double value)
	{
		int level = Mathf.RoundToInt(value);
		spectralValueLabel.Text = $"SPECTRAL LEVEL: {level}";
		fragmentCanvas.SetProcessingLevel(FragmentCanvas.FilterType.Spectral, level);
	}

	private void OnSurfaceLevelChanged(double value)
	{
		int level = Mathf.RoundToInt(value);
		surfaceValueLabel.Text = $"SURFACE LEVEL: {level}";
		fragmentCanvas.SetProcessingLevel(FragmentCanvas.FilterType.Surface, level);
	}
}
