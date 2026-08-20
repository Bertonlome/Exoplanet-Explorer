using Godot;
using System;
using Game;
using Game.UI;

public partial class FragmentAnalysisUI : CanvasLayer, IFragmentAnalysisCommandSink
{
	private const float RotationStepDegrees = 10f;

	private FragmentCanvas fragmentCanvas;
	private Button quitButton;
	private Button reloadButton;
	private Button rotateCounterClockwiseButton;
	private Button rotateClockwiseButton;
	private Label rotationValueLabel;
	private SpinBox fineRotationSpinBox;
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
	private bool wasEverSolved;
	private bool isRestoredSession;
	private FragmentAnalysisActionOrigin initiationOrigin;
	private bool isSyncingRotationControl;

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
		fineRotationSpinBox = GetNode<SpinBox>("%FineRotationSpinBox");
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
		InitializeAutonomyNodes();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		UpdateResponsiveHeader();
		UpdateFeatureOverlayView();
		if (isRegionSequenceRefreshPending) RefreshRegionSequence();
	}

	public void SetupUI(
		Vector2I fragmentPosition,
		Vector2I monolithPosition,
		FragmentAnalysisState savedState = null,
		FragmentAutonomyMode initialAutonomyMode = FragmentAutonomyMode.Off,
		bool wasRestored = false,
		FragmentAnalysisActionOrigin initiationOrigin = FragmentAnalysisActionOrigin.Player)
	{
		this.fragmentPosition = fragmentPosition;
		isRestoredSession = wasRestored;
		this.initiationOrigin = initiationOrigin;
		Visible = true;
		GameUI.Instance?.SetMinimapInputEnabled(false);
		GameUI.Instance?.SetWorldCameraInputEnabled(false);
		monolithFragment = this.TryGetNodeAtPosition<MonolithFragment>(fragmentPosition);
		fragmentVariant = monolithFragment?.currentVariant ?? MonolithFragment.Variant.Hominid;
		SetFragmentOverviewTexture(monolithFragment?.FragmentTexture);
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

		SyncFilterState();
		wasEverSolved = savedState?.WasEverSolved == true || savedState?.WasSolved == true;
		if (fragmentCanvas.IsPuzzleSolved()) wasEverSolved = true;
		FragmentAutonomyState restoredRoverState = savedState?.RoverState?.Clone() ??
			FragmentAutonomyState.CreateDefault(autonomySettings);
		restoredRoverState.GlobalMode = initialAutonomyMode;
		InitializeAutonomy(restoredRoverState);
		UpdateFragmentLifecycleLabel(isRestoredSession, wasEverSolved);

		quitButton.Pressed += HideUI;
		reloadButton.Pressed += OnReloadPressed;
		rotateCounterClockwiseButton.Pressed += OnRotateCounterClockwisePressed;
		rotateClockwiseButton.Pressed += OnRotateClockwisePressed;
		fineRotationSpinBox.ValueChanged += OnFineRotationChanged;
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
		DisconnectAutonomySignals();
		GameUI.Instance?.SetMinimapInputEnabled(true);
		GameUI.Instance?.SetWorldCameraInputEnabled(true);
	}

	private void DisconnectSignals()
	{
		DisconnectAutonomySignals();
		quitButton.Pressed -= HideUI;
		reloadButton.Pressed -= OnReloadPressed;
		rotateCounterClockwiseButton.Pressed -= OnRotateCounterClockwisePressed;
		rotateClockwiseButton.Pressed -= OnRotateClockwisePressed;
		fineRotationSpinBox.ValueChanged -= OnFineRotationChanged;
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
		ShowReloadConfirmation();
	}

	private void OnRotateCounterClockwisePressed()
	{
		DispatchAnalysisCommand(FragmentAnalysisCommand.Rotation(
			fragmentCanvas.DisplayRotationDegrees - RotationStepDegrees,
			FragmentAnalysisActionOrigin.Player));
	}

	private void OnRotateClockwisePressed()
	{
		DispatchAnalysisCommand(FragmentAnalysisCommand.Rotation(
			fragmentCanvas.DisplayRotationDegrees + RotationStepDegrees,
			FragmentAnalysisActionOrigin.Player));
	}

	private void OnFineRotationChanged(double value)
	{
		if (isSyncingRotationControl) return;
		DispatchAnalysisCommand(FragmentAnalysisCommand.Rotation(
			(float)value,
			FragmentAnalysisActionOrigin.Player));
	}

	private void OnPuzzleStateChanged(bool filterCombinationCorrect, bool rotationCorrect)
	{
		if (filterCombinationCorrect && rotationCorrect)
		{
			wasEverSolved = true;
			UpdateFragmentLifecycleLabel(isRestoredSession, true);
		}
		UpdateRotationLabel();
	}

	private void UpdateRotationLabel()
	{
		float rotation = fragmentCanvas.DisplayRotationDegrees;
		rotationValueLabel.Text = $"ROTATION: {rotation:+0.0;-0.0;0.0}°";
		if (!IsInstanceValid(fineRotationSpinBox)) return;
		isSyncingRotationControl = true;
		fineRotationSpinBox.Value = rotation;
		isSyncingRotationControl = false;
	}

	private void SyncFilterState()
	{
		fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Polarization, polarizationButton.ButtonPressed);
		fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Spectral, spectralButton.ButtonPressed);
		fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Surface, surfaceButton.ButtonPressed);
		fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Electromagnetic, electromagneticButton.ButtonPressed);
		fragmentCanvas.SetFilter(FragmentCanvas.FilterType.Resonance, resonanceButton.ButtonPressed);
		fragmentCanvas.SetFilter(FragmentCanvas.FilterType.XRay, xRayButton.ButtonPressed);
		fragmentCanvas.SetProcessingLevel(
			FragmentCanvas.FilterType.Polarization,
			Mathf.RoundToInt(polarizationSlider.Value));
		fragmentCanvas.SetProcessingLevel(
			FragmentCanvas.FilterType.Spectral,
			Mathf.RoundToInt(spectralSlider.Value));
		fragmentCanvas.SetProcessingLevel(
			FragmentCanvas.FilterType.Surface,
			Mathf.RoundToInt(surfaceSlider.Value));
		UpdateProcessingLabels();
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
			WasSolved = fragmentCanvas.IsPuzzleSolved(),
			WasEverSolved = wasEverSolved || fragmentCanvas.IsPuzzleSolved(),
			InitiationOrigin = initiationOrigin,
			RoverState = fragmentAnalysisRover?.CaptureState()
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

	private void OnPolarizationToggled(bool enabled) => DispatchToggle(
		FragmentAnalysisParameter.PolarizationEnabled, enabled);
	private void OnSpectralToggled(bool enabled) => DispatchToggle(
		FragmentAnalysisParameter.SpectralEnabled, enabled);
	private void OnSurfaceToggled(bool enabled) => DispatchToggle(
		FragmentAnalysisParameter.SurfaceEnabled, enabled);
	private void OnElectromagneticToggled(bool enabled) => DispatchToggle(
		FragmentAnalysisParameter.ElectromagneticEnabled, enabled);
	private void OnResonanceToggled(bool enabled) => DispatchToggle(
		FragmentAnalysisParameter.ResonanceEnabled, enabled);
	private void OnXRayToggled(bool enabled) => DispatchToggle(
		FragmentAnalysisParameter.XRayEnabled, enabled);

	private void OnPolarizationLevelChanged(double value)
	{
		DispatchLevel(FragmentAnalysisParameter.PolarizationLevel, Mathf.RoundToInt(value));
	}

	private void OnSpectralLevelChanged(double value)
	{
		DispatchLevel(FragmentAnalysisParameter.SpectralLevel, Mathf.RoundToInt(value));
	}

	private void OnSurfaceLevelChanged(double value)
	{
		DispatchLevel(FragmentAnalysisParameter.SurfaceLevel, Mathf.RoundToInt(value));
	}
}
