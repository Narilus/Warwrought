using System;
using System.Collections.Generic;
using System.IO;

namespace Warwrought.Bootstrap;

public sealed class BootstrapLabArguments
{
    public const string AcceptanceScenario = "bootstrap.m0";
    public const string UnexpectedErrorInjection = "unexpected-error";

    private BootstrapLabArguments(
        bool isAcceptanceRequested,
        bool isValid,
        string? scenarioId,
        string? reportPath,
        string? faultInjection,
        string? validationError)
    {
        IsAcceptanceRequested = isAcceptanceRequested;
        IsValid = isValid;
        ScenarioId = scenarioId;
        ReportPath = reportPath;
        FaultInjection = faultInjection;
        ValidationError = validationError;
    }

    public bool IsAcceptanceRequested { get; }

    public bool IsAcceptanceMode => IsAcceptanceRequested && IsValid;

    public bool IsValid { get; }

    public string? ScenarioId { get; }

    public string? ReportPath { get; }

    public string? FaultInjection { get; }

    public string? ValidationError { get; }

    public static BootstrapLabArguments Parse(IReadOnlyList<string> userArguments)
    {
        ArgumentNullException.ThrowIfNull(userArguments);

        var errors = new List<string>();
        string? scenarioId = null;
        string? reportPath = null;
        string? faultInjection = null;
        var sawAcceptanceOption = false;
        var sawReportOption = false;
        var sawFaultOption = false;

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

            if (TryReadOption(userArguments, ref index, argument, "--inject-failure", out var faultValue))
            {
                if (sawFaultOption)
                {
                    errors.Add("--inject-failure was supplied more than once.");
                }

                sawFaultOption = true;
                faultInjection = faultValue;
                continue;
            }

            errors.Add(string.IsNullOrWhiteSpace(argument)
                ? "An empty user argument is not valid."
                : $"Unknown BootstrapLab user argument '{argument}'.");
        }

        var acceptanceRequested = userArguments.Count > 0;

        if (!acceptanceRequested)
        {
            return new BootstrapLabArguments(false, true, null, null, null, null);
        }

        if (!sawAcceptanceOption || string.IsNullOrWhiteSpace(scenarioId))
        {
            errors.Add("Acceptance mode requires --acceptance=bootstrap.m0.");
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

        if (sawFaultOption && !string.Equals(faultInjection, UnexpectedErrorInjection, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported --inject-failure value '{faultInjection}'. Expected '{UnexpectedErrorInjection}'.");
        }

        return new BootstrapLabArguments(
            true,
            errors.Count == 0,
            scenarioId,
            reportPath,
            faultInjection,
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
