using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnvifManager.ViewModels;

public partial class EventsViewModel : ObservableObject
{
    [ObservableProperty] private bool _motionEnabled = true;
    [ObservableProperty] private double _motionSensitivity = 60;

    [ObservableProperty] private bool _lineCrossing = true;
    [ObservableProperty] private bool _intrusion;
    [ObservableProperty] private bool _objectLeft;
    [ObservableProperty] private bool _faceRecognition;

    [ObservableProperty] private bool _emailNotify = true;
    [ObservableProperty] private bool _alarmSound;
    [ObservableProperty] private bool _ftpUpload;

    [ObservableProperty] private string _statusText = "События (stub) — ONVIF Event Service не подключён";

    [RelayCommand]
    private void Apply() => StatusText = "Применено (stub)";

    [RelayCommand]
    private void Cancel() => StatusText = "Отменено (stub)";
}
