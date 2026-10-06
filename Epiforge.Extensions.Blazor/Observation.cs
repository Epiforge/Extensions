namespace Epiforge.Extensions.Blazor;

/// <summary>
/// What a reactivity manager remembers about one chain of members read from one root, so that reading it again finds what it observed last time
/// </summary>
sealed class Observation(object root, MemberPath path)
{
    public ObservedSource? CollectionSource;
    NameSlot? firstSlot;
    NameSlot?[]? laterSlots;
    public readonly MemberPath Path = path;
    public readonly object Root = root;
    public int Stamp;

    public NameSlot? GetSlot(int index) =>
        index == 0 ? firstSlot : laterSlots?[index - 1];

    public void SetSlot(int index, NameSlot slot)
    {
        if (index == 0)
            firstSlot = slot;
        else
            (laterSlots ??= new NameSlot?[Path.Steps.Length - 1])[index - 1] = slot;
    }
}
