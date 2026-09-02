using Godot;

namespace Game;

public partial class Monolith : Node2D
{
	private const int ACTIVE_Z_INDEX = 1000;

	[Export]
	private Texture2D activeTexture;

	private Node2D upDownRoot;
	private Sprite2D sprite;

	public override void _Ready()
	{
		sprite = GetNode<Sprite2D>("%MonolithSprite2D");
	}

	public void SetActive()
	{
		// The activation tween changes the monolith's Y position. Since the world
		// root is Y-sorted, that could place the rover in front during the win
		// animation. Use an absolute foreground depth while active; CanvasLayer UI
		// (including the level-complete screen) still renders above the world.
		ZAsRelative = false;
		ZIndex = ACTIVE_Z_INDEX;

		upDownRoot = GetNode<Node2D>("%UpDownRoot");
		sprite.Texture = activeTexture;
		var upDownTween = CreateTween();
		upDownTween.SetLoops(0);
		upDownTween.TweenProperty(upDownRoot, "position", Vector2.Down * 6, .3)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Quad);
		upDownTween.TweenProperty(upDownRoot, "position", Vector2.Up * 6, .3)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Quad); ;
	}

	public void SetVisible()
	{
		this.Visible = true;
	}
}
