using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

internal static class Program
{
    private const string AppId = "BS-IME-Assistant";
    private const string AppName = "BS IME Assistant";
    private const string AppVersion = "1.0.0";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BS-IME-Assistant";

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string RoamingAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string AppDir => Path.Combine(LocalAppData, "BS-IME-Assistant");

    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                return Uninstall();
            }

            return Install();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Operation failed:");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            Console.WriteLine("Press Enter to close.");
            Console.ReadLine();
            return 1;
        }
    }

    private static int Install()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "BS-IME-Assistant-Setup-" + Guid.NewGuid().ToString("N"));

        try
        {
            Console.WriteLine("BS IME Assistant installer");
            Console.WriteLine("Installing for current user...");

            Directory.CreateDirectory(tempRoot);
            var payloadZip = Path.Combine(tempRoot, "Payload.zip");
            ExtractPayload(payloadZip);
            ZipFile.ExtractToDirectory(payloadZip, tempRoot, overwriteFiles: true);

            var payloadRoot = Path.Combine(tempRoot, "stage");
            var appSource = Path.Combine(payloadRoot, "app");
            var cadSource = Path.Combine(payloadRoot, "cad", "BS-CAD-Tools.bundle");
            var maxSource = Path.Combine(payloadRoot, "max");

            if (!Directory.Exists(appSource)) throw new DirectoryNotFoundException(appSource);

            StopAssistantIfRunning();

            CopyDirectory(appSource, AppDir);
            Console.WriteLine($"Installed assistant: {AppDir}");

            RegisterStartup();
            Console.WriteLine("Registered Windows startup for current user.");

            InstallUninstaller();
            RegisterWindowsUninstallEntry();
            Console.WriteLine("Registered Windows uninstall entry.");

            if (Directory.Exists(cadSource))
            {
                var cadDest = GetCadBundleDir();
                CopyDirectory(cadSource, cadDest);
                Console.WriteLine($"Installed AutoCAD bundle: {cadDest}");
            }
            else
            {
                Console.WriteLine("AutoCAD bundle payload not found, skipped.");
            }

            Install3dsMaxStartup(maxSource);

            LaunchAssistant();
            Console.WriteLine("Done.");
            Console.WriteLine("You can uninstall later from Windows Settings > Apps, or run BS-IME-Assistant-Uninstall.exe.");
            Console.WriteLine("You can close this window.");
            return 0;
        }
        finally
        {
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static int Uninstall()
    {
        Console.WriteLine("BS IME Assistant uninstaller");
        Console.WriteLine("Uninstalling for current user...");

        StopAssistantIfRunning();
        RemoveStartup();
        RemoveWindowsUninstallEntry();
        RemoveCadBundle();
        Remove3dsMaxStartup();
        ScheduleAppDirectoryRemoval();

        Console.WriteLine("Uninstall cleanup scheduled.");
        Console.WriteLine("You can close this window.");
        return 0;
    }

    private static void ExtractPayload(string destination)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip");
        if (stream == null) throw new InvalidOperationException("Embedded payload not found.");
        using var file = File.Create(destination);
        stream.CopyTo(file);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var sourcePath in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, sourcePath);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, sourcePath);
            var destinationPath = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void RegisterStartup()
    {
        var exe = Path.Combine(AppDir, "BS.IME.Assistant.exe");
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        key?.SetValue(AppId, $"\"{exe}\"");
    }

    private static void RemoveStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue(AppId, throwOnMissingValue: false);
    }

    private static void InstallUninstaller()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe)) return;

        var uninstallExe = GetUninstallerPath();
        Directory.CreateDirectory(Path.GetDirectoryName(uninstallExe)!);
        File.Copy(currentExe, uninstallExe, overwrite: true);
    }

    private static void RegisterWindowsUninstallEntry()
    {
        var uninstallExe = GetUninstallerPath();
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
        key?.SetValue("DisplayName", AppName);
        key?.SetValue("DisplayVersion", AppVersion);
        key?.SetValue("Publisher", "White Dimension");
        key?.SetValue("InstallLocation", AppDir);
        key?.SetValue("DisplayIcon", Path.Combine(AppDir, "BS.IME.Assistant.exe"));
        key?.SetValue("UninstallString", $"\"{uninstallExe}\" --uninstall");
        key?.SetValue("QuietUninstallString", $"\"{uninstallExe}\" --uninstall");
        key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void RemoveWindowsUninstallEntry()
    {
        using var parent = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
        parent?.DeleteSubKeyTree(AppId, throwOnMissingSubKey: false);
    }

    private static void Install3dsMaxStartup(string maxSource)
    {
        var startupSource = Path.Combine(maxSource, "startup", "BS_IME_Startup.ms");
        if (!File.Exists(startupSource))
        {
            Console.WriteLine("3ds Max startup payload not found, skipped.");
            return;
        }

        var maxUserRoot = GetMaxUserRoot();
        if (!Directory.Exists(maxUserRoot))
        {
            Console.WriteLine("3ds Max user folder not found, skipped.");
            return;
        }

        var count = 0;
        foreach (var versionDir in Directory.EnumerateDirectories(maxUserRoot))
        {
            var startupDir = Path.Combine(versionDir, "ENU", "scripts", "startup");
            Directory.CreateDirectory(startupDir);
            File.Copy(startupSource, Path.Combine(startupDir, "BS_IME_Startup.ms"), overwrite: true);
            count++;
        }

        Console.WriteLine(count == 0
            ? "No 3ds Max version folders found, skipped."
            : $"Installed 3ds Max startup bridge for {count} version folder(s).");
    }

    private static void RemoveCadBundle()
    {
        var cadBundle = GetCadBundleDir();
        if (Directory.Exists(cadBundle))
        {
            Directory.Delete(cadBundle, recursive: true);
            Console.WriteLine($"Removed AutoCAD bundle: {cadBundle}");
        }
    }

    private static void Remove3dsMaxStartup()
    {
        var maxUserRoot = GetMaxUserRoot();
        if (!Directory.Exists(maxUserRoot)) return;

        var count = 0;
        foreach (var versionDir in Directory.EnumerateDirectories(maxUserRoot))
        {
            var startupFile = Path.Combine(versionDir, "ENU", "scripts", "startup", "BS_IME_Startup.ms");
            if (File.Exists(startupFile))
            {
                File.Delete(startupFile);
                count++;
            }
        }

        if (count > 0) Console.WriteLine($"Removed 3ds Max startup bridge from {count} version folder(s).");
    }

    private static void ScheduleAppDirectoryRemoval()
    {
        var appDir = AppDir;
        if (!Directory.Exists(appDir)) return;

        var command = $"Start-Sleep -Seconds 2; Remove-Item -LiteralPath '{EscapePowerShellSingleQuoted(appDir)}' -Recurse -Force -ErrorAction SilentlyContinue";
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command " + QuoteArgument(command),
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void StopAssistantIfRunning()
    {
        var currentId = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("BS.IME.Assistant"))
        {
            if (process.Id == currentId) continue;
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(1500)) process.Kill(entireProcessTree: true);
            }
            catch { }
        }
    }

    private static void LaunchAssistant()
    {
        var exe = Path.Combine(AppDir, "BS.IME.Assistant.exe");
        if (!File.Exists(exe)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                WorkingDirectory = AppDir
            });
        }
        catch { }
    }

    private static string GetCadBundleDir()
    {
        return Path.Combine(RoamingAppData, "Autodesk", "ApplicationPlugins", "BS-CAD-Tools.bundle");
    }

    private static string GetMaxUserRoot()
    {
        return Path.Combine(LocalAppData, "Autodesk", "3dsMax");
    }

    private static string GetUninstallerPath()
    {
        return Path.Combine(AppDir, "BS-IME-Assistant-Uninstall.exe");
    }

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return value.Replace("'", "''");
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
