using Microsoft.UI.Xaml.Data;

namespace Gauge.Converters;

/// <summary>
/// Maps null/empty/whitespace strings to <c>null</c> and passes any other string
/// through unchanged. Bound to <c>ToolTipService.ToolTip</c> it suppresses the empty
/// tooltip popover that WinUI shows when the source text is blank.
/// </summary>
public sealed class EmptyStringToNullConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? s : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
