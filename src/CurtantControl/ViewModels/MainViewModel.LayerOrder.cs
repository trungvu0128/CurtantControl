using System;
using System.Linq;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private CurtainCommandState ReadCurrentCurtainState(int id) => Curtains[id - 1].CommandStatus switch
    {
        "Opened" => CurtainCommandState.Opened,
        "Closed" => CurtainCommandState.Closed,
        _ => CurtainCommandState.Unknown
    };

    private void ReconcileLayerOrderSelection()
    {
        CancelUserDraft();
        foreach (var option in BackgroundOptions) option.IsSelected = false;
        _selectionRestored = false;
        var savedForeground = false;
        foreach (var curtain in DisplayCurtains)
        {
            if (curtain.CommandStatus.StartsWith("Closed", StringComparison.Ordinal))
            {
                savedForeground |= curtain.CommandStatus.EndsWith(" (saved)", StringComparison.Ordinal);
                continue;
            }
            if (curtain.CommandStatus.StartsWith("Opened", StringComparison.Ordinal))
            {
                curtain.IsSelected = true;
                _selectionRestored = savedForeground || curtain.CommandStatus.EndsWith(" (saved)", StringComparison.Ordinal);
                SelectionLabel = $"{curtain.Name} selected";
            }
            else SelectionLabel = "Background unknown · layer order changed";
            OnPropertyChanged(nameof(SelectionColor));
            NotifyUserActions();
            return;
        }
        FixedWhiteBackground.IsSelected = true;
        _selectionRestored = Curtains.Any(c => c.CommandStatus.EndsWith(" (saved)", StringComparison.Ordinal));
        SelectionLabel = $"{FixedWhiteBackground.Name} selected";
        OnPropertyChanged(nameof(SelectionColor));
        NotifyUserActions();
    }
}
