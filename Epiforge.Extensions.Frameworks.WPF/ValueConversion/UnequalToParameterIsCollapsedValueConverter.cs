namespace Epiforge.Extensions.Frameworks.WPF.ValueConversion;

/// <summary>
/// Compares the value to the parameter using <see cref="object.Equals(object?, object?)"/>,
/// converting to <see cref="Visibility.Collapsed"/> when <c>false</c> and <see cref="Visibility.Visible"/> when <c>true</c>
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class UnequalToParameterIsCollapsedValueConverter :
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
        Equals(value, parameter) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    /// <summary>
    /// Gets a shared instance of <see cref="UnequalToParameterIsCollapsedValueConverter"/>
    /// </summary>
    public static UnequalToParameterIsCollapsedValueConverter Default { get; } = new UnequalToParameterIsCollapsedValueConverter();
}
