namespace Epiforge.Extensions.Components;

/// <summary>
/// Exposes a method that compares two objects of a specified type
/// </summary>
public class FastComparer :
    IComparer
{
    abstract class TypedComparer
    {
        internal abstract int Compare(object? x, object? y);
    }

    sealed class TypedComparer<T> :
        TypedComparer
    {
        internal override int Compare(object? x, object? y) =>
            Comparer<T>.Default.Compare((T)x!, (T)y!);
    }

    static readonly ConcurrentDictionary<Type, FastComparer> comparers = new();

    static FastComparer ComparersValueFactory(Type type) =>
        new(type);

    /// <summary>
    /// Gets a <see cref="FastComparer"/> for the specified type
    /// </summary>
    /// <param name="type">The type</param>
    public static FastComparer Get(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return comparers.GetOrAdd(type, ComparersValueFactory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FastEqualityComparer"/> class
    /// </summary>
    /// <param name="type">The type</param>
    public FastComparer(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
        typedComparer = (TypedComparer)Activator.CreateInstance(typeof(TypedComparer<>).MakeGenericType(type))!;
    }

    readonly TypedComparer typedComparer;

    /// <summary>
    /// Gets the type
    /// </summary>
    public Type Type { get; }

    /// <inheritdoc/>
    public int Compare(object? x, object? y) =>
        typedComparer.Compare(x, y);
}
