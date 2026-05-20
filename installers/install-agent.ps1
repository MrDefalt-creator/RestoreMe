<#
.SYNOPSIS
    RestoreMe Agent installer for Windows.

.DESCRIPTION
    Downloads the latest agent binary, installs it under Program Files,
    registers a Windows Service, and starts it.

.EXAMPLE
    # Install
    .\install-agent.ps1 -Server https://restoreme.example.com -Token <enrollment-token>

.EXAMPLE
    # Install a specific tag
    .\install-agent.ps1 -Server https://restoreme.example.com -Token <token> -Version v0.1.0

.EXAMPLE
    # Uninstall (keeps state)
    .\install-agent.ps1 -Uninstall

.EXAMPLE
    # Uninstall and wipe state
    .\install-agent.ps1 -Uninstall -Purge
#>
[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    [Parameter(ParameterSetName = 'Install', Mandatory = $true)]
    [string]$Server,

    [Parameter(ParameterSetName = 'Install', Mandatory = $true)]
    [string]$Token,

    [Parameter(ParameterSetName = 'Install')]
    [string]$Version = 'latest',

    [Parameter(ParameterSetName = 'Install')]
    [string]$StateDir,

    [Parameter(ParameterSetName = 'Install')]
    [string]$Repo = 'MrDefalt-creator/RestorMe',

    [Parameter(ParameterSetName = 'Uninstall', Mandatory = $true)]
    [switch]$Uninstall,

    [Parameter(ParameterSetName = 'Uninstall')]
    [switch]$Purge
)

$ErrorActionPreference = 'Stop'

$ServiceName    = 'RestoreMeAgent'
$ServiceDisplay = 'RestoreMe Agent'
$InstallDir     = Join-Path $env:ProgramFiles 'RestoreMe\Agent'
$ConfigDir      = Join-Path $env:ProgramData  'RestoreMe\Agent'
$BinaryPath     = Join-Path $InstallDir 'restoreme-agent.exe'

if (-not $StateDir) {
    $StateDir = Join-Path $env:ProgramData 'RestoreMe\Agent\state'
}

function Assert-Admin {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This installer must be run from an elevated (Administrator) PowerShell session.'
    }
}

Assert-Admin

if ($Uninstall) {
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "==> Stopping $ServiceName"
        try { Stop-Service -Name $ServiceName -Force -ErrorAction Stop } catch {}
        & sc.exe delete $ServiceName | Out-Null
    }

    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
    if (Test-Path $ConfigDir)  { Remove-Item -Recurse -Force $ConfigDir }

    if ($Purge -and (Test-Path $StateDir)) {
        Remove-Item -Recurse -Force $StateDir
        Write-Host "==> Purged state at $StateDir"
    } else {
        Write-Host "==> State preserved at $StateDir. Re-run with -Purge to delete it."
    }

    Write-Host 'Uninstall complete.'
    return
}

# Only ship 64-bit
if (-not [Environment]::Is64BitOperatingSystem) {
    throw 'Only 64-bit Windows is supported.'
}

$rid   = 'win-x64'
$asset = "restoreme-agent-$rid.exe"

if ($Version -eq 'latest') {
    $url = "https://github.com/$Repo/releases/latest/download/$asset"
} else {
    $url = "https://github.com/$Repo/releases/download/$Version/$asset"
}

Write-Host "==> Downloading: $url"
New-Item -ItemType Directory -Force -Path $InstallDir, $ConfigDir, $StateDir | Out-Null

# Stop existing service before overwriting the binary (re-install path)
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    try { Stop-Service -Name $ServiceName -Force -ErrorAction Stop } catch {}
}

# UseBasicParsing avoids the IE engine dependency on minimal Server Core images
Invoke-WebRequest -Uri $url -OutFile $BinaryPath -UseBasicParsing

# Register service if missing
if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "==> Registering Windows Service '$ServiceDisplay'"
    & sc.exe create $ServiceName binPath= "`"$BinaryPath`"" DisplayName= "$ServiceDisplay" start= auto | Out-Null
    & sc.exe description $ServiceName "RestoreMe backup agent. See https://github.com/$Repo" | Out-Null
    & sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/10000/restart/10000 | Out-Null
}

# Inject env vars via the service's registry key. SCM hands these to the
# process at start, so the agent picks them up exactly like it would on
# Linux from systemd's EnvironmentFile=.
$svcReg    = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$envValues = @(
    "RESTOREME_SERVER=$Server",
    "RESTOREME_ENROLLMENT_TOKEN=$Token",
    "RESTOREME_STATE_DIR=$StateDir"
)
Set-ItemProperty -Path $svcReg -Name Environment -Value $envValues -Type MultiString
Write-Host '==> Wrote service env vars to registry'

Write-Host "==> Starting $ServiceName"
Start-Service -Name $ServiceName
Start-Sleep -Seconds 2
Get-Service -Name $ServiceName | Format-Table -AutoSize

Write-Host ''
Write-Host 'Logs:'
Write-Host '  Event Viewer -> Windows Logs -> Application (source: RestoreMe Agent)'
Write-Host "  Get-EventLog -LogName Application -Source 'RestoreMe*' -Newest 20"
