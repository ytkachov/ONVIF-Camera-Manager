using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.ViewModels;

public partial class VideoConfigViewModel : ObservableObject
{
    private readonly DiscoveryViewModel _discovery;
    private CameraDevice? _camera;

    [ObservableProperty]
    private ObservableCollection<CameraProfile> _profiles = new();

    [ObservableProperty]
    private CameraProfile? _selectedProfile;

    [ObservableProperty]
    private ObservableCollection<VideoEncoderConfig> _encoderConfigs = new();

    [ObservableProperty]
    private VideoEncoderConfig? _selectedEncoder;

    [ObservableProperty]
    private string _encoding = "H264";

    [ObservableProperty]
    private int _width = 1920;

    [ObservableProperty]
    private int _height = 1080;

    [ObservableProperty]
    private int _frameRateLimit = 30;

    [ObservableProperty]
    private int _bitrateLimit = 4096;

    [ObservableProperty]
    private int _encodingInterval = 1;

    [ObservableProperty]
    private string _govLength = "30";

    [ObservableProperty]
    private string _h264Profile = "High";

    [ObservableProperty]
    private VideoQualityType _quality;

    [ObservableProperty]
    private string _streamUri = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "";

    public VideoConfigViewModel(DiscoveryViewModel discovery)
    {
        _discovery = discovery;
        _discovery.CameraSelected += OnCameraChanged;
    }

    private void OnCameraChanged() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        _camera = _discovery.SelectedCamera;
        if (_camera == null) return;

        IsLoading = true;
        StatusText = "Loading video configurations...";

        try
        {
            var client = new OnvifClient(_camera);
            var mediaService = new MediaService(client);

            var profiles = await mediaService.GetProfilesAsync();
            Profiles = new ObservableCollection<CameraProfile>(profiles);

            var configs = await mediaService.GetAllVideoEncoderConfigurationsAsync();
            EncoderConfigs = new ObservableCollection<VideoEncoderConfig>(configs);

            if (configs.Count > 0)
            {
                SelectedEncoder = configs[0];
                LoadEncoderIntoFields(configs[0]);
            }

            if (profiles.Count > 0)
            {
                SelectedProfile = profiles[0];
                try
                {
                    StreamUri = await mediaService.GetStreamUriAsync(profiles[0].Token);
                }
                catch
                {
                    StreamUri = "Unable to retrieve stream URI";
                }
            }

            StatusText = "Configuration loaded";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadEncoderIntoFields(VideoEncoderConfig config)
    {
        Encoding = config.Encoding;
        Width = config.Width;
        Height = config.Height;
        FrameRateLimit = config.FrameRateLimit;
        BitrateLimit = config.BitrateLimit;
        EncodingInterval = config.EncodingInterval;
        GovLength = config.GovLength;
        H264Profile = config.H264Profile;
        Quality = config.Quality;
    }

    partial void OnSelectedEncoderChanged(VideoEncoderConfig? value)
    {
        if (value != null)
            LoadEncoderIntoFields(value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedEncoder == null || _camera == null) return;

        IsLoading = true;
        StatusText = "Saving configuration...";

        try
        {
            SelectedEncoder.Encoding = Encoding;
            SelectedEncoder.Width = Width;
            SelectedEncoder.Height = Height;
            SelectedEncoder.FrameRateLimit = FrameRateLimit;
            SelectedEncoder.BitrateLimit = BitrateLimit;
            SelectedEncoder.EncodingInterval = EncodingInterval;
            SelectedEncoder.GovLength = GovLength;
            SelectedEncoder.H264Profile = H264Profile;
            SelectedEncoder.Quality = Quality;

            var client = new OnvifClient(_camera);
            var mediaService = new MediaService(client);
            await mediaService.SetVideoEncoderConfigurationAsync(SelectedEncoder);

            StatusText = "Configuration saved successfully";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back() => _discovery.SelectedCamera = null;
}
