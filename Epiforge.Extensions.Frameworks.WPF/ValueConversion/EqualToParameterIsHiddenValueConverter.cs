namespace Epiforge.Extensions.Frameworks.WPF.ValueConversion;

/// <summary>
/// Compares the value to the parameter using <see cref="object.Equals(object?, object?)"/>,
/// converting to <see cref="Visibility.Hidden"/> when <c>true</c> and <see cref="Visibility.Visible"/> when <c>false</c>
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class EqualToParameterIsHiddenValueConverter :
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
        Equals(value, parameter) ? Visibility.Hidden : Visibility.Visible;

    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility == Visibility.Hidden ? parameter : Binding.DoNothing;

    /// <summary>
    /// Gets a shared instance of <see cref="EqualToParameterIsHiddenValueConverter"/>
    /// </summary>
    public static EqualToParameterIsHiddenValueConverter Default { get; } = new EqualToParameterIsHiddenValueConverter();
}
