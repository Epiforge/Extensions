namespace Epiforge.Extensions.Benchmarking;

public sealed class BenchmarkTeam :
    PropertyChangeNotifier
{
    public BenchmarkTeam(ObservableRangeCollection<BenchmarkPerson> people) =>
        this.people = people;

    ObservableRangeCollection<BenchmarkPerson> people;

    public ObservableRangeCollection<BenchmarkPerson> People
    {
        get => people;
        set => SetBackedProperty(ref people, in value);
    }
}
