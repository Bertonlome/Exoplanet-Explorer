using Godot;

public partial class ResizeHandle : Control
{
    [Export]
    public Control Target;

    [Export]
    public float MinHeight = 100f;

    [Export]
    public float MaxHeight = 500f;

	private Game.Autoload.Cursor _cursor;

    private bool _dragging;
    private float _startMouseY;
    private float _startHeight;

    public override void _Ready()
    {
		_cursor = GetNode<Game.Autoload.Cursor>("/root/Cursor");

		MouseEntered += () =>
		{
			_cursor.SetCursor(Game.Autoload.Cursor.CursorType.VerticalResize);
		};
		MouseExited += () =>
		{
			_cursor.SetCursor(Game.Autoload.Cursor.CursorType.Default);
		};
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _dragging = true;
				_cursor.SetCursor(Game.Autoload.Cursor.CursorType.VerticalResize);
                _startMouseY = GetGlobalMousePosition().Y;
                _startHeight = Target.Size.Y;
            }
            else
            {
                _dragging = false;
				_cursor.SetCursor(Game.Autoload.Cursor.CursorType.Default);
            }

            AcceptEvent();
        }

        if (@event is InputEventMouseMotion && _dragging)
        {
            float deltaY =
                GetGlobalMousePosition().Y - _startMouseY;

            float newHeight = Mathf.Clamp(
                _startHeight + deltaY,
                MinHeight,
                MaxHeight
            );

            Target.CustomMinimumSize = new Vector2(
                Target.CustomMinimumSize.X,
                newHeight
            );

            AcceptEvent();
        }
    }
}