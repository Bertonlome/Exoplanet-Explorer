using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Geometry-only arrow detector. It receives observable feature segments and reconstructed
/// membership, never puzzle roles, glyph identity, monolith position, or hidden direction data.
/// </summary>
public static class FragmentArrowDetector
{
	private const int MaximumCandidates = 8;

	private readonly struct Segment
	{
		public readonly Vector2 Start;
		public readonly Vector2 End;
		public readonly int FeatureId;

		public Segment(Vector2 start, Vector2 end, int featureId)
		{
			Start = start;
			End = end;
			FeatureId = featureId;
		}
	}

	public static IReadOnlyList<FragmentArrowCandidate> DetectCandidates(
		IReadOnlyList<FragmentDetectedFeature> features,
		IReadOnlyList<FragmentDetectedStructure> structures,
		Vector2 sampleSize)
	{
		List<FragmentArrowCandidate> candidates = new();
		if (features == null || features.Count == 0) return candidates;
		Vector2 safeSize = new(MathF.Max(sampleSize.X, 1f), MathF.Max(sampleSize.Y, 1f));
		List<List<int>> groups = BuildGroups(features, structures);
		foreach (List<int> group in groups)
		{
			List<Segment> segments = CollectSegments(features, group, safeSize);
			DetectInGroup(segments, safeSize, candidates);
		}
		candidates.Sort((first, second) => second.Confidence.CompareTo(first.Confidence));
		if (candidates.Count > MaximumCandidates)
			candidates.RemoveRange(MaximumCandidates, candidates.Count - MaximumCandidates);
		for (int index = 0; index < candidates.Count; index++)
		{
			FragmentArrowCandidate source = candidates[index];
			candidates[index] = new FragmentArrowCandidate
			{
				Id = index + 1,
				Tail = source.Tail,
				Tip = source.Tip,
				Confidence = source.Confidence,
				Disposition = FragmentAnnotationDisposition.Proposed,
				FeatureIds = source.FeatureIds,
				Provenance = FragmentAnnotationProvenance.Rover,
				Evidence = source.Evidence
			};
		}
		return candidates;
	}

	private static List<List<int>> BuildGroups(
		IReadOnlyList<FragmentDetectedFeature> features,
		IReadOnlyList<FragmentDetectedStructure> structures)
	{
		List<List<int>> groups = new();
		if (structures != null)
			foreach (FragmentDetectedStructure structure in structures)
			{
				if (structure == null ||
					structure.Disposition == FragmentAnnotationDisposition.Dismissed ||
					structure.FeatureIds.Count == 0) continue;
				groups.Add(new List<int>(structure.FeatureIds));
			}
		foreach (FragmentDetectedFeature feature in features)
		{
			if (feature == null || feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				continue;
			groups.Add(new List<int> { feature.Id });
		}
		return groups;
	}

	private static List<Segment> CollectSegments(
		IReadOnlyList<FragmentDetectedFeature> features,
		IReadOnlyList<int> featureIds,
		Vector2 sampleSize)
	{
		List<Segment> segments = new();
		foreach (int featureId in featureIds)
		{
			FragmentDetectedFeature feature = FindFeature(features, featureId);
			if (feature == null || feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				continue;
			if (feature.Segments.Count == 0)
				Add(feature.Start, feature.End, feature.Id);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					Add(segment.Start, segment.End, feature.Id);
		}
		return segments;

		void Add(Vector2 start, Vector2 end, int featureId)
		{
			Vector2 pixelStart = start * sampleSize;
			Vector2 pixelEnd = end * sampleSize;
			if (pixelStart.DistanceTo(pixelEnd) >= 6f)
				segments.Add(new Segment(pixelStart, pixelEnd, featureId));
		}
	}

	private static void DetectInGroup(
		IReadOnlyList<Segment> segments,
		Vector2 sampleSize,
		List<FragmentArrowCandidate> output)
	{
		for (int shaftIndex = 0; shaftIndex < segments.Count; shaftIndex++)
		{
			Segment shaft = segments[shaftIndex];
			float shaftLength = shaft.Start.DistanceTo(shaft.End);
			if (shaftLength < 12f) continue;
			EvaluateTip(shaft.Start, shaft.End);
			EvaluateTip(shaft.End, shaft.Start);

			void EvaluateTip(Vector2 tip, Vector2 tail)
			{
				Vector2 shaftDirection = (tip - tail).Normalized();
				float connectionTolerance = Mathf.Clamp(shaftLength * 0.14f, 7f, 22f);
				List<(Segment segment, Vector2 outward, float length, float connection)> heads = new();
				for (int index = 0; index < segments.Count; index++)
				{
					if (index == shaftIndex) continue;
					Segment segment = segments[index];
					Vector2 near;
					Vector2 far;
					float startDistance = segment.Start.DistanceTo(tip);
					float endDistance = segment.End.DistanceTo(tip);
					if (startDistance <= endDistance)
					{
						near = segment.Start;
						far = segment.End;
					}
					else
					{
						near = segment.End;
						far = segment.Start;
					}
					float connection = near.DistanceTo(tip);
					float length = near.DistanceTo(far);
					if (connection > connectionTolerance ||
						length < shaftLength * 0.1f || length > shaftLength * 0.85f) continue;
					Vector2 outward = (far - near).Normalized();
					if (outward.Dot(-shaftDirection) < 0.2f) continue;
					heads.Add((segment, outward, length, connection));
				}
				for (int first = 0; first < heads.Count; first++)
					for (int second = first + 1; second < heads.Count; second++)
					{
						var a = heads[first];
						var b = heads[second];
						float crossA = shaftDirection.Cross(a.outward);
						float crossB = shaftDirection.Cross(b.outward);
						if (crossA * crossB >= -0.02f) continue;
						float lengthSymmetry = MathF.Min(a.length, b.length) /
							MathF.Max(a.length, b.length);
						float angleSymmetry = 1f - Mathf.Clamp(
							MathF.Abs(MathF.Abs(crossA) - MathF.Abs(crossB)), 0f, 1f);
						float convergence = 1f - Mathf.Clamp(
							(a.connection + b.connection) / (connectionTolerance * 2f), 0f, 1f);
						float dominance = Mathf.Clamp(
							shaftLength / MathF.Max(a.length + b.length, 1f), 0f, 1.5f) / 1.5f;
						float confidence = Mathf.Clamp(
							0.22f + lengthSymmetry * 0.25f + angleSymmetry * 0.22f +
							convergence * 0.2f + dominance * 0.11f,
							0f,
							0.98f);
						List<int> ids = new() { shaft.FeatureId };
						if (!ids.Contains(a.segment.FeatureId)) ids.Add(a.segment.FeatureId);
						if (!ids.Contains(b.segment.FeatureId)) ids.Add(b.segment.FeatureId);
						Vector2 normalizedTail = tail / sampleSize;
						Vector2 normalizedTip = tip / sampleSize;
						if (IsDuplicate(output, normalizedTail, normalizedTip)) continue;
						output.Add(new FragmentArrowCandidate
						{
							Tail = normalizedTail,
							Tip = normalizedTip,
							Confidence = confidence,
							FeatureIds = ids,
							Evidence = $"SHAFT {shaftLength:0}px · HEAD SYMMETRY {lengthSymmetry:0.00} · " +
								$"CONVERGENCE {convergence:0.00}"
						});
					}
			}
		}
	}

	private static bool IsDuplicate(
		IReadOnlyList<FragmentArrowCandidate> candidates,
		Vector2 tail,
		Vector2 tip)
	{
		foreach (FragmentArrowCandidate candidate in candidates)
			if (candidate.Tail.DistanceSquaredTo(tail) < 0.0004f &&
				candidate.Tip.DistanceSquaredTo(tip) < 0.0004f)
				return true;
		return false;
	}

	private static FragmentDetectedFeature FindFeature(
		IReadOnlyList<FragmentDetectedFeature> features,
		int id)
	{
		for (int index = 0; index < features.Count; index++)
			if (features[index]?.Id == id) return features[index];
		return null;
	}
}
