<#
.SYNOPSIS
    Removes the SystemBar Indicator scheduled task, desktop shortcut and launcher.

.DESCRIPTION
    Stops the running instance, unregisters the scheduled task (self-elevates for that
    step -- one UAC prompt), and deletes the desktop shortcut. The published files under
    %LOCALAPPDATA%\Programs\SystemBarIndicator are kept unless -RemoveFiles is given.

    ASCII-only on purpose (see the note in Install.ps1).

.PARAMETER RemoveFiles
    Also delete the published install directory.

.PARAMETER Internal
    Internal. Used by the self-elevated child to stop the app and remove the task.

.EXAMPLE
    pwsh -File deploy/Uninstall.ps1
.EXAMPLE
    pwsh -File deploy/Uninstall.ps1 -RemoveFiles
#>
[CmdletBinding()]
param(
    [switch]$RemoveFiles,
    [switch]$Internal
)

$ErrorActionPreference = 'Stop'

$AppName  = 'SystemBarIndicator'
$TaskName = 'SystemBarIndicator'
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$Lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'SystemBar Indicator.lnk'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Remove-Task {
    Get-Process $AppName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue | ForEach-Object {
        Unregister-ScheduledTask -TaskName $_.TaskName -TaskPath $_.TaskPath -Confirm:$false
    }
}

# --- Self-elevated child: stop app + remove task, then exit ---
if ($Internal) {
    Remove-Task
    return
}

# --- Remove the task (needs admin -> self-elevate: one UAC prompt) ---
Write-Host "Removing scheduled task '$TaskName' (a UAC prompt may appear -- click Yes)"
if (Test-Admin) {
    Remove-Task
} else {
    $self = $MyInvocation.MyCommand.Path
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$self`"", '-Internal')
}

# --- Remove shortcut + launcher (unelevated) ---
if (Test-Path $Lnk) { [System.IO.File]::Delete($Lnk); Write-Host "Removed shortcut $Lnk" }
$vbs = Join-Path $InstallDir 'launch.vbs'
if (Test-Path $vbs) { [System.IO.File]::Delete($vbs); Write-Host "Removed launcher $vbs" }

if ($RemoveFiles -and (Test-Path $InstallDir)) {
    [System.IO.Directory]::Delete($InstallDir, $true)
    Write-Host "Removed install directory $InstallDir"
}

Write-Host "Done."
