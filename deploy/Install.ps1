<#
.SYNOPSIS
    Installs SystemBar Indicator for the current user: publishes the app, registers a
    scheduled task that launches it elevated WITHOUT a UAC prompt, and creates a desktop
    shortcut that triggers the task silently.

.DESCRIPTION
    The app manifest is asInvoker, but it needs administrator rights to read CPU sensors
    (PawnIO kernel driver via LibreHardwareMonitor). A shortcut with the "run as admin"
    flag would prompt UAC on every launch. Instead we register a Task Scheduler task with
    RunLevel=Highest and point the shortcut at a hidden launcher (wscript -> schtasks /run).
    Triggering your own task does not require elevation, so there is no UAC prompt.

    Only the task registration needs admin rights, so the script self-elevates just that
    step (one UAC prompt during install). Publishing and the shortcut run unelevated.

    NOTE: this script is intentionally ASCII-only. A .ps1 with non-ASCII characters, saved
    as UTF-8 without BOM and re-read by Windows PowerShell (powershell.exe) as the ANSI
    code page, gets corrupted -- which previously broke the scheduled task. Keep it ASCII.

.PARAMETER AutoStart
    Also add an "at logon" trigger so the indicator starts automatically when you sign in
    (still elevated, still no UAC prompt).

.PARAMETER RegisterTaskOnly
    Internal. Used by the self-elevated child process to register the task only.

.EXAMPLE
    pwsh -File deploy/Install.ps1
.EXAMPLE
    pwsh -File deploy/Install.ps1 -AutoStart
#>
[CmdletBinding()]
param(
    [switch]$AutoStart,
    [switch]$RegisterTaskOnly
)

$ErrorActionPreference = 'Stop'

$AppName  = 'SystemBarIndicator'
$TaskName = 'SystemBarIndicator'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Project    = Join-Path $RepoRoot 'src\SystemBarIndicator.App'
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$Exe        = Join-Path $InstallDir "$AppName.exe"
$Vbs        = Join-Path $InstallDir 'launch.vbs'
$Desktop    = [Environment]::GetFolderPath('Desktop')
$Lnk        = Join-Path $Desktop 'SystemBar Indicator.lnk'
$User       = "$env:USERDOMAIN\$env:USERNAME"

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Register-Task {
    param([string]$ExePath, [bool]$Logon)

    $action    = New-ScheduledTaskAction -Execute $ExePath
    $principal = New-ScheduledTaskPrincipal -UserId $User -LogonType Interactive -RunLevel Highest
    $settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
                    -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -StartWhenAvailable

    $params = @{
        TaskName    = $TaskName
        TaskPath    = '\'
        Action      = $action
        Principal   = $principal
        Settings    = $settings
        Description = 'SystemBar Indicator - run elevated without UAC prompt'
        Force       = $true
    }
    if ($Logon) {
        $params['Trigger'] = New-ScheduledTaskTrigger -AtLogOn -User $User
    }
    Register-ScheduledTask @params | Out-Null
}

# --- Self-elevated child: register the task and exit ---
if ($RegisterTaskOnly) {
    Register-Task -ExePath $Exe -Logon:$AutoStart
    return
}

# --- 1) Publish the app (flat layout; LHM 0.9.6 is a RID-split package) ---
Write-Host "[1/4] Publishing $AppName -> $InstallDir"
dotnet publish $Project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=false -p:PublishReadyToRun=false -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# --- 2) Hidden launcher: triggers the task with no console flash (window style 0 = hidden) ---
Write-Host "[2/4] Writing launcher $Vbs"
$vbsContent = 'CreateObject("WScript.Shell").Run "schtasks /run /tn ""' + $TaskName + '""", 0, False'
[System.IO.File]::WriteAllText($Vbs, $vbsContent, [System.Text.Encoding]::ASCII)

# --- 3) Scheduled task (needs admin -> self-elevate just this step: one UAC prompt) ---
Write-Host "[3/4] Registering scheduled task '$TaskName' (a UAC prompt may appear -- click Yes)"
if (Test-Admin) {
    Register-Task -ExePath $Exe -Logon:$AutoStart
} else {
    $self = $MyInvocation.MyCommand.Path
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$self`"", '-RegisterTaskOnly')
    if ($AutoStart) { $argList += '-AutoStart' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs -Wait
}
# Verify the task is reachable at the root path (throws if registration failed)
$null = Get-ScheduledTask -TaskName $TaskName -TaskPath '\' -ErrorAction Stop

# --- 4) Desktop shortcut: wscript -> launcher, app icon, NO "run as admin" flag ---
Write-Host "[4/4] Creating desktop shortcut $Lnk"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($Lnk)
$sc.TargetPath       = Join-Path $env:SystemRoot 'System32\wscript.exe'
$sc.Arguments        = '"' + $Vbs + '"'
$sc.WorkingDirectory = $InstallDir
$sc.IconLocation     = "$Exe,0"
$sc.WindowStyle      = 7
$sc.Description       = 'SystemBar Indicator'
$sc.Save()
[void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($ws)

# CreateShortcut inherits the RunAsUser bit from an existing .lnk -- clear it explicitly.
# (LinkFlags bit 13 "RunAsUser" = bit 0x20 of the byte at offset 0x15.)
$b = [System.IO.File]::ReadAllBytes($Lnk)
$b[0x15] = $b[0x15] -band (-bnot 0x20)
[System.IO.File]::WriteAllBytes($Lnk, $b)

Write-Host ""
Write-Host "Done. Double-click 'SystemBar Indicator' on the desktop to launch (no UAC prompt)."
if ($AutoStart) { Write-Host "Auto-start at logon is ENABLED." }
