# BS IME Assistant Installer

This folder builds a per-user Windows installer for BS IME Assistant and its CAD/3ds Max integrations.

The installer does four things:

1. Installs BS IME Assistant under `%LOCALAPPDATA%\BS-IME-Assistant`.
2. Optionally registers BS IME Assistant to start with Windows under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
3. Optionally installs the AutoCAD ApplicationPlugin bundle under `%APPDATA%\Autodesk\ApplicationPlugins\BS-CAD-Tools.bundle`.
4. Optionally installs the 3ds Max startup loader into detected `%LOCALAPPDATA%\Autodesk\3dsMax\<version>\ENU\scripts\startup` folders.

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
- Paths use `%LOCALAPPDATA%`, `%APPDATA%`, and AutoCAD/3ds Max standard user plugin folders.
- The source projects can live under any parent directory as long as the four sibling repositories stay together.
