param(
    [string]$Configuration = "Release",
    [switch]$SkipCompile
)

$ErrorActionPreference = "Stop"

$InstallerRoot = $PSScriptRoot
$AssistantRepo = (Resolve-Path (Join-Path $InstallerRoot "..")).Path

$StageRoot = Join-Path $InstallerRoot "stage"
$AppStage = Join-Path $StageRoot "app"
$CadBridgeStage = Join-Path $StageRoot "cad\BS-IME-Assistant-CadBridge.bundle"
$CadBridgeContents = Join-Path $CadBridgeStage "Contents"
$DistRoot = Join-Path $InstallerRoot "dist"

$AssistantProject = Join-Path $AssistantRepo "src\BS.IME.Assistant\BS.IME.Assistant.csproj"
$CadBridgeProject = Join-Path $AssistantRepo "src\BS.IME.Assistant.CadBridge\BS.IME.Assistant.CadBridge.csproj"
$CadBridgeManifest = Join-Path $InstallerRoot "cad\PackageContents.xml"
$CadBridgePublish = Join-Path $StageRoot "cad_publish"

if (Test-Path $StageRoot) { Remove-Item -LiteralPath $StageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $AppStage, $CadBridgeContents, $DistRoot -Force | Out-Null

Write-Host "=== Publish BS IME Assistant ===" -ForegroundColor Cyan
dotnet publish $AssistantProject --configuration $Configuration --runtime win-x64 --self-contained true --output $AppStage /p:PublishSingleFile=false /p:PublishReadyToRun=false
if ($LASTEXITCODE -ne 0) { throw "BS IME Assistant publish failed ($LASTEXITCODE)" }

Write-Host "=== Publish AutoCAD IME bridge ===" -ForegroundColor Cyan
dotnet publish $CadBridgeProject --configuration $Configuration --output $CadBridgePublish
if ($LASTEXITCODE -ne 0) { throw "AutoCAD IME bridge publish failed ($LASTEXITCODE)" }

Copy-Item -LiteralPath $CadBridgeManifest -Destination (Join-Path $CadBridgeStage "PackageContents.xml") -Force
Copy-Item -LiteralPath (Join-Path $CadBridgePublish "BS.IME.Assistant.CadBridge.dll") -Destination (Join-Path $CadBridgeContents "BS.IME.Assistant.CadBridge.dll") -Force
if (Test-Path (Join-Path $CadBridgePublish "BS.IME.Assistant.CadBridge.pdb")) {
    Copy-Item -LiteralPath (Join-Path $CadBridgePublish "BS.IME.Assistant.CadBridge.pdb") -Destination (Join-Path $CadBridgeContents "BS.IME.Assistant.CadBridge.pdb") -Force
}
Remove-Item -LiteralPath $CadBridgePublish -Recurse -Force

$IssPath = Join-Path $InstallerRoot "BS-IME-Assistant-Setup.iss"
$IsccCandidateRoots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$IsccCandidates = @($IsccCandidateRoots | ForEach-Object {
    Join-Path $_ "Inno Setup 6\ISCC.exe"
} | Where-Object { Test-Path -LiteralPath $_ })

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

