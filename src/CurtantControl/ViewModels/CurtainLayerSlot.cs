using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public partial class CurtainLayerSlot : ViewModelBase
{
    private readonly Action<CurtainLayerSlot, CurtainColorPreset, CurtainColorPreset> _changed;
    public int CurtainId { get; }
    [ObservableProperty] public partial string Position { get; set; } = "";
    public CurtainColorPreset[] Colors { get; }
    [ObservableProperty] public partial CurtainColorPreset SelectedColor { get; set; } = null!;

    public CurtainLayerSlot(int curtainId, string position, CurtainColorPreset[] colors, string selectedKey,
        Action<CurtainLayerSlot, CurtainColorPreset, CurtainColorPreset> changed)
    {
        CurtainId = curtainId;
        Position = position;
        Colors = colors;
        _changed = changed;
        SelectedColor = colors.Single(c => c.Key == selectedKey);
    }
    partial void OnSelectedColorChanged(CurtainColorPreset oldValue, CurtainColorPreset newValue)
    {
        if (oldValue is not null && newValue is not null) _changed(this, oldValue, newValue);
    }
}
