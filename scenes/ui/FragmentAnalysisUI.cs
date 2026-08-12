using Godot;
using System;

public partial class FragmentAnalysisUI : CanvasLayer
{

	private FragmentCanvas fragmentCanvas;
	private Button quitButton;
	private Button reloadButton;
	private CheckButton polarizationButton;
	private CheckButton spectralButton;
	private CheckButton surfaceButton;
	private CheckButton electromagneticButton;
	private CheckButton resonanceButton;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		fragmentCanvas = GetNode<FragmentCanvas>("%FragmentCanvas");
		quitButton = GetNode<Button>("%QuitButton");
		reloadButton = GetNode<Button>("%ReloadButton");
		polarizationButton = GetNode<CheckButton>("%PolarizationButton");
		spectralButton = GetNode<CheckButton>("%SpectralButton");
		surfaceButton = GetNode<CheckButton>("%SurfaceButton");
		electromagneticButton = GetNode<CheckButton>("%ElectromagneticButton");
		resonanceButton = GetNode<CheckButton>("%ResonanceButton");

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void SetupUI()
	{
		Visible = true;
		quitButton.Pressed += HideUI;
		reloadButton.Pressed += OnReloadPressed;
		polarizationButton.Toggled += OnPolarizationToggled;
		spectralButton.Toggled += OnSpectralToggled;
		surfaceButton.Toggled += OnSurfaceToggled;
		electromagneticButton.Toggled += OnElectromagneticToggled;
		resonanceButton.Toggled += OnResonanceToggled;
	}

	public void HideUI()
	{
		Visible = false;
		DisconnectSignals();
	}

	private void DisconnectSignals()
	{
		quitButton.Pressed -= HideUI;
		reloadButton.Pressed -= OnReloadPressed;
		polarizationButton.Toggled -= OnPolarizationToggled;
		spectralButton.Toggled -= OnSpectralToggled;
		surfaceButton.Toggled -= OnSurfaceToggled;
		electromagneticButton.Toggled -= OnElectromagneticToggled;
		resonanceButton.Toggled -= OnResonanceToggled;
	}

	private void OnReloadPressed() => fragmentCanvas.GenerateFragment();
	private void OnPolarizationToggled(bool enabled) => fragmentCanvas.SetLayer(FragmentCanvas.FilterType.Polarization, enabled);
	private void OnSpectralToggled(bool enabled) => fragmentCanvas.SetLayer(FragmentCanvas.FilterType.Spectral, enabled);
	private void OnSurfaceToggled(bool enabled) => fragmentCanvas.SetLayer(FragmentCanvas.FilterType.Surface, enabled);
	private void OnElectromagneticToggled(bool enabled) => fragmentCanvas.SetLayer(FragmentCanvas.FilterType.Electromagnetic, enabled);
	private void OnResonanceToggled(bool enabled) => fragmentCanvas.SetLayer(FragmentCanvas.FilterType.Resonance, enabled);
}
