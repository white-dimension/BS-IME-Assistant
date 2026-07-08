param(
    [string]$Configuration = "Release",
    [switch]$SkipCompile
)

$ErrorActionPreference = "Stop"

$InstallerRoot = $PSScriptRoot
$AssistantRepo = (Resolve-Path (Join-Path $InstallerRoot "..")).Path
$BsOsRoot = (Resolve-Path (Join-Path $AssistantRepo "..")).Path
$CadToolsRepo = Join-Path $BsOsRoot "BS-CAD-Tools"
$MaxToolsRepo = Join-Path $BsOsRoot "BS-3dsMax-Tools"

$StageRoot = Join-Path $InstallerRoot "stage"
$AppStage = Join-Path $StageRoot "app"
$CadStage = Join-Path $StageRoot "cad\BS-CAD-Tools.bundle"
$CadContents = Join-Path $CadStage "Contents"
$MaxStage = Join-Path $StageRoot "max"
$MaxStartupStage = Join-Path $MaxStage "startup"
$MaxScriptsStage = Join-Path $MaxStage "scripts"
$DistRoot = Join-Path $InstallerRoot "dist"

$AssistantProject = Join-Path $AssistantRepo "src\BS.IME.Assistant\BS.IME.Assistant.csproj"
$CadProject = Join-Path $CadToolsRepo "src\BS.CAD.Tools\BS.CAD.Tools.csproj"
$CadManifest = Join-Path $CadToolsRepo "installer\auto_load.xml"
$CadPublish = Join-Path $StageRoot "cad_publish"

if (Test-Path $StageRoot) { Remove-Item -LiteralPath $StageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $AppStage, $CadContents, $MaxStartupStage, $MaxScriptsStage, $DistRoot -Force | Out-Null

Write-Host "=== Publish BS IME Assistant ===" -ForegroundColor Cyan
dotnet publish $AssistantProject --configuration $Configuration --runtime win-x64 --self-contained true --output $AppStage /p:PublishSingleFile=false /p:PublishReadyToRun=false
if ($LASTEXITCODE -ne 0) { throw "BS IME Assistant publish failed ($LASTEXITCODE)" }

Write-Host "=== Publish BS-CAD-Tools plugin ===" -ForegroundColor Cyan
dotnet publish $CadProject --configuration $Configuration --output $CadPublish
if ($LASTEXITCODE -ne 0) { throw "BS-CAD-Tools publish failed ($LASTEXITCODE)" }

Copy-Item -LiteralPath $CadManifest -Destination (Join-Path $CadStage "PackageContents.xml") -Force
Copy-Item -LiteralPath (Join-Path $CadPublish "BS.CAD.Tools.dll") -Destination (Join-Path $CadContents "BS.CAD.Tools.dll") -Force
if (Test-Path (Join-Path $CadPublish "BS.CAD.Tools.pdb")) {
    Copy-Item -LiteralPath (Join-Path $CadPublish "BS.CAD.Tools.pdb") -Destination (Join-Path $CadContents "BS.CAD.Tools.pdb") -Force
}
Remove-Item -LiteralPath $CadPublish -Recurse -Force

Write-Host "=== Stage 3ds Max bridge ===" -ForegroundColor Cyan
Copy-Item -LiteralPath (Join-Path $MaxToolsRepo "startup\BS_IME_Startup.ms") -Destination (Join-Path $MaxStartupStage "BS_IME_Startup.ms") -Force
Copy-Item -LiteralPath (Join-Path $MaxToolsRepo "scripts\BS_IME_Bridge.ms") -Destination (Join-Path $MaxScriptsStage "BS_IME_Bridge.ms") -Force

$IssPath = Join-Path $InstallerRoot "BS-IME-Assistant-Setup.iss"
$IsccCandidateRoots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$IsccCandidates = $IsccCandidateRoots | ForEach-Object {
    Join-Path $_ "Inno Setup 6\ISCC.exe"
} | Where-Object { Test-Path -LiteralPath $_ }

if ($SkipCompile -or $IsccCandidates.Count -eq 0) {
    Write-Host "Stage is ready: $StageRoot" -ForegroundColor Yellow
    Write-Host "Install Inno Setup 6, then run this script again to create the setup EXE." -ForegroundColor Yellow
    return
}

$Iscc = $IsccCandidates[0]
Write-Host "=== Compile installer with Inno Setup ===" -ForegroundColor Cyan
& $Iscc $IssPath
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed ($LASTEXITCODE)" }

Write-Host "Installer output: $(Join-Path $DistRoot 'BS-IME-Assistant-Setup.exe')" -ForegroundColor Green

