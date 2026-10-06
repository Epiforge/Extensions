namespace Epiforge.Extensions.Blazor;

/// <summary>
/// A property name observed on a source, kept for as long as some render cycle uses it
/// </summary>
sealed class NameSlot(ObservedSource source, string name)
{
    public bool IsLive = true;
    public readonly string Name = name;
    public readonly ObservedSource Source = source;
    public int Stamp;
}
