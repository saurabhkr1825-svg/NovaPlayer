using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nova.App.ViewModels;
using Nova.App.Views.Dialogs;
using Nova.Core.Models;

namespace Nova.App.Views;

public partial class MainWindow : Window
{
    private readonly PlayerViewModel _viewModel;
    private WindowStyle _previousStyle = WindowStyle.SingleBorderWindow;
    private WindowState _previousState = WindowState.Normal;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;

    private bool _isMiniPlayerActive;
    private double _normalWidth = 1160;
    private double _normalHeight = 720;
    private double _normalLeft = 100;
    private double _normalTop = 100;
    private WindowStyle _normalStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _normalResizeMode = ResizeMode.CanResize;
    private bool _normalTopmost;

    public MainWindow(PlayerViewModel viewModel, LibraryViewModel? libraryViewModel = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        if (libraryViewModel != null)
        {
            _viewModel.Library = libraryViewModel;
            libraryViewModel.PlayMediaRequested += async (s, path) =>
            {
                _viewModel.ActiveTabIndex = 0;
                await _viewModel.PlayFileAsync(path);
            };
            libraryViewModel.PlayQueueRequested += (s, req) =>
            {
                _viewModel.ActiveTabIndex = 0;
                _viewModel.SetQueue(req.items, req.startIndex);
            };
            _ = libraryViewModel.InitializeAsync();
        }

        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ShowMediaInfoRequested += OnShowMediaInfoRequested;
        _viewModel.ShowEqualizerRequested += OnShowEqualizerRequested;
        _viewModel.OpenUrlRequested += OnOpenUrlRequested;
    }

    private void OnShowMediaInfoRequested(MediaDetails details)
    {
        var dialog = new MediaInfoDialog(details)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnShowEqualizerRequested()
    {
        var dialog = new EqualizerDialog(_viewModel.Equalizer)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnOpenUrlRequested()
    {
        var dialog = new OpenUrlDialog
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ValidatedUrl))
        {
            _viewModel.ActiveTabIndex = 0;
            _ = _viewModel.PlayUrlAsync(dialog.ValidatedUrl);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
        {
            ApplyFullscreenState(_viewModel.IsFullscreen);
        }
        else if (e.PropertyName == nameof(PlayerViewModel.IsMiniPlayer))
        {
            ApplyMiniPlayerState(_viewModel.IsMiniPlayer);
        }
    }

    private void ApplyMiniPlayerState(bool isMini)
    {
        if (isMini)
        {
            if (!_isMiniPlayerActive)
            {
                _isMiniPlayerActive = true;
                _normalWidth = Width;
                _normalHeight = Height;
                _normalLeft = Left;
                _normalTop = Top;
                _normalStyle = WindowStyle;
                _normalResizeMode = ResizeMode;
                _normalTopmost = Topmost;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.CanResizeWithGrip;
                Topmost = true;

                Width = 420;
                Height = 240;

                var workArea = SystemParameters.WorkArea;
                Left = Math.Max(0, workArea.Right - Width - 30);
                Top = Math.Max(0, workArea.Bottom - Height - 50);
            }
        }
        else
        {
            if (_isMiniPlayerActive)
            {
                _isMiniPlayerActive = false;
                Topmost = _normalTopmost;
                WindowStyle = _normalStyle;
                ResizeMode = _normalResizeMode;
                Width = _normalWidth > 100 ? _normalWidth : 1160;
                Height = _normalHeight > 100 ? _normalHeight : 720;
                Left = _normalLeft;
                Top = _normalTop;
            }
        }
    }

    private void OnMiniPlayerHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMiniPlayerCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
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
            if (e.OriginalSource is Grid || e.OriginalSource is LibVLCSharp.WPF.VideoView)
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
        // Don't intercept shortcuts when typing in a TextBox (e.g. search box)
        if (e.OriginalSource is TextBox) return;

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
                _viewModel.SetVolumeCommand.Execute(Math.Min(100, _viewModel.Volume + 5));
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.SetVolumeCommand.Execute(Math.Max(0, _viewModel.Volume - 5));
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
                if (_viewModel.IsMiniPlayer)
                {
                    _viewModel.ExitMiniPlayer();
                    e.Handled = true;
                }
                else if (_viewModel.IsFullscreen)
                {
                    _viewModel.ExitFullscreenCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.I:
                _viewModel.ShowMediaInfoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.U:
                _viewModel.OpenUrl();
                e.Handled = true;
                break;

            case Key.E:
                _viewModel.ShowEqualizer();
                e.Handled = true;
                break;

            case Key.T:
                _ = _viewModel.ToggleThemeAsync();
                e.Handled = true;
                break;

            case Key.F9:
                _viewModel.ToggleMiniPlayer();
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
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    _ = _viewModel.TakeSnapshotAsync();
                }
                else
                {
                    CycleSubtitleTrack();
                }
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

            case Key.L:
                _viewModel.ActiveTabIndex = _viewModel.ActiveTabIndex == 0 ? 1 : 0;
                e.Handled = true;
                break;

            case Key.N:
                _viewModel.PlayNextTrackCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.P:
                _viewModel.PlayPreviousTrackCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.R:
                _viewModel.CycleRepeatModeCommand.Execute(null);
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
            delayReset.Click += (s, ev) => _viewModel.AdjustAudioDelayCommand.Execute(0 - _viewModel.AudioDelayMs);
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
            loadItem.Click += (s, ev) => _viewModel.LoadExternalSubtitleCommand.Execute(null);
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
            delayReset.Click += (s, ev) => _viewModel.AdjustSubtitleDelayCommand.Execute(0 - _viewModel.SubtitleDelayMs);
            menu.Items.Add(delayReset);

            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
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
            menu.PlacementTarget = button;
            menu.IsOpen = true;
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
                _viewModel.ActiveTabIndex = 0;
                await _viewModel.PlayFileAsync(files[0]);
            }
        }
    }

    // Media Library Events
    private async void OnMediaItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView lv && lv.SelectedItem is MediaItem item)
        {
            _viewModel.ActiveTabIndex = 0;
            await _viewModel.PlayFileAsync(item.FilePathOrUrl);
        }
    }

    private async void OnPlaySelectedMediaItemClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedMediaItem != null)
        {
            _viewModel.ActiveTabIndex = 0;
            await _viewModel.PlayFileAsync(_viewModel.Library.SelectedMediaItem.FilePathOrUrl);
        }
    }

    private void OnToggleFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedMediaItem != null)
        {
            _ = _viewModel.Library.ToggleFavoriteCommand.ExecuteAsync(_viewModel.Library.SelectedMediaItem);
        }
    }

    private void OnOpenMediaLocationClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedMediaItem != null)
        {
            _viewModel.Library.OpenFileLocationCommand.Execute(_viewModel.Library.SelectedMediaItem.FilePathOrUrl);
        }
    }

    private void OnRemoveMediaItemClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedMediaItem != null)
        {
            _ = _viewModel.Library.DeleteMediaItemCommand.ExecuteAsync(_viewModel.Library.SelectedMediaItem);
        }
    }

    // Playlist Events
    private void OnNewPlaylistButtonClick(object sender, RoutedEventArgs e)
    {
        var defaultName = $"Playlist {(_viewModel.Library?.Playlists.Count ?? 0) + 1}";
        var dialog = new InputDialog("New Playlist — NovaPlayer", "Enter playlist name:", defaultName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            _ = _viewModel.Library?.CreatePlaylistCommand.ExecuteAsync(dialog.InputText);
        }
    }

    private void OnPlayCurrentPlaylistClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylist != null)
        {
            _viewModel.Library.PlayPlaylistCommand.Execute(_viewModel.Library.SelectedPlaylist);
        }
    }

    private void OnShufflePlaylistClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylist != null)
        {
            _ = _viewModel.Library.ShufflePlaylistCommand.ExecuteAsync(_viewModel.Library.SelectedPlaylist);
        }
    }

    private void OnExportPlaylistClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylist != null)
        {
            _ = _viewModel.Library.ExportPlaylistDialogCommand.ExecuteAsync(_viewModel.Library.SelectedPlaylist);
        }
    }

    private void OnDeletePlaylistClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylist != null)
        {
            _ = _viewModel.Library.DeletePlaylistCommand.ExecuteAsync(_viewModel.Library.SelectedPlaylist);
        }
    }

    private async void OnPlaylistItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView lv && lv.SelectedItem is PlaylistItem item && item.MediaItem != null)
        {
            _viewModel.ActiveTabIndex = 0;
            await _viewModel.PlayFileAsync(item.MediaItem.FilePathOrUrl);
        }
    }

    private async void OnPlaySelectedPlaylistItemClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylistItem?.MediaItem != null)
        {
            _viewModel.ActiveTabIndex = 0;
            await _viewModel.PlayFileAsync(_viewModel.Library.SelectedPlaylistItem.MediaItem.FilePathOrUrl);
        }
    }

    private void OnMoveItemUpClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylistItem != null)
        {
            _ = _viewModel.Library.MovePlaylistItemUpCommand.ExecuteAsync(_viewModel.Library.SelectedPlaylistItem);
        }
    }

    private void OnMoveItemDownClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylistItem != null)
        {
            _ = _viewModel.Library.MovePlaylistItemDownCommand.ExecuteAsync(_viewModel.Library.SelectedPlaylistItem);
        }
    }

    private void OnRemovePlaylistItemClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedPlaylistItem != null)
        {
            _ = _viewModel.Library.RemovePlaylistItemCommand.ExecuteAsync(_viewModel.Library.SelectedPlaylistItem);
        }
    }

    // Playback History Events
    private async void OnHistoryItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView lv && lv.SelectedItem is PlaybackHistoryEntry entry && !string.IsNullOrWhiteSpace(entry.FilePathOrUrl))
        {
            _viewModel.ActiveTabIndex = 0;
            await _viewModel.PlayFileAsync(entry.FilePathOrUrl);
        }
    }

    private async void OnPlayHistoryItemClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedHistoryEntry != null && !string.IsNullOrWhiteSpace(_viewModel.Library.SelectedHistoryEntry.FilePathOrUrl))
        {
            _viewModel.ActiveTabIndex = 0;
            await _viewModel.PlayFileAsync(_viewModel.Library.SelectedHistoryEntry.FilePathOrUrl);
        }
    }

    private void OnDeleteHistoryItemClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Library?.SelectedHistoryEntry != null)
        {
            _ = _viewModel.Library.DeleteHistoryItemCommand.ExecuteAsync(_viewModel.Library.SelectedHistoryEntry);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ShowMediaInfoRequested -= OnShowMediaInfoRequested;
        _viewModel.ShowEqualizerRequested -= OnShowEqualizerRequested;
        _viewModel.OpenUrlRequested -= OnOpenUrlRequested;
        _viewModel.Library?.Dispose();
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}
