using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    private void OnRegionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SaveMask();
    }

    private void OnAutoCleanChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        NbAutoClean.IsEnabled = ChkAutoClean.IsChecked == true;
        IniConfig.WriteBool("AutoreductEnable", ChkAutoClean.IsChecked == true);
    }

    private void OnIntervalCleanChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        NbInterval.IsEnabled = ChkIntervalClean.IsChecked == true;
        IniConfig.WriteBool("AutoreductIntervalEnable", ChkIntervalClean.IsChecked == true);
    }

    private void OnAutoCleanValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        IniConfig.WriteUInt("AutoreductValue", (uint)args.NewValue);
    }

    private void OnIntervalValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        IniConfig.WriteUInt("AutoreductIntervalValue", (uint)args.NewValue);
    }

    private void OnBoolChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, ChkAlwaysOnTop))
            IniConfig.WriteBool("AlwaysOnTop", ChkAlwaysOnTop.IsChecked == true);
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
    }
}
