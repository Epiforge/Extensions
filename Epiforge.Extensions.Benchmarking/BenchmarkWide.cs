namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkWide :
    PropertyChangeNotifier
{
    public BenchmarkWide(int seed)
    {
        v0 = seed;
        v1 = seed + 1;
        v2 = seed + 2;
        v3 = seed + 3;
        v4 = seed + 4;
        v5 = seed + 5;
        v6 = seed + 6;
        v7 = seed + 7;
    }

    int v0;
    int v1;
    int v2;
    int v3;
    int v4;
    int v5;
    int v6;
    int v7;

    public int V0
    {
        get => v0;
        set => SetBackedProperty(ref v0, in value);
    }

    public int V1
    {
        get => v1;
        set => SetBackedProperty(ref v1, in value);
    }

    public int V2
    {
        get => v2;
        set => SetBackedProperty(ref v2, in value);
    }

    public int V3
    {
        get => v3;
        set => SetBackedProperty(ref v3, in value);
    }

    public int V4
    {
        get => v4;
        set => SetBackedProperty(ref v4, in value);
    }

    public int V5
    {
        get => v5;
        set => SetBackedProperty(ref v5, in value);
    }

    public int V6
    {
        get => v6;
        set => SetBackedProperty(ref v6, in value);
    }

    public int V7
    {
        get => v7;
        set => SetBackedProperty(ref v7, in value);
    }
}
