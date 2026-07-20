using MemReduct.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Core;
using Windows.System;

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
        _loading = false;
    }

    public void ApplyLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);

        var v = s(StrId.TitleMemoryRegions);
        if (v != null) RegionsExpander.Header = v;
        v = s(StrId.TitleMemoryManagement);
        if (v != null) AutoExpander.Header = v;
        v = s(StrId.SettingsGeneral);
        if (v != null) GeneralExpander.Header = v;
        v = s(StrId.LanguageHint);
        if (v != null) LanguageLabel.Text = v + ":";

        v = s(StrId.WorkingSet);           if (v != null) ChkWorkingSet.Content = v;
        v = s(StrId.SystemFileCache);       if (v != null) ChkSystemFileCache.Content = v;
        v = s(StrId.ModifiedList);          if (v != null) ChkModifiedList.Content = v;
        v = s(StrId.StandbyList);           if (v != null) ChkStandbyList.Content = v;
        v = s(StrId.StandbyPriority0);      if (v != null) ChkStandbyPriority0.Content = v;
        v = s(StrId.CombineMemoryLists);    if (v != null) ChkCombineLists.Content = v;

        v = s(StrId.AutoCleanEnable);       if (v != null) ChkAutoClean.Content = v;
        v = s(StrId.AutoCleanInterval);     if (v != null) ChkIntervalClean.Content = v;

        v = s(StrId.AlwaysOnTop);           if (v != null) ChkAlwaysOnTop.Content = v;
        v = s(StrId.StartMinimized);        if (v != null) ChkStartMinimized.Content = v;
        v = s(StrId.ConfirmCleaning);       if (v != null) ChkConfirmClean.Content = v;
        v = s(StrId.ShowCleanResult);       if (v != null) ChkShowResults.Content = v;

        v = s(StrId.HotkeyClean);            if (v != null) ChkHotkey.Content = v;
        v = s(StrId.TitleHotkeys);           if (v != null) HotkeyExpander.Header = v;
        v = s(StrId.ColorIndication);        if (v != null) ColorExpander.Header = v;
        v = s(StrId.WarningLevel);           if (v != null) WarningLabel.Text = v;
        v = s(StrId.DangerLevel);            if (v != null) DangerLabel.Text = v;
    }

    private void LoadSettings()
    {
        var mask = IniConfig.ReadUInt("ReductMask2", MemoryMask.Default);
        ChkWorkingSet.IsChecked       = (mask & MemoryMask.WorkingSet) != 0;
        ChkSystemFileCache.IsChecked   = (mask & MemoryMask.SystemFileCache) != 0;
        ChkModifiedFileCache.IsChecked = (mask & MemoryMask.ModifiedFileCache) != 0;
        ChkModifiedList.IsChecked      = (mask & MemoryMask.ModifiedList) != 0;
        ChkStandbyList.IsChecked       = (mask & MemoryMask.StandbyList) != 0;
        ChkStandbyPriority0.IsChecked  = (mask & MemoryMask.StandbyPriority0List) != 0;
        ChkRegistryCache.IsChecked     = (mask & MemoryMask.RegistryCache) != 0;
        ChkCombineLists.IsChecked      = (mask & MemoryMask.CombineMemoryLists) != 0;

        ChkAutoClean.IsChecked = IniConfig.ReadBool("AutoreductEnable");
        NbAutoClean.Value = IniConfig.ReadUInt("AutoreductValue", 90);
        NbAutoClean.IsEnabled = ChkAutoClean.IsChecked == true;

        ChkIntervalClean.IsChecked = IniConfig.ReadBool("AutoreductIntervalEnable");
        NbInterval.Value = IniConfig.ReadUInt("AutoreductIntervalValue", 30);
        NbInterval.IsEnabled = ChkIntervalClean.IsChecked == true;

        ChkAlwaysOnTop.IsChecked = IniConfig.ReadBool("AlwaysOnTop");
        ChkStartMinimized.IsChecked = IniConfig.ReadBool("IsStartMinimized");
        ChkConfirmClean.IsChecked = IniConfig.ReadBool("IsShowReductConfirmation", true);
        ChkShowResults.IsChecked = IniConfig.ReadBool("BalloonCleanResults", true);

        NbWarning.Value = IniConfig.ReadUInt("TrayLevelWarning", 70);
        NbDanger.Value = IniConfig.ReadUInt("TrayLevelDanger", 90);

        CmbTheme.Items.Clear();
        CmbTheme.Items.Add(new ComboBoxItem { Content = "System default", Tag = "System" });
        CmbTheme.Items.Add(new ComboBoxItem { Content = "Light", Tag = "Light" });
        CmbTheme.Items.Add(new ComboBoxItem { Content = "Dark", Tag = "Dark" });
        CmbTheme.SelectedIndex = (IniConfig.ReadString("Theme", "System") ?? "System") switch { "Light" => 1, "Dark" => 2, _ => 0 };

        ChkHotkey.IsChecked = IniConfig.ReadBool("HotkeyCleanEnable");
        LoadHotkeyDisplay();
    }

    private void LoadLocales()
    {
        CmbLanguage.Items.Clear();
        var count = CoreService.GetLocaleCount();
        var current = CoreService.GetCurrentLocaleIndex();
        for (uint i = 0; i <= count; i++)
        {
            var name = i == 0 ? "System default" : CoreService.GetLocaleName((uint)(i - 1));
            if (name != null)
            {
                CmbLanguage.Items.Add(new ComboBoxItem { Content = name, Tag = i });
                if (i == current)
                    CmbLanguage.SelectedIndex = (int)i;
            }
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbTheme.SelectedItem is not ComboBoxItem item) return;
        var theme = item.Tag?.ToString() ?? "System";
        IniConfig.WriteString("Theme", theme);
        App.ApplyTheme(theme);
    }

    private void SaveMask()
    {
        uint mask = 0;
        if (ChkWorkingSet.IsChecked == true)        mask |= MemoryMask.WorkingSet;
        if (ChkSystemFileCache.IsChecked == true)    mask |= MemoryMask.SystemFileCache;
        if (ChkModifiedFileCache.IsChecked == true)  mask |= MemoryMask.ModifiedFileCache;
        if (ChkModifiedList.IsChecked == true)       mask |= MemoryMask.ModifiedList;
        if (ChkStandbyList.IsChecked == true)        mask |= MemoryMask.StandbyList;
        if (ChkStandbyPriority0.IsChecked == true)   mask |= MemoryMask.StandbyPriority0List;
        if (ChkRegistryCache.IsChecked == true)      mask |= MemoryMask.RegistryCache;
        if (ChkCombineLists.IsChecked == true)       mask |= MemoryMask.CombineMemoryLists;
        IniConfig.WriteUInt("ReductMask2", mask);
    }

    private void OnRegionChanged(object sender, RoutedEventArgs e) { if (!_loading) SaveMask(); }
    private void OnAutoCleanChanged(object sender, RoutedEventArgs e) { if (_loading) return; NbAutoClean.IsEnabled = ChkAutoClean.IsChecked == true; IniConfig.WriteBool("AutoreductEnable", ChkAutoClean.IsChecked == true); AutoCleanService.Refresh(); }
    private void OnIntervalCleanChanged(object sender, RoutedEventArgs e) { if (_loading) return; NbInterval.IsEnabled = ChkIntervalClean.IsChecked == true; IniConfig.WriteBool("AutoreductIntervalEnable", ChkIntervalClean.IsChecked == true); AutoCleanService.Refresh(); }
    private void OnAutoCleanValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("AutoreductValue", (uint)args.NewValue); }
    private void OnIntervalValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("AutoreductIntervalValue", (uint)args.NewValue); }

    private void OnWarningValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("TrayLevelWarning", (uint)args.NewValue); }
    private void OnDangerValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("TrayLevelDanger", (uint)args.NewValue); }

    private void OnBoolChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, ChkAlwaysOnTop))
        {
            IniConfig.WriteBool("AlwaysOnTop", ChkAlwaysOnTop.IsChecked == true);
            if (App.MainWindow is MainWindow w) w.ApplyTopmost();
        }
        else if (ReferenceEquals(sender, ChkStartMinimized))
            IniConfig.WriteBool("IsStartMinimized", ChkStartMinimized.IsChecked == true);
        else if (ReferenceEquals(sender, ChkConfirmClean))
            IniConfig.WriteBool("IsShowReductConfirmation", ChkConfirmClean.IsChecked == true);
        else if (ReferenceEquals(sender, ChkShowResults))
            IniConfig.WriteBool("BalloonCleanResults", ChkShowResults.IsChecked == true);
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbLanguage.SelectedItem is not ComboBoxItem item) return;
        var name = (string?)item.Content ?? "";
        IniConfig.WriteString("Language", name == "System default" ? "" : name);
        CoreService.SetLocale((uint)(item.Tag ?? 0u));
        ApplyLocalization();
    }

    private void LoadHotkeyDisplay()
    {
        var hotkey = IniConfig.ReadInt("HotkeyClean", (0x02 << 8 | 0x70));
        var vk = hotkey & 0xFF;
        var mods = (hotkey >> 8) & 0xFF;
        HotkeyBox.Text = HotkeyToString((uint)mods, (uint)vk);
    }

    private void OnHotkeyChanged(object sender, RoutedEventArgs e) { if (_loading) return; IniConfig.WriteBool("HotkeyCleanEnable", ChkHotkey.IsChecked == true); if (App.MainWindow is MainWindow w) TrayIcon.RefreshHotkey(); }
    private void OnHotkeyTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) { HotkeyBox.PlaceholderText = "Press a key combination..."; HotkeyBox.Focus(FocusState.Keyboard); }

    private void OnHotkeyKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var vk = (uint)e.Key;
        if (vk is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5) { e.Handled = false; return; }

        uint mods = 0;
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        var lwin = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.LeftWindows);
        var rwin = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.RightWindows);

        if (ctrl.HasFlag(CoreVirtualKeyStates.Down)) mods |= 2;
        if (alt.HasFlag(CoreVirtualKeyStates.Down)) mods |= 4;
        if (shift.HasFlag(CoreVirtualKeyStates.Down)) mods |= 1;
        if (lwin.HasFlag(CoreVirtualKeyStates.Down) || rwin.HasFlag(CoreVirtualKeyStates.Down)) mods |= 8;

        var hotkey = ((int)mods << 8) | (int)vk;
        IniConfig.WriteInt("HotkeyClean", hotkey);
        HotkeyBox.Text = HotkeyToString(mods, vk);
        if (App.MainWindow is MainWindow w) TrayIcon.RefreshHotkey();
        e.Handled = true;
    }

    private static string HotkeyToString(uint mods, uint vk)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 4) != 0) parts.Add("Shift");
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
