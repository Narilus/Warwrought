using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;

namespace Warwrought.Bootstrap;

public partial class BootstrapLab : Node
{
    public const string SceneIdentity = "BootstrapLab";
    public const string ScenePath = "res://scenes/Labs/BootstrapLab.tscn";

    private int _unexpectedErrors;
    private string? _failureCategory;
    private string? _failureMessage;

    public override void _Ready()
    {
        try
        {
            var arguments = BootstrapLabArguments.Parse(OS.GetCmdlineUserArgs());

            if (!arguments.IsAcceptanceRequested)
            {
                GD.Print("BootstrapLab ready: maintained production C# Node, normal launch mode.");
                return;
            }

            RunAcceptance(arguments);
        }
        catch (Exception exception)
        {
            GD.PushError($"BootstrapLab bootstrap exception: {exception}");
            GetTree().Quit(1);
        }
    }

    private void RunAcceptance(BootstrapLabArguments arguments)
    {
        if (!arguments.IsValid)
        {
            RecordFailure(
                "InvalidArguments",
                arguments.ValidationError ?? "BootstrapLab acceptance arguments are invalid.",
                countAsUnexpectedError: false);

            CompleteAcceptance(arguments, passed: false, processExitCode: 1);
            return;
        }

        try
        {
            if (string.Equals(arguments.FaultInjection, BootstrapLabArguments.UnexpectedErrorInjection, StringComparison.Ordinal))
            {
                RecordFailure(
                    "InjectedUnexpectedError",
                    "Controlled acceptance-only fault injection requested an application-caught unexpected error.",
                    countAsUnexpectedError: true);
            }

            var passed = _unexpectedErrors == 0;

            // The injected fault intentionally leaves the Godot process exit code at zero. The report/log
            // verifier must reject the runtime-produced failed evidence rather than trusting process status.
            var processExitCode = arguments.FaultInjection is null ? (passed ? 0 : 1) : 0;
            CompleteAcceptance(arguments, passed, processExitCode);
        }
        catch (Exception exception)
        {
            RecordFailure("UnhandledRuntimeException", exception.ToString(), countAsUnexpectedError: true);
            CompleteAcceptance(arguments, passed: false, processExitCode: 1);
        }
    }

    private void CompleteAcceptance(BootstrapLabArguments arguments, bool passed, int processExitCode)
    {
        if (string.IsNullOrWhiteSpace(arguments.ReportPath))
        {
            GD.PushError("BootstrapLab acceptance cannot complete without an explicit --report path.");
            GetTree().Quit(2);
            return;
        }

        var report = CreateReport(arguments, passed);
        var reportPath = arguments.ReportPath;

        try
        {
            var validationErrors = report.Validate();
            if (validationErrors.Count > 0)
            {
                RecordFailure("ReportValidation", string.Join(" ", validationErrors), countAsUnexpectedError: true);
                report = CreateReport(arguments, passed: false);
                processExitCode = 2;
            }

            var fullReportPath = Path.GetFullPath(reportPath);
            var reportDirectory = Path.GetDirectoryName(fullReportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            File.WriteAllText(fullReportPath, report.ToJson());
            GD.Print($"BootstrapLab runtime report written: {fullReportPath}");
            GD.Print($"BootstrapLab acceptance result: passed={report.Passed}, unexpectedErrors={report.UnexpectedErrors}.");
            GD.Print($"BootstrapLab requesting intentional exit with code {processExitCode}.");
            GetTree().Quit(processExitCode);
        }
        catch (Exception exception)
        {
            RecordFailure("ReportWrite", exception.ToString(), countAsUnexpectedError: true);
            TryWriteFailureReport(arguments, reportPath, processExitCode: 2);
        }
    }

    private void TryWriteFailureReport(BootstrapLabArguments arguments, string reportPath, int processExitCode)
    {
        try
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            var reportDirectory = Path.GetDirectoryName(fullReportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            var failureReport = CreateReport(arguments, passed: false);
            File.WriteAllText(fullReportPath, failureReport.ToJson());
            GD.PushError($"BootstrapLab wrote a failed report after the initial report write failed: {fullReportPath}");
        }
        catch (Exception secondException)
        {
            GD.PushError($"BootstrapLab could not write a failure report: {secondException}");
        }

        GetTree().Quit(processExitCode);
    }

    private BootstrapLabReport CreateReport(BootstrapLabArguments arguments, bool passed)
    {
        return BootstrapLabReport.Create(
            arguments.ScenarioId ?? BootstrapLabArguments.AcceptanceScenario,
            SceneIdentity,
            ScenePath,
            GetGodotVersion(),
            GetBuildRuntimeIdentifier(),
            RuntimeInformation.RuntimeIdentifier,
            GetProjectSetting("application/config/name", "Warwrought"),
            GetProjectSetting("application/config/version", "0.1.0"),
            _unexpectedErrors,
            passed && _unexpectedErrors == 0,
            passed && _unexpectedErrors == 0 ? null : _failureCategory ?? "AcceptanceFailure",
            passed && _unexpectedErrors == 0 ? null : _failureMessage ?? "BootstrapLab acceptance did not pass.");
    }

    private void RecordFailure(string category, string message, bool countAsUnexpectedError)
    {
        if (countAsUnexpectedError)
        {
            _unexpectedErrors++;
        }

        _failureCategory ??= category;
        _failureMessage ??= message;
        GD.PushError($"BootstrapLab failure [{category}]: {message}");
    }

    private static string GetGodotVersion()
    {
        var versionInfo = Engine.GetVersionInfo();
        return versionInfo["string"].ToString();
    }

    private static string GetBuildRuntimeIdentifier()
    {
        var assemblyName = typeof(BootstrapLab).GetTypeInfo().Assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "development";
        return $"{assemblyName.Name ?? "Warwrought"}@{version}";
    }

    private static string GetProjectSetting(string settingName, string fallback)
    {
        var value = ProjectSettings.GetSetting(settingName, fallback).ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
