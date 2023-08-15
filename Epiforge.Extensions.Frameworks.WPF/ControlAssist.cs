namespace Epiforge.Extensions.Frameworks.WPF;

/// <summary>
/// Provides attached dependency properties to enhance the functionality of controls
/// </summary>
public static class ControlAssist
{
    #region AdditionalInputBindings

    /// <summary>
    /// Identifies the AdditionalInputBindings attached dependency property
    /// </summary>
    public static readonly DependencyProperty AdditionalInputBindingsProperty = DependencyProperty.RegisterAttached("AdditionalInputBindings", typeof(InputBindingCollection), typeof(ControlAssist), new PropertyMetadata(null, OnAdditionalInputBindingsChanged));

    /// <summary>
    /// Gets the value of the AdditionalInputBindings attached dependency property for the specified UI element
    /// </summary>
    /// <param name="uiElement">The UI element for which to get the value</param>
    [AttachedPropertyBrowsableForType(typeof(UIElement))]
    [SuppressMessage("WpfAnalyzers.DependencyProperty", "WPF0042: Avoid side effects in CLR accessors", Justification = "It's going to happen anyway, might as well provide helpful information")]
    public static InputBindingCollection? GetAdditionalInputBindings(UIElement uiElement)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(uiElement);
#else
        if (uiElement is null)
            throw new ArgumentNullException(nameof(uiElement));
#endif
        return (InputBindingCollection)uiElement.GetValue(AdditionalInputBindingsProperty);
    }

    static void OnAdditionalInputBindingsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is UIElement uiElement)
        {
            if (e.OldValue is InputBindingCollection oldValue)
                foreach (var inputBinding in oldValue.OfType<InputBinding>().Where(ib => ib is not null))
                    uiElement.InputBindings.Remove(inputBinding);
            if (e.NewValue is InputBindingCollection newValue)
                uiElement.InputBindings.AddRange(newValue);
        }
    }

    /// <summary>
    /// Sets the value of the AdditionalInputBindings attached dependency property for the specified UI element
    /// </summary>
    /// <param name="uiElement">The UI element for which to set the value</param>
    /// <param name="value">The value to set</param>
    [SuppressMessage("WpfAnalyzers.DependencyProperty", "WPF0042: Avoid side effects in CLR accessors", Justification = "It's going to happen anyway, might as well provide helpful information")]
    public static void SetAdditionalInputBindings(UIElement uiElement, InputBindingCollection? value)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(uiElement);
#else
        if (uiElement is null)
            throw new ArgumentNullException(nameof(uiElement));
#endif
        uiElement.SetValue(AdditionalInputBindingsProperty, value);
    }

    #endregion AdditionalInputBindings
}
