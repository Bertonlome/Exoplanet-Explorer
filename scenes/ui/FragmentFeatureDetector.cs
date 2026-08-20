using System;
using System.Collections.Generic;
using Godot;

public static class FragmentFeatureDetector
{
    private const float MinimumSegmentLength = 0.008f;
    private const float MinimumIntensity = 0.075f;
    private const float DuplicateTolerance = 0.004f;
    private const float EndpointConnectionTolerance = 0.014f;
	private const float MinimumDirectionContinuity = 0.82f;
	private const float MaximumIntensityDifference = 0.24f;
	private const float MaximumWidthRatio = 2.2f;
	// A complete visible glyph can legitimately contain many disconnected or perpendicular stroke
	// groups. Ten groups proved too small and discarded most of otherwise observable figures before
	// structure reconstruction. The primitive comparison cap below remains the expensive-work guard.
	private const int MaximumFeatureCount = 48;
	private const int MaximumComparedPrimitiveCount = 384;

    private sealed class FeatureGroup
    {
        public List<FragmentObservablePrimitive> Primitives { get; } = new();
        public float Score { get; set; }
        public int StableOrder { get; set; }
    }

    public static IReadOnlyList<FragmentDetectedFeature> DetectFeatures(
        FragmentObservableScan scan)
    {
        if (scan?.Primitives == null) return Array.Empty<FragmentDetectedFeature>();
        List<FragmentObservablePrimitive> primitives = GetStrongestUniquePrimitives(scan.Primitives);
        if (primitives.Count == 0) return Array.Empty<FragmentDetectedFeature>();

        int[] parents = new int[primitives.Count];
        for (int i = 0; i < parents.Length; i++) parents[i] = i;
        for (int i = 0; i < primitives.Count; i++)
        {
            for (int j = i + 1; j < primitives.Count; j++)
            {
                if (EndpointsConnect(primitives[i], primitives[j])) Union(parents, i, j);
            }
        }

        Dictionary<int, FeatureGroup> grouped = new();
        for (int i = 0; i < primitives.Count; i++)
        {
            int root = Find(parents, i);
            if (!grouped.TryGetValue(root, out FeatureGroup group))
            {
                group = new FeatureGroup { StableOrder = primitives[i].Id };
                grouped[root] = group;
            }
            group.Primitives.Add(primitives[i]);
            group.StableOrder = Math.Min(group.StableOrder, primitives[i].Id);
        }

        List<FeatureGroup> groups = new(grouped.Values);
        foreach (FeatureGroup group in groups) group.Score = ScoreGroup(group);
        groups.RemoveAll(group => group.Score <= 0f);
        groups.Sort((first, second) =>
        {
            int scoreOrder = second.Score.CompareTo(first.Score);
            return scoreOrder != 0 ? scoreOrder : first.StableOrder.CompareTo(second.StableOrder);
        });
        if (groups.Count > MaximumFeatureCount)
            groups.RemoveRange(MaximumFeatureCount, groups.Count - MaximumFeatureCount);

        List<FragmentDetectedFeature> features = new(groups.Count);
        foreach (FeatureGroup group in groups)
        {
            FindFarthestEndpoints(group.Primitives, out Vector2 start, out Vector2 end);
			// Segments are the Rover's observable geometry contract, not merely a decorative highlight.
			// Retaining only a visual subset here also starved structure and orientation estimation.
			List<FragmentFeatureSegment> segments = new(group.Primitives.Count);
			foreach (FragmentObservablePrimitive primitive in group.Primitives)
            {
                segments.Add(new FragmentFeatureSegment
                {
                    Start = primitive.Start,
                    End = primitive.End
                });
            }

            features.Add(new FragmentDetectedFeature
            {
                Id = group.StableOrder,
                Start = start,
                End = end,
                Segments = segments,
                Confidence = Mathf.Clamp(group.Score, 0.2f, 0.98f),
                Provenance = FragmentAnnotationProvenance.Rover,
                Disposition = FragmentAnnotationDisposition.Proposed
            });
        }

        return features;
    }

    private static List<FragmentObservablePrimitive> GetStrongestUniquePrimitives(
        IReadOnlyList<FragmentObservablePrimitive> source)
    {
		List<FragmentObservablePrimitive> bounded = new();
		foreach (FragmentObservablePrimitive primitive in source)
		{
			if (primitive == null ||
				primitive.Intensity < MinimumIntensity ||
				primitive.Start.DistanceTo(primitive.End) < MinimumSegmentLength)
				continue;
			bounded.Add(primitive);
		}
		bounded.Sort((first, second) =>
		{
			float firstSalience = first.Intensity * first.Start.DistanceTo(first.End);
			float secondSalience = second.Intensity * second.Start.DistanceTo(second.End);
			int scoreOrder = secondSalience.CompareTo(firstSalience);
			return scoreOrder != 0 ? scoreOrder : first.Id.CompareTo(second.Id);
		});
		if (bounded.Count > MaximumComparedPrimitiveCount)
			bounded.RemoveRange(MaximumComparedPrimitiveCount,
				bounded.Count - MaximumComparedPrimitiveCount);

        List<FragmentObservablePrimitive> unique = new();
		foreach (FragmentObservablePrimitive primitive in bounded)
        {
            int duplicateIndex = unique.FindIndex(existing => SameSegment(
                existing.Start,
                existing.End,
                primitive.Start,
                primitive.End));
            if (duplicateIndex < 0)
            {
                unique.Add(primitive);
            }
            else if (primitive.Intensity > unique[duplicateIndex].Intensity)
            {
                unique[duplicateIndex] = primitive;
            }
        }
        return unique;
    }

    private static float ScoreGroup(FeatureGroup group)
    {
        float totalLength = 0f;
        float weightedIntensity = 0f;
        int intersections = 0;
        for (int i = 0; i < group.Primitives.Count; i++)
        {
            FragmentObservablePrimitive primitive = group.Primitives[i];
            float length = primitive.Start.DistanceTo(primitive.End);
            totalLength += length;
            weightedIntensity += primitive.Intensity * length;
            for (int j = i + 1; j < group.Primitives.Count; j++)
            {
                Variant intersection = Geometry2D.SegmentIntersectsSegment(
                    primitive.Start,
                    primitive.End,
                    group.Primitives[j].Start,
                    group.Primitives[j].End);
                if (intersection.VariantType != Variant.Type.Nil) intersections++;
            }
        }

        if (totalLength < 0.018f) return 0f;
        float averageIntensity = weightedIntensity / MathF.Max(totalLength, 0.001f);
        float coherence = Mathf.Clamp((group.Primitives.Count - 1) * 0.055f, 0f, 0.28f);
        float structure = Mathf.Clamp(intersections * 0.035f, 0f, 0.2f);
        return 0.12f + averageIntensity * 0.48f +
            Mathf.Clamp(totalLength * 0.8f, 0f, 0.3f) + coherence + structure;
    }

    private static bool EndpointsConnect(
        FragmentObservablePrimitive first,
        FragmentObservablePrimitive second)
    {
        float toleranceSquared = EndpointConnectionTolerance * EndpointConnectionTolerance;
        bool endpointsTouch = first.Start.DistanceSquaredTo(second.Start) <= toleranceSquared ||
            first.Start.DistanceSquaredTo(second.End) <= toleranceSquared ||
            first.End.DistanceSquaredTo(second.Start) <= toleranceSquared ||
            first.End.DistanceSquaredTo(second.End) <= toleranceSquared;
		if (!endpointsTouch) return false;

		Vector2 firstDirection = (first.End - first.Start).Normalized();
		Vector2 secondDirection = (second.End - second.Start).Normalized();
		float directionContinuity = MathF.Abs(firstDirection.Dot(secondDirection));
		float smallerWidth = MathF.Max(MathF.Min(first.Width, second.Width), 0.0001f);
		float widthRatio = MathF.Max(first.Width, second.Width) / smallerWidth;
		return directionContinuity >= MinimumDirectionContinuity &&
			MathF.Abs(first.Intensity - second.Intensity) <= MaximumIntensityDifference &&
			widthRatio <= MaximumWidthRatio;
    }

    private static bool SameSegment(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd)
    {
        float toleranceSquared = DuplicateTolerance * DuplicateTolerance;
        return (firstStart.DistanceSquaredTo(secondStart) <= toleranceSquared &&
                firstEnd.DistanceSquaredTo(secondEnd) <= toleranceSquared) ||
            (firstStart.DistanceSquaredTo(secondEnd) <= toleranceSquared &&
             firstEnd.DistanceSquaredTo(secondStart) <= toleranceSquared);
    }

    private static int Find(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }
        return index;
    }

    private static void Union(int[] parents, int first, int second)
    {
        int firstRoot = Find(parents, first);
        int secondRoot = Find(parents, second);
        if (firstRoot != secondRoot) parents[secondRoot] = firstRoot;
    }

    private static void FindFarthestEndpoints(
        IReadOnlyList<FragmentObservablePrimitive> primitives,
        out Vector2 start,
        out Vector2 end)
    {
        List<Vector2> endpoints = new(primitives.Count * 2);
        foreach (FragmentObservablePrimitive primitive in primitives)
        {
            endpoints.Add(primitive.Start);
            endpoints.Add(primitive.End);
        }

        start = endpoints[0];
        end = endpoints[1];
        float greatestDistance = start.DistanceSquaredTo(end);
        for (int i = 0; i < endpoints.Count; i++)
        {
            for (int j = i + 1; j < endpoints.Count; j++)
            {
                float distance = endpoints[i].DistanceSquaredTo(endpoints[j]);
                if (distance <= greatestDistance) continue;
                greatestDistance = distance;
                start = endpoints[i];
                end = endpoints[j];
            }
        }
    }
}
