using Godot;


namespace Game.UI;
public partial class Resourceindicator : Node2D
{

	private AnimatedSprite2D animatedSprite2D;
	private Tween activeTween;

	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
		animatedSprite2D = GetNode<AnimatedSprite2D>("%AnimatedSprite2D");

		var duration = GD.RandRange(.7, 1);

		activeTween = CreateTween();
		activeTween.SetLoops();
		activeTween.TweenProperty(animatedSprite2D, "position", Vector2.Up * 4, duration)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.InOut);
		activeTween.TweenProperty(animatedSprite2D, "position", Vector2.Down * 4, duration)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.InOut);
		
		var life = GD.RandRange(2, 3);
		await ToSignal(GetTree().CreateTimer(life), SceneTreeTimer.SignalName.Timeout);
		Destroy();
	}

	public void Destroy()
	{
		if (activeTween != null && activeTween.IsValid())
		{
			activeTween.Kill();
		}

		activeTween = CreateTween();
		activeTween.SetParallel();
		activeTween.TweenInterval(GD.RandRange(.1, .3));
		activeTween.Chain();
		activeTween.TweenProperty(animatedSprite2D, "scale", Vector2.Zero, .3)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.In);
		activeTween.TweenProperty(animatedSprite2D, "position", Vector2.Up * 32, .3)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		activeTween.Chain();
		activeTween.TweenCallback(Callable.From(() => QueueFree()));
	}

}
