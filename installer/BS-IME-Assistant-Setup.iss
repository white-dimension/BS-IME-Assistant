#define AppName "BS IME Assistant"
#define AppVersion "1.0.0"
#define Publisher "White Dimension"

[Setup]
AppId={{9C795D77-5B41-4F16-84F2-6F394498640D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={localappdata}\BS-IME-Assistant
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=dist
OutputBaseFilename=BS-IME-Assistant-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\BS.IME.Assistant.exe

[Tasks]
Name: "autostart"; Description: "Start BS IME Assistant when Windows starts"; Flags: checkedonce
Name: "cad"; Description: "Install AutoCAD ApplicationPlugin bundle for current user"; Flags: checkedonce
Name: "max"; Description: "Install 3ds Max startup bridge for current user"; Flags: checkedonce

[Files]
Source: "stage\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "stage\cad\BS-CAD-Tools.bundle\*"; DestDir: "{userappdata}\Autodesk\ApplicationPlugins\BS-CAD-Tools.bundle"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: cad
Source: "stage\max\*"; DestDir: "{app}\3dsMax"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: max
Source: "scripts\Install-3dsMaxStartup.ps1"; DestDir: "{app}\install"; Flags: ignoreversion
Source: "scripts\Uninstall-3dsMaxStartup.ps1"; DestDir: "{app}\install"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BS-IME-Assistant"; ValueData: """{app}\BS.IME.Assistant.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\install\Install-3dsMaxStartup.ps1"" -AppRoot ""{app}"""; Flags: runhidden; Tasks: max
Filename: "{app}\BS.IME.Assistant.exe"; Description: "Launch BS IME Assistant"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\install\Uninstall-3dsMaxStartup.ps1"""; Flags: runhidden

[Icons]
Name: "{autoprograms}\BS IME Assistant"; Filename: "{app}\BS.IME.Assistant.exe"
