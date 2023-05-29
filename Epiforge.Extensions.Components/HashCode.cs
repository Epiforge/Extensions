namespace Epiforge.Extensions.Components;

#pragma warning disable CA1066, CA1815, CA2231

/// <summary>
/// Combines the hash code for multiple values into a single hash code
/// </summary>
public struct HashCode
{
    /// <summary>
    /// Diffuses the hash code returned by the specified value
    /// </summary>
    /// <typeparam name="T1">The type of the value to add the hash code</typeparam>
    /// <param name="value1">The value to add to the hash code</param>
    /// <returns>The hash code that represents the single value</returns>
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1>(T1 value1) =>
#if IS_NET_STANDARD_2_1_OR_GREATER
        System.HashCode.Combine(value1);
#else
        value1?.GetHashCode() ?? 0;
#endif

    /// <summary>
    /// Combines two values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <returns>The hash code that represents the two values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2>(T1 value1, T2 value2)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Combines three values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <param name="value3">The third value to combine into the hash code</param>
    /// <returns>The hash code that represents the three values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2, value3);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value3?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Combines four values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <param name="value3">The third value to combine into the hash code</param>
    /// <param name="value4">The fourth value to combine into the hash code</param>
    /// <returns>The hash code that represents the four values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2, value3, value4);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value3?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value4?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Combines five values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <param name="value3">The third value to combine into the hash code</param>
    /// <param name="value4">The fourth value to combine into the hash code</param>
    /// <param name="value5">The fifth value to combine into the hash code</param>
    /// <returns>The hash code that represents the five values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2, value3, value4, value5);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value3?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value4?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value5?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Combines six values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code</typeparam>
    /// <typeparam name="T6">The type of the sixth value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <param name="value3">The third value to combine into the hash code</param>
    /// <param name="value4">The fourth value to combine into the hash code</param>
    /// <param name="value5">The fifth value to combine into the hash code</param>
    /// <param name="value6">The sixth value to combine into the hash code</param>
    /// <returns>The hash code that represents the six values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2, value3, value4, value5, value6);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value3?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value4?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value5?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value6?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Combines seven values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code</typeparam>
    /// <typeparam name="T6">The type of the sixth value to combine into the hash code</typeparam>
    /// <typeparam name="T7">The type of the seventh value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <param name="value3">The third value to combine into the hash code</param>
    /// <param name="value4">The fourth value to combine into the hash code</param>
    /// <param name="value5">The fifth value to combine into the hash code</param>
    /// <param name="value6">The sixth value to combine into the hash code</param>
    /// <param name="value7">The seventh value to combine into the hash code</param>
    /// <returns>The hash code that represents the seven values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2, value3, value4, value5, value6, value7);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value3?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value4?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value5?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value6?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value7?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Combines eight values into a hash code
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code</typeparam>
    /// <typeparam name="T6">The type of the sixth value to combine into the hash code</typeparam>
    /// <typeparam name="T7">The type of the seventh value to combine into the hash code</typeparam>
    /// <typeparam name="T8">The type of the eighth value to combine into the hash code</typeparam>
    /// <param name="value1">The first value to combine into the hash code</param>
    /// <param name="value2">The second value to combine into the hash code</param>
    /// <param name="value3">The third value to combine into the hash code</param>
    /// <param name="value4">The fourth value to combine into the hash code</param>
    /// <param name="value5">The fifth value to combine into the hash code</param>
    /// <param name="value6">The sixth value to combine into the hash code</param>
    /// <param name="value7">The seventh value to combine into the hash code</param>
    /// <param name="value8">The eighth value to combine into the hash code</param>
    /// <returns>The hash code that represents the eight values</returns>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        return System.HashCode.Combine(value1, value2, value3, value4, value5, value6, value7, value8);
#else
        unchecked
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + (value1?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value2?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value3?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value4?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value5?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value6?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value7?.GetHashCode() ?? 0);
            hashCode = hashCode * 23 + (value8?.GetHashCode() ?? 0);
            return hashCode;
        }
#endif
    }

    /// <summary>
    /// Creates a new instance of <see cref="HashCode"/>
    /// </summary>
#if IS_NET_STANDARD_2_1_OR_GREATER
    [Obsolete("Use System.HashCode instead")]
#endif
    public HashCode() =>
#if IS_NET_STANDARD_2_1_OR_GREATER
        hashCode = new();
#else
        hashCode = 17;
#endif

#if IS_NET_STANDARD_2_1_OR_GREATER
    System.HashCode hashCode;
#else
    int hashCode;
#endif

    /// <summary>
    /// Adds a single value to the hash code
    /// </summary>
    /// <typeparam name="T">The type of the value to add to the hash code</typeparam>
    /// <param name="value">The value to add to the hash code</param>
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public void Add<T>(T value) =>
        Add(value, null);

    /// <summary>
    /// Adds a single value to the hash code, specifying the type that provides the hash code function
    /// </summary>
    /// <typeparam name="T">The type of the value to add to the hash code</typeparam>
    /// <param name="value">The value to add to the hash code</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> to use to calculate the hash code</param>
    [SuppressMessage("Style", "IDE0022: Use expression body for method")]
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public void Add<T>(T value, IEqualityComparer<T>? comparer)
    {
#if IS_NET_STANDARD_2_1_OR_GREATER
        hashCode.Add(value, comparer);
#else
        unchecked
        {
            hashCode = hashCode * 23 + (value == null ? 0 : (comparer ?? EqualityComparer<T>.Default).GetHashCode(value));
        }
#endif
    }

#pragma warning disable 0809
    /// <inheritdoc/>
    [Obsolete("HashCode is a mutable struct and should not be compared with other HashCodes.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override readonly bool Equals(object? obj) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    [Obsolete("HashCode is a mutable struct and should not be compared with other HashCodes. Use ToHashCode to retrieve the computed hash code.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override readonly int GetHashCode() =>
        throw new NotSupportedException();
#pragma warning restore 0809

    /// <summary>
    /// Calculates the final hash code after consecutive <see cref="Add{T}(T)"/>
    /// </summary>
    /// <returns></returns>
#if IS_NET_STANDARD_2_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public int ToHashCode() =>
#if IS_NET_STANDARD_2_1_OR_GREATER
        hashCode.ToHashCode();
#else
        hashCode;
#endif
}