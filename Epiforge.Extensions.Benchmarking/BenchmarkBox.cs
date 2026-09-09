namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkBox
{
    public BenchmarkBox()
    {
    }

    public BenchmarkBox(int first) =>
        First = first;

    public int First;
}
