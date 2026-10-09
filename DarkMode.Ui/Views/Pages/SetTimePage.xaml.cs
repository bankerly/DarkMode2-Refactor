using System.Windows;
using System.Windows.Controls;
using DarkMode.Ui.Models;
using DarkMode.Ui.ViewModels.Pages;

namespace DarkMode.Ui.Views.Pages;

public partial class SetTimePage : Page
{
    public SetTimePage(SetTimePageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();

        Loaded += (_, _) => ViewModel.StartMonitoring();
        Unloaded += (_, _) => ViewModel.StopMonitoring();
    }

    public SetTimePageViewModel ViewModel { get; }

    private void OnCustomTimeClick(object sender, RoutedEventArgs e) => ViewModel.SelectMode(TimeMode.CustomTime);

    private void OnSunriseSunsetClick(object sender, RoutedEventArgs e) => ViewModel.SelectMode(TimeMode.SunriseSunset);

    private void OnSensorClick(object sender, RoutedEventArgs e) => ViewModel.SelectMode(TimeMode.Sensor);
}
