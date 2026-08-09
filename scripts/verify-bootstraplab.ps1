[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportPath,

    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([Parameter(Mandatory = $true)][string]$Reason)

    [void]$failures.Add($Reason)
}

function Has-Property {
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    return $null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name
}

if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    Add-Failure "Runtime report was not found: $ReportPath"
}
else {
    $report = $null
    $reportParsed = $false
    try {
        $reportText = Get-Content -LiteralPath $ReportPath -Raw
        $report = $reportText | ConvertFrom-Json
        $reportParsed = $true
    }
    catch {
        Add-Failure "Runtime report is not valid JSON: $($_.Exception.Message)"
    }

    if ($reportParsed -and $null -eq $report) {
        Add-Failure 'Runtime report JSON did not contain an object.'
    }
    elseif ($null -ne $report) {
        if (-not (Has-Property $report 'schemaVersion')) {
            Add-Failure 'Runtime report is missing schemaVersion.'
        }
        else {
            try {
                if ([int]$report.schemaVersion -ne 1) {
                    Add-Failure "Unsupported runtime report schemaVersion: $($report.schemaVersion)"
                }
            }
            catch {
                Add-Failure "Runtime report schemaVersion is not an integer: $($report.schemaVersion)"
            }
        }

        foreach ($requiredProperty in @('scenario', 'scene', 'scenePath', 'godotVersion', 'buildRuntimeIdentifier', 'runtimeIdentifier', 'projectIdentity', 'projectVersion', 'unexpectedErrors', 'passed')) {
            if (-not (Has-Property $report $requiredProperty)) {
                Add-Failure "Runtime report is missing $requiredProperty."
            }
        }

        if ((Has-Property $report 'scenario') -and $report.scenario -ne 'bootstrap.m0') {
            Add-Failure "Runtime report scenario is not bootstrap.m0: $($report.scenario)"
        }

        if ((Has-Property $report 'scene') -and $report.scene -ne 'BootstrapLab') {
            Add-Failure "Runtime report scene is not BootstrapLab: $($report.scene)"
        }

        if ((Has-Property $report 'passed') -and (-not [bool]$report.passed)) {
            $category = if (Has-Property $report 'failureCategory') { $report.failureCategory } else { 'unknown category' }
            $message = if (Has-Property $report 'failureMessage') { $report.failureMessage } else { 'unknown message' }
            Add-Failure "Runtime report declares passed=false [$category]: $message"
        }

        if (Has-Property $report 'unexpectedErrors') {
            try {
                $unexpectedErrors = [int]$report.unexpectedErrors
                if ($unexpectedErrors -ne 0) {
                    Add-Failure "Runtime report declares unexpectedErrors=$unexpectedErrors."
                }
            }
            catch {
                Add-Failure "Runtime report unexpectedErrors is not an integer: $($report.unexpectedErrors)"
            }
        }
    }
}

if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
    Add-Failure "Explicit Godot log was not found: $LogPath"
}
else {
    try {
        $logText = Get-Content -LiteralPath $LogPath -Raw
    }
    catch {
        Add-Failure "Explicit Godot log could not be read: $($_.Exception.Message)"
        $logText = ''
    }

    # These are the documented unexpected-log categories for BootstrapLab acceptance.
    $documentedPatterns = @(
        [pscustomobject]@{ Name = 'Godot error record'; Regex = '(?im)^\s*ERROR\s*:' },
        [pscustomobject]@{ Name = 'exception'; Regex = '(?i)\b(?:unhandled\s+)?exception\b' },
        [pscustomobject]@{ Name = 'assertion failure'; Regex = '(?i)\bassert(?:ion)?\b(?:\s*[:\-]|\s+(?:failed|failure|error))' },
        [pscustomobject]@{ Name = 'missing resource/node'; Regex = '(?i)\b(?:missing|failed\s+to\s+load|cannot\s+load|can''t\s+load)\b[^\r\n]{0,100}\b(?:resource|node|scene)\b' },
        [pscustomobject]@{ Name = 'resource/node not found'; Regex = '(?i)\b(?:resource|node|scene)\b[^\r\n]{0,60}\bnot\s+found\b' },
        [pscustomobject]@{ Name = 'invalid node/resource'; Regex = '(?i)\binvalid\s+(?:node|resource|scene|reference)\b' }
    )

    foreach ($documentedPattern in $documentedPatterns) {
        $match = [regex]::Match($logText, $documentedPattern.Regex)
        if ($match.Success) {
            $line = ($match.Value -replace '[\r\n]+', ' ').Trim()
            Add-Failure "Log matched $($documentedPattern.Name) pattern '$($documentedPattern.Regex)': $line"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'BootstrapLab verification FAILED.'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }

    exit 1
}

Write-Host "BootstrapLab verification PASSED: report '$ReportPath' and log '$LogPath' are clean."
exit 0
