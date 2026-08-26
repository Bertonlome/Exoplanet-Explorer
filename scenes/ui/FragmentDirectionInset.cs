using Godot;

public partial class FragmentDirectionInset : Control
{
	private FragmentDirectionInterpretation direction;

	public void SetDirection(FragmentDirectionInterpretation value)
	{
		direction = value;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.015f, 0.025f, 0.045f, 0.96f), true);
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.1f, 0.85f, 1f, 0.8f), false, 1.5f);
		if (direction == null)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(12f, 28f),
				"ACCEPT A# TO MAP · SCREEN UP = NORTH", HorizontalAlignment.Left, -1, 13,
				new Color(0.7f, 0.78f, 0.84f));
			return;
		}

		Color color = new(0.25f, 1f, 0.45f);
		Vector2 center = new(Size.X * 0.5f, Size.Y * 0.44f);
		float radius = Mathf.Clamp(Mathf.Min(Size.X, Size.Y) * 0.29f, 45f, 88f);
		DrawString(ThemeDB.FallbackFont, new Vector2(12f, 25f), "WORLD / GRID BEARING",
			HorizontalAlignment.Left, -1, 15, color);
		DrawCircle(center, radius, new Color(0f, 0f, 0f, 0.48f), true);
		DrawCircle(center, radius, color, false, 2f);
		DrawLine(center - Vector2.Right * radius, center + Vector2.Right * radius,
			new Color(1f, 1f, 1f, 0.2f), 1f);
		DrawLine(center - Vector2.Up * radius, center + Vector2.Up * radius,
			new Color(1f, 1f, 1f, 0.2f), 1f);
		DrawCompassLabel("N", center + Vector2.Up * (radius + 8f), true);
		DrawCompassLabel("E", center + Vector2.Right * (radius + 12f), false);
		DrawCompassLabel("S", center + Vector2.Down * (radius + 18f), true);
		DrawCompassLabel("W", center + Vector2.Left * (radius + 20f), false);
		Vector2 vector = direction.WorldGridDirection.Normalized();
		Vector2 tip = center + vector * (radius - 8f);
		DrawLine(center, tip, Colors.Black, 9f, true);
		DrawLine(center, tip, color, 4.5f, true);
		DrawArrowHead(tip, vector, color);
		void DrawCompassLabel(string label, Vector2 position, bool centered)
		{
			Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(
				label, HorizontalAlignment.Left, -1, 12);
			Vector2 origin = centered
				? position - new Vector2(textSize.X * 0.5f, 0f)
				: position - new Vector2(textSize.X * 0.5f, textSize.Y * 0.5f);
			DrawString(ThemeDB.FallbackFont, origin, label,
				HorizontalAlignment.Left, -1, 12, Colors.White);
		}
	}

	private void DrawArrowHead(Vector2 tip, Vector2 direction, Color color)
	{
		Vector2 back = -direction;
		DrawLine(tip, tip + back.Rotated(0.6f) * 9f, color, 3f);
		DrawLine(tip, tip + back.Rotated(-0.6f) * 9f, color, 3f);
	}
}
