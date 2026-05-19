using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OnvifManager.Services;
using OnvifManager.ViewModels;

namespace OnvifManager.Views;

public partial class VideoView : UserControl
{
    private VideoPlayerService? _player;

    public VideoView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        _player = vm.VideoPlayer;
        _player.PropertyChanged += OnPlayerPropertyChanged;
        ResizeVideoSurface();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_player != null)
            _player.PropertyChanged -= OnPlayerPropertyChanged;
        _player = null;
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VideoPlayerService.VideoWidth)
                            or nameof(VideoPlayerService.VideoHeight)
                            or nameof(VideoPlayerService.IsPlaying))
        {
            Dispatcher.BeginInvoke(new Action(ResizeVideoSurface));
        }
    }

    private void VideoArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizeVideoSurface();
    }

    private void ResizeVideoSurface()
    {
        if (VideoSurface == null || VideoArea == null) return;

        var cw = VideoArea.ActualWidth;
        var ch = VideoArea.ActualHeight;
        if (cw <= 0 || ch <= 0) return;

        uint vw = _player?.VideoWidth ?? 0;
        uint vh = _player?.VideoHeight ?? 0;
        var playing = _player?.IsPlaying ?? false;

        if (!playing || vw == 0 || vh == 0)
        {
            VideoSurface.Width = cw;
            VideoSurface.Height = ch;
            return;
        }

        double videoAspect = (double)vw / vh;
        double containerAspect = cw / ch;

        if (containerAspect > videoAspect)
        {
            VideoSurface.Height = ch;
            VideoSurface.Width = ch * videoAspect;
        }
        else
        {
            VideoSurface.Width = cw;
            VideoSurface.Height = cw / videoAspect;
        }
    }

    private void VideoArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (DataContext is MainViewModel vm)
        {
            vm.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }
}
