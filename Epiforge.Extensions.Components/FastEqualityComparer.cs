namespace Epiforge.Extensions.Components;

/// <summary>
/// Defines methods to support the comparison of objects of a specified type for equality
/// </summary>
public class FastEqualityComparer :
    IEqualityComparer
{
    static readonly ConcurrentDictionary<Type, FastEqualityComparer> equalityComparers = new();

    static FastEqualityComparer EqualityComparersValueFactory(Type type) =>
        new(type);

    /// <summary>
    /// Gets a <see cref="FastEqualityComparer"/> for the specified type
    /// </summary>
    /// <param name="type">The type</param>
    public static FastEqualityComparer Get(Type type) =>
        type is null ? throw new ArgumentNullException(nameof(type)) : equalityComparers.GetOrAdd(type, EqualityComparersValueFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="FastEqualityComparer"/> class
    /// </summary>
    /// <param name="type">The type</param>
    public FastEqualityComparer(Type type)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null)
            throw new ArgumentNullException(nameof(type));
#endif
        Type = type;
        var equalityComparerType = typeof(EqualityComparer<>).MakeGenericType(type);
        equalityComparer = equalityComparerType.GetProperty(nameof(EqualityComparer<object>.Default), BindingFlags.Public | BindingFlags.Static)!.FastGetValue(null)!;
        equals = equalityComparerType.GetMethod(nameof(EqualityComparer<object>.Equals), new[] { type, type })!;
        getHashCode = equalityComparerType.GetMethod(nameof(EqualityComparer<object>.GetHashCode), new[] { type })!;
    }

    readonly object equalityComparer;
    readonly MethodInfo equals;
    readonly MethodInfo getHashCode;

    /// <summary>
    /// Gets the type
    /// </summary>
    public Type Type { get; }

    /// <inheritdoc/>
    public new bool Equals(object? x, object? y) =>
        (bool)equals.FastInvoke(equalityComparer, x, y)!;

    /// <inheritdoc/>
    public int GetHashCode(object obj) =>
        (int)getHashCode.FastInvoke(equalityComparer, obj)!;
}
