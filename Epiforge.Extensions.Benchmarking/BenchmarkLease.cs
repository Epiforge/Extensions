namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// A value the observer disposes of when it is replaced, constructed from an argument which can change, which is what refuses an expression at its root
/// </summary>
public sealed class BenchmarkLease(int value) :
    IDisposable
{
    public int Value { get; } = value;

    public void Dispose()
    {
    }
}
