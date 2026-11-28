using System.Diagnostics;
using CurtantControl.Services;
using CurtantControl.Services.Relays;
using CurtantControl.ViewModels;

internal static class Razr3Checks
{
    public static async Task Run(Func<string, Func<Task>, Task> check, Action<bool, string> assert)
    {
        await check("RAZR 3 stages choices, waits before relay pulses and reuses the Room 1 direction", async () =>
        {
            var output = new RecordingOutput();
            var motor = new RecordingRazrHardware();
            await using var vm = Create(output, motor, new() { SettleSeconds = 0.08 });
            vm.EnterUserModeCommand.Execute(null);
            assert(vm.IsRazr3ScreenVisible && !vm.IsRazrIScreenVisible && !vm.IsRazrIIScreenVisible && !vm.CanStart, "Wrong initial screen or Start state.");
            assert(vm.Razr3Backgrounds.Select(scene => scene.Name).SequenceEqual(["White", "Sky Blue", "Green", "Purple", "Gray"]), "Unexpected default palette.");
            vm.SelectRazr3SceneCommand.Execute(vm.Razr3Backgrounds[2]);
            vm.SelectRazr3SceneCommand.Execute(vm.Razr3Outside);
            var baseline = output.Events.Count;
            assert(vm.Razr3Outside.IsSelected && vm.Razr3Backgrounds.All(scene => !scene.IsSelected) && motor.Angles.Count == 0, "Staging changed hardware or left two selections.");
            await vm.StartBackgroundCommand.ExecuteAsync(null);
            assert(motor.Angles.SequenceEqual([70d]) && output.Events.Count == baseline && vm.Status == "Ready" && !vm.CanStart, "Outside touched relays or missed Ready.");
            vm.SelectRazr3SceneCommand.Execute(vm.Razr3Backgrounds[2]);
            var start = Stopwatch.GetTimestamp();
            var run = vm.StartBackgroundCommand.ExecuteAsync(null);
            assert(vm.IsMoving && !vm.CanStart, "The complete sequence must own the busy state.");
            vm.SelectRazr3SceneCommand.Execute(vm.Razr3Outside);
            vm.OpenRazrSettingsCommand.Execute(null);
            vm.OpenSettingsCommand.Execute(null);
            await vm.StartBackgroundCommand.ExecuteAsync(null);
            await run;
            var firstPulse = output.Events.Skip(baseline).First(e => e.On);
            assert(firstPulse.Channel == 3 && Stopwatch.GetElapsedTime(start, firstPulse.Timestamp) >= TimeSpan.FromMilliseconds(70), "Green pulsed before settling or used the wrong relay.");
            assert(motor.Angles.SequenceEqual([70d, 180d]) && vm.Razr3Backgrounds[2].IsSelected && !vm.IsSettingsOpen, "A repeated Start or mid-run navigation changed the sequence.");
            await Apply(vm, 3);
            await Apply(vm, 0);
            assert(motor.Angles.Count == 2 && output.Events.Where(e => e.On).Select(e => e.Channel).SequenceEqual([3, 4, 5, 6]), "Room 1 color change resent the motor or changed relay ordering.");
            assert(vm.Status == "Ready" && !vm.CanStart && output.AllOff, "White failed to complete.");
            await Apply(vm, 5);
            await Apply(vm, 2);
            assert(motor.Angles.SequenceEqual([70d, 180d, 70d, 180d]), "Returning from Outside did not establish Room 1 again.");
        });

        await check("RAZR 3 uses four physical slot IDs and honors both curtain layer orders", async () =>
        {
            foreach (var reverse in new[] { false, true })
            {
                var output = new RecordingOutput();
                var motor = new RecordingRazrHardware();
                int[] order = reverse ? [4, 3, 2, 1] : [1, 2, 3, 4];
                await using var vm = Create(output, motor, new() { SettleSeconds = 0 }, order: order);
                vm.EnterDeveloperModeCommand.Execute(null);
                foreach (var id in order) await Apply(vm, id);
                await Apply(vm, 0);
                var expected = reverse
                    ? order.SelectMany((id, index) => order.Take(index).Select(foreground => foreground * 2).Append(id * 2 - 1))
                        .Concat([2, 4, 6, 8])
                    : order.SelectMany(id => new[] { id * 2 - 1, id * 2 });
                assert(output.Events.Where(e => e.On).Select(e => e.Channel).SequenceEqual(expected) && motor.Angles.SequenceEqual([180d]), "Wrong open/close IDs or order across the five backgrounds.");
            }
        });

        await check("RAZR 3 inherits the RAZR I paper sequences by backdrop slot after settling", async () =>
        {
            var output = new RecordingOutput();
            var motor = new RecordingRazrHardware();
            var model = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrI } };
            var palette = new MemoryPaletteStore();
            await using var vm = Create(output, motor, new() { SettleSeconds = 0.08 }, order: [4, 3, 2, 1], model: model, palette: palette);
            vm.EnterDeveloperModeCommand.Execute(null);
            vm.OpenSettingsCommand.Execute(null);
            vm.CurtainSettings.SelectedRelayOrder = vm.CurtainSettings.RelayOrders[0];
            vm.CurtainSettings.SaveCommand.Execute(null);
            vm.CurtainSettings.CancelCommand.Execute(null);
            vm.OpenRazrSettingsCommand.Execute(null);
            vm.EnterRazr3Command.Execute(null);

            assert(vm.Razr3Backgrounds.Select(scene => scene.Name).SequenceEqual(["White", "Sky Blue", "Green", "Purple", "Gray"])
                && vm.Razr3RelayPairs.SequenceEqual(["Curtain 4 · R7/R8", "Curtain 3 · R5/R6", "Curtain 2 · R3/R4", "Curtain 1 · R1/R2"]),
                "RAZR 3 changed the color positions or displayed the old relay assignments.");
            await Apply(vm, 5);
            assert(!output.Events.Any(e => e.On) && motor.Angles.SequenceEqual([70d]), "Outside pulsed the curtains under the paper preset.");

            async Task VerifyPulses(int slot, params int[] expected)
            {
                var before = output.Events.Count;
                await Apply(vm, slot);
                var pulses = output.Events.Skip(before).Where(e => e.On).Select(e => e.Channel);
                assert(pulses.SequenceEqual(expected) && output.AllOff && vm.Status == "Ready" && !vm.CanStart,
                    $"RAZR 3 sent a different sequence for {vm.Razr3Backgrounds[slot].Name} or did not finish cleanly.");
            }

            var start = Stopwatch.GetTimestamp();
            await VerifyPulses(1, 2, 4, 6, 7);
            assert(Stopwatch.GetElapsedTime(start, output.Events.First(e => e.On).Timestamp) >= TimeSpan.FromMilliseconds(70),
                "The first paper pulse ran before Room 1 settled.");
            assert(vm.ActivityLog.Any(item => item.Contains("RAZR 3 · Sky Blue · sending R2 pulse", StringComparison.Ordinal)),
                "The paper pulse log used the wrong backdrop name.");
            await VerifyPulses(2, 2, 4, 5);
            await VerifyPulses(3, 2, 3);
            await VerifyPulses(4, 1);
            await VerifyPulses(0, 2, 4, 6, 8);
            await VerifyPulses(0);
            assert(motor.Angles.SequenceEqual([70d, 180d]), "Indoor paper sequences resent the settled camera angle.");
        });

        await check("RAZR 3 guards the curtain phase against motor failure, cancellation and shutdown", async () =>
        {
            foreach (var failure in new[] { "dac", "stop", "shutdown" })
            {
                var output = new RecordingOutput();
                var motor = new RecordingRazrHardware { FailMove = failure == "dac" };
                await using var vm = Create(output, motor, new() { SettleSeconds = 10 });
                vm.EnterDeveloperModeCommand.Execute(null);
                vm.SelectRazr3SceneCommand.Execute(vm.Razr3Backgrounds[2]);
                var baseline = output.Events.Count;
                var run = vm.StartBackgroundCommand.ExecuteAsync(null);
                if (failure != "dac") assert(vm.Status.Contains("settle", StringComparison.Ordinal), "Expected to cancel during the wait.");
                if (failure == "stop") vm.StopCommand.Execute(null);
                if (failure == "shutdown") await vm.DisposeAsync();
                await run.WaitAsync(TimeSpan.FromSeconds(2));
                assert(!output.Events.Any(e => e.On) && vm.Status != "Ready", "An unsuccessful motor sequence pulsed a relay or reported Ready.");
                if (failure == "shutdown") assert(motor.Disposed && output.Disposed, "Shutdown leaked output ownership.");
                else assert(vm.CanStart && output.Events.Count == baseline, "Failure touched relays or lost retry selection.");
            }
        });

        await check("RAZR 3 preserves the selected scene after a relay failure", async () =>
        {
            var output = new RecordingOutput { FailChannel = 3, FailOnlyWhenOn = true };
            var motor = new RecordingRazrHardware();
            await using var vm = Create(output, motor, new() { SettleSeconds = 0 });
            vm.EnterDeveloperModeCommand.Execute(null);
            await Apply(vm, 2);
            assert(vm.Status.Contains("failed", StringComparison.Ordinal) && vm.Curtains[1].CommandStatus == "Unknown", "An affected curtain stayed known or its error was lost.");
            assert(vm.CanStart && vm.Razr3Backgrounds[2].IsSelected && output.AllOff, "Failure lost the retry selection or left a relay on.");
            await Apply(vm, 2);
            assert(motor.Angles.Count == 1 && vm.CanStart && vm.Status != "Ready", "Retry resent a settled motor command or ignored the relay fault.");
        });

        await check("RAZR 3 settings save independently and restore selection without movement", async () =>
        {
            var output = new RecordingOutput();
            var motor = new RecordingRazrHardware();
            var model = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrI } };
            var profile = new MemoryRazr3Store { Value = new() { SettleSeconds = 0 } };
            await using (var vm = Create(output, motor, profile.Value, model: model, profile: profile))
            {
                vm.EnterUserModeCommand.Execute(null);
                vm.OpenRazrSettingsCommand.Execute(null);
                vm.EnterRazr3Command.Execute(null);
                assert(vm.IsPinDialogOpen && vm.IsRazrI, "RAZR 3 bypassed the User PIN.");
                vm.CancelPinCommand.Execute(null);
                vm.EnterDeveloperModeCommand.Execute(null);
                vm.OpenRazrSettingsCommand.Execute(null);
                vm.EnterRazr3Command.Execute(null);
                var razrIName = vm.BackgroundOptions[2].Name;
                vm.OpenSettingsCommand.Execute(null);
                assert(vm.Razr3Draft.OutsideAngle == 70 && vm.Razr3Draft.RoomAngle == 180, "A legacy blank profile did not populate both documented defaults.");
                vm.Razr3Draft.RoomAngle = 165;
                vm.Razr3Draft.M2Color = "pink";
                profile.FailSave = true;
                vm.SaveRazr3SettingsCommand.Execute(null);
                assert(profile.Value.M2Color == "green" && vm.Razr3Backgrounds[2].Name == "Green", "Failed save changed active settings.");
                profile.FailSave = false;
                vm.SaveRazr3SettingsCommand.Execute(null);
                assert(vm.Razr3Backgrounds[2].Name == "Pink" && vm.BackgroundOptions[2].Name == razrIName, "RAZR 3 color save also changed RAZR I.");
                vm.Razr3Draft.M2Color = "green";
                vm.CancelRazrSettingsCommand.Execute(null);
                assert(vm.Razr3Backgrounds[2].Name == "Pink" && motor.Angles.Count == 0 && output.Events.All(e => !e.On), "Cancel applied draft edits or Save moved the booth.");
                await Apply(vm, 2);
            }
            var nextMotor = new RecordingRazrHardware();
            await using var restarted = Create(new RecordingOutput(), nextMotor, profile.Value, model: model, profile: profile);
            restarted.EnterUserModeCommand.Execute(null);
            assert(restarted.IsRazr3 && restarted.Razr3Backgrounds[2].IsSelected && restarted.CanStart && nextMotor.Angles.Count == 0, "Restart lost the staged scene or started movement.");
            await restarted.StartBackgroundCommand.ExecuteAsync(null);
            assert(nextMotor.Angles.SequenceEqual([165d]), "Restart skipped the first camera targeting step or replaced the saved angle.");
        });

        await check("RAZR 3 fills legacy simulation angles within the saved limits and requires explicit hardware angles", () =>
        {
            var partial = new Razr3Configuration { OutsideAngle = 80, M2Color = "pink", CalibrationConfirmed = true }.WithSimulationAngleDefaults();
            assert(partial.OutsideAngle == 80 && partial.RoomAngle == 180 && partial.M2Color == "pink", "Defaulting lost a saved setting.");
            assert(!partial.CalibrationConfirmed, "A new default must require hardware confirmation.");
            var limited = new Razr3Configuration { MaximumAngle = 150 }.WithSimulationAngleDefaults();
            assert(limited.OutsideAngle == 45 && limited.RoomAngle == 135, "Defaulting ignored saved travel limits.");
            var hardware = new Razr3Configuration { Simulate = false }.WithSimulationAngleDefaults();
            assert(hardware.OutsideAngle is null && hardware.RoomAngle is null, "Defaults must be scoped to simulation profiles.");
            return Task.CompletedTask;
        });

        await check("RAZR 3 real motor invalidates RAZR II history before writing and simulation isolates GPIO", async () =>
        {
            var installation = new PhotoboothSettings { SimulateRelays = false, ActiveLow = false, BcmPinsByChannel = [4, 5, 6, 7, 8, 9, 10, 11] };
            var profile = new MemoryRazr3Store { Value = new() { SettleSeconds = 0 } };
            var model = new MemoryRazrStore { Value = new() { Model = RazrModel.RazrIII, SimulateMotor = false,
                CalibrationConfirmed = true, LastCameraPosition = new(101, 45, 360, true) } };
            var output = new RecordingOutput();
            var motors = new List<RecordingRazrHardware>();
            await using var vm = new MainViewModel(installation, CreateRelays(output), razrStore: model, razr3Store: profile,
                razr3MotorFactory: _ => { var motor = new RecordingRazrHardware(); motors.Add(motor); return motor; });
            vm.EnterDeveloperModeCommand.Execute(null);
            var writes = output.Events.Count;
            await Apply(vm, 2);
            assert(output.Events.Count == writes && vm.Curtains[1].CommandStatus == "Unknown" && model.Value.LastCameraPosition!.Completed,
                "Simulation touched real curtain estimates, GPIO or RAZR II history.");
            vm.OpenSettingsCommand.Execute(null);
            vm.Razr3Draft.Simulate = false;
            vm.SaveRazr3SettingsCommand.Execute(null);
            assert(profile.Value.Simulate && vm.RazrNotice.StartsWith("Could not save", StringComparison.Ordinal), "Hardware mode accepted missing calibration.");
            vm.Razr3Draft.OutsideAngle = 70;
            vm.Razr3Draft.RoomAngle = 160;
            vm.Razr3Draft.SettleSeconds = 0.06;
            vm.Razr3Draft.CalibrationConfirmed = true;
            vm.SaveRazr3SettingsCommand.Execute(null);
            vm.CancelRazrSettingsCommand.Execute(null);
            model.FailSave = true;
            await Apply(vm, 5);
            assert(motors.Count == 1 && vm.CanStart, "DAC was opened without persisting invalidation.");
            model.FailSave = false;
            await Apply(vm, 5);
            assert(motors.Count == 2 && motors[1].Angles.SequenceEqual([70d]) && !model.Value.LastCameraPosition!.Completed && output.Events.Count == writes,
                "Outside touched GPIO, used an uncalibrated angle or left stale RAZR II history.");
        });
    }

    private static MainViewModel Create(RecordingOutput output, RecordingRazrHardware motor, Razr3Configuration configuration,
        int[]? order = null, MemoryRazrStore? model = null, MemoryRazr3Store? profile = null, MemoryPaletteStore? palette = null) => new(
        new() { CurtainOrderFrontToBack = order ?? [1, 2, 3, 4], ActivationMilliseconds = 5 }, CreateRelays(output),
        razrStore: model ?? new() { Value = new() { Model = RazrModel.RazrIII } },
        razr3Store: profile ?? new() { Value = configuration }, razr3MotorFactory: _ => motor, paletteStore: palette);

    private static CurtainRelayController CreateRelays(RecordingOutput output) =>
        new(output, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(3));

    private static async Task Apply(MainViewModel vm, int scene)
    {
        vm.SelectRazr3SceneCommand.Execute(scene == 5 ? vm.Razr3Outside : vm.Razr3Backgrounds[scene]);
        await vm.StartBackgroundCommand.ExecuteAsync(null);
    }

    private sealed class MemoryRazr3Store : IRazr3ConfigurationStore
    {
        public Razr3Configuration Value { get; set; } = new();
        public bool FailSave { get; set; }
        public Razr3Configuration Load() => Value with { };
        public void Save(Razr3Configuration configuration)
        {
            if (FailSave) throw new IOException("Injected RAZR 3 save failure.");
            Value = configuration with { };
        }
    }
}
