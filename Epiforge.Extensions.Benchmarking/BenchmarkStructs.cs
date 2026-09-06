namespace Epiforge.Extensions.Benchmarking;

public readonly struct OneFieldStruct
{
    public static OneFieldStruct Create(int first) =>
        new(first);

    public OneFieldStruct(int first) =>
        First = first;

    public int First { get; }
}

public readonly struct TwoFieldStruct
{
    public static TwoFieldStruct Create(int first, int second) =>
        new(first, second);

    public TwoFieldStruct(int first, int second)
    {
        First = first;
        Second = second;
    }

    public int First { get; }

    public int Second { get; }
}

public readonly struct ThreeFieldStruct
{
    public static ThreeFieldStruct Create(int first, int second, int third) =>
        new(first, second, third);

    public ThreeFieldStruct(int first, int second, int third)
    {
        First = first;
        Second = second;
        Third = third;
    }

    public int First { get; }

    public int Second { get; }

    public int Third { get; }
}
