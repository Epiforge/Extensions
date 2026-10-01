namespace Epiforge.Extensions.Components;

/// <summary>
/// Exposes a method that compares two objects of a specified type
/// </summary>
public class FastComparer :
    IComparer
{
    /// <summary>
    /// Compares as <see cref="Comparer{T}.Default"/> would, through reflection, where the runtime cannot generate code: <see langword="null"/> first, then the type's own <see cref="IComparable{T}"/> where it implements one, otherwise <see cref="IComparable"/>
    /// </summary>
    sealed class ReflectedComparer(Type type) :
        TypedComparer
    {
        readonly ReflectedInvocation? compareTo = ReflectedInvocation.OfOwnGenericInterface(Nullable.GetUnderlyingType(type) ?? type, typeof(IComparable<>), nameof(IComparable<object>.CompareTo));

        internal override int Compare(object? x, object? y) =>
            x is null ? y is null ? 0 : -1 : y is null ? 1 : compareTo is { } invocation ? (int)invocation.InvokeWithOneArgument(x, y)! : Comparer.Default.Compare(x, y);
    }

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
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code which needs dynamic code runs only where RuntimeFeature.IsDynamicCodeSupported is true, which the analyzer recognizes as a guard from .NET 9 on")]
    public FastComparer(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
        if (RuntimeFeature.IsDynamicCodeSupported)
            typedComparer = Typed(type);
        else
            typedComparer = new ReflectedComparer(type);
    }

    readonly TypedComparer typedComparer;

    [RequiresDynamicCode("Constructs a comparer for the type")]
    static TypedComparer Typed(Type type) =>
        (TypedComparer)Activator.CreateInstance(typeof(TypedComparer<>).MakeGenericType(type))!;

    /// <summary>
    /// Gets the type
    /// </summary>
    public Type Type { get; }

    /// <inheritdoc/>
    public int Compare(object? x, object? y) =>
        typedComparer.Compare(x, y);
}
