
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
 
// Labels the theme button with the mode it will switch TO, not the current one.
public class ThemeIconConverter : IValueConverter
{
    public static readonly ThemeIconConverter Instance = new();
 
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isDark = value is bool b && b;
        // Text rather than symbols, since the moon and sun glyphs fall back
        // to an empty box in the default font on macOS.
        return isDark ? "Light" : "Dark";
    }
 
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}