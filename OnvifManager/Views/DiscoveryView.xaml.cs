using System.Windows;
using System.Windows.Controls;
using OnvifManager.ViewModels;

namespace OnvifManager.Views;

public partial class DiscoveryView : UserControl
{
    public DiscoveryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ManualPasswordBox != null)
            ManualPasswordBox.PasswordChanged += (s, args) =>
            {
                if (DataContext is DiscoveryViewModel vm)
                    vm.ManualPassword = ManualPasswordBox.Password;
            };
    }
}
