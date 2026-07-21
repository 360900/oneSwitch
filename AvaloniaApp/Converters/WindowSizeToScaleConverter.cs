using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace AvaloniaApp.Converters;

// Drives whole-UI proportional scaling (fonts, icons, cards, spacing, everything)
// as the window is resized, by converting the window's client size into a uniform
// scale factor relative to the app's baseline design size (1000x700).
public class WindowSizeToScaleConverter : IValueConverter
{
    public static readonly WindowSizeToScaleConverter Instance = new();

    private const double BaselineWidth = 1000;
    private const double BaselineHeight = 700;
    private const double MinScale = 0.85;
    private const double MaxScale = 2.0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Size size && size.Width > 0 && size.Height > 0)
        {
            double scale = Math.Min(size.Width / BaselineWidth, size.Height / BaselineHeight);
            return Math.Clamp(scale, MinScale, MaxScale);
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
