# BS-IME-Assistant

BS-IME-Assistant 是一个独立 Windows 常驻输入法助手，用于设计软件场景下自动管理中英文输入法。当前版本只做最小稳定能力：托盘常驻、识别前台设计软件、自动切换默认输入法、全局快捷键手动切换、配置文件和日志。

本项目不集成 BS OS，不实现 AutoCAD 插件，不引用 Autodesk.AutoCAD.*，不实现 3ds Max 插件，也不引用 3ds Max SDK。

## 如何运行

```powershell
dotnet build .\src\BS.IME.Assistant\BS.IME.Assistant.csproj
dotnet run --project .\src\BS.IME.Assistant\BS.IME.Assistant.csproj
```

启动后程序不会弹出主窗口，会常驻系统托盘。右键托盘图标可以暂停自动切换、手动切换中英文、打开配置文件、打开日志目录或退出。

## 配置文件

配置文件位置：

```text
%APPDATA%\BS-IME-Assistant\settings.json
```

如果文件不存在，程序会自动创建默认配置。如果 `targetChineseHkl` 或 `targetEnglishHkl` 为空，程序会根据系统当前可用键盘布局自动选择一个合理默认值，并写回配置文件。

## 日志

日志位置：

```text
%APPDATA%\BS-IME-Assistant\logs\debug.log
```

日志超过 3MB 后会轮转为：

```text
%APPDATA%\BS-IME-Assistant\logs\debug.old.log
```

## 默认支持软件

- `acad.exe`
- `3dsmax.exe`
- `SketchUp.exe`
- `Rhino.exe`
- `Revit.exe`
- `Photoshop.exe`

当这些进程成为前台窗口，并且对应配置的 `switchOnActivate` 为 `true` 时，会切换到该 profile 的 `defaultIme`。当前阶段 `defaultIme` 支持：

- `en`
- `zh`

## 快捷键

默认全局快捷键：

- `Ctrl+Alt+E`：切换到英文输入法
- `Ctrl+Alt+C`：切换到中文输入法

如果热键被其他软件占用，程序会继续运行，并在日志和托盘菜单中提示热键注册失败。

## 当前阶段限制

- 不做复杂 UI，只提供托盘菜单和一个最小状态窗口。
- 不做复杂插件架构。
- 不接 BS OS。
- 不接 AutoCAD API。
- 不接 3ds Max SDK。
- 输入法检测基于 Windows 当前可用键盘布局，部分第三方输入法的 HKL/描述可能因系统环境不同而不同。
- 自动切换通过向当前前台窗口发送 `WM_INPUTLANGCHANGEREQUEST`，少数高权限窗口或特殊程序可能拒绝处理。
