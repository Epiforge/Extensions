namespace Epiforge.Extensions.Blazor.Tests;

public class Person :
    Notifier
{
    int age;
    string? name;
    Person? partner;
    TrackedCollection<string>? tags;

    public int Age
    {
        get => age;
        set => Set(ref age, value);
    }

    public string? Name
    {
        get => name;
        set => Set(ref name, value);
    }

    public string? Nickname;

    public Person? Partner
    {
        get => partner;
        set => Set(ref partner, value);
    }

    public Point Position { get; set; }

    public int ReadOnlyAge =>
        age;

    public TrackedCollection<string>? Tags
    {
        get => tags;
        set => Set(ref tags, value);
    }
}
