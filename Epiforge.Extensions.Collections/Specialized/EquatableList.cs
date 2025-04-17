namespace Epiforge.Extensions.Collections.Specialized;

// TODO: dotnet8 Frozen

/// <summary>
/// Represents a strongly typed list of objects that can be compared to other lists
/// </summary>
/// <typeparam name="T">The type of the elements in the list</typeparam>
public readonly struct EquatableList<T> :
    IReadOnlyList<T>,
    IEquatable<EquatableList<T>>
{
    /// <summary>
    /// Determines whether two specified instances of <see cref="EquatableList{T}"/> are equal
    /// </summary>
    /// <param name="a">The first object to compare</param>
    /// <param name="b">The second object to compare</param>
    /// <returns><c>true</c> if <paramref name="a"/> and <paramref name="b"/> represent the list; otherwise, <c>false</c></returns>
    public static bool operator ==(EquatableList<T> a, EquatableList<T> b) =>
        a.Equals(b);

    /// <summary>
    /// Determines whether two specified instances of <see cref="EquatableList{T}"/> are not equal
    /// </summary>
    /// <param name="a">The first object to compare</param>
    /// <param name="b">The second object to compare</param>
    /// <returns><c>true</c> if <paramref name="a"/> and <paramref name="b"/> do not represent the list; otherwise, <c>false</c></returns>
    public static bool operator !=(EquatableList<T> a, EquatableList<T> b) =>
        !(a == b);

    /// <summary>
    /// Initializes a new instance of the <see cref="EquatableList{T}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="sequence">The sequence whose elements are copied</param>
    public EquatableList(IEnumerable<T> sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        EqualityComparer = null;
        elements = sequence.ToImmutableArray();
        var hashCode = new System.HashCode();
        foreach (var element in elements)
            hashCode.Add(element);
        this.hashCode = hashCode.ToHashCode();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EquatableList{T}"/> class that contains elements copied from the specified collection and compares elements using the specified equality comparer
    /// </summary>
    /// <param name="sequence">The sequence whose elements are copied</param>
    /// <param name="equalityComparer">The equality comparer to use to determine whether elements are equal</param>
    public EquatableList(IEnumerable<T> sequence, IEqualityComparer<T> equalityComparer)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(equalityComparer);
        EqualityComparer = equalityComparer;
        elements = sequence.ToImmutableArray();
        var hashCode = new System.HashCode();
        foreach (var element in elements)
            hashCode.Add(element, EqualityComparer);
        this.hashCode = hashCode.ToHashCode();
    }

    readonly IReadOnlyList<T> elements;
    readonly int hashCode;

    /// <summary>
    /// Gets the element at the specified index
    /// </summary>
    /// <param name="index">The zero-based index of the element to get</param>
    /// <returns>The element at the specified index</returns>
    public readonly T this[int index] =>
        elements[index];

    /// <summary>
    /// Gets the number of elements contained in the <see cref="EquatableList{T}"/>
    /// </summary>
    public readonly int Count =>
        elements.Count;

    /// <summary>
    /// Gets the equality comparer used to determine whether elements are equal if one was specified when the <see cref="EquatableList{T}"/> was instantiated
    /// </summary>
    public IEqualityComparer<T>? EqualityComparer { get; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    /// <param name="obj">The object to compare with the current object</param>
    /// <returns><c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c></returns>
    public override readonly bool Equals(object? obj) =>
        obj is EquatableList<T> other && Equals(other);

    /// <summary>
    /// Determines whether the specified <see cref="EquatableList{T}"/> is equal to the current <see cref="EquatableList{T}"/> using the current <see cref="IEqualityComparer{T}"/> (see <see cref="EqualityComparer"/>)
    /// </summary>
    /// <param name="other">The <see cref="EquatableList{T}"/> to compare with the current <see cref="EquatableList{T}"/></param>
    /// <returns><c>true</c> if the specified <see cref="EquatableList{T}"/> is equal to the current <see cref="EquatableList{T}"/>; otherwise, <c>false</c></returns>
    public readonly bool Equals(EquatableList<T> other) =>
        EqualityComparer is not null && EqualityComparer.Equals(other.EqualityComparer) && elements.SequenceEqual(other.elements, EqualityComparer) || other.EqualityComparer is null && elements.SequenceEqual(other.elements);

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="EquatableList{T}"/>
    /// </summary>
    public readonly IEnumerator<T> GetEnumerator() =>
        elements.GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator() =>
        elements.GetEnumerator();

    /// <summary>
    /// Gets a hash code for the <see cref="EquatableList{T}"/>
    /// </summary>
    public override readonly int GetHashCode() =>
        hashCode;
}
