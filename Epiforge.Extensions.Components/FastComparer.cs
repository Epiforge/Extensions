namespace Epiforge.Extensions.Components;

/// <summary>
/// Exposes a method that compares two objects of a specified type
/// </summary>
public class FastComparer :
    IComparer
{
    static readonly ConcurrentDictionary<Type, FastComparer> comparers = new();

    static FastComparer ComparersValueFactory(Type type) =>
        new(type);

    /// <summary>
    /// Gets a <see cref="FastComparer"/> for the specified type
    /// </summary>
    /// <param name="type">The type</param>
    public static FastComparer Get(Type type)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null)
            throw new ArgumentNullException(nameof(type));
#endif
        return comparers.GetOrAdd(type, ComparersValueFactory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FastEqualityComparer"/> class
    /// </summary>
    /// <param name="type">The type</param>
    public FastComparer(Type type)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type is null)
            throw new ArgumentNullException(nameof(type));
#endif
        Type = type;
        var comparerType = typeof(Comparer<>).MakeGenericType(type);
        comparer = comparerType.GetProperty(nameof(Comparer<object>.Default), BindingFlags.Public | BindingFlags.Static)!.FastGetValue(null)!;
        compare = comparerType.GetMethod(nameof(Comparer<object>.Compare), new[] { type, type })!;
    }

    readonly object comparer;
    readonly MethodInfo compare;

    /// <summary>
    /// Gets the type
    /// </summary>
    public Type Type { get; }

    /// <inheritdoc/>
    public int Compare(object? x, object? y) =>
        (int)compare.FastInvoke(comparer, x, y)!;
}