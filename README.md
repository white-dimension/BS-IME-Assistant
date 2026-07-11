# BS IME Assistant

BS IME Assistant 是面向 AutoCAD 和 3ds Max 的 Windows 输入法切换助手。程序常驻系统托盘，根据当前软件、窗口和焦点控件自动切换中文或英文输入法，并通过悬浮胶囊显示当前状态。3ds Max 完全独立识别；AutoCAD 使用安装包内置的极小识别组件补充内部文字命令状态。

当前软件规则只保留：

- AutoCAD：`acad.exe`
- 3ds Max：`3dsmax.exe`

## 主要功能

- AutoCAD 和 3ds Max 激活时自动切换到指定输入法。
- 内置 AutoCAD 焦点控件识别，并由轻量 CadBridge 补充 AutoCAD 内部文字命令状态。
- 内置 3ds Max 焦点控件识别，区分文本输入、数字参数与普通操作。
- 托盘菜单支持暂停/启用自动切换、手动切换中英文、打开设置、显示或隐藏悬浮胶囊、重置胶囊位置、重启和退出。
- 设置页面支持选择中英文输入法、开机启动、软件规则和悬浮胶囊参数。
- 悬浮胶囊支持拖动、置顶、位置记忆和屏幕边界校正。
- 单实例运行，重复启动不会创建多个托盘程序。
- 自动保存设置并记录运行日志，日志超过 3 MB 后轮转。

## 系统要求

- Windows 10 或 Windows 11，64 位。
- 源码构建需要 .NET 8 SDK。
- 安装包构建需要 Inno Setup 6。

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

## AutoCAD 识别

程序通过 Windows UI Automation 检查 AutoCAD 当前焦点控件。普通绘图、数字输入、搜索框和命令行保持英文；检测到可编辑文字控件时切换中文，离开后恢复英文。

安装包还会部署 `BS-IME-Assistant-CadBridge.bundle`。它只读取 AutoCAD 的 `CMDNAMES` 和文字编辑命令事件，通过当前用户命名管道通知主程序，不包含命令、面板、图层、字体、清理或标准工具。没有加载该组件时，主程序仍会使用独立焦点识别作为后备。

## 3ds Max 集成

3ds Max 识别完全内置在主程序中，通过 Windows UI Automation 判断焦点输入控件。普通视口、停靠面板和滚动区域不会仅因暴露 `ValuePattern` 就被当作文本输入框。

## 构建安装包

安装 Inno Setup 6 后执行：

```powershell
.\installer\build_setup.ps1
```

生成的安装包位于：

```text
installer\dist\BS-IME-Assistant-Setup.exe
```

安装程序按当前用户安装，不需要管理员权限，可部署主程序、Windows 开机启动项和极小的 AutoCAD CadBridge。卸载可通过 Windows“设置 > 应用”完成。

## 安装位置

正式安装后的主程序目录：

```text
%LOCALAPPDATA%\BS-IME-Assistant
```

安装包会同时部署极小的 AutoCAD CadBridge；不会向 3ds Max 目录写入任何启动脚本。

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
- AutoCAD CadBridge 当前面向 AutoCAD 2027；其他版本仍可使用主程序的焦点识别后备。
- AutoCAD 和 3ds Max 的部分自定义控件不完全遵循标准 UI Automation，无法保证识别每一种原位文字编辑器。

## 项目结构

```text
src/BS.IME.Assistant/            WPF 主程序
src/BS.IME.Assistant.CadBridge/  极小的 AutoCAD 文字编辑识别组件
installer/                       安装包脚本、Inno Setup 配置和引导程序
```
