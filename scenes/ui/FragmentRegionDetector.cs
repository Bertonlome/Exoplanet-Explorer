using System;
using System.Collections.Generic;
using Godot;

public static class FragmentRegionDetector
{
	private const float FeaturePadding = 0.018f;
	private const float ClusterGap = 0.035f;
	private const float MaximumMergedWidth = 0.42f;
	private const float MaximumMergedHeight = 0.52f;
	private const float MaximumMergedArea = 0.16f;
	private const int MaximumRegionCount = 6;

	private sealed class RegionGroup
	{
		public Rect2 Bounds;
		public List<FragmentDetectedFeature> Features { get; } = new();
	}

	public static IReadOnlyList<FragmentCandidateRegion> GroupCandidateRegions(
		IReadOnlyList<FragmentDetectedFeature> features)
	{
		if (features == null) return Array.Empty<FragmentCandidateRegion>();
		List<RegionGroup> groups = new();
		foreach (FragmentDetectedFeature feature in features)
		{
			if (feature == null || feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				continue;
			Rect2 bounds = GetFeatureBounds(feature).Grow(FeaturePadding);
			List<RegionGroup> touching = groups.FindAll(group =>
				group.Bounds.Grow(ClusterGap).Intersects(bounds, true) &&
				CanMerge(group.Bounds, bounds));
			if (touching.Count == 0)
			{
				RegionGroup group = new() { Bounds = bounds };
				group.Features.Add(feature);
				groups.Add(group);
				continue;
			}

			RegionGroup target = touching[0];
			target.Bounds = target.Bounds.Merge(bounds);
			target.Features.Add(feature);
			for (int index = 1; index < touching.Count; index++)
			{
				if (!CanMerge(target.Bounds, touching[index].Bounds)) continue;
				target.Bounds = target.Bounds.Merge(touching[index].Bounds);
				target.Features.AddRange(touching[index].Features);
				groups.Remove(touching[index]);
			}
		}

		groups.Sort((first, second) => Score(second).CompareTo(Score(first)));
		if (groups.Count > MaximumRegionCount)
			groups.RemoveRange(MaximumRegionCount, groups.Count - MaximumRegionCount);

		List<FragmentCandidateRegion> regions = new(groups.Count);
		for (int index = 0; index < groups.Count; index++)
		{
			RegionGroup group = groups[index];
			Rect2 clamped = ClampNormalized(group.Bounds);
			regions.Add(new FragmentCandidateRegion
			{
				Id = index + 1,
				NormalizedBounds = clamped,
				Confidence = Score(group),
				Provenance = FragmentAnnotationProvenance.Rover,
				Disposition = FragmentAnnotationDisposition.Proposed,
				FeatureIds = group.Features.ConvertAll(feature => feature.Id)
			});
		}
		return regions;
	}

	private static bool CanMerge(Rect2 first, Rect2 second)
	{
		Rect2 merged = first.Merge(second);
		return merged.Size.X <= MaximumMergedWidth &&
			merged.Size.Y <= MaximumMergedHeight &&
			merged.Size.X * merged.Size.Y <= MaximumMergedArea;
	}

	private static float Score(RegionGroup group)
	{
		float confidence = 0f;
		foreach (FragmentDetectedFeature feature in group.Features)
			confidence += feature.Confidence;
		confidence /= Math.Max(group.Features.Count, 1);
		float density = Mathf.Clamp(group.Features.Count / 4f, 0f, 1f);
		float boundedness = 1f - Mathf.Clamp(group.Bounds.Size.X * group.Bounds.Size.Y, 0f, 1f);
		return Mathf.Clamp(confidence * 0.55f + density * 0.3f + boundedness * 0.15f, 0.15f, 0.98f);
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
