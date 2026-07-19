namespace MemReduct.Core;

public static class ToastService
{
    public static void Show(string title, string message)
    {
        var noSound = !IniConfig.ReadBool("IsNotificationsSound", true);
        TrayIcon.ShowBalloon(title, message, noSound);
    }
}
