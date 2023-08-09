namespace Epiforge.Extensions.Expressions.Tests.Observable;

public class TestPerson :
    PropertyChangeNotifier
{
    public static RangeObservableCollection<TestPerson> CreatePeopleCollection() =>
        new(MakePeople());

    public static ObservableDictionary<int, TestPerson> CreatePeopleDictionary(SynchronizationContext? synchronizationContext = null) =>
        new(MakePeople().Select((person, index) => (person, index)).ToDictionary(pi => pi.index, pi => pi.person));

    public static IEnumerable<TestPerson> MakePeople() => new TestPerson[]
    {
        new TestPerson("John"),
        new TestPerson("Emily"),
        new TestPerson("Charles"),
        new TestPerson("Erin"),
        new TestPerson("Cliff"),
        new TestPerson("Hunter"),
        new TestPerson("Ben"),
        new TestPerson("Craig"),
        new TestPerson("Bridget"),
        new TestPerson("Nanette"),
        new TestPerson("George"),
        new TestPerson("Bryan"),
        new TestPerson("James"),
        new TestPerson("Steve")
    };

    public TestPerson()
    {
    }

    public TestPerson(string name) =>
        this.name = name;

    string? name;
    long nameGets;

    public override string ToString() =>
        $"{{{name}}}";

    public string? Name
    {
        get
        {
            OnPropertyChanging(nameof(NameGets));
            Interlocked.Increment(ref nameGets);
            OnPropertyChanged(nameof(NameGets));
            return name;
        }
        set => SetBackedProperty(ref name, in value);
    }

    public long NameGets =>
        Interlocked.Read(ref nameGets);

#pragma warning disable CA1822 // Mark members as static
    public string? Placeholder =>
        null;
#pragma warning restore CA1822 // Mark members as static

    public static TestPerson CreateEmily() =>
        new() { name = "Emily" };

    public static TestPerson CreateJohn() =>
        new() { name = "John" };

    public static TestPerson operator +(TestPerson a, TestPerson b) =>
        new() { name = $"{a.name} {b.name}" };

    public static TestPerson operator -(TestPerson testPerson) =>
        new() { name = new string(testPerson.name?.Reverse().ToArray()) };
}
