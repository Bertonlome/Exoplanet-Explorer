using Godot;

public partial class TrunkElement : Sprite2D
{
    [Export]
    public Texture2D[] Variants;

    public override void _Ready()
    {
        if (Variants.Length == 0)
            return;

        Texture = Variants[GD.RandRange(0, Variants.Length - 1)];
    }
}