using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;

namespace memreduct_winui;

public sealed partial class SettingsPage : Page
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        _loading = true;
        LoadSettings();
        LoadLocales();
        LoadThemes();
        LoadTrayActions();
        ApplyLocalization();
        _loading = false;
    }

    private void LoadSettings()
    {
        // 1. 常规设置
        ToggleAlwaysOnTop.IsOn = IniConfig.ReadBool("AlwaysOnTop");
        ToggleLoadOnStartup.IsOn = IniConfig.ReadBool("LoadOnStartup");
        ToggleStartMinimized.IsOn = IniConfig.ReadBool("StartMinimized");
        ToggleConfirmClean.IsOn = IniConfig.ReadBool("ConfirmCleaning");

        // 2. 通知设置
        ToggleShowResults.IsOn = IniConfig.ReadBool("BalloonCleanResults", true);
        ToggleNotificationSound.IsOn = IniConfig.ReadBool("SoundCleanResults", true);

        // 3. 内存清理区域
        uint mask = IniConfig.ReadUInt("ReductMask2", MemoryMask.Default);
        ChkWorkingSet.IsChecked = (mask & MemoryMask.WorkingSet) != 0;
        ChkSystemFileCache.IsChecked = (mask & MemoryMask.SystemFileCache) != 0;
        ChkStandbyPriority0.IsChecked = (mask & MemoryMask.StandbyPriority0List) != 0;
        ChkStandbyList.IsChecked = (mask & MemoryMask.StandbyList) != 0;
        ChkModifiedList.IsChecked = (mask & MemoryMask.ModifiedList) != 0;
        ChkCombineMemoryLists.IsChecked = (mask & MemoryMask.CombineMemoryLists) != 0;
        ChkRegistryCache.IsChecked = (mask & MemoryMask.RegistryCache) != 0;
        ChkModifiedFileCache.IsChecked = (mask & MemoryMask.ModifiedFileCache) != 0;
    }

    public void ApplyLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);

        var v = s(StrId.SettingsGeneral);      if (v != null) GeneralHeader.Text = v;
        v = s(StrId.LanguageHint);              if (v != null) LanguageLabel.Text = v + ":";
        v = s(StrId.AlwaysOnTop);               if (v != null) AlwaysOnTopLabel.Text = v;
        v = s(StrId.LoadOnStartup);             if (v != null) LoadOnStartupLabel.Text = v;
        v = s(StrId.StartMinimized);            if (v != null) StartMinimizedLabel.Text = v;
        v = s(StrId.ConfirmCleaning);           if (v != null) ConfirmCleanLabel.Text = v;

        v = s(StrId.SettingsAppearance);        if (v != null) AppearanceHeader.Text = v;
        v = s(StrId.Theme);                     if (v != null) ThemeLabel.Text = v;

        v = s(StrId.ShowCleanResult);           if (v != null) ShowResultsLabel.Text = v;

        v = s(StrId.TitleMemoryRegions);        if (v != null) RegionsHeader.Text = v;
        v = s(StrId.WorkingSet);               if (v != null) WorkingSetLabel.Text = v;
        v = s(StrId.SystemFileCache);           if (v != null) SystemFileCacheLabel.Text = v;
        v = s(StrId.StandbyPriority0);          if (v != null) StandbyListLowPriorityLabel.Text = v;
        v = s(StrId.StandbyList);              if (v != null) StandbyListLabel.Text = v;
        v = s(StrId.ModifiedList);             if (v != null) ModifiedListLabel.Text = v;
        v = s(StrId.CombineMemoryLists);       if (v != null) CombineMemoryListsLabel.Text = v;

        v = s(StrId.TrayShow);                  if (v != null) TrayHeader.Text = v;
    }

    private void LoadLocales()
    {
        CmbLanguage.Items.Clear();
        var locales = CoreService.GetAvailableLocales();
        var currentLocale = IniConfig.ReadString("Language", "");

        int selectIndex = 0;
        for (int i = 0; i < locales.Count; i++)
        {
            var item = new ComboBoxItem { Content = locales[i].Name, Tag = locales[i].Code };
            CmbLanguage.Items.Add(item);
            if (locales[i].Code.Equals(currentLocale, StringComparison.OrdinalIgnoreCase))
                selectIndex = i;
        }
        CmbLanguage.SelectedIndex = selectIndex;
    }

    private void LoadThemes()
    {
        CmbTheme.Items.Clear();
        CmbTheme.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeSystem) ?? "跟随系统", Tag = ElementTheme.Default });
        CmbTheme.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeLight) ?? "浅色", Tag = ElementTheme.Light });
        CmbTheme.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeDark) ?? "深色", Tag = ElementTheme.Dark });

        var themeStr = IniConfig.ReadString("Theme", "System");
        CmbTheme.SelectedIndex = themeStr switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
    }

    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        if (ReferenceEquals(sender, ToggleAlwaysOnTop))
        {
            IniConfig.WriteBool("AlwaysOnTop", ToggleAlwaysOnTop.IsOn);
            if (App.MainWindow is MainWindow mw) mw.SetAlwaysOnTop(ToggleAlwaysOnTop.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleLoadOnStartup))
        {
            IniConfig.WriteBool("LoadOnStartup", ToggleLoadOnStartup.IsOn);
            SetAutoStart(ToggleLoadOnStartup.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleStartMinimized))
        {
            IniConfig.WriteBool("StartMinimized", ToggleStartMinimized.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleConfirmClean))
        {
            IniConfig.WriteBool("ConfirmCleaning", ToggleConfirmClean.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleShowResults))
        {
            IniConfig.WriteBool("BalloonCleanResults", ToggleShowResults.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleNotificationSound))
        {
            IniConfig.WriteBool("SoundCleanResults", ToggleNotificationSound.IsOn);
        }
    }

    private void OnRegionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        uint mask = 0;
        if (ChkWorkingSet.IsChecked == true) mask |= MemoryMask.WorkingSet;
        if (ChkSystemFileCache.IsChecked == true) mask |= MemoryMask.SystemFileCache;
        if (ChkStandbyPriority0.IsChecked == true) mask |= MemoryMask.StandbyPriority0List;
        if (ChkStandbyList.IsChecked == true) mask |= MemoryMask.StandbyList;
        if (ChkModifiedList.IsChecked == true) mask |= MemoryMask.ModifiedList;
        if (ChkCombineMemoryLists.IsChecked == true) mask |= MemoryMask.CombineMemoryLists;
        if (ChkRegistryCache.IsChecked == true) mask |= MemoryMask.RegistryCache;
        if (ChkModifiedFileCache.IsChecked == true) mask |= MemoryMask.ModifiedFileCache;

        IniConfig.WriteUInt("ReductMask2", mask);
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string code)
        {
            IniConfig.WriteString("Language", code);
            ApplyLocalization();
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (CmbTheme.SelectedItem is ComboBoxItem item && item.Tag is ElementTheme theme)
        {
            IniConfig.WriteString("Theme", theme.ToString());
            App.ApplyTheme(theme == ElementTheme.Dark ? "Dark" : theme == ElementTheme.Light ? "Light" : "System");
        }
    }

    private void LoadTrayActions()
    {
        SetupTrayCmb(CmbTrayLeft, IniConfig.ReadInt("TrayActionDc", TrayIcon.ACTION_SHOW));
        SetupTrayCmb(CmbTrayMid, IniConfig.ReadInt("TrayActionMc", TrayIcon.ACTION_CLEAN));
    }

    private static void SetupTrayCmb(ComboBox cmb, int current)
    {
        cmb.Items.Clear();
        cmb.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.TrayShow) ?? "显示 / 隐藏", Tag = TrayIcon.ACTION_SHOW });
        cmb.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.CleanMemory) ?? "清理内存", Tag = TrayIcon.ACTION_CLEAN });
        cmb.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.TrayAction3) ?? "打开任务管理器", Tag = TrayIcon.ACTION_TASKMGR });
        cmb.SelectedIndex = current switch { TrayIcon.ACTION_CLEAN => 1, TrayIcon.ACTION_TASKMGR => 2, _ => 0 };
    }

    private void OnTrayActionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, CmbTrayLeft) && CmbTrayLeft.SelectedItem is ComboBoxItem left)
            IniConfig.WriteInt("TrayActionDc", (int)(left.Tag ?? 0));
        else if (ReferenceEquals(sender, CmbTrayMid) && CmbTrayMid.SelectedItem is ComboBoxItem mid)
            IniConfig.WriteInt("TrayActionMc", (int)(mid.Tag ?? 1));
    }

    private static void SetAutoStart(bool enable)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(exePath)) return;

        if (enable)
        {
            Process.Start(new ProcessStartInfo("schtasks.exe", $"/create /tn \"MemReductWinUI\" /tr \"\\\"{exePath}\\\" -autostart\" /sc onlogon /rl highest /f")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.WaitForExit();
        }
        else
        {
            Process.Start(new ProcessStartInfo("schtasks.exe", "/delete /tn \"MemReductWinUI\" /f")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.WaitForExit();
        }
    }
}