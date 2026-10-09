using System.Windows.Controls;
using DarkMode.Ui.ViewModels.Pages;

namespace DarkMode.Ui.Views.Pages;

public partial class AboutPage : Page
{
    public AboutPage(AboutPageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public AboutPageViewModel ViewModel { get; }
}
