using Godot;
using System;

public partial class MonolithFragment : Node2D
{
	[Export]
	public Texture2D[] Variants;
	private Sprite2D fragmentSprite;
	public Texture2D FragmentTexture
	{
		get
		{
			if (fragmentSprite?.Texture != null) return fragmentSprite.Texture;
			int variantIndex = (int)currentVariant;
			return Variants != null && variantIndex >= 0 && variantIndex < Variants.Length
				? Variants[variantIndex]
				: null;
		}
	}
    public Variant currentVariant;
    public enum Variant
    {
        Hominid,
        Chip,
        Television
    }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (Variants.Length == 0)
			return;

        currentVariant = (Variant)GD.RandRange(0, Variants.Length - 1);
		fragmentSprite = GetNode<Sprite2D>("%MonolithFragmentSprite2D");
		fragmentSprite.Texture = Variants[(int)currentVariant];
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
