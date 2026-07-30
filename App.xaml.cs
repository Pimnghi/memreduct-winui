using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace memreduct_winui;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    [DllImport("kernel32")]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
    private const string CliPipePrefix = "--cli-pipe=";

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var commandArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var pipeArguments = commandArgs.Where(IsCliPipeArgument).ToArray();
        var helpArguments = commandArgs.Where(IsCliHelpArgument).ToArray();
        var pipeName = pipeArguments.Length == 1
            ? GetCliPipeName(pipeArguments[0])
            : null;
        var publicArguments = commandArgs
            .Where(arg => !IsCliPipeArgument(arg) && !IsCliHelpArgument(arg))
            .ToArray();
        var isCliHelp = helpArguments.Length == 1;
        var isClean = publicArguments.Any(IsCleanArgument);
        var isFullClean = publicArguments.Any(IsFullCleanArgument);
        var isAutostart = publicArguments.Any(IsAutostartArgument);
        var hasInvalidArguments = publicArguments.Any(arg =>
            !IsCleanArgument(arg) && !IsFullCleanArgument(arg) && !IsAutostartArgument(arg));
        var hasInvalidPipeArgument = pipeArguments.Length > 1
            || (pipeArguments.Length == 1 && pipeName == null)
            || (pipeName != null && !isClean && !isCliHelp);

        if (isCliHelp
            && helpArguments.Length == 1
            && publicArguments.Length == 0
            && !hasInvalidPipeArgument)
        {
            ExitCommandLine(pipeName, FormatCommandLineHelp(), 0, isError: false);
            return;
        }

        if (hasInvalidArguments
            || hasInvalidPipeArgument
            || helpArguments.Length > 0
            || (isClean && isAutostart))
        {
            ExitCommandLine(
                pipeName,
                MemReduct.Core.CoreService.GetString(MemReduct.Core.StrId.CommandLineUsage)
                    ?? "Usage: memreduct-winui.exe [-clean|-clean:full|-autostart]",
                2,
                isError: true);
            return;
        }

        if (isClean)
        {
#if !DEBUG
            if (!MemReduct.Core.CoreService.IsElevated())
            {
                if (pipeName == null)
                    AttachConsole(ATTACH_PARENT_PROCESS);
                Environment.Exit(RunAsAdmin(commandArgs, waitForExit: true));
                return;
            }
#endif

            var mask = isFullClean
                ? MemReduct.Core.MemoryMask.All
                : MemReduct.Core.IniConfig.ReadUInt("ReductMask2", MemReduct.Core.MemoryMask.Default);
            var result = System.Threading.Tasks.Task
                .Run(() => MemReduct.Core.CleanupCoordinator.CleanAsync(
                    MemReduct.Core.CleanupSource.CommandLine,
                    mask))
                .GetAwaiter()
                .GetResult();

            if (result?.Status == MemReduct.Core.CleanupStatus.Success)
            {
                ExitCommandLine(
                    pipeName,
                    MemReduct.Core.CoreService.FormatCleanedMessage(result.FreedFormatted),
                    0,
                    isError: false);
            }
            else
            {
                string message;
                if (result?.Status == MemReduct.Core.CleanupStatus.PartialSuccess)
                {
                    message = MemReduct.Core.CoreService.FormatPartialCleanupMessage(result);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(result?.ErrorMessage))
                    {
                        message = result.ErrorMessage;
                    }
                    else
                    {
                        var failedMask = result?.FailedMask ?? mask;
                        var title = MemReduct.Core.CoreService.GetString(
                            MemReduct.Core.StrId.CleaningFailed) ?? "Memory cleaning failed";
                        message =
                            $"{title}. {MemReduct.Core.CoreService.FormatFailedAreasMessage(failedMask)}";
                    }
                }
                ExitCommandLine(pipeName, message, 1, isError: true);
            }
            return;
        }

#if !DEBUG
        if (!MemReduct.Core.CoreService.IsElevated())
        {
            Environment.Exit(RunAsAdmin(commandArgs, waitForExit: false));
            return;
        }
#endif

        var appInstance = AppInstance.FindOrRegisterForKey("memreduct_winui_instance");
        if (!appInstance.IsCurrent)
        {
            appInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs()).AsTask().Wait();
            Environment.Exit(0);
            return;
        }

        MemReduct.Core.ToastService.Initialize();
        MemReduct.Core.TrayIcon.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        MemReduct.Core.TrayIcon.Create("Mem Reduct WinUI");

        MainWindow = new MainWindow();
        MainWindow.Activate();

        ApplySavedTheme();

        if (MemReduct.Core.IniConfig.ReadBool("IsStartMinimized") || isAutostart)
            MainWindow.AppWindow.Hide();

        MemReduct.Core.AutoCleanService.Refresh();
        MemReduct.Core.AutoStartService.EnsureConfigured();

        appInstance.Activated += (s, e) =>
        {
            MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (MainWindow is MainWindow w && w.AppWindow != null)
                    w.AppWindow.Show(true);
            });
        };
    }

    public static void ApplyTheme(string? theme)
    {
        if (theme == null) theme = MemReduct.Core.IniConfig.ReadString("Theme", "System") ?? "System";
        var value = theme == "Dark" ? ElementTheme.Dark :
                    theme == "Light" ? ElementTheme.Light : ElementTheme.Default;

        if (MainWindow?.Content is FrameworkElement fe)
            fe.RequestedTheme = value;
    }

    private static void ApplySavedTheme()
    {
        ApplyTheme(null);
    }

    private static bool IsCleanArgument(string value) =>
        value.Equals("-clean", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/clean", StringComparison.OrdinalIgnoreCase)
        || IsFullCleanArgument(value);

    private static bool IsFullCleanArgument(string value) =>
        value.Equals("-clean:full", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/clean:full", StringComparison.OrdinalIgnoreCase);

    private static bool IsAutostartArgument(string value) =>
        value.Equals("-autostart", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/autostart", StringComparison.OrdinalIgnoreCase);

    private static bool IsCliPipeArgument(string value) =>
        value.StartsWith(CliPipePrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsCliHelpArgument(string value) =>
        value.Equals("--cli-help", StringComparison.OrdinalIgnoreCase);

    private static string FormatCommandLineHelp()
    {
        var title = MemReduct.Core.CoreService.GetString(
            MemReduct.Core.StrId.CommandLineHelpTitle)
            ?? "Mem Reduct WinUI command-line tool";
        var showHelp = MemReduct.Core.CoreService.GetString(
            MemReduct.Core.StrId.CommandLineHelpShow)
            ?? "Show this help information.";
        var clean = MemReduct.Core.CoreService.GetString(
            MemReduct.Core.StrId.CommandLineHelpClean)
            ?? "Clean memory using the areas selected in the current configuration.";
        var cleanFull = MemReduct.Core.CoreService.GetString(
            MemReduct.Core.StrId.CommandLineHelpFull)
            ?? "Clean all memory areas.";

        return $"""
            {title}

              mrw-cli
                  {showHelp}

              mrw-cli -clean
                  {clean}

              mrw-cli -clean:full
                  {cleanFull}
            """;
    }

    private static string? GetCliPipeName(string value)
    {
        var pipeName = value[CliPipePrefix.Length..];
        if (pipeName.Length is 0 or > 128)
            return null;

        foreach (var character in pipeName)
        {
            var isAsciiLetterOrDigit =
                character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9';
            if (!isAsciiLetterOrDigit && character is not '.' and not '-' and not '_')
                return null;
        }
        return pipeName;
    }

    private static void ExitCommandLine(
        string? pipeName,
        string message,
        int exitCode,
        bool isError)
    {
        if (!WriteCommandLineResult(pipeName, message, isError))
            exitCode = 2;

        Environment.Exit(exitCode);
    }

    private static bool WriteCommandLineResult(
        string? pipeName,
        string message,
        bool isError)
    {
        if (pipeName == null)
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            if (isError)
                Console.Error.WriteLine(message);
            else
                Console.WriteLine(message);
            return true;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.None);
            pipe.Connect(15000);
            using var writer = new StreamWriter(
                pipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: false);
            writer.WriteLine(message);
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int RunAsAdmin(string[] arguments, bool waitForExit)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return 2;

        try
        {
            var startInfo = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return 2;

            if (waitForExit)
            {
                process.WaitForExit();
                return process.ExitCode;
            }

            return 0;
        }
        catch
        {
            return 2;
        }
    }
}
