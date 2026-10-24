using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.App.ViewModels;

public partial class EqualizerBandViewModel : ObservableObject
{
    public int Index { get; }
    public string FrequencyLabel { get; }

    [ObservableProperty]
    private float _gainDb;

    public EqualizerBandViewModel(int index, string label, float initialGain = 0f)
    {
        Index = index;
        FrequencyLabel = label;
        _gainDb = initialGain;
    }
}

public partial class EqualizerViewModel : ObservableObject
{
    private readonly IPlaybackService _playbackService;
    private readonly ILibraryRepository? _libraryRepository;
    private bool _isUpdatingPreset;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private float _preampGainDb;

    [ObservableProperty]
    private ObservableCollection<EqualizerBandViewModel> _bands = new();

    [ObservableProperty]
    private ObservableCollection<string> _availablePresets = new();

    [ObservableProperty]
    private string _selectedPreset = "Flat";

    public EqualizerViewModel(IPlaybackService playbackService, ILibraryRepository? libraryRepository = null)
    {
        _playbackService = playbackService;
        _libraryRepository = libraryRepository;

        // Initialize bands
        for (int i = 0; i < EqualizerSettings.BandCount; i++)
        {
            var label = i < EqualizerSettings.BandLabels.Length ? EqualizerSettings.BandLabels[i] : $"{i + 1}";
            var band = new EqualizerBandViewModel(i, label, 0f);
            band.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EqualizerBandViewModel.GainDb) && !_isUpdatingPreset)
                {
                    SelectedPreset = "Custom";
                    _ = ApplyToEngineAsync();
                }
            };
            Bands.Add(band);
        }

        // Initialize presets
        foreach (var p in EqualizerSettings.Presets)
        {
            AvailablePresets.Add(p);
        }
        AvailablePresets.Add("Custom");

        _ = LoadSavedSettingsAsync();
    }

    private async Task LoadSavedSettingsAsync()
    {
        if (_libraryRepository == null) return;

        try
        {
            var result = await _libraryRepository.GetSettingAsync("equalizer_config");
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                var settings = JsonSerializer.Deserialize<EqualizerSettings>(result.Value);
                if (settings != null)
                {
                    ApplySettings(settings);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load saved equalizer configuration.");
        }
    }

    public void ApplySettings(EqualizerSettings settings)
    {
        _isUpdatingPreset = true;
        try
        {
            IsEnabled = settings.IsEnabled;
            PreampGainDb = settings.PreampGainDb;
            SelectedPreset = settings.ActivePreset;

            for (int i = 0; i < Bands.Count && i < settings.BandGainsDb.Length; i++)
            {
                Bands[i].GainDb = settings.BandGainsDb[i];
            }
        }
        finally
        {
            _isUpdatingPreset = false;
        }

        _ = ApplyToEngineAsync();
    }

    public EqualizerSettings GetCurrentSettings()
    {
        var settings = new EqualizerSettings
        {
            IsEnabled = IsEnabled,
            PreampGainDb = PreampGainDb,
            ActivePreset = SelectedPreset
        };

        for (int i = 0; i < Bands.Count && i < settings.BandGainsDb.Length; i++)
        {
            settings.BandGainsDb[i] = Bands[i].GainDb;
        }

        return settings;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _ = ApplyToEngineAsync();
    }

    partial void OnPreampGainDbChanged(float value)
    {
        _ = ApplyToEngineAsync();
    }

    partial void OnSelectedPresetChanged(string value)
    {
        if (_isUpdatingPreset || value == "Custom") return;

        var preset = EqualizerSettings.CreatePreset(value);
        _isUpdatingPreset = true;
        try
        {
            for (int i = 0; i < Bands.Count && i < preset.BandGainsDb.Length; i++)
            {
                Bands[i].GainDb = preset.BandGainsDb[i];
            }
        }
        finally
        {
            _isUpdatingPreset = false;
        }

        _ = ApplyToEngineAsync();
    }

    [RelayCommand]
    public async Task ApplyPresetAsync(string presetName)
    {
        SelectedPreset = presetName;
        await ApplyToEngineAsync();
    }

    [RelayCommand]
    public async Task ResetAsync()
    {
        _isUpdatingPreset = true;
        try
        {
            PreampGainDb = 0f;
            foreach (var b in Bands)
            {
                b.GainDb = 0f;
            }
            SelectedPreset = "Flat";
            IsEnabled = true;
        }
        finally
        {
            _isUpdatingPreset = false;
        }

        await ApplyToEngineAsync();
    }

    [RelayCommand]
    public async Task ApplyToEngineAsync()
    {
        var settings = GetCurrentSettings();
        await _playbackService.SetEqualizerAsync(settings);

        if (_libraryRepository != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings);
                await _libraryRepository.SetSettingAsync("equalizer_config", json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to persist equalizer settings.");
            }
        }
    }
}
