using Warwrought.Bootstrap;
using Warwrought.Core;
using Xunit;

namespace Warwrought.Tests;

public sealed class StableIdAndBootstrapValidationTests
{
    [Fact]
    public void StableIdsUseOrdinalEqualityAndOrdering()
    {
        var alpha = new StableId("unit.alpha");
        var sameAlpha = new StableId("unit.alpha");
        var beta = new StableId("unit.beta");

        Assert.Equal(alpha, sameAlpha);
        Assert.Equal(alpha.GetHashCode(), sameAlpha.GetHashCode());
        Assert.NotEqual(alpha, beta);
        Assert.True(alpha < beta);
        Assert.True(beta > alpha);
    }

    [Fact]
    public void BootstrapAcceptanceArgumentsRequireTheScenarioAndReportPath()
    {
        var valid = BootstrapLabArguments.Parse(
            new[] { "--acceptance=bootstrap.m0", "--report=artifacts/local/m0.2/report.json" });
        var missingReport = BootstrapLabArguments.Parse(new[] { "--acceptance=bootstrap.m0" });

        Assert.True(valid.IsAcceptanceMode);
        Assert.Equal(BootstrapLabArguments.AcceptanceScenario, valid.ScenarioId);
        Assert.False(missingReport.IsValid);
        Assert.Contains("--report", missingReport.ValidationError, StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapReportCleanPassRequiresZeroUnexpectedErrors()
    {
        var report = BootstrapLabReport.Create(
            BootstrapLabArguments.AcceptanceScenario,
            "BootstrapLab",
            "res://scenes/Labs/BootstrapLab.tscn",
            "4.7.1.stable.mono.official",
            "Warwrought@0.1.0",
            "win-x64",
            "Warwrought",
            "0.1.0",
            unexpectedErrors: 0,
            passed: true,
            failureCategory: null,
            failureMessage: null);

        Assert.True(report.IsCleanPass);
        Assert.Empty(report.Validate());
        Assert.Contains("\"schemaVersion\": 1", report.ToJson(), StringComparison.Ordinal);
    }
}
