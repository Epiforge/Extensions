namespace Epiforge.Extensions.Blazor.Workbench.Model;

/// <summary>
/// Counts the renders of the cells and layout of one circuit, by what they are bound to and by which implementation rendered them
/// </summary>
public sealed class RenderLedger
{
    readonly ConcurrentDictionary<(object subject, string implementation), int> renders = new();
    int rendersOffTheCircuit;
    long lastRenderTimestamp;

    public long LastRenderTimestamp =>
        Interlocked.Read(ref lastRenderTimestamp);

    public int RendersOffTheCircuit =>
        Volatile.Read(ref rendersOffTheCircuit);

    public int RendersOf(object subject, string implementation) =>
        renders.TryGetValue((subject, implementation), out var count) ? count : 0;

    public int Total(string implementation) =>
        renders.Where(pair => pair.Key.implementation == implementation).Sum(pair => pair.Value);

    public void Rendered(object subject, string implementation)
    {
        renders.AddOrUpdate((subject, implementation), 1, static (_, count) => count + 1);
        if (SynchronizationContext.Current?.GetType().Name.Contains("Renderer", StringComparison.Ordinal) != true)
            Interlocked.Increment(ref rendersOffTheCircuit);
        Interlocked.Exchange(ref lastRenderTimestamp, Stopwatch.GetTimestamp());
    }
}
