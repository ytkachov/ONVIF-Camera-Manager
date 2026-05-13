using System.Windows;
using OnvifManager.ViewModels;

namespace OnvifManager;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel;
    }
}
