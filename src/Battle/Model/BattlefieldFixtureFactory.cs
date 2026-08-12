using Warwrought.Core;

namespace Warwrought.Battle.Model;

/// <summary>
/// Fixed M2 battlefield lab profiles. These are local presentation/data fixtures, not campaign
/// provinces, final biome taxonomy, or additional M1 combat inputs.
/// </summary>
public static class BattlefieldFixtureFactory
{
    public const ulong M2OpenMeadowSeed = 0x4D324D4541444F57UL;
    public const ulong M2BroadHighlandSeed = 0x4D324849474C4E44UL;

    public static BattlefieldDefinition CreateM2OpenMeadowProfile(ulong seed = M2OpenMeadowSeed)
    {
        return BattlefieldGenerator.Generate(CreateRequest(
            new BattlefieldId("battlefield.m2.open-meadow"),
            seed,
            new BattlefieldGenerationParameters(
                averageElevationUnits: 5_500,
                roughnessPermille: 240,
                broadHillFrequency: 2,
                deploymentFlatteningStrengthPermille: 1_000,
                deploymentFlatteningRadiusUnits: 8_000,
                usableDeploymentMarginUnits: 4_000,
                minimumElevationUnits: 0,
                maximumElevationUnits: 18_000)));
    }

    public static BattlefieldDefinition CreateM2BroadHighlandProfile(ulong seed = M2BroadHighlandSeed)
    {
        return BattlefieldGenerator.Generate(CreateRequest(
            new BattlefieldId("battlefield.m2.broad-highland"),
            seed,
            new BattlefieldGenerationParameters(
                averageElevationUnits: 7_500,
                roughnessPermille: 520,
                broadHillFrequency: 4,
                deploymentFlatteningStrengthPermille: 1_000,
                deploymentFlatteningRadiusUnits: 7_000,
                usableDeploymentMarginUnits: 4_000,
                minimumElevationUnits: 0,
                maximumElevationUnits: 22_000)));
    }

    private static BattlefieldGenerationRequest CreateRequest(
        BattlefieldId battlefieldId,
        ulong seed,
        BattlefieldGenerationParameters generation)
    {
        var bounds = new BattlefieldBounds(-60_000, 60_000, -40_000, 40_000);
        var resolution = new BattlefieldResolution(17, 13);
        var zones = new[]
        {
            new BattlefieldDeploymentZoneInput(
                new StableId("deployment.side-a"),
                new BattlefieldBounds(-40_000, 40_000, -32_000, -10_000)),
            new BattlefieldDeploymentZoneInput(
                new StableId("deployment.side-b"),
                new BattlefieldBounds(-40_000, 40_000, 10_000, 32_000)),
        };

        return new BattlefieldGenerationRequest(
            BattlefieldDefinition.CurrentSchemaVersion,
            battlefieldId,
            seed,
            bounds,
            resolution,
            generation,
            zones);
    }
}
