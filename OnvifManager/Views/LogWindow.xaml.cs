using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using OnvifManager.Services;

namespace OnvifManager.Views;

public partial class LogWindow : Window
{
    private readonly DispatcherTimer _timer;

    public LogWindow()
    {
        InitializeComponent();
        PathText.Text = SoapLog.FilePath;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => LoadLog();
        Loaded += (_, _) => LoadLog();
        Closed += (_, _) => _timer.Stop();
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
    }

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

    private void LoadLog()
    {
        try
        {
            if (!File.Exists(SoapLog.FilePath))
            {
                LogText.Text = "(лог пустой — никаких SOAP-запросов ещё не было)";
                return;
            }
            using var fs = new FileStream(SoapLog.FilePath,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var atBottom = Scroller.VerticalOffset + Scroller.ViewportHeight
                           >= Scroller.ExtentHeight - 16;
            LogText.Text = sr.ReadToEnd();
            if (atBottom) Scroller.ScrollToEnd();
        }
        catch (Exception ex)
        {
            LogText.Text = $"Не удалось прочитать лог: {ex.Message}";
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadLog();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            File.WriteAllText(SoapLog.FilePath,
                $"=== cleared {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
            LoadLog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось очистить лог");
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.GetDirectoryName(SoapLog.FilePath);
            if (!string.IsNullOrEmpty(dir))
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
        catch { }
    }

    private void AutoRefresh_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoRefresh.IsChecked == true) _timer.Start();
        else _timer.Stop();
    }
}
