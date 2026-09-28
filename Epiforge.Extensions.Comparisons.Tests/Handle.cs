namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// A value a view makes from each element, which counts in its ledger how many were made and how many disposed of
/// </summary>
public sealed class Handle :
    IDisposable
{
    public Handle(Ledger ledger, int value)
    {
        this.ledger = ledger;
        Value = value;
        Interlocked.Increment(ref ledger.Made);
    }

    readonly Ledger ledger;

    public int Value { get; }

    public void Dispose() =>
        Interlocked.Increment(ref ledger.Disposed);

    public sealed class Ledger
    {
        public int Disposed;
        public int Made;
    }
}
