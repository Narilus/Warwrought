using System;
using System.Collections.Generic;
using Warwrought.Battle.Model;
using Warwrought.Core;

namespace Warwrought.Presentation.Battle;

public enum BattlefieldDecorationKind
{
    Tree,
    Bush,
    Rock,
}

public readonly record struct BattlefieldDecorationPlacement(
    int Ordinal,
    BattlefieldDecorationKind Kind,
    int XUnits,
    int ZUnits,
    int ScalePermille,
    int TintVariant,
    bool Flipped,
    StableId TerrainRegionId);

/// <summary>
/// Small deterministic presentation scatter for M2. It places only decorative, non-colliding
/// billboard/primitive placeholders. Deployment and contact exclusion is evaluated in simulation
/// X/Z data, never by inspecting nodes or rendered geometry.
/// </summary>
public sealed class BattlefieldFoliageScatter
{
    public const ulong PresentationSeedSalt = 0xBA7710F0114EUL;
    public const int DefaultPlacementCount = 30;
    public const int MinimumScalePermille = 760;
    public const int MaximumScalePermille = 1_260;

    private readonly BattlefieldDefinition _definition;
    private readonly BattlefieldHeightSampler _sampler;

    public BattlefieldFoliageScatter(BattlefieldDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _sampler = new BattlefieldHeightSampler(definition);
    }

    public BattlefieldDefinition Definition => _definition;

    public IReadOnlyList<BattlefieldDecorationPlacement> CreatePlacements(int placementCount = DefaultPlacementCount)
    {
        if (placementCount < 0 || placementCount > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(placementCount), placementCount, "M2 presentation scatter count must be between 0 and 256.");
        }

        var placements = new List<BattlefieldDecorationPlacement>(placementCount);
        var rng = new DeterministicRng(unchecked(_definition.Seed ^ PresentationSeedSalt));
        var attempts = 0;
        var maxAttempts = Math.Max(placementCount * 24, 24);
        while (placements.Count < placementCount && attempts < maxAttempts)
        {
            attempts++;
            var x = rng.NextInt(_definition.Bounds.MinX, _definition.Bounds.MaxX + 1);
            var z = rng.NextInt(_definition.Bounds.MinZ, _definition.Bounds.MaxZ + 1);
            if (IsExcluded(x, z))
            {
                continue;
            }

            var kind = (BattlefieldDecorationKind)(rng.NextInt(0, 10) < 5 ? 0 : rng.NextInt(1, 3));
            placements.Add(new BattlefieldDecorationPlacement(
                placements.Count,
                kind,
                x,
                z,
                rng.NextInt(MinimumScalePermille, MaximumScalePermille + 1),
                rng.NextInt(0, 3),
                rng.NextInt(0, 2) == 1,
                _sampler.RegionAt(x, z)));
        }

        return placements.AsReadOnly();
    }

    public string ComputePlacementDigest(IReadOnlyList<BattlefieldDecorationPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        using var writer = new CanonicalDigestWriter();
        writer.WriteString("warwrought.battlefield-decoration.m2.v1");
        writer.WriteString(_definition.BattlefieldId.Value);
        writer.WriteUInt64(_definition.Seed ^ PresentationSeedSalt);
        writer.WriteInt32(placements.Count);
        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            writer.WriteInt32(placement.Ordinal);
            writer.WriteInt32((int)placement.Kind);
            writer.WriteInt32(placement.XUnits);
            writer.WriteInt32(placement.ZUnits);
            writer.WriteInt32(placement.ScalePermille);
            writer.WriteInt32(placement.TintVariant);
            writer.WriteBoolean(placement.Flipped);
            writer.WriteString(placement.TerrainRegionId.Value);
        }

        return writer.ComputeSha256Hex();
    }

    public bool IsExcluded(int x, int z)
    {
        foreach (var zone in _definition.DeploymentZones)
        {
            if (zone.Bounds.ContainsInclusive(x, z))
            {
                return true;
            }
        }

        // Keep the central contact corridor quiet without introducing an obstacle or rule.
        var center = _definition.Bounds.Center;
        return Math.Abs((long)x - center.X) <= 5_000 && Math.Abs((long)z - center.Z) <= 7_000;
    }
}
