using System;
using System.IO;
using System.Text.Json;
using CurtantControl.Services.Relays;

namespace CurtantControl.Services;

public sealed class PhotoboothSettings
{
    public bool SimulateRelays { get; set; } = true;
    public int[] BcmPinsByChannel { get; set; } = Array.Empty<int>();
    public bool? ActiveLow { get; set; }
    public int[] CurtainOrderFrontToBack { get; set; } = [1, 2, 3, 4];
    public int ActivationMilliseconds { get; set; } = 500;
    public int MaximumActivationMilliseconds { get; set; } = 1000;
    public int BreakBeforeMakeMilliseconds { get; set; } = 100;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string? CameraDevice { get; set; }
    public string? CameraStreamUrl { get; set; }
    public bool EnablePythonCameraActivation { get; set; } = true;

    public static PhotoboothSettings Load(string? configPath = null, ICurtainOrderStore? orderStore = null)
    {
        var path = configPath ?? Path.Combine(AppContext.BaseDirectory, "photobooth.json");
        var settings = File.Exists(path)
            ? JsonSerializer.Deserialize<PhotoboothSettings>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("photobooth.json is empty.")
            : new PhotoboothSettings();
        var savedOrder = (orderStore ?? new CurtainOrderStore()).Load();
        if (savedOrder is not null) settings.CurtainOrderFrontToBack = savedOrder;
        return settings;
    }

    public CurtainRelayController CreateRelays()
    {
        if (ActivationMilliseconds <= 0 || ActivationMilliseconds > MaximumActivationMilliseconds ||
            BreakBeforeMakeMilliseconds <= 0)
            throw new InvalidDataException("Check relay activation, maximum activation, and off interval settings.");
        if (!SimulateRelays && ActiveLow is null)
            throw new InvalidDataException("Set ActiveLow explicitly before enabling GPIO.");
        IRelayOutput output = SimulateRelays ? new SimulatedRelayOutput()
            : new GpioRelayOutput(BcmPinsByChannel, ActiveLow!.Value);
        return new CurtainRelayController(output, TimeSpan.FromMilliseconds(MaximumActivationMilliseconds),
            TimeSpan.FromMilliseconds(BreakBeforeMakeMilliseconds));
    }
}
