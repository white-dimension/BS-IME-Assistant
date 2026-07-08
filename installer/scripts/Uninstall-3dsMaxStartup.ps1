$ErrorActionPreference = "Stop"

$MaxRoot = Join-Path $env:LOCALAPPDATA "Autodesk\3dsMax"
if (-not (Test-Path -LiteralPath $MaxRoot)) { exit 0 }

Get-ChildItem -LiteralPath $MaxRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $StartupFile = Join-Path $_.FullName "ENU\scripts\startup\BS_IME_Startup.ms"
    if (Test-Path -LiteralPath $StartupFile) {
        Remove-Item -LiteralPath $StartupFile -Force
    }
}
