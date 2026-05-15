using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnvifManager.ViewModels;

public partial class PtzViewModel : ObservableObject
{
    [ObservableProperty] private string _capabilities = "Pan, Tilt, Zoom, Focus";
    [ObservableProperty] private double _panSpeed = 70;
    [ObservableProperty] private double _tiltSpeed = 70;
    [ObservableProperty] private double _zoomSpeed = 50;

    [ObservableProperty] private string _preset1 = "Главный вход";
    [ObservableProperty] private string _preset2 = "Парковка";
    [ObservableProperty] private string _preset3 = "Периметр";

    [ObservableProperty] private bool _patrolEnabled;
    [ObservableProperty] private int _patrolDelay = 10;

    [ObservableProperty] private string _statusText = "PTZ (stub) — ONVIF не подключён";

    [RelayCommand]
    private void Apply() => StatusText = "Применено (stub)";

    [RelayCommand]
    private void Cancel() => StatusText = "Отменено (stub)";
}
