using System.IO;
using System.Windows;
using OnvifManager.Services;

namespace OnvifManager.Views;

public partial class SettingsDialog : Window
{
    private readonly AppSettingsService _settings;

    public SettingsDialog(AppSettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        AutoPlayChk.IsChecked = settings.AutoPlayOnSelect;
        RecordingsPathBox.Text = settings.RecordingsPath;
        DefaultHint.Text = $"по умолчанию: {AppSettings.DefaultRecordingsPath()}";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Папка для записи видео",
            InitialDirectory = Directory.Exists(RecordingsPathBox.Text)
                ? RecordingsPathBox.Text
                : AppSettings.DefaultRecordingsPath()
        };
        if (dlg.ShowDialog(this) == true)
            RecordingsPathBox.Text = dlg.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var path = (RecordingsPathBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(path)) path = AppSettings.DefaultRecordingsPath();
        _settings.RecordingsPath = path;
        _settings.AutoPlayOnSelect = AutoPlayChk.IsChecked == true;
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
