using System.Windows;
using OnvifManager.ViewModels;

namespace OnvifManager.Views;

public partial class SearchCamerasDialog : Window
{
    private readonly SearchCamerasViewModel _vm;

    public SearchCamerasDialog(SearchCamerasViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _vm.Cancel();
        Close();
    }
}
