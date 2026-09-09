namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Stands in for the query object an application's formula engine obtains from a method and then reads a notifying value from, which is disposable and which the observer is therefore told to dispose of
/// </summary>
public sealed class BenchmarkValueQuery :
    IDisposable,
    INotifyPropertyChanged
{
    internal BenchmarkValueQuery(BenchmarkPersonWithPartner person)
    {
        this.person = person;
        person.PropertyChanged += PersonPropertyChanged;
    }

    readonly BenchmarkPersonWithPartner person;

    public object? Value =>
        person.Rank;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        person.PropertyChanged -= PersonPropertyChanged;
        PropertyChanged = null;
    }

    void PersonPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}

/// <summary>
/// The same object without disposal, sealed and implementing neither disposal interface, which is what makes the shape reading through it eligible today and therefore the ceiling the disposable one is measured against
/// </summary>
public sealed class BenchmarkPlainValue :
    INotifyPropertyChanged
{
    internal BenchmarkPlainValue(BenchmarkPersonWithPartner person)
    {
        this.person = person;
        person.PropertyChanged += PersonPropertyChanged;
    }

    readonly BenchmarkPersonWithPartner person;

    public object? Value =>
        person.Rank;

    public event PropertyChangedEventHandler? PropertyChanged;

    void PersonPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}

/// <summary>
/// Stands in for the core an application's formula engine captures and calls through, whose every operand in the emitted expression is a constant or a captured local
/// </summary>
public sealed class BenchmarkValueSource
{
    [return: DisposeWhenDiscarded]
    public BenchmarkValueQuery Open(BenchmarkPersonWithPartner person) =>
        new(person);

    public BenchmarkPlainValue OpenPlain(BenchmarkPersonWithPartner person) =>
        new(person);
}
