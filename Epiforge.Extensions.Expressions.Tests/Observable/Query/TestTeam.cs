namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

public class TestTeam :
    PropertyChangeNotifier,
    IComparable<TestTeam>
{
    public TestTeam() :
        this(new ObservableRangeCollection<TestPerson>())
    {
    }

    public TestTeam(ObservableRangeCollection<TestPerson>? people) =>
        this.people = people;

    ObservableRangeCollection<TestPerson>? people;

    public int CompareTo(TestTeam? other) =>
        GetHashCode().CompareTo(other?.GetHashCode() ?? 0);

    public ObservableRangeCollection<TestPerson>? People
    {
        get => people;
        set => SetBackedProperty(ref people, in value);
    }
}
