using Game.Autoload;
using Game.Resources.Building;
using Godot;

namespace Game.UI;

public partial class BuildingSection : PanelContainer
{
	[Signal]
	public delegate void SelectButtonPressedEventHandler();
	[Export]
	private Texture2D materialIcon;

	private Label titleLabel;
	private Label descriptionLabel;
	private Label costLabel;
	private TextureRect materialIconRect;
	private Button selectButton;

	public override void _Ready()
	{
		titleLabel = GetNode<Label>("%TitleLabel");
		descriptionLabel = GetNode<Label>("%DescriptionLabel");
		costLabel = GetNode<Label>("%CostLabel");
		materialIconRect = GetNodeOrNull<TextureRect>("%MaterialIcon");
		selectButton = GetNode<Button>("%Button");

		AudioHelpers.RegisterButtons(new Button[] {selectButton});

		selectButton.Pressed += OnSelectButtonPressed;
	}

	public void SetBuildingResource(BuildingResource buildingResource)
	{
		titleLabel.Text = buildingResource.DisplayName;
		costLabel.Text = $"{buildingResource.ResourceCost}";
		if (materialIconRect != null)
		{
			materialIconRect.Texture = materialIcon;
		}
		descriptionLabel.Text = buildingResource.Description;
	}

	private void OnSelectButtonPressed()
	{
		EmitSignal(SignalName.SelectButtonPressed);
	}
}
