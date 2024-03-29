namespace Epiforge.Extensions.Frameworks.WPF.Behaviors;

/// <summary>
/// Allows binding to <see cref="PasswordBox.Password"/>
/// </summary>
public sealed class PasswordBindingTarget :
    Behavior<PasswordBox>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordBindingTarget"/> class
    /// </summary>
    public PasswordBindingTarget()
    {
        dependencyPropertyChangeSource = false;
        passwordBoxChangeSource = false;
    }

    bool dependencyPropertyChangeSource;
    bool passwordBoxChangeSource;

    /// <summary>
    /// Gets/sets the password
    /// </summary>
    public string? Password
    {
        get => (string?)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    void PasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!dependencyPropertyChangeSource)
        {
            passwordBoxChangeSource = true;
            SetCurrentValue(PasswordProperty, AssociatedObject.Password);
            passwordBoxChangeSource = false;
        }
    }

    /// <summary>
    /// Called after the behavior is attached to an <see cref="Behavior{PasswordBox}.AssociatedObject"/>
    /// </summary>
    protected override void OnAttached()
    {
        dependencyPropertyChangeSource = true;
        AssociatedObject.Password = (string)GetValue(PasswordProperty);
        dependencyPropertyChangeSource = false;
        AssociatedObject.PasswordChanged += PasswordBoxPasswordChanged;
    }

    /// <summary>
    /// Called when the behavior is being detached from its <see cref="Behavior{PasswordBox}.AssociatedObject"/>, but before it has actually occurred
    /// </summary>
    protected override void OnDetaching()
    {
        AssociatedObject.PasswordChanged -= PasswordBoxPasswordChanged;
        dependencyPropertyChangeSource = true;
        AssociatedObject.Password = null;
        dependencyPropertyChangeSource = false;
    }

    /// <summary>
    /// Identifies the Password dependency property
    /// </summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(string), typeof(PasswordBindingTarget), new PropertyMetadata(null, OnPasswordChanged));

    /// <summary>
    /// Gets the value of the Password dependency property for the specified password binding target
    /// </summary>
    /// <param name="passwordBindingTarget">The password binding target</param>
    [AttachedPropertyBrowsableForType(typeof(PasswordBindingTarget))]
    [SuppressMessage("WpfAnalyzers.DependencyProperty", "WPF0042: Avoid side effects in CLR accessors", Justification = "It's going to happen anyway, might as well provide helpful information")]
    public static string? GetPassword(PasswordBindingTarget passwordBindingTarget)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(passwordBindingTarget);
#else
        if (passwordBindingTarget is null)
            throw new ArgumentNullException(nameof(passwordBindingTarget));
#endif
        return (string?)passwordBindingTarget.GetValue(PasswordProperty);
    }

    static void OnPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is PasswordBindingTarget passwordBindingTarget && passwordBindingTarget.AssociatedObject is { } passwordBox && !passwordBindingTarget.passwordBoxChangeSource)
        {
            passwordBindingTarget.dependencyPropertyChangeSource = true;
            passwordBox.Password = e.NewValue as string;
            passwordBindingTarget.dependencyPropertyChangeSource = false;
        }
    }

    /// <summary>
    /// Sets the value of the Password dependency property for the specified password binding target
    /// </summary>
    /// <param name="passwordBindingTarget">The password binding target</param>
    /// <param name="value">The value to set</param>
    [SuppressMessage("WpfAnalyzers.DependencyProperty", "WPF0042: Avoid side effects in CLR accessors", Justification = "It's going to happen anyway, might as well provide helpful information")]
    public static void SetPassword(PasswordBindingTarget passwordBindingTarget, string? value)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(passwordBindingTarget);
#else
        if (passwordBindingTarget is null)
            throw new ArgumentNullException(nameof(passwordBindingTarget));
#endif
        passwordBindingTarget.SetValue(PasswordProperty, value);
    }
}
