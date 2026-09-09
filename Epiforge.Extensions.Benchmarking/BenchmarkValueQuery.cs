namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Stands in for the query object an application's formula engine obtains from a method and then reads a notifying value from, which is disposable and which the observer is therefore told to dispose of
/// </summary>
public sealed class BenchmarkValueQuery :
    IDisposable,
    INotifyPropertyChanged
{
    internal BenchmarkValueQuery(BenchmarkPersonWithPartner person) =>
        this.person = person;

    readonly BenchmarkPersonWithPartner person;

    public object? Value =>
        person.Rank;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose() =>
        PropertyChanged = null;
}

/// <summary>
/// The same object without disposal, sealed and implementing neither disposal interface, which is what makes the shape reading through it eligible whatever the options say and therefore the ceiling the disposable one is measured against
/// </summary>
/// <remarks>
/// Neither this nor its disposable twin subscribes to the person it reads. An earlier form of both did, and the plain one, having no disposal to detach in, left a handler on every person for every element of every invocation, which grew without bound across a run and inflated exactly the arms this instrument exists to compare. These arms construct and discard rather than mutate, so what is lost by not subscribing is nothing they measure
/// </remarks>
public sealed class BenchmarkPlainValue :
    INotifyPropertyChanged
{
    internal BenchmarkPlainValue(BenchmarkPersonWithPartner person) =>
        this.person = person;

    readonly BenchmarkPersonWithPartner person;

    public object? Value =>
        person.Rank;

#pragma warning disable CS0067 // The event is never used
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
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
