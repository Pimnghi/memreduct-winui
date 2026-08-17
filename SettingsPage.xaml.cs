using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace memreduct_winui;

public sealed partial class SettingsPage : Page
{
    private bool _loading;
    private ContentDialog? _hotkeyDialog;
    private StackPanel? _hotkeyDialogKeycaps;
    private TextBlock? _hotkeyDialogStatus;
    private TextBox? _hotkeyCaptureBox;
    private uint _pressedHotkeyModifiers;
    private uint _editingHotkeyModifiers;
    private uint _editingHotkeyKey;

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
        ToggleTrayMemoryUsage.IsOn = IniConfig.ReadBool("TrayShowMemoryUsage", false);

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
        UpdateDangerousRegionAvailability(ToggleAllowStandby.IsOn, true);

        SliderWarning.Value = IniConfig.ReadUInt("TrayLevelWarning", 70);
        WarningValueText.Text = $"{(int)SliderWarning.Value}%";
        SliderDanger.Value = IniConfig.ReadUInt("TrayLevelDanger", 90);
        DangerValueText.Text = $"{(int)SliderDanger.Value}%";

        ToggleAutoClean.IsOn = IniConfig.ReadBool("AutoreductEnable");
        SliderAutoClean.Value = IniConfig.ReadUInt("AutoreductValue", 90);
        AutoCleanValueText.Text = $"{(int)SliderAutoClean.Value}%";
        AutoCleanThresholdCard.IsEnabled = ToggleAutoClean.IsOn;

        ToggleIntervalClean.IsOn = IniConfig.ReadBool("AutoreductIntervalEnable");
        NbInterval.Value = IniConfig.ReadUInt("AutoreductIntervalValue", 30);
        IntervalValueCard.IsEnabled = ToggleIntervalClean.IsOn;

        ToggleHotkey.IsOn = IniConfig.ReadBool("HotkeyCleanEnable");
        LoadHotkeyDisplay();
    }

    public void ApplyLocalization()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            var s = (uint id) => CoreService.GetString(id);

            var v = s(StrId.Settings);             if (v != null) SettingsPageTitle.Text = v;
            v = s(StrId.SettingsGeneral);            if (v != null) GeneralHeader.Text = v;
            v = s(StrId.LanguageHint);               if (v != null) LanguageCard.Header = v;
            v = s(StrId.AlwaysOnTop);                if (v != null) AlwaysOnTopCard.Header = v;
            v = s(StrId.LoadOnStartup);              if (v != null) LoadOnStartupCard.Header = v;
            v = s(StrId.StartMinimized);             if (v != null) StartMinimizedCard.Header = v;
            v = s(StrId.ConfirmCleaning);            if (v != null) ConfirmCleanCard.Header = v;

            v = s(StrId.SettingsAppearance);        if (v != null) AppearanceHeader.Text = v;
            v = s(StrId.Theme);                     if (v != null) ThemeCard.Header = v;
            v = s(StrId.WarningLevel);              if (v != null) WarningCard.Header = v;
            v = s(StrId.DangerLevel);               if (v != null) DangerCard.Header = v;

            v = s(StrId.SettingsMemory);            if (v != null) MemoryHeader.Text = v;
            v = s(StrId.TitleMemoryRegions);        if (v != null) RegionsExpander.Header = v;
            v = s(StrId.WorkingSet);                if (v != null) WorkingSetCard.Header = v;
            v = s(StrId.SystemFileCache);           if (v != null) SystemFileCacheCard.Header = v;
            v = s(StrId.StandbyPriority0);          if (v != null) StandbyPriority0Card.Header = v;
            v = s(StrId.StandbyList);               if (v != null) StandbyListCard.Header = v;
            v = s(StrId.ModifiedList);              if (v != null) ModifiedListCard.Header = v;
            v = s(StrId.CombineMemoryLists);        if (v != null) CombineMemoryListsCard.Header = v;
            v = s(StrId.RegistryCache);             if (v != null) RegistryCacheCard.Header = v;
            v = s(StrId.ModifiedFileCache);         if (v != null) ModifiedFileCacheCard.Header = v;
            v = s(StrId.AutoCleanEnable);           if (v != null) AutoCleanExpander.Header = v;
            v = s(StrId.AutoCleanInterval);         if (v != null) IntervalCleanExpander.Header = v;
            v = s(StrId.MinuteUnit);                if (v != null) IntervalMinuteText.Text = v;
            v = s(StrId.TitleHotkeys);              if (v != null) HotkeyExpander.Header = v;

            v = s(StrId.ShowCleanResult);           if (v != null) ShowResultsCard.Header = v;
            v = s(StrId.NotificationSound);         if (v != null) NotificationSoundCard.Header = v;
            v = s(StrId.BalloonTips);               if (v != null) NotificationHeader.Text = v;

            v = s(StrId.AllowStandbyCleanup);       if (v != null) AllowStandbyCard.Header = v;
            v = s(StrId.LogCleanResults);           if (v != null) LogResultsCard.Header = v;
            v = s(StrId.TitleAdvanced);             if (v != null) AdvancedHeader.Text = v;

            v = s(StrId.SettingsTray);              if (v != null) TrayHeader.Text = v;
            v = s(StrId.TrayShowMemoryUsage);       if (v != null) TrayMemoryUsageCard.Header = v;
            v = s(StrId.TrayActionScHint);          if (v != null) TrayLeftCard.Header = v;
            v = s(StrId.TrayActionMcHint);          if (v != null) TrayMidCard.Header = v;

            UpdateSystemLocaleLabel();
            LoadThemes();
            LoadTrayActions();
        }
        finally
        {
            _loading = wasLoading;
        }
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
                CmbLanguage.Items.Add(new ComboBoxItem { Content = GetLocaleDisplayName(name), Tag = name });
                if (name == currentLocale) selected = idx;
                idx++;
            }
        }

        if (CmbLanguage.Items.Count > 0 && selected < CmbLanguage.Items.Count)
            CmbLanguage.SelectedIndex = selected;
        else
            CmbLanguage.SelectedIndex = 0;
    }

    private void UpdateSystemLocaleLabel()
    {
        if (CmbLanguage.Items.Count > 0 && CmbLanguage.Items[0] is ComboBoxItem systemItem)
            systemItem.Content = CoreService.GetString(StrId.ThemeSystem) ?? "System default";
    }

    private void LoadThemes()
    {
        CmbTheme.Items.Clear();
        CmbTheme.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeSystem) ?? "System default", Tag = ElementTheme.Default });
        CmbTheme.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeLight) ?? "Light", Tag = ElementTheme.Light });
        CmbTheme.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.ThemeDark) ?? "Dark", Tag = ElementTheme.Dark });

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
        else if (ReferenceEquals(sender, ToggleTrayMemoryUsage))
        {
            IniConfig.WriteBool("TrayShowMemoryUsage", ToggleTrayMemoryUsage.IsOn);
            TrayIcon.RefreshMemoryDisplay();
        }
        else if (ReferenceEquals(sender, ToggleAutoClean))
        {
            IniConfig.WriteBool("AutoreductEnable", ToggleAutoClean.IsOn);
            AutoCleanThresholdCard.IsEnabled = ToggleAutoClean.IsOn;
            AutoCleanService.Refresh();
        }
        else if (ReferenceEquals(sender, ToggleIntervalClean))
        {
            IniConfig.WriteBool("AutoreductIntervalEnable", ToggleIntervalClean.IsOn);
            IntervalValueCard.IsEnabled = ToggleIntervalClean.IsOn;
            AutoCleanService.Refresh();
        }
        else if (ReferenceEquals(sender, ToggleAllowStandby))
        {
            IniConfig.WriteBool("IsAllowStandbyListCleanup", ToggleAllowStandby.IsOn);
            UpdateDangerousRegionAvailability(ToggleAllowStandby.IsOn, true);
        }
        else if (ReferenceEquals(sender, ToggleLogResults))
        {
            IniConfig.WriteBool("LogCleanResults", ToggleLogResults.IsOn);
        }
    }

    private void OnRegionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        WriteRegionMask();
    }

    private void UpdateDangerousRegionAvailability(bool isAllowed, bool persistMask)
    {
        if (isAllowed)
        {
            StandbyListCard.IsEnabled = true;
            ModifiedListCard.IsEnabled = true;
            return;
        }

        var selectionChanged = ChkStandbyList.IsChecked == true || ChkModifiedList.IsChecked == true;
        ChkStandbyList.IsChecked = false;
        ChkModifiedList.IsChecked = false;
        StandbyListCard.IsEnabled = false;
        ModifiedListCard.IsEnabled = false;
        if (persistMask && selectionChanged)
            WriteRegionMask();
    }

    private void WriteRegionMask()
    {
        uint mask = 0;
        if (ChkWorkingSet.IsChecked == true) mask |= MemoryMask.WorkingSet;
        if (ChkSystemFileCache.IsChecked == true) mask |= MemoryMask.SystemFileCache;
        if (ChkStandbyPriority0.IsChecked == true) mask |= MemoryMask.StandbyPriority0List;
        if (ToggleAllowStandby.IsOn && ChkStandbyList.IsChecked == true) mask |= MemoryMask.StandbyList;
        if (ToggleAllowStandby.IsOn && ChkModifiedList.IsChecked == true) mask |= MemoryMask.ModifiedList;
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

            // Rebuilding an open ComboBox inside SelectionChanged can re-enter
            // WinUI's selection logic. Refresh after the current input event.
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () =>
                {
                    if (App.MainWindow is MainWindow window)
                        window.ApplyLocalization();
                    else if (XamlRoot is not null)
                        ApplyLocalization();
                });
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
        cmb.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.TrayShow) ?? "Show / Hide", Tag = TrayIcon.ACTION_SHOW });
        cmb.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.CleanMemory) ?? "Clean memory", Tag = TrayIcon.ACTION_CLEAN });
        cmb.Items.Add(new ComboBoxItem { Content = CoreService.GetString(StrId.TrayAction3) ?? "Open task manager", Tag = TrayIcon.ACTION_TASKMGR });
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

    private void OnWarningValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args) { if (_loading || WarningValueText == null) return; var v = (uint)args.NewValue; IniConfig.WriteUInt("TrayLevelWarning", v); WarningValueText.Text = $"{v}%"; TrayIcon.RefreshMemoryDisplay(); }
    private void OnDangerValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args) { if (_loading || DangerValueText == null) return; var v = (uint)args.NewValue; IniConfig.WriteUInt("TrayLevelDanger", v); DangerValueText.Text = $"{v}%"; TrayIcon.RefreshMemoryDisplay(); }
    private void OnAutoCleanValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args) { if (_loading || AutoCleanValueText == null) return; var v = (uint)args.NewValue; IniConfig.WriteUInt("AutoreductValue", v); AutoCleanValueText.Text = $"{v}%"; }
    private void OnIntervalValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (_loading || double.IsNaN(args.NewValue)) return; IniConfig.WriteUInt("AutoreductIntervalValue", (uint)args.NewValue); }

    private void LoadHotkeyDisplay()
    {
        var hotkey = IniConfig.ReadInt("HotkeyClean", (0x02 << 8 | 0x70));
        RenderHotkeyKeycaps(HotkeyKeycapsPanel, (uint)((hotkey >> 8) & 0xFF), (uint)(hotkey & 0xFF), false);
        HotkeyStatusText.Visibility = Visibility.Collapsed;
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
        HotkeyStatusText.Text = GetHotkeyEditorStrings().Conflict;
        HotkeyStatusText.Visibility = Visibility.Visible;
    }

    private async void OnEditHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (_hotkeyDialog != null) return;

        var strings = GetHotkeyEditorStrings();
        var currentHotkey = IniConfig.ReadInt("HotkeyClean", (0x02 << 8 | 0x70));
        _editingHotkeyModifiers = (uint)((currentHotkey >> 8) & 0xFF);
        _editingHotkeyKey = (uint)(currentHotkey & 0xFF);
        _pressedHotkeyModifiers = 0;

        _hotkeyDialogKeycaps = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        _hotkeyDialogStatus = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = strings.Hint,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        var captureBox = new TextBox
        {
            Width = 1,
            Height = 1,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            IsTabStop = true,
            Opacity = 0
        };
        _hotkeyCaptureBox = captureBox;

        var keycapHost = new Grid();
        keycapHost.Children.Add(_hotkeyDialogKeycaps);
        keycapHost.Children.Add(captureBox);

        var content = new StackPanel
        {
            MinWidth = 420,
            Padding = new Thickness(0, 8, 0, 4),
            Spacing = 28
        };
        content.Children.Add(new TextBlock
        {
            Text = strings.Prompt,
            FontSize = 18,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(keycapHost);
        content.Children.Add(_hotkeyDialogStatus);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = HotkeyExpander.Header?.ToString() ?? "Hotkey",
            Content = content,
            PrimaryButtonText = strings.Save,
            CloseButtonText = strings.Cancel,
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = IsValidHotkey(_editingHotkeyModifiers, _editingHotkeyKey)
        };
        _hotkeyDialog = dialog;
        dialog.PreviewKeyDown += OnHotkeyDialogPreviewKeyDown;
        dialog.PreviewKeyUp += OnHotkeyDialogPreviewKeyUp;
        dialog.Opened += (_, _) => captureBox.Focus(FocusState.Programmatic);
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!IsValidHotkey(_editingHotkeyModifiers, _editingHotkeyKey))
            {
                args.Cancel = true;
                SetHotkeyDialogStatus(strings.Hint, true);
                return;
            }

            var oldHotkey = IniConfig.ReadInt("HotkeyClean", currentHotkey);
            var newHotkey = ((int)_editingHotkeyModifiers << 8) | (int)_editingHotkeyKey;
            IniConfig.WriteInt("HotkeyClean", newHotkey);
            if (ToggleHotkey.IsOn && !TrayIcon.RefreshHotkey())
            {
                IniConfig.WriteInt("HotkeyClean", oldHotkey);
                TrayIcon.RefreshHotkey();
                args.Cancel = true;
                SetHotkeyDialogStatus(strings.Conflict, true);
            }
        };

        RenderHotkeyKeycaps(_hotkeyDialogKeycaps, _editingHotkeyModifiers, _editingHotkeyKey, true);

        try
        {
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                LoadHotkeyDisplay();
        }
        finally
        {
            dialog.PreviewKeyDown -= OnHotkeyDialogPreviewKeyDown;
            dialog.PreviewKeyUp -= OnHotkeyDialogPreviewKeyUp;
            _hotkeyDialog = null;
            _hotkeyDialogKeycaps = null;
            _hotkeyDialogStatus = null;
            _hotkeyCaptureBox = null;
            _pressedHotkeyModifiers = 0;
            EditHotkeyButton.Focus(FocusState.Programmatic);
        }
    }

    private void OnHotkeyDialogPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var key = (uint)e.Key;
        var modifier = GetHotkeyModifierFlag(key);

        if (modifier != 0)
        {
            _pressedHotkeyModifiers |= modifier;
            _editingHotkeyModifiers = _pressedHotkeyModifiers;
            _editingHotkeyKey = 0;
        }
        else if (key <= byte.MaxValue)
        {
            _editingHotkeyModifiers = _pressedHotkeyModifiers;
            _editingHotkeyKey = key;
        }

        if (_hotkeyDialogKeycaps != null)
            RenderHotkeyKeycaps(_hotkeyDialogKeycaps, _editingHotkeyModifiers, _editingHotkeyKey, true);

        var isValid = IsValidHotkey(_editingHotkeyModifiers, _editingHotkeyKey);
        if (_hotkeyDialog != null)
            _hotkeyDialog.IsPrimaryButtonEnabled = isValid;
        SetHotkeyDialogStatus(GetHotkeyEditorStrings().Hint, !isValid);
        _hotkeyCaptureBox?.Focus(FocusState.Keyboard);
        e.Handled = true;
    }

    private void OnHotkeyDialogPreviewKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var modifier = GetHotkeyModifierFlag((uint)e.Key);
        if (modifier == 0) return;

        _pressedHotkeyModifiers &= ~modifier;
        if (_editingHotkeyKey == 0)
        {
            _editingHotkeyModifiers = _pressedHotkeyModifiers;
            if (_hotkeyDialogKeycaps != null)
                RenderHotkeyKeycaps(_hotkeyDialogKeycaps, _editingHotkeyModifiers, 0, true);
        }

        e.Handled = true;
    }

    private void RenderHotkeyKeycaps(StackPanel panel, uint modifiers, uint key, bool large)
    {
        panel.Children.Clear();
        var parts = GetHotkeyParts(modifiers, key);
        if (parts.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = large ? 22 : 16,
                Text = "—"
            });
            return;
        }

        var style = (Style)Resources[large ? "HotkeyDialogKeycapStyle" : "HotkeyKeycapStyle"];
        foreach (var part in parts)
            panel.Children.Add(new Button { Content = part, Style = style });
    }

    private void SetHotkeyDialogStatus(string text, bool isError)
    {
        if (_hotkeyDialogStatus == null) return;
        _hotkeyDialogStatus.Text = text;
        _hotkeyDialogStatus.Foreground = isError
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private static bool IsModifierKey(uint key)
    {
        return GetHotkeyModifierFlag(key) != 0;
    }

    private static uint GetHotkeyModifierFlag(uint key)
    {
        return key switch
        {
            0x10 or 0xA0 or 0xA1 => 1,
            0x11 or 0xA2 or 0xA3 => 2,
            0x12 or 0xA4 or 0xA5 => 4,
            0x5B or 0x5C => 8,
            _ => 0
        };
    }

    private static bool IsValidHotkey(uint modifiers, uint key)
    {
        return modifiers != 0 && key is > 0 and <= byte.MaxValue && !IsModifierKey(key);
    }

    private static System.Collections.Generic.List<string> GetHotkeyParts(uint modifiers, uint key)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & 8) != 0) parts.Add("Win");
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 4) != 0) parts.Add("Alt");
        if ((modifiers & 1) != 0) parts.Add("Shift");
        if (key != 0) parts.Add(GetHotkeyKeyName(key));
        return parts;
    }

    private static string GetHotkeyKeyName(uint key)
    {
        return key switch
        {
            >= 0x30 and <= 0x39 => ((char)key).ToString(),
            >= 0x41 and <= 0x5A => ((char)key).ToString(),
            >= 0x70 and <= 0x87 => "F" + (key - 0x70 + 1),
            0x08 => "Back",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PgUp",
            0x22 => "PgDn",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Ins",
            0x2E => "Del",
            _ => ((Windows.System.VirtualKey)key).ToString()
        };
    }

    private static (string Prompt, string Hint, string Save, string Cancel, string Conflict) GetHotkeyEditorStrings()
    {
        return (
            CoreService.GetString(StrId.HotkeyPrompt) ?? "Press a new key combination",
            CoreService.GetString(StrId.HotkeyHint) ?? "The shortcut must include Win, Ctrl, Alt, or Shift.",
            CoreService.GetString(StrId.Save) ?? "Save",
            CoreService.GetString(StrId.Cancel) ?? "Cancel",
            CoreService.GetString(StrId.HotkeyConflict) ?? "This shortcut is already in use.");
    }

    private static string GetLocaleDisplayName(string localeName) => localeName switch
    {
        "Arabic" => "العربية",
        "Bulgarian" => "Български",
        "Catalan" => "Català",
        "Chinese (Simplified)" => "简体中文",
        "Chinese (Traditional)" => "繁體中文",
        "Czech" => "Čeština",
        "Dutch" => "Nederlands",
        "French" => "Français",
        "German" => "Deutsch",
        "Hebrew" => "עברית",
        "Hungarian" => "Magyar",
        "Indonesian" => "Bahasa Indonesia",
        "Italian" => "Italiano",
        "Japanese" => "日本語",
        "Kazakh" => "Қазақша",
        "Korean" => "한국어",
        "Persian" => "فارسی",
        "Polish" => "Polski",
        "Portuguese (Brazil)" => "Português (Brasil)",
        "Portuguese" => "Português",
        "Romanian" => "Română",
        "Russian" => "Русский",
        "Serbian (Cyrillic)" => "Српски (ћирилица)",
        "Serbian (Latin)" => "Srpski (latinica)",
        "Slovak" => "Slovenčina",
        "Spanish" => "Español",
        "Swedish" => "Svenska",
        "Turkish" => "Türkçe",
        "Ukrainian" => "Українська",
        "Vietnamese" => "Tiếng Việt",
        _ => localeName
    };

}
