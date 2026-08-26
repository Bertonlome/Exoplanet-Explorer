using System;
using System.Collections.Generic;
using Godot;

public static class FragmentRegionDetector
{
	private const float FeaturePadding = 0.014f;
	private const float CoreNeighbourGap = 0.026f;
	private const float PeripheralGap = 0.014f;
	private const float SparseClusterGap = 0.012f;
	private const float MaximumMergedWidth = 0.36f;
	private const float MaximumMergedHeight = 0.46f;
	private const float MaximumMergedArea = 0.13f;
	private const int MinimumCoreStrokeUnits = 5;
	private const int VeryHighDensityStrokeUnits = 6;
	private const float VeryHighDensityThreshold = 0.62f;
	private const int MaximumRegionCount = 6;

	private sealed class RegionGroup
	{
		public Rect2 Bounds;
		public List<FragmentDetectedFeature> Features { get; } = new();
		public int StrokeUnits;
		public float Density;
		public int StableOrder = int.MaxValue;
		public bool HasCore;
	}

	public static IReadOnlyList<FragmentCandidateRegion> GroupCandidateRegions(
		IReadOnlyList<FragmentDetectedFeature> features)
	{
		if (features == null) return Array.Empty<FragmentCandidateRegion>();
		List<FragmentDetectedFeature> active = new();
		foreach (FragmentDetectedFeature feature in features)
			if (feature != null &&
				feature.Disposition != FragmentAnnotationDisposition.Dismissed)
				active.Add(feature);
		if (active.Count == 0) return Array.Empty<FragmentCandidateRegion>();

		Rect2[] bounds = new Rect2[active.Count];
		int[] strokeUnits = new int[active.Count];
		bool[] core = new bool[active.Count];
		for (int index = 0; index < active.Count; index++)
		{
			bounds[index] = GetFeatureBounds(active[index]);
			strokeUnits[index] = GetStrokeUnits(active[index]);
		}
		for (int index = 0; index < active.Count; index++)
		{
			int neighbourCount = 0;
			int localStrokeUnits = strokeUnits[index];
			for (int other = 0; other < active.Count; other++)
			{
				if (other == index || FeatureGap(active[index], active[other]) > CoreNeighbourGap)
					continue;
				neighbourCount++;
				localStrokeUnits += strokeUnits[other];
			}
			core[index] = strokeUnits[index] >= 4 ||
				(neighbourCount >= 2 && localStrokeUnits >= MinimumCoreStrokeUnits);
		}

		List<RegionGroup> groups = BuildCoreGroups(active, bounds, strokeUnits, core);
		AttachSupportedPeriphery(groups, active, bounds, strokeUnits, core);
		BuildSparseGroups(groups, active, bounds, strokeUnits, core);
		foreach (RegionGroup group in groups)
			group.Density = CalculateDensity(group);
		groups.Sort(CompareGroups);

		// Once a clearly dense glyph-like cluster exists, proposing sparse vein regions adds noise
		// without helping the player. Keep only the top density tier in that case.
		if (groups.Count > 0 && IsVeryHighDensity(groups[0]))
		{
			float densityFloor = MathF.Max(VeryHighDensityThreshold, groups[0].Density - 0.12f);
			groups.RemoveAll(group => !IsVeryHighDensity(group) || group.Density < densityFloor);
		}
		if (groups.Count > MaximumRegionCount)
			groups.RemoveRange(MaximumRegionCount, groups.Count - MaximumRegionCount);

		List<FragmentCandidateRegion> regions = new(groups.Count);
		for (int index = 0; index < groups.Count; index++)
		{
			RegionGroup group = groups[index];
			regions.Add(new FragmentCandidateRegion
			{
				Id = index + 1,
				NormalizedBounds = ClampNormalized(group.Bounds.Grow(FeaturePadding)),
				// Retained as an internal ranking value; it is not displayed to the player.
				Confidence = group.Density,
				Provenance = FragmentAnnotationProvenance.Rover,
				Disposition = FragmentAnnotationDisposition.Proposed,
				FeatureIds = group.Features.ConvertAll(feature => feature.Id)
			});
		}
		return regions;
	}

	private static List<RegionGroup> BuildCoreGroups(
		IReadOnlyList<FragmentDetectedFeature> features,
		IReadOnlyList<Rect2> bounds,
		IReadOnlyList<int> strokeUnits,
		IReadOnlyList<bool> core)
	{
		List<RegionGroup> groups = new();
		bool[] visited = new bool[features.Count];
		for (int start = 0; start < features.Count; start++)
		{
			if (!core[start] || visited[start]) continue;
			RegionGroup group = new() { HasCore = true };
			Queue<int> pending = new();
			AddFeature(group, features[start], bounds[start], strokeUnits[start]);
			pending.Enqueue(start);
			visited[start] = true;
			while (pending.Count > 0)
			{
				int index = pending.Dequeue();
				for (int other = 0; other < features.Count; other++)
				{
					if (!core[other] || visited[other] ||
						FeatureGap(features[index], features[other]) > CoreNeighbourGap ||
						!CanMerge(group.Bounds, bounds[other])) continue;
					visited[other] = true;
					AddFeature(group, features[other], bounds[other], strokeUnits[other]);
					pending.Enqueue(other);
				}
			}
			groups.Add(group);
		}
		return groups;
	}

	private static void AttachSupportedPeriphery(
		IReadOnlyList<RegionGroup> groups,
		IReadOnlyList<FragmentDetectedFeature> features,
		IReadOnlyList<Rect2> bounds,
		IReadOnlyList<int> strokeUnits,
		IReadOnlyList<bool> core)
	{
		for (int index = 0; index < features.Count; index++)
		{
			if (core[index] || IsAssigned(groups, features[index].Id)) continue;
			RegionGroup best = null;
			float bestGap = float.MaxValue;
			foreach (RegionGroup group in groups)
			{
				int supportingCoreNeighbours = 0;
				float nearestGap = float.MaxValue;
				foreach (FragmentDetectedFeature member in group.Features)
				{
					int memberIndex = FindFeatureIndex(features, member.Id);
					if (memberIndex < 0 || !core[memberIndex]) continue;
					float gap = FeatureGap(features[index], features[memberIndex]);
					nearestGap = MathF.Min(nearestGap, gap);
					if (gap <= CoreNeighbourGap) supportingCoreNeighbours++;
				}
				bool supported = supportingCoreNeighbours >= 2 || strokeUnits[index] >= 2;
				if (!supported || nearestGap > PeripheralGap || nearestGap >= bestGap ||
					!CanMerge(group.Bounds, bounds[index])) continue;
				best = group;
				bestGap = nearestGap;
			}
			if (best != null) AddFeature(best, features[index], bounds[index], strokeUnits[index]);
		}
	}

	private static void BuildSparseGroups(
		List<RegionGroup> groups,
		IReadOnlyList<FragmentDetectedFeature> features,
		IReadOnlyList<Rect2> bounds,
		IReadOnlyList<int> strokeUnits,
		IReadOnlyList<bool> core)
	{
		List<int> remaining = new();
		for (int index = 0; index < features.Count; index++)
			if (!core[index] && !IsAssigned(groups, features[index].Id)) remaining.Add(index);
		remaining.Sort((first, second) =>
		{
			int strokes = strokeUnits[second].CompareTo(strokeUnits[first]);
			return strokes != 0 ? strokes : features[first].Id.CompareTo(features[second].Id);
		});
		foreach (int index in remaining)
		{
			RegionGroup best = null;
			float bestGap = float.MaxValue;
			foreach (RegionGroup group in groups)
			{
				if (group.HasCore) continue;
				float gap = GroupFeatureGap(group, features[index]);
				if (gap > SparseClusterGap || gap >= bestGap ||
					!CanMerge(group.Bounds, bounds[index])) continue;
				best = group;
				bestGap = gap;
			}
			if (best == null)
			{
				best = new RegionGroup();
				groups.Add(best);
			}
			AddFeature(best, features[index], bounds[index], strokeUnits[index]);
		}
	}

	private static void AddFeature(
		RegionGroup group,
		FragmentDetectedFeature feature,
		Rect2 bounds,
		int strokeUnits)
	{
		group.Bounds = group.Features.Count == 0 ? bounds : group.Bounds.Merge(bounds);
		group.Features.Add(feature);
		group.StrokeUnits += strokeUnits;
		group.StableOrder = Math.Min(group.StableOrder, feature.Id);
	}

	private static bool IsAssigned(IReadOnlyList<RegionGroup> groups, int featureId)
	{
		foreach (RegionGroup group in groups)
			if (group.Features.Exists(feature => feature.Id == featureId)) return true;
		return false;
	}

	private static int FindFeatureIndex(
		IReadOnlyList<FragmentDetectedFeature> features,
		int featureId)
	{
		for (int index = 0; index < features.Count; index++)
			if (features[index].Id == featureId) return index;
		return -1;
	}

	private static int CompareGroups(RegionGroup first, RegionGroup second)
	{
		int density = second.Density.CompareTo(first.Density);
		if (density != 0) return density;
		int strokes = second.StrokeUnits.CompareTo(first.StrokeUnits);
		return strokes != 0 ? strokes : first.StableOrder.CompareTo(second.StableOrder);
	}

	private static bool IsVeryHighDensity(RegionGroup group) =>
		group.StrokeUnits >= VeryHighDensityStrokeUnits &&
		group.Density >= VeryHighDensityThreshold;

	private static float CalculateDensity(RegionGroup group)
	{
		float area = MathF.Max(group.Bounds.Size.X * group.Bounds.Size.Y, 0.0025f);
		float rawDensity = group.StrokeUnits / area;
		float compactDensity = rawDensity / (rawDensity + 85f);
		float complexity = Mathf.Clamp((group.StrokeUnits - 1f) / 10f, 0f, 1f);
		float featureRichness = Mathf.Clamp(group.Features.Count / 5f, 0f, 1f);
		return Mathf.Clamp(
			compactDensity * 0.55f + complexity * 0.30f + featureRichness * 0.15f,
			0.05f,
			0.99f);
	}

	private static int GetStrokeUnits(FragmentDetectedFeature feature) =>
		Math.Max(feature.Segments?.Count ?? 0, 1);

	private static bool CanMerge(Rect2 first, Rect2 second)
	{
		Rect2 merged = first.Merge(second);
		return merged.Size.X <= MaximumMergedWidth &&
			merged.Size.Y <= MaximumMergedHeight &&
			merged.Size.X * merged.Size.Y <= MaximumMergedArea;
	}

	private static float GroupFeatureGap(RegionGroup group, FragmentDetectedFeature feature)
	{
		float nearest = float.MaxValue;
		foreach (FragmentDetectedFeature member in group.Features)
			nearest = MathF.Min(nearest, FeatureGap(member, feature));
		return nearest;
	}

	private static float FeatureGap(
		FragmentDetectedFeature first,
		FragmentDetectedFeature second)
	{
		float nearest = float.MaxValue;
		int firstCount = Math.Max(first.Segments?.Count ?? 0, 1);
		int secondCount = Math.Max(second.Segments?.Count ?? 0, 1);
		for (int firstIndex = 0; firstIndex < firstCount; firstIndex++)
		{
			GetSegment(first, firstIndex, out Vector2 firstStart, out Vector2 firstEnd);
			for (int secondIndex = 0; secondIndex < secondCount; secondIndex++)
			{
				GetSegment(second, secondIndex, out Vector2 secondStart, out Vector2 secondEnd);
				Variant intersection = Geometry2D.SegmentIntersectsSegment(
					firstStart,
					firstEnd,
					secondStart,
					secondEnd);
				if (intersection.VariantType != Variant.Type.Nil) return 0f;
				nearest = MathF.Min(nearest,
					firstStart.DistanceTo(Geometry2D.GetClosestPointToSegment(
						firstStart, secondStart, secondEnd)));
				nearest = MathF.Min(nearest,
					firstEnd.DistanceTo(Geometry2D.GetClosestPointToSegment(
						firstEnd, secondStart, secondEnd)));
				nearest = MathF.Min(nearest,
					secondStart.DistanceTo(Geometry2D.GetClosestPointToSegment(
						secondStart, firstStart, firstEnd)));
				nearest = MathF.Min(nearest,
					secondEnd.DistanceTo(Geometry2D.GetClosestPointToSegment(
						secondEnd, firstStart, firstEnd)));
			}
		}
		return nearest;
	}

	private static void GetSegment(
		FragmentDetectedFeature feature,
		int index,
		out Vector2 start,
		out Vector2 end)
	{
		if (feature.Segments == null || feature.Segments.Count == 0)
		{
			start = feature.Start;
			end = feature.End;
			return;
		}
		start = feature.Segments[index].Start;
		end = feature.Segments[index].End;
	}

	private static Rect2 GetFeatureBounds(FragmentDetectedFeature feature)
	{
		Vector2 minimum = feature.Start.Min(feature.End);
		Vector2 maximum = feature.Start.Max(feature.End);
		foreach (FragmentFeatureSegment segment in feature.Segments)
		{
			minimum = minimum.Min(segment.Start).Min(segment.End);
			maximum = maximum.Max(segment.Start).Max(segment.End);
		}
		return new Rect2(minimum, maximum - minimum);
	}

	private static Rect2 ClampNormalized(Rect2 bounds)
	{
		Vector2 start = bounds.Position.Clamp(Vector2.Zero, Vector2.One);
		Vector2 end = bounds.End.Clamp(Vector2.Zero, Vector2.One);
		return new Rect2(start, end - start);
	}
}
