using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Geometry-only grouping of observable feature annotations. The detector receives no puzzle,
/// semantic role, glyph identity, or feature provenance.
/// </summary>
public static class FragmentStructureDetector
{
	public static IReadOnlyList<FragmentDetectedStructure> DetectStructures(
		IReadOnlyList<FragmentDetectedFeature> features,
		float connectionDistance = 0.025f,
		int minimumFeatureCount = 2,
		int maximumFeatureCount = 256)
	{
		List<FragmentDetectedFeature> active = new();
		if (features != null)
			foreach (FragmentDetectedFeature feature in features)
				if (feature != null &&
					feature.Disposition != FragmentAnnotationDisposition.Dismissed)
					active.Add(feature);
		if (active.Count > Math.Max(maximumFeatureCount, 2))
		{
			active.Sort((first, second) =>
			{
				int confidence = second.Confidence.CompareTo(first.Confidence);
				return confidence != 0 ? confidence : first.Id.CompareTo(second.Id);
			});
			active.RemoveRange(Math.Max(maximumFeatureCount, 2),
				active.Count - Math.Max(maximumFeatureCount, 2));
		}
		active.Sort((first, second) => first.Id.CompareTo(second.Id));

		int[] parent = new int[active.Count];
		for (int index = 0; index < parent.Length; index++) parent[index] = index;
		float thresholdSquared = MathF.Max(connectionDistance, 0f);
		thresholdSquared *= thresholdSquared;
		for (int first = 0; first < active.Count; first++)
		{
			for (int second = first + 1; second < active.Count; second++)
			{
				if (AreConnected(active[first], active[second], thresholdSquared))
					Union(parent, first, second);
			}
		}

		Dictionary<int, List<FragmentDetectedFeature>> components = new();
		for (int index = 0; index < active.Count; index++)
		{
			int root = Find(parent, index);
			if (!components.TryGetValue(root, out List<FragmentDetectedFeature> component))
			{
				component = new List<FragmentDetectedFeature>();
				components[root] = component;
			}
			component.Add(active[index]);
		}

		List<FragmentDetectedStructure> structures = new();
		foreach (List<FragmentDetectedFeature> component in components.Values)
		{
			if (component.Count < Math.Max(minimumFeatureCount, 1)) continue;
			List<int> ids = component.ConvertAll(feature => feature.Id);
			ids.Sort();
			float confidence = 0f;
			foreach (FragmentDetectedFeature feature in component)
				confidence += Mathf.Clamp(feature.Confidence, 0f, 1f);
			confidence = component.Count == 0 ? 0f : confidence / component.Count;
			structures.Add(new FragmentDetectedStructure
			{
				Confidence = confidence,
				Provenance = FragmentAnnotationProvenance.Rover,
				Disposition = FragmentAnnotationDisposition.Proposed,
				FeatureIds = ids
			});
		}
		structures.Sort((first, second) =>
			first.FeatureIds[0].CompareTo(second.FeatureIds[0]));
		return structures;
	}

	private static bool AreConnected(
		FragmentDetectedFeature first,
		FragmentDetectedFeature second,
		float thresholdSquared)
	{
		foreach ((Vector2 Start, Vector2 End) firstSegment in GetSegments(first))
		{
			foreach ((Vector2 Start, Vector2 End) secondSegment in GetSegments(second))
			{
				if (PointSegmentDistanceSquared(firstSegment.Start, secondSegment) <= thresholdSquared ||
					PointSegmentDistanceSquared(firstSegment.End, secondSegment) <= thresholdSquared ||
					PointSegmentDistanceSquared(secondSegment.Start, firstSegment) <= thresholdSquared ||
					PointSegmentDistanceSquared(secondSegment.End, firstSegment) <= thresholdSquared)
					return true;
			}
		}
		return false;
	}

	private static IEnumerable<(Vector2 Start, Vector2 End)> GetSegments(
		FragmentDetectedFeature feature)
	{
		if (feature.Segments == null || feature.Segments.Count == 0)
		{
			yield return (feature.Start, feature.End);
			yield break;
		}
		foreach (FragmentFeatureSegment segment in feature.Segments)
			yield return (segment.Start, segment.End);
	}

	private static float PointSegmentDistanceSquared(
		Vector2 point,
		(Vector2 Start, Vector2 End) segment)
	{
		Vector2 delta = segment.End - segment.Start;
		float lengthSquared = delta.LengthSquared();
		if (lengthSquared <= 0.0000001f) return point.DistanceSquaredTo(segment.Start);
		float amount = Mathf.Clamp((point - segment.Start).Dot(delta) / lengthSquared, 0f, 1f);
		return point.DistanceSquaredTo(segment.Start + delta * amount);
	}

	private static int Find(int[] parent, int index)
	{
		while (parent[index] != index)
		{
			parent[index] = parent[parent[index]];
			index = parent[index];
		}
		return index;
	}

	private static void Union(int[] parent, int first, int second)
	{
		int firstRoot = Find(parent, first);
		int secondRoot = Find(parent, second);
		if (firstRoot != secondRoot) parent[secondRoot] = firstRoot;
	}
}
