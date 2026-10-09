using System.Collections.ObjectModel;
using DarkMode.Ui.Models;
using DarkMode.Ui.Views.Pages;
using MaterialDesignThemes.Wpf;

namespace DarkMode.Ui.ViewModels.Windows;

public partial class SettingsWindowViewModel : ViewModel
{
    public ObservableCollection<NavItem> NavItems { get; } =
    [
        new("时间模式", PackIconKind.ClockOutline, typeof(SetTimePage)),
        new("设置", PackIconKind.CogOutline, typeof(SettingsPage)),
        new("关于", PackIconKind.InformationOutline, typeof(AboutPage))
    ];
}
