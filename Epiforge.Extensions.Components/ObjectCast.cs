namespace Epiforge.Extensions.Components;

/// <summary>
/// Casts an object to a type as a cast from <see cref="object"/> to that type does in compiled code, so that invoking and comparing through reflection accept and refuse exactly what the generated invokers and comparers do
/// </summary>
/// <remarks>
/// A value type takes a value of exactly its type, or of an enumeration or primitive type with the same underlying type, and refuses <see langword="null"/> with a <see cref="NullReferenceException"/>; a nullable value type takes <see langword="null"/> or a value of exactly its underlying type; any other type takes <see langword="null"/> or an instance of itself. What is refused otherwise throws an <see cref="InvalidCastException"/>
/// </remarks>
sealed class ObjectCast
{
    internal ObjectCast(Type type)
    {
        this.type = type;
        nullableOf = Nullable.GetUnderlyingType(type);
        if (type.IsValueType && nullableOf is null)
            underlying = UnderlyingOf(type);
    }

    readonly Type? nullableOf;
    readonly Type type;
    readonly Type? underlying;

    /// <summary>
    /// Casts the specified value, returning it as the type would hold it
    /// </summary>
    internal object? Apply(object? value)
    {
        if (value is null)
            return underlying is null ? null : throw new NullReferenceException();
        var valueType = value.GetType();
        if (valueType == type)
            return value;
        if (nullableOf is not null)
            return valueType == nullableOf ? value : throw Refusal(valueType);
        if (underlying is null)
            return type.IsInstanceOfType(value) ? value : throw Refusal(valueType);
        if (UnderlyingOf(valueType) == underlying)
            return type.IsEnum ? Enum.ToObject(type, value) : Convert.ChangeType(value, type, null);
        throw Refusal(valueType);
    }

    InvalidCastException Refusal(Type valueType) =>
        new($"Unable to cast object of type '{valueType}' to type '{type}'.");

    static Type UnderlyingOf(Type type) =>
        type.IsEnum ? Enum.GetUnderlyingType(type) : type;
}
