using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nova.App.ViewModels;

namespace Nova.App.Views;

public partial class MainWindow : Window
{
    private readonly PlayerViewModel _viewModel;
    private WindowStyle _previousStyle = WindowStyle.SingleBorderWindow;
    private WindowState _previousState = WindowState.Normal;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;

    public MainWindow(PlayerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ShowMediaInfoRequested += OnShowMediaInfoRequested;
    }

    private void OnShowMediaInfoRequested(Nova.Core.Models.MediaDetails details)
    {
        var dialog = new Dialogs.MediaInfoDialog(details)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
        {
            ApplyFullscreenState(_viewModel.IsFullscreen);
        }
    }

    private void ApplyFullscreenState(bool isFullscreen)
    {
        if (isFullscreen)
        {
            _previousStyle = WindowStyle;
            _previousState = WindowState;
            _previousResizeMode = ResizeMode;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = false;
        }
        else
        {
            WindowStyle = _previousStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousState == WindowState.Maximized ? WindowState.Normal : _previousState;
            Topmost = false;
        }
    }

    private void OnVideoSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.TriggerUserActivity();

        if (e.ClickCount == 2)
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.ClickCount == 1)
        {
            // Single click on empty video surface toggles play/pause
            if (e.OriginalSource is System.Windows.Controls.Grid || e.OriginalSource is LibVLCSharp.WPF.VideoView)
            {
                _viewModel.PlayPauseCommand.Execute(null);
            }
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        _viewModel.TriggerUserActivity();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        _viewModel.TriggerUserActivity();

        switch (e.Key)
        {
            case Key.Space:
                _viewModel.PlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left:
                _viewModel.SeekRelativeCommand.Execute(-5000L);
                e.Handled = true;
                break;

            case Key.Right:
                _viewModel.SeekRelativeCommand.Execute(5000L);
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.VolumeRelativeCommand.Execute(5);
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.VolumeRelativeCommand.Execute(-5);
                e.Handled = true;
                break;

            case Key.M:
                _viewModel.ToggleMuteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F:
            case Key.F11:
                _viewModel.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                if (_viewModel.IsFullscreen)
                {
                    _viewModel.ExitFullscreenCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.I:
                _viewModel.ShowMediaInfoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemOpenBrackets: // '['
                _viewModel.SetPlaybackSpeedCommand.Execute(Math.Max(0.25f, _viewModel.PlaybackSpeed - 0.25f));
                e.Handled = true;
                break;

            case Key.OemCloseBrackets: // ']'
                _viewModel.SetPlaybackSpeedCommand.Execute(Math.Min(4.0f, _viewModel.PlaybackSpeed + 0.25f));
                e.Handled = true;
                break;

            case Key.Back:
                _viewModel.SetPlaybackSpeedCommand.Execute(1.0f);
                e.Handled = true;
                break;

            case Key.A:
                CycleAudioTrack();
                e.Handled = true;
                break;

            case Key.S:
                CycleSubtitleTrack();
                e.Handled = true;
                break;

            case Key.G:
                _viewModel.AdjustAudioDelayCommand.Execute(-50L);
                e.Handled = true;
                break;

            case Key.H:
                _viewModel.AdjustAudioDelayCommand.Execute(50L);
                e.Handled = true;
                break;

            case Key.J:
                _viewModel.AdjustSubtitleDelayCommand.Execute(-50L);
                e.Handled = true;
                break;

            case Key.K:
                _viewModel.AdjustSubtitleDelayCommand.Execute(50L);
                e.Handled = true;
                break;
        }
    }

    private void CycleAudioTrack()
    {
        if (_viewModel.AudioTracks.Count <= 1) return;

        var currentIndex = -1;
        for (int i = 0; i < _viewModel.AudioTracks.Count; i++)
        {
            if (_viewModel.AudioTracks[i].Id == _viewModel.SelectedAudioTrackId)
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = (currentIndex + 1) % _viewModel.AudioTracks.Count;
        _viewModel.SelectAudioTrackCommand.Execute(_viewModel.AudioTracks[nextIndex].Id);
    }

    private void CycleSubtitleTrack()
    {
        if (_viewModel.SubtitleTracks.Count <= 1) return;

        var currentIndex = -1;
        for (int i = 0; i < _viewModel.SubtitleTracks.Count; i++)
        {
            if (_viewModel.SubtitleTracks[i].Id == _viewModel.SelectedSubtitleTrackId)
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = (currentIndex + 1) % _viewModel.SubtitleTracks.Count;
        _viewModel.SelectSubtitleTrackCommand.Execute(_viewModel.SubtitleTracks[nextIndex].Id);
    }

    private void OnAudioMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var menu = new ContextMenu();

            if (_viewModel.AudioTracks.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "No audio tracks detected", IsEnabled = false });
            }
            else
            {
                foreach (var track in _viewModel.AudioTracks)
                {
                    var item = new MenuItem
                    {
                        Header = track.ToString(),
                        IsCheckable = true,
                        IsChecked = track.Id == _viewModel.SelectedAudioTrackId
                    };
                    var trackId = track.Id;
                    item.Click += (s, ev) => _viewModel.SelectAudioTrackCommand.Execute(trackId);
                    menu.Items.Add(item);
                }
            }

            menu.Items.Add(new Separator());
            var delayHeader = new MenuItem { Header = $"Audio Delay: {_viewModel.AudioDelayMs} ms", IsEnabled = false };
            menu.Items.Add(delayHeader);

            var delayPlus = new MenuItem { Header = "Advance (+50 ms) [H]" };
            delayPlus.Click += (s, ev) => _viewModel.AdjustAudioDelayCommand.Execute(50L);
            menu.Items.Add(delayPlus);

            var delayMinus = new MenuItem { Header = "Delay (-50 ms) [G]" };
            delayMinus.Click += (s, ev) => _viewModel.AdjustAudioDelayCommand.Execute(-50L);
            menu.Items.Add(delayMinus);

            var delayReset = new MenuItem { Header = "Reset Delay (0 ms)" };
            delayReset.Click += (s, ev) => _viewModel.ResetAudioDelayCommand.Execute(null);
            menu.Items.Add(delayReset);

            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnSubtitleMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var menu = new ContextMenu();

            var loadItem = new MenuItem { Header = "📂 Load External Subtitle File..." };
            loadItem.Click += (s, ev) => _viewModel.AddExternalSubtitleCommand.Execute(null);
            menu.Items.Add(loadItem);
            menu.Items.Add(new Separator());

            if (_viewModel.SubtitleTracks.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "No subtitles available", IsEnabled = false });
            }
            else
            {
                foreach (var track in _viewModel.SubtitleTracks)
                {
                    var item = new MenuItem
                    {
                        Header = track.ToString(),
                        IsCheckable = true,
                        IsChecked = track.Id == _viewModel.SelectedSubtitleTrackId
                    };
                    var trackId = track.Id;
                    item.Click += (s, ev) => _viewModel.SelectSubtitleTrackCommand.Execute(trackId);
                    menu.Items.Add(item);
                }
            }

            menu.Items.Add(new Separator());
            var delayHeader = new MenuItem { Header = $"Subtitle Delay: {_viewModel.SubtitleDelayMs} ms", IsEnabled = false };
            menu.Items.Add(delayHeader);

            var delayPlus = new MenuItem { Header = "Advance (+50 ms) [K]" };
            delayPlus.Click += (s, ev) => _viewModel.AdjustSubtitleDelayCommand.Execute(50L);
            menu.Items.Add(delayPlus);

            var delayMinus = new MenuItem { Header = "Delay (-50 ms) [J]" };
            delayMinus.Click += (s, ev) => _viewModel.AdjustSubtitleDelayCommand.Execute(-50L);
            menu.Items.Add(delayMinus);

            var delayReset = new MenuItem { Header = "Reset Delay (0 ms)" };
            delayReset.Click += (s, ev) => _viewModel.ResetSubtitleDelayCommand.Execute(null);
            menu.Items.Add(delayReset);

            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        // Right click opens speed menu
        if (sender is Button button && button.ContextMenu == null)
        {
            var menu = new ContextMenu();
            foreach (var speed in PlayerViewModel.AvailablePlaybackSpeeds)
            {
                var s = speed;
                var item = new MenuItem
                {
                    Header = $"{s:0.##}×",
                    IsCheckable = true,
                    IsChecked = Math.Abs(_viewModel.PlaybackSpeed - s) < 0.05f
                };
                item.Click += (menuSender, ev) => _viewModel.SetPlaybackSpeedCommand.Execute(s);
                menu.Items.Add(item);
            }
            button.ContextMenu = menu;
        }
    }

    private void OnAspectRatioButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var menu = new ContextMenu();
            foreach (var ratio in PlayerViewModel.AvailableAspectRatios)
            {
                var r = ratio;
                var item = new MenuItem
                {
                    Header = r,
                    IsCheckable = true,
                    IsChecked = _viewModel.SelectedAspectRatio.Equals(r, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (s, ev) => _viewModel.SetAspectRatioCommand.Execute(r);
                menu.Items.Add(item);
            }
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnSeekSliderMouseDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.StartSeek();
    }

    private void OnSeekSliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        _viewModel.EndSeekCommand.Execute(SeekSlider.Value);
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                await _viewModel.PlayFileAsync(files[0]);
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ShowMediaInfoRequested -= OnShowMediaInfoRequested;
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}
