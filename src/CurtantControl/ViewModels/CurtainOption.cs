using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace CurtantControl.ViewModels;

public partial class CurtainOption(int id, string name, string color, string? previewImage = null) : ObservableObject
{
    public string? PreviewImage { get; set; } = previewImage;
    public int Id { get; } = id;
    public int DisplayLevel { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = name;
    [ObservableProperty] public partial IBrush Color { get; set; } = Brush.Parse(color);
    public IBrush UserTextColor => Color is ISolidColorBrush brush
        && (brush.Color.R * 299 + brush.Color.G * 587 + brush.Color.B * 114) > 205000
        ? Brushes.Black : Brushes.White;
    partial void OnColorChanged(IBrush value) => OnPropertyChanged(nameof(UserTextColor));
    public ICommand? SelectCommand { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial string CommandStatus { get; set; } = "Unknown";
}
