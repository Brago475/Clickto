
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Styling;
 
namespace Clickto.ViewModels;
 
// Converts the IsDark bool into Avalonia's ThemeVariant so the window
// switches its whole theme dictionary (Dark vs Light) when toggled.
public class ThemeConverter : IValueConverter
{
    public static readonly ThemeConverter Instance = new();
 
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = value is bool b && b;
        return isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
 
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
 
// Shows a moon when in dark mode (tap to go light) and a sun when in light mode.
public class ThemeIconConverter : IValueConverter
{
    public static readonly ThemeIconConverter Instance = new();
 
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = value is bool b && b;
        return isDark ? "\u263D" : "\u2600";  // ☽ moon  /  ☀ sun
    }
 
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}