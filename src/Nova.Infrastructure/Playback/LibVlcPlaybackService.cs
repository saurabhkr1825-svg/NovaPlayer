using System.Text;
using LibVLCSharp.Shared;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.Infrastructure.Playback;

public class LibVlcPlaybackService : IPlaybackService
{
    private static readonly object InitLock = new();
    private static bool _isCoreInitialized;

    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;
    private bool _isDisposed;

    private PlaybackState _state = PlaybackState.Stopped;
    private long _currentTimeMs;
    private long _totalDurationMs;
    private int _volume = 100;
    private bool _isMuted;
    private MediaItem? _currentMediaItem;

    private List<TrackInfo> _audioTracks = new();
    private List<TrackInfo> _subtitleTracks = new();
    private int _currentAudioTrackId = -1;
    private int _currentSubtitleTrackId = -1;
    private long _audioDelayMs;
    private long _subtitleDelayMs;
    private float _playbackSpeed = 1.0f;
    private string? _aspectRatio;
    private float _bufferingPercentage;
    private EqualizerSettings? _currentEqualizer;

    public PlaybackState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, _state);
            }
        }
    }

    public long CurrentTimeMs => _currentTimeMs;
    public long TotalDurationMs => _totalDurationMs;
    public int Volume => _volume;
    public bool IsMuted => _isMuted;
    public bool IsSeekable => _mediaPlayer.IsSeekable;
    public MediaItem? CurrentMedia => _currentMediaItem;

    public IReadOnlyList<TrackInfo> AudioTracks => _audioTracks.AsReadOnly();
    public int CurrentAudioTrackId => _currentAudioTrackId;
    public long AudioDelayMs => _audioDelayMs;

    public IReadOnlyList<TrackInfo> SubtitleTracks => _subtitleTracks.AsReadOnly();
    public int CurrentSubtitleTrackId => _currentSubtitleTrackId;
    public long SubtitleDelayMs => _subtitleDelayMs;

    public float PlaybackSpeed => _playbackSpeed;
    public string? AspectRatio => _aspectRatio;
    public float BufferingPercentage => _bufferingPercentage;
    public EqualizerSettings? CurrentEqualizer => _currentEqualizer;

    public object NativeMediaPlayer => _mediaPlayer;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<PlaybackPositionEventArgs>? PositionChanged;
    public event EventHandler<long>? DurationChanged;
    public event EventHandler<int>? VolumeChanged;
    public event EventHandler<bool>? MuteChanged;
    public event EventHandler<PlaybackErrorEventArgs>? ErrorOccurred;

    public event EventHandler<IReadOnlyList<TrackInfo>>? AudioTracksChanged;
    public event EventHandler<int>? AudioTrackChanged;
    public event EventHandler<long>? AudioDelayChanged;

    public event EventHandler<IReadOnlyList<TrackInfo>>? SubtitleTracksChanged;
    public event EventHandler<int>? SubtitleTrackChanged;
    public event EventHandler<long>? SubtitleDelayChanged;

    public event EventHandler<float>? PlaybackSpeedChanged;
    public event EventHandler<string?>? AspectRatioChanged;
    public event EventHandler<float>? BufferingProgressChanged;

    public LibVlcPlaybackService()
    {
        EnsureCoreInitialized();

        _libVlc = new LibVLC(enableDebugLogs: false);
        _mediaPlayer = new MediaPlayer(_libVlc);

        RegisterEvents();
    }

    private static void EnsureCoreInitialized()
    {
        if (_isCoreInitialized) return;

        lock (InitLock)
        {
            if (!_isCoreInitialized)
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                var libvlcDir = Path.Combine(baseDir, "libvlc", arch);

                if (Directory.Exists(libvlcDir))
                {
                    LibVLCSharp.Shared.Core.Initialize(libvlcDir);
                }
                else
                {
                    LibVLCSharp.Shared.Core.Initialize();
                }

                _isCoreInitialized = true;
                Log.Information("LibVLC core initialized successfully from {Path}.", libvlcDir);
            }
        }
    }

    private void RegisterEvents()
    {
        _mediaPlayer.Playing += (s, e) =>
        {
            _bufferingPercentage = 100f;
            BufferingProgressChanged?.Invoke(this, 100f);
            State = PlaybackState.Playing;
            RefreshTracks();
        };

        _mediaPlayer.Paused += (s, e) =>
        {
            State = PlaybackState.Paused;
        };

        _mediaPlayer.Stopped += (s, e) =>
        {
            _bufferingPercentage = 0f;
            State = PlaybackState.Stopped;
            ClearTracks();
        };

        _mediaPlayer.EndReached += (s, e) =>
        {
            _bufferingPercentage = 0f;
            State = PlaybackState.Ended;
        };

        _mediaPlayer.Buffering += (s, e) =>
        {
            _bufferingPercentage = e.Cache;
            BufferingProgressChanged?.Invoke(this, e.Cache);

            if (State != PlaybackState.Playing && State != PlaybackState.Paused)
            {
                State = PlaybackState.Buffering;
            }
        };

        _mediaPlayer.EncounteredError += (s, e) =>
        {
            State = PlaybackState.Error;
            Log.Error("LibVLC native playback error encountered.");
            ErrorOccurred?.Invoke(this, new PlaybackErrorEventArgs("An error occurred during media playback."));
        };

        _mediaPlayer.TimeChanged += (s, e) =>
        {
            _currentTimeMs = e.Time;
            PositionChanged?.Invoke(this, new PlaybackPositionEventArgs(_currentTimeMs, _totalDurationMs));
        };

        _mediaPlayer.LengthChanged += (s, e) =>
        {
            _totalDurationMs = e.Length;
            if (_currentMediaItem != null)
            {
                _currentMediaItem.DurationMs = _totalDurationMs;
            }
            DurationChanged?.Invoke(this, _totalDurationMs);
            RefreshTracks();
        };

        _mediaPlayer.Muted += (s, e) =>
        {
            _isMuted = true;
            MuteChanged?.Invoke(this, true);
        };

        _mediaPlayer.Unmuted += (s, e) =>
        {
            _isMuted = false;
            MuteChanged?.Invoke(this, false);
        };

        _mediaPlayer.VolumeChanged += (s, e) =>
        {
            _volume = (int)(e.Volume * 100);
            VolumeChanged?.Invoke(this, _volume);
        };
    }

    private void RefreshTracks()
    {
        try
        {
            // Audio Tracks
            var audioDescs = _mediaPlayer.AudioTrackDescription;
            var currentAudio = _mediaPlayer.AudioTrack;
            _currentAudioTrackId = currentAudio;

            var newAudioList = new List<TrackInfo>();
            if (audioDescs != null)
            {
                foreach (var desc in audioDescs)
                {
                    newAudioList.Add(new TrackInfo(desc.Id, desc.Name, null, desc.Id == currentAudio));
                }
            }
            _audioTracks = newAudioList;
            AudioTracksChanged?.Invoke(this, _audioTracks.AsReadOnly());
            AudioTrackChanged?.Invoke(this, _currentAudioTrackId);

            // Subtitle Tracks
            var spuDescs = _mediaPlayer.SpuDescription;
            var currentSpu = _mediaPlayer.Spu;
            _currentSubtitleTrackId = currentSpu;

            var newSpuList = new List<TrackInfo>();
            if (spuDescs != null)
            {
                foreach (var desc in spuDescs)
                {
                    newSpuList.Add(new TrackInfo(desc.Id, desc.Name, null, desc.Id == currentSpu));
                }
            }
            _subtitleTracks = newSpuList;
            SubtitleTracksChanged?.Invoke(this, _subtitleTracks.AsReadOnly());
            SubtitleTrackChanged?.Invoke(this, _currentSubtitleTrackId);

            // Delays
            _audioDelayMs = _mediaPlayer.AudioDelay / 1000;
            AudioDelayChanged?.Invoke(this, _audioDelayMs);

            _subtitleDelayMs = _mediaPlayer.SpuDelay / 1000;
            SubtitleDelayChanged?.Invoke(this, _subtitleDelayMs);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error refreshing tracks from MediaPlayer.");
        }
    }

    private void ClearTracks()
    {
        _audioTracks.Clear();
        _subtitleTracks.Clear();
        AudioTracksChanged?.Invoke(this, _audioTracks.AsReadOnly());
        SubtitleTracksChanged?.Invoke(this, _subtitleTracks.AsReadOnly());
    }

    public Task<Result> PlayAsync(string pathOrUrl, long startPositionMs = 0)
    {
        return Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pathOrUrl))
                    return Result.Failure("Invalid file path or URL.");

                Log.Information("Loading media: {PathOrUrl}", pathOrUrl);

                _currentMediaItem = new MediaItem(pathOrUrl);

                // Dispose previous Media cleanly
                _currentMedia?.Dispose();

                if (_currentMediaItem.IsNetworkStream)
                {
                    _currentMedia = new Media(_libVlc, pathOrUrl, FromType.FromLocation);
                    _currentMedia.AddOption(":network-caching=1500");
                    _currentMedia.AddOption(":live-caching=1500");
                }
                else
                {
                    if (!File.Exists(pathOrUrl))
                        return Result.Failure($"File not found: {pathOrUrl}");

                    _currentMedia = new Media(_libVlc, pathOrUrl, FromType.FromPath);
                }

                _mediaPlayer.Media = _currentMedia;
                _mediaPlayer.Play();

                // Re-apply equalizer if active
                if (_currentEqualizer != null && _currentEqualizer.IsEnabled)
                {
                    ApplyEqualizerInternal(_currentEqualizer);
                }

                // Apply current playback speed and aspect ratio
                if (Math.Abs(_playbackSpeed - 1.0f) > 0.01f)
                {
                    _mediaPlayer.SetRate(_playbackSpeed);
                }

                if (!string.IsNullOrWhiteSpace(_aspectRatio))
                {
                    _mediaPlayer.AspectRatio = _aspectRatio;
                }

                if (startPositionMs > 0)
                {
                    _mediaPlayer.Time = startPositionMs;
                }

                // Check for auto-discovery of external subtitles
                if (!_currentMediaItem.IsNetworkStream)
                {
                    DiscoverAndLoadExternalSubtitles(pathOrUrl);
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to play media: {PathOrUrl}", pathOrUrl);
                return Result.Failure($"Playback failed: {ex.Message}");
            }
        });
    }

    private void DiscoverAndLoadExternalSubtitles(string mediaFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(mediaFilePath);
            var baseName = Path.GetFileNameWithoutExtension(mediaFilePath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            var supportedExts = new[] { ".srt", ".ass", ".ssa", ".vtt", ".sub" };
            var candidateFiles = Directory.GetFiles(directory, $"{baseName}*.*")
                .Where(f => supportedExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            foreach (var subFile in candidateFiles)
            {
                Log.Information("Discovered matching external subtitle: {SubFile}", subFile);
                var subUri = new Uri(subFile).AbsoluteUri;
                _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, subUri, select: false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed during automatic subtitle discovery.");
        }
    }

    public Task<Result> PauseAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (_mediaPlayer.CanPause)
                {
                    _mediaPlayer.Pause();
                    return Result.Success();
                }
                return Result.Failure("Media cannot be paused.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error pausing playback.");
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> ResumeAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _mediaPlayer.Play();
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error resuming playback.");
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> StopAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _mediaPlayer.Stop();
                _currentTimeMs = 0;
                PositionChanged?.Invoke(this, new PlaybackPositionEventArgs(0, _totalDurationMs));
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping playback.");
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> SeekAsync(long positionMs)
    {
        return Task.Run(() =>
        {
            try
            {
                if (_mediaPlayer.IsSeekable && _totalDurationMs > 0)
                {
                    var boundedPos = Math.Clamp(positionMs, 0, _totalDurationMs);
                    _mediaPlayer.Time = boundedPos;
                    _currentTimeMs = boundedPos;
                    return Result.Success();
                }
                return Result.Failure("Media is not seekable.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error seeking to position {PositionMs}ms", positionMs);
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> SetVolumeAsync(int volume)
    {
        return Task.Run(() =>
        {
            try
            {
                var clampedVolume = Math.Clamp(volume, 0, 100);
                _mediaPlayer.Volume = clampedVolume;
                _volume = clampedVolume;
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting volume to {Volume}", volume);
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> ToggleMuteAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _mediaPlayer.ToggleMute();
                _isMuted = _mediaPlayer.Mute;
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error toggling mute.");
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> SetAudioTrackAsync(int trackId)
    {
        return Task.Run(() =>
        {
            try
            {
                _mediaPlayer.SetAudioTrack(trackId);
                _currentAudioTrackId = trackId;
                AudioTrackChanged?.Invoke(this, trackId);
                RefreshTracks();
                Log.Information("Selected audio track: {TrackId}", trackId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set audio track {TrackId}", trackId);
                return Result.Failure($"Failed to set audio track: {ex.Message}");
            }
        });
    }

    public Task<Result> SetAudioDelayAsync(long delayMs)
    {
        return Task.Run(() =>
        {
            try
            {
                // Clamp audio delay between -5000ms and +5000ms
                var clampedDelayMs = Math.Clamp(delayMs, -5000, 5000);
                var delayMicroseconds = clampedDelayMs * 1000;
                _mediaPlayer.SetAudioDelay(delayMicroseconds);
                _audioDelayMs = clampedDelayMs;
                AudioDelayChanged?.Invoke(this, clampedDelayMs);
                Log.Information("Set audio delay to {DelayMs}ms", clampedDelayMs);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set audio delay {DelayMs}", delayMs);
                return Result.Failure($"Failed to set audio delay: {ex.Message}");
            }
        });
    }

    public Task<Result> SetSubtitleTrackAsync(int trackId)
    {
        return Task.Run(() =>
        {
            try
            {
                _mediaPlayer.SetSpu(trackId);
                _currentSubtitleTrackId = trackId;
                SubtitleTrackChanged?.Invoke(this, trackId);
                RefreshTracks();
                Log.Information("Selected subtitle track: {TrackId}", trackId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set subtitle track {TrackId}", trackId);
                return Result.Failure($"Failed to set subtitle track: {ex.Message}");
            }
        });
    }

    public Task<Result> AddSubtitleFileAsync(string subtitleFilePath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(subtitleFilePath))
                    return Result.Failure($"Subtitle file not found: {subtitleFilePath}");

                var uri = new Uri(subtitleFilePath).AbsoluteUri;
                var success = _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, uri, select: true);
                if (success)
                {
                    Log.Information("Added external subtitle file: {Path}", subtitleFilePath);
                    RefreshTracks();
                    return Result.Success();
                }
                return Result.Failure("LibVLC failed to load external subtitle track.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding subtitle file: {Path}", subtitleFilePath);
                return Result.Failure(ex.Message);
            }
        });
    }

    public Task<Result> SetSubtitleDelayAsync(long delayMs)
    {
        return Task.Run(() =>
        {
            try
            {
                // Clamp subtitle delay between -5000ms and +5000ms
                var clampedDelayMs = Math.Clamp(delayMs, -5000, 5000);
                var delayMicroseconds = clampedDelayMs * 1000;
                _mediaPlayer.SetSpuDelay(delayMicroseconds);
                _subtitleDelayMs = clampedDelayMs;
                SubtitleDelayChanged?.Invoke(this, clampedDelayMs);
                Log.Information("Set subtitle delay to {DelayMs}ms", clampedDelayMs);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set subtitle delay {DelayMs}", delayMs);
                return Result.Failure($"Failed to set subtitle delay: {ex.Message}");
            }
        });
    }

    public Task<Result> SetPlaybackSpeedAsync(float speed)
    {
        return Task.Run(() =>
        {
            try
            {
                // LibVLC supports 0.25x to 4.0x
                var clampedSpeed = Math.Clamp(speed, 0.25f, 4.0f);
                _mediaPlayer.SetRate(clampedSpeed);
                _playbackSpeed = clampedSpeed;
                PlaybackSpeedChanged?.Invoke(this, clampedSpeed);
                Log.Information("Set playback speed to {Speed}x", clampedSpeed);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set playback speed {Speed}", speed);
                return Result.Failure($"Failed to set playback speed: {ex.Message}");
            }
        });
    }

    public Task<Result> SetAspectRatioAsync(string? aspectRatio)
    {
        return Task.Run(() =>
        {
            try
            {
                // Null or empty resets to default/original aspect ratio
                var targetRatio = (string.IsNullOrWhiteSpace(aspectRatio) || aspectRatio.Equals("Default", StringComparison.OrdinalIgnoreCase) || aspectRatio.Equals("Original", StringComparison.OrdinalIgnoreCase))
                    ? null
                    : aspectRatio;

                _mediaPlayer.AspectRatio = targetRatio;
                _aspectRatio = targetRatio;
                AspectRatioChanged?.Invoke(this, targetRatio);
                Log.Information("Set aspect ratio to {AspectRatio}", targetRatio ?? "Default");
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set aspect ratio {AspectRatio}", aspectRatio);
                return Result.Failure($"Failed to set aspect ratio: {ex.Message}");
            }
        });
    }

    public Task<Result<MediaDetails>> GetMediaDetailsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (_currentMedia == null || _currentMediaItem == null)
                    return Result<MediaDetails>.Failure("No media currently loaded.");

                var details = new MediaDetails
                {
                    FileName = _currentMediaItem.Title,
                    FilePath = _currentMediaItem.FilePathOrUrl,
                    DurationMs = _totalDurationMs > 0 ? _totalDurationMs : _currentMedia.Duration
                };

                // Infer container
                if (!string.IsNullOrWhiteSpace(details.FilePath))
                {
                    var ext = Path.GetExtension(details.FilePath).TrimStart('.').ToUpperInvariant();
                    details.Container = string.IsNullOrWhiteSpace(ext) ? "Stream" : ext;
                }

                // Analyze media tracks
                var tracks = _currentMedia.Tracks;
                if (tracks != null)
                {
                    foreach (var track in tracks)
                    {
                        if (track.TrackType == TrackType.Video && details.VideoCodec == null)
                        {
                            details.VideoCodec = FourCcToString(track.Codec);
                            details.Width = (int)track.Data.Video.Width;
                            details.Height = (int)track.Data.Video.Height;
                            if (track.Data.Video.FrameRateDen > 0)
                            {
                                details.FrameRate = (double)track.Data.Video.FrameRateNum / track.Data.Video.FrameRateDen;
                            }
                            if (track.Bitrate > 0)
                            {
                                details.VideoBitrate = track.Bitrate / 1000;
                            }
                        }
                        else if (track.TrackType == TrackType.Audio && details.AudioCodec == null)
                        {
                            details.AudioCodec = FourCcToString(track.Codec);
                            details.AudioChannels = (int)track.Data.Audio.Channels;
                            details.AudioSampleRate = (int)track.Data.Audio.Rate;
                            if (track.Bitrate > 0)
                            {
                                details.AudioBitrate = track.Bitrate / 1000;
                            }
                        }
                    }
                }

                details.AudioTracks = new List<TrackInfo>(_audioTracks);
                details.SubtitleTracks = new List<TrackInfo>(_subtitleTracks);

                return Result<MediaDetails>.Success(details);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve media details.");
                return Result<MediaDetails>.Failure($"Failed to extract media details: {ex.Message}");
            }
        });
    }

    private static string FourCcToString(uint fourcc)
    {
        try
        {
            var bytes = BitConverter.GetBytes(fourcc);
            var sb = new StringBuilder(4);
            foreach (var b in bytes)
            {
                if (b >= 32 && b <= 126)
                    sb.Append((char)b);
            }
            var s = sb.ToString().Trim();
            return string.IsNullOrEmpty(s) ? $"0x{fourcc:X8}" : s.ToUpperInvariant();
        }
        catch
        {
            return $"0x{fourcc:X8}";
        }
    }

    public Task<Result> SetEqualizerAsync(EqualizerSettings settings)
    {
        return Task.Run(() =>
        {
            try
            {
                _currentEqualizer = settings;
                if (!settings.IsEnabled)
                {
                    _mediaPlayer.SetEqualizer(null!);
                    return Result.Success();
                }

                ApplyEqualizerInternal(settings);
                Log.Information("Applied 10-band equalizer settings: Preset={Preset}, Enabled={Enabled}", settings.ActivePreset, settings.IsEnabled);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply equalizer settings");
                return Result.Failure($"Equalizer error: {ex.Message}");
            }
        });
    }

    public Task<Result> DisableEqualizerAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (_currentEqualizer != null)
                {
                    _currentEqualizer.IsEnabled = false;
                }
                _mediaPlayer.SetEqualizer(null!);
                Log.Information("Equalizer disabled");
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to disable equalizer");
                return Result.Failure(ex.Message);
            }
        });
    }

    private void ApplyEqualizerInternal(EqualizerSettings settings)
    {
        var eq = new Equalizer();
        eq.SetPreamp(Math.Clamp(settings.PreampGainDb, -20.0f, 20.0f));

        for (uint i = 0; i < Math.Min(eq.BandCount, (uint)settings.BandGainsDb.Length); i++)
        {
            eq.SetAmp(Math.Clamp(settings.BandGainsDb[i], -20.0f, 20.0f), i);
        }

        _mediaPlayer.SetEqualizer(eq);
    }

    public Task<Result<string>> TakeSnapshotAsync(string? targetFilePath = null)
    {
        return Task.Run(() =>
        {
            try
            {
                if (State == PlaybackState.Stopped || State == PlaybackState.Error)
                {
                    return Result<string>.Failure("Cannot take snapshot: No active media loaded.");
                }

                if (string.IsNullOrWhiteSpace(targetFilePath))
                {
                    var picturesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "NovaPlayer");
                    if (!Directory.Exists(picturesDir))
                    {
                        Directory.CreateDirectory(picturesDir);
                    }

                    var title = _currentMediaItem?.Title ?? "Snapshot";
                    var cleanTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    targetFilePath = Path.Combine(picturesDir, $"{cleanTitle}_{timestamp}.png");
                }
                else
                {
                    var dir = Path.GetDirectoryName(targetFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }

                var success = _mediaPlayer.TakeSnapshot(0, targetFilePath, 0, 0);
                if (success)
                {
                    Log.Information("Video snapshot saved to {Path}", targetFilePath);
                    return Result<string>.Success(targetFilePath);
                }

                return Result<string>.Failure("Native video snapshot failed. Ensure video is actively rendering.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception taking video snapshot to {Path}", targetFilePath);
                return Result<string>.Failure($"Snapshot failed: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Log.Information("Disposing LibVlcPlaybackService resources.");

        try
        {
            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _mediaPlayer.Dispose();
            _libVlc.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception during LibVLC disposal.");
        }
    }
}
