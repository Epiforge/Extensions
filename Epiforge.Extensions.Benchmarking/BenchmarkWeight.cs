namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Stands in for a captured object an expression calls a cheap, pure method on, whose return type is sealed and implements neither disposal interface and which is therefore admitted but not held
/// </summary>
/// <remarks>
/// Nothing this call reads can change, so the graph gives it a node evaluated once and never again. The fast path has one compiled delegate for the whole body and re-invokes all of it whenever any subscribed source announces, so it makes this call again on every notification. That difference is what these arms exist to price
/// </remarks>
public sealed class BenchmarkWeight
{
    public int Scale() =>
        2;
}
