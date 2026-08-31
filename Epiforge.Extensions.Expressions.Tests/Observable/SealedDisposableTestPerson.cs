namespace Epiforge.Extensions.Expressions.Tests.Observable;

public sealed class SealedDisposableTestPerson :
    Disposable
{
    public SealedDisposableTestPerson()
    {
    }

    public SealedDisposableTestPerson(string name) =>
        this.name = name;

    string? name;

    public string? Name
    {
        get => name;
        set => SetBackedProperty(ref name, in value);
    }

    protected override bool Dispose(bool disposing) =>
        true;

    protected override ValueTask<bool> DisposeAsyncCore() =>
        new(true);

    public override string ToString() =>
        $"{{{name}}}";

    public static SealedDisposableTestPerson operator +(SealedDisposableTestPerson a, SealedDisposableTestPerson b) =>
        new($"{a.name} {b.name}");
}
