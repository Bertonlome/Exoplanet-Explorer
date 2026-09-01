using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Deterministic, observation-driven coordinate search. It sees only control states and measured
/// S/N history; puzzle truth is deliberately outside this API. Untested branches are explored in a
/// reproducibly shuffled order so the search does not systematically favour the leftmost controls.
/// </summary>
public static class FragmentConfigurationSearch
{
	public static FragmentProcessingAdjustment PlanNextAdjustment(
		FragmentAnalysisControlState current,
		IReadOnlyList<FragmentProcessingHistoryEntry> history,
		IReadOnlyCollection<string> rejectedConfigurations,
		IReadOnlyCollection<FragmentAnalysisParameter> lockedParameters,
		int? targetRegionId,
		float meaningfulDifference = 0.02f)
	{
		if (current == null) return null;
		HashSet<string> tested = new();
		if (history != null)
			foreach (FragmentProcessingHistoryEntry entry in history)
				if (entry?.Configuration != null && entry.TargetRegionId == targetRegionId)
					tested.Add(GetConfigurationKey(entry.Configuration));
		HashSet<string> rejected = new();
		if (rejectedConfigurations != null)
		{
			string targetPrefix = $"R{targetRegionId}:";
			foreach (string rejectedKey in rejectedConfigurations)
			{
				if (rejectedKey == null) continue;
				if (rejectedKey.StartsWith(targetPrefix, StringComparison.Ordinal))
					rejected.Add(rejectedKey[targetPrefix.Length..]);
				else if (!rejectedKey.Contains(':'))
					rejected.Add(rejectedKey);
			}
		}
		int explorationOrdinal = tested.Count + rejected.Count;

		FragmentProcessingAdjustment currentNeighbour = FindUntestedNeighbour(
			current, tested, rejected, lockedParameters, targetRegionId, explorationOrdinal, false);

		FragmentProcessingHistoryEntry best = null;
		FragmentProcessingHistoryEntry currentMeasurement = null;
		if (history != null)
		{
			foreach (FragmentProcessingHistoryEntry entry in history)
			{
				if (entry?.Configuration != null && entry.Metrics != null &&
					entry.TargetRegionId == targetRegionId &&
					GetConfigurationKey(entry.Configuration) == GetConfigurationKey(current))
					currentMeasurement = entry;
				if (entry?.Configuration == null || entry.Metrics == null ||
					entry.TargetRegionId != targetRegionId ||
					FindUntestedNeighbour(entry.Configuration, tested, rejected, lockedParameters,
						targetRegionId, explorationOrdinal, false) == null ||
					(GetConfigurationKey(entry.Configuration) != GetConfigurationKey(current) &&
					 StepToward(current, entry.Configuration, lockedParameters) == null))
					continue;
				if (best == null || entry.Metrics.SignalToNoise > best.Metrics.SignalToNoise ||
					(Mathf.IsEqualApprox(entry.Metrics.SignalToNoise, best.Metrics.SignalToNoise) &&
					 entry.Sequence < best.Sequence))
					best = entry;
			}
		}

		if (best != null && currentMeasurement != null &&
			best.Metrics.SignalToNoise > currentMeasurement.Metrics.SignalToNoise +
				MathF.Max(meaningfulDifference, 0f))
		{
			FragmentProcessingAdjustment measuredBacktrack =
				StepToward(current, best.Configuration, lockedParameters);
			if (measuredBacktrack != null) return measuredBacktrack;
		}

		if (currentNeighbour != null) return currentNeighbour;
		if (best == null) return null;
		if (GetConfigurationKey(best.Configuration) == GetConfigurationKey(current))
			return FindUntestedNeighbour(current, tested, rejected, lockedParameters,
				targetRegionId, explorationOrdinal, false);

		return StepToward(current, best.Configuration, lockedParameters);
	}

	public static string GetConfigurationKey(FragmentAnalysisControlState state) => state == null
		? ""
		: $"P{Bit(state.PolarizationEnabled)}{state.PolarizationLevel}" +
		  $"S{Bit(state.SpectralEnabled)}{state.SpectralLevel}" +
		  $"T{Bit(state.SurfaceEnabled)}{state.SurfaceLevel}" +
		  $"E{Bit(state.ElectromagneticEnabled)}" +
		  $"R{Bit(state.ResonanceEnabled)}" +
		  $"X{Bit(state.XRayEnabled)}";

	private static int Bit(bool value) => value ? 1 : 0;

	private static FragmentProcessingAdjustment FindUntestedNeighbour(
		FragmentAnalysisControlState state,
		HashSet<string> tested,
		HashSet<string> rejected,
		IReadOnlyCollection<FragmentAnalysisParameter> locks,
		int? targetRegionId,
		int explorationOrdinal,
		bool isBacktrack)
	{
		List<FragmentProcessingAdjustment> candidates = new(BuildNeighbours(state, locks, isBacktrack));
		string currentKey = GetConfigurationKey(state);
		candidates.Sort((first, second) =>
		{
			uint firstPriority = GetExplorationPriority(
				currentKey, first.ConfigurationKey, targetRegionId, explorationOrdinal);
			uint secondPriority = GetExplorationPriority(
				currentKey, second.ConfigurationKey, targetRegionId, explorationOrdinal);
			int priorityComparison = firstPriority.CompareTo(secondPriority);
			return priorityComparison != 0
				? priorityComparison
				: string.CompareOrdinal(first.ConfigurationKey, second.ConfigurationKey);
		});

		foreach (FragmentProcessingAdjustment candidate in candidates)
			if (!tested.Contains(candidate.ConfigurationKey) && !rejected.Contains(candidate.ConfigurationKey))
				return candidate;
		return null;
	}

	/// <summary>
	/// Produces a stable pseudo-random priority. Using a stable hash instead of Godot's RNG makes an
	/// identical puzzle, target, and search history repeatable while still mixing parameter families.
	/// </summary>
	private static uint GetExplorationPriority(
		string currentKey,
		string candidateKey,
		int? targetRegionId,
		int explorationOrdinal)
	{
		unchecked
		{
			uint hash = 2166136261u;
			AddToStableHash(ref hash, currentKey);
			AddToStableHash(ref hash, candidateKey);
			hash = (hash ^ (uint)(targetRegionId ?? -1)) * 16777619u;
			hash = (hash ^ (uint)explorationOrdinal) * 16777619u;

			// Avalanche the FNV state so similar configuration keys do not remain adjacent.
			hash ^= hash >> 16;
			hash *= 0x7feb352du;
			hash ^= hash >> 15;
			hash *= 0x846ca68bu;
			hash ^= hash >> 16;
			return hash;
		}
	}

	private static void AddToStableHash(ref uint hash, string value)
	{
		unchecked
		{
			foreach (char character in value)
				hash = (hash ^ character) * 16777619u;
		}
	}

	private static IEnumerable<FragmentProcessingAdjustment> BuildNeighbours(
		FragmentAnalysisControlState state,
		IReadOnlyCollection<FragmentAnalysisParameter> locks,
		bool isBacktrack)
	{
		if (!IsLocked(locks, FragmentAnalysisParameter.PolarizationEnabled))
		{
			yield return Toggle(state, FragmentAnalysisParameter.PolarizationEnabled,
				state.PolarizationEnabled, "Polarization", isBacktrack);
			if (state.PolarizationEnabled)
				foreach (FragmentProcessingAdjustment item in LevelNeighbours(state,
					FragmentAnalysisParameter.PolarizationLevel, state.PolarizationLevel,
					"Polarization level", isBacktrack)) yield return item;
		}
		if (!IsLocked(locks, FragmentAnalysisParameter.SpectralEnabled))
		{
			yield return Toggle(state, FragmentAnalysisParameter.SpectralEnabled,
				state.SpectralEnabled, "Spectral", isBacktrack);
			if (state.SpectralEnabled)
				foreach (FragmentProcessingAdjustment item in LevelNeighbours(state,
					FragmentAnalysisParameter.SpectralLevel, state.SpectralLevel,
					"Spectral level", isBacktrack)) yield return item;
		}
		if (!IsLocked(locks, FragmentAnalysisParameter.SurfaceEnabled))
		{
			yield return Toggle(state, FragmentAnalysisParameter.SurfaceEnabled,
				state.SurfaceEnabled, "Surface topography", isBacktrack);
			if (state.SurfaceEnabled)
				foreach (FragmentProcessingAdjustment item in LevelNeighbours(state,
					FragmentAnalysisParameter.SurfaceLevel, state.SurfaceLevel,
					"Surface level", isBacktrack)) yield return item;
		}
		if (!IsLocked(locks, FragmentAnalysisParameter.ElectromagneticEnabled))
			yield return Toggle(state, FragmentAnalysisParameter.ElectromagneticEnabled,
				state.ElectromagneticEnabled, "Electromagnetic channel", isBacktrack);
		if (!IsLocked(locks, FragmentAnalysisParameter.ResonanceEnabled))
			yield return Toggle(state, FragmentAnalysisParameter.ResonanceEnabled,
				state.ResonanceEnabled, "Resonance channel", isBacktrack);
		if (!IsLocked(locks, FragmentAnalysisParameter.XRayEnabled))
			yield return Toggle(state, FragmentAnalysisParameter.XRayEnabled,
				state.XRayEnabled, "X-Ray channel", isBacktrack);
	}

	private static IEnumerable<FragmentProcessingAdjustment> LevelNeighbours(
		FragmentAnalysisControlState state,
		FragmentAnalysisParameter parameter,
		int value,
		string name,
		bool isBacktrack)
	{
		if (value < 5) yield return Level(state, parameter, value, value + 1, name, isBacktrack);
		if (value > 1) yield return Level(state, parameter, value, value - 1, name, isBacktrack);
	}

	private static FragmentProcessingAdjustment StepToward(
		FragmentAnalysisControlState current,
		FragmentAnalysisControlState target,
		IReadOnlyCollection<FragmentAnalysisParameter> locks)
	{
		if (!IsLocked(locks, FragmentAnalysisParameter.PolarizationEnabled))
		{
			if (current.PolarizationEnabled != target.PolarizationEnabled)
				return ToggleTo(current, FragmentAnalysisParameter.PolarizationEnabled,
					current.PolarizationEnabled, target.PolarizationEnabled, "Polarization", true);
			if (current.PolarizationLevel != target.PolarizationLevel)
				return Level(current, FragmentAnalysisParameter.PolarizationLevel,
					current.PolarizationLevel, Step(current.PolarizationLevel, target.PolarizationLevel),
					"Polarization level", true);
		}
		if (!IsLocked(locks, FragmentAnalysisParameter.SpectralEnabled))
		{
			if (current.SpectralEnabled != target.SpectralEnabled)
				return ToggleTo(current, FragmentAnalysisParameter.SpectralEnabled,
					current.SpectralEnabled, target.SpectralEnabled, "Spectral", true);
			if (current.SpectralLevel != target.SpectralLevel)
				return Level(current, FragmentAnalysisParameter.SpectralLevel,
					current.SpectralLevel, Step(current.SpectralLevel, target.SpectralLevel),
					"Spectral level", true);
		}
		if (!IsLocked(locks, FragmentAnalysisParameter.SurfaceEnabled))
		{
			if (current.SurfaceEnabled != target.SurfaceEnabled)
				return ToggleTo(current, FragmentAnalysisParameter.SurfaceEnabled,
					current.SurfaceEnabled, target.SurfaceEnabled, "Surface topography", true);
			if (current.SurfaceLevel != target.SurfaceLevel)
				return Level(current, FragmentAnalysisParameter.SurfaceLevel,
					current.SurfaceLevel, Step(current.SurfaceLevel, target.SurfaceLevel),
					"Surface level", true);
		}
		if (!IsLocked(locks, FragmentAnalysisParameter.ElectromagneticEnabled) &&
			current.ElectromagneticEnabled != target.ElectromagneticEnabled)
			return ToggleTo(current, FragmentAnalysisParameter.ElectromagneticEnabled,
				current.ElectromagneticEnabled, target.ElectromagneticEnabled,
				"Electromagnetic channel", true);
		if (!IsLocked(locks, FragmentAnalysisParameter.ResonanceEnabled) &&
			current.ResonanceEnabled != target.ResonanceEnabled)
			return ToggleTo(current, FragmentAnalysisParameter.ResonanceEnabled,
				current.ResonanceEnabled, target.ResonanceEnabled, "Resonance channel", true);
		if (!IsLocked(locks, FragmentAnalysisParameter.XRayEnabled) &&
			current.XRayEnabled != target.XRayEnabled)
			return ToggleTo(current, FragmentAnalysisParameter.XRayEnabled,
				current.XRayEnabled, target.XRayEnabled, "X-Ray channel", true);
		return null;
	}

	private static int Step(int current, int target) => current + Math.Sign(target - current);

	private static FragmentProcessingAdjustment Toggle(
		FragmentAnalysisControlState state,
		FragmentAnalysisParameter parameter,
		bool value,
		string name,
		bool backtrack) => ToggleTo(state, parameter, value, !value, name, backtrack);

	private static FragmentProcessingAdjustment ToggleTo(
		FragmentAnalysisControlState state,
		FragmentAnalysisParameter parameter,
		bool previous,
		bool proposed,
		string name,
		bool backtrack)
	{
		FragmentAnalysisControlState candidate = With(state, parameter, proposed, 0);
		return new FragmentProcessingAdjustment
		{
			Parameter = parameter,
			BoolValue = proposed,
			ParameterName = name,
			PreviousValue = previous ? "ON" : "OFF",
			ProposedValue = proposed ? "ON" : "OFF",
			Rationale = backtrack
				? "Backtrack toward the best measured branch"
				: "Test an unmeasured neighbouring configuration",
			ConfigurationKey = GetConfigurationKey(candidate),
			IsBacktrack = backtrack
		};
	}

	private static FragmentProcessingAdjustment Level(
		FragmentAnalysisControlState state,
		FragmentAnalysisParameter parameter,
		int previous,
		int proposed,
		string name,
		bool backtrack)
	{
		FragmentAnalysisControlState candidate = With(state, parameter, false, proposed);
		return new FragmentProcessingAdjustment
		{
			Parameter = parameter,
			IntValue = proposed,
			ParameterName = name,
			PreviousValue = previous.ToString(),
			ProposedValue = proposed.ToString(),
			Rationale = backtrack
				? "Backtrack toward the best measured branch"
				: "Test an unmeasured neighbouring configuration",
			ConfigurationKey = GetConfigurationKey(candidate),
			IsBacktrack = backtrack
		};
	}

	private static bool IsLocked(
		IReadOnlyCollection<FragmentAnalysisParameter> locks,
		FragmentAnalysisParameter parameter)
	{
		if (locks == null) return false;
		foreach (FragmentAnalysisParameter locked in locks)
			if (locked == parameter) return true;
		return false;
	}

	private static FragmentAnalysisControlState With(
		FragmentAnalysisControlState state,
		FragmentAnalysisParameter parameter,
		bool boolValue,
		int intValue) => new()
	{
		PolarizationEnabled = parameter == FragmentAnalysisParameter.PolarizationEnabled
			? boolValue : state.PolarizationEnabled,
		PolarizationLevel = parameter == FragmentAnalysisParameter.PolarizationLevel
			? intValue : state.PolarizationLevel,
		SpectralEnabled = parameter == FragmentAnalysisParameter.SpectralEnabled
			? boolValue : state.SpectralEnabled,
		SpectralLevel = parameter == FragmentAnalysisParameter.SpectralLevel
			? intValue : state.SpectralLevel,
		SurfaceEnabled = parameter == FragmentAnalysisParameter.SurfaceEnabled
			? boolValue : state.SurfaceEnabled,
		SurfaceLevel = parameter == FragmentAnalysisParameter.SurfaceLevel
			? intValue : state.SurfaceLevel,
		ElectromagneticEnabled = parameter == FragmentAnalysisParameter.ElectromagneticEnabled
			? boolValue : state.ElectromagneticEnabled,
		ResonanceEnabled = parameter == FragmentAnalysisParameter.ResonanceEnabled
			? boolValue : state.ResonanceEnabled,
		XRayEnabled = parameter == FragmentAnalysisParameter.XRayEnabled
			? boolValue : state.XRayEnabled,
		RotationDegrees = state.RotationDegrees,
		ViewZoom = state.ViewZoom,
		ViewPan = state.ViewPan
	};
}
