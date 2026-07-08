param(
    [string]$AppRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AppRoot)) {
    $AppRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}

$StartupSource = Join-Path $AppRoot "3dsMax\startup\BS_IME_Startup.ms"
$MaxRoot = Join-Path $env:LOCALAPPDATA "Autodesk\3dsMax"
$LogRoot = Join-Path $env:APPDATA "BS-IME-Assistant\logs"
$LogPath = Join-Path $LogRoot "installer.log"

New-Item -ItemType Directory -Path $LogRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $StartupSource)) {
    "3ds Max startup source missing: $StartupSource" | Add-Content -LiteralPath $LogPath
    exit 0
}

if (-not (Test-Path -LiteralPath $MaxRoot)) {
    "3ds Max user folder not found: $MaxRoot" | Add-Content -LiteralPath $LogPath
    exit 0
}

$Targets = Get-ChildItem -LiteralPath $MaxRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    Join-Path $_.FullName "ENU\scripts\startup"
}

$Installed = 0
foreach ($Target in $Targets) {
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
    Copy-Item -LiteralPath $StartupSource -Destination (Join-Path $Target "BS_IME_Startup.ms") -Force
    "Installed 3ds Max startup bridge: $Target" | Add-Content -LiteralPath $LogPath
    $Installed++
}

if ($Installed -eq 0) {
    "No 3ds Max startup folders found under: $MaxRoot" | Add-Content -LiteralPath $LogPath
}
