namespace Epiforge.Extensions.Frameworks.WPF.ValueConversion;

/// <summary>
/// Converts the value to <see cref="Visibility.Collapsed"/> when <c>false</c> and <see cref="Visibility.Visible"/> when <c>true</c>
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class FalseIsCollapsedValueConverter :
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
        value is bool boolean ? (boolean ? Visibility.Visible : Visibility.Collapsed) : Binding.DoNothing;

    /// <summary>
    /// Converts a value
    /// </summary>
    /// <param name="value">The value produced by the binding source</param>
    /// <param name="targetType">The type of the binding target property</param>
    /// <param name="parameter">The converter parameter to use</param>
    /// <param name="culture">The culture to use in the converter</param>
    /// <returns>A converted value</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility ? visibility == Visibility.Visible : Binding.DoNothing;

    /// <summary>
    /// Gets a shared instance of <see cref="FalseIsCollapsedValueConverter"/>
    /// </summary>
    public static FalseIsCollapsedValueConverter Default { get; } = new FalseIsCollapsedValueConverter();
}
