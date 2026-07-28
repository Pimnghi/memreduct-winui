using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.IO;

namespace MemReduct.Core;

public static class ToastService
{
    private const string AppDisplayName = "Mem Reduct WinUI";
    private static bool _registered;

    public static void Initialize()
    {
        if (_registered)
            return;

        try
        {
            var manager = AppNotificationManager.Default;
            if (!AppNotificationManager.IsSupported())
                return;

            var iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "AppIcon.Notification.png");
            if (!File.Exists(iconPath))
                return;

            manager.NotificationInvoked += OnNotificationInvoked;
            manager.Register(AppDisplayName, new Uri(iconPath, UriKind.Absolute));
            _registered = true;
        }
        catch
        {
            _registered = false;
        }
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

    public static void ShowCleanResult(ulong bytesFreed, string formatted)
    {
        var noSound = !IniConfig.ReadBool("IsNotificationsSound", true);
        var title = CoreService.GetString(StrId.CleanMemory) ?? "Memory cleaned";
        var msg = CoreService.GetString(StrId.StatusCleaned);
        if (msg != null)
            msg = msg.Replace("%s", formatted);
        else
            msg = $"Memory released: {formatted}";

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
