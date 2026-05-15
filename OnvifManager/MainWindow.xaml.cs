using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OnvifManager.ViewModels;
using OnvifManager.Views;

namespace OnvifManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private LogWindow? _logWindow;

    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _vm = mainViewModel;
        DataContext = mainViewModel;
        mainViewModel.Discovery.AddManualRequested += OnAddManualRequested;
        Closed += (_, _) => mainViewModel.Discovery.AddManualRequested -= OnAddManualRequested;
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
    }

    private void OnAddManualRequested()
    {
        var dlg = new ManualAddDialog(_vm.Discovery) { Owner = this };
        dlg.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreGlyph()
    {
        if (MaxRestoreGlyph is not System.Windows.Shapes.Path p) return;
        p.Data = WindowState == WindowState.Maximized
            ? Geometry.Parse("M 2 0 L 10 0 L 10 8 M 0 2 L 8 2 L 8 10 L 0 10 Z")
            : Geometry.Parse("M 0 0 L 10 0 L 10 10 L 0 10 Z");
        MaxRestoreBtn.ToolTip = WindowState == WindowState.Maximized ? "Восстановить" : "Развернуть";
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (_logWindow == null || !_logWindow.IsLoaded)
        {
            _logWindow = new LogWindow { Owner = null };
            _logWindow.Closed += (_, _) => _logWindow = null;
        }
        _logWindow.Show();
        _logWindow.Activate();
    }

    private void OpenLog_Click(object sender, MouseButtonEventArgs e) => OpenLog_Click(sender, (RoutedEventArgs)e);
}
