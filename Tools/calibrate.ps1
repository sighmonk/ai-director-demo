<#
.SYNOPSIS
Runs the static-arm (S) calibration for the simulation bot and writes a Markdown report.

.DESCRIPTION
Each session runs in its own headless Godot process with a fixed 1/60 physics step, so autoload
state never leaks between seeds. Raw JSONL records go to TestResults/calibration (gitignored); the
aggregated report is written to Docs/calibration.md.

.EXAMPLE
pwsh Tools/calibrate.ps1 -SeedCount 25

.EXAMPLE
pwsh Tools/calibrate.ps1 -Profiles novice,expert -SeedCount 3 -SessionSeconds 200
#>
[CmdletBinding()]
param(
    [string]$GodotBin = 'C:\Godot\4.7.2mono\Godot_v4.7.2-stable_mono_win64_console.exe',
    [string[]]$Profiles = @('novice', 'average', 'expert', 'sandbagger'),
    [int]$SeedCount = 25,
    [int]$SessionSeconds = 600,
    [string]$OutDirectory = 'TestResults/calibration',
    [string]$ReportPath = 'Docs/calibration.md',
    [int]$Parallel = 1,    [switch]$SkipExisting,
    [switch]$EnforceWindows
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = if ([System.IO.Path]::IsPathRooted($OutDirectory)) { $OutDirectory } else { Join-Path $root $OutDirectory }
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

Write-Host 'Importing the Godot project...'
& $GodotBin --headless --path $root --import *> $null
if ($LASTEXITCODE -ne 0) { throw "Godot import failed with exit code $LASTEXITCODE." }

function Wait-CalibrationBatch {
    param([object[]]$Batch)

    try {
        foreach ($job in $Batch) {
            $job.Process.WaitForExit()
            if (-not (Test-Path $job.Record)) {
                throw "[$($job.Profile)] seed $($job.Seed) failed with exit code $($job.Process.ExitCode) and wrote no record."
            }
            if ($job.Process.ExitCode -ne 0) {
                Write-Warning "[$($job.Profile)] seed $($job.Seed) exited with $($job.Process.ExitCode) after writing its record (Godot shutdown)."
            }
        }
    }
    catch {
        foreach ($job in $Batch) {
            if (-not $job.Process.HasExited) {
                Stop-Process -Id $job.Process.Id -Force -ErrorAction SilentlyContinue
            }
        }
        throw
    }
}

$started = Get-Date
$logDirectory = Join-Path $outputDirectory 'logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$batch = @()
foreach ($profile in $Profiles) {
    for ($seed = 1; $seed -le $SeedCount; $seed++) {
        $recordPath = Join-Path $outputDirectory "$profile-$seed.jsonl"
        if ($SkipExisting -and (Test-Path $recordPath)) {
            Write-Host "[$profile] seed $seed already present, skipped."
            continue
        }

        if (Test-Path $recordPath) { Remove-Item $recordPath -Force }

        Write-Host "[$profile] seed $seed..."
        $logPath = Join-Path $logDirectory "$profile-$seed.log"
        $errorPath = Join-Path $logDirectory "$profile-$seed.err.log"
        $arguments = @(
            '--headless',
            '--path', $root,
            '--fixed-fps', '60',
            '--',
            "--calibration-profile=$profile",
            "--calibration-seed=$seed",
            "--calibration-seconds=$SessionSeconds",
            "--calibration-out=$recordPath"
        )

        $process = Start-Process -FilePath $GodotBin -ArgumentList $arguments -NoNewWindow -PassThru `
            -RedirectStandardOutput $logPath -RedirectStandardError $errorPath
        $batch += [pscustomobject]@{ Process = $process; Profile = $profile; Seed = $seed; Record = $recordPath }

        if ($batch.Count -lt [Math]::Max(1, $Parallel)) { continue }

        Wait-CalibrationBatch -Batch $batch
        $batch = @()
    }
}

if ($batch.Count -gt 0) {
    Wait-CalibrationBatch -Batch $batch
}

$elapsed = (Get-Date) - $started

$records = foreach ($profile in $Profiles) {
    foreach ($file in Get-ChildItem -Path $outputDirectory -Filter "$profile-*.jsonl") {
        foreach ($line in Get-Content -Path $file.FullName) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $line | ConvertFrom-Json
        }
    }
}

$targets = @{
    novice = @{ Min = 90.0; Max = 140.0 }
    expert = @{ Min = 380.0; Max = 450.0 }
}

$summary = foreach ($profile in $Profiles) {
    $values = @($records | Where-Object { $_.profile -eq $profile } | ForEach-Object { [double]$_.survived } | Sort-Object)
    if ($values.Count -eq 0) { continue }

    $median = if ($values.Count % 2 -eq 1) {
        $values[[int](($values.Count - 1) / 2)]
    } else {
        ($values[($values.Count / 2) - 1] + $values[$values.Count / 2]) / 2
    }

    $target = $targets[$profile]
    $status = 'n/a'
    if ($null -ne $target) {
        $status = if ($median -ge $target.Min -and $median -le $target.Max) { 'pass' } else { 'fail' }
    }

    [pscustomobject]@{
        Profile = $profile
        Seeds = $values.Count
        Min = [math]::Round($values[0], 1)
        Median = [math]::Round($median, 1)
        Max = [math]::Round($values[-1], 1)
        Target = if ($null -ne $target) { "$($target.Min)-$($target.Max) s" } else { '-' }
        Status = $status
    }
}

$monotonic = $true
$medians = @()
foreach ($profile in @('novice', 'average', 'expert')) {
    $row = $summary | Where-Object { $_.Profile -eq $profile } | Select-Object -First 1
    if ($null -ne $row) { $medians += $row.Median }
}
for ($i = 1; $i -lt $medians.Count; $i++) {
    if ($medians[$i] -le $medians[$i - 1]) { $monotonic = $false }
}

$reportDirectory = Split-Path -Parent $ReportPath
if ($reportDirectory -and -not [System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath = Join-Path $root $ReportPath
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null

$lines = @(
    '# Simulation bot calibration',
    '',
    'Generated by `Tools/calibrate.ps1`. Each session runs headless on the static arm S with a',
    'fixed 1/60 physics step and one process per seed.',
    '',
    '```powershell',
    "pwsh Tools/calibrate.ps1 -SeedCount $SeedCount -SessionSeconds $SessionSeconds",
    '```',
    '',
    '| Profile | Seeds | Min | Median | Max | Target | Result |',
    '| --- | --- | --- | --- | --- | --- | --- |'
)

foreach ($row in $summary) {
    $lines += "| $($row.Profile) | $($row.Seeds) | $($row.Min) s | $($row.Median) s | $($row.Max) s | $($row.Target) | $($row.Status) |"
}

$lines += ''
$lines += "- Monotonic novice < average < expert medians: $monotonic"
$lines += "- Wall-clock run time: $([math]::Round($elapsed.TotalMinutes, 1)) min"
$lines += ''
$lines += 'Raw JSONL records are written to `TestResults/calibration/` and are not committed.'

$lines | Set-Content -Path $ReportPath -Encoding utf8
Write-Host "Report written to $ReportPath"
$summary | Format-Table -AutoSize

if ($EnforceWindows) {
    $failed = @($summary | Where-Object { $_.Status -eq 'fail' })
    if ($failed.Count -gt 0 -or -not $monotonic) {
        throw "Calibration windows not met (monotonic: $monotonic)."
    }
}
