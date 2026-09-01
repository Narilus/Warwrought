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
$verifiedScenario = $null
$verifiedScene = $null

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

        if (Has-Property $report 'scenario') {
            switch ([string]$report.scenario) {
                'bootstrap.m0' {
                    $verifiedScenario = 'bootstrap.m0'
                    $verifiedScene = 'BootstrapLab'
                }
                'battlelab.m1.melee' {
                    $verifiedScenario = 'battlelab.m1.melee'
                    $verifiedScene = 'BattleLab'
                }
                'battlelab.m2.open-meadow' {
                    $verifiedScenario = 'battlelab.m2.open-meadow'
                    $verifiedScene = 'BattleLab'
                }
                'battlelab.m2.broad-highland' {
                    $verifiedScenario = 'battlelab.m2.broad-highland'
                    $verifiedScene = 'BattleLab'
                }
                'battlescalelab.m2.100v100' {
                    $verifiedScenario = 'battlescalelab.m2.100v100'
                    $verifiedScene = 'BattleScaleLab'
                }
                default {
                    Add-Failure "Runtime report scenario is unsupported: $($report.scenario)"
                }
            }
        }

        if ((Has-Property $report 'scene') -and $null -ne $verifiedScene -and $report.scene -ne $verifiedScene) {
            Add-Failure "Runtime report scene '$($report.scene)' does not match scenario '$verifiedScenario' (expected '$verifiedScene')."
        }

        if ((Has-Property $report 'scenePath') -and $null -ne $verifiedScene) {
            $expectedScenePath = if ($verifiedScene -eq 'BootstrapLab') {
                'res://scenes/Labs/BootstrapLab.tscn'
            }
            elseif ($verifiedScene -eq 'BattleScaleLab') {
                'res://scenes/Labs/BattleScaleLab.tscn'
            }
            else {
                'res://scenes/Labs/BattleLab.tscn'
            }

            if ($report.scenePath -ne $expectedScenePath) {
                Add-Failure "Runtime report scenePath '$($report.scenePath)' does not match expected '$expectedScenePath'."
            }
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

        if ($verifiedScenario -eq 'battlelab.m1.melee' -or
            $verifiedScenario -eq 'battlelab.m2.open-meadow' -or
            $verifiedScenario -eq 'battlelab.m2.broad-highland') {
            $battlelabRequiredProperties = @(
                'battleId',
                'seed',
                'simulationVersion',
                'authoritativeInputDigest',
                'transcriptDigest',
                'resultDigest',
                'digest',
                'result',
                'terminalTick',
                'survivorCount',
                'casualtyCount',
                'retreatedUnitCount',
                'skipResultDigest',
                'watchedResultDigest',
                'skipResult',
                'watchedResult',
                'skipWatchEquivalent',
                'resolutionIdentityShared',
                'headlessResolutionElapsedMilliseconds',
                'nominalTranscriptDurationMilliseconds'
            )

            foreach ($requiredProperty in $battlelabRequiredProperties) {
                if (-not (Has-Property $report $requiredProperty)) {
                    Add-Failure "BattleLab runtime report is missing $requiredProperty."
                }
            }

            $expectedBattleLabValues = @{
                battleId = 'battle.m1.melee.fixture'
                seed = 82741
                simulationVersion = '1.0'
                authoritativeInputDigest = '65ff279dac01cc31305336485af41cb595c37137edd25e04753aa5c62ec84787'
                transcriptDigest = '75dc2d6f0dda9fc71c364a5c265f1c640ce7bc949435f7522dc08896272faf58'
                resultDigest = '7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c'
                digest = '7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c'
                result = 'SideAWin'
                terminalTick = 576
                survivorCount = 100
                casualtyCount = 28
                retreatedUnitCount = 49
                skipResultDigest = '7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c'
                watchedResultDigest = '7faa2ec40c0830f317f4c87296c2e54c27d6f43def9b18f92d113834e982da1c'
                skipResult = 'SideAWin'
                watchedResult = 'SideAWin'
            }

            if ($verifiedScenario -eq 'battlelab.m2.open-meadow' -or $verifiedScenario -eq 'battlelab.m2.broad-highland') {
                $m2RequiredProperties = @(
                    'battlefieldProfile',
                    'battlefieldSeed',
                    'battlefieldDigest',
                    'terrainMeshTriangleCount',
                    'terrainMeshVertexCount',
                    'terrainMeshSamplerAgreement',
                    'foliagePlacementCount',
                    'foliageDigest',
                    'propCount',
                    'projectedUnitCount',
                    'projectedRemainsCount',
                    'projectedEffectCount',
                    'cameraOrthographic',
                    'cameraPanOperations',
                    'cameraZoomOperations',
                    'cameraResetOperations',
                    'cameraControlsObserved'
                )
                foreach ($requiredProperty in $m2RequiredProperties) {
                    if (-not (Has-Property $report $requiredProperty)) {
                        Add-Failure "M2 BattleLab runtime report is missing $requiredProperty."
                    }
                }

                $expectedProfile = if ($verifiedScenario -eq 'battlelab.m2.open-meadow') { 'battlefield.m2.open-meadow' } else { 'battlefield.m2.broad-highland' }
                $expectedSeed = if ($verifiedScenario -eq 'battlelab.m2.open-meadow') { [ulong]5562593449599061847 } else { [ulong]5562587969321979460 }
                $expectedDigest = if ($verifiedScenario -eq 'battlelab.m2.open-meadow') { '567f72c3d9e754722839fb8f1fb034fea7d9f647bc66690a36416ed0e7dc5443' } else { '5973015037208b1d09e4d50be3e0353b3bf200dd06fd1a1ccdf77ec4f691326f' }
                foreach ($expectedPair in @(
                    @('battlefieldProfile', $expectedProfile),
                    @('battlefieldSeed', $expectedSeed),
                    @('battlefieldDigest', $expectedDigest)
                )) {
                    if ((Has-Property $report $expectedPair[0]) -and [string]$report.($expectedPair[0]) -ne [string]$expectedPair[1]) {
                        Add-Failure "M2 BattleLab runtime report $($expectedPair[0]) '$($report.($expectedPair[0]))' does not match expected '$($expectedPair[1])'."
                    }
                }

                if ((Has-Property $report 'terrainMeshTriangleCount') -and [int]$report.terrainMeshTriangleCount -le 0) { Add-Failure 'M2 terrain mesh triangle count must be positive.' }
                if ((Has-Property $report 'terrainMeshVertexCount') -and [int]$report.terrainMeshVertexCount -le 0) { Add-Failure 'M2 terrain mesh vertex count must be positive.' }
                if ((Has-Property $report 'terrainMeshSamplerAgreement') -and $report.terrainMeshSamplerAgreement -ne $true) { Add-Failure 'M2 terrain mesh/sampler agreement must be true.' }
                if ((Has-Property $report 'foliagePlacementCount') -and [int]$report.foliagePlacementCount -le 0) { Add-Failure 'M2 foliage placement count must be positive.' }
                if ((Has-Property $report 'projectedUnitCount') -and [int]$report.projectedUnitCount -le 0) { Add-Failure 'M2 projected unit count must be positive.' }
                if ((Has-Property $report 'projectedRemainsCount') -and [int]$report.projectedRemainsCount -le 0) { Add-Failure 'M2 projected remains count must be positive.' }
                if ((Has-Property $report 'projectedEffectCount') -and [int]$report.projectedEffectCount -le 0) { Add-Failure 'M2 projected effect count must be positive.' }
                if ((Has-Property $report 'cameraOrthographic') -and $report.cameraOrthographic -ne $true) { Add-Failure 'M2 camera must report orthographic projection.' }
                if ((Has-Property $report 'cameraControlsObserved') -and $report.cameraControlsObserved -ne $true) { Add-Failure 'M2 camera controls were not observed.' }
            }

            foreach ($expectedProperty in $expectedBattleLabValues.Keys) {
                if ((Has-Property $report $expectedProperty) -and [string]$report.$expectedProperty -ne [string]$expectedBattleLabValues[$expectedProperty]) {
                    Add-Failure "BattleLab runtime report $expectedProperty '$($report.$expectedProperty)' does not match expected '$($expectedBattleLabValues[$expectedProperty])'."
                }
            }

            if ((Has-Property $report 'skipWatchEquivalent') -and $report.skipWatchEquivalent -ne $true) {
                Add-Failure 'BattleLab runtime report must prove skipWatchEquivalent=true.'
            }

            if ((Has-Property $report 'resolutionIdentityShared') -and $report.resolutionIdentityShared -ne $true) {
                Add-Failure 'BattleLab runtime report must prove resolutionIdentityShared=true.'
            }

            if ((Has-Property $report 'headlessResolutionElapsedMilliseconds') -and
                (Has-Property $report 'nominalTranscriptDurationMilliseconds')) {
                try {
                    $headlessMilliseconds = [double]$report.headlessResolutionElapsedMilliseconds
                    $nominalMilliseconds = [double]$report.nominalTranscriptDurationMilliseconds
                    if ($nominalMilliseconds -le 0) {
                        Add-Failure 'BattleLab nominalTranscriptDurationMilliseconds must be positive.'
                    }
                    elseif ($headlessMilliseconds -ge ($nominalMilliseconds / 5.0)) {
                        Add-Failure "BattleLab headless resolution ${headlessMilliseconds}ms did not complete in less than one fifth of nominal 1x duration ${nominalMilliseconds}ms."
                    }
                }
                catch {
                    Add-Failure 'BattleLab timing fields must be numeric milliseconds.'
                }
            }
        }

        if ($verifiedScenario -eq 'battlescalelab.m2.100v100') {
            $scaleRequiredProperties = @(
                'battleId',
                'seed',
                'simulationVersion',
                'authoritativeInputDigest',
                'transcriptDigest',
                'resultDigest',
                'result',
                'resolutionSourceClassification',
                'authoritativeResolutionRetained',
                'resolutionIdentityShared',
                'skipWatchEquivalent',
                'requestedUnitsPerSide',
                'expectedTotalUnitCount',
                'actualSideAUnitCount',
                'actualSideBUnitCount',
                'actualTotalUnitCount',
                'unitsSpawned',
                'projectedUnitCount',
                'transcriptEventCount',
                'transcriptKeyframeCount',
                'transcriptEventsConsumed',
                'movementKeyframesConsumed',
                'contactEventsPresented',
                'attackEventsPresented',
                'damageEventsPresented',
                'deathEventsPresented',
                'remainsSpawned',
                'routedFormationsShown',
                'controlTransitions',
                'resultShown',
                'playbackCompleted',
                'battlefieldProfile',
                'battlefieldSeed',
                'battlefieldDigest',
                'terrainMeshTriangleCount',
                'terrainMeshVertexCount',
                'terrainMeshSamplerAgreement',
                'foliagePlacementCount',
                'foliageDigest',
                'cameraOrthographic',
                'cameraPanOperations',
                'cameraZoomOperations',
                'cameraResetOperations',
                'cameraControlsObserved'
            )

            foreach ($requiredProperty in $scaleRequiredProperties) {
                if (-not (Has-Property $report $requiredProperty)) {
                    Add-Failure "BattleScaleLab runtime report is missing $requiredProperty."
                }
            }

            $expectedScaleValues = @{
                battleId = 'battle.m2.scale.100v100.fixture'
                seed = 82742
                simulationVersion = '1.0'
                resolutionSourceClassification = 'authoritative.real-resolver'
                requestedUnitsPerSide = 100
                expectedTotalUnitCount = 200
                actualSideAUnitCount = 100
                actualSideBUnitCount = 100
                actualTotalUnitCount = 200
                unitsSpawned = 200
            }
            foreach ($expectedProperty in $expectedScaleValues.Keys) {
                if ((Has-Property $report $expectedProperty) -and [string]$report.$expectedProperty -ne [string]$expectedScaleValues[$expectedProperty]) {
                    Add-Failure "BattleScaleLab runtime report $expectedProperty '$($report.$expectedProperty)' does not match expected '$($expectedScaleValues[$expectedProperty])'."
                }
            }

            foreach ($booleanProperty in @('authoritativeResolutionRetained', 'resolutionIdentityShared', 'skipWatchEquivalent', 'resultShown', 'playbackCompleted', 'cameraOrthographic', 'cameraControlsObserved', 'terrainMeshSamplerAgreement')) {
                if ((Has-Property $report $booleanProperty) -and $report.$booleanProperty -ne $true) {
                    Add-Failure "BattleScaleLab runtime report must prove $booleanProperty=true."
                }
            }

            foreach ($positiveProperty in @('seed', 'battlefieldSeed', 'projectedUnitCount', 'transcriptEventCount', 'transcriptKeyframeCount', 'transcriptEventsConsumed', 'movementKeyframesConsumed', 'contactEventsPresented', 'attackEventsPresented', 'damageEventsPresented', 'deathEventsPresented', 'remainsSpawned', 'routedFormationsShown', 'controlTransitions', 'terrainMeshTriangleCount', 'terrainMeshVertexCount', 'foliagePlacementCount', 'cameraPanOperations', 'cameraZoomOperations', 'cameraResetOperations')) {
                if ((Has-Property $report $positiveProperty) -and [double]$report.$positiveProperty -le 0) {
                    Add-Failure "BattleScaleLab runtime report $positiveProperty must be positive."
                }
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
    Write-Host 'Shared runtime verification FAILED.'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }

    exit 1
}

if ($verifiedScene -eq 'BattleLab') {
    Write-Host "BattleLab verification PASSED: report '$ReportPath' and log '$LogPath' are clean."
}
elseif ($verifiedScene -eq 'BattleScaleLab') {
    Write-Host "BattleScaleLab verification PASSED: report '$ReportPath' and log '$LogPath' are clean."
}
else {
    Write-Host "BootstrapLab verification PASSED: report '$ReportPath' and log '$LogPath' are clean."
}
exit 0
