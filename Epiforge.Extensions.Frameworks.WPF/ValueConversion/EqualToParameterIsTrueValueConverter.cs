namespace Epiforge.Extensions.Frameworks.WPF.ValueConversion;

/// <summary>
/// Compares the value to the parameter using <see cref="object.Equals(object?, object?)"/>
/// </summary>
[ValueConversion(typeof(object), typeof(bool))]
public sealed class EqualToParameterIsTrueValueConverter :
    IValueConverter
{
    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value, parameter);

    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool boolean && boolean ? parameter : Binding.DoNothing;

    /// <summary>
    /// Gets a shared instance of <see cref="EqualToParameterIsTrueValueConverter"/>
    /// </summary>
    public static EqualToParameterIsTrueValueConverter Default { get; } = new EqualToParameterIsTrueValueConverter();
}
