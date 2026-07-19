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
        LoadSettings();
    }

    public void LoadSettings()
    {
        _loading = true;

        var mask = CoreService.GetConfigMask();
        ChkWorkingSet.IsChecked = (mask & MemoryMask.WorkingSet) != 0;
        ChkSystemFileCache.IsChecked = (mask & MemoryMask.SystemFileCache) != 0;
        ChkModifiedFileCache.IsChecked = (mask & MemoryMask.ModifiedFileCache) != 0;
        ChkModifiedList.IsChecked = (mask & MemoryMask.ModifiedList) != 0;
        ChkStandbyList.IsChecked = (mask & MemoryMask.StandbyList) != 0;
        ChkStandbyPriority0.IsChecked = (mask & MemoryMask.StandbyPriority0List) != 0;
        ChkRegistryCache.IsChecked = (mask & MemoryMask.RegistryCache) != 0;
        ChkCombineLists.IsChecked = (mask & MemoryMask.CombineMemoryLists) != 0;

        ChkAutoClean.IsChecked = CoreService.GetBool("AutoreductEnable");
        NbAutoClean.Value = CoreService.GetUInt("AutoreductValue", 90);
        NbAutoClean.IsEnabled = ChkAutoClean.IsChecked == true;

        ChkIntervalClean.IsChecked = CoreService.GetBool("AutoreductIntervalEnable");
        NbInterval.Value = CoreService.GetUInt("AutoreductIntervalValue", 30);
        NbInterval.IsEnabled = ChkIntervalClean.IsChecked == true;

        ChkAlwaysOnTop.IsChecked = CoreService.GetBool("AlwaysOnTop");
        ChkStartMinimized.IsChecked = CoreService.GetBool("IsStartMinimized");
        ChkConfirmClean.IsChecked = CoreService.GetBool("IsShowReductConfirmation", true);
        ChkShowResults.IsChecked = CoreService.GetBool("BalloonCleanResults", true);

        _loading = false;
    }

    private uint GetMaskFromChecks()
    {
        uint mask = 0;
        if (ChkWorkingSet.IsChecked == true) mask |= MemoryMask.WorkingSet;
        if (ChkSystemFileCache.IsChecked == true) mask |= MemoryMask.SystemFileCache;
        if (ChkModifiedFileCache.IsChecked == true) mask |= MemoryMask.ModifiedFileCache;
        if (ChkModifiedList.IsChecked == true) mask |= MemoryMask.ModifiedList;
        if (ChkStandbyList.IsChecked == true) mask |= MemoryMask.StandbyList;
        if (ChkStandbyPriority0.IsChecked == true) mask |= MemoryMask.StandbyPriority0List;
        if (ChkRegistryCache.IsChecked == true) mask |= MemoryMask.RegistryCache;
        if (ChkCombineLists.IsChecked == true) mask |= MemoryMask.CombineMemoryLists;
        return mask;
    }

    private void OnRegionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        CoreService.SetConfigMask(GetMaskFromChecks());
    }

    private void OnAutoCleanChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        NbAutoClean.IsEnabled = ChkAutoClean.IsChecked == true;
        CoreService.SetBool("AutoreductEnable", ChkAutoClean.IsChecked == true);
    }

    private void OnIntervalCleanChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        NbInterval.IsEnabled = ChkIntervalClean.IsChecked == true;
        CoreService.SetBool("AutoreductIntervalEnable", ChkIntervalClean.IsChecked == true);
    }

    private void OnAutoCleanValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        CoreService.SetUInt("AutoreductValue", (uint)args.NewValue);
    }

    private void OnIntervalValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        CoreService.SetUInt("AutoreductIntervalValue", (uint)args.NewValue);
    }

    private void OnBoolChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, ChkAlwaysOnTop))
            CoreService.SetBool("AlwaysOnTop", ChkAlwaysOnTop.IsChecked == true);
        else if (ReferenceEquals(sender, ChkStartMinimized))
            CoreService.SetBool("IsStartMinimized", ChkStartMinimized.IsChecked == true);
        else if (ReferenceEquals(sender, ChkConfirmClean))
            CoreService.SetBool("IsShowReductConfirmation", ChkConfirmClean.IsChecked == true);
        else if (ReferenceEquals(sender, ChkShowResults))
            CoreService.SetBool("BalloonCleanResults", ChkShowResults.IsChecked == true);
    }
}
