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

    private WindowState _preFullscreenState = WindowState.Normal;
    private Rect _preFullscreenBounds;

    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _vm = mainViewModel;
        DataContext = mainViewModel;
        mainViewModel.Discovery.AddManualRequested += OnAddManualRequested;
        mainViewModel.PropertyChanged += OnVmPropertyChanged;
        Closed += (_, _) =>
        {
            mainViewModel.Discovery.AddManualRequested -= OnAddManualRequested;
            mainViewModel.PropertyChanged -= OnVmPropertyChanged;
        };
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsFullscreen)) return;
        if (_vm.IsFullscreen)
        {
            _preFullscreenState = WindowState;
            _preFullscreenBounds = new Rect(Left, Top, Width, Height);
            if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
        else
        {
            Left = _preFullscreenBounds.Left;
            Top = _preFullscreenBounds.Top;
            Width = _preFullscreenBounds.Width;
            Height = _preFullscreenBounds.Height;
            WindowState = _preFullscreenState;
        }
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
            _logWindow = new LogWindow { Owner = this };
            _logWindow.Closed += (_, _) => _logWindow = null;
        }
        _logWindow.Show();
        _logWindow.Activate();
    }

    private void OpenLog_Click(object sender, MouseButtonEventArgs e) => OpenLog_Click(sender, (RoutedEventArgs)e);
}
