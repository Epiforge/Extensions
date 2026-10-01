namespace Epiforge.Extensions.Components.Tests;

/// <summary>
/// Pins what the reflection helpers and the comparers answer, so that a runtime without dynamic code is held to the same answers as one with it
/// </summary>
[TestClass]
public class DynamicCodeAgreement
{
    #region Test Types

    public enum Shade
    {
        Light = 1,
        Dark = 2
    }

    public readonly struct OrderedOnlyGenerically(int rank) :
        IComparable<OrderedOnlyGenerically>
    {
        public int Rank { get; } = rank;

        public int CompareTo(OrderedOnlyGenerically other) =>
            other.Rank.CompareTo(Rank);
    }

    public readonly struct OrderedTwoWays(int rank) :
        IComparable<OrderedTwoWays>,
        IComparable
    {
        public int Rank { get; } = rank;

        public int CompareTo(OrderedTwoWays other) =>
            other.Rank.CompareTo(Rank);

        public int CompareTo(object? obj) =>
            obj is OrderedTwoWays other ? Rank.CompareTo(other.Rank) : 1;
    }

    public readonly struct EquatableByParity(int value) :
        IEquatable<EquatableByParity>
    {
        public int Value { get; } = value;

        public bool Equals(EquatableByParity other) =>
            Value % 2 == other.Value % 2;

        public override bool Equals(object? obj) =>
            obj is EquatableByParity other && Value == other.Value;

        public override int GetHashCode() =>
            Value % 2;
    }

    public struct Point
    {
        public int X;
        public string? Label;
    }

    public sealed class Subject
    {
        public Subject()
        {
        }

        public Subject(int a) =>
            Total = a;

        public Subject(int a, int b) =>
            Total = a + b;

        public Subject(int a, int b, int c) =>
            Total = a + b + c;

        public int Total { get; set; }

        public int this[int index] =>
            Total * index;

        public int Divide(int divisor) =>
            Total / divisor;

        public int None() =>
            Total;

        public int One(int a) =>
            Total + a;

        public int Two(int a, int b) =>
            Total + a + b;

        public int Three(int a, int b, int c) =>
            Total + a + b + c;

        public static int Static(int a, int b) =>
            a * b;
    }

    #endregion Test Types

    [TestMethod]
    public void AComparerFollowsAGenericOrderingWhenItIsTheOnlyOne()
    {
        var comparer = Components.FastComparer.Get(typeof(OrderedOnlyGenerically));
        Assert.IsTrue(comparer.Compare(new OrderedOnlyGenerically(1), new OrderedOnlyGenerically(2)) > 0);
        Assert.IsTrue(comparer.Compare(new OrderedOnlyGenerically(2), new OrderedOnlyGenerically(1)) < 0);
        Assert.AreEqual(0, comparer.Compare(new OrderedOnlyGenerically(3), new OrderedOnlyGenerically(3)));
    }

    [TestMethod]
    public void AComparerPrefersTheGenericOrdering()
    {
        var comparer = Components.FastComparer.Get(typeof(OrderedTwoWays));
        Assert.IsTrue(comparer.Compare(new OrderedTwoWays(1), new OrderedTwoWays(2)) > 0);
    }

    [TestMethod]
    public void AComparerOrdersNullableValuesWithNullFirst()
    {
        var comparer = Components.FastComparer.Get(typeof(int?));
        Assert.IsTrue(comparer.Compare(null, 1) < 0);
        Assert.IsTrue(comparer.Compare(1, null) > 0);
        Assert.AreEqual(0, comparer.Compare(null, null));
        Assert.IsTrue(comparer.Compare(1, 2) < 0);
    }

    [TestMethod]
    public void AComparerOrdersEnumerationsByValue()
    {
        var comparer = Components.FastComparer.Get(typeof(Shade));
        Assert.IsTrue(comparer.Compare(Shade.Light, Shade.Dark) < 0);
    }

    [TestMethod]
    public void AnEqualityComparerPrefersTheGenericEquality()
    {
        var comparer = Components.FastEqualityComparer.Get(typeof(EquatableByParity));
        Assert.IsTrue(comparer.Equals(new EquatableByParity(1), new EquatableByParity(3)));
        Assert.IsFalse(comparer.Equals(new EquatableByParity(1), new EquatableByParity(2)));
        Assert.AreEqual(1, comparer.GetHashCode(new EquatableByParity(3)));
    }

    [TestMethod]
    public void AnEqualityComparerComparesNullableValues()
    {
        var comparer = Components.FastEqualityComparer.Get(typeof(int?));
        Assert.IsTrue(comparer.Equals(null, null));
        Assert.IsFalse(comparer.Equals(null, 1));
        Assert.IsTrue(comparer.Equals(1, 1));
    }

    [TestMethod]
    public void DefaultsOfValueTypes()
    {
        Assert.AreEqual(0, typeof(int).FastDefault());
        Assert.AreEqual(default(DateTime), typeof(DateTime).FastDefault());
        Assert.AreEqual((Shade)0, typeof(Shade).FastDefault());
        Assert.IsNull(typeof(int?).FastDefault());
        var point = (Point)typeof(Point).FastDefault()!;
        Assert.AreEqual(0, point.X);
        Assert.IsNull(point.Label);
    }

    [TestMethod]
    public void ConstructorsOfEveryArity()
    {
        var type = typeof(Subject);
        Assert.AreEqual(0, ((Subject)type.GetConstructor(Type.EmptyTypes)!.FastInvoke()!).Total);
        Assert.AreEqual(1, ((Subject)type.GetConstructor([typeof(int)])!.FastInvoke(1)!).Total);
        Assert.AreEqual(3, ((Subject)type.GetConstructor([typeof(int), typeof(int)])!.FastInvoke(1, 2)!).Total);
        Assert.AreEqual(6, ((Subject)type.GetConstructor([typeof(int), typeof(int), typeof(int)])!.FastInvoke(1, 2, 3)!).Total);
        Assert.AreEqual(new DateTime(2026, 9, 30), typeof(DateTime).GetConstructor([typeof(int), typeof(int), typeof(int)])!.FastInvoke(2026, 9, 30));
    }

    [TestMethod]
    public void MethodsOfEveryArity()
    {
        var subject = new Subject { Total = 10 };
        var type = typeof(Subject);
        Assert.AreEqual(10, type.GetMethod(nameof(Subject.None))!.FastInvoke(subject));
        Assert.AreEqual(11, type.GetMethod(nameof(Subject.One))!.FastInvoke(subject, 1));
        Assert.AreEqual(13, type.GetMethod(nameof(Subject.Two))!.FastInvoke(subject, 1, 2));
        Assert.AreEqual(16, type.GetMethod(nameof(Subject.Three))!.FastInvoke(subject, 1, 2, 3));
        Assert.AreEqual(6, type.GetMethod(nameof(Subject.Static))!.FastInvoke(null, 2, 3));
    }

    [TestMethod]
    public void MethodsThroughAnInvokerOfEveryArity()
    {
        var subject = new Subject { Total = 10 };
        var type = typeof(Subject);
        Assert.AreEqual(10, type.GetMethod(nameof(Subject.None))!.GetFastInvoker().Invoke(subject));
        Assert.AreEqual(11, type.GetMethod(nameof(Subject.One))!.GetFastInvoker().Invoke(subject, 1));
        Assert.AreEqual(13, type.GetMethod(nameof(Subject.Two))!.GetFastInvoker().Invoke(subject, 1, 2));
        Assert.AreEqual(16, type.GetMethod(nameof(Subject.Three))!.GetFastInvoker().Invoke(subject, 1, 2, 3));
        Assert.AreEqual(3, type.GetConstructor([typeof(int), typeof(int)])!.GetFastInvoker().Invoke(null, 1, 2) is Subject constructed ? constructed.Total : -1);
    }

    [TestMethod]
    public void PropertiesAndIndexers()
    {
        var subject = new Subject { Total = 4 };
        var total = typeof(Subject).GetProperty(nameof(Subject.Total))!;
        total.FastSetValue(subject, 5);
        Assert.AreEqual(5, total.FastGetValue(subject));
        Assert.AreEqual(15, typeof(Subject).GetProperty("Item")!.FastGetValue(subject, 3));
    }

    [TestMethod]
    public void AnInvokerRefusesTheWrongNumberOfArguments()
    {
        var subject = new Subject { Total = 10 };
        var type = typeof(Subject);
        Assert.ThrowsException<ArgumentException>(() => type.GetMethod(nameof(Subject.One))!.GetFastInvoker().Invoke(subject));
        Assert.ThrowsException<ArgumentException>(() => type.GetMethod(nameof(Subject.None))!.GetFastInvoker().Invoke(subject, 1));
        Assert.ThrowsException<ArgumentException>(() => type.GetMethod(nameof(Subject.Three))!.GetFastInvoker().Invoke(subject, 1, 2));
    }

    [TestMethod]
    public void AFaultIsThrownAsItself()
    {
        var subject = new Subject { Total = 1 };
        var divide = typeof(Subject).GetMethod(nameof(Subject.Divide))!;
        Assert.ThrowsException<DivideByZeroException>(() => divide.FastInvoke(subject, 0));
        Assert.ThrowsException<DivideByZeroException>(() => divide.GetFastInvoker().Invoke(subject, 0));
    }
}
