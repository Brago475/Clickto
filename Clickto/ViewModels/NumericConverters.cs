using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Clickto.ViewModels;

/// <summary>
/// Two way converter for numeric text boxes. An empty or partial entry, which
/// happens the moment someone clears the field to retype it, would otherwise
/// throw an InvalidCastException and paint the box with an error.
/// </summary>
public class IntTextConverter : IValueConverter
{
    public static readonly IntTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() ?? "0";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;

        // Treat a cleared box as zero rather than an error.
        if (string.IsNullOrWhiteSpace(text)) return 0;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        // Mid-typing garbage: keep the old value instead of throwing.
        return BindingOperations.DoNothing;
    }
}

/// <summary>Same idea for the decimal fields, such as custom speed and coordinates.</summary>
public class DoubleTextConverter : IValueConverter
{
    public static readonly DoubleTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return d.ToString("0.####", CultureInfo.InvariantCulture);
        return value?.ToString() ?? "0";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text)) return 0d;

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return BindingOperations.DoNothing;
    }
}
