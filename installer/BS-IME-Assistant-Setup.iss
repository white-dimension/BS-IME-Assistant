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
DefaultGroupName=BS IME Assistant
WizardStyle=modern
CloseApplications=force
UninstallDisplayIcon={app}\BS.IME.Assistant.exe

[Tasks]
Name: "autostart"; Description: "Start BS IME Assistant when Windows starts"; Flags: checkablealone

[Files]
Source: "stage\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "stage\cad\BS-IME-Assistant-CadBridge.bundle\*"; DestDir: "{userappdata}\Autodesk\ApplicationPlugins\BS-IME-Assistant-CadBridge.bundle"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: filesandordirs; Name: "{app}\3dsMax"
Type: filesandordirs; Name: "{app}\install"
Type: filesandordirs; Name: "{userappdata}\Autodesk\ApplicationPlugins\BS-CAD-Tools.bundle"
Type: files; Name: "{localappdata}\Autodesk\3dsMax\*\ENU\scripts\startup\BS_IME_Startup.ms"
Type: files; Name: "{localappdata}\Autodesk\3dsMax\*\ENU\scripts\startup\BS_IME_Bridge.ms"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BS-IME-Assistant"; ValueData: """{app}\BS.IME.Assistant.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\BS.IME.Assistant.exe"; Description: "Launch BS IME Assistant"; Flags: nowait postinstall skipifsilent

[Icons]
Name: "{autoprograms}\BS IME Assistant"; Filename: "{app}\BS.IME.Assistant.exe"
