using System;
using System.Collections.Generic;
using System.IO;

namespace Warwrought.Bootstrap;

/// <summary>
/// Narrow command-line contract for the maintained 100v100 ScaleLab production scene.
/// </summary>
public sealed class BattleScaleLabArguments
{
    public const string AcceptanceScenario = "battlescalelab.m2.100v100";
    public const string OpenMeadowProfile = BattleLabArguments.OpenMeadowProfile;
    public const string BroadHighlandProfile = BattleLabArguments.BroadHighlandProfile;

    private BattleScaleLabArguments(
        bool isAcceptanceRequested,
        bool isValid,
        string? scenarioId,
        string? battlefieldProfileId,
        string? reportPath,
        string? validationError)
    {
        IsAcceptanceRequested = isAcceptanceRequested;
        IsValid = isValid;
        ScenarioId = scenarioId;
        BattlefieldProfileId = battlefieldProfileId;
        ReportPath = reportPath;
        ValidationError = validationError;
    }

    public bool IsAcceptanceRequested { get; }

    public bool IsAcceptanceMode => IsAcceptanceRequested && IsValid;

    public bool IsValid { get; }

    public string? ScenarioId { get; }

    public string? BattlefieldProfileId { get; }

    public string? ReportPath { get; }

    public string? ValidationError { get; }

    public static BattleScaleLabArguments Parse(IReadOnlyList<string> userArguments)
    {
        ArgumentNullException.ThrowIfNull(userArguments);

        var errors = new List<string>();
        string? scenarioId = null;
        string? reportPath = null;
        string? battlefieldProfileId = null;
        var sawAcceptanceOption = false;
        var sawReportOption = false;
        var sawProfileOption = false;

        for (var index = 0; index < userArguments.Count; index++)
        {
            var argument = userArguments[index];
            if (TryReadOption(userArguments, ref index, argument, "--acceptance", out var acceptanceValue))
            {
                if (sawAcceptanceOption)
                {
                    errors.Add("--acceptance was supplied more than once.");
                }

                sawAcceptanceOption = true;
                scenarioId = acceptanceValue;
                continue;
            }

            if (TryReadOption(userArguments, ref index, argument, "--report", out var reportValue))
            {
                if (sawReportOption)
                {
                    errors.Add("--report was supplied more than once.");
                }

                sawReportOption = true;
                reportPath = reportValue;
                continue;
            }

            if (TryReadOption(userArguments, ref index, argument, "--battlefield-profile", out var profileValue))
            {
                if (sawProfileOption)
                {
                    errors.Add("--battlefield-profile was supplied more than once.");
                }

                sawProfileOption = true;
                battlefieldProfileId = profileValue;
                continue;
            }

            errors.Add(string.IsNullOrWhiteSpace(argument)
                ? "An empty user argument is not valid."
                : $"Unknown BattleScaleLab user argument '{argument}'.");
        }

        var acceptanceRequested = userArguments.Count > 0;
        if (!acceptanceRequested)
        {
            return new BattleScaleLabArguments(false, true, null, OpenMeadowProfile, null, null);
        }

        if (!sawAcceptanceOption && !sawReportOption && sawProfileOption && errors.Count == 0)
        {
            return new BattleScaleLabArguments(false, true, null, battlefieldProfileId, null, null);
        }

        if (!sawAcceptanceOption || string.IsNullOrWhiteSpace(scenarioId))
        {
            errors.Add($"Acceptance mode requires --acceptance={AcceptanceScenario}.");
        }
        else if (!string.Equals(scenarioId, AcceptanceScenario, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported acceptance scenario '{scenarioId}'. Expected '{AcceptanceScenario}'.");
        }

        if (string.IsNullOrWhiteSpace(battlefieldProfileId))
        {
            battlefieldProfileId = OpenMeadowProfile;
        }
        else if (!string.Equals(battlefieldProfileId, OpenMeadowProfile, StringComparison.Ordinal) &&
                 !string.Equals(battlefieldProfileId, BroadHighlandProfile, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported battlefield profile '{battlefieldProfileId}'. Expected '{OpenMeadowProfile}' or '{BroadHighlandProfile}'.");
        }

        if (!sawReportOption || string.IsNullOrWhiteSpace(reportPath))
        {
            errors.Add("Acceptance mode requires a non-empty explicit --report path.");
        }
        else
        {
            try
            {
                _ = Path.GetFullPath(reportPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"The report path is invalid: {exception.Message}");
            }
        }

        return new BattleScaleLabArguments(
            true,
            errors.Count == 0,
            scenarioId,
            battlefieldProfileId,
            reportPath,
            errors.Count == 0 ? null : string.Join(" ", errors));
    }

    private static bool TryReadOption(
        IReadOnlyList<string> arguments,
        ref int index,
        string argument,
        string option,
        out string value)
    {
        var prefix = option + "=";
        if (argument.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = argument[prefix.Length..];
            return true;
        }

        if (!string.Equals(argument, option, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        if (index + 1 >= arguments.Count)
        {
            value = string.Empty;
            return true;
        }

        index++;
        value = arguments[index];
        return true;
    }
}
