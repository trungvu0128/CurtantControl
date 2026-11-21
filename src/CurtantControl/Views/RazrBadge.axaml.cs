using Avalonia;
using Avalonia.Controls;

namespace CurtantControl.Views;

public partial class RazrBadge : UserControl
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<RazrBadge, string>(nameof(Text), "CAMERA RAZR I");
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public RazrBadge() => InitializeComponent();
}
