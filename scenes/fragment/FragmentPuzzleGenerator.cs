using System;
using System.Collections.Generic;
using Godot;

public static class FragmentPuzzleGenerator
{
    private const int GlyphBaseSegmentCount = 20;
    private const int DistractorGlyphSegmentCount = 14;

    private readonly struct SignalSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly bool IsImportant;
        public readonly float WidthMultiplier;

        public SignalSegment(
            Vector2 start,
            Vector2 end,
            bool isImportant = false,
            float widthMultiplier = 1f)
        {
            Start = start;
            End = end;
            IsImportant = isImportant;
            WidthMultiplier = widthMultiplier;
        }
    }

    private readonly struct DistractorSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly Vector2 Center;
        public readonly FragmentDistractorGlyphType GlyphType;
        public readonly float WidthMultiplier;

        public DistractorSegment(
            Vector2 start,
            Vector2 end,
            Vector2 center,
            FragmentDistractorGlyphType glyphType,
            float widthMultiplier = 1f)
        {
            Start = start;
            End = end;
            Center = center;
            GlyphType = glyphType;
            WidthMultiplier = widthMultiplier;
        }
    }

    public static FragmentPuzzle Generate(
        FragmentGenerationSettings generationSettings,
        FragmentRockSettings rockSettings,
        Vector2 canvasSize,
        ulong seed,
        Vector2I fragmentPosition,
        Vector2I monolithPosition,
        FragmentGlyphType glyphType)
    {
        Vector2 referenceSize = new(
            MathF.Max(canvasSize.X, 1f),
            MathF.Max(canvasSize.Y, 1f));

        ResolveCorrectChannelCombination(
            generationSettings,
            seed,
            out bool correctElectromagnetic,
            out bool correctResonance,
            out bool correctXRay);
        ResolveCorrectProcessingCombination(
            generationSettings,
            seed,
            out bool correctPolarization,
            out int correctPolarizationLevel,
            out bool correctSpectral,
            out int correctSpectralLevel,
            out bool correctSurface,
            out int correctSurfaceLevel);

        Vector2 monolithDirection = new Vector2(
            monolithPosition.X - fragmentPosition.X,
            monolithPosition.Y - fragmentPosition.Y).Normalized();
        ResolveFigureTransform(
            generationSettings,
            referenceSize,
            seed,
            out Vector2 figureCenter,
            out float initialRotationDegrees);

        FragmentPuzzle puzzle = new()
        {
            Seed = seed,
            ReferenceSize = referenceSize,
            FragmentPosition = fragmentPosition,
            MonolithPosition = monolithPosition,
            MonolithDirection = monolithDirection,
            GlyphType = glyphType,
            FigureCenter = figureCenter,
            InitialRotationDegrees = initialRotationDegrees,
            CorrectRotationDegrees = generationSettings.CorrectRotationDegrees,
            CorrectPolarizationEnabled = correctPolarization,
            CorrectPolarizationLevel = correctPolarizationLevel,
            CorrectSpectralEnabled = correctSpectral,
            CorrectSpectralLevel = correctSpectralLevel,
            CorrectSurfaceEnabled = correctSurface,
            CorrectSurfaceLevel = correctSurfaceLevel,
            CorrectElectromagneticEnabled = correctElectromagnetic,
            CorrectResonanceEnabled = correctResonance,
            CorrectXRayEnabled = correctXRay
        };

        List<FragmentDistractorGlyphType> distractorGlyphTypes = ResolveDistractorGlyphTypes(
            glyphType,
            seed);
        GenerateDistractorFilterKeys(puzzle, generationSettings, seed, distractorGlyphTypes);
        RandomNumberGenerator rng = new() { Seed = seed };
        GenerateLines(puzzle, generationSettings, rng);
        GenerateVeins(
            puzzle,
            rockSettings,
            MathF.Max(generationSettings.CanvasSizeMultiplier, 1f),
            seed);
        return puzzle;
    }

    private static void GenerateLines(
        FragmentPuzzle puzzle,
        FragmentGenerationSettings settings,
        RandomNumberGenerator rng)
    {
        int totalLineCount = Mathf.Max(
            settings.LineCount,
            GlyphBaseSegmentCount + DistractorGlyphSegmentCount);
        int signalLineCount = Mathf.Clamp(
            Mathf.RoundToInt(totalLineCount * Mathf.Clamp(settings.SignalFraction, 0f, 1f)),
            GlyphBaseSegmentCount,
            totalLineCount - DistractorGlyphSegmentCount);

        List<SignalSegment> signalGeometry = GenerateSignalGeometry(
            signalLineCount,
            puzzle.ReferenceSize,
            puzzle.FigureCenter,
            puzzle.MonolithDirection,
            puzzle.GlyphType,
            settings);
        foreach (SignalSegment segment in signalGeometry)
        {
            bool important = segment.IsImportant || rng.Randf() < settings.ImportantSignalFraction;
            FragmentLine line = CreateLine(segment.Start, segment.End, FragmentLineRole.Signal, settings, rng);
            line.Width *= segment.WidthMultiplier;
            line.IsImportant = important;
            line.RequiresCorrectCombination = important || rng.Randf() < settings.SolutionLockedSignalFraction;
            float minimumThreshold = important
                ? settings.ImportantSignalRevealThresholdMinimum
                : settings.SignalRevealThresholdMinimum;
            float maximumThreshold = important
                ? settings.ImportantSignalRevealThresholdMaximum
                : settings.SignalRevealThresholdMaximum;
            line.RevealThreshold = rng.RandfRange(
                Mathf.Min(minimumThreshold, maximumThreshold),
                Mathf.Max(minimumThreshold, maximumThreshold));
            puzzle.Lines.Add(line);

            if (important)
                puzzle.ImportantPoints.Add((segment.Start + segment.End) * 0.5f);
        }

        int distractorCount = totalLineCount - signalLineCount;
        List<DistractorSegment> distractorGlyphGeometry = GenerateDistractorGlyphGeometry(
            puzzle,
            settings,
            rng);
        int glyphSegmentCount = distractorGlyphGeometry.Count;
        for (int i = 0; i < glyphSegmentCount; i++)
        {
            DistractorSegment segment = distractorGlyphGeometry[i];
            FragmentLine line = CreateLine(
                segment.Start,
                segment.End,
                FragmentLineRole.Distractor,
                settings,
                rng);
            line.Width *= segment.WidthMultiplier;
            line.DistractorGlyphType = segment.GlyphType;
            line.HasCustomRotationCenter = true;
            line.RotationCenter = segment.Center;
            line.Color = settings.LineColor;
            line.IsImportant = rng.Randf() < settings.ImportantSignalFraction;
            line.RequiresCorrectCombination = true;
            line.RevealedInCorrectCombination = rng.Randf() < settings.CorrectCombinationDistractorFraction;
            line.RevealThreshold = rng.RandfRange(0.1f, 0.75f);
            puzzle.Lines.Add(line);
        }

        Vector2 minimumStart = new(
            Mathf.Min(settings.StartMinimumX, settings.StartMaximumX) * puzzle.ReferenceSize.X,
            Mathf.Min(settings.StartMinimumY, settings.StartMaximumY) * puzzle.ReferenceSize.Y);
        Vector2 maximumStart = new(
            Mathf.Max(settings.StartMinimumX, settings.StartMaximumX) * puzzle.ReferenceSize.X,
            Mathf.Max(settings.StartMinimumY, settings.StartMaximumY) * puzzle.ReferenceSize.Y);

        float minimumOffsetX = Mathf.Min(settings.MinimumOffset.X, settings.MaximumOffset.X);
        float maximumOffsetX = Mathf.Max(settings.MinimumOffset.X, settings.MaximumOffset.X);
        float minimumOffsetY = Mathf.Min(settings.MinimumOffset.Y, settings.MaximumOffset.Y);
        float maximumOffsetY = Mathf.Max(settings.MinimumOffset.Y, settings.MaximumOffset.Y);

        for (int i = DistractorGlyphSegmentCount; i < distractorCount; i++)
        {
            Vector2 start = new(
                rng.RandfRange(minimumStart.X, maximumStart.X),
                rng.RandfRange(minimumStart.Y, maximumStart.Y));
            Vector2 end = start + new Vector2(
                rng.RandfRange(minimumOffsetX, maximumOffsetX),
                rng.RandfRange(minimumOffsetY, maximumOffsetY));

            FragmentLine line = CreateLine(start, end, FragmentLineRole.Distractor, settings, rng);
            line.RevealedInCorrectCombination = rng.Randf() < settings.CorrectCombinationDistractorFraction;
            line.RevealThreshold = rng.RandfRange(0.1f, 0.75f);
            puzzle.Lines.Add(line);
        }
    }

    private static void GenerateDistractorFilterKeys(
        FragmentPuzzle puzzle,
        FragmentGenerationSettings settings,
        ulong seed,
        List<FragmentDistractorGlyphType> glyphTypes)
    {
        HashSet<int> usedSignatures = new()
        {
            GetFilterKeySignature(
                puzzle.CorrectPolarizationEnabled,
                puzzle.CorrectPolarizationLevel,
                puzzle.CorrectSpectralEnabled,
                puzzle.CorrectSpectralLevel,
                puzzle.CorrectSurfaceEnabled,
                puzzle.CorrectSurfaceLevel,
                puzzle.CorrectElectromagneticEnabled,
                puzzle.CorrectResonanceEnabled,
                puzzle.CorrectXRayEnabled)
        };

        for (int glyphIndex = 0; glyphIndex < glyphTypes.Count; glyphIndex++)
        {
            FragmentDistractorGlyph filterKey;
            int signature;
            int attempt = 0;
            do
            {
                ulong keySeed = seed ^
                    (0xA24BAED4963EE407UL * (ulong)(glyphIndex + 1)) ^
                    (0x9E3779B97F4A7C15UL * (ulong)(attempt + 1));
                filterKey = CreateRandomDistractorFilterKey(glyphTypes[glyphIndex], settings, keySeed);
                signature = GetFilterKeySignature(filterKey);
                attempt++;
            }
            while (usedSignatures.Contains(signature) && attempt < 64);

            usedSignatures.Add(signature);
            puzzle.DistractorGlyphs.Add(filterKey);
        }
    }

    private static List<FragmentDistractorGlyphType> ResolveDistractorGlyphTypes(
        FragmentGlyphType trueGlyphType,
        ulong seed)
    {
        List<FragmentDistractorGlyphType> glyphTypes = new()
        {
            FragmentDistractorGlyphType.Trident,
            FragmentDistractorGlyphType.DiamondEye,
            FragmentDistractorGlyphType.AngularSpiral
        };
        List<FragmentGlyphType> availableRealDecoys = new()
        {
            FragmentGlyphType.Hominid,
            FragmentGlyphType.Key,
            FragmentGlyphType.Television
        };
        availableRealDecoys.Remove(trueGlyphType);

        RandomNumberGenerator rng = new() { Seed = seed ^ 0xD6E8FEB86659FD93UL };
        int realDecoyCount = rng.RandiRange(0, 2);
        if (rng.RandiRange(0, 1) == 1)
        {
            (availableRealDecoys[0], availableRealDecoys[1]) =
                (availableRealDecoys[1], availableRealDecoys[0]);
        }

        for (int i = 0; i < realDecoyCount; i++)
            glyphTypes.Add(ToDistractorGlyphType(availableRealDecoys[i]));

        return glyphTypes;
    }

    private static FragmentDistractorGlyphType ToDistractorGlyphType(FragmentGlyphType glyphType)
    {
        return glyphType switch
        {
            FragmentGlyphType.Key => FragmentDistractorGlyphType.KeyDecoy,
            FragmentGlyphType.Television => FragmentDistractorGlyphType.TelevisionDecoy,
            _ => FragmentDistractorGlyphType.HominidDecoy
        };
    }

    private static FragmentGlyphType ToRealGlyphType(FragmentDistractorGlyphType glyphType)
    {
        return glyphType switch
        {
            FragmentDistractorGlyphType.KeyDecoy => FragmentGlyphType.Key,
            FragmentDistractorGlyphType.TelevisionDecoy => FragmentGlyphType.Television,
            _ => FragmentGlyphType.Hominid
        };
    }

    private static FragmentDistractorGlyph CreateRandomDistractorFilterKey(
        FragmentDistractorGlyphType glyphType,
        FragmentGenerationSettings settings,
        ulong seed)
    {
        RandomNumberGenerator rng = new() { Seed = seed };
        float enabledProbability = Mathf.Clamp(settings.ProcessingEnabledProbability, 0f, 1f);
        bool polarization = rng.Randf() < enabledProbability;
        bool spectral = rng.Randf() < enabledProbability;
        bool surface = rng.Randf() < enabledProbability;
        if (!settings.AllowNoProcessingSolution && !polarization && !spectral && !surface)
        {
            int requiredProcessor = rng.RandiRange(0, 2);
            polarization = requiredProcessor == 0;
            spectral = requiredProcessor == 1;
            surface = requiredProcessor == 2;
        }

        int channelMask;
        do
        {
            channelMask = rng.RandiRange(0, 7);
        }
        while ((!settings.AllowNoChannelsSolution && channelMask == 0) ||
            (!settings.AllowAllChannelsSolution && channelMask == 7));

        return new FragmentDistractorGlyph
        {
            GlyphType = glyphType,
            CorrectPolarizationEnabled = polarization,
            CorrectPolarizationLevel = rng.RandiRange(1, 5),
            CorrectSpectralEnabled = spectral,
            CorrectSpectralLevel = rng.RandiRange(1, 5),
            CorrectSurfaceEnabled = surface,
            CorrectSurfaceLevel = rng.RandiRange(1, 5),
            CorrectElectromagneticEnabled = (channelMask & 1) != 0,
            CorrectResonanceEnabled = (channelMask & 2) != 0,
            CorrectXRayEnabled = (channelMask & 4) != 0
        };
    }

    private static int GetFilterKeySignature(FragmentDistractorGlyph filterKey)
    {
        return GetFilterKeySignature(
            filterKey.CorrectPolarizationEnabled,
            filterKey.CorrectPolarizationLevel,
            filterKey.CorrectSpectralEnabled,
            filterKey.CorrectSpectralLevel,
            filterKey.CorrectSurfaceEnabled,
            filterKey.CorrectSurfaceLevel,
            filterKey.CorrectElectromagneticEnabled,
            filterKey.CorrectResonanceEnabled,
            filterKey.CorrectXRayEnabled);
    }

    private static int GetFilterKeySignature(
        bool polarization,
        int polarizationLevel,
        bool spectral,
        int spectralLevel,
        bool surface,
        int surfaceLevel,
        bool electromagnetic,
        bool resonance,
        bool xRay)
    {
        int signature = polarization ? Mathf.Clamp(polarizationLevel, 1, 5) : 0;
        signature = signature * 6 + (spectral ? Mathf.Clamp(spectralLevel, 1, 5) : 0);
        signature = signature * 6 + (surface ? Mathf.Clamp(surfaceLevel, 1, 5) : 0);
        signature = signature * 2 + (electromagnetic ? 1 : 0);
        signature = signature * 2 + (resonance ? 1 : 0);
        signature = signature * 2 + (xRay ? 1 : 0);
        return signature;
    }

    private static List<DistractorSegment> GenerateDistractorGlyphGeometry(
        FragmentPuzzle puzzle,
        FragmentGenerationSettings settings,
        RandomNumberGenerator rng)
    {
        List<DistractorSegment> segments = new(DistractorGlyphSegmentCount);
        List<Vector2> occupiedCenters = new();
        Vector2 size = puzzle.ReferenceSize;
        Vector2 trueGlyphCenter = puzzle.FigureCenter;
        float canvasScale = MathF.Max(settings.CanvasSizeMultiplier, 1f);
        float trueGlyphRadius = Mathf.Max(
            Mathf.Min(size.X, size.Y) / canvasScale * settings.PatternRadius,
            1f);
        float distractorRadius = trueGlyphRadius * Mathf.Clamp(
            settings.DistractorGlyphScale,
            0.2f,
            1f);

        AddDistractorGlyph(
            segments,
            CreateTridentGlyph(),
            FragmentDistractorGlyphType.Trident,
            FindDistractorCenter(size, trueGlyphCenter, trueGlyphRadius, distractorRadius, occupiedCenters, rng),
            distractorRadius * rng.RandfRange(0.85f, 1.15f),
            rng.RandfRange(-Mathf.Pi, Mathf.Pi));
        AddDistractorGlyph(
            segments,
            CreateDiamondEyeGlyph(),
            FragmentDistractorGlyphType.DiamondEye,
            FindDistractorCenter(size, trueGlyphCenter, trueGlyphRadius, distractorRadius, occupiedCenters, rng),
            distractorRadius * rng.RandfRange(0.85f, 1.15f),
            rng.RandfRange(-Mathf.Pi, Mathf.Pi));
        AddDistractorGlyph(
            segments,
            CreateAngularSpiralGlyph(),
            FragmentDistractorGlyphType.AngularSpiral,
            FindDistractorCenter(size, trueGlyphCenter, trueGlyphRadius, distractorRadius, occupiedCenters, rng),
            distractorRadius * rng.RandfRange(0.85f, 1.15f),
            rng.RandfRange(-Mathf.Pi, Mathf.Pi));

        foreach (FragmentDistractorGlyph distractorGlyph in puzzle.DistractorGlyphs)
        {
            if (!IsRealDecoyGlyph(distractorGlyph.GlyphType)) continue;
            AddRealDecoyGlyph(
                segments,
                distractorGlyph.GlyphType,
                ToRealGlyphType(distractorGlyph.GlyphType),
                size,
                trueGlyphCenter,
                trueGlyphRadius,
                occupiedCenters,
                puzzle.MonolithDirection,
                settings,
                rng);
        }

        return segments;
    }

    private static bool IsRealDecoyGlyph(FragmentDistractorGlyphType glyphType)
    {
        return glyphType is FragmentDistractorGlyphType.HominidDecoy or
            FragmentDistractorGlyphType.KeyDecoy or
            FragmentDistractorGlyphType.TelevisionDecoy;
    }

    private static void AddRealDecoyGlyph(
        List<DistractorSegment> destination,
        FragmentDistractorGlyphType distractorGlyphType,
        FragmentGlyphType realGlyphType,
        Vector2 size,
        Vector2 trueGlyphCenter,
        float trueGlyphRadius,
        List<Vector2> occupiedCenters,
        Vector2 monolithDirection,
        FragmentGenerationSettings settings,
        RandomNumberGenerator rng)
    {
        float radius = trueGlyphRadius * Mathf.Clamp(settings.RealDecoyGlyphScale, 0.5f, 1.2f);
        float placementExtent = radius * (
            1f +
            MathF.Max(settings.DirectionArrowLengthMultiplier, 0.1f) +
            MathF.Max(settings.DirectionArrowHeadMultiplier, 0.05f));
        Vector2 center = FindDistractorCenter(
            size,
            trueGlyphCenter,
            trueGlyphRadius,
            placementExtent,
            occupiedCenters,
            rng);

        List<SignalSegment> geometry = new();
        Vector2[] hexagon = CreatePointyHexagon(center, radius);
        AddClosedPolygon(geometry, hexagon);
        switch (realGlyphType)
        {
            case FragmentGlyphType.Key:
                AddKeyGlyph(geometry, center, radius);
                break;
            case FragmentGlyphType.Television:
                AddTelevisionGlyph(geometry, center, radius);
                break;
            default:
                AddHominidGlyph(geometry, center, radius, settings);
                break;
        }

        Vector2 referenceDirection = monolithDirection.IsZeroApprox()
            ? Vector2.Up
            : monolithDirection.Normalized();
        float falseDirectionOffset = rng.RandfRange(Mathf.Pi / 3f, Mathf.Pi * 5f / 3f);
        AddDirectionArrow(
            geometry,
            center,
            radius,
            referenceDirection.Rotated(falseDirectionOffset),
            hexagon,
            settings);

        float localRotation = rng.RandfRange(-Mathf.Pi, Mathf.Pi);
        foreach (SignalSegment segment in geometry)
        {
            Vector2 start = center + (segment.Start - center).Rotated(localRotation);
            Vector2 end = center + (segment.End - center).Rotated(localRotation);
            destination.Add(new DistractorSegment(
                start,
                end,
                center,
                distractorGlyphType,
                segment.WidthMultiplier));
        }
    }

    private static Vector2 FindDistractorCenter(
        Vector2 size,
        Vector2 trueGlyphCenter,
        float trueGlyphRadius,
        float distractorRadius,
        List<Vector2> occupiedCenters,
        RandomNumberGenerator rng)
    {
        float margin = distractorRadius * 1.35f;
        Vector2 candidate = size * 0.5f;

        for (int attempt = 0; attempt < 32; attempt++)
        {
            candidate = new Vector2(
                rng.RandfRange(margin, MathF.Max(size.X - margin, margin)),
                rng.RandfRange(margin, MathF.Max(size.Y - margin, margin)));
            if (candidate.DistanceTo(trueGlyphCenter) < trueGlyphRadius + distractorRadius * 1.8f)
                continue;

            bool overlapsDistractor = false;
            foreach (Vector2 occupiedCenter in occupiedCenters)
            {
                if (candidate.DistanceTo(occupiedCenter) >= distractorRadius * 2.5f) continue;
                overlapsDistractor = true;
                break;
            }

            if (!overlapsDistractor) break;
        }

        occupiedCenters.Add(candidate);
        return candidate;
    }

    private static void AddDistractorGlyph(
        List<DistractorSegment> destination,
        Vector2[] points,
        FragmentDistractorGlyphType glyphType,
        Vector2 center,
        float scale,
        float rotation)
    {
        for (int i = 0; i < points.Length; i += 2)
        {
            Vector2 start = center + (points[i] * scale).Rotated(rotation);
            Vector2 end = center + (points[i + 1] * scale).Rotated(rotation);
            destination.Add(new DistractorSegment(start, end, center, glyphType));
        }
    }

    private static Vector2[] CreateTridentGlyph() => new[]
    {
        new Vector2(0f, 0.65f), new Vector2(0f, -0.1f),
        new Vector2(0f, -0.1f), new Vector2(-0.55f, -0.6f),
        new Vector2(0f, -0.1f), new Vector2(0f, -0.7f),
        new Vector2(0f, -0.1f), new Vector2(0.55f, -0.6f)
    };

    private static Vector2[] CreateDiamondEyeGlyph() => new[]
    {
        new Vector2(-0.65f, 0f), new Vector2(0f, -0.5f),
        new Vector2(0f, -0.5f), new Vector2(0.65f, 0f),
        new Vector2(0.65f, 0f), new Vector2(0f, 0.5f),
        new Vector2(0f, 0.5f), new Vector2(-0.65f, 0f),
        new Vector2(-0.32f, 0f), new Vector2(0.32f, 0f)
    };

    private static Vector2[] CreateAngularSpiralGlyph() => new[]
    {
        new Vector2(-0.55f, 0.55f), new Vector2(-0.55f, -0.55f),
        new Vector2(-0.55f, -0.55f), new Vector2(0.55f, -0.55f),
        new Vector2(0.55f, -0.55f), new Vector2(0.55f, 0.35f),
        new Vector2(0.55f, 0.35f), new Vector2(-0.2f, 0.35f),
        new Vector2(-0.2f, 0.35f), new Vector2(-0.2f, -0.08f)
    };

    private static FragmentLine CreateLine(
        Vector2 start,
        Vector2 end,
        FragmentLineRole role,
        FragmentGenerationSettings settings,
        RandomNumberGenerator rng)
    {
        return new FragmentLine
        {
            Start = start,
            End = end,
            Role = role,
            Channel = ChooseScanChannel(settings, rng),
            Color = role == FragmentLineRole.Signal ? settings.LineColor : settings.DistractorColor,
            Width = settings.LineWidth,
            VisibleIntervals = GenerateVisibleIntervals(settings, rng)
        };
    }

    private static FragmentScanChannel ChooseScanChannel(
        FragmentGenerationSettings settings,
        RandomNumberGenerator rng)
    {
        float xRayFraction = Mathf.Clamp(settings.XRayChannelFraction, 0f, 0.9f);
        if (rng.Randf() < xRayFraction)
            return FragmentScanChannel.XRay;

        return rng.Randf() < Mathf.Clamp(settings.ElectromagneticChannelFraction, 0f, 1f)
            ? FragmentScanChannel.Electromagnetic
            : FragmentScanChannel.Resonance;
    }

    private static void ResolveCorrectChannelCombination(
        FragmentGenerationSettings settings,
        ulong seed,
        out bool electromagnetic,
        out bool resonance,
        out bool xRay)
    {
        if (!settings.RandomizeCorrectChannelCombination)
        {
            electromagnetic = settings.CorrectElectromagneticEnabled;
            resonance = settings.CorrectResonanceEnabled;
            xRay = settings.CorrectXRayEnabled;
            return;
        }

        RandomNumberGenerator rng = new() { Seed = seed ^ 0xD1B54A32D192ED03UL };
        int mask;
        do
        {
            mask = rng.RandiRange(0, 7);
        }
        while ((!settings.AllowNoChannelsSolution && mask == 0) ||
            (!settings.AllowAllChannelsSolution && mask == 7));

        electromagnetic = (mask & 1) != 0;
        resonance = (mask & 2) != 0;
        xRay = (mask & 4) != 0;
    }

    private static void ResolveCorrectProcessingCombination(
        FragmentGenerationSettings settings,
        ulong seed,
        out bool polarization,
        out int polarizationLevel,
        out bool spectral,
        out int spectralLevel,
        out bool surface,
        out int surfaceLevel)
    {
        if (!settings.RandomizeCorrectProcessingCombination)
        {
            polarization = settings.CorrectPolarizationEnabled;
            polarizationLevel = Mathf.Clamp(settings.CorrectPolarizationLevel, 1, 5);
            spectral = settings.CorrectSpectralEnabled;
            spectralLevel = Mathf.Clamp(settings.CorrectSpectralLevel, 1, 5);
            surface = settings.CorrectSurfaceEnabled;
            surfaceLevel = Mathf.Clamp(settings.CorrectSurfaceLevel, 1, 5);
            return;
        }

        RandomNumberGenerator rng = new() { Seed = seed ^ 0x94D049BB133111EBUL };
        float enabledProbability = Mathf.Clamp(settings.ProcessingEnabledProbability, 0f, 1f);
        polarization = rng.Randf() < enabledProbability;
        spectral = rng.Randf() < enabledProbability;
        surface = rng.Randf() < enabledProbability;
        if (!settings.AllowNoProcessingSolution && !polarization && !spectral && !surface)
        {
            int requiredProcessor = rng.RandiRange(0, 2);
            polarization = requiredProcessor == 0;
            spectral = requiredProcessor == 1;
            surface = requiredProcessor == 2;
        }

        polarizationLevel = rng.RandiRange(1, 5);
        spectralLevel = rng.RandiRange(1, 5);
        surfaceLevel = rng.RandiRange(1, 5);
    }

    private static List<SignalSegment> GenerateSignalGeometry(
        int count,
        Vector2 size,
        Vector2 center,
        Vector2 monolithDirection,
        FragmentGlyphType glyphType,
        FragmentGenerationSettings settings)
    {
        List<SignalSegment> segments = new(count);
        float canvasScale = MathF.Max(settings.CanvasSizeMultiplier, 1f);
        float radius = Mathf.Max(
            Mathf.Min(size.X, size.Y) / canvasScale * settings.PatternRadius,
            1f);
        // The frame and glyph are authored upright in puzzle-local coordinates.
        // FragmentCanvas applies any later display rotation to every stroke together.
        Vector2[] hexagon = CreatePointyHexagon(center, radius);
        AddClosedPolygon(segments, hexagon);

        switch (glyphType)
        {
            case FragmentGlyphType.Key:
                AddKeyGlyph(segments, center, radius);
                break;
            case FragmentGlyphType.Television:
                AddTelevisionGlyph(segments, center, radius);
                break;
            default:
                AddHominidGlyph(segments, center, radius, settings);
                break;
        }

        AddDirectionArrow(segments, center, radius, monolithDirection, hexagon, settings);

        // More signal lines increase reconstruction granularity without changing
        // the glyph: repeatedly split the longest existing stroke in half.
        while (segments.Count < count)
        {
            int longestIndex = 0;
            float longestLengthSquared = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                float lengthSquared = segments[i].Start.DistanceSquaredTo(segments[i].End);
                if (lengthSquared <= longestLengthSquared) continue;
                longestLengthSquared = lengthSquared;
                longestIndex = i;
            }

            SignalSegment longest = segments[longestIndex];
            Vector2 midpoint = (longest.Start + longest.End) * 0.5f;
            segments[longestIndex] = new SignalSegment(
                longest.Start,
                midpoint,
                longest.IsImportant,
                longest.WidthMultiplier);
            segments.Insert(longestIndex + 1, new SignalSegment(
                midpoint,
                longest.End,
                longest.IsImportant,
                longest.WidthMultiplier));
        }

        return segments;
    }

    private static void AddHominidGlyph(
        List<SignalSegment> segments,
        Vector2 center,
        float radius,
        FragmentGenerationSettings settings)
    {
        Vector2 right = Vector2.Right;
        Vector2 down = Vector2.Down;

        float outerHalfWidth = radius * Mathf.Clamp(settings.PatternAspect.X, 0.1f, 0.6f);
        float outerHalfHeight = radius * Mathf.Clamp(settings.PatternAspect.Y, 0.05f, 0.3f);
        AddRectangle(segments, center, right, down, outerHalfWidth, outerHalfHeight);

        float innerScale = Mathf.Clamp(settings.InnerRectangleScale, 0.1f, 0.9f);
        AddRectangle(
            segments,
            center,
            right,
            down,
            outerHalfWidth * innerScale,
            outerHalfHeight * innerScale);

        float longLegLength = radius * Mathf.Clamp(settings.LongLegLengthMultiplier, 0.2f, 0.9f);
        Vector2 longLegEnd = center + down * longLegLength;
        segments.Add(new SignalSegment(center, longLegEnd, true));

        Vector2 branchStart = center + down * longLegLength *
            Mathf.Clamp(settings.BranchPosition, 0.1f, 0.9f);
        Vector2 branchCorner = branchStart + right * radius *
            Mathf.Clamp(settings.BranchLengthMultiplier, 0.05f, 0.5f);
        Vector2 branchEnd = branchCorner + down * radius *
            Mathf.Clamp(settings.BranchDropLengthMultiplier, 0.05f, 0.5f);
        segments.Add(new SignalSegment(branchStart, branchCorner, true));
        segments.Add(new SignalSegment(branchCorner, branchEnd, true));
    }

    private static void AddKeyGlyph(
        List<SignalSegment> segments,
        Vector2 center,
        float radius)
    {
        float chipHalfWidth = radius * 0.62f;
        float chipHalfHeight = radius * 0.2f;
        AddRectangle(
            segments,
            center,
            Vector2.Right,
            Vector2.Down,
            chipHalfWidth,
            chipHalfHeight);

        // Connector legs on every side make the central rectangle read as a chip while
        // keeping enough space for the surrounding hexagonal frame.
        float pinLength = radius * 0.14f;
        float innerPinX = chipHalfWidth * 0.42f;
        AddPin(segments, center + new Vector2(-innerPinX, -chipHalfHeight), Vector2.Up, pinLength);
        AddPin(segments, center - Vector2.Down * chipHalfHeight, Vector2.Up, pinLength);
        AddPin(segments, center + new Vector2(innerPinX, -chipHalfHeight), Vector2.Up, pinLength);
        AddPin(segments, center + new Vector2(-innerPinX, chipHalfHeight), Vector2.Down, pinLength);
        AddPin(segments, center + new Vector2(innerPinX, chipHalfHeight), Vector2.Down, pinLength);
        AddPin(segments, center + new Vector2(-chipHalfWidth, 0f), Vector2.Left, pinLength);
        AddPin(segments, center + new Vector2(chipHalfWidth, 0f), Vector2.Right, pinLength);

        float terminalHalfWidth = radius * 0.1f;
        float terminalHalfHeight = radius * 0.08f;
        Vector2 terminalCenter = center + Vector2.Down * radius * 0.72f;
        Vector2 mastStart = center + Vector2.Down * chipHalfHeight;
        Vector2 mastEnd = terminalCenter - Vector2.Down * terminalHalfHeight;
        segments.Add(new SignalSegment(mastStart, mastEnd, true));
        AddRectangle(
            segments,
            terminalCenter,
            Vector2.Right,
            Vector2.Down,
            terminalHalfWidth,
            terminalHalfHeight,
            true);
    }

    private static void AddTelevisionGlyph(
        List<SignalSegment> segments,
        Vector2 center,
        float radius)
    {
        float bodyHalfWidth = radius * 0.55f;
        float bodyHalfHeight = radius * 0.24f;
        Vector2 bodyCenter = center;
        AddRectangle(
            segments,
            bodyCenter,
            Vector2.Right,
            Vector2.Down,
            bodyHalfWidth,
            bodyHalfHeight);

        // A divider forms the two display panes seen on the television fragment.
        segments.Add(new SignalSegment(
            bodyCenter - Vector2.Down * bodyHalfHeight,
            bodyCenter + Vector2.Down * bodyHalfHeight,
            true));

        // Four stands make the silhouette almost vertically symmetrical. The
        // slightly longer and heavier bottom-right stand is the orientation cue.
        float standLength = radius * 0.2f;
        float legOffset = bodyHalfWidth * 0.58f;
        float footHalfWidth = radius * 0.09f;
        const float bottomRightEmphasis = 1.1f;

        AddTelevisionStand(
            segments,
            bodyCenter + new Vector2(-legOffset, -bodyHalfHeight),
            Vector2.Up,
            standLength,
            footHalfWidth);
        AddTelevisionStand(
            segments,
            bodyCenter + new Vector2(legOffset, -bodyHalfHeight),
            Vector2.Up,
            standLength,
            footHalfWidth);
        AddTelevisionStand(
            segments,
            bodyCenter + new Vector2(-legOffset, bodyHalfHeight),
            Vector2.Down,
            standLength,
            footHalfWidth);
        AddTelevisionStand(
            segments,
            bodyCenter + new Vector2(legOffset, bodyHalfHeight),
            Vector2.Down,
            standLength * 1.28f * bottomRightEmphasis,
            footHalfWidth * 1.2f * bottomRightEmphasis,
            1.8f * bottomRightEmphasis);
    }

    private static void AddTelevisionStand(
        List<SignalSegment> segments,
        Vector2 start,
        Vector2 direction,
        float length,
        float footHalfWidth,
        float widthMultiplier = 1f)
    {
        Vector2 end = start + direction * length;
        segments.Add(new SignalSegment(start, end, true, widthMultiplier));
        segments.Add(new SignalSegment(
            end - Vector2.Right * footHalfWidth,
            end + Vector2.Right * footHalfWidth,
            true,
            widthMultiplier));
    }

    private static void AddPin(
        List<SignalSegment> segments,
        Vector2 start,
        Vector2 direction,
        float length)
    {
        segments.Add(new SignalSegment(start, start + direction * length));
    }

    private static void AddDirectionArrow(
        List<SignalSegment> segments,
        Vector2 center,
        float radius,
        Vector2 monolithDirection,
        Vector2[] hexagon,
        FragmentGenerationSettings settings)
    {
        // The direction marker is separate from the upright glyph. It starts where
        // the fragment-to-monolith ray exits the hexagon and continues outward.
        Vector2 arrowDirection = monolithDirection.IsZeroApprox()
            ? Vector2.Up
            : monolithDirection.Normalized();
        Vector2 arrowStart = FindRayPolygonBoundary(center, arrowDirection, hexagon);
        Vector2 arrowTip = arrowStart + arrowDirection * radius *
            MathF.Max(settings.DirectionArrowLengthMultiplier, 0.1f);
        float arrowHeadLength = radius * MathF.Max(settings.DirectionArrowHeadMultiplier, 0.05f);
        float arrowAngle = arrowDirection.Angle();
        segments.Add(new SignalSegment(arrowStart, arrowTip, true));
        segments.Add(new SignalSegment(
            arrowTip,
            arrowTip + Vector2.FromAngle(arrowAngle + Mathf.Pi - 0.5f) * arrowHeadLength,
            true));
        segments.Add(new SignalSegment(
            arrowTip,
            arrowTip + Vector2.FromAngle(arrowAngle + Mathf.Pi + 0.5f) * arrowHeadLength,
            true));
    }

    private static void ResolveFigureTransform(
        FragmentGenerationSettings settings,
        Vector2 size,
        ulong seed,
        out Vector2 center,
        out float initialRotationDegrees)
    {
        RandomNumberGenerator rng = new() { Seed = seed ^ 0xBF58476D1CE4E5B9UL };

        Vector2 normalizedCenter;
        if (settings.RandomizePatternPosition)
        {
            float minimumX = Mathf.Clamp(
                Mathf.Min(settings.PatternCenterMinimum.X, settings.PatternCenterMaximum.X),
                0f,
                1f);
            float maximumX = Mathf.Clamp(
                Mathf.Max(settings.PatternCenterMinimum.X, settings.PatternCenterMaximum.X),
                0f,
                1f);
            float minimumY = Mathf.Clamp(
                Mathf.Min(settings.PatternCenterMinimum.Y, settings.PatternCenterMaximum.Y),
                0f,
                1f);
            float maximumY = Mathf.Clamp(
                Mathf.Max(settings.PatternCenterMinimum.Y, settings.PatternCenterMaximum.Y),
                0f,
                1f);
            normalizedCenter = new Vector2(
                rng.RandfRange(minimumX, maximumX),
                rng.RandfRange(minimumY, maximumY));
        }
        else
        {
            normalizedCenter = new Vector2(
                Mathf.Clamp(settings.PatternCenter.X, 0f, 1f),
                Mathf.Clamp(settings.PatternCenter.Y, 0f, 1f));
        }

        center = normalizedCenter * size;

        // Keep the complete rotated figure, including the outward arrow, inside
        // the canvas whenever the canvas is large enough to contain it.
        float canvasScale = MathF.Max(settings.CanvasSizeMultiplier, 1f);
        float radius = Mathf.Max(
            Mathf.Min(size.X, size.Y) / canvasScale * settings.PatternRadius,
            1f);
        float maximumExtent = radius * (
            1f + MathF.Max(settings.DirectionArrowLengthMultiplier, 0.1f) +
            MathF.Max(settings.DirectionArrowHeadMultiplier, 0.05f));
        center = new Vector2(
            ClampCenterAxis(center.X, size.X, maximumExtent),
            ClampCenterAxis(center.Y, size.Y, maximumExtent));

        if (settings.RandomizeInitialRotation)
        {
            float minimumRotation = Mathf.Min(
                settings.InitialRotationMinimumDegrees,
                settings.InitialRotationMaximumDegrees);
            float maximumRotation = Mathf.Max(
                settings.InitialRotationMinimumDegrees,
                settings.InitialRotationMaximumDegrees);
            initialRotationDegrees = rng.RandfRange(minimumRotation, maximumRotation);
        }
        else
        {
            initialRotationDegrees = settings.InitialRotationDegrees;
        }

        initialRotationDegrees = Mathf.Wrap(initialRotationDegrees, -180f, 180f);
    }

    private static float ClampCenterAxis(float value, float axisSize, float extent)
    {
        if (extent * 2f >= axisSize) return axisSize * 0.5f;
        return Mathf.Clamp(value, extent, axisSize - extent);
    }

    private static Vector2[] CreatePointyHexagon(Vector2 center, float radius)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = -Mathf.Pi * 0.5f + Mathf.Tau * i / points.Length;
            points[i] = center + Vector2.FromAngle(angle) * radius;
        }
        return points;
    }

    private static void AddClosedPolygon(List<SignalSegment> segments, Vector2[] points)
    {
        for (int i = 0; i < points.Length; i++)
            segments.Add(new SignalSegment(points[i], points[(i + 1) % points.Length]));
    }

    private static Vector2 FindRayPolygonBoundary(Vector2 origin, Vector2 direction, Vector2[] polygon)
    {
        float nearestDistance = float.PositiveInfinity;
        Vector2 nearestPoint = origin;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 edgeStart = polygon[i];
            Vector2 edge = polygon[(i + 1) % polygon.Length] - edgeStart;
            float denominator = direction.Cross(edge);
            if (Mathf.IsZeroApprox(denominator)) continue;

            Vector2 offset = edgeStart - origin;
            float rayDistance = offset.Cross(edge) / denominator;
            float edgePosition = offset.Cross(direction) / denominator;
            if (rayDistance < 0f || edgePosition < 0f || edgePosition > 1f) continue;

            if (rayDistance >= nearestDistance) continue;
            nearestDistance = rayDistance;
            nearestPoint = origin + direction * rayDistance;
        }

        return nearestPoint;
    }

    private static void AddRectangle(
        List<SignalSegment> segments,
        Vector2 center,
        Vector2 right,
        Vector2 forward,
        float halfWidth,
        float halfHeight,
        bool isImportant = false)
    {
        Vector2 topLeft = center - right * halfWidth - forward * halfHeight;
        Vector2 topRight = center + right * halfWidth - forward * halfHeight;
        Vector2 bottomRight = center + right * halfWidth + forward * halfHeight;
        Vector2 bottomLeft = center - right * halfWidth + forward * halfHeight;

        segments.Add(new SignalSegment(topLeft, topRight, isImportant));
        segments.Add(new SignalSegment(topRight, bottomRight, isImportant));
        segments.Add(new SignalSegment(bottomRight, bottomLeft, isImportant));
        segments.Add(new SignalSegment(bottomLeft, topLeft, isImportant));
    }

    private static List<Vector2> GenerateVisibleIntervals(
        FragmentGenerationSettings settings,
        RandomNumberGenerator rng)
    {
        int maximumGapCount = Mathf.Max(settings.InactiveErasureSections, 1);
        int gapCount = rng.RandiRange(1, maximumGapCount);
        float erasedFraction = Mathf.Clamp(settings.InactiveErasedFraction, 0f, 0.95f);
        float visibleFraction = 1f - erasedFraction;
        float[] visibleWeights = new float[gapCount + 1];
        float[] gapWeights = new float[gapCount];
        float visibleWeightTotal = 0f;
        float gapWeightTotal = 0f;

        for (int i = 0; i < visibleWeights.Length; i++)
        {
            visibleWeights[i] = rng.RandfRange(0.15f, 1.85f);
            visibleWeightTotal += visibleWeights[i];
        }
        for (int i = 0; i < gapWeights.Length; i++)
        {
            gapWeights[i] = rng.RandfRange(0.15f, 1.85f);
            gapWeightTotal += gapWeights[i];
        }

        List<Vector2> intervals = new();
        float position = 0f;
        for (int i = 0; i < visibleWeights.Length; i++)
        {
            float length = visibleFraction * visibleWeights[i] / visibleWeightTotal;
            if (length > 0.001f)
                intervals.Add(new Vector2(position, position + length));
            position += length;
            if (i < gapWeights.Length)
                position += erasedFraction * gapWeights[i] / gapWeightTotal;
        }
        return intervals;
    }

    private static void GenerateVeins(
        FragmentPuzzle puzzle,
        FragmentRockSettings settings,
        float canvasScale,
        ulong seed)
    {
        RandomNumberGenerator rng = new() { Seed = seed ^ 0x9E3779B97F4A7C15UL };
        float resolution = Mathf.Clamp(settings.Resolution * canvasScale, 32f, 4096f);
        float minimumStep = Mathf.Min(settings.MinimumStepLength, settings.MaximumStepLength) / resolution;
        float maximumStep = Mathf.Max(settings.MinimumStepLength, settings.MaximumStepLength) / resolution;
        float minimumOpacity = Mathf.Min(settings.MinimumVeinOpacity, settings.MaximumVeinOpacity);
        float maximumOpacity = Mathf.Max(settings.MinimumVeinOpacity, settings.MaximumVeinOpacity);

        for (int veinIndex = 0; veinIndex < settings.VeinCount; veinIndex++)
        {
            Vector2[] points = new Vector2[settings.PointsPerVein];
            Vector2 point = new(rng.Randf(), rng.Randf());
            float angle = rng.RandfRange(0f, Mathf.Tau);
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                points[pointIndex] = point;
                angle += rng.RandfRange(-settings.MaximumTurn, settings.MaximumTurn);
                point += Vector2.FromAngle(angle) * rng.RandfRange(minimumStep, maximumStep);
            }

            puzzle.Veins.Add(new FragmentVein
            {
                NormalizedPoints = points,
                Opacity = rng.RandfRange(minimumOpacity, maximumOpacity)
            });
        }
    }
}
