using System.Globalization;
using System.Windows.Data;
using MacDock.Interop;

namespace MacDock.App.Converters;

/// <summary>Binds a pin's target string to its shell icon.</summary>
public sealed class TargetToIconConverter(IIconProvider icons) : IValueConverter
{
    /// <summary>Requested bitmap size; large enough for a fully magnified icon at 200% display scaling.</summary>
    private const int IconPixels = 256;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string target ? icons.Get(target, IconPixels) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
