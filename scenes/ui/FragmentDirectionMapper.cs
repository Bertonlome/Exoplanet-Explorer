using System;
using Godot;

/// <summary>
/// Converts player-accepted, observable analyzer geometry into a Godot grid bearing. This mapper
/// has no puzzle/truth dependency and cannot snap its ray to a known world endpoint.
/// </summary>
public static class FragmentDirectionMapper
{
	private static readonly string[] CompassLabels =
		{ "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

	public static FragmentDirectionInterpretation Map(
		FragmentArrowCandidate arrow,
		FragmentOrientationHypothesis orientation,
		Vector2 sampleSize,
		float uprightCorrectionDegrees)
	{
		if (arrow == null || orientation == null) return null;
		Vector2 safeSize = new(MathF.Max(sampleSize.X, 1f), MathF.Max(sampleSize.Y, 1f));
		Vector2 scan = (arrow.Tip - arrow.Tail) * safeSize;
		if (scan.LengthSquared() <= 0.0001f) return null;
		scan = scan.Normalized();
		Vector2 upright = scan.Rotated(Mathf.DegToRad(uprightCorrectionDegrees)).Normalized();
		// Analyzer pixels and Godot grid coordinates both use +X right and +Y down.
		Vector2 world = upright;
		float bearing = Mathf.PosMod(
			Mathf.RadToDeg(MathF.Atan2(world.X, -world.Y)),
			360f);
		int compassIndex = Mathf.RoundToInt(bearing / 45f) % CompassLabels.Length;
		return new FragmentDirectionInterpretation
		{
			SourceArrowId = arrow.Id,
			SourceOrientationId = orientation.Id,
			ScanDirection = scan,
			UprightDirection = upright,
			WorldGridDirection = world,
			UprightCorrectionDegrees = uprightCorrectionDegrees,
			BearingDegrees = bearing,
			CompassLabel = CompassLabels[compassIndex]
		};
	}

	public static string FormatBearing(FragmentDirectionInterpretation direction) =>
		direction == null
			? "BEARING: Awaiting accepted arrow and orientation"
			: $"{direction.CompassLabel} · {direction.BearingDegrees:0.0}° · " +
				$"({direction.WorldGridDirection.X:+0.00;-0.00;0.00}, " +
				$"{direction.WorldGridDirection.Y:+0.00;-0.00;0.00})";

	public static bool ValidateCoordinateContract(out string error)
	{
		(FragmentArrowCandidate arrow, string compass, Vector2 vector)[] cases =
		{
			(CreateArrow(Vector2.Right), "E", Vector2.Right),
			(CreateArrow(Vector2.Up), "N", Vector2.Up),
			(CreateArrow(Vector2.Down), "S", Vector2.Down),
			(CreateArrow(new Vector2(1f, -1f)), "NE", new Vector2(1f, -1f).Normalized())
		};
		FragmentOrientationHypothesis orientation = new() { Id = 1 };
		foreach ((FragmentArrowCandidate arrow, string compass, Vector2 expected) in cases)
		{
			FragmentDirectionInterpretation mapped = Map(
				arrow, orientation, new Vector2(960f, 540f), 0f);
			if (mapped == null || mapped.CompassLabel != compass ||
				mapped.WorldGridDirection.DistanceTo(expected) > 0.001f)
			{
				error = $"{compass} contract failed";
				return false;
			}
		}
		error = string.Empty;
		return true;

		static FragmentArrowCandidate CreateArrow(Vector2 pixelDirection)
		{
			Vector2 normalizedDirection = new(
				pixelDirection.X / 960f,
				pixelDirection.Y / 540f);
			return new FragmentArrowCandidate
			{
				Id = 1,
				Tail = new Vector2(0.5f, 0.5f),
				Tip = new Vector2(0.5f, 0.5f) + normalizedDirection
			};
		}
	}
}
