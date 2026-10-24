using FluentAssertions;
using Moq;
using Nova.App.ViewModels;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Xunit;

namespace Nova.App.Tests;

public class EqualizerViewModelTests
{
    private readonly Mock<IPlaybackService> _mockPlaybackService;
    private readonly Mock<ILibraryRepository> _mockLibraryRepository;

    public EqualizerViewModelTests()
    {
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockLibraryRepository = new Mock<ILibraryRepository>();

        _mockPlaybackService
            .Setup(p => p.SetEqualizerAsync(It.IsAny<EqualizerSettings>()))
            .ReturnsAsync(Result.Success());

        _mockLibraryRepository
            .Setup(r => r.GetSettingAsync("equalizer_config", null))
            .ReturnsAsync(Result<string?>.Success(null));
    }

    [Fact]
    public void Constructor_InitializesTenBandsAndPresets()
    {
        var vm = new EqualizerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        vm.Bands.Should().HaveCount(10);
        vm.AvailablePresets.Should().Contain("Flat");
        vm.AvailablePresets.Should().Contain("Rock");
        vm.AvailablePresets.Should().Contain("Bass Boost");
        vm.AvailablePresets.Should().Contain("Custom");
        vm.SelectedPreset.Should().Be("Flat");
        vm.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyPreset_Rock_UpdatesBandGainsAndAppliesToEngine()
    {
        var vm = new EqualizerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        await vm.ApplyPresetAsync("Rock");

        vm.SelectedPreset.Should().Be("Rock");
        vm.Bands[0].GainDb.Should().Be(4.5f);
        vm.Bands[1].GainDb.Should().Be(3.0f);
        _mockPlaybackService.Verify(p => p.SetEqualizerAsync(It.Is<EqualizerSettings>(s => s.ActivePreset == "Rock")), Times.AtLeastOnce);
    }

    [Fact]
    public void ModifyingSingleBand_SwitchesPresetToCustom()
    {
        var vm = new EqualizerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        vm.SelectedPreset = "Rock";
        vm.Bands[3].GainDb = 8.0f; // Manual tweak

        vm.SelectedPreset.Should().Be("Custom");
    }

    [Fact]
    public async Task ResetAsync_SetsAllBandsToZeroAndPresetToFlat()
    {
        var vm = new EqualizerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);
        await vm.ApplyPresetAsync("Pop");
        vm.PreampGainDb = 3.5f;

        await vm.ResetAsync();

        vm.SelectedPreset.Should().Be("Flat");
        vm.PreampGainDb.Should().Be(0f);
        vm.Bands.Should().OnlyContain(b => b.GainDb == 0f);
    }

    [Fact]
    public void ToggleEnabled_SendsUpdatedStateToEngine()
    {
        var vm = new EqualizerViewModel(_mockPlaybackService.Object, _mockLibraryRepository.Object);

        vm.IsEnabled = false;

        _mockPlaybackService.Verify(p => p.SetEqualizerAsync(It.Is<EqualizerSettings>(s => !s.IsEnabled)), Times.AtLeastOnce);
    }
}
