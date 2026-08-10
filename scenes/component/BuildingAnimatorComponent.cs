using System.Linq;
using Game.Autoload;
using Godot;

namespace Game.Component;

public partial class BuildingAnimatorComponent : Node2D
{
	[Signal]
	public delegate void DestroyAnimationFinishedEventHandler();
	[Signal]
	public delegate void MoveAnimationFinishedEventHandler();

	[Export]
	private PackedScene impactParticlesScene;
	[Export]
	private PackedScene destroyParticlesScene;
	[Export]
	private Texture2D maskTexture;

	private Tween activeTween;
	private Node2D animationRootNode;
	private Sprite2D maskNode;
	private AudioStreamPlayer impactAudioStreamPlayer;
	private AnimatedSprite2D robotSprite;
    private AnimatedSprite2D loadingIcon;

	public override void _Ready()
	{
		YSortEnabled = false;
		impactAudioStreamPlayer = GetNode<AudioStreamPlayer>("ImpactAudioStreamPlayer");
		SetupNodes();
	}

	public void PlayInAnimation()
	{
		if (animationRootNode == null) return;

		if (activeTween != null && activeTween.IsValid())
		{
			activeTween.Kill();
		}
		activeTween = CreateTween();
		activeTween
			.TweenProperty(animationRootNode, "position", Vector2.Zero, .3)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In)
			.From(Vector2.Up * 128);
		activeTween.TweenCallback(Callable.From(() =>
		{
			var impactParticles = impactParticlesScene.Instantiate<Node2D>();
			Owner.GetParent().AddChild(impactParticles);
			impactParticles.GlobalPosition = GlobalPosition;
			impactAudioStreamPlayer.Play();
			GameCamera.Shake();
		}));
		activeTween
			.TweenProperty(animationRootNode, "position", Vector2.Up * 16, .1)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		activeTween
			.TweenProperty(animationRootNode, "position", Vector2.Zero, .1)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
	}

	public void PlayDestroyAnimation()
	{
		if (animationRootNode == null) return;

		if (activeTween != null && activeTween.IsValid())
		{
			activeTween.Kill();
		}

		animationRootNode.Position = Vector2.Zero;

		maskNode.ClipChildren = ClipChildrenMode.Only;
		maskNode.Texture = maskTexture;

		var destroyParticles = destroyParticlesScene.Instantiate<Node2D>();
		Owner.GetParent().AddChild(destroyParticles);
		destroyParticles.GlobalPosition = GlobalPosition;

		AudioHelpers.PlayBuildingDestruction();
		GameCamera.Shake();

		activeTween = CreateTween();
		activeTween.TweenProperty(animationRootNode, "rotation_degrees", -5, .1);
		activeTween.TweenProperty(animationRootNode, "rotation_degrees", 5, .1);
		activeTween.TweenProperty(animationRootNode, "rotation_degrees", -2, .1);
		activeTween.TweenProperty(animationRootNode, "rotation_degrees", 2, .1);
		activeTween.TweenProperty(animationRootNode, "rotation_degrees", 0, .1);

		activeTween.TweenProperty(animationRootNode, "position", Vector2.Down * 300, .4)
			.SetTrans(Tween.TransitionType.Quart)
			.SetEase(Tween.EaseType.In);
		activeTween.Finished += () =>
		{
			EmitSignal(SignalName.DestroyAnimationFinished);
		};
	}

	public void PlayMoveAnimation(Vector2I originPos, Vector2I destinationPos)
	{
		AudioHelpers.PlayMove();
	}

	private void SetupNodes()
	{
		var spriteNode = this.GetFirstNodeOfType<Node2D>();
		if (spriteNode == null)
		{
			return;
		}
		RemoveChild(spriteNode);
		Position = new Vector2(spriteNode.Position.X, spriteNode.Position.Y);

		maskNode = new Sprite2D
		{
			Centered = false,
			Offset = new Vector2(-160, -256),
		};
		AddChild(maskNode);

		animationRootNode = new Node2D();
		maskNode.AddChild(animationRootNode);

		animationRootNode.AddChild(spriteNode);
		spriteNode.Position = new Vector2(0, 0);


		loadingIcon = GetNodeOrNull<AnimatedSprite2D>("%LoadingIconAnimatedSprite2D");
		// Try to find an existing loading icon anywhere under the animation root or owner.
		// Prefer the editor-placed node so its position/scale are preserved.
		var found = FindAnimatedSpriteRecursive(animationRootNode, "LoadingIconAnimatedSprite2D");
		if (found == null && Owner != null)
		{
			found = FindAnimatedSpriteRecursive(Owner, "LoadingIconAnimatedSprite2D");
		}
		if (found != null)
		{
			loadingIcon = found;
			loadingIcon.Visible = false; // ensure hidden initially but keep editor transform
		}
		else
		{
			loadingIcon = new AnimatedSprite2D
			{
				Name = "LoadingIconAnimatedSprite2D",
				Visible = false,
				Position = new Vector2(0, -48),
				Scale = new Vector2(0.5f, 0.5f)
			};
			animationRootNode.AddChild(loadingIcon);
		}
	}

	private AnimatedSprite2D FindAnimatedSpriteRecursive(Node start, string name)
	{
		if (start == null) return null;
		foreach (var childObj in start.GetChildren())
		{
			if (childObj is Node childNode)
			{
				if (childNode.Name == name && childNode is AnimatedSprite2D sprite)
					return sprite;
				var found = FindAnimatedSpriteRecursive(childNode, name);
				if (found != null) return found;
			}
		}
		return null;
	}

	public void ShowLoading()
	{
		if (loadingIcon == null) return;
		loadingIcon.Visible = true;
	}

	public void HideLoading()
	{
		if (loadingIcon == null) return;
		loadingIcon.Visible = false;
	}
}
