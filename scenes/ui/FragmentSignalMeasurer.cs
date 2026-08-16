using System;
using System.Collections.Generic;
using Godot;

public static class FragmentSignalMeasurer
{
	private const float SegmentMatchTolerance = 0.014f;
	private const float MinimumDirectionAgreement = 0.7f;
	private const float CoverageSampleSpacing = 0.006f;

	private sealed class VisibleSegment
	{
		public Vector2 Start;
		public Vector2 End;
	}

	private sealed class FeatureSegment
	{
		public int FeatureId;
		public Vector2 Start;
		public Vector2 End;
		public float Length;
	}

	public static FragmentSignalMetrics Measure(
		FragmentObservableScan scan,
		Rect2 normalizedRegion,
		IReadOnlyList<FragmentDetectedFeature> detectedFeatures,
		IReadOnlyList<int> expectedFeatureIds)
	{
		if (scan?.Primitives == null || detectedFeatures == null)
			return new FragmentSignalMetrics();
		Rect2 scope = ClampNormalized(normalizedRegion);
		if (scope.Size.X <= 0.0001f || scope.Size.Y <= 0.0001f)
			return new FragmentSignalMetrics();

		List<VisibleSegment> visibleSegments = BuildVisibleSegments(scan, scope);
		List<FeatureSegment> signalSegments = new();
		List<FeatureSegment> dismissedSegments = new();
		HashSet<int> expectedIds = new(expectedFeatureIds ?? Array.Empty<int>());
		foreach (FragmentDetectedFeature feature in detectedFeatures)
		{
			if (feature == null) continue;
			bool belongsToRegion = expectedIds.Contains(feature.Id) ||
				scope.HasPoint(GetFeatureCenter(feature));
			if (!belongsToRegion) continue;
			if (feature.Disposition == FragmentAnnotationDisposition.Dismissed)
			{
				expectedIds.Remove(feature.Id);
				AddFeatureSegments(feature, scope, dismissedSegments);
				continue;
			}
			expectedIds.Add(feature.Id);
			AddFeatureSegments(feature, scope, signalSegments);
		}

		if (visibleSegments.Count == 0 || expectedIds.Count == 0 || signalSegments.Count == 0)
			return new FragmentSignalMetrics();

		float completenessTotal = 0f;
		int measuredFeatureCount = 0;
		float totalSignalLength = 0f;
		foreach (int featureId in expectedIds)
		{
			List<FeatureSegment> expectedSegments = signalSegments.FindAll(segment =>
				segment.FeatureId == featureId);
			if (expectedSegments.Count == 0)
			{
				measuredFeatureCount++;
				continue;
			}
			float featureLength = 0f;
			float coveredLength = 0f;
			foreach (FeatureSegment expected in expectedSegments)
			{
				featureLength += expected.Length;
				coveredLength += expected.Length * MeasureSegmentCoverage(expected, visibleSegments);
			}
			totalSignalLength += featureLength;
			completenessTotal += coveredLength / MathF.Max(featureLength, 0.00001f);
			measuredFeatureCount++;
		}

		float completeness = completenessTotal / Math.Max(measuredFeatureCount, 1);
		float visibleDismissedLength = 0f;
		foreach (FeatureSegment dismissed in dismissedSegments)
			visibleDismissedLength += dismissed.Length *
				MeasureSegmentCoverage(dismissed, visibleSegments);
		float selectivity = totalSignalLength /
			MathF.Max(totalSignalLength + visibleDismissedLength, 0.00001f);
		return new FragmentSignalMetrics
		{
			SignalToNoise = Mathf.Clamp(completeness * selectivity, 0f, 1f)
		};
	}

	private static List<VisibleSegment> BuildVisibleSegments(
		FragmentObservableScan scan,
		Rect2 scope)
	{
		List<VisibleSegment> result = new();
		foreach (FragmentObservablePrimitive primitive in scan.Primitives)
		{
			Vector2 start = primitive.Start;
			Vector2 end = primitive.End;
			if (!ClipSegment(scope, ref start, ref end)) continue;
			float length = start.DistanceTo(end);
			if (length <= 0.00001f) continue;
			result.Add(new VisibleSegment
			{
				Start = start,
				End = end
			});
		}
		return result;
	}

	private static void AddFeatureSegments(
		FragmentDetectedFeature feature,
		Rect2 scope,
		List<FeatureSegment> destination)
	{
		if (feature.Segments.Count == 0)
		{
			AddSegment(feature.Id, feature.Start, feature.End, scope, destination);
			return;
		}
		foreach (FragmentFeatureSegment segment in feature.Segments)
			AddSegment(feature.Id, segment.Start, segment.End, scope, destination);
	}

	private static void AddSegment(
		int featureId,
		Vector2 start,
		Vector2 end,
		Rect2 scope,
		List<FeatureSegment> destination)
	{
		if (!ClipSegment(scope, ref start, ref end)) return;
		float length = start.DistanceTo(end);
		if (length <= 0.00001f) return;
		destination.Add(new FeatureSegment
		{
			FeatureId = featureId,
			Start = start,
			End = end,
			Length = length
		});
	}

	private static float MeasureSegmentCoverage(
		FeatureSegment feature,
		IReadOnlyList<VisibleSegment> visibleSegments)
	{
		Vector2 featureDelta = feature.End - feature.Start;
		Vector2 featureDirection = featureDelta.Normalized();
		int sampleCount = Mathf.Clamp(
			Mathf.CeilToInt(feature.Length / CoverageSampleSpacing) + 1,
			4,
			48);
		int covered = 0;
		for (int sample = 0; sample < sampleCount; sample++)
		{
			Vector2 point = feature.Start + featureDelta * sample / (sampleCount - 1f);
			bool hasMatch = false;
			foreach (VisibleSegment visible in visibleSegments)
			{
				Vector2 visibleDelta = visible.End - visible.Start;
				if (visibleDelta.LengthSquared() <= 0.0000001f ||
					MathF.Abs(featureDirection.Dot(visibleDelta.Normalized())) < MinimumDirectionAgreement)
					continue;
				if (DistanceToSegment(point, visible.Start, visible.End) > SegmentMatchTolerance)
					continue;
				hasMatch = true;
				break;
			}
			if (hasMatch) covered++;
		}
		return (float)covered / sampleCount;
	}

	private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
	{
		Vector2 delta = end - start;
		float lengthSquared = delta.LengthSquared();
		if (lengthSquared <= 0.0000001f) return point.DistanceTo(start);
		float amount = Mathf.Clamp((point - start).Dot(delta) / lengthSquared, 0f, 1f);
		return point.DistanceTo(start + delta * amount);
	}

	private static Vector2 GetFeatureCenter(FragmentDetectedFeature feature)
	{
		if (feature.Segments.Count == 0) return (feature.Start + feature.End) * 0.5f;
		Vector2 sum = Vector2.Zero;
		foreach (FragmentFeatureSegment segment in feature.Segments)
			sum += (segment.Start + segment.End) * 0.5f;
		return sum / feature.Segments.Count;
	}

	private static Rect2 ClampNormalized(Rect2 bounds)
	{
		Vector2 start = bounds.Position.Clamp(Vector2.Zero, Vector2.One);
		Vector2 end = bounds.End.Clamp(Vector2.Zero, Vector2.One);
		return new Rect2(start, end - start);
	}

	private static bool ClipSegment(Rect2 rectangle, ref Vector2 start, ref Vector2 end)
	{
		Vector2 delta = end - start;
		float minimum = 0f;
		float maximum = 1f;
		if (!Clip(-delta.X, start.X - rectangle.Position.X, ref minimum, ref maximum) ||
			!Clip(delta.X, rectangle.End.X - start.X, ref minimum, ref maximum) ||
			!Clip(-delta.Y, start.Y - rectangle.Position.Y, ref minimum, ref maximum) ||
			!Clip(delta.Y, rectangle.End.Y - start.Y, ref minimum, ref maximum)) return false;
		Vector2 originalStart = start;
		start = originalStart + delta * minimum;
		end = originalStart + delta * maximum;
		return true;
	}

	private static bool Clip(float direction, float distance, ref float minimum, ref float maximum)
	{
		if (Mathf.IsZeroApprox(direction)) return distance >= 0f;
		float ratio = distance / direction;
		if (direction < 0f)
		{
			if (ratio > maximum) return false;
			if (ratio > minimum) minimum = ratio;
		}
		else
		{
			if (ratio < minimum) return false;
			if (ratio < maximum) maximum = ratio;
		}
		return true;
	}
}
