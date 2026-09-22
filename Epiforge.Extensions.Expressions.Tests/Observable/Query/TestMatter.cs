namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

public class TestMatter :
    PropertyChangeNotifier
{
    public TestMatter(string name, int hours)
    {
        this.name = name;
        this.hours = hours;
    }

    int hours;
    string name;

    public int Hours
    {
        get => hours;
        set => SetBackedProperty(ref hours, in value);
    }

    public string Name
    {
        get => name;
        set => SetBackedProperty(ref name, in value);
    }

    public override string ToString() =>
        $"{{{name}: {hours}}}";
}
