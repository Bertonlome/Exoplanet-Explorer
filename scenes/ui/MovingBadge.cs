using Godot;

public partial class MovingBadge : TextureRect
{
    [Export]
    public float Speed = 180f;

    private Vector2 _direction = new Vector2(1f, 0.65f).Normalized();

    public override void _Process(double delta)
    {
        Vector2 viewportSize = GetViewportRect().Size;

        Position += _direction * Speed * (float)delta;

        // Left edge
        if (Position.X <= 0)
        {
            Position = new Vector2(0, Position.Y);
            _direction.X *= -1;
        }

        // Right edge
        if (Position.X + Size.X >= viewportSize.X)
        {
            Position = new Vector2(
                viewportSize.X - Size.X,
                Position.Y
            );

            _direction.X *= -1;
        }

        // Top edge
        if (Position.Y <= 0)
        {
            Position = new Vector2(Position.X, 0);
            _direction.Y *= -1;
        }

        // Bottom edge
        if (Position.Y + Size.Y >= viewportSize.Y)
        {
            Position = new Vector2(
                Position.X,
                viewportSize.Y - Size.Y
            );

            _direction.Y *= -1;
        }
    }
}