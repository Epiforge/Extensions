namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkPerson :
    PropertyChangeNotifier
{
    public static ObservableRangeCollection<BenchmarkPerson> CreateCollection(int count)
    {
        var people = new List<BenchmarkPerson>(count);
        for (var i = 0; i < count; ++i)
            people.Add(new BenchmarkPerson($"P{i}", i));
        return new ObservableRangeCollection<BenchmarkPerson>(people);
    }

    public BenchmarkPerson(string name, int rank)
    {
        this.name = name;
        this.rank = rank;
    }

    string name;
    int rank;

    public string Name
    {
        get => name;
        set => SetBackedProperty(ref name, in value);
    }

    public int Rank
    {
        get => rank;
        set => SetBackedProperty(ref rank, in value);
    }
}
