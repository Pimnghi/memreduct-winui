using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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
        ToggleStartMinimized.IsOn = IniConfig.ReadBool("IsStartMinimized");
        ToggleConfirmClean.IsOn = IniConfig.ReadBool("IsShowReductConfirmation", true);

        // 2. 通知设置
        ToggleShowResults.IsOn = IniConfig.ReadBool("BalloonCleanResults", true);
        ToggleNotificationSound.IsOn = IniConfig.ReadBool("IsNotificationsSound", true);

        ToggleAllowStandby.IsOn = IniConfig.ReadBool("IsAllowStandbyListCleanup", false);
        ToggleLogResults.IsOn = IniConfig.ReadBool("LogCleanResults", false);

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

        SliderWarning.Value = IniConfig.ReadUInt("TrayLevelWarning", 70);
        WarningValueText.Text = ((int)SliderWarning.Value).ToString();
        SliderDanger.Value = IniConfig.ReadUInt("TrayLevelDanger", 90);
        DangerValueText.Text = ((int)SliderDanger.Value).ToString();

        ToggleAutoClean.IsOn = IniConfig.ReadBool("AutoreductEnable");
        NbAutoClean.Value = IniConfig.ReadUInt("AutoreductValue", 90);
        NbAutoClean.IsEnabled = ToggleAutoClean.IsOn;

        ToggleIntervalClean.IsOn = IniConfig.ReadBool("AutoreductIntervalEnable");
        NbInterval.Value = IniConfig.ReadUInt("AutoreductIntervalValue", 30);
        NbInterval.IsEnabled = ToggleIntervalClean.IsOn;

        ToggleHotkey.IsOn = IniConfig.ReadBool("HotkeyCleanEnable");
        LoadHotkeyDisplay();
    }

    public void ApplyLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);

        var v = s(StrId.SettingsGeneral);      if (v != null) GeneralHeader.Text = v;
        v = s(StrId.LanguageHint);              if (v != null) LanguageLabel.Text = v;
        v = s(StrId.AlwaysOnTop);               if (v != null) AlwaysOnTopLabel.Text = v;
        v = s(StrId.LoadOnStartup);             if (v != null) LoadOnStartupLabel.Text = v;
        v = s(StrId.StartMinimized);            if (v != null) StartMinimizedLabel.Text = v;
        v = s(StrId.ConfirmCleaning);           if (v != null) ConfirmCleanLabel.Text = v;

        v = s(StrId.SettingsAppearance);        if (v != null) AppearanceHeader.Text = v;
        v = s(StrId.Theme);                     if (v != null) ThemeLabel.Text = v;
        v = s(StrId.WarningLevel);              if (v != null) WarningLabel.Text = v;
        v = s(StrId.DangerLevel);               if (v != null) DangerLabel.Text = v;

        v = s(StrId.SettingsMemory);            if (v != null) MemoryHeader.Text = v;
        v = s(StrId.TitleMemoryRegions);        if (v != null) RegionsExpander.Header = v;
        v = s(StrId.WorkingSet);                if (v != null) ChkWorkingSet.Content = v;
        v = s(StrId.SystemFileCache);           if (v != null) ChkSystemFileCache.Content = v;
        v = s(StrId.StandbyPriority0);          if (v != null) ChkStandbyPriority0.Content = v;
        v = s(StrId.StandbyList);               if (v != null) ChkStandbyList.Content = v;
        v = s(StrId.ModifiedList);              if (v != null) ChkModifiedList.Content = v;
        v = s(StrId.CombineMemoryLists);        if (v != null) ChkCombineMemoryLists.Content = v;
        v = s(StrId.RegistryCache);             if (v != null) ChkRegistryCache.Content = v;
        v = s(StrId.ModifiedFileCache);         if (v != null) ChkModifiedFileCache.Content = v;
        v = s(StrId.AutoCleanEnable);           if (v != null) AutoCleanLabel.Text = v;
        v = s(StrId.AutoCleanInterval);         if (v != null) IntervalCleanLabel.Text = v;
        v = s(StrId.TitleHotkeys);              if (v != null) HotkeyLabel.Text = v;

        v = s(StrId.ShowCleanResult);           if (v != null) ShowResultsLabel.Text = v;
        v = s(StrId.NotificationSound);         if (v != null) NotificationSoundLabel.Text = v;
        v = s(StrId.BalloonTips);               if (v != null) NotificationHeader.Text = v;

        v = s(StrId.AllowStandbyCleanup);       if (v != null) AllowStandbyLabel.Text = v;
        v = s(StrId.LogCleanResults);           if (v != null) LogResultsLabel.Text = v;
        v = s(StrId.TitleAdvanced);             if (v != null) AdvancedHeader.Text = v;

        v = s(StrId.SettingsTray);              if (v != null) TrayHeader.Text = v;
        v = s(StrId.TrayActionScHint);          if (v != null) TrayLeftLabel.Text = v;
        v = s(StrId.TrayActionMcHint);          if (v != null) TrayMidLabel.Text = v;
    }

    private void LoadLocales()
    {
        CmbLanguage.Items.Clear();
        CmbLanguage.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeSystem) ?? "System default", Tag = "" });

        var count = CoreService.GetLocaleCount();
        var currentLocale = IniConfig.ReadString("Language", "");
        var idx = 1;
        int selected = 0;

        for (uint i = 0; i < count; i++)
        {
            var name = CoreService.GetLocaleName(i);
            if (name != null)
            {
                CmbLanguage.Items.Add(new ComboBoxItem { Content = name, Tag = name });
                if (name == currentLocale) selected = idx;
                idx++;
            }
        }

        if (CmbLanguage.Items.Count > 0 && selected < CmbLanguage.Items.Count)
            CmbLanguage.SelectedIndex = selected;
        else
            CmbLanguage.SelectedIndex = 0;
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
            var requested = ToggleLoadOnStartup.IsOn;
            if (!AutoStartService.SetEnabled(requested))
            {
                _loading = true;
                ToggleLoadOnStartup.IsOn = !requested;
                _loading = false;
            }
        }
        else if (ReferenceEquals(sender, ToggleStartMinimized))
        {
            IniConfig.WriteBool("IsStartMinimized", ToggleStartMinimized.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleConfirmClean))
        {
            IniConfig.WriteBool("IsShowReductConfirmation", ToggleConfirmClean.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleShowResults))
        {
            IniConfig.WriteBool("BalloonCleanResults", ToggleShowResults.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleNotificationSound))
        {
            IniConfig.WriteBool("IsNotificationsSound", ToggleNotificationSound.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleAutoClean))
        {
            IniConfig.WriteBool("AutoreductEnable", ToggleAutoClean.IsOn);
            NbAutoClean.IsEnabled = ToggleAutoClean.IsOn;
            AutoCleanService.Refresh();
        }
        else if (ReferenceEquals(sender, ToggleIntervalClean))
        {
            IniConfig.WriteBool("AutoreductIntervalEnable", ToggleIntervalClean.IsOn);
            NbInterval.IsEnabled = ToggleIntervalClean.IsOn;
            AutoCleanService.Refresh();
        }
        else if (ReferenceEquals(sender, ToggleAllowStandby))
        {
            IniConfig.WriteBool("IsAllowStandbyListCleanup", ToggleAllowStandby.IsOn);
        }
        else if (ReferenceEquals(sender, ToggleLogResults))
        {
            IniConfig.WriteBool("LogCleanResults", ToggleLogResults.IsOn);
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
        if (CmbLanguage.SelectedItem is ComboBoxItem item)
        {
            var code = item.Tag?.ToString() ?? "";
            IniConfig.WriteString("Language", code);
            if (CmbLanguage.SelectedIndex > 0)
                CoreService.SetLocale((uint)(CmbLanguage.SelectedIndex - 1));
            if (App.MainWindow is MainWindow window)
                window.ApplyLocalization();
            else
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

    private void OnWarningValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args) { if (_loading || WarningValueText == null) return; var v = (uint)args.NewValue; IniConfig.WriteUInt("TrayLevelWarning", v); WarningValueText.Text = v.ToString(); }
    private void OnDangerValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args) { if (_loading || DangerValueText == null) return; var v = (uint)args.NewValue; IniConfig.WriteUInt("TrayLevelDanger", v); DangerValueText.Text = v.ToString(); }
    private void OnAutoCleanValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("AutoreductValue", (uint)args.NewValue); }
    private void OnIntervalValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("AutoreductIntervalValue", (uint)args.NewValue); }

    private void LoadHotkeyDisplay()
    {
        var hotkey = IniConfig.ReadInt("HotkeyClean", (0x02 << 8 | 0x70));
        var vk = hotkey & 0xFF;
        var mods = (hotkey >> 8) & 0xFF;
        HotkeyBox.Text = HotkeyToString((uint)mods, (uint)vk);
    }

    private void OnHotkeyChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        IniConfig.WriteBool("HotkeyCleanEnable", ToggleHotkey.IsOn);
        if (TrayIcon.RefreshHotkey()) return;

        IniConfig.WriteBool("HotkeyCleanEnable", false);
        _loading = true;
        ToggleHotkey.IsOn = false;
        _loading = false;
        HotkeyBox.PlaceholderText = "This hotkey is already in use.";
    }
    private void OnHotkeyTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) { HotkeyBox.PlaceholderText = "Press a key combination..."; HotkeyBox.Focus(FocusState.Keyboard); }

    private void OnHotkeyKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var vk = (uint)e.Key;
        if (vk is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5) { e.Handled = false; return; }
        uint mods = 0;
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= 2;
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= 4;
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= 1;
        var hotkey = ((int)mods << 8) | (int)vk;
        IniConfig.WriteInt("HotkeyClean", hotkey);
        HotkeyBox.Text = HotkeyToString(mods, vk);
        if (ToggleHotkey.IsOn && !TrayIcon.RefreshHotkey())
        {
            IniConfig.WriteBool("HotkeyCleanEnable", false);
            _loading = true;
            ToggleHotkey.IsOn = false;
            _loading = false;
            HotkeyBox.PlaceholderText = "This hotkey is already in use.";
        }
        e.Handled = true;
    }

    private static string HotkeyToString(uint mods, uint vk)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 4) != 0) parts.Add("Alt");
        if ((mods & 1) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Win");
        var keyName = vk switch
        {
            >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1),
            0x2E => "Del", 0x08 => "Back", 0x09 => "Tab", 0x0D => "Enter",
            0x20 => "Space", 0x21 => "PgUp", 0x22 => "PgDn",
            0x23 => "End", 0x24 => "Home", 0x25 => "Left", 0x26 => "Up",
            0x27 => "Right", 0x28 => "Down", 0x2D => "Ins",
            _ => ((char)vk).ToString()
        };
        parts.Add(keyName);
        return string.Join(" + ", parts);
    }

}
