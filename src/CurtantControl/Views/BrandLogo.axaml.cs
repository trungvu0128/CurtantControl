using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace CurtantControl.Views;

public partial class BrandLogo : UserControl
{
    private static readonly Lazy<Bitmap> DarkLogo = new(() => Load("123click-logo.png"));
    private static readonly Lazy<Bitmap> LightLogo = new(() => Load("123click-logo-light.png"));

    public BrandLogo()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => UpdateLogo();
        AttachedToVisualTree += (_, _) => UpdateLogo();
        UpdateLogo();
    }

    private void UpdateLogo() => LogoImage.Source = ActualThemeVariant == ThemeVariant.Light
        ? LightLogo.Value : DarkLogo.Value;

    private static Bitmap Load(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://CurtantControl/Assets/Brand/{name}"));
        return new Bitmap(stream);
    }
}
