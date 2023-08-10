namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides a mechanism for notifying about property changes for a <see cref="DynamicObject"/>
/// </summary>
public class DynamicPropertyChangeNotifier :
    DynamicObject,
    INotifyPropertyChanged,
    INotifyPropertyChanging
{
    /// <summary>
    /// Gets/sets the <see cref="ILogger"/> which will be used
    /// </summary>
    protected ILogger? Logger { get; set; }

    /// <summary>
    /// Occurs when a property value changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when a property value is changing
    /// </summary>
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event
    /// </summary>
	/// <param name="e">The arguments of the event</param>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is null</exception>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e is null)
            throw new ArgumentNullException(nameof(e));
        Logger?.LogTrace("Raising PropertyChanged event for {PropertyName} property", e.PropertyName);
        PropertyChanged?.Invoke(this, e);
        Logger?.LogTrace("Raised PropertyChanged event for {PropertyName} property", e.PropertyName);
    }

    /// <summary>
    /// Notifies that a property changed
    /// </summary>
    /// <param name="propertyName">The name of the property that changed</param>
	/// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null</exception>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            throw new ArgumentNullException(nameof(propertyName));
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanging"/> event
    /// </summary>
	/// <param name="e">The arguments of the event</param>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is null</exception>
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        if (e is null)
            throw new ArgumentNullException(nameof(e));
        Logger?.LogTrace("Raising PropertyChanging event for {PropertyName} property", e.PropertyName);
        PropertyChanging?.Invoke(this, e);
        Logger?.LogTrace("Raised PropertyChanging event for {PropertyName} property", e.PropertyName);
    }

    /// <summary>
    /// Notifies that a property is changing
    /// </summary>
	/// <param name="propertyName">The name of the property that is changing</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null</exception>
    protected void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            throw new ArgumentNullException(nameof(propertyName));
        OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
    }

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyName">The name of the property</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, TValue value, [CallerMemberName] string? propertyName = null) =>
        SetBackedProperty(ref backingField, value, EqualityComparer<TValue>.Default, propertyName);

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{TValue}"/> to use when comparing <paramref name="backingField"/> and <paramref name="value"/></param>
    /// <param name="propertyName">The name of the property</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, TValue value, IEqualityComparer<TValue> equalityComparer, [CallerMemberName] string? propertyName = null)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(equalityComparer);
#else
        if (equalityComparer is null)
            throw new ArgumentNullException(nameof(equalityComparer));
#endif
        if (!equalityComparer.Equals(backingField, value))
        {
            Logger?.LogTrace("{PropertyName} property is changing from {OldValue} to {NewValue}", propertyName, backingField, value);
            OnPropertyChanging(propertyName);
            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyChangingEventArgs">Arguments to use when raising the <see cref="PropertyChanging"/> event</param>
    /// <param name="propertyChangedEventArgs">Arguments to use when raising the <see cref="PropertyChanged"/> event</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, TValue value, PropertyChangingEventArgs propertyChangingEventArgs, PropertyChangedEventArgs propertyChangedEventArgs) =>
        SetBackedProperty(ref backingField, value, EqualityComparer<TValue>.Default, propertyChangingEventArgs, propertyChangedEventArgs);

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{TValue}"/> to use when comparing <paramref name="backingField"/> and <paramref name="value"/></param>
    /// <param name="propertyChangingEventArgs">Arguments to use when raising the <see cref="PropertyChanging"/> event</param>
    /// <param name="propertyChangedEventArgs">Arguments to use when raising the <see cref="PropertyChanged"/> event</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, TValue value, IEqualityComparer<TValue> equalityComparer, PropertyChangingEventArgs propertyChangingEventArgs, PropertyChangedEventArgs propertyChangedEventArgs)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(equalityComparer);
        ArgumentNullException.ThrowIfNull(propertyChangingEventArgs);
        ArgumentNullException.ThrowIfNull(propertyChangedEventArgs);
#else
        if (equalityComparer is null)
            throw new ArgumentNullException(nameof(equalityComparer));
        if (propertyChangingEventArgs is null)
            throw new ArgumentNullException(nameof(propertyChangingEventArgs));
        if (propertyChangedEventArgs is null)
            throw new ArgumentNullException(nameof(propertyChangedEventArgs));
#endif
        if (!equalityComparer.Equals(backingField, value))
        {
            Logger?.LogTrace("{PropertyName} property is changing from {OldValue} to {NewValue}", propertyChangedEventArgs.PropertyName, backingField, value);
            OnPropertyChanging(propertyChangingEventArgs);
            backingField = value;
            OnPropertyChanged(propertyChangedEventArgs);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyName">The name of the property</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, in TValue value, [CallerMemberName] string? propertyName = null) =>
        SetBackedProperty(ref backingField, in value, EqualityComparer<TValue>.Default, propertyName);

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{TValue}"/> to use when comparing <paramref name="backingField"/> and <paramref name="value"/></param>
    /// <param name="propertyName">The name of the property</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, in TValue value, IEqualityComparer<TValue> equalityComparer, [CallerMemberName] string? propertyName = null)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(equalityComparer);
#else
        if (equalityComparer is null)
            throw new ArgumentNullException(nameof(equalityComparer));
#endif
        if (!equalityComparer.Equals(backingField, value))
        {
            Logger?.LogTrace("{PropertyName} property is changing from {OldValue} to {NewValue}", propertyName, backingField, value);
            OnPropertyChanging(propertyName);
            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyChangingEventArgs">Arguments to use when raising the <see cref="PropertyChanging"/> event</param>
    /// <param name="propertyChangedEventArgs">Arguments to use when raising the <see cref="PropertyChanged"/> event</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, in TValue value, PropertyChangingEventArgs propertyChangingEventArgs, PropertyChangedEventArgs propertyChangedEventArgs) =>
        SetBackedProperty(ref backingField, in value, EqualityComparer<TValue>.Default, propertyChangingEventArgs, propertyChangedEventArgs);

    /// <summary>
    /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
    /// </summary>
    /// <typeparam name="TValue">The type of the property</typeparam>
    /// <param name="backingField">A reference to the backing field of the property</param>
    /// <param name="value">The new value</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{TValue}"/> to use when comparing <paramref name="backingField"/> and <paramref name="value"/></param>
    /// <param name="propertyChangingEventArgs">Arguments to use when raising the <see cref="PropertyChanging"/> event</param>
    /// <param name="propertyChangedEventArgs">Arguments to use when raising the <see cref="PropertyChanged"/> event</param>
    /// <returns>true if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, false</returns>
    [SuppressMessage("Code Analysis", "CA1045: Do not pass types by reference", Justification = "To 'correct' this would defeat the purpose of the method")]
    protected bool SetBackedProperty<TValue>(ref TValue backingField, in TValue value, IEqualityComparer<TValue> equalityComparer, PropertyChangingEventArgs propertyChangingEventArgs, PropertyChangedEventArgs propertyChangedEventArgs)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(equalityComparer);
        ArgumentNullException.ThrowIfNull(propertyChangingEventArgs);
        ArgumentNullException.ThrowIfNull(propertyChangedEventArgs);
#else
        if (equalityComparer is null)
            throw new ArgumentNullException(nameof(equalityComparer));
        if (propertyChangingEventArgs is null)
            throw new ArgumentNullException(nameof(propertyChangingEventArgs));
        if (propertyChangedEventArgs is null)
            throw new ArgumentNullException(nameof(propertyChangedEventArgs));
#endif
        if (!equalityComparer.Equals(backingField, value))
        {
            Logger?.LogTrace("{PropertyName} property is changing from {OldValue} to {NewValue}", propertyChangedEventArgs.PropertyName, backingField, value);
            OnPropertyChanging(propertyChangingEventArgs);
            backingField = value;
            OnPropertyChanged(propertyChangedEventArgs);
            return true;
        }
        return false;
    }
}
