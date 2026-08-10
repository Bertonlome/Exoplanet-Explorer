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
}