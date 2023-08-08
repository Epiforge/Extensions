namespace Epiforge.Extensions.Collections.Generic;

/// <summary>
/// Provides an implementation of the <see cref="IComparer{T}"/> generic interface which reverses the order of the comparison
/// </summary>
/// <typeparam name="T">The type of objects to compare</typeparam>
public class ReversedComparer<T> :
    IComparer<T>
{
    /// <summary>
    /// Returns a default reversed sort order comparer for the type specified by the generic argument
    /// </summary>
    public static ReversedComparer<T> Default { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReversedComparer{T}"/> class
    /// </summary>
    public ReversedComparer() :
        this(Comparer<T>.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReversedComparer{T}"/> class based on the specified implementation of the <see cref="IComparer{T}"/> generic interface
    /// </summary>
    public ReversedComparer(IComparer<T> comparer) =>
        this.comparer = comparer;

    readonly IComparer<T> comparer;

    /// <summary>
    /// Performs a comparison of two objects of the same type and returns a value indicating whether one object is less than, equal to, or greater than the other
    /// </summary>
    /// <param name="x">The first object to compare</param>
    /// <param name="y">The second object to compare</param>
    /// <returns>
    /// A signed integer that indicates the relative values of <paramref name="x"/> and <paramref name="y"/>, as shown in the following table:
    /// Value – Meaning
    /// Less than zero – <paramref name="x"/> is greater than <paramref name="y"/>.
    /// Zero – <paramref name="x"/> equals <paramref name="y"/>.
    /// Greater than zero – <paramref name="x"/> is less than <paramref name="y"/>.
    /// </returns>
    public int Compare(T? x, T? y) =>
        comparer.Compare(y, x);
}
