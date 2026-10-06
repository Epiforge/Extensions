namespace Epiforge.Extensions.Blazor;

readonly struct ObservationKey(object root, MemberPath path) :
    IEquatable<ObservationKey>
{
    public readonly MemberPath Path = path;
    public readonly object Root = root;

    public bool Equals(ObservationKey other) =>
        ReferenceEquals(Root, other.Root) && ReferenceEquals(Path, other.Path);

    public override bool Equals(object? obj) =>
        obj is ObservationKey other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(Root), RuntimeHelpers.GetHashCode(Path));
}
