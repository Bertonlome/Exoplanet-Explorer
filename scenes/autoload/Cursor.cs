using Godot;

namespace Game.Autoload;

public partial class Cursor : CanvasLayer
{
    [Export] public Texture2D DefaultTexture;
    [Export] public Texture2D VerticalResizeTexture;

    private Sprite2D sprite2D;

    public enum CursorType
    {
        Default,
        VerticalResize
    }

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        sprite2D = GetNode<Sprite2D>("Sprite2D");
        SetCursor(CursorType.Default);
    }

    public override void _Process(double delta)
    {
        sprite2D.GlobalPosition = sprite2D.GetGlobalMousePosition();
    }

    public void SetCursor(CursorType type)
    {
        sprite2D.Texture = type switch
        {
            CursorType.VerticalResize => VerticalResizeTexture,
            _ => DefaultTexture
        };
    }

	/// <summary>
	/// Uses a native cursor while a separate Window is visible. CanvasLayer cursors cannot render
	/// above embedded/native window viewports, whereas an OS cursor always remains on top.
	/// </summary>
	public void SetPopupCursorOverride(bool enabled)
	{
		if (sprite2D == null) return;
		sprite2D.Visible = !enabled;
		if (enabled)
		{
			Input.SetCustomMouseCursor(
				DefaultTexture,
				Input.CursorShape.Arrow,
				new Vector2(24f, 19f));
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		else
		{
			Input.SetCustomMouseCursor(null);
			Input.MouseMode = Input.MouseModeEnum.Hidden;
		}
	}
}
