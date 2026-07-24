using System;

namespace MemReduct.Core;

public static class ToastService
{
    public static void ShowCleanResult(ulong bytesFreed, string formatted)
    {
        var noSound = !IniConfig.ReadBool("IsNotificationsSound", true);
        var title = CoreService.GetString(StrId.CleanMemory) ?? "Memory cleaned";
        var msg = CoreService.GetString(StrId.StatusCleaned);
        if (msg != null)
            msg = msg.Replace("%s", formatted);
        else
            msg = $"Memory released: {formatted}";
        TrayIcon.ShowBalloon("Mem Reduct WinUI", msg, noSound);
    }
}
