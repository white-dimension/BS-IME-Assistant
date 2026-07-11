using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BS.IME.Assistant.CadBridge;

public sealed class CadBridgeApp : IExtensionApplication
{
    private bool _textInputActive;

    public void Initialize()
    {
        AcadApp.DocumentManager.DocumentLockModeChanged += OnDocumentLockModeChanged;
        AcadApp.Idle += OnIdle;
        CadImeNotifier.Notify("PluginReady", "en", "CAD IME 识别组件已连接");
    }

    public void Terminate()
    {
        AcadApp.DocumentManager.DocumentLockModeChanged -= OnDocumentLockModeChanged;
        AcadApp.Idle -= OnIdle;
        CadImeNotifier.Notify("PluginDisconnected", "en", "CAD IME 识别组件已断开");
    }

    private void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
    {
        var command = NormalizeCommand(e.GlobalCommandName);
        if (!NeedsChineseInput(command))
        {
            return;
        }

        _textInputActive = true;
        CadImeNotifier.Notify("TextEditStarted", "zh", GetInputMode(command));
    }

    private void OnIdle(object? sender, EventArgs e)
    {
        if (!_textInputActive)
        {
            return;
        }

        var activeCommands = AcadApp.GetSystemVariable("CMDNAMES")?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(activeCommands))
        {
            return;
        }

        _textInputActive = false;
        CadImeNotifier.Notify("TextEditEnded", "en", "CAD 命令模式");
    }

    private static string NormalizeCommand(string? command) =>
        (command ?? "").Trim().TrimStart('_', '.', '\'', '-').ToUpperInvariant();

    private static bool NeedsChineseInput(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        return command is "T" or "TEXT" or "DTEXT" or "MTEXT" or "MTEDIT" or "DDEDIT" or "ED" or "TEXTEDIT"
            or "ATTEDIT" or "ATTIPEDIT" or "EATTEDIT" or "DDATTE" or "ATTDEF"
            or "MLEADER" or "MLEADEREDIT"
            or "TABLE" or "TABLEDIT" or "FIELD"
            or "DIMEDIT" or "DIMTEDIT"
            or "RENAME" or "BLOCK" or "WBLOCK" or "BEDIT" or "REFEDIT"
            || command.Contains("TEXT", StringComparison.Ordinal)
            || command.Contains("ATTRIB", StringComparison.Ordinal)
            || command.Contains("MLEADER", StringComparison.Ordinal);
    }

    private static string GetInputMode(string command)
    {
        if (command.Contains("ATT", StringComparison.Ordinal)) return "CAD 块属性编辑";
        if (command.Contains("MLEADER", StringComparison.Ordinal)) return "CAD 多重引线编辑";
        if (command.Contains("TABLE", StringComparison.Ordinal) || command.Contains("FIELD", StringComparison.Ordinal)) return "CAD 表格/字段编辑";
        if (command.Contains("DIM", StringComparison.Ordinal)) return "CAD 标注文字编辑";
        if (command is "RENAME" or "BLOCK" or "WBLOCK" or "BEDIT" or "REFEDIT") return "CAD 名称编辑";
        return "CAD 文字编辑";
    }
}
