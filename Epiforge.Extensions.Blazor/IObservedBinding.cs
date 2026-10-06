namespace Epiforge.Extensions.Blazor;

/// <summary>
/// Represents a value which is observed and which can be read and written
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
public interface IObservedBinding<T>
{
    /// <summary>
    /// Gets/sets the value
    /// </summary>
    T Value { get; set; }
}
