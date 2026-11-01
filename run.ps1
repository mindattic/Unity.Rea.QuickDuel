<#
.SYNOPSIS
    Builds the standalone Windows player to Build/QuickDuel/QuickDuel.exe.
.DESCRIPTION
    Runs the Unity editor in batch mode with QuickDuelBuild.BuildWindows. Exits non-zero when Unity
    fails or the exe isn't produced. The Unity log goes to Logs/deploy.log.
.PARAMETER Unity
    Path to Unity.exe. Defaults to $env:UNITY_EXE, then the Hub install of the version in
    ProjectSettings/ProjectVersion.txt.
#>
param([string]$Unity = $env:UNITY_EXE)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not $Unity) {
    $version = (Select-String -Path (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
    $Unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $Unity)) {
    Write-Error "Unity not found at '$Unity'. Pass -Unity or set UNITY_EXE."
    exit 2
}

$exe = Join-Path $root 'Build/QuickDuel/QuickDuel.exe'
$log = Join-Path $root 'Logs/deploy.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
if (Test-Path $exe) { Remove-Item $exe -Force }

Write-Host "Building QuickDuel with $Unity ..."
$arguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$root`"",
    '-buildTarget', 'Win64',
    '-executeMethod', 'Unity.Rea.QuickDuel.EditorTools.QuickDuelBuild.BuildWindows',
    '-logFile', "`"$log`""
)
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow

if ($process.ExitCode -ne 0) {
    Select-String -Path $log -Pattern 'error CS|Build Finished|QuickDuel build|Error building' | Select-Object -Last 20 | ForEach-Object { Write-Host $_.Line }
    Write-Error "Unity exited with $($process.ExitCode); see $log"
    exit $process.ExitCode
}
if (-not (Test-Path $exe)) {
    Write-Error "Unity exited 0 but $exe was not produced; see $log"
    exit 1
}

Write-Host "Built $exe"
exit 0
