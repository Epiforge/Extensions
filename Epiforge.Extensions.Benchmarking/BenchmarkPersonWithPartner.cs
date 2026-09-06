namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkPersonWithPartner :
    PropertyChangeNotifier
{
    public static ObservableRangeCollection<BenchmarkPersonWithPartner> CreateCollection(int count)
    {
        var people = new List<BenchmarkPersonWithPartner>(count);
        for (var i = 0; i < count; ++i)
            people.Add(new BenchmarkPersonWithPartner($"P{i}", i) { Partner = new BenchmarkPersonWithPartner($"Q{i}", i) });
        return new ObservableRangeCollection<BenchmarkPersonWithPartner>(people);
    }

    public BenchmarkPersonWithPartner(string name, int rank)
    {
        this.name = name;
        this.rank = rank;
    }

    string name;
    BenchmarkPersonWithPartner? partner;
    int rank;

    public string Name
    {
        get => name;
        set => SetBackedProperty(ref name, in value);
    }

    public BenchmarkPersonWithPartner? Partner
    {
        get => partner;
        set => SetBackedProperty(ref partner, in value);
    }

    public int Rank
    {
        get => rank;
        set => SetBackedProperty(ref rank, in value);
    }
}
