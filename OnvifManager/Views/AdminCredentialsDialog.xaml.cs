using System.Windows;

namespace OnvifManager.Views;

// Prompts for the camera's web-admin account (used for vendor ISAPI traffic), shown when
// the user switches a profiled camera into Full mode and no admin creds are stored yet.
public partial class AdminCredentialsDialog : Window
{
    public string ResultUsername { get; private set; } = string.Empty;
    public string ResultPassword { get; private set; } = string.Empty;

    public AdminCredentialsDialog(string cameraLabel, string initialUsername)
    {
        InitializeComponent();
        Title = $"Веб-админка — {cameraLabel}";
        UserBox.Text = initialUsername;
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(UserBox.Text)) UserBox.Focus();
            else PwdBox.Focus();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var user = (UserBox.Text ?? "").Trim();
        var pwd = PwdBox.Password;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pwd))
        {
            ErrorText.Text = "Укажите пользователя и пароль, либо нажмите «Пропустить».";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        ResultUsername = user;
        ResultPassword = pwd;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
