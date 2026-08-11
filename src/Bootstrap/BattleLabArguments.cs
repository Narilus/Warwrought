using System;
using System.Collections.Generic;
using System.IO;

namespace Warwrought.Bootstrap;

/// <summary>
/// The deliberately narrow BattleLab command-line contract. A normal launch has no user
/// arguments; acceptance has exactly one supported scenario and an explicit report path.
/// </summary>
public sealed class BattleLabArguments
{
    public const string AcceptanceScenario = "battlelab.m1.melee";

    private BattleLabArguments(
        bool isAcceptanceRequested,
        bool isValid,
        string? scenarioId,
        string? reportPath,
        string? validationError)
    {
        IsAcceptanceRequested = isAcceptanceRequested;
        IsValid = isValid;
        ScenarioId = scenarioId;
        ReportPath = reportPath;
        ValidationError = validationError;
    }

    public bool IsAcceptanceRequested { get; }

    public bool IsAcceptanceMode => IsAcceptanceRequested && IsValid;

    public bool IsValid { get; }

    public string? ScenarioId { get; }

    public string? ReportPath { get; }

    public string? ValidationError { get; }

    public static BattleLabArguments Parse(IReadOnlyList<string> userArguments)
    {
        ArgumentNullException.ThrowIfNull(userArguments);

        var errors = new List<string>();
        string? scenarioId = null;
        string? reportPath = null;
        var sawAcceptanceOption = false;
        var sawReportOption = false;

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

            errors.Add(string.IsNullOrWhiteSpace(argument)
                ? "An empty user argument is not valid."
                : $"Unknown BattleLab user argument '{argument}'.");
        }

        var acceptanceRequested = userArguments.Count > 0;
        if (!acceptanceRequested)
        {
            return new BattleLabArguments(false, true, null, null, null);
        }

        if (!sawAcceptanceOption || string.IsNullOrWhiteSpace(scenarioId))
        {
            errors.Add("Acceptance mode requires --acceptance=battlelab.m1.melee.");
        }
        else if (!string.Equals(scenarioId, AcceptanceScenario, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported acceptance scenario '{scenarioId}'. Expected '{AcceptanceScenario}'.");
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

        return new BattleLabArguments(
            true,
            errors.Count == 0,
            scenarioId,
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
