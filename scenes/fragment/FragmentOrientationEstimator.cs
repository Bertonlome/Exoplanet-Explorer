using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Geometry-only upright-axis estimator. It receives selected feature geometry and canvas scale,
/// never glyph identity, correct rotation, puzzle truth, or semantic line roles.
/// </summary>
public static class FragmentOrientationEstimator
{
	private const int BinCount = 18;
	private const float MinimumAlternativeSeparationDegrees = 12f;

	private readonly struct WeightedSegment
	{
		public readonly float AxisDegrees;
		public readonly float Weight;

		public WeightedSegment(float axisDegrees, float weight)
		{
			AxisDegrees = axisDegrees;
			Weight = weight;
		}
	}

	public static float CalculateCorrection(
		FragmentOrientationHypothesis acceptedHypothesis,
		float currentDisplayRotation,
		float sourceDisplayRotation)
	{
		if (acceptedHypothesis == null) return 0f;
		float currentAxis = acceptedHypothesis.AxisDegrees +
			(currentDisplayRotation - sourceDisplayRotation);
		return NormalizeSignedDegrees(-currentAxis);
	}

	public static float CalculateCorrection(
		FragmentOrientationHypothesis acceptedHypothesis,
		float currentDisplayRotation) =>
		CalculateCorrection(acceptedHypothesis, currentDisplayRotation, currentDisplayRotation);

	public static IReadOnlyList<FragmentOrientationHypothesis> EstimateHypotheses(
		FragmentDetectedStructure structure,
		IReadOnlyList<FragmentDetectedFeature> features,
		Vector2 sampleSize,
		float confidenceScale = 1f,
		int maximumHypotheses = 8)
	{
		List<FragmentOrientationHypothesis> results = new();
		if (structure == null || features == null || maximumHypotheses <= 0) return results;
		List<WeightedSegment> segments = CollectSegments(structure, features, sampleSize);
		if (segments.Count == 0) return results;

		float totalWeight = 0f;
		float[] bins = new float[BinCount];
		foreach (WeightedSegment segment in segments)
		{
			totalWeight += segment.Weight;
			int bin = Mathf.Clamp(
				Mathf.FloorToInt(segment.AxisDegrees / (180f / BinCount)), 0, BinCount - 1);
			bins[bin] += segment.Weight;
		}
		if (totalWeight <= 0.0001f) return results;

		List<float> axes = FindDistinctAxes(segments, bins);
		float reliability = Mathf.Clamp(confidenceScale, 0f, 1f);
		ulong signature = ComputeGeometrySignature(structure, features);
		float principal = CalculatePrincipalGeometryAxis(structure, features, sampleSize);
		AddAxisPair(principal, "PRINCIPAL GEOMETRY AXIS");
		foreach (float axis in axes)
		{
			if (results.Count >= maximumHypotheses) break;
			if (results.Exists(result =>
				AxisDistanceDegrees(result.AxisDegrees, axis) < 5f)) continue;
			AddAxisPair(axis, "OBSERVED LINE AXIS");
		}
		if (results.Count < maximumHypotheses)
			AddAxisPair(NormalizeSignedDegrees(principal + 90f),
				"PERPENDICULAR GEOMETRY AXIS");
		return results;

		void AddAxisPair(float axis, string evidence)
		{
			if (results.Count >= maximumHypotheses) return;
			float support = CalculateSupport(segments, axis, totalWeight);
			float confidence = ScaleConfidence(support, reliability);
			results.Add(CreateHypothesis(results.Count + 1, structure.Id, signature, axis,
				confidence, support, evidence, true));
			if (results.Count < maximumHypotheses)
				results.Add(CreateHypothesis(results.Count + 1, structure.Id, signature,
					NormalizeSignedDegrees(axis + 180f), confidence, support,
					$"REVERSED {evidence}", true));
		}
	}

	private static float CalculatePrincipalGeometryAxis(
		FragmentDetectedStructure structure,
		IReadOnlyList<FragmentDetectedFeature> features,
		Vector2 sampleSize)
	{
		List<Vector2> points = new();
		Vector2 scale = new(MathF.Max(sampleSize.X, 1f), MathF.Max(sampleSize.Y, 1f));
		foreach (int id in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindFeature(features, id);
			if (feature == null || feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				AddSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					AddSegment(segment.Start, segment.End);
		}
		if (points.Count < 2) return 0f;
		Vector2 mean = Vector2.Zero;
		foreach (Vector2 point in points) mean += point;
		mean /= points.Count;
		float xx = 0f;
		float yy = 0f;
		float xy = 0f;
		foreach (Vector2 point in points)
		{
			Vector2 delta = point - mean;
			xx += delta.X * delta.X;
			yy += delta.Y * delta.Y;
			xy += delta.X * delta.Y;
		}
		float radiansFromX = 0.5f * MathF.Atan2(2f * xy, xx - yy);
		Vector2 principal = new(MathF.Cos(radiansFromX), MathF.Sin(radiansFromX));
		return NormalizeSignedAxisDegrees(
			Mathf.RadToDeg(MathF.Atan2(principal.X, -principal.Y)));

		void AddSegment(Vector2 start, Vector2 end)
		{
			points.Add(start * scale);
			points.Add(end * scale);
		}
	}

	public static ulong ComputeGeometrySignature(
		FragmentDetectedStructure structure,
		IReadOnlyList<FragmentDetectedFeature> features)
	{
		if (structure == null || features == null) return 0;
		ulong hash = 1469598103934665603UL;
		Mix(structure.Id);
		List<int> ids = new(structure.FeatureIds);
		ids.Sort();
		foreach (int id in ids)
		{
			Mix(id);
			FragmentDetectedFeature feature = FindFeature(features, id);
			if (feature == null) continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				MixSegment(feature.Start, feature.End);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					MixSegment(segment.Start, segment.End);
		}
		return hash;

		void Mix(int value)
		{
			hash ^= unchecked((uint)value);
			hash *= 1099511628211UL;
		}

		void MixPoint(Vector2 point)
		{
			Mix(Mathf.RoundToInt(point.X * 512f));
			Mix(Mathf.RoundToInt(point.Y * 512f));
		}

		void MixSegment(Vector2 start, Vector2 end)
		{
			MixPoint(start);
			MixPoint(end);
		}
	}

	private static List<WeightedSegment> CollectSegments(
		FragmentDetectedStructure structure,
		IReadOnlyList<FragmentDetectedFeature> features,
		Vector2 sampleSize)
	{
		List<WeightedSegment> segments = new();
		Vector2 scale = new(MathF.Max(sampleSize.X, 1f), MathF.Max(sampleSize.Y, 1f));
		foreach (int id in structure.FeatureIds)
		{
			FragmentDetectedFeature feature = FindFeature(features, id);
			if (feature == null || feature.Disposition == FragmentAnnotationDisposition.Dismissed)
				continue;
			if (feature.Segments == null || feature.Segments.Count == 0)
				Add(feature.Start, feature.End, feature.Confidence);
			else
				foreach (FragmentFeatureSegment segment in feature.Segments)
					Add(segment.Start, segment.End, feature.Confidence);
		}
		return segments;

		void Add(Vector2 start, Vector2 end, float confidence)
		{
			Vector2 delta = (end - start) * scale;
			float length = delta.Length();
			if (length <= 0.5f) return;
			float angle = NormalizeAxisDegrees(Mathf.RadToDeg(MathF.Atan2(delta.X, -delta.Y)));
			segments.Add(new WeightedSegment(angle,
				length * Mathf.Lerp(0.25f, 1f, Mathf.Clamp(confidence, 0f, 1f))));
		}
	}

	private static List<float> FindDistinctAxes(
		IReadOnlyList<WeightedSegment> segments,
		IReadOnlyList<float> bins)
	{
		List<int> order = new();
		for (int index = 0; index < bins.Count; index++) order.Add(index);
		order.Sort((first, second) =>
		{
			int weight = bins[second].CompareTo(bins[first]);
			return weight != 0 ? weight : first.CompareTo(second);
		});
		List<float> axes = new();
		foreach (int bin in order)
		{
			if (bins[bin] <= 0f) continue;
			float seed = (bin + 0.5f) * (180f / BinCount);
			float refined = RefineAxis(segments, seed);
			bool distinct = true;
			foreach (float existing in axes)
				if (AxisDistanceDegrees(existing, refined) < MinimumAlternativeSeparationDegrees)
					distinct = false;
			if (!distinct) continue;
			axes.Add(NormalizeSignedAxisDegrees(refined));
				if (axes.Count == 4) break;
		}
		if (axes.Count == 0) axes.Add(0f);
		return axes;
	}

	private static float RefineAxis(IReadOnlyList<WeightedSegment> segments, float seed)
	{
		float sine = 0f;
		float cosine = 0f;
		foreach (WeightedSegment segment in segments)
		{
			if (AxisDistanceDegrees(segment.AxisDegrees, seed) > 30f) continue;
			float doubled = Mathf.DegToRad(segment.AxisDegrees * 2f);
			sine += MathF.Sin(doubled) * segment.Weight;
			cosine += MathF.Cos(doubled) * segment.Weight;
		}
		if (MathF.Abs(sine) + MathF.Abs(cosine) <= 0.0001f) return seed;
		return NormalizeAxisDegrees(Mathf.RadToDeg(0.5f * MathF.Atan2(sine, cosine)));
	}

	private static float CalculateSupport(
		IReadOnlyList<WeightedSegment> segments,
		float axis,
		float totalWeight)
	{
		float aligned = 0f;
		foreach (WeightedSegment segment in segments)
		{
			float distance = AxisDistanceDegrees(segment.AxisDegrees, axis);
			float alignment = MathF.Max(0f, MathF.Cos(Mathf.DegToRad(distance * 2f)));
			aligned += segment.Weight * alignment;
		}
		return Mathf.Clamp(aligned / MathF.Max(totalWeight, 0.0001f), 0f, 1f);
	}

	private static FragmentOrientationHypothesis CreateHypothesis(
		int id,
		int structureId,
		ulong signature,
		float axis,
		float confidence,
		float support,
		string evidence,
		bool ambiguous) => new()
	{
		Id = id,
		SourceStructureId = structureId,
		GeometrySignature = signature,
		AxisDegrees = NormalizeSignedDegrees(axis),
		Confidence = Mathf.Clamp(confidence, 0f, 1f),
		Disposition = FragmentAnnotationDisposition.Proposed,
		IsPolarityAmbiguous = ambiguous,
		Evidence = $"{evidence} · {support:P0} WEIGHTED SUPPORT" +
			(ambiguous ? " · POLARITY AMBIGUOUS" : string.Empty)
	};

	private static float ScaleConfidence(float support, float reliability) =>
		Mathf.Clamp((0.25f + support * 0.75f) * (0.5f + reliability * 0.5f), 0f, 1f);

	private static FragmentDetectedFeature FindFeature(
		IReadOnlyList<FragmentDetectedFeature> features,
		int id)
	{
		for (int index = 0; index < features.Count; index++)
			if (features[index]?.Id == id) return features[index];
		return null;
	}

	private static float AxisDistanceDegrees(float first, float second)
	{
		float difference = MathF.Abs(NormalizeAxisDegrees(first) - NormalizeAxisDegrees(second));
		return MathF.Min(difference, 180f - difference);
	}

	private static float NormalizeAxisDegrees(float degrees)
	{
		degrees %= 180f;
		if (degrees < 0f) degrees += 180f;
		return degrees;
	}

	private static float NormalizeSignedAxisDegrees(float degrees)
	{
		degrees = NormalizeAxisDegrees(degrees);
		return degrees >= 90f ? degrees - 180f : degrees;
	}

	private static float NormalizeSignedDegrees(float degrees)
	{
		degrees = (degrees + 180f) % 360f;
		if (degrees < 0f) degrees += 360f;
		return degrees - 180f;
	}
}
