namespace Epiforge.Extensions.Components;

/// <summary>
/// Defines methods to support the comparison of objects of a specified type for equality
/// </summary>
public class FastEqualityComparer :
    IEqualityComparer
{
    /// <summary>
    /// Compares values of a type which arrive already boxed without unboxing them, which the default comparer for a value type that does not implement <see cref="IEquatable{T}"/> cannot do without boxing one of them again to reach the same comparison; <see langword="null"/>, which is how a nullable value type without a value arrives, equals only itself and hashes to zero
    /// </summary>
    sealed class BoxedComparer :
        TypedComparer
    {
        internal override bool AreEqual(object? x, object? y) =>
            x is null ? y is null : x.Equals(y);

        internal override int HashCodeOf(object obj) =>
            obj?.GetHashCode() ?? 0;
    }

    /// <summary>
    /// Compares as <see cref="EqualityComparer{T}.Default"/> would, through reflection, where the runtime cannot generate code: each value cast to the type as the generated comparer casts it, then the type's own <see cref="IEquatable{T}"/> where it implements one, otherwise <see cref="object.Equals(object?)"/>
    /// </summary>
    sealed class ReflectedComparer(Type type, ReflectedInvocation? equals) :
        TypedComparer
    {
        readonly ObjectCast cast = new(type);

        internal override bool AreEqual(object? x, object? y)
        {
            x = cast.Apply(x);
            y = cast.Apply(y);
            return x is null ? y is null : y is not null && (equals is { } invocation ? (bool)invocation.InvokeWithOneArgument(x, y)! : x.Equals(y));
        }

        internal override int HashCodeOf(object obj) =>
            cast.Apply(obj)?.GetHashCode() ?? 0;
    }

    abstract class TypedComparer
    {
        internal abstract bool AreEqual(object? x, object? y);

        internal abstract int HashCodeOf(object obj);
    }

    sealed class TypedComparer<T> :
        TypedComparer
    {
        internal override bool AreEqual(object? x, object? y) =>
            EqualityComparer<T>.Default.Equals((T)x!, (T)y!);

        internal override int HashCodeOf(object obj) =>
            EqualityComparer<T>.Default.GetHashCode((T)obj);
    }

    static readonly ConcurrentDictionary<Type, FastEqualityComparer> equalityComparers = new();

    static FastEqualityComparer EqualityComparersValueFactory(Type type) =>
        new(type);

    /// <summary>
    /// Gets a <see cref="FastEqualityComparer"/> for the specified type
    /// </summary>
    /// <param name="type">The type</param>
    public static FastEqualityComparer Get(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return equalityComparers.GetOrAdd(type, EqualityComparersValueFactory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FastEqualityComparer"/> class
    /// </summary>
    /// <param name="type">The type</param>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code which needs dynamic code runs only where RuntimeFeature.IsDynamicCodeSupported is true, which the analyzer recognizes as a guard from .NET 9 on")]
    public FastEqualityComparer(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
        if (RuntimeFeature.IsDynamicCodeSupported)
            typedComparer = Typed(type);
        else
            typedComparer = Reflected(type);
    }

    readonly TypedComparer typedComparer;

    static TypedComparer Reflected(Type type)
    {
        var equals = ReflectedInvocation.OfOwnGenericInterface(type, typeof(IEquatable<>), nameof(IEquatable<object>.Equals));
        return type.IsValueType && equals is null ? new BoxedComparer() : new ReflectedComparer(type, equals);
    }

    [RequiresDynamicCode("Constructs a comparer for the type")]
    static TypedComparer Typed(Type type) =>
        type.IsValueType && !typeof(IEquatable<>).MakeGenericType(type).IsAssignableFrom(type)
            ? new BoxedComparer()
            : (TypedComparer)Activator.CreateInstance(typeof(TypedComparer<>).MakeGenericType(type))!;

    /// <summary>
    /// Gets the type
    /// </summary>
    public Type Type { get; }

    /// <inheritdoc/>
    public new bool Equals(object? x, object? y) =>
        typedComparer.AreEqual(x, y);

    /// <inheritdoc/>
    public int GetHashCode(object obj) =>
        typedComparer.HashCodeOf(obj);
}
