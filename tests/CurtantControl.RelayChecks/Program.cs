using CurtantControl.Services.Relays;
using System.Diagnostics;
using CurtantControl.Services;
using CurtantControl.ViewModels;
using CurtantControl.Services.Camera;
using System.Net;
using System.Net.Http;
using System.Text;

if (args is ["--verify-window-close"]) { UiPreview.VerifyWindowClose(); return; }
if (args is ["--render-updates", var updatesPath]) { UiPreview.Render(updatesPath, showUpdates: true); return; }
if (args is ["--render-updates-small", var smallUpdatesPath]) { UiPreview.Render(smallUpdatesPath, showUpdates: true, width: 800, height: 480); return; }
if (args is ["--render-pin", var pinPath]) { UiPreview.Render(pinPath, mode: "user", pinDialog: true); return; }
if (args is ["--render-pin-small", var pinSmallPath]) { UiPreview.Render(pinSmallPath, mode: "user", pinDialog: true, width: 800, height: 480, light: true); return; }
if (args is ["--render-pin-settings", var pinSettingsPath]) { UiPreview.Render(pinSettingsPath, mode: "developer", pinDialog: true); return; }
if (args is ["--verify-razr-state", var statePath]) { UiPreview.Render(statePath, mode: "user", razrII: true, verifyRazrState: true); return; }
if (args is ["--render-razr2-user-settings", var simpleSetupPath]) { UiPreview.Render(simpleSetupPath, mode: "user", razrII: true, razrSetup: true); return; }
if (args is ["--render-razr-selector", var selectorPath]) { UiPreview.Render(selectorPath, modelChooser: true); return; }
if (args is ["--verify-razr2-hold", var razrHoldPath]) { UiPreview.Render(razrHoldPath, mode: "user", razrII: true, verifyModeHold: true); return; }
if (args is ["--render-razr-motor", var motorSetupPath]) { UiPreview.Render(motorSetupPath, razrSetup: true, setupBottom: true); return; }
if (args is ["--render-razr2", var razrPath]) { UiPreview.Render(razrPath, mode: "user", razrII: true); return; }
if (args is ["--render-razr-setup", var razrSetupPath]) { UiPreview.Render(razrSetupPath, razrSetup: true); return; }
if (args is ["--render-curtain-hover", var curtainHoverPath])
{
    UiPreview.Render(curtainHoverPath, mode: "user", hoverCurtain: true);
    return;
}
if (args is ["--verify-mode-hold", var holdPath])
{
    UiPreview.Render(holdPath, mode: "user", verifyModeHold: true);
    return;
}
if (args is ["--render-user-settings", var userSettingsPath])
{
    UiPreview.Render(userSettingsPath, settingsScreen: true, mode: "user");
    return;
}
if (args is ["--render-forward", var forwardPath])
{
    UiPreview.Render(forwardPath, mode: "user", nearestRelayOne: true);
    return;
}
if (args is ["--render", var renderPath])
{
    UiPreview.Render(renderPath);
    return;
}
if (args is ["--render-startup", var startupPath])
{
    UiPreview.Render(startupPath, mode: "startup");
    return;
}
if (args is ["--render-user", var userPath])
{
    UiPreview.Render(userPath, mode: "user", liveSample: true);
    return;
}
if (args is ["--render-user-light", var userLightPath])
{
    UiPreview.Render(userLightPath, mode: "user", light: true);
    return;
}
if (args is ["--render-user-compact", var compactUserPath])
{
    UiPreview.Render(compactUserPath, mode: "user", width: 1024, height: 600);
    return;
}
if (args is ["--render-light", var lightPath])
{
    UiPreview.Render(lightPath, light: true);
    return;
}
if (args is ["--render-light-hover", var hoverPath])
{
    UiPreview.Render(hoverPath, light: true, hoverReconnect: true);
    return;
}
if (args is ["--render-settings-light", var lightSettingsPath])
{
    UiPreview.Render(lightSettingsPath, settingsScreen: true, light: true);
    return;
}
if (args is ["--render-settings", var settingsRenderPath])
{
    UiPreview.Render(settingsRenderPath, settingsScreen: true);
    return;
}
if (args is ["--render-wide", var wideRenderPath])
{
    UiPreview.Render(wideRenderPath, width: 1920, height: 1080);
    return;
}
if (args is ["--render-compact", var compactRenderPath])
{
    UiPreview.Render(compactRenderPath, width: 1024, height: 600);
    return;
}
if (args is ["--render-camera", var cameraRenderPath])
{
    UiPreview.Render(cameraRenderPath, liveSample: true);
    return;
}

var checks = 0;
await Check("background preview timer replaces old selections and cancels on START or exit", async () =>
{
    using var timer = new BackgroundPreviewTimer(TimeSpan.FromMilliseconds(40));
    var old = 0;
    var latest = 0;
    var first = timer.RestartAsync(() => Interlocked.Increment(ref old));
    var second = timer.RestartAsync(() => Interlocked.Increment(ref latest));
    await Task.WhenAll(first, second);
    Assert(old == 0 && latest == 1, "Older preview timer replaced the latest selection.");
    var canceled = timer.RestartAsync(() => Interlocked.Increment(ref latest));
    timer.Stop();
    await canceled;
    Assert(latest == 1, "Stopped preview timer fired.");
});

await Check("palette defaults add photo choices without overwriting custom slots or colors", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "curtant-preview-palette-" + Guid.NewGuid());
    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "colors.json");
    try
    {
        var palette = new CurtainPalette();
        palette.Colors[0] = palette.Colors[0] with { Name = "Custom pink", Color = "#AA1122" };
        palette.Slots = ["purple", "pink", "green", "blue"];
        var store = new CurtainPaletteStore(path);
        store.Save(palette);
        var before = File.ReadAllText(path);
        var defaults = new CurtainPalette();
        defaults.Colors = defaults.Colors.Select(c => c with { PreviewImage = "Backgrounds/" + c.Key + ".png" })
            .Append(new CurtainColorPreset("gray", "Gray", "#888888", "Backgrounds/gray.png")).ToArray();
        File.WriteAllText(Path.Combine(folder, "colors.defaults.json"), System.Text.Json.JsonSerializer.Serialize(defaults));
        var loaded = store.Load();
        Assert(loaded.Colors.Length == 5 && loaded.Colors[0].Name == "Custom pink" && loaded.Colors[0].Color == "#AA1122"
            && loaded.Colors[0].PreviewImage == "Backgrounds/pink.png" && loaded.Slots.SequenceEqual(palette.Slots)
            && File.ReadAllText(path) == before, "Preview defaults changed user assignments or rewrote config.");
        await Task.CompletedTask;
    }
    finally { File.Delete(path); File.Delete(Path.Combine(folder, "colors.defaults.json")); Directory.Delete(folder); }
});

await Check("sample photo loader rejects paths outside application folder", async () =>
{
    Assert(BackgroundPreviewLoader.Load(null) is null, "Missing preview path should fall back to camera.");
    await Throws<InvalidDataException>(() => { BackgroundPreviewLoader.Load("../outside.png"); return Task.CompletedTask; });
    await Throws<InvalidDataException>(() => { BackgroundPreviewLoader.Load(Path.GetFullPath("outside.png")); return Task.CompletedTask; });
});
await Check("returning to applied background disables START without resending relays", async () =>
{
    var output = new RecordingOutput();
    await using var vm = new MainViewModel(new PhotoboothSettings(), Create(output),
        orderStore: new MemoryOrderStore());
    vm.EnterUserModeCommand.Execute(null);
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    var writes = output.Events.Count;
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[1]);
    Assert(vm.CanStart, "New draft background should enable START.");
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    Assert(!vm.CanStart, "Returning to applied background enabled START.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(output.Events.Count == writes, "Applied background sent redundant relay commands.");
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[1]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    Assert(vm.CanStart, "Previously applied but now closed background should enable START.");
});

await Check("paper five backgrounds map White and M1-M4 to the exact relay table", async () =>
{
    for (var id = 0; id <= 4; id++)
    {
        var output = new RecordingOutput();
        await using var vm = new MainViewModel(new PhotoboothSettings
            { SimulateRelays = false, CurtainOrderFrontToBack = [4, 3, 2, 1] }, Create(output),
            orderStore: new MemoryOrderStore());
        Assert(vm.BackgroundOptions.Select(c => c.Id).SequenceEqual(new[] { 0, 1, 2, 3, 4 }), "Backgrounds are not in paper order: fixed, M1, M2, M3, M4.");
        vm.EnterUserModeCommand.Execute(null);
        var before = output.Events.Count;
        await vm.SelectCurtainCommand.ExecuteAsync(vm.BackgroundOptions.Single(c => c.Id == id));
        Assert(output.Events.Count == before && vm.CanStart, "Paper background selection did not stage.");
        await vm.StartBackgroundCommand.ExecuteAsync(null);
        var expected = id == 0 ? new[] { 2, 4, 6, 8 }
            : Enumerable.Range(id + 1, 4 - id).Select(m => m * 2).Append(id * 2 - 1).Order().ToArray();
        Assert(output.Events.Where(e => e.On).Select(e => e.Channel).Order().SequenceEqual(expected.Order()), "Paper relay table mismatch.");
        Assert(!vm.CanStart, "Completed paper background still enables START.");
        Assert(vm.BackgroundOptions.Single(c => c.Id == id).IsSelected, "Applied paper background is not highlighted.");
    }
});

await Check("fixed background palette choice saves atomically, reloads, and still closes four motors", async () =>
{
    var store = new MemoryPaletteStore();
    store.Value.Colors = store.Value.Colors.Select(c => c.Key == "cyan" ? c with { PreviewImage = "Backgrounds/cyan.png" } : c).ToArray();
    var output = new RecordingOutput();
    await using var vm = new MainViewModel(new PhotoboothSettings { CurtainOrderFrontToBack = [4, 3, 2, 1] }, Create(output), paletteStore: store);
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    var before = output.Events.Count;
    vm.CurtainSettings.FixedColor = vm.CurtainSettings.FixedColors.Single(c => c.Key == "cyan");
    Assert(vm.FixedWhiteBackground.Name == "White", "Unsaved fixed draft changed main screen.");
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(vm.IsSettingsOpen && vm.CurtainSettings.Notice == "Saved" && output.Events.Count == before, "Fixed save moved relays or navigated.");
    Assert(store.Value.FixedBackground == "cyan" && vm.FixedWhiteBackground.Name == "Cyan"
        && vm.FixedWhiteBackground.PreviewImage == "Backgrounds/cyan.png" && vm.SelectionLabel.Contains("Cyan"), "Fixed name/photo/selection was not updated.");
    vm.CurtainSettings.FixedColor = vm.CurtainSettings.FixedColors.Single(c => c.Key == "pink");
    Assert(vm.CurtainSettings.FixedColor.Key == "cyan" && vm.CurtainSettings.Error.Length > 0, "Duplicate fixed choice accepted.");
    var slot = vm.CurtainSettings.Slots[0];
    var prior = slot.SelectedColor;
    slot.SelectedColor = slot.Colors.Single(c => c.Key == "cyan");
    Assert(slot.SelectedColor == prior, "Motor accepted fixed background's color.");
    vm.CurtainSettings.FixedColor = vm.CurtainSettings.FixedColors.Single(c => c.Key == "white");
    store.FailSave = true;
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(vm.FixedWhiteBackground.Name == "Cyan" && store.Value.FixedBackground == "cyan", "Failed save changed fixed choice.");
    vm.CurtainSettings.CancelCommand.Execute(null);
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[3]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    before = output.Events.Count;
    await vm.SelectCurtainCommand.ExecuteAsync(vm.FixedWhiteBackground);
    Assert(output.Events.Count == before, "Fixed selection in Developer sent relays before START.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(output.Events.Skip(before).Where(e => e.On).Select(e => e.Channel).Order().SequenceEqual(new[] { 8 }), "Fixed background did not close only the open motor.");
    await using var restored = new MainViewModel(new PhotoboothSettings(), paletteStore: store);
    Assert(restored.FixedWhiteBackground.Name == "Cyan" && restored.SelectionLabel.Contains("Cyan"), "Saved fixed choice did not reload.");
    var path = Path.Combine(Path.GetTempPath(), "fixed-background-" + Guid.NewGuid() + ".json");
    try
    {
        var disk = new CurtainPaletteStore(path);
        disk.Save(store.Value);
        Assert(disk.Load().FixedBackground == "cyan", "Fixed key did not survive JSON round-trip.");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});

await Check("unified five Settings colors match main order and save to correct physical IDs", async () =>
{
    var store = new MemoryPaletteStore();
    await using var vm = new MainViewModel(new PhotoboothSettings { CurtainOrderFrontToBack = [4, 3, 2, 1] }, paletteStore: store);
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    var editor = vm.CurtainSettings;
    Assert(editor.DisplaySlots.Select(s => s.CurtainId).SequenceEqual(vm.BackgroundOptions.Select(c => c.Id)), "Settings/main background order differs.");
    Assert(editor.DisplaySlots.Where(d => d.CurtainId != 0).All(d => ReferenceEquals(d, editor.Slots.Single(s => s.CurtainId == d.CurtainId))), "Motor dropdown is not sharing its draft.");
    var first = editor.DisplaySlots.Single(s => s.CurtainId == 0);
    first.SelectedColor = first.Colors.Single(c => c.Key == "cyan");
    Assert(editor.FixedColor.Key == "cyan", "First dropdown did not edit fixed background.");
    var motor = editor.DisplaySlots.Single(s => s.CurtainId == 1);
    motor.SelectedColor = motor.Colors.Single(c => c.Key == "cyan");
    Assert(motor.SelectedColor.Key == "pink", "Motor accepted duplicate fixed color.");
    editor.SaveCommand.Execute(null);
    Assert(store.Value.FixedBackground == "cyan" && vm.FixedWhiteBackground.Name == "Cyan" && store.Value.Slots[0] == "pink", "Unified save changed physical motor mapping.");
});

await Check("Developer stages previews until START and derives fixed-background status from motor commands", async () =>
{
    var output = new RecordingOutput();
    await using var vm = new MainViewModel(new PhotoboothSettings
        { ActivationMilliseconds = 200, CurtainOrderFrontToBack = [4, 3, 2, 1] }, Create(output));
    vm.EnterDeveloperModeCommand.Execute(null);
    Assert(vm.FixedWhiteBackground.CommandStatus == "Visible", "Simulated fixed status not Visible.");
    var before = output.Events.Count;
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[3]);
    Assert(vm.CanStart && output.Events.Count == before && vm.FixedWhiteBackground.CommandStatus == "Visible", "Developer selection changed outputs/status before START.");
    var start = vm.StartBackgroundCommand.ExecuteAsync(null);
    await output.WaitForOn(7);
    Assert(!vm.CanStart && vm.FixedWhiteBackground.CommandStatus == "Changing", "START did not show moving status.");
    await start;
    Assert(vm.FixedWhiteBackground.CommandStatus == "Hidden" && !vm.CanStart, "Opened curtain did not hide fixed background.");
    before = output.Events.Count;
    await vm.SelectCurtainCommand.ExecuteAsync(vm.FixedWhiteBackground);
    Assert(vm.CanStart && output.Events.Count == before && vm.FixedWhiteBackground.CommandStatus == "Hidden", "Fixed draft moved relays.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(vm.FixedWhiteBackground.CommandStatus == "Visible" && !vm.CanStart, "Fixed START did not show Visible after all Closed.");
    foreach (var curtain in vm.Curtains) curtain.CommandStatus = "Closed (saved)";
    Assert(vm.FixedWhiteBackground.CommandStatus == "Visible (saved)", "Saved fixed visibility lost estimate annotation.");
    foreach (var curtain in vm.Curtains) curtain.CommandStatus = "Unknown";
    Assert(vm.FixedWhiteBackground.CommandStatus == "Unknown", "Unknown motor states implied fixed visibility.");
});

await Check("all 80 known-state/background combinations send exactly necessary relay channels", async () =>
{
    for (var mask = 0; mask < 16; mask++)
    for (var selected = 0; selected <= 4; selected++)
    {
        var states = Enumerable.Range(0, 4).Select(i => (mask & (1 << i)) != 0
            ? CurtainCommandState.Opened : CurtainCommandState.Closed).ToArray();
        var original = states.ToArray();
        var output = new RecordingOutput();
        using var relays = Create(output);
        var backgrounds = new BackgroundController(relays, [4, 3, 2, 1], TimeSpan.FromMilliseconds(2), id => states[id - 1]);
        var expected = new List<int>();
        for (var id = selected + 1; id <= 4; id++)
            if (original[id - 1] != CurtainCommandState.Closed) expected.Add(id * 2);
        if (selected > 0 && original[selected - 1] != CurtainCommandState.Opened) expected.Add(selected * 2 - 1);
        void Report(CurtainCommandUpdate update) => states[update.Curtain - 1] = update.State;
        if (selected == 0) await backgrounds.CloseAllAsync(report: Report);
        else await backgrounds.SelectAsync(selected, report: Report);
        var channels = output.Events.Where(e => e.On).Select(e => e.Channel).ToArray();
        Assert(channels.Order().SequenceEqual(expected.Order()), $"Unnecessary/missing command for state mask {mask}, background {selected}.");
        if (selected > 0 && expected.Contains(selected * 2 - 1))
            Assert(channels.Last() == selected * 2 - 1, "Target opened before necessary foreground closes finished.");
        for (var id = 1; id <= 4; id++)
            Assert(states[id - 1] == (selected == 0 || id > selected ? CurtainCommandState.Closed
                : id == selected ? CurtainCommandState.Opened : original[id - 1]), "Wrong final state or changed background behind target.");
        var writes = output.Events.Count;
        if (selected == 0) await backgrounds.CloseAllAsync(report: Report);
        else await backgrounds.SelectAsync(selected, report: Report);
        Assert(output.Events.Count == writes, "Repeated completed selection wrote relays.");
    }
});

await Check("paper transitions skip closed foregrounds, open targets, and fixed background's closed motors", async () =>
{
    var output = new RecordingOutput();
    await using var vm = new MainViewModel(new PhotoboothSettings
        { ActivationMilliseconds = 20, CurtainOrderFrontToBack = [4, 3, 2, 1] }, Create(output));
    vm.EnterUserModeCommand.Execute(null);
    async Task Apply(int id, params int[] expected)
    {
        var before = output.Events.Count;
        await vm.SelectCurtainCommand.ExecuteAsync(vm.BackgroundOptions.Single(c => c.Id == id));
        await vm.StartBackgroundCommand.ExecuteAsync(null);
        Assert(output.Events.Skip(before).Where(e => e.On).Select(e => e.Channel).SequenceEqual(expected), $"Wrong optimized transition to background {id}.");
    }
    await Apply(4, 7);
    await Apply(2, 8, 3);
    await Apply(4, 7);
    await Apply(2, 8);
    await Apply(0, 4);
    await Apply(0);
});

await Check("unknown states still send commands and cancellation never opens target", async () =>
{
    var states = new[] { CurtainCommandState.Opened, CurtainCommandState.Unknown, CurtainCommandState.Closed, CurtainCommandState.Unknown };
    var output = new RecordingOutput();
    using var relays = Create(output);
    var backgrounds = new BackgroundController(relays, [4, 3, 2, 1], TimeSpan.FromMilliseconds(200), id => states[id - 1]);
    using var cancel = new CancellationTokenSource();
    var task = backgrounds.SelectAsync(2, cancel.Token, update => states[update.Curtain - 1] = update.State);
    await output.WaitForOn(8);
    cancel.Cancel();
    await Throws<OperationCanceledException>(() => task);
    Assert(states[3] == CurtainCommandState.Unknown && !output.Events.Any(e => e.On && e.Channel is 6 or 3), "Canceled selection pulsed skipped foreground or opened target.");
});

await Check("runtime layer reversal saves without GPIO, updates next START, restores and reconciles selection", async () =>
{
    var output = new RecordingOutput();
    var palette = new MemoryPaletteStore();
    var settings = new PhotoboothSettings { ActivationMilliseconds = 20, CurtainOrderFrontToBack = [4, 3, 2, 1] };
    await using var vm = new MainViewModel(settings, Create(output), paletteStore: palette);
    vm.EnterDeveloperModeCommand.Execute(null);
    async Task Apply(int id, params int[] channels)
    {
        var before = output.Events.Count;
        await vm.SelectCurtainCommand.ExecuteAsync(vm.BackgroundOptions.Single(c => c.Id == id));
        await vm.StartBackgroundCommand.ExecuteAsync(null);
        Assert(output.Events.Skip(before).Where(e => e.On).Select(e => e.Channel).SequenceEqual(channels), "Runtime order sent wrong relay sequence.");
    }
    await Apply(1, 1);
    await Apply(4, 7);
    await vm.SelectCurtainCommand.ExecuteAsync(vm.BackgroundOptions.Single(c => c.Id == 2));
    Assert(vm.CanStart, "Draft not staged before changing order.");
    vm.OpenSettingsCommand.Execute(null);
    var beforeSave = output.Events.Count;
    var states = vm.Curtains.Select(c => c.CommandStatus).ToArray();
    vm.CurtainSettings.SelectedRelayOrder = vm.CurtainSettings.RelayOrders.Single(o => o.FrontToBack.SequenceEqual(new[] { 1, 2, 3, 4 }));
    Assert(settings.CurtainOrderFrontToBack.SequenceEqual(new[] { 4, 3, 2, 1 }), "Unsaved layer draft applied.");
    Assert(vm.BackgroundOptions.Select(c => c.Id).SequenceEqual(new[] { 0, 1, 2, 3, 4 })
        && vm.CurtainSettings.DisplaySlots.Select(s => s.CurtainId).SequenceEqual(new[] { 0, 1, 2, 3, 4 }), "Changing relay layer draft reordered paper background buttons.");
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(vm.IsSettingsOpen && vm.CurtainSettings.Notice == "Saved" && output.Events.Count == beforeSave, "Order Save moved relay or navigated.");
    Assert(vm.Curtains.Select(c => c.CommandStatus).SequenceEqual(states) && vm.Curtains[0].IsSelected && !vm.CanStart, "Order save changed motor states or retained stale draft/highlight.");
    Assert(settings.CurtainOrderFrontToBack.SequenceEqual(new[] { 1, 2, 3, 4 }) && palette.Value.RelayOrderFrontToBack!.SequenceEqual(new[] { 1, 2, 3, 4 }), "Forward order did not persist/apply.");
    Assert(vm.BackgroundOptions.Select(c => c.Id).SequenceEqual(new[] { 0, 1, 2, 3, 4 }), "Forward layer order changed paper background button order.");
    vm.CurtainSettings.CancelCommand.Execute(null);
    await Apply(2, 2, 3);
    vm.OpenSettingsCommand.Execute(null);
    beforeSave = output.Events.Count;
    vm.CurtainSettings.SelectedRelayOrder = vm.CurtainSettings.RelayOrders.Single(o => o.FrontToBack.SequenceEqual(new[] { 4, 3, 2, 1 }));
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(output.Events.Count == beforeSave && vm.Curtains[3].IsSelected, "Reverse save pulsed relays or failed visible-layer inference.");
    Assert(vm.BackgroundOptions.Select(c => c.Id).SequenceEqual(new[] { 0, 1, 2, 3, 4 })
        && vm.CurtainSettings.DisplaySlots.Select(s => s.CurtainId).SequenceEqual(vm.BackgroundOptions.Select(c => c.Id)), "Reverse relay layers changed paper main/Settings button order.");
    vm.CurtainSettings.CancelCommand.Execute(null);
    await Apply(2, 8);
    await using var restored = new MainViewModel(new PhotoboothSettings { CurtainOrderFrontToBack = [1, 2, 3, 4] }, paletteStore: palette);
    Assert(restored.DisplayCurtains.Select(c => c.Id).SequenceEqual(new[] { 4, 3, 2, 1 }), "Palette order was overridden after restart.");
    var path = Path.Combine(Path.GetTempPath(), "relay-order-" + Guid.NewGuid() + ".json");
    try
    {
        var disk = new CurtainPaletteStore(path);
        disk.Save(palette.Value);
        Assert(disk.Load().RelayOrderFrontToBack!.SequenceEqual(new[] { 4, 3, 2, 1 }), "Layer order lost in JSON round-trip.");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});

await Check("layer order Cancel, failed Save, and Save during movement leave applied mapping unchanged", async () =>
{
    var output = new RecordingOutput();
    var palette = new MemoryPaletteStore();
    var settings = new PhotoboothSettings { ActivationMilliseconds = 200, CurtainOrderFrontToBack = [4, 3, 2, 1] };
    await using var vm = new MainViewModel(settings, Create(output), paletteStore: palette);
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    vm.CurtainSettings.SelectedRelayOrder = vm.CurtainSettings.RelayOrders[0];
    vm.CurtainSettings.CancelCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    Assert(vm.CurtainSettings.SelectedRelayOrder.FrontToBack.SequenceEqual(new[] { 4, 3, 2, 1 }), "Canceled order retained on reopen.");
    var before = output.Events.Count;
    vm.CurtainSettings.SelectedRelayOrder = vm.CurtainSettings.RelayOrders[0];
    palette.FailSave = true;
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(vm.CurtainSettings.Error.Length > 0 && settings.CurtainOrderFrontToBack.SequenceEqual(new[] { 4, 3, 2, 1 })
        && palette.Value.RelayOrderFrontToBack is null && output.Events.Count == before, "Failed order save partially applied.");
    palette.FailSave = false;
    vm.CurtainSettings.CancelCommand.Execute(null);
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[3]);
    var start = vm.StartBackgroundCommand.ExecuteAsync(null);
    await output.WaitForOn(7);
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(vm.CurtainSettings.Error.Contains("Wait") && palette.Value.RelayOrderFrontToBack is null
        && settings.CurtainOrderFrontToBack.SequenceEqual(new[] { 4, 3, 2, 1 }), "Mapping changed during movement.");
    await start;
});

await Check("relay order Settings is Developer-only while User keeps five colors and saved order", async () =>
{
    var palette = new MemoryPaletteStore();
    var settings = new PhotoboothSettings { CurtainOrderFrontToBack = [4, 3, 2, 1] };
    await using var vm = new MainViewModel(settings, paletteStore: palette);
    vm.EnterUserModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    Assert(!vm.IsSettingsOpen && !vm.ShowCurtainSettingsButton, "User Settings remained accessible.");
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    Assert(vm.CurtainSettings.IsRelayOrderVisible, "Developer relay order hidden.");
    vm.CurtainSettings.SelectedRelayOrder = vm.CurtainSettings.RelayOrders[0];
    vm.CurtainSettings.SaveCommand.Execute(null);
    vm.CurtainSettings.CancelCommand.Execute(null);
    vm.EnterUserModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    Assert(!vm.IsSettingsOpen && settings.CurtainOrderFrontToBack.SequenceEqual(new[] { 1, 2, 3, 4 }), "User lost saved Developer mapping or opened Settings.");
});

await Check("mode chooser starts offline; both modes stage colors until START", async () =>
{
    var output = new RecordingOutput();
    var rig = new FakeCameraRig();
    await using var vm = new MainViewModel(new PhotoboothSettings { ActivationMilliseconds = 20 }, Create(output),
        cameraCatalog: new FakeCameraCatalog(), cameraFactory: rig.Create);
    Assert(vm.IsModeSelectionVisible && !vm.IsDeveloperScreenVisible && !vm.IsUserScreenVisible, "Startup mode chooser missing.");
    vm.StartCamera();
    Assert(rig.Active == 0, "Camera started before choosing mode.");
    vm.EnterUserModeCommand.Execute(null);
    Assert(vm.IsUserScreenVisible && !vm.IsDeveloperScreenVisible && !vm.CanStart, "User mode state incorrect.");
    var before = output.Events.Count;
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[1]);
    Assert(output.Events.Count == before && vm.Curtains[1].IsSelected && vm.CanStart, "User color press sent a relay pulse.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(output.Events.Where(e => e.On).Select(e => e.Channel).SequenceEqual(new[] { 3 }) && !vm.CanStart,
        "START did not apply staged background once.");
    before = output.Events.Count;
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(output.Events.Count == before, "START repeated without a pending selection.");
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[2]);
    vm.ChooseModeCommand.Execute(null);
    Assert(!vm.CanStart && vm.Curtains.All(c => !c.IsSelected), "Abandoned user draft retained selection.");
    vm.EnterDeveloperModeCommand.Execute(null);
    Assert(vm.IsDeveloperScreenVisible, "Developer screen unavailable.");
    before = output.Events.Count;
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    Assert(output.Events.Count == before && vm.CanStart, "Developer color selection sent relay commands.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(output.Events.Last(e => e.On).Channel == 1 && !vm.CanStart, "Developer START did not apply background.");
});

await Check("mode changes blocked during START; Stop cancels and leaves user selection retryable", async () =>
{
    var output = new RecordingOutput();
    await using var vm = new MainViewModel(new PhotoboothSettings { ActivationMilliseconds = 2000 }, Create(output));
    vm.EnterUserModeCommand.Execute(null);
    Assert(vm.ActivityLog.Count == 0, "Entering user mode did not clear old logs.");
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    var start = vm.StartBackgroundCommand.ExecuteAsync(null);
    await output.WaitForOn(1);
    vm.ChooseModeCommand.Execute(null);
    Assert(vm.Mode == ApplicationMode.User && !vm.CanChangeMode, "Mode changed during relay command.");
    vm.StopCommand.Execute(null);
    await start;
    Assert(output.AllOff && vm.CanStart && vm.CanChangeMode, "Stopped user command could not be retried.");
});
await Check("last command estimates restore by physical ID without moving relays", async () =>
{
    var path = Path.Combine(Path.GetTempPath(), "curtant-state-" + Guid.NewGuid() + ".json");
    var store = new CurtainCommandStateStore(path: path);
    try
    {
        var settings = new PhotoboothSettings { SimulateRelays = false, ActivationMilliseconds = 20 };
        await using (var vm = new MainViewModel(settings, Create(new RecordingOutput()), commandStateStore: store))
            await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[1]);
        Assert(store.Load()!.SequenceEqual(new[] { "Closed", "Opened", "Unknown", "Unknown" }), "Last commands not saved by ID.");
        var output = new RecordingOutput();
        await using (var vm = new MainViewModel(settings, Create(output), commandStateStore: store))
        {
            Assert(vm.Curtains[0].CommandStatus == "Closed (saved)" && vm.Curtains[1].CommandStatus == "Opened (saved)"
                && vm.Curtains[2].CommandStatus == "Unknown" && !output.Events.Any(e => e.On), "Restore moved relays or implied verified status.");
            Assert(vm.Curtains[1].IsSelected && vm.SelectionLabel == "Sky Blue selected (saved)", "Saved background was not highlighted.");
            await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[1]);
            Assert(output.Events.Count(e => e.On) == 2, "Saved highlight suppressed explicit re-selection.");
            var beforeClose = output.Events.Count(e => e.On);
            await vm.CloseAllCommand.ExecuteAsync(null);
            Assert(output.Events.Count(e => e.On) - beforeClose == 3, "Close all did not skip only the freshly confirmed Closed curtain.");
        }
        File.WriteAllText(path, "bad json");
        await using var invalid = new MainViewModel(settings, Create(new RecordingOutput()), commandStateStore: store);
        Assert(invalid.Curtains.All(c => c.CommandStatus == "Unknown") && invalid.ActivityLog.Any(e => e.Contains("Could not restore")),
            "Corrupt saved state was treated as valid.");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});

await Check("saved background inference respects front-to-back order and unknown foregrounds", async () =>
{
    var path = Path.Combine(Path.GetTempPath(), "curtant-infer-" + Guid.NewGuid() + ".json");
    var store = new CurtainCommandStateStore(path: path);
    var settings = new PhotoboothSettings { SimulateRelays = false, CurtainOrderFrontToBack = [4, 2, 1, 3] };
    try
    {
        store.Save(["Opened", "Opened", "Unknown", "Closed"]);
        var output = new RecordingOutput();
        await using (var vm = new MainViewModel(settings, Create(output), commandStateStore: store))
            Assert(vm.Curtains[1].IsSelected && vm.Curtains.Count(c => c.IsSelected) == 1 && !output.Events.Any(e => e.On),
                "Inference ignored layer order or unknown states behind selected curtain.");
        store.Save(["Opened", "Opened", "Closed", "Unknown"]);
        await using (var vm = new MainViewModel(settings, Create(new RecordingOutput()), commandStateStore: store))
            Assert(vm.Curtains.All(c => !c.IsSelected) && vm.SelectionLabel.Contains("unknown"), "Unknown foreground was ignored.");
        store.Save(["Closed", "Closed", "Closed", "Closed"]);
        await using (var vm = new MainViewModel(settings, Create(new RecordingOutput()), commandStateStore: store))
            Assert(vm.Curtains.All(c => !c.IsSelected) && vm.FixedWhiteBackground.IsSelected && vm.SelectionLabel == "White selected (saved)", "All-closed state inferred a background.");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});

await Check("pending and canceled pulses persist Unknown instead of stale completion", async () =>
{
    var path = Path.Combine(Path.GetTempPath(), "curtant-pending-" + Guid.NewGuid() + ".json");
    var store = new CurtainCommandStateStore(path: path);
    try
    {
        store.Save(["Closed", "Closed", "Closed", "Closed"]);
        var output = new RecordingOutput();
        await using var vm = new MainViewModel(new PhotoboothSettings { SimulateRelays = false, ActivationMilliseconds = 2000 },
            Create(output), commandStateStore: store);
        var move = vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
        await output.WaitForOn(1);
        Assert(store.Load()![0] == "Unknown", "Pending pulse retained stale Closed state.");
        vm.StopCommand.Execute(null);
        await move;
        Assert(store.Load()![0] == "Unknown", "Canceled command saved completion.");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});

await Check("state write failure prevents sending an unrecorded new pulse", async () =>
{
    var output = new RecordingOutput();
    await using var vm = new MainViewModel(new PhotoboothSettings(), Create(output), commandStateStore: new FailingCommandStateStore());
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    Assert(!output.Events.Any(e => e.On) && vm.Curtains[0].CommandStatus == "Unknown"
        && vm.ActivityLog.Any(e => e.Contains("Could not save curtain commands")), "Unrecorded command was sent.");
});
await Check("JSON palette extends choices while four curtain IDs and relay assignments remain fixed", async () =>
{
    var path = Path.Combine(Path.GetTempPath(), "curtant-palette-" + Guid.NewGuid() + ".json");
    var store = new CurtainPaletteStore(path);
    try
    {
        var palette = new CurtainPalette();
        palette.Colors = palette.Colors.Append(new CurtainColorPreset("cyan", "Cyan", "#00FFFF")).ToArray();
        store.Save(palette);
        var output = new RecordingOutput();
        await using var vm = new MainViewModel(new PhotoboothSettings { ActivationMilliseconds = 20 },
            Create(output), orderStore: new MemoryOrderStore(), paletteStore: store);
        Assert(vm.Curtains.Count == 4 && vm.Curtains.Select(c => c.Id).SequenceEqual(new[] { 1, 2, 3, 4 }), "Palette changed curtain IDs.");
        vm.OpenSettingsCommand.Execute(null);
        var editor = vm.CurtainSettings;
        editor.Slots[0].SelectedColor = editor.Slots[0].Colors.Single(c => c.Key == "cyan");
        vm.CurtainSettings.CancelCommand.Execute(null);
        Assert(vm.Curtains[0].Name == "Pink" && store.Load().Slots[0] == "pink", "Draft changed active/persisted colors.");
        vm.OpenSettingsCommand.Execute(null);
        editor.Slots[0].SelectedColor = editor.Slots[0].Colors.Single(c => c.Key == "cyan");
        var before = output.Events.Count;
        editor.SaveCommand.Execute(null);
        Assert(output.Events.Count == before && vm.Curtains[0].Name == "Cyan"
            && store.Load().Slots[0] == "cyan" && vm.IsSettingsOpen, "Color save moved relays or did not persist.");
        vm.CurtainSettings.CancelCommand.Execute(null);
        await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
        Assert(output.Events.Where(e => e.On).Last().Channel == 1 && vm.SelectionLabel == "Cyan selected", "Color reassigned relay identity.");
        palette.Slots = ["pink", "blue", "missing", "purple"];
        await Throws<InvalidDataException>(() => { store.Save(palette); return Task.CompletedTask; });
        Assert(store.Load().Slots[0] == "cyan", "Invalid palette replaced saved colors.");
        palette.Slots = ["pink", "blue", "green", "purple"];
        palette.Colors = [new("pink", "Pink", "not-a-color")];
        await Throws<InvalidDataException>(() => { store.Save(palette); return Task.CompletedTask; });
    }
    finally { if (File.Exists(path)) File.Delete(path); }
});
await Check("app starts dark despite a saved light preference without changing hardware settings", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "curtant-theme-" + Guid.NewGuid());
    var path = Path.Combine(folder, "appearance.json");
    try
    {
        var store = new ThemePreferenceStore(path);
        Assert(store.LoadDarkMode(), "New preference must default to dark.");
        var settings = new PhotoboothSettings();
        await using (var vm = new MainViewModel(settings, themeStore: store)) vm.IsDarkMode = false;
        await using (var vm = new MainViewModel(settings, themeStore: store))
        {
            Assert(vm.IsDarkMode && settings.SimulateRelays, "Old light preference overrode kiosk dark default or relay setting changed.");
            Assert(!store.LoadDarkMode(), "Startup unnecessarily rewrote the old preference.");
            store.SaveDarkMode(true);
        }
        Assert(store.LoadDarkMode(), "Dark preference did not save.");
        File.WriteAllText(path, "broken");
        Assert(store.LoadDarkMode(), "Invalid preference did not fall back to dark.");
    }
    finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(folder)) Directory.Delete(folder); }
});
await Check("check relays reports initialization failure after log is cleared", async () =>
{
    await using var viewModel = new MainViewModel(new PhotoboothSettings { ActivationMilliseconds = 0 });
    viewModel.ClearLogCommand.Execute(null);
    viewModel.CheckRelaysCommand.Execute(null);
    Assert(viewModel.ActivityLog.Count == 1 && viewModel.ActivityLog[0].Contains("Cannot check relays")
        && viewModel.Status.Contains("activation"), "Setup failure was silently ignored.");
    viewModel.CheckRelaysCommand.Execute(null);
    Assert(viewModel.ActivityLog.Count == 2, "Repeated check did not produce feedback.");
});
await Check("startup reads all eight initialized outputs and logs each channel", async () =>
{
    var output = new SimulatedRelayOutput();
    var controller = Create(output);
    await using var viewModel = new MainViewModel(new PhotoboothSettings(), controller);
    var snapshot = controller.ReadOutputs();
    Assert(snapshot.Count == 8 && snapshot.Select(r => r.Channel).SequenceEqual(Enumerable.Range(1, 8))
        && snapshot.All(r => r.Energized == false), "Startup output snapshot is wrong.");
    Assert(viewModel.ActivityLog.Count(e => e.Contains("(control signal)")) == 8, "Startup did not log eight relay readings.");
    Assert(viewModel.Curtains.All(c => c.CommandStatus == "Closed"), "Simulated curtains must initialize closed.");
});

await Check("relay reads report active output and isolate read failures", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    var command = controller.OpenAsync(2, TimeSpan.FromSeconds(2));
    await output.WaitForOn(3);
    output.ReadFailChannel = 1;
    var snapshot = controller.ReadOutputs();
    Assert(snapshot[0].Energized is null && snapshot[0].Error is not null, "Read failure was not reported unknown.");
    Assert(snapshot[2].Energized == true && snapshot[3].Energized == false && snapshot.Count == 8, "Read failure blocked later channels.");
    controller.StopAll();
    await Throws<OperationCanceledException>(() => command);
});
await Check("mapping, timed cutoff, and break before make", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    Assert(output.Events.Count == 8 && output.Events.All(e => !e.On), "Startup must turn off all eight channels.");
    for (var curtain = 1; curtain <= 4; curtain++)
    {
        foreach (var direction in new[] { CurtainDirection.Open, CurtainDirection.Close })
        {
            var start = Stopwatch.GetTimestamp();
            await controller.ExecuteAsync(curtain, direction, TimeSpan.FromMilliseconds(15));
            var on = output.Events.Last(e => e.On);
            Assert(on.Channel == curtain * 2 - (direction == CurtainDirection.Open ? 1 : 0), "Wrong channel.");
            Assert(Stopwatch.GetElapsedTime(start, on.Timestamp) >= TimeSpan.FromMilliseconds(4), "Missing off interval.");
            Assert(output.AllOff, "Timed command did not shut off.");
        }
    }
});

await Check("same-curtain commands rejected; different curtains independent", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    var first = controller.OpenAsync(1, TimeSpan.FromSeconds(2));
    var other = controller.CloseAsync(2, TimeSpan.FromSeconds(2));
    await output.WaitForOn(1);
    await output.WaitForOn(4);
    await Throws<InvalidOperationException>(() => controller.CloseAsync(1, TimeSpan.FromMilliseconds(20)));
    controller.Stop(1);
    await Throws<OperationCanceledException>(() => first);
    Assert(output.IsOn(4), "Stopping curtain 1 affected curtain 2.");
    controller.StopAll();
    await Throws<OperationCanceledException>(() => other);
    Assert(output.AllOff, "StopAll left an output active.");
    await controller.CloseAsync(1, TimeSpan.FromMilliseconds(15));
});

await Check("cancellation during movement and before activation", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    using var cancellation = new CancellationTokenSource();
    var movement = controller.OpenAsync(3, TimeSpan.FromSeconds(2), cancellation.Token);
    await output.WaitForOn(5);
    cancellation.Cancel();
    await Throws<OperationCanceledException>(() => movement);
    Assert(output.AllOff, "Cancellation did not turn off output.");
    var onCount = output.Events.Count(e => e.On);
    await Throws<OperationCanceledException>(() => controller.OpenAsync(1, TimeSpan.FromMilliseconds(15), cancellation.Token));
    var pending = controller.CloseAsync(4, TimeSpan.FromSeconds(2));
    controller.Stop(4);
    await Throws<OperationCanceledException>(() => pending);
    Assert(output.Events.Count(e => e.On) == onCount, "Cancelled command energized a relay.");
});

await Check("invalid commands never energize outputs", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    await Throws<ArgumentOutOfRangeException>(() => controller.OpenAsync(0, TimeSpan.FromSeconds(1)));
    await Throws<ArgumentOutOfRangeException>(() => controller.CloseAsync(5, TimeSpan.FromSeconds(1)));
    await Throws<ArgumentOutOfRangeException>(() => controller.OpenAsync(1, TimeSpan.Zero));
    await Throws<ArgumentOutOfRangeException>(() => controller.OpenAsync(1, TimeSpan.FromSeconds(4)));
    await Throws<ArgumentOutOfRangeException>(() => controller.ExecuteAsync(1, (CurtainDirection)99, TimeSpan.FromSeconds(1)));
    Assert(!output.Events.Any(e => e.On), "Invalid input energized output.");
});

await Check("output fault attempts both off writes and latches fault", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    output.FailChannel = 1;
    await Throws<AggregateException>(() => controller.OpenAsync(1, TimeSpan.FromMilliseconds(20)));
    Assert(output.Events.Last().Channel == 2 && !output.Events.Last().On, "Second off write skipped after first failed.");
    output.FailChannel = null;
    await Throws<InvalidOperationException>(() => controller.CloseAsync(2, TimeSpan.FromMilliseconds(20)));
    controller.StopAll();
});

await Check("activation failure cancels other curtains and cleans outputs", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    var first = controller.OpenAsync(1, TimeSpan.FromSeconds(2));
    await output.WaitForOn(1);
    output.FailChannel = 3;
    output.FailOnlyWhenOn = true;
    await Throws<IOException>(() => controller.OpenAsync(2, TimeSpan.FromMilliseconds(20)));
    await Throws<OperationCanceledException>(() => first);
    Assert(output.AllOff, "Activation failure left another curtain running.");
});

await Check("initialization failure attempts every channel and releases output", async () =>
{
    var output = new RecordingOutput { FailChannel = 1 };
    await Throws<AggregateException>(() =>
    {
        using var controller = Create(output);
        return Task.CompletedTask;
    });
    Assert(output.Events.Count == 8, "Initialization skipped an off write after a failure.");
    Assert(output.Disposed, "Initialization failure did not release output.");
});

await Check("shutdown cancels movement and rejects later commands", async () =>
{
    var output = new RecordingOutput();
    var controller = Create(output);
    var movement = controller.CloseAsync(4, TimeSpan.FromSeconds(2));
    await output.WaitForOn(8);
    controller.Dispose();
    await Throws<OperationCanceledException>(() => movement);
    Assert(output.AllOff && output.Disposed, "Shutdown did not release output.");
    await Throws<ObjectDisposedException>(() => controller.OpenAsync(1, TimeSpan.FromMilliseconds(20)));
    controller.Dispose();
});

await Check("background closes only front layers before selected layer opens", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    var backgrounds = new BackgroundController(controller, [2, 1, 3, 4], TimeSpan.FromMilliseconds(20));
    await backgrounds.SelectAsync(3);
    var onEvents = output.Events.Where(e => e.On).ToList();
    Assert(onEvents.Count == 3 && onEvents.Take(2).Select(e => e.Channel).Order().SequenceEqual(new[] { 2, 4 }),
        "Wrong foreground curtains closed.");
    Assert(onEvents[2].Channel == 5, "Selected curtain did not open after front curtains closed.");
    var selectedOn = onEvents[2].Timestamp;
    foreach (var channel in new[] { 2, 4 })
        Assert(output.Events.Any(e => e.Channel == channel && !e.On && e.Timestamp > onEvents.First(x => x.Channel == channel).Timestamp && e.Timestamp < selectedOn),
            "Selected curtain opened before foreground command finished.");
    Assert(!onEvents.Any(e => e.Channel is 7 or 8), "Background curtain behind selected layer moved.");
});

await Check("cancelled background never opens selected curtain", async () =>
{
    var output = new RecordingOutput();
    using var controller = Create(output);
    var backgrounds = new BackgroundController(controller, [1, 2, 3, 4], TimeSpan.FromSeconds(2));
    using var cancellation = new CancellationTokenSource();
    var selection = backgrounds.SelectAsync(2, cancellation.Token);
    await output.WaitForOn(2);
    cancellation.Cancel();
    await Throws<OperationCanceledException>(() => selection);
    Assert(output.AllOff && !output.Events.Any(e => e.Channel == 3 && e.On), "Cancelled background opened selected curtain.");
});

await Check("UI selection highlights one color, runs relays, and closes all", async () =>
{
    var output = new RecordingOutput();
    var controller = Create(output);
    await using var viewModel = new MainViewModel(new PhotoboothSettings { ActivationMilliseconds = 20 }, controller);
    await viewModel.SelectCurtainCommand.ExecuteAsync(viewModel.Curtains[1]);
    Assert(viewModel.Curtains.Count(c => c.IsSelected) == 1 && viewModel.Curtains[1].IsSelected, "Selection highlight is wrong.");
    Assert(viewModel.SelectionLabel == "Sky Blue selected" && viewModel.CanSelect, "Selection state did not finish.");
    Assert(viewModel.Curtains[0].CommandStatus == "Closed" && viewModel.Curtains[1].CommandStatus == "Opened"
        && viewModel.Curtains[2].CommandStatus == "Closed", "Curtain command status is wrong.");
    Assert(!viewModel.ActivityLog.Any(e => e.Contains("Pink · Closing"))
        && viewModel.ActivityLog.Any(e => e.Contains("Sky Blue · Opened")), "Skipped curtain logged a redundant close, or required open missing.");
    Assert(output.Events.Where(e => e.On).Select(e => e.Channel).SequenceEqual(new[] { 3 }), "UI did not skip the already Closed foreground.");
    var eventCount = output.Events.Count;
    var logCount = viewModel.ActivityLog.Count;
    await viewModel.SelectCurtainCommand.ExecuteAsync(viewModel.Curtains[1]);
    Assert(output.Events.Count == eventCount && viewModel.ActivityLog.Count == logCount,
        "Clicking the selected color repeated relay commands.");
    var beforeClose = output.Events.Count;
    await viewModel.CloseAllCommand.ExecuteAsync(null);
    Assert(viewModel.Curtains.All(c => !c.IsSelected) && output.AllOff, "Close all did not clear selection and stop relays.");
    Assert(output.Events.Skip(beforeClose).Where(e => e.On).Select(e => e.Channel).SequenceEqual(new[] { 4 }), "Close all did not close only the open curtain.");
    Assert(viewModel.Curtains.All(c => c.CommandStatus == "Closed"), "Close all did not update statuses.");
    eventCount = output.Events.Count;
    logCount = viewModel.ActivityLog.Count;
    await viewModel.CloseAllCommand.ExecuteAsync(null);
    Assert(output.Events.Count == eventCount && viewModel.ActivityLog.Count == logCount,
        "Close all retriggered after every curtain was marked Closed.");
    await viewModel.SelectCurtainCommand.ExecuteAsync(viewModel.Curtains[1]);
    Assert(output.Events.Where(e => e.On).Last().Channel == 3,
        "Color could not be selected again after Close all.");
    viewModel.ClearLogCommand.Execute(null);
    Assert(viewModel.ActivityLog.Count == 0 && viewModel.Curtains[1].CommandStatus == "Opened", "Clear log changed curtain status.");
});

await Check("Close all skips simulated Closed startup but still commands Unknown hardware positions", async () =>
{
    var simulatedOutput = new RecordingOutput();
    await using (var vm = new MainViewModel(new PhotoboothSettings(), Create(simulatedOutput)))
    {
        var before = simulatedOutput.Events.Count;
        await vm.CloseAllCommand.ExecuteAsync(null);
        Assert(simulatedOutput.Events.Count == before, "Closed simulated startup triggered relays.");
    }
    var output = new RecordingOutput();
    await using var realVm = new MainViewModel(new PhotoboothSettings { SimulateRelays = false, ActivationMilliseconds = 20 }, Create(output));
    Assert(realVm.Curtains.All(c => c.CommandStatus == "Unknown"), "Real startup inferred position.");
    await realVm.CloseAllCommand.ExecuteAsync(null);
    Assert(output.Events.Where(e => e.On).Select(e => e.Channel).Order().SequenceEqual(new[] { 2, 4, 6, 8 }),
        "Unknown curtains were incorrectly skipped.");
});

await Check("stopped curtain status becomes unknown and is logged", async () =>
{
    var output = new RecordingOutput();
    var controller = Create(output);
    await using var viewModel = new MainViewModel(new PhotoboothSettings { ActivationMilliseconds = 2000 }, controller);
    var selection = viewModel.SelectCurtainCommand.ExecuteAsync(viewModel.Curtains[0]);
    await output.WaitForOn(1);
    Assert(viewModel.Curtains[0].CommandStatus == "Opening", "Moving status not shown.");
    viewModel.StopCommand.Execute(null);
    await selection;
    Assert(viewModel.Curtains[0].CommandStatus == "Unknown" && viewModel.ActivityLog.Any(e => e.Contains("Pink · Unknown")),
        "Stopped curtain was reported opened.");
});

await Check("settings fixed slots reject duplicate colors without swapping or changing relay order", async () =>
{
    var output = new RecordingOutput();
    var palette = new MemoryPaletteStore();
    var settings = new PhotoboothSettings { ActivationMilliseconds = 20, CurtainOrderFrontToBack = [2, 1, 3, 4] };
    await using var vm = new MainViewModel(settings, Create(output), paletteStore: palette);
    var before = output.Events.Count;
    vm.OpenSettingsCommand.Execute(null);
    var editor = vm.CurtainSettings;
    Assert(editor.Slots[0].CurtainId == 2, "Saved physical order was ignored.");
    Assert(vm.DisplayCurtains.Select(c => c.Id).SequenceEqual(editor.Slots.Select(s => s.CurtainId))
        && vm.DisplayCurtains.Select(c => c.DisplayLevel).SequenceEqual(new[] { 1, 2, 3, 4 }),
        "Main buttons/levels do not match setup front-to-back order.");
    editor.Slots[0].SelectedColor = editor.Slots[0].Colors.Single(c => c.Key == "pink");
    Assert(editor.Slots[0].SelectedColor.Key == "blue" && editor.Slots[1].SelectedColor.Key == "pink"
        && editor.Error.Contains("already selected"), "Duplicate swapped or changed another slot.");
    editor.Slots[0].SelectedColor = editor.Slots[0].Colors.Single(c => c.Key == "cyan");
    editor.SaveCommand.Execute(null);
    Assert(vm.IsSettingsOpen && editor.Notice == "Saved" && vm.Curtains[1].Name == "Cyan"
        && palette.Value.Slots[1] == "cyan" && settings.CurtainOrderFrontToBack.SequenceEqual(new[] { 2, 1, 3, 4 })
        && output.Events.Count == before, "Color Save changed relay/order or did not persist.");
    Assert(vm.DisplayCurtains[0].Name == "Cyan" && vm.DisplayCurtains[0].Id == 2,
        "Color Save updated the wrong displayed curtain.");
    editor.Slots[0].SelectedColor = editor.Slots[0].Colors.Single(c => c.Key == "blue");
    editor.CancelCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    Assert(editor.Slots[0].SelectedColor.Key == "cyan", "Cancel retained unsaved color.");
    editor.CancelCommand.Execute(null);
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    Assert(output.Events.Where(e => e.On).Select(e => e.Channel).SequenceEqual(new[] { 1 }), "Color change altered relay mapping or retriggered a Closed foreground.");
});

await Check("palette save failure stays in settings without applying draft", async () =>
{
    var palette = new MemoryPaletteStore { FailSave = true };
    await using var vm = new MainViewModel(new PhotoboothSettings(), Create(new RecordingOutput()), paletteStore: palette);
    vm.OpenSettingsCommand.Execute(null);
    vm.CurtainSettings.Slots[0].SelectedColor = vm.CurtainSettings.Slots[0].Colors.Single(c => c.Key == "cyan");
    vm.CurtainSettings.SaveCommand.Execute(null);
    Assert(vm.IsSettingsOpen && vm.CurtainSettings.Notice == "" && vm.CurtainSettings.Error.Contains("Could not save")
        && vm.Curtains[0].Name == "Pink" && palette.Value.Slots[0] == "pink", "Failed save applied draft.");
});

await Check("saved order reloads and rejects duplicate assignments", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "CurtantControl-check-" + Guid.NewGuid().ToString("N"));
    var path = Path.Combine(folder, "order.json");
    var store = new CurtainOrderStore(path);
    try
    {
        store.Save([4, 2, 1, 3]);
        var loaded = PhotoboothSettings.Load(Path.Combine(folder, "missing-config.json"), store);
        Assert(loaded.CurtainOrderFrontToBack.SequenceEqual(new[] { 4, 2, 1, 3 }), "Saved order did not survive reload.");
        await Throws<InvalidDataException>(() => { store.Save([1, 1, 2, 3]); return Task.CompletedTask; });
        Assert(store.Load()!.SequenceEqual(new[] { 4, 2, 1, 3 }), "Invalid save overwrote valid order.");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
        if (Directory.Exists(folder)) Directory.Delete(folder);
    }
});

await Check("camera discovery parses video devices and excludes metadata", () =>
{
    var devices = CameraCatalog.ParseDirectShow("""
        [dshow @ 123] "USB Camera" (video)
        [dshow @ 123] Alternative name "@device_cam_a"
        [dshow @ 123] "USB Camera" (video)
        [dshow @ 123] Alternative name "@device_cam_b"
        [dshow @ 123] "Microphone" (audio)
        [dshow @ 123] Alternative name "@device_audio"
        """);
    Assert(devices.Count == 2 && devices.Select(c => c.Id).SequenceEqual(new[] { "@device_cam_a", "@device_cam_b" }), "DirectShow video device IDs wrong.");
    var legacy = CameraCatalog.ParseDirectShow("DirectShow video devices\n\"Camera\"\nAlternative name \"id\"\nDirectShow audio devices\n\"Mic\"");
    Assert(legacy.Count == 1 && legacy[0].Id == "id", "Legacy DirectShow list parsing failed.");
    Assert(!CameraCatalog.HasCaptureCapability("Capabilities: Video Capture\nDevice Caps: Metadata Capture"), "Metadata node treated as camera.");
    Assert(CameraCatalog.HasCaptureCapability("Device Caps: Video Capture\nStreaming"), "Capture node excluded.");
    Assert(!CameraCatalog.HasCaptureCapability("Device Caps : 0x84204000\n\tVideo Memory-to-Memory Multiplanar\nFormat Video Capture Multiplanar:"),
        "Codec pipeline incorrectly listed as camera.");
    Assert(!CameraCatalog.HasCaptureCapability("Device Caps : 0x04201000\n\tVideo Capture Multiplanar"),
        "Multi-planar ISP pipeline incorrectly listed for single-planar capture.");
    Assert(CameraCatalog.HasCaptureCapability("Capabilities : 0x84a00001\nDevice Caps : 0x04200001\n\tVideo Capture"),
        "Single-planar camera excluded.");
    return Task.CompletedTask;
});

await Check("Developer camera selection and Auto name carry into User mode", async () =>
{
    var catalog = new FakeCameraCatalog();
    var rig = new FakeCameraRig();
    await using var vm = new MainViewModel(new PhotoboothSettings(),
        orderStore: new MemoryOrderStore(), cameraCatalog: catalog, cameraFactory: rig.Create);
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.StartCamera();
    await vm.RefreshCamerasCommand.ExecuteAsync(null);
    vm.SelectedCamera = vm.Cameras[1];
    await rig.WaitForStart("camera-b");
    var selected = vm.SelectedCamera;
    vm.ChooseModeCommand.Execute(null);
    vm.EnterUserModeCommand.Execute(null);
    Assert(ReferenceEquals(vm.SelectedCamera, selected) && vm.CameraDisplayName == selected!.Name,
        "User mode did not retain Developer camera selection/name.");
    Assert(rig.Active == 1 && rig.MaximumActive == 1, "Mode switch overlapped camera sessions.");
    catalog.Devices = [new CameraDevice(CameraCatalog.AutomaticCameraId, "Auto")];
    vm.EnterDeveloperModeCommand.Execute(null);
    await vm.RefreshCamerasCommand.ExecuteAsync(null);
    vm.EnterUserModeCommand.Execute(null);
    Assert(vm.SelectedCamera?.Id == CameraCatalog.AutomaticCameraId && vm.CameraDisplayName == "Auto",
        "Automatic source is not named Auto in User mode.");
    vm.CameraLive = true;
    Assert(vm.UserCameraLabel == "Live Camera Preview", "Original live heading was replaced with source name.");
    vm.CameraLive = false;
    Assert(vm.UserCameraLabel == "Camera preview offline", "Original offline heading was replaced.");
});

await Check("camera selection closes old stream, refresh handles disconnection", async () =>
{
    var catalog = new FakeCameraCatalog();
    var rig = new FakeCameraRig();
    await using var viewModel = new MainViewModel(new PhotoboothSettings { CameraDevice = "camera-b" },
        orderStore: new MemoryOrderStore(), cameraCatalog: catalog, cameraFactory: rig.Create);
    await viewModel.RefreshCamerasCommand.ExecuteAsync(null);
    Assert(viewModel.Cameras.Count == 2 && viewModel.SelectedCamera?.Id == "camera-b", "Configured camera not selected.");
    viewModel.SelectedCamera = viewModel.Cameras[0];
    await rig.WaitForStart("camera-a");
    Assert(rig.Active == 1 && rig.MaximumActive == 1, "Camera streams overlapped during switch.");
    viewModel.SelectedCamera = viewModel.Cameras[1];
    viewModel.SelectedCamera = viewModel.Cameras[0];
    await viewModel.ReconnectCameraCommand.ExecuteAsync(null);
    Assert(viewModel.SelectedCamera?.Id == "camera-a" && rig.Active == 1 && rig.MaximumActive == 1,
        "Rapid selection left an obsolete or overlapping stream.");
    catalog.Devices = [new CameraDevice("camera-b", "USB camera B")];
    await viewModel.RefreshCamerasCommand.ExecuteAsync(null);
    Assert(viewModel.SelectedCamera?.Id == "camera-b" && rig.Active == 1, "Disconnected camera did not switch to available camera.");
    catalog.Devices = [];
    await viewModel.RefreshCamerasCommand.ExecuteAsync(null);
    Assert(viewModel.Cameras.Count == 0 && viewModel.SelectedCamera is null && rig.Active == 0
        && viewModel.CameraStatus.Contains("No cameras"), "Empty camera list left capture running.");
});

await Check("camera discovery failure is displayed and capture stops on shutdown", async () =>
{
    var catalog = new FakeCameraCatalog();
    var rig = new FakeCameraRig();
    var viewModel = new MainViewModel(new PhotoboothSettings(), cameraCatalog: catalog, cameraFactory: rig.Create);
    await viewModel.RefreshCamerasCommand.ExecuteAsync(null);
    catalog.Fail = true;
    await viewModel.RefreshCamerasCommand.ExecuteAsync(null);
    Assert(viewModel.CameraStatus.Contains("discovery failed"), "Camera discovery error not shown.");
    await viewModel.DisposeAsync();
    Assert(rig.Active == 0, "Shutdown left camera capture active.");
});

await Check("legacy CSI uses raspivid/H264 pipe, not direct V4L2 capture", () =>
{
    Assert(CameraCatalog.HasLegacyCamera("supported=1 detected=1, libcamera interfaces=0"), "Detected legacy CSI camera excluded.");
    Assert(!CameraCatalog.HasLegacyCamera("supported=1 detected=0, libcamera interfaces=0"), "Missing CSI camera reported present.");
    Assert(CameraCatalog.IsInternalPipeline("bcm2835-isp-capture0")
        && CameraCatalog.IsInternalPipeline("bcm2835-codec-decode")
        && !CameraCatalog.IsInternalPipeline("USB Camera"), "Internal pipeline filter incorrect.");
    var decoder = CameraProcessPlan.Ffmpeg(CameraCatalog.LegacyCameraId, windows: false);
    Assert(decoder.Contains("h264") && decoder.Contains("pipe:0") && decoder.Contains("pipe:1")
        && !decoder.Contains("v4l2"), "CSI incorrectly routed to a device node.");
    var source = CameraProcessPlan.Raspivid();
    Assert(source.Contains("-n") && source.Contains("640") && source.Contains("360") && source.Last() == "-", "CSI source is not preview-free stdout capture.");
    var usb = CameraProcessPlan.Ffmpeg("/dev/video0", windows: false);
    Assert(usb.Contains("v4l2") && usb.Contains("/dev/video0") && !usb.Contains("pipe:0"), "USB capture was changed to CSI pipeline.");
    return Task.CompletedTask;
});

await Check("configured MJPEG stream bypasses physical camera discovery and capture", async () =>
{
    const string url = "http://127.0.0.1:5000/video_feed";
    var catalog = new CameraCatalog("nonexistent-ffmpeg-for-discovery-check", url);
    var devices = await catalog.ListAsync(CancellationToken.None);
    Assert(devices.Count == 1 && devices[0].Id == url, "Stream option missing or physical cameras also listed.");
    var args = CameraProcessPlan.Ffmpeg(url, windows: false);
    Assert(args.Contains("mjpeg") && args.Contains("pipe:0") && !args.Contains("v4l2")
        && !args.Contains("dshow") && !args.Contains(url), "Stream must decode extracted JPEGs through stdin.");
    await Throws<ArgumentException>(() => new CameraCatalog("unused", "file:///tmp/video").ListAsync(CancellationToken.None));
    var rig = new FakeCameraRig();
    await using var viewModel = new MainViewModel(new PhotoboothSettings { CameraStreamUrl = url },
        orderStore: new MemoryOrderStore(), cameraFactory: rig.Create, cameraCatalog: catalog);
    await viewModel.RefreshCamerasCommand.ExecuteAsync(null);
    await rig.WaitForStart(url);
    Assert(viewModel.SelectedCamera?.Id == url && rig.Active == 1, "Configured stream was not selected and started.");
});

await Check("automatic camera falls back sequentially after failure and disconnect", async () =>
{
    using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var attempts = new List<string>();
    var messages = new List<string>();
    var active = 0;
    var maximum = 0;
    var frames = 0;
    var automatic = new AutomaticCameraPreview(
        _ => Task.FromResult<IReadOnlyList<CameraDevice>>(
            [new("service", "Service"), new("usb", "USB"), new("csi", "Ribbon")]),
        id => new DelegateCamera(async (onFrame, token) =>
        {
            attempts.Add(id);
            maximum = Math.Max(maximum, ++active);
            try
            {
                if (id == "service") throw new IOException("HTTP unavailable");
                if (id == "usb") { await onFrame([1]); throw new IOException("Unplugged"); }
                await onFrame([2]);
                cancel.Cancel();
                token.ThrowIfCancellationRequested();
            }
            finally { active--; }
        }), messages.Add);
    await Throws<OperationCanceledException>(() => automatic.RunAsync(_ => { frames++; return Task.CompletedTask; }, cancel.Token));
    Assert(attempts.SequenceEqual(new[] { "service", "usb", "csi" }) && frames == 2,
        "Automatic camera failed to continue after service/USB failure.");
    Assert(maximum == 1 && active == 0 && messages.Any(m => m.Contains("Unplugged")), "Cleanup or diagnostics missing.");
});

await Check("automatic catalog retains service and selector when hardware discovery is unavailable", async () =>
{
    const string url = "http://127.0.0.1:5000/video_feed";
    var catalog = new CameraCatalog("nonexistent-camera-discovery-executable", url, automatic: true);
    var devices = await catalog.ListAsync(CancellationToken.None);
    Assert(devices[0].Id == CameraCatalog.AutomaticCameraId && devices.Any(d => d.Id == url),
        "Hardware discovery failure hid automatic/service options.");
    using var cancel = new CancellationTokenSource();
    cancel.Cancel();
    await Throws<OperationCanceledException>(() => catalog.ListAsync(cancel.Token));
});

await Check("automatic camera rescans after no devices or discovery failure and stops on cancel", async () =>
{
    using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var scans = 0;
    var automatic = new AutomaticCameraPreview(_ =>
    {
        scans++;
        if (scans == 1) throw new IOException("Discovery failed");
        if (scans == 3) cancel.Cancel();
        return Task.FromResult<IReadOnlyList<CameraDevice>>(Array.Empty<CameraDevice>());
    }, _ => throw new Exception("No camera should open"), _ => { }, TimeSpan.FromMilliseconds(1));
    await Throws<OperationCanceledException>(() => automatic.RunAsync(_ => Task.CompletedTask, cancel.Token));
    Assert(scans == 3, "Empty discovery did not retry or cancellation did not stop retries.");
});

await Check("disabled local Python feed activates once then streams after service accepts", async () =>
{
    var methods = new List<string>();
    byte[] jpeg = [0xff,0xd8,0xff,0xda,0,2,1,0xff,0xd9];
    using var client = new HttpClient(new DelegateHttpHandler(async request =>
    {
        methods.Add(request.Method + " " + request.RequestUri!.AbsolutePath);
        if (methods.Count == 1) return new(HttpStatusCode.NoContent);
        if (request.Method == HttpMethod.Post)
        {
            Assert(request.RequestUri.AbsolutePath == "/camera_control" &&
                await request.Content!.ReadAsStringAsync() == "{\"mode\":1}", "Wrong activation command.");
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"type\":\"camera\"}") };
        }
        return new(HttpStatusCode.OK) { Content = new ByteArrayContent(jpeg) };
    }));
    using var output = new MemoryStream();
    await HttpMjpegInput.CopyAsync(client, "http://127.0.0.1:5000/video_feed", output, CancellationToken.None);
    Assert(methods.SequenceEqual(new[] { "GET /video_feed", "POST /camera_control", "GET /video_feed" })
        && output.ToArray().SequenceEqual(jpeg), "Activation/retry sequence incorrect.");
});

await Check("Python activation refuses denied service, unrelated URL and disabled setting", async () =>
{
    foreach (var url in new[] { "http://192.168.1.2:5000/video_feed", "http://localhost:5000/other", "http://localhost:8080/video_feed" })
    {
        var calls = 0;
        using var client = new HttpClient(new DelegateHttpHandler(request =>
        {
            calls++;
            Assert(request.Method == HttpMethod.Get, "Activation sent to unrelated service.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }));
        await Throws<InvalidDataException>(() => HttpMjpegInput.CopyAsync(client, url, Stream.Null, CancellationToken.None));
        Assert(calls == 1, "Unrelated endpoint retried.");
    }
    var count = 0;
    using var disabled = new HttpClient(new DelegateHttpHandler(_ =>
    {
        count++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }));
    await Throws<InvalidDataException>(() => HttpMjpegInput.CopyAsync(disabled, "http://127.0.0.1:5000/video_feed", Stream.Null, CancellationToken.None, false));
    Assert(count == 1, "Opt-out ignored.");
    using var denied = new HttpClient(new DelegateHttpHandler(request => Task.FromResult(
        request.Method == HttpMethod.Get ? new HttpResponseMessage(HttpStatusCode.NoContent) :
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"type\":\"text\",\"value\":\"No camera connected\"}") })));
    await Throws<InvalidDataException>(() => HttpMjpegInput.CopyAsync(denied, "http://127.0.0.1:5000/video_feed", Stream.Null, CancellationToken.None));
    var retryCalls = 0;
    using var stillDisabled = new HttpClient(new DelegateHttpHandler(request =>
    {
        retryCalls++;
        return Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"type\":\"camera\"}") }
            : new HttpResponseMessage(HttpStatusCode.NoContent));
    }));
    await Throws<InvalidDataException>(() => HttpMjpegInput.CopyAsync(stillDisabled, "http://127.0.0.1:5000/video_feed", Stream.Null, CancellationToken.None));
    Assert(retryCalls == 3, "Activation repeated indefinitely.");
});

await Check("MJPEG reader ignores bad boundaries and preserves JPEG segment/entropy markers", async () =>
{
    byte[] first = [0xff,0xd8, 0xff,0xe1,0,8, 0xff,0xd8,0xff,0xd9,0xaa,0xbb,
        0xff,0xda,0,2, 1,0xff,0,0xd9,0xff,0xd0,2, 0xff,0xd9];
    byte[] second = [0xff,0xd8,0xff,0xda,0,2,5,0xff,0xd9];
    var header = Encoding.ASCII.GetBytes("frame\r\nContent-Type: image/jpeg\r\n\r\n");
    var bytes = header.Concat(first).Concat(header).Concat(second).ToArray();
    using var input = new ChunkedJpegStream(bytes);
    using var output = new MemoryStream();
    await MjpegFrameReader.CopyAsync(input, output, CancellationToken.None);
    Assert(output.ToArray().SequenceEqual(first.Concat(second)), "JPEG extraction altered a frame or included boundary bytes.");
    using var oversized = new ChunkedJpegStream(bytes);
    await Throws<InvalidDataException>(() => MjpegFrameReader.CopyAsync(oversized, Stream.Null, CancellationToken.None, maximumFrameBytes: 12));
    using var truncated = new MemoryStream(first[..^2]);
    await Throws<InvalidDataException>(() => MjpegFrameReader.CopyAsync(truncated, Stream.Null, CancellationToken.None));
});

await Check("HTTP camera accepts malformed multipart framing but rejects HTML errors", async () =>
{
    byte[] jpeg = [0xff,0xd8,0xff,0xda,0,2,1,2,3,0xff,0xd9];
    var bytes = Encoding.ASCII.GetBytes("frame\r\nContent-Type: image/jpeg\r\n\r\n").Concat(jpeg).ToArray();
    using var client = new HttpClient(new CameraHttpHandler(() =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ChunkedJpegStream(bytes)) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/x-mixed-replace");
        return response;
    }));
    using var output = new MemoryStream();
    await HttpMjpegInput.CopyAsync(client, "http://127.0.0.1:5000/video_feed", output, CancellationToken.None);
    Assert(output.ToArray().SequenceEqual(jpeg), "HTTP input failed to normalize malformed multipart stream.");
    using var errorClient = new HttpClient(new CameraHttpHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("<html>Error</html>", Encoding.UTF8, "text/html") }));
    await Throws<InvalidDataException>(() => HttpMjpegInput.CopyAsync(errorClient, "http://127.0.0.1:5000/video_feed", Stream.Null, CancellationToken.None));
});


await Check("Razr II validates motor limits independently from Razr I", async () =>
{
    var config = new RazrConfiguration { Model = RazrModel.RazrII, Position1Angle = 200 };
    var installation = new PhotoboothSettings();
    await Throws<InvalidDataException>(() => { config.Validate(installation); return Task.CompletedTask; });
    config.Model = RazrModel.RazrI;
    config.Validate(installation);
    config.Model = RazrModel.RazrII; config.Position1Angle = 45; config.SimulateMotor = false;
    await Throws<InvalidDataException>(() => { config.Validate(installation); return Task.CompletedTask; });
    config.CalibrationConfirmed = true; config.Validate(installation);
    config.DacAddress = 0x40;
    await Throws<InvalidDataException>(() => { config.Validate(installation); return Task.CompletedTask; });
});
await Check("Razr II startup never initializes curtain GPIO and Razr I never moves camera", async () =>
{
    var hardware = new RecordingRazrHardware();
    await using var camera = new MainViewModel(new PhotoboothSettings { SimulateRelays = false },
        paletteStore: new MemoryPaletteStore(),
        razrStore: new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } },
        razrHardwareFactory: _ => hardware);
    camera.EnterUserModeCommand.Execute(null);
    camera.SelectCameraPositionCommand.Execute(camera.CameraPositions[0]);
    await camera.StartBackgroundCommand.ExecuteAsync(null);
    Assert(!camera.Status.Contains("Relay setup failed") && hardware.Angles.Count == 1, "Camera depended on invalid curtain hardware settings.");
    await using var curtains = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(),
        razrHardwareFactory: _ => hardware);
    curtains.EnterUserModeCommand.Execute(null);
    await curtains.SelectCurtainCommand.ExecuteAsync(curtains.Curtains[0]);
    await curtains.StartBackgroundCommand.ExecuteAsync(null);
    Assert(hardware.Angles.Count == 1 && curtains.CameraPositions.Count == 0, "Razr I accessed camera-position controls.");
});
await Check("Razr persisted configuration round trip and old-install defaults", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "razr-" + Guid.NewGuid());
    var path = Path.Combine(directory, "razr.json");
    try
    {
        var store = new RazrConfigurationStore(path);
        Assert(store.Load().Model == RazrModel.RazrI && store.Load().SimulateMotor, "Fresh settings unsafe.");
        store.Save(new RazrConfiguration { Model = RazrModel.RazrII, Position2Angle = 120, Position2Name = "Custom" });
        var loaded = store.Load();
        Assert(loaded.Model == RazrModel.RazrII && loaded.Position2Angle == 120 && loaded.Position2Name == "Custom", "Settings lost.");
        Assert(!File.Exists(path + ".tmp"), "Temporary config left behind.");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    return Task.CompletedTask;
});
await Check("Razr II stages two positions; START moves motor and never curtains", async () =>
{
    var output = new RecordingOutput();
    var hardware = new RecordingRazrHardware();
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    await using var vm = new MainViewModel(new PhotoboothSettings(), Create(output),
        paletteStore: new MemoryPaletteStore(), razrStore: store, razrHardwareFactory: _ => hardware);
    vm.EnterUserModeCommand.Execute(null);
    var writes = output.Events.Count;
    Assert(vm.SceneRows == 2 && vm.SceneOptions.Count == 2, "Wrong Razr II options.");
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
    Assert(hardware.Angles.Count == 0 && vm.CanStart, "Selection moved camera or not staged.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(hardware.Angles.SequenceEqual(new[] {135d}) && !vm.CanStart, "START wrong angle or retained pending command.");
    Assert(output.Events.Count == writes, "Razr II pulsed curtains.");
    await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
    await vm.CloseAllCommand.ExecuteAsync(null);
    Assert(output.Events.Count == writes, "Hidden curtain commands ran in Razr II.");
});
await Check("Razr camera failure keeps retry selection and unknown status", async () =>
{
    var hardware = new RecordingRazrHardware { FailMove = true };
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(),
        razrStore: new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } },
        razrHardwareFactory: _ => hardware);
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(vm.CanStart && vm.CameraPositions[0].CommandStatus == "Unknown" && !vm.IsMoving, "Failure falsely completed.");
    hardware.FailMove = false;
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(!vm.CanStart && hardware.Angles.Count == 2, "Retry failed.");
});
await Check("separate Razr model selection and camera settings preserve curtain configuration", async () =>
{
    var store = new MemoryRazrStore();
    var hardware = new RecordingRazrHardware();
    var palette = new MemoryPaletteStore();
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: palette, razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware());
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null);
    Assert(vm.IsCurtainSettingsVisible && !vm.IsRazrSettingsOpen, "Razr I gear opened camera setup.");
    vm.CurtainSettings.CancelCommand.Execute(null);
    vm.OpenCameraSettingsCommand.Execute(null);
    Assert(!vm.IsSettingsOpen, "Camera setup accessible from Razr I.");
    vm.OpenRazrSettingsCommand.Execute(null);
    store.FailSave = true;
    vm.EnterRazrIICommand.Execute(null);
    Assert(!vm.IsRazrII && vm.IsModelSelectionOpen, "Failed switch changed model.");
    store.FailSave = false;
    vm.EnterRazrIICommand.Execute(null);
    Assert(vm.IsRazrII && vm.IsRazrIIScreenVisible && !vm.IsRazrIScreenVisible, "Wrong separate UI after switch.");
    vm.OpenSettingsCommand.Execute(null);
    Assert(vm.IsRazrSettingsOpen && !vm.IsCurtainSettingsVisible && !vm.IsModelSelectionOpen, "Razr II gear opened curtain settings.");
    vm.RazrDraft.Position1Angle = 60;
    vm.CancelRazrSettingsCommand.Execute(null);
    Assert(store.Value.Position1Angle == 45, "Cancel applied camera draft.");
    vm.OpenCameraSettingsCommand.Execute(null);
    vm.RazrDraft.Position1Angle = 60;
    store.FailSave = true;
    vm.SaveRazrSettingsCommand.Execute(null);
    Assert(store.Value.Position1Angle == 45 && vm.RazrNotice.Contains("Could not save"), "Failed save applied camera draft.");
    store.FailSave = false;
    vm.SaveRazrSettingsCommand.Execute(null);
    Assert(store.Value.Position1Angle == 60 && palette.Value.Slots.SequenceEqual(new CurtainPalette().Slots), "Camera save altered curtains.");
    vm.CancelRazrSettingsCommand.Execute(null);
    vm.OpenRazrSettingsCommand.Execute(null);
    vm.EnterRazrICommand.Execute(null);
    Assert(!vm.IsRazrII && vm.IsRazrIScreenVisible && !vm.IsRazrIIScreenVisible && store.Value.Position1Angle == 60, "Switch lost independent camera settings.");
    vm.EnterUserModeCommand.Execute(null);
    vm.OpenRazrSettingsCommand.Execute(null);
    Assert(vm.IsPinDialogOpen && !vm.IsModelSelectionOpen, "User switch bypassed PIN.");
    foreach (var digit in "1234") vm.PinDigitCommand.Execute(digit.ToString());
    vm.ConfirmPinCommand.Execute(null);
    Assert(vm.IsModelSelectionOpen, "Correct PIN did not open chooser.");
    vm.EnterRazrIICommand.Execute(null);
    Assert(vm.IsRazrII && vm.IsUserScreenVisible, "User model switch changed operating mode.");
    vm.OpenSettingsCommand.Execute(null);
    vm.OpenCameraSettingsCommand.Execute(null);
    Assert(!vm.IsSettingsOpen && !vm.ShowCurtainSettingsButton && !vm.CanEditCameraSettings, "User Settings still accessible.");
    Assert(store.Value.Position1Angle == 60, "User command changed Developer settings.");
});
await Check("legacy light settings are ignored and no longer saved", () =>
{
    var config = System.Text.Json.JsonSerializer.Deserialize<RazrConfiguration>("{\"Model\":0,\"LightsEnabled\":true,\"SimulateLights\":false,\"Light1Bcm\":22}")!;
    config.Validate(new PhotoboothSettings());
    var saved = System.Text.Json.JsonSerializer.Serialize(config);
    Assert(!saved.Contains("Light") && config.Model == RazrModel.RazrI, "Removed lights survived migration.");
    return Task.CompletedTask;
});
await Check("Razr shutdown cancels active camera movement before releasing hardware", async () =>
{
    var hardware = new RecordingRazrHardware { BlockMove = true };
    var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(),
        razrStore: new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } },
        razrHardwareFactory: _ => hardware);
    vm.EnterUserModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    var running = vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(vm.IsMoving && !vm.CanSelectScene, "Movement guard missing.");
    await vm.DisposeAsync();
    await running;
    Assert(hardware.Disposed && vm.CameraPositions[0].CommandStatus == "Unknown", "Shutdown did not cancel/release.");
});

await Check("Razr II restores independent selected button and completed position without moving", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    await using (var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware()))
    {
        vm.EnterUserModeCommand.Execute(null);
        vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
        Assert(store.Value.LastSimulatedCameraPosition is null, "Browsing saved an applied position.");
        await vm.StartBackgroundCommand.ExecuteAsync(null);
        Assert(store.Value.LastSimulatedCameraPosition is { Id: 102, Completed: true }, "Completion not saved.");
        vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
        vm.EnterDeveloperModeCommand.Execute(null);
        Assert(vm.CameraPositions[0].IsSelected && vm.CanStart, "Mode navigation lost the selected button.");
    }
    var hardware = new RecordingRazrHardware();
    await using var restored = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => hardware);
    restored.EnterUserModeCommand.Execute(null);
    Assert(restored.CameraPositions[0].IsSelected && restored.SelectionLabel.Contains("saved") && restored.CanStart,
        "Restart did not restore selected saved button.");
    Assert(hardware.Angles.Count == 0, "Restoring a button moved the motor.");
});
await Check("Razr II pending-write failure blocks motor and failed movement clears saved completion", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0,
        LastSimulatedCameraPosition = new(101,45,360,true) }, FailSave = true };
    var hardware = new RecordingRazrHardware();
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => hardware);
    vm.EnterUserModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(hardware.Angles.Count == 0 && vm.CanStart, "Motor ran despite failed pending-state write.");
    store.FailSave = false; hardware.FailMove = true;
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(store.Value.LastSimulatedCameraPosition is { Id:102, Completed:false }, "Failure retained stale completed position.");
    vm.EnterDeveloperModeCommand.Execute(null);
    Assert(vm.CameraPositions[1].IsSelected && vm.CameraPositions[1].CommandStatus == "Unknown" && vm.CanStart, "Failed command lost selection or claimed completion.");
});
await Check("Razr II isolates real/simulated snapshots and invalidates changed angle", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII,
        LastSimulatedCameraPosition = new(101,45,360,true), LastCameraPosition = new(102,135,360,true) } };
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware());
    vm.EnterDeveloperModeCommand.Execute(null);
    Assert(vm.CameraPositions[0].IsSelected, "Simulation restored real state.");
    vm.OpenCameraSettingsCommand.Execute(null);
    vm.RazrDraft.SimulateMotor = false; vm.RazrDraft.CalibrationConfirmed = true;
    vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[0].IsSelected && vm.CanStart && store.Value.LastCameraPosition?.Id == 102, "Mode change lost selection or fabricated real completion.");
    vm.OpenCameraSettingsCommand.Execute(null); vm.RazrDraft.Position1Angle = 120;
    vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[0].IsSelected && vm.CanStart, "Changed angle lost selection or blocked reapplication.");
});
await Check("Razr camera snapshot survives configuration disk round trip", () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "camera-state-" + Guid.NewGuid());
    try
    {
        var store = new RazrConfigurationStore(Path.Combine(folder,"razr.json"));
        store.Save(new RazrConfiguration { LastCameraPosition = new(102,135,360,true), LastSimulatedCameraPosition = new(101,45,360,false) });
        var loaded = store.Load();
        Assert(loaded.LastCameraPosition is { Id:102, Completed:true } && loaded.LastSimulatedCameraPosition is { Completed:false }, "Snapshot not persisted.");
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder,true); }
    return Task.CompletedTask;
});
await Check("Razr II keeps selected button through settings cancel, save and motor error", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware { FailMove = true });
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
    vm.OpenCameraSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[1].IsSelected && vm.CanStart, "Settings Back lost selection.");
    vm.OpenCameraSettingsCommand.Execute(null); vm.RazrDraft.Position2Name = "Chosen room";
    vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[1].IsSelected && vm.CameraPositions[1].Name == "Chosen room" && vm.CanStart,
        "Settings Save lost selected ID.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    vm.OpenCameraSettingsCommand.Execute(null); vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[1].IsSelected && vm.CameraPositions[1].CommandStatus == "Unknown" && vm.CanStart,
        "Error/settings lost selection or claimed successful movement.");
    Assert(store.Value.SelectedSimulatedCameraPositionId == 102 && store.Value.LastSimulatedCameraPosition is {Completed:false},
        "Selection and command outcome were not independently saved.");
});
await Check("external camera name JSON is normalized and never overwritten", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "razr-names-" + Guid.NewGuid());
    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "names.json");
    try
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { " Custom A ", "Custom B", "Custom A" });
        File.WriteAllText(path, json);
        Assert(CameraPositionNameCatalog.Load(path).SequenceEqual(new[] { "Custom A", "Custom B" }), "Names not loaded or normalized.");
        Assert(File.ReadAllText(path) == json, "User JSON overwritten.");
        File.WriteAllText(path, "[null]");
        await Throws<InvalidDataException>(() => { CameraPositionNameCatalog.Load(path); return Task.CompletedTask; });
        File.WriteAllText(path, "[");
        await Throws<System.Text.Json.JsonException>(() => { CameraPositionNameCatalog.Load(path); return Task.CompletedTask; });
    }
    finally { Directory.Delete(folder, true); }
});
await Check("Razr II color selection uses curtain palette and applies only after successful Save", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII } };
    var hardware = new RecordingRazrHardware();
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => hardware);
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.OpenCameraSettingsCommand.Execute(null);
    var original = vm.CameraPositions[0].Color.ToString();
    vm.CameraPosition1Choice = vm.CameraPositionChoices.Single(c => c.Name == "Pink");
    Assert(vm.CameraPositions[0].Color.ToString() == original, "Draft recolored main button before Save.");
    vm.CancelRazrSettingsCommand.Execute(null);
    vm.OpenCameraSettingsCommand.Execute(null);
    vm.CameraPosition1Choice = vm.CameraPositionChoices.Single(c => c.Name == "Green");
    store.FailSave = true; vm.SaveRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[0].Color.ToString() == original, "Failed Save applied color.");
    store.FailSave = false; vm.SaveRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[0].Name == "Green" && vm.CameraPositions[0].Color.ToString() == vm.Curtains.Single(c => c.Name == "Green").Color.ToString(), "Razr II color differs from Razr I palette.");
    Assert(hardware.Angles.Count == 0, "Color Save moved camera.");
});
await Check("combined camera options JSON keeps name and color together", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "razr-options-" + Guid.NewGuid());
    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "options.json");
    try
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { new CameraNameColor(" Room A ","#123456"), new CameraNameColor("Room B","pink") });
        File.WriteAllText(path,json);
        var loaded = CameraPositionNameCatalog.LoadOptions(path);
        Assert(loaded[0] == new CameraNameColor("Room A","#123456") && loaded[1].Color == "pink", "Combined values lost.");
        Assert(File.ReadAllText(path) == json, "Options JSON rewritten.");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new[]{new CameraNameColor("A","pink"),new CameraNameColor("a","green")}));
        await Throws<InvalidDataException>(() => { CameraPositionNameCatalog.LoadOptions(path); return Task.CompletedTask; });
    }
    finally { Directory.Delete(folder,true); }
});
await Check("Razr II selected state matches Razr I repeat and browse-back behavior", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    var hardware = new RecordingRazrHardware();
    await using var vm = new MainViewModel(new PhotoboothSettings(), paletteStore: new MemoryPaletteStore(), razrStore: store,
        razrHardwareFactory: _ => hardware);
    vm.EnterUserModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(vm.CameraPositions[0].IsSelected && !vm.CanStart, "Completion lost highlight or left STATE active.");
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    Assert(!vm.CanStart, "Already applied button enabled repeated movement.");
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
    Assert(vm.CameraPositions[1].IsSelected && vm.CanStart, "Other position was not staged.");
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    Assert(vm.CameraPositions[0].IsSelected && !vm.CanStart, "Browsing back lost applied state.");
    vm.OpenCameraSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[0].IsSelected && !vm.CanStart, "Settings Back lost applied state.");
    vm.OpenCameraSettingsCommand.Execute(null); vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[0].IsSelected && !vm.CanStart, "Settings Save lost applied state.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(hardware.Angles.Count == 1, "Repeated movement was sent.");
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]); await vm.StartBackgroundCommand.ExecuteAsync(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    Assert(vm.CanStart && vm.CameraPositions[0].IsSelected, "Applying a different position did not allow return.");
});
await Check("Razr II routes positions to R1/R2 without creating a motor in relay-only mode", async () =>
{
    var output = new RecordingOutput();
    var installation = new PhotoboothSettings { ActivationMilliseconds = 20 };
    var configuration = new RazrConfiguration { Model = RazrModel.RazrII, ControlMode = RazrControlMode.Relays,
        SimulateMotor = false, CalibrationConfirmed = false };
    configuration.Validate(installation); // An unavailable motor must not block the backup.
    var created = 0;
    using var hardware = new RazrPositionHardware(configuration, installation,
        () => throw new Exception("Relay-only touched motor"), () => { created++; return Create(output); });
    Assert(created == 0, "Construction initialized outputs.");
    await hardware.MovePositionAsync(1, double.NaN, CancellationToken.None);
    await hardware.MovePositionAsync(2, double.NaN, CancellationToken.None);
    Assert(created == 1 && output.Events.Where(e => e.On).Select(e => e.Channel).SequenceEqual(new[] {1,2}), "Wrong relay routing.");
    Assert(output.AllOff, "Relay was left energized.");
});
await Check("Razr II motor-only never initializes relays", async () =>
{
    var motor = new RecordingRazrHardware();
    using var hardware = new RazrPositionHardware(new() { Model = RazrModel.RazrII }, new(),
        () => motor, () => throw new Exception("Motor-only touched relay"));
    await hardware.MovePositionAsync(2, 135, CancellationToken.None);
    Assert(motor.Angles.SequenceEqual(new[] {135d}), "Motor angle changed.");
});
await Check("Razr II parallel outputs overlap and cancellation releases relay", async () =>
{
    var output = new RecordingOutput();
    var motor = new RecordingRazrHardware { BlockMove = true };
    using var hardware = new RazrPositionHardware(new() { Model = RazrModel.RazrII, ControlMode = RazrControlMode.MotorAndRelays },
        new() { ActivationMilliseconds = 1000 }, () => motor, () => Create(output));
    using var cancel = new CancellationTokenSource();
    var running = hardware.MovePositionAsync(1,45,cancel.Token);
    await output.WaitForOn(1);
    Assert(motor.Angles.Count == 1 && !running.IsCompleted, "Outputs did not overlap.");
    cancel.Cancel();
    await Throws<OperationCanceledException>(() => running);
    Assert(output.AllOff, "Cancellation left relay on.");
});
await Check("Razr II parallel relay failure cancels motor and motor failure suppresses pulse", async () =>
{
    var output = new RecordingOutput { FailChannel = 1, FailOnlyWhenOn = true };
    using (var hardware = new RazrPositionHardware(new() { Model = RazrModel.RazrII, ControlMode = RazrControlMode.MotorAndRelays },
        new(), () => new RecordingRazrHardware { BlockMove = true }, () => Create(output)))
        await Throws<IOException>(() => hardware.MovePositionAsync(1,45,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3)));
    Assert(output.AllOff, "Failure left relay on.");
    var second = new RecordingOutput();
    using var failedMotor = new RazrPositionHardware(new() { Model = RazrModel.RazrII, ControlMode = RazrControlMode.MotorAndRelays },
        new(), () => new RecordingRazrHardware { FailMove = true }, () => Create(second));
    await Throws<IOException>(() => failedMotor.MovePositionAsync(2,135,CancellationToken.None));
    Assert(!second.Events.Any(e => e.On), "Relay fired after immediate motor failure.");
});
await Check("Razr II developer mode selection persists, invalidates completion, and is protected in User", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware());
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    vm.OpenCameraSettingsCommand.Execute(null);
    vm.SelectedControlMode = vm.ControlModeChoices.Single(c => c.Mode == RazrControlMode.Relays);
    vm.CancelRazrSettingsCommand.Execute(null);
    Assert(store.Value.ControlMode == RazrControlMode.Motor, "Cancelled mode applied.");
    vm.OpenCameraSettingsCommand.Execute(null);
    vm.SelectedControlMode = vm.ControlModeChoices.Single(c => c.Mode == RazrControlMode.Relays);
    vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(store.Value.ControlMode == RazrControlMode.Relays && vm.CanStart, "Mode change did not invalidate motor-only completion.");
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(store.Value.LastSimulatedCameraPosition?.ExecutionProfile is not null, "Relay profile not persisted.");
    vm.EnterUserModeCommand.Execute(null); vm.OpenCameraSettingsCommand.Execute(null);
    vm.RazrDraft.ControlMode = RazrControlMode.MotorAndRelays;
    vm.SaveRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
    Assert(store.Value.ControlMode == RazrControlMode.Relays, "User changed Developer mode.");
    var json = System.Text.Json.JsonSerializer.Serialize(store.Value);
    Assert(System.Text.Json.JsonSerializer.Deserialize<RazrConfiguration>(json)!.ControlMode == RazrControlMode.Relays, "Mode did not roundtrip.");
});
await Check("Razr II validates relay wiring and pulse before commands", async () =>
{
    var config = new RazrConfiguration { Model = RazrModel.RazrII, ControlMode = RazrControlMode.Relays };
    await Throws<InvalidDataException>(() => { config.Validate(new() { ActivationMilliseconds = 0 }); return Task.CompletedTask; });
    await Throws<InvalidDataException>(() => { config.Validate(new() { SimulateRelays = false, ActiveLow = false,
        BcmPinsByChannel = new[] {2,10,9,11,5,6,13,19} }); return Task.CompletedTask; });
});
await Check("Razr I restores last selected button from disk without issuing relays", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    try
    {
        var store = new RazrConfigurationStore(Path.Combine(folder, "razr.json"));
        foreach (var apply in new[] { false, true })
        {
            await using (var vm = new MainViewModel(new() { ActivationMilliseconds = 10 },
                cameraCatalog: new FakeCameraCatalog(), razrStore: store))
            {
                vm.EnterUserModeCommand.Execute(null);
                await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[2]);
                if (apply) await vm.StartBackgroundCommand.ExecuteAsync(null);
            }
            var output = new RecordingOutput();
            using var relays = Create(output);
            await using var restored = new MainViewModel(new(), relays,
                cameraCatalog: new FakeCameraCatalog(), razrStore: store);
            var before = output.Events.Count;
            restored.EnterUserModeCommand.Execute(null);
            Assert(restored.Curtains[2].IsSelected && restored.CanStart, "Last Razr I selection lost on relaunch.");
            Assert(output.Events.Count == before, "Selection restoration moved relays.");
        }
    }
    finally { Directory.Delete(folder, true); }
});
await Check("Razr II preserves selected button across simulation and all output mode settings", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware());
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
    await vm.StartBackgroundCommand.ExecuteAsync(null);
    foreach (var mode in new[] { RazrControlMode.Relays, RazrControlMode.MotorAndRelays, RazrControlMode.Motor })
    foreach (var simulated in new[] { false, true })
    {
        vm.OpenCameraSettingsCommand.Execute(null);
        vm.RazrDraft.ControlMode = mode;
        vm.RazrDraft.SimulateMotor = simulated;
        vm.RazrDraft.CalibrationConfirmed = true;
        vm.RazrDraft.Position2Angle = 120;
        vm.RazrDraft.SettleSeconds = 0;
        vm.SaveRazrSettingsCommand.Execute(null);
        vm.CancelRazrSettingsCommand.Execute(null);
        Assert(vm.CameraPositions[1].IsSelected && !vm.CameraPositions[0].IsSelected,
            "Changing motor/relay/simulation lost selection.");
        await vm.StartBackgroundCommand.ExecuteAsync(null);
        vm.OpenCameraSettingsCommand.Execute(null);
        vm.CancelRazrSettingsCommand.Execute(null);
        Assert(vm.CameraPositions[1].IsSelected, "Settings Back lost selection.");
    }
});
await Check("Razr II selection survives full navigation/settings/result/restart matrix", async () =>
{
    foreach (var id in new[] {101,102})
    foreach (var mode in Enum.GetValues<RazrControlMode>())
    foreach (var fail in new[] {false,true})
    {
        var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, ControlMode = mode, SettleSeconds = 0 } };
        await using (var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
            razrHardwareFactory: _ => new RecordingRazrHardware { FailMove = fail }))
        {
            vm.EnterDeveloperModeCommand.Execute(null);
            vm.SelectCameraPositionCommand.Execute(vm.CameraPositions.Single(p => p.Id == id));
            await vm.StartBackgroundCommand.ExecuteAsync(null);
            void Selected(string step) => Assert(vm.CameraPositions.Single(p => p.Id == id).IsSelected
                && vm.CameraPositions.Count(p => p.IsSelected) == 1, $"Lost {id}/{mode}/{fail} at {step}");
            Selected("STATE");
            vm.OpenRazrSettingsCommand.Execute(null); vm.CancelRazrSettingsCommand.Execute(null);
            Selected("model chooser Back");
            Assert(vm.CanStart == fail, "Chooser Back discarded retry/applied state.");
            vm.ChooseModeCommand.Execute(null); vm.EnterUserModeCommand.Execute(null); Selected("User");
            vm.ChooseModeCommand.Execute(null); vm.EnterDeveloperModeCommand.Execute(null); Selected("Developer");
            vm.OpenRazrSettingsCommand.Execute(null); vm.EnterRazrICommand.Execute(null);
            vm.OpenRazrSettingsCommand.Execute(null); vm.EnterRazrIICommand.Execute(null); Selected("Razr I/II");
            foreach (var setting in new[] {"name","angle","wait","mode","simulation","limits","timeout","calibration"})
            {
                vm.OpenCameraSettingsCommand.Execute(null);
                switch (setting)
                {
                    case "name": vm.RazrDraft.Position1Name = "Green"; break;
                    case "angle": vm.RazrDraft.Position1Angle = 60; break;
                    case "wait": vm.RazrDraft.SettleSeconds = 1; break;
                    case "mode": vm.RazrDraft.ControlMode = RazrControlMode.Motor; break;
                    case "simulation": vm.RazrDraft.SimulateMotor = false; vm.RazrDraft.CalibrationConfirmed = true; break;
                    case "limits": vm.RazrDraft.MaximumAngle = 170; break;
                    case "timeout": vm.RazrDraft.MoveTimeoutSeconds = 20; break;
                    case "calibration": vm.RazrDraft.DacAddress = 97; break;
                }
                vm.SaveRazrSettingsCommand.Execute(null); Selected(setting + " Save");
                vm.SaveRazrSettingsCommand.Execute(null); Selected(setting + " repeat Save");
                vm.CancelRazrSettingsCommand.Execute(null); Selected(setting + " Back");
            }
            vm.OpenCameraSettingsCommand.Execute(null); vm.RazrDraft.Position1Name = vm.RazrDraft.Position2Name;
            vm.SaveRazrSettingsCommand.Execute(null); Selected("invalid Save");
            vm.CancelRazrSettingsCommand.Execute(null);
            store.FailSave = true; vm.OpenCameraSettingsCommand.Execute(null);
            vm.SaveRazrSettingsCommand.Execute(null); Selected("write failure"); vm.CancelRazrSettingsCommand.Execute(null);
            store.FailSave = false;
        }
        var hardware = new RecordingRazrHardware();
        await using var restored = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
            razrHardwareFactory: _ => hardware);
        restored.EnterUserModeCommand.Execute(null);
        Assert(restored.CameraPositions.Single(p => p.Id == id).IsSelected && hardware.Angles.Count == 0,
            "Restart lost selection or moved hardware.");
    }
});
await Check("Razr II independent selection file survives config loss and invalid hardware settings", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "camera-selection-" + Guid.NewGuid());
    try
    {
        var path = Path.Combine(directory, "selection.json");
        var selection = new CameraButtonSelectionStore(path);
        Assert(selection.Load() is null, "Missing selection fabricated an ID.");
        selection.Save(102);
        Assert(new CameraButtonSelectionStore(path).Load()?.Id == 102 && Directory.GetFiles(directory, "*.tmp").Length == 0,
            "Selection did not roundtrip atomically.");
        var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SelectedSimulatedCameraPositionId = 101,
            Position1Angle = 999 } };
        var hardware = new RecordingRazrHardware();
        await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
            razrHardwareFactory: _ => hardware, cameraButtonSelectionStore: selection);
        vm.EnterUserModeCommand.Execute(null);
        Assert(vm.IsRazrII && vm.CameraPositions[1].IsSelected && hardware.Angles.Count == 0,
            "Hardware validation cleared independent choice or controller.");
        store.FailSave = true;
        vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[0]);
        Assert(selection.Load()?.Id == 101 && vm.CameraPositions[0].IsSelected, "Config failure lost independent selection.");
        File.WriteAllText(path,"broken");
        await Throws<System.Text.Json.JsonException>(() => { selection.Load(); return Task.CompletedTask; });
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
});
await Check("Razr II selection migration ignores completion validity and output context", async () =>
{
    foreach (var legacy in new[]
    {
        new RazrConfiguration { Model = RazrModel.RazrII, SimulateMotor = true, SelectedCameraPositionId = 102 },
        new RazrConfiguration { Model = RazrModel.RazrII, LastSimulatedCameraPosition = new(102,999,360,false) }
    })
    {
        await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(),
            razrStore: new MemoryRazrStore { Value = legacy }, razrHardwareFactory: _ => new RecordingRazrHardware());
        vm.EnterUserModeCommand.Execute(null);
        Assert(vm.CameraPositions[1].IsSelected && vm.CanStart, "Legacy selection was filtered by command validity.");
    }
});
await Check("Razr II settings draft cannot restore stale history after repeated Save", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SettleSeconds = 0 } };
    await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
        razrHardwareFactory: _ => new RecordingRazrHardware());
    vm.EnterDeveloperModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]); await vm.StartBackgroundCommand.ExecuteAsync(null);
    vm.OpenCameraSettingsCommand.Execute(null);
    vm.RazrDraft.SelectedCameraButtonId = 101;
    vm.RazrDraft.SelectedSimulatedCameraPositionId = 101;
    vm.RazrDraft.LastSimulatedCameraPosition = null;
    vm.SaveRazrSettingsCommand.Execute(null); vm.SaveRazrSettingsCommand.Execute(null);
    vm.CancelRazrSettingsCommand.Execute(null);
    Assert(vm.CameraPositions[1].IsSelected && !vm.CanStart && store.Value.LastSimulatedCameraPosition?.Id == 102,
        "Draft rolled history or selection backwards.");
});
await Check("Razr II restart resolves freshest selection across both durable files", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "camera-restart-" + Guid.NewGuid());
    try
    {
        var config = new RazrConfigurationStore(Path.Combine(directory, "razr.json"));
        var selection = new CameraButtonSelectionStore(Path.Combine(directory, "selection.json"));
        config.Save(new() { Model = RazrModel.RazrII, SettleSeconds = 0 });
        foreach (var id in new[] {102,101,102})
        {
            await using (var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: config,
                cameraButtonSelectionStore: selection, razrHardwareFactory: _ => new RecordingRazrHardware()))
            {
                vm.EnterUserModeCommand.Execute(null);
                vm.SelectCameraPositionCommand.Execute(vm.CameraPositions.Single(p => p.Id == id));
                await vm.StartBackgroundCommand.ExecuteAsync(null);
            }
            await using var restored = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: config,
                cameraButtonSelectionStore: selection, razrHardwareFactory: _ => new RecordingRazrHardware());
            restored.EnterUserModeCommand.Execute(null);
            Assert(restored.CameraPositions.Single(p => p.Id == id).IsSelected, "Actual file restart lost last STATE button.");
        }
        // Simulate either destination missing its latest write, without fabricating motor completion.
        foreach (var independentNewer in new[] {false,true})
        {
            var before = DateTimeOffset.UtcNow;
            var after = before.AddSeconds(1);
            config.Save(new() { Model = RazrModel.RazrII, SelectedCameraButtonId = 101,
                SelectedCameraButtonSavedAt = independentNewer ? before : after });
            selection.Save(new CameraButtonSelection(102, independentNewer ? after : before));
            await using var restored = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: config,
                cameraButtonSelectionStore: selection, razrHardwareFactory: _ => new RecordingRazrHardware());
            restored.EnterUserModeCommand.Execute(null);
            Assert(restored.CameraPositions.Single(p => p.Id == (independentNewer ? 102 : 101)).IsSelected && restored.CanStart,
                "Stale selection file overrode a newer saved choice.");
        }
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
});
await Check("Razr II real motor writes DAC targets without opening or reading ADC", async () =>
{
    var device = new RecordingDac();
    var config = new RazrConfiguration { Model = RazrModel.RazrII, SimulateMotor = false, CalibrationConfirmed = true,
        FeedbackMinVolts = 99, FeedbackMaxVolts = -99, AngleTolerance = -1, MoveTimeoutSeconds = -1, AdcAddress = -1 };
    config.Validate(new()); // Obsolete feedback settings cannot block a command.
    var opened = new List<int>();
    using (var hardware = new RazrHardware(config, settings => { opened.Add(settings.DeviceAddress); return device; }))
    {
        await hardware.MoveCameraAsync(45, CancellationToken.None);
        await hardware.MoveCameraAsync(135, CancellationToken.None);
        Assert(opened.SequenceEqual(new[] {0x60}) && device.Reads == 0 && device.Writes.Count == 2,
            "Motor command accessed ADC or opened extra devices.");
        Assert(device.Writes[0].SequenceEqual(new byte[] {2,0}) && device.Writes[1].SequenceEqual(new byte[] {6,0}),
            "DAC target mapping changed.");
    }
    Assert(device.Disposed, "DAC not released.");
});
await Check("Razr II DAC-only commands retain cancellation and I2C write errors", async () =>
{
    var device = new RecordingDac { FailWrite = true };
    using var hardware = new RazrHardware(new() { Model = RazrModel.RazrII, SimulateMotor = false, CalibrationConfirmed = true }, _ => device);
    await Throws<OperationCanceledException>(() => hardware.MoveCameraAsync(45, new CancellationToken(true)));
    Assert(device.Writes.Count == 0, "Cancelled command wrote DAC.");
    await Throws<IOException>(() => hardware.MoveCameraAsync(45, CancellationToken.None));
});
await Check("Razr II saves completed button after DAC send and configured wait only", async () =>
{
    var store = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrII, SimulateMotor = false,
        CalibrationConfirmed = true, SettleSeconds = 1 } };
    var device = new RecordingDac();
    await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), razrStore: store,
        razrHardwareFactory: c => new RazrHardware(c, _ => device));
    vm.EnterUserModeCommand.Execute(null);
    vm.SelectCameraPositionCommand.Execute(vm.CameraPositions[1]);
    var command = vm.StartBackgroundCommand.ExecuteAsync(null);
    Assert(device.Writes.Count == 1 && store.Value.LastCameraPosition is { Completed: false } && vm.IsMoving,
        "Command did not respect configured wait.");
    await command;
    Assert(store.Value.LastCameraPosition is { Id: 102, Completed: true } && vm.CameraPositions[1].IsSelected
        && !vm.CanStart && vm.Status.Contains("Command sent") && device.Reads == 0,
        "DAC-only completion did not retain state or claimed measured stability.");
});

await Check("User Settings hidden and Razr switch requires a fresh correct PIN each time", async () =>
{
    await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(),
        razrStore: new MemoryRazrStore(), razrHardwareFactory: _ => new RecordingRazrHardware());
    vm.EnterUserModeCommand.Execute(null);
    vm.OpenSettingsCommand.Execute(null); vm.OpenCameraSettingsCommand.Execute(null);
    Assert(!vm.ShowCurtainSettingsButton && !vm.IsSettingsOpen, "User Settings was exposed.");
    vm.EditSwitchPinCommand.Execute(null);
    Assert(!vm.IsPinDialogOpen, "User could edit PIN.");
    vm.OpenRazrSettingsCommand.Execute(null);
    Assert(vm.IsPinDialogOpen && !vm.IsModelSelectionOpen && !vm.CanStart && !vm.CanSelectScene, "Missing modal gate.");
    vm.EnterRazrIICommand.Execute(null);
    Assert(!vm.IsRazrII, "Model command bypassed PIN.");
    TypePin(vm, "9999"); vm.ConfirmPinCommand.Execute(null);
    Assert(vm.IsPinDialogOpen && vm.PinNotice.Contains("Incorrect") && !vm.IsModelSelectionOpen, "Wrong PIN accepted.");
    vm.CancelPinCommand.Execute(null);
    Assert(!vm.IsPinDialogOpen && vm.IsUserScreenVisible, "Cancel navigated away.");
    vm.OpenRazrSettingsCommand.Execute(null); TypePin(vm, "1234"); vm.ConfirmPinCommand.Execute(null);
    Assert(vm.IsModelSelectionOpen && !vm.IsPinDialogOpen, "Correct PIN blocked.");
    vm.EnterRazrIICommand.Execute(null);
    Assert(vm.IsRazrII && vm.Mode == ApplicationMode.User && !vm.ShowCurtainSettingsButton, "Model switch changed operator mode.");
    vm.OpenRazrSettingsCommand.Execute(null);
    Assert(vm.IsPinDialogOpen && !vm.IsModelSelectionOpen, "Previous authorization was reused.");
    vm.CancelPinCommand.Execute(null);
    vm.IsModelSelectionOpen = true; vm.EnterRazrICommand.Execute(null);
    Assert(vm.IsRazrII, "Forged chooser bypassed authorization.");
    vm.CancelRazrSettingsCommand.Execute(null);
});
await Check("Developer PIN confirmation, keypad bounds, cancel and disk relaunch persist safely", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "switch-pin-" + Guid.NewGuid());
    var path = Path.Combine(folder,"switch-pin.json");
    try
    {
        var store = new SwitchPinStore(path);
        Assert(store.Verify("1234"), "Default PIN unavailable.");
        await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), switchPinStore: store);
        vm.EnterDeveloperModeCommand.Execute(null); vm.EditSwitchPinCommand.Execute(null);
        TypePin(vm,"876543210");
        Assert(vm.MaskedPin.Length == 8, "Keypad exceeded eight digits.");
        vm.PinBackspaceCommand.Execute(null); vm.PinDigitCommand.Execute("x"); vm.PinDigitCommand.Execute(null);
        Assert(vm.MaskedPin.Length == 7, "Delete or digit filter failed.");
        vm.CancelPinCommand.Execute(null);
        Assert(!File.Exists(path) && store.Verify("1234"), "Cancel saved PIN.");
        vm.EditSwitchPinCommand.Execute(null); TypePin(vm,"0278"); vm.ConfirmPinCommand.Execute(null);
        Assert(vm.PinTitle.Contains("Confirm") && vm.IsPinDialogOpen, "New PIN saved before confirmation.");
        TypePin(vm,"9999"); vm.ConfirmPinCommand.Execute(null);
        Assert(vm.PinNotice.Contains("match") && !File.Exists(path), "Mismatch saved PIN.");
        TypePin(vm,"0278"); vm.ConfirmPinCommand.Execute(null);
        Assert(!vm.IsPinDialogOpen && File.Exists(path), "PIN not saved.");
        var reloaded = new SwitchPinStore(path);
        Assert(reloaded.Verify("0278") && !reloaded.Verify("1234") && !File.ReadAllText(path).Contains("0278"), "PIN persistence/hash failed.");
        await using var reopened = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), switchPinStore: reloaded);
        reopened.EnterUserModeCommand.Execute(null); reopened.OpenRazrSettingsCommand.Execute(null);
        TypePin(reopened,"0278"); reopened.ConfirmPinCommand.Execute(null);
        Assert(reopened.IsModelSelectionOpen, "Saved PIN lost after relaunch.");
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder,true); }
});
await Check("Invalid or unreadable PIN configuration fails closed and save errors stay visible", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(),"pin-errors-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder,"switch-pin.json"); File.WriteAllText(path,"broken JSON");
    try
    {
        await using var vm = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), switchPinStore: new SwitchPinStore(path));
        vm.EnterUserModeCommand.Execute(null); vm.OpenRazrSettingsCommand.Execute(null); TypePin(vm,"1234"); vm.ConfirmPinCommand.Execute(null);
        Assert(vm.IsPinDialogOpen && !vm.IsModelSelectionOpen && vm.PinNotice.Contains("configuration"), "Corruption fell back to default PIN.");
        vm.CancelPinCommand.Execute(null);
        await using var writer = new MainViewModel(new(), cameraCatalog: new FakeCameraCatalog(), switchPinStore: new SwitchPinStore(folder));
        writer.EnterDeveloperModeCommand.Execute(null); writer.EditSwitchPinCommand.Execute(null);
        TypePin(writer,"2468"); writer.ConfirmPinCommand.Execute(null); TypePin(writer,"2468"); writer.ConfirmPinCommand.Execute(null);
        Assert(writer.IsPinDialogOpen && writer.PinNotice.Contains("configuration"), "Save failure reported success.");
    }
    finally { Directory.Delete(folder,true); }
});

await Check("shutdown releases relays despite stuck camera discovery and cancellation callback failure", async () =>
{
    foreach (var throwOnCancel in new[] { false, true })
    {
        var output = new RecordingOutput();
        var catalog = new HangingCameraCatalog(throwOnCancel);
        var controller = Create(output);
        var vm = new MainViewModel(new() { ActivationMilliseconds = 2000 }, controller,
            cameraCatalog: catalog, paletteStore: new MemoryPaletteStore());
        vm.EnterUserModeCommand.Execute(null); vm.StartCamera();
        await catalog.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await vm.SelectCurtainCommand.ExecuteAsync(vm.Curtains[0]);
        var movement = vm.StartBackgroundCommand.ExecuteAsync(null);
        await output.WaitForOn(1);
        var timer = Stopwatch.StartNew();
        await vm.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert(timer.Elapsed < TimeSpan.FromSeconds(5) && output.AllOff && output.Disposed && movement.IsCompleted,
            "A stuck camera prevented relay release or kept shutdown waiting.");
        Assert(vm.ActivityLog.Any(line => line.Contains("Camera cleanup timed out")), "Missing timeout diagnostic.");
        catalog.Completion.TrySetResult([]);
        if (vm.RefreshCamerasCommand.ExecutionTask is { } pending) await pending.WaitAsync(TimeSpan.FromSeconds(1));
        await vm.DisposeAsync();
    }
});
await UpdateChecks.Run(Check, Assert);
Console.WriteLine($"Passed {checks} relay/background/UI/camera checks.");

void TypePin(MainViewModel vm, string pin)
{
    foreach (var digit in pin) vm.PinDigitCommand.Execute(digit.ToString());
}

CurtainRelayController Create(IRelayOutput output) => new(output, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(5));

async Task Check(string name, Func<Task> action)
{
    await action();
    checks++;
    Console.WriteLine($"PASS {name}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

sealed class RecordingOutput : IRelayOutput
{
    private readonly object _sync = new();
    private readonly bool[] _states = new bool[8];
    private readonly List<(int Channel, bool On, long Timestamp)> _events = new();
    private readonly TaskCompletionSource[] _on = Enumerable.Range(0, 8)
        .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
    public int? FailChannel { get; set; }
    public bool FailOnlyWhenOn { get; set; }
    public int? ReadFailChannel { get; set; }
    public bool? Read(int channel)
    {
        if (ReadFailChannel == channel) throw new IOException("Injected read failure.");
        return IsOn(channel);
    }
    public bool Disposed { get; private set; }
    public List<(int Channel, bool On, long Timestamp)> Events { get { lock (_sync) return new(_events); } }
    public bool AllOff { get { lock (_sync) return _states.All(s => !s); } }
    public bool IsOn(int channel) { lock (_sync) return _states[channel - 1]; }
    public Task WaitForOn(int channel) => _on[channel - 1].Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void Write(int channel, bool energized)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            _events.Add((channel, energized, Stopwatch.GetTimestamp()));
            if (channel == FailChannel && (!FailOnlyWhenOn || energized))
                throw new IOException("Injected output failure.");
            if (energized && _states[(channel - 1) ^ 1])
                throw new Exception("Both directions were energized together.");
            _states[channel - 1] = energized;
            if (energized) _on[channel - 1].TrySetResult();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            Array.Clear(_states);
            Disposed = true;
        }
    }
}

sealed class MemoryOrderStore : ICurtainOrderStore
{
    public int[]? Value { get; private set; }
    public bool FailSave { get; init; }
    public int[]? Load() => Value is null ? null : (int[])Value.Clone();
    public void Save(int[] order)
    {
        if (FailSave) throw new IOException("Injected persistence failure.");
        Value = (int[])order.Clone();
    }
}

sealed class FakeCameraCatalog : ICameraCatalog
{
    public IReadOnlyList<CameraDevice> Devices { get; set; } =
        [new("camera-a", "USB camera A"), new("camera-b", "USB camera B")];
    public bool Fail { get; set; }
    public Task<IReadOnlyList<CameraDevice>> ListAsync(CancellationToken cancellationToken)
        => Fail ? throw new IOException("Injected discovery failure") : Task.FromResult(Devices);
}

sealed class FakeCameraRig
{
    private readonly object _sync = new();
    private readonly Dictionary<string, TaskCompletionSource> _started = new();
    public int Active { get; private set; }
    public int MaximumActive { get; private set; }
    public ICameraPreview Create(string device) => new Capture(this, device);
    public Task WaitForStart(string device)
    {
        lock (_sync)
        {
            if (!_started.TryGetValue(device, out var signal))
                _started[device] = signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
    private sealed class Capture(FakeCameraRig rig, string device) : ICameraPreview
    {
        public async Task RunAsync(Func<byte[], Task> onFrame, CancellationToken cancellationToken)
        {
            lock (rig._sync)
            {
                rig.Active++;
                rig.MaximumActive = Math.Max(rig.MaximumActive, rig.Active);
                if (!rig._started.TryGetValue(device, out var signal))
                    rig._started[device] = signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
                signal.TrySetResult();
            }
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            finally { lock (rig._sync) rig.Active--; }
        }
    }
}

sealed class ChunkedJpegStream(byte[] bytes) : MemoryStream(bytes)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => base.ReadAsync(buffer[..Math.Min(buffer.Length, 3)], cancellationToken);
}

sealed class CameraHttpHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri?.AbsolutePath != "/video_feed")
            throw new InvalidOperationException("Preview must only GET the video endpoint.");
        return Task.FromResult(response());
    }
}

sealed class DelegateCamera(Func<Func<byte[], Task>, CancellationToken, Task> run) : ICameraPreview
{
    public Task RunAsync(Func<byte[], Task> onFrame, CancellationToken token) => run(onFrame, token);
}

sealed class DelegateHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => respond(request);
}

sealed class MemoryPaletteStore : ICurtainPaletteStore
{
    public CurtainPalette Value { get; private set; } = new()
    {
        Colors = new CurtainPalette().Colors.Append(new CurtainColorPreset("cyan", "Cyan", "#00FFFF")).ToArray()
    };
    public bool FailSave { get; set; }
    public CurtainPalette Load() => Value;
    public void Save(CurtainPalette palette)
    {
        if (FailSave) throw new IOException("Injected palette save failure");
        Value = palette;
    }
}

sealed class FailingCommandStateStore : ICurtainCommandStateStore
{
    public string[]? Load() => null;
    public void Save(string[] states) => throw new IOException("Injected state write failure");
}

sealed class MemoryRazrStore : IRazrConfigurationStore
{
    public RazrConfiguration Value { get; set; } = new();
    public int Saves { get; private set; }
    public bool FailSave { get; set; }
    public RazrConfiguration Load() => Value.Copy();
    public void Save(RazrConfiguration value)
    {
        if (FailSave) throw new IOException("Injected save failure");
        Value = value.Copy(); Saves++;
    }
}
sealed class RecordingRazrHardware : IRazrHardware
{
    public List<double> Angles { get; } = new();
    public bool FailMove { get; set; }
    public bool BlockMove { get; set; }
    public bool Disposed { get; private set; }
    public async Task MoveCameraAsync(double angle, CancellationToken token)
    {
        Angles.Add(angle);
        if (FailMove) throw new IOException("Injected motor failure");
        if (BlockMove) await Task.Delay(Timeout.Infinite, token);
    }
    public void Dispose() { Disposed = true; }
}

sealed class RecordingDac : System.Device.I2c.I2cDevice
{
    public override System.Device.I2c.I2cConnectionSettings ConnectionSettings { get; } = new(1,0x60);
    public List<byte[]> Writes { get; } = new();
    public int Reads { get; private set; }
    public bool FailWrite { get; set; }
    public bool Disposed { get; private set; }
    public override void Read(Span<byte> buffer) { Reads++; throw new Exception("ADC reads are forbidden."); }
    public override void WriteRead(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer) { Reads++; throw new Exception("ADC reads are forbidden."); }
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (FailWrite) throw new IOException("Injected DAC write failure.");
        Writes.Add(buffer.ToArray());
    }
    protected override void Dispose(bool disposing) { Disposed = true; }
}


sealed class HangingCameraCatalog(bool throwOnCancel) : ICameraCatalog
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<IReadOnlyList<CameraDevice>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<IReadOnlyList<CameraDevice>> ListAsync(CancellationToken token)
    {
        if (throwOnCancel) token.Register(() => throw new IOException("Injected cancellation callback failure"));
        Started.TrySetResult();
        return Completion.Task;
    }
}

sealed class HangingCameraPreview : ICameraPreview
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task RunAsync(Func<byte[],Task> frame, CancellationToken token)
    {
        Started.TrySetResult();
        return Completion.Task;
    }
}
