using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public sealed record RelayLayerOrderChoice(string Name, int[] FrontToBack);

public partial class CurtainSettingsViewModel(Func<string[], string, int[], string?> save, Action cancel) : ViewModelBase
{
    public ObservableCollection<CurtainLayerSlot> Slots { get; } = new();
    public ObservableCollection<CurtainLayerSlot> DisplaySlots { get; } = new();
    [ObservableProperty] public partial RelayLayerOrderChoice[] RelayOrders { get; set; } = [];
    [ObservableProperty] public partial RelayLayerOrderChoice SelectedRelayOrder { get; set; } = null!;
    partial void OnSelectedRelayOrderChanged(RelayLayerOrderChoice value)
    {
        if (!_reverting) { Notice = ""; Error = ""; RebuildDisplaySlots(); }
    }
    [ObservableProperty] public partial bool IsRelayOrderVisible { get; set; }
    private CurtainLayerSlot? _fixedSlot;
    private bool _reverting;
    [ObservableProperty] public partial CurtainColorPreset[] FixedColors { get; set; } = [];
    [ObservableProperty] public partial CurtainColorPreset FixedColor { get; set; } = null!;
    partial void OnFixedColorChanged(CurtainColorPreset oldValue, CurtainColorPreset newValue)
    {
        if (_reverting || oldValue is null || newValue is null) return;
        Notice = "";
        if (Slots.Any(s => s.SelectedColor.Key == newValue.Key))
        {
            _reverting = true;
            try { FixedColor = oldValue; } finally { _reverting = false; }
            Error = $"{newValue.Name} is already selected. Choose an unused color.";
        }
        else Error = "";
        SyncFixedSlot();
    }
    private void SyncFixedSlot()
    {
        if (_fixedSlot is null) return;
        _reverting = true;
        try { _fixedSlot.SelectedColor = FixedColor; } finally { _reverting = false; }
    }
    private void ChangeFixedSlot(CurtainLayerSlot slot, CurtainColorPreset previous, CurtainColorPreset selected)
    {
        if (_reverting) return;
        FixedColor = selected;
    }
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial string Notice { get; set; } = "";

    public void Begin(CurtainPalette palette, int[] frontToBack, bool developerMode = false)
    {
        _reverting = true;
        IsRelayOrderVisible = developerMode;
        RelayOrders =
        [
            new("R1/R2 nearest → R3/R4 → R5/R6 → R7/R8", [1, 2, 3, 4]),
            new("R7/R8 nearest → R5/R6 → R3/R4 → R1/R2", [4, 3, 2, 1])
        ];
        var existing = RelayOrders.FirstOrDefault(o => o.FrontToBack.SequenceEqual(frontToBack));
        if (existing is null)
        {
            existing = new("Current custom layer order", (int[])frontToBack.Clone());
            RelayOrders = RelayOrders.Append(existing).ToArray();
        }
        SelectedRelayOrder = existing;
        Slots.Clear();
        DisplaySlots.Clear();
        _fixedSlot = null;
        var fixedPreset = palette.GetFixedBackground();
        FixedColors = palette.Colors.Any(c => c.Key == fixedPreset.Key) ? palette.Colors : palette.Colors.Append(fixedPreset).ToArray();
        FixedColor = FixedColors.Single(c => c.Key == fixedPreset.Key);
        for (var index = 0; index < 4; index++)
        {
            var id = frontToBack[index];
            Slots.Add(new(id, $"{id + 1} · Background {id + 1} · M{id}", palette.Colors, palette.Slots[id - 1], ChangeColor));
        }
        _fixedSlot = new(0, "1 · Fixed background", FixedColors, FixedColor.Key, ChangeFixedSlot);
        RebuildDisplaySlots();
        _reverting = false;
        Error = "";
        Notice = "";
    }

    private void RebuildDisplaySlots()
    {
        if (_fixedSlot is null || Slots.Count != 4 || SelectedRelayOrder is null) return;
        DisplaySlots.Clear();
        _fixedSlot.Position = "1 · Fixed background";
        DisplaySlots.Add(_fixedSlot);
        foreach (var slot in Slots.OrderBy(s => s.CurtainId))
        {
            var id = slot.CurtainId;
            slot.Position = $"{id + 1} · Background {id + 1} · M{id} · R{id * 2 - 1}/R{id * 2}";
            DisplaySlots.Add(slot);
        }
    }

    private void ChangeColor(CurtainLayerSlot slot, CurtainColorPreset previous, CurtainColorPreset selected)
    {
        if (_reverting) return;
        Notice = "";
        if (FixedColor.Key == selected.Key || Slots.Any(s => s != slot && s.SelectedColor.Key == selected.Key))
        {
            _reverting = true;
            try { slot.SelectedColor = previous; }
            finally { _reverting = false; }
            Error = $"{selected.Name} is already selected. Choose an unused color.";
            return;
        }
        Error = "";
    }

    [RelayCommand]
    private void Save()
    {
        Error = save(Slots.OrderBy(s => s.CurtainId).Select(s => s.SelectedColor.Key).ToArray(), FixedColor.Key, SelectedRelayOrder.FrontToBack) ?? "";
        Notice = Error.Length == 0 ? "Saved" : "";
    }
    [RelayCommand]
    private void Cancel() => cancel();
}
