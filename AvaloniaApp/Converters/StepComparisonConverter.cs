using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaloniaApp.Converters;

// Drives the step-circle stepper: compares MainWindowViewModel.CurrentStep (the
// bound value) against a step number passed as ConverterParameter, so the same
// converter instance can mark a step "completed" or "current" from XAML alone.
public class StepComparisonConverter : IValueConverter
{
    public static readonly StepComparisonConverter GreaterThan = new(Mode.GreaterThan);
    public static readonly StepComparisonConverter EqualTo = new(Mode.EqualTo);

    private enum Mode
    {
        GreaterThan,
        EqualTo,
    }

    private readonly Mode _mode;

    private StepComparisonConverter(Mode mode) => _mode = mode;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int current && parameter is string s && int.TryParse(s, out var step))
            return _mode == Mode.GreaterThan ? current > step : current == step;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
