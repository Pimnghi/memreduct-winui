using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MemReduct.Core;

public static class ToastService
{
    private const string AppUserModelId = "Pimnghi.MemReductWinUI";
    private const string AppDisplayName = "Mem Reduct WinUI";
    private const string IdentityVersionKey = "NotificationIdentityVersion";
    private const int IdentityVersion = 2;
    private static bool _registered;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        string appId);

    public static void Initialize()
    {
        if (_registered)
            return;

        try
        {
            var iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "AppIcon.ico");
            if (!File.Exists(iconPath))
                return;

            if (!ConfigureNotificationIdentity(iconPath))
                return;

            var manager = AppNotificationManager.Default;
            if (!AppNotificationManager.IsSupported())
                return;

            if (IniConfig.ReadInt(IdentityVersionKey) < IdentityVersion)
                manager.UnregisterAll();

            manager.NotificationInvoked += OnNotificationInvoked;
            manager.Register(AppDisplayName, new Uri(iconPath, UriKind.Absolute));
            IniConfig.WriteInt(IdentityVersionKey, IdentityVersion);
            _registered = true;
        }
        catch
        {
            _registered = false;
        }
    }

    private static bool ConfigureNotificationIdentity(string iconPath)
    {
        if (SetCurrentProcessExplicitAppUserModelID(AppUserModelId) < 0)
            return false;

        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\AppUserModelId\{AppUserModelId}");
        if (key == null)
            return false;

        key.SetValue("DisplayName", AppDisplayName, RegistryValueKind.String);
        key.SetValue("IconUri", iconPath, RegistryValueKind.String);
        key.SetValue("IconBackgroundColor", "00000000", RegistryValueKind.String);
        return true;
    }

    public static void Shutdown()
    {
        if (!_registered)
            return;

        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked -= OnNotificationInvoked;
            manager.Unregister();
        }
        catch
        {
            // The process is exiting, so there is no useful recovery action.
        }
        finally
        {
            _registered = false;
        }
    }

    public static void ShowCleanResult(CleanupResult result)
    {
        var noSound = !IniConfig.ReadBool("IsNotificationsSound", true);
        var title = CoreService.GetString(StrId.CleanMemory) ?? "Memory cleaned";
        string msg;
        if (result.Status == CleanupStatus.PartialSuccess)
        {
            msg = CoreService.FormatPartialCleanupMessage(result);
        }
        else
        {
            msg = CoreService.FormatCleanedMessage(result.FreedFormatted);
        }

        if (_registered)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(msg);
                if (noSound)
                    builder.MuteAudio();

                AppNotificationManager.Default.Show(builder.BuildNotification());
                return;
            }
            catch
            {
                // Fall back to the tray balloon on systems without app notification support.
            }
        }

        TrayIcon.ShowBalloon(AppDisplayName, msg, noSound);
    }

    private static void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
    }
}
