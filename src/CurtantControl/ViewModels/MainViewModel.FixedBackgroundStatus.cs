using System;
using System.ComponentModel;
using System.Linq;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private void OnCurtainStatusChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CurtainOption.CommandStatus)) UpdateFixedBackgroundStatus();
    }

    // Derived command estimate, never a separate relay or physical position sensor.
    private void UpdateFixedBackgroundStatus()
    {
        var states = Curtains.Select(c => c.CommandStatus).ToArray();
        var saved = states.Any(s => s.EndsWith(" (saved)", StringComparison.Ordinal));
        var status = states.Any(s => s is "Opening" or "Closing") ? "Changing"
            : states.Length == 4 && states.All(s => s.StartsWith("Closed", StringComparison.Ordinal)) ? "Visible"
            : states.Any(s => s.StartsWith("Opened", StringComparison.Ordinal)) ? "Hidden"
            : "Unknown";
        FixedWhiteBackground.CommandStatus = status + (saved && status is "Visible" or "Hidden" ? " (saved)" : "");
    }
}
