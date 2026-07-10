# BS IME Assistant

BS IME Assistant 是面向 AutoCAD 和 3ds Max 的 Windows 输入法切换助手。程序常驻系统托盘，根据当前软件、窗口和文本编辑状态自动切换中文或英文输入法，并通过悬浮胶囊显示当前状态。

当前软件规则只保留：

- AutoCAD：`acad.exe`
- 3ds Max：`3dsmax.exe`

## 主要功能

- AutoCAD 和 3ds Max 激活时自动切换到指定输入法。
- AutoCAD 插件通过命名管道发送文字编辑状态，实现更准确的中英文切换。
- 3ds Max 启动脚本与焦点控件识别配合，区分文本输入与普通操作。
- 托盘菜单支持暂停/启用自动切换、手动切换中英文、打开设置、显示或隐藏悬浮胶囊、重置胶囊位置、重启和退出。
- 设置页面支持选择中英文输入法、开机启动、软件规则、AutoCAD 增强识别和悬浮胶囊参数。
- 悬浮胶囊支持拖动、置顶、位置记忆和屏幕边界校正。
- 单实例运行，重复启动不会创建多个托盘程序。
- 自动保存设置并记录运行日志，日志超过 3 MB 后轮转。

## 系统要求

- Windows 10 或 Windows 11，64 位。
- 源码构建需要 .NET 8 SDK。
- 安装包构建需要 Inno Setup 6。
- AutoCAD/3ds Max 增强识别需要对应桥接文件正确安装。

## 快速运行

在仓库根目录执行：

```powershell
dotnet build .\src\BS.IME.Assistant\BS.IME.Assistant.csproj
dotnet run --project .\src\BS.IME.Assistant\BS.IME.Assistant.csproj
```

Debug 编译结果位于：

```text
src\BS.IME.Assistant\bin\Debug\net8.0-windows\BS.IME.Assistant.exe
```

程序启动后不会显示传统主窗口，而是进入系统托盘并显示悬浮状态胶囊。右键托盘图标可打开操作菜单和设置页面。

## 设置

设置文件位于：

```text
%APPDATA%\BS-IME-Assistant\settings.json
```

首次启动时程序会自动创建设置文件，并从 Windows 已安装的键盘布局中选择合适的中文和英文输入法。设置页面保存后立即应用；开机启动选项写入当前用户的 Windows 启动项。

旧设置中的其他软件规则会在加载时自动清理。如果 AutoCAD 或 3ds Max 规则缺失，程序会自动补齐。

## AutoCAD 集成

AutoCAD 增强识别由 `BS-CAD-Tools` 插件提供。插件将文字编辑事件发送到本程序的命名管道：

```text
BS_IME_Assistant_Pipe
```

检测到 AutoCAD 后，程序可提示启用增强识别。启用后，进入文字编辑时切换中文，离开编辑状态时恢复英文。未连接插件时，仍会执行软件激活规则。

## 3ds Max 集成

3ds Max 集成由 `BS-3dsMax-Tools` 中的启动脚本和桥接脚本提供。安装程序会把启动脚本部署到已检测到的用户目录：

```text
%LOCALAPPDATA%\Autodesk\3dsMax\<版本>\ENU\scripts\startup
```

程序结合桥接事件和 Windows UI Automation 焦点信息识别输入控件。普通视口、停靠面板和滚动区域不会仅因暴露 `ValuePattern` 就被当作文本输入框。

## 构建安装包

完整安装包依赖以下三个同级仓库：

```text
D:\BS_OS\BS-IME-Assistant
D:\BS_OS\BS-CAD-Tools
D:\BS_OS\BS-3dsMax-Tools
```

安装 Inno Setup 6 后执行：

```powershell
.\installer\build_setup.ps1
```

生成的安装包位于：

```text
installer\dist\BS-IME-Assistant-Setup.exe
```

安装程序按当前用户安装，不需要管理员权限，可部署主程序、Windows 开机启动项、AutoCAD ApplicationPlugin 和 3ds Max 启动桥接。卸载可通过 Windows“设置 > 应用”完成。

## 安装位置

正式安装后的主程序目录：

```text
%LOCALAPPDATA%\BS-IME-Assistant
```

AutoCAD 插件默认安装到：

```text
%APPDATA%\Autodesk\ApplicationPlugins\BS-CAD-Tools.bundle
```

## 日志与排查

运行日志位于：

```text
%APPDATA%\BS-IME-Assistant\logs\debug.log
```

日志轮转文件：

```text
%APPDATA%\BS-IME-Assistant\logs\debug.old.log
```

遇到切换异常时，建议保留问题发生前后的日志，并记录当时的软件、窗口或输入框位置。设置文件损坏时，程序会尝试备份为 `settings.broken.json`，然后恢复默认设置。

## 当前限制

- 仅维护 AutoCAD 和 3ds Max 两条软件规则。
- 输入法切换依赖 Windows 窗口消息和当前用户安装的键盘布局，部分高权限窗口可能拒绝普通权限程序发送的切换请求。
- 第三方输入法显示名称和 HKL 会随系统环境不同而变化。
- 3ds Max 的部分自定义控件不完全遵循标准 UI Automation，仍需结合真实操作日志持续校准。

## 项目结构

```text
src/BS.IME.Assistant/   WPF 主程序
installer/              安装包脚本、Inno Setup 配置和引导程序
```
