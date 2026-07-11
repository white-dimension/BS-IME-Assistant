# BS IME Assistant Installer

This folder builds a per-user Windows installer for BS IME Assistant and its minimal AutoCAD IME recognition bridge.

The installer does three things:

1. Installs BS IME Assistant under `%LOCALAPPDATA%\BS-IME-Assistant`.
2. Optionally registers BS IME Assistant to start with Windows under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
3. Installs `BS-IME-Assistant-CadBridge.bundle` under the current user's AutoCAD ApplicationPlugins folder.

On upgrade, the installer removes the old full `BS-CAD-Tools.bundle` and 3ds Max startup bridge. The replacement AutoCAD bundle only reports text-edit state to the assistant and contains no CAD tool commands or UI.

## Build

Install Inno Setup 6 first, then run from this folder:

```powershell
.\build_setup.ps1
```

The setup EXE will be created at:

```text
installer\dist\BS-IME-Assistant-Setup.exe
```

If Inno Setup is not installed, the script still prepares `installer\stage` and tells you what is missing.

## Notes

- The installer is per-user and does not require administrator rights.
- The installer only depends on this repository.
- No 3ds Max startup script is installed.
- The AutoCAD component is a minimal auto-loaded bridge, not the former BS-CAD-Tools plugin.
