param(
    [string]$ExecutablePath = ".\build\TankBattle.exe",
    [string]$OutputDirectory = ".\TrainingData\ChampionDemos",
    [int]$MatchesPerChampion = 50,
    [int]$MatchTimeSeconds = 180,
    [double]$TimeScale = 1,
    [int]$TargetFrameRate = -1,
    [int]$Parallelism = 1,
    [bool]$Headless = $true,
    [string[]]$Champions = @(
        "SYK.MyTank",
        "HYQ.MyTank",
        "ZYH_ICE.MyTank",
        "SM.MyTank"
    ),
    [string[]]$RecordingOpponentPool = @(
        "UtilityBasedAI.MyTank",
        "LTY.RuleBasedTank",
        "FSM.MyTank",
        "LGB.MyTank",
        "GOAP.MyTank",
        "HZR.MyTank",
        "BT.MyTank",
        "SensorAI.MyTank"
    ),
    [switch]$JsonlOnly,
    [switch]$DemoOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-UnityBool {
    param([bool]$Value)
    return $Value.ToString().ToLowerInvariant()
}

function ConvertTo-InvariantString {
    param([double]$Value)
    return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0}", $Value)
}

function ConvertTo-SafeFileName {
    param([string]$Value)
    $safeName = $Value
    foreach ($invalidChar in [System.IO.Path]::GetInvalidFileNameChars()) {
        $safeName = $safeName.Replace($invalidChar, "_")
    }
    return $safeName
}

$exe = Resolve-Path -Path $ExecutablePath -ErrorAction Stop
$outputRoot = New-Item -ItemType Directory -Force -Path $OutputDirectory
$logRoot = New-Item -ItemType Directory -Force -Path (Join-Path $outputRoot.FullName "Logs")

$recordMlAgentsDemo = -not $JsonlOnly.IsPresent
$recordJsonlDemo = -not $DemoOnly.IsPresent
$timeScaleText = ConvertTo-InvariantString $TimeScale
$maxParallelism = [Math]::Max(1, $Parallelism)

function Start-ChampionRecording {
    param([string]$Champion)

    $champion = $Champion
    $safeChampionName = ConvertTo-SafeFileName $champion
    $demoName = "tankbattle_expert_$safeChampionName"
    $logPath = Join-Path $logRoot.FullName "$safeChampionName.log"
    $jsonlPath = Join-Path $outputRoot.FullName "$demoName.jsonl"
    $opponents = @($RecordingOpponentPool | Where-Object { $_ -and $_ -ne $champion })
    if ($opponents.Count -eq 0) {
        $opponents = @($RecordingOpponentPool | Where-Object { $_ })
    }

    $unityArgs = @()
    if ($Headless) {
        $unityArgs += @("-batchmode", "-nographics")
    }
    $unityArgs += @("-logFile", $logPath)

    $tankBattleArgs = @(
        "--tankbattle-attach-demo-recorder", "true",
        "--tankbattle-opponent", $champion,
        "--tankbattle-demo-output-directory", $outputRoot.FullName,
        "--tankbattle-demo-directory", $outputRoot.FullName,
        "--tankbattle-demo-jsonl", $jsonlPath,
        "--tankbattle-record-mlagents-demo", (ConvertTo-UnityBool $recordMlAgentsDemo),
        "--tankbattle-record-jsonl-demo", (ConvertTo-UnityBool $recordJsonlDemo),
        "--tankbattle-recording-match-limit", $MatchesPerChampion,
        "--tankbattle-randomize-recording-opponent", "true",
        "--tankbattle-recording-opponent-pool", ($opponents -join ","),
        "--tankbattle-match-time", $MatchTimeSeconds,
        "--tankbattle-time-scale", $timeScaleText,
        "--tankbattle-target-frame-rate", $TargetFrameRate
    )

    Write-Host "Recording $champion -> $($outputRoot.FullName)"
    $process = Start-Process -FilePath $exe.Path -ArgumentList ($unityArgs + $tankBattleArgs) -PassThru
    return [pscustomobject]@{
        Champion = $champion
        DemoName = $demoName
        OutputDirectory = $outputRoot.FullName
        LogPath = $logPath
        Process = $process
    }
}


$running = New-Object System.Collections.ArrayList
$nextChampionIndex = 0
$failedRecordings = New-Object System.Collections.ArrayList

while ($nextChampionIndex -lt $Champions.Count -or $running.Count -gt 0) {
    while ($nextChampionIndex -lt $Champions.Count -and $running.Count -lt $maxParallelism) {
        [void]$running.Add((Start-ChampionRecording -Champion $Champions[$nextChampionIndex]))
        $nextChampionIndex++
    }

    $finishedIndex = -1
    for ($i = 0; $i -lt $running.Count; ++$i) {
        if ($running[$i].Process.HasExited) {
            $finishedIndex = $i
            break
        }
    }

    if ($finishedIndex -lt 0) {
        Start-Sleep -Seconds 2
        continue
    }

    $finished = $running[$finishedIndex]
    $running.RemoveAt($finishedIndex)
    if ($finished.Process.ExitCode -ne 0) {
        [void]$failedRecordings.Add($finished)
        Write-Warning "Recording failed for $($finished.Champion) with exit code $($finished.Process.ExitCode). See log: $($finished.LogPath)"
    }
    else {
        Write-Host "Finished $($finished.Champion)"
    }
}

if ($failedRecordings.Count -gt 0) {
    throw "$($failedRecordings.Count) champion recording(s) failed. Check logs under $($logRoot.FullName)."
}

Write-Host "Champion demo recording completed. Output: $($outputRoot.FullName)"
