namespace Epiforge.Extensions.Frameworks.WPF.ValueConversion;

/// <summary>
/// Converts the value to a <see cref="string"/> when it is a <see cref="Nullable{Double}"/>, optionally accepting a <see cref="NumberStyles"/> value as a parameter and retaining the results of the last successful call to <see cref="ConvertBack(object?, Type, object, CultureInfo)"/> which prevents two-way bindings from overriding decimals when the user is typing
/// </summary>
[ValueConversion(typeof(double), typeof(string))]
public sealed class StatefulNullableDoubleIsStringValueConverter :
    IValueConverter
{
    (string? str, double? dbl)? lastSuccessfulConvertBack;

    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double dbl)
        {
            if (lastSuccessfulConvertBack is { } last)
            {
                var (lastStr, lastDbl) = last;
                if (lastDbl == dbl)
                    return lastStr;
            }
            return parameter switch
            {
                NumberStyles numberStyles => numberStyles switch
                {
                    NumberStyles ns when (ns & NumberStyles.Currency) > 0 => dbl.ToString("c", culture),
                    NumberStyles ns when (ns & NumberStyles.HexNumber) > 0 => dbl.ToString("x", culture),
                    NumberStyles ns when (ns & NumberStyles.Integer) > 0 => Math.Truncate(dbl).ToString(culture),
                    NumberStyles ns when (ns & NumberStyles.Number) > 0 => dbl.ToString("n", culture),
                    _ => dbl.ToString(culture)
                },
                null => dbl.ToString(culture),
                _ => throw new NotSupportedException()
            };
        }
        if (value is null && lastSuccessfulConvertBack is { } nullLast)
        {
            var (lastStr, lastDbl) = nullLast;
            if (lastDbl is null)
                return lastStr;
        }
        return null;
    }

    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            if (double.TryParse(str, parameter is NumberStyles numberStyles ? numberStyles : NumberStyles.Any, culture, out var dbl))
            {
                lastSuccessfulConvertBack = (str, dbl);
                return dbl;
            }
            lastSuccessfulConvertBack = (str, null);
        }
        return null;
    }

    /// <summary>
    /// Gets a new instance of <see cref="StatefulNullableDoubleIsStringValueConverter"/>
    /// </summary>
    public static StatefulNullableDoubleIsStringValueConverter Default => new();
}
