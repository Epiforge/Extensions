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

    public enum WideShade :
        long
    {
        Light = 1
    }

    public enum UnsignedShade :
        uint
    {
        Light = 1
    }

    public enum Tone
    {
        Low = 1
    }

    public interface ICounted
    {
        int Count();
    }

    public struct Tally
    {
        public int Value;

        public readonly int Read() =>
            Value;
    }

    public abstract class Incomplete
    {
        public Incomplete()
        {
        }
    }

    public sealed class Receiver :
        ICounted
    {
        int refReturned;

        public Receiver()
        {
        }

        public Receiver(int value) =>
            Value = value;

        public int Value { get; set; }

        public int Count() =>
            3;

        public T Echo<T>(T value) =>
            value;

        public int Int(int value) =>
            value;

        public long Long(long value) =>
            value;

        public double Double(double value) =>
            value;

        public int? NullableInt(int? value) =>
            value;

        public Shade Shade(Shade value) =>
            value;

        public string? Text(string? value) =>
            value;

        public int Sequence(IEnumerable<int> values) =>
            values.Count();

        public int Objects(object?[] values) =>
            values.Length;

        public int Three(int a, int b, int c) =>
            a + b + c;

        public void Out(out int value) =>
            value = 1;

        public void Ref(ref int value) =>
            ++value;

        public ref int RefReturn() =>
            ref refReturned;

        public int SpanLength(Span<int> values) =>
            values.Length;

        public Span<int> EmptySpan() =>
            [];
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

    static void InvokingThrows<TException>(MethodInfo method, object? instance, params object?[] arguments)
        where TException : Exception
    {
        Assert.ThrowsException<TException>(() => method.FastInvoke(instance, arguments));
        var invoker = method.GetFastInvoker();
        Assert.ThrowsException<TException>(() => invoker.Invoke(instance, arguments));
        if (arguments.Length == 0)
            Assert.ThrowsException<TException>(() => invoker.Invoke(instance));
        else if (arguments.Length == 1)
            Assert.ThrowsException<TException>(() => invoker.Invoke(instance, arguments[0]));
        else if (arguments.Length == 2)
            Assert.ThrowsException<TException>(() => invoker.Invoke(instance, arguments[0], arguments[1]));
    }

    static object? InvokingReturns(MethodInfo method, object? instance, params object?[] arguments)
    {
        var result = method.FastInvoke(instance, arguments);
        var invoker = method.GetFastInvoker();
        Assert.AreEqual(result, invoker.Invoke(instance, arguments));
        if (arguments.Length == 0)
            Assert.AreEqual(result, invoker.Invoke(instance));
        else if (arguments.Length == 1)
            Assert.AreEqual(result, invoker.Invoke(instance, arguments[0]));
        return result;
    }

    static MethodInfo ReceiverMethod(string name) =>
        typeof(Receiver).GetMethod(name)!;

    [TestMethod]
    public void AnArgumentIsRefusedWhereACastWouldRefuseIt()
    {
        var receiver = new Receiver();
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, "1");
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, 1L);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, (short)1);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, 'a');
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, 1u);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, true);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), receiver, UnsignedShade.Light);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Long)), receiver, 1);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Double)), receiver, 1);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Shade)), receiver, 1L);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Shade)), receiver, WideShade.Light);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.NullableInt)), receiver, 1L);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.NullableInt)), receiver, Shade.Light);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Text)), receiver, 1);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Text)), receiver, new object());
        var constructor = typeof(Receiver).GetConstructor([typeof(int)])!;
        Assert.ThrowsException<InvalidCastException>(() => constructor.FastInvoke(1L));
        Assert.ThrowsException<InvalidCastException>(() => constructor.GetFastInvoker().Invoke(null, (short)1));
    }

    [TestMethod]
    public void AnArgumentIsConvertedWhereACastWouldConvertIt()
    {
        var receiver = new Receiver();
        Assert.AreEqual(1, InvokingReturns(ReceiverMethod(nameof(Receiver.Int)), receiver, Shade.Light));
        Assert.AreEqual(Shade.Light, InvokingReturns(ReceiverMethod(nameof(Receiver.Shade)), receiver, 1));
        Assert.AreEqual(Shade.Light, InvokingReturns(ReceiverMethod(nameof(Receiver.Shade)), receiver, Tone.Low));
        Assert.AreEqual(1, InvokingReturns(ReceiverMethod(nameof(Receiver.NullableInt)), receiver, 1));
        Assert.IsNull(InvokingReturns(ReceiverMethod(nameof(Receiver.NullableInt)), receiver, [null]));
        Assert.IsNull(InvokingReturns(ReceiverMethod(nameof(Receiver.Text)), receiver, [null]));
        Assert.AreEqual(2, InvokingReturns(ReceiverMethod(nameof(Receiver.Sequence)), receiver, new[] { 1, 2 }));
        Assert.AreEqual(1, InvokingReturns(ReceiverMethod(nameof(Receiver.Objects)), receiver, new object?[] { new[] { "a" } }));
        Assert.AreEqual(0, typeof(int).GetMethod(nameof(int.CompareTo), [typeof(int)])!.FastInvoke(Shade.Light, 1));
    }

    [TestMethod]
    public void NullIsRefusedForAValueTypeParameter()
    {
        var receiver = new Receiver();
        InvokingThrows<NullReferenceException>(ReceiverMethod(nameof(Receiver.Int)), receiver, [null]);
        InvokingThrows<NullReferenceException>(ReceiverMethod(nameof(Receiver.Shade)), receiver, [null]);
        var constructor = typeof(Receiver).GetConstructor([typeof(int)])!;
        Assert.ThrowsException<NullReferenceException>(() => constructor.FastInvoke([null]));
        Assert.ThrowsException<NullReferenceException>(() => constructor.GetFastInvoker().Invoke(null, (object?)null));
    }

    [TestMethod]
    public void AnInstanceIsRefusedWhereACastWouldRefuseIt()
    {
        InvokingThrows<NullReferenceException>(ReceiverMethod(nameof(Receiver.Int)), null, 1);
        InvokingThrows<InvalidCastException>(ReceiverMethod(nameof(Receiver.Int)), "receiver", 1);
        InvokingThrows<InvalidCastException>(typeof(ICounted).GetMethod(nameof(ICounted.Count))!, "receiver");
        InvokingThrows<NullReferenceException>(typeof(Tally).GetMethod(nameof(Tally.Read))!, null);
        InvokingThrows<InvalidCastException>(typeof(Tally).GetMethod(nameof(Tally.Read))!, 1);
        Assert.AreEqual(3, InvokingReturns(typeof(ICounted).GetMethod(nameof(ICounted.Count))!, new Receiver()));
        Assert.AreEqual(2, InvokingReturns(typeof(Tally).GetMethod(nameof(Tally.Read))!, new Tally { Value = 2 }));
        Assert.AreEqual(6, InvokingReturns(typeof(Subject).GetMethod(nameof(Subject.Static))!, "ignored", 2, 3));
    }

    [TestMethod]
    public void InvokingRefusesTheWrongNumberOfArguments()
    {
        var receiver = new Receiver();
        var three = ReceiverMethod(nameof(Receiver.Three));
        Assert.ThrowsException<ArgumentException>(() => three.FastInvoke(receiver, 1, 2));
        Assert.ThrowsException<ArgumentException>(() => three.FastInvoke(receiver, 1, 2, 3, 4));
        Assert.ThrowsException<ArgumentException>(() => ReceiverMethod(nameof(Receiver.Int)).FastInvoke(receiver, 1, 2));
        Assert.ThrowsException<ArgumentException>(() => ReceiverMethod(nameof(Receiver.Int)).FastInvoke(receiver, null!));
        Assert.ThrowsException<ArgumentException>(() => ReceiverMethod(nameof(Receiver.Count)).FastInvoke(receiver, 1));
        Assert.AreEqual(3, ReceiverMethod(nameof(Receiver.Count)).FastInvoke(receiver, null!));
        var constructor = typeof(Receiver).GetConstructor([typeof(int)])!;
        Assert.ThrowsException<ArgumentException>(() => constructor.FastInvoke());
        Assert.ThrowsException<ArgumentException>(() => constructor.FastInvoke(1, 2));
    }

    [TestMethod]
    public void AMemberWhichCannotTakeOrReturnObjectsIsRefused()
    {
        var receiver = new Receiver();
        InvokingThrows<NotSupportedException>(ReceiverMethod(nameof(Receiver.Out)), receiver, 1);
        InvokingThrows<NotSupportedException>(ReceiverMethod(nameof(Receiver.Ref)), receiver, 1);
        InvokingThrows<NotSupportedException>(ReceiverMethod(nameof(Receiver.RefReturn)), receiver);
        InvokingThrows<NotSupportedException>(ReceiverMethod(nameof(Receiver.SpanLength)), receiver, [null]);
        InvokingThrows<NotSupportedException>(ReceiverMethod(nameof(Receiver.EmptySpan)), receiver);
        InvokingThrows<NotSupportedException>(typeof(Buffer).GetMethods().Single(method => method.Name == nameof(Buffer.MemoryCopy) && method.GetParameters()[2].ParameterType == typeof(long)), null, null, null, 0L, 0L);
        InvokingThrows<NotSupportedException>(typeof(NativeMemory).GetMethod(nameof(NativeMemory.Alloc), [typeof(nuint)])!, null, (nuint)1);
        InvokingThrows<InvalidOperationException>(ReceiverMethod(nameof(Receiver.Echo)), receiver, 1);
        Assert.ThrowsException<InvalidOperationException>(() => typeof(List<>).GetConstructor(Type.EmptyTypes)!.FastInvoke());
        Assert.ThrowsException<InvalidOperationException>(() => typeof(Incomplete).GetConstructor(Type.EmptyTypes)!.FastInvoke());
        Assert.ThrowsException<InvalidOperationException>(() => typeof(Incomplete).GetConstructor(Type.EmptyTypes)!.GetFastInvoker().Invoke(null));
        Assert.AreEqual(1, InvokingReturns(ReceiverMethod(nameof(Receiver.Echo)).MakeGenericMethod(typeof(int)), receiver, 1));
    }

    [TestMethod]
    public void DefaultsOfTypesWhichCannotBeBoxedAreRefused()
    {
        Assert.ThrowsException<ArgumentException>(() => typeof(void).FastDefault());
        Assert.ThrowsException<ArgumentException>(() => typeof(Span<int>).FastDefault());
        Assert.ThrowsException<ArgumentException>(() => typeof(TypedReference).FastDefault());
        Assert.ThrowsException<ArgumentException>(() => typeof(KeyValuePair<,>).FastDefault());
        Assert.IsNull(typeof(int).MakePointerType().FastDefault());
        Assert.IsNull(typeof(int).MakeByRefType().FastDefault());
        Assert.IsNull(typeof(List<>).FastDefault());
    }

    [TestMethod]
    public void AComparerRefusesAValueWhereACastWouldRefuseIt()
    {
        var integers = Components.FastComparer.Get(typeof(int));
        Assert.ThrowsException<InvalidCastException>(() => integers.Compare(1, "2"));
        Assert.ThrowsException<InvalidCastException>(() => integers.Compare("1", 2));
        Assert.ThrowsException<InvalidCastException>(() => integers.Compare(1, 2L));
        Assert.ThrowsException<InvalidCastException>(() => integers.Compare(1, (short)2));
        Assert.ThrowsException<NullReferenceException>(() => integers.Compare(null, 2));
        Assert.ThrowsException<NullReferenceException>(() => integers.Compare(1, null));
        var texts = Components.FastComparer.Get(typeof(string));
        Assert.ThrowsException<InvalidCastException>(() => texts.Compare("a", 1));
        Assert.ThrowsException<InvalidCastException>(() => texts.Compare(1, "a"));
        Assert.AreEqual(0, integers.Compare(1, Shade.Light));
        Assert.AreEqual(0, Components.FastComparer.Get(typeof(Shade)).Compare(Shade.Light, 1));
        Assert.IsTrue(Components.FastComparer.Get(typeof(Shade)).Compare(Shade.Dark, Tone.Low) > 0);
    }

    [TestMethod]
    public void AnEqualityComparerRefusesAValueWhereACastWouldRefuseIt()
    {
        var integers = Components.FastEqualityComparer.Get(typeof(int));
        Assert.ThrowsException<InvalidCastException>(() => integers.Equals(1, 1L));
        Assert.ThrowsException<InvalidCastException>(() => integers.Equals(1, "1"));
        Assert.ThrowsException<InvalidCastException>(() => integers.Equals("1", 1));
        Assert.ThrowsException<NullReferenceException>(() => integers.Equals(1, null));
        Assert.ThrowsException<NullReferenceException>(() => integers.Equals(null, 1));
        Assert.ThrowsException<InvalidCastException>(() => integers.GetHashCode("1"));
        Assert.ThrowsException<InvalidCastException>(() => integers.GetHashCode(1L));
        Assert.ThrowsException<NullReferenceException>(() => integers.GetHashCode(null!));
        var texts = Components.FastEqualityComparer.Get(typeof(string));
        Assert.ThrowsException<InvalidCastException>(() => texts.Equals("a", 1));
        Assert.ThrowsException<InvalidCastException>(() => texts.Equals(1, "a"));
        Assert.ThrowsException<InvalidCastException>(() => texts.GetHashCode(1));
        Assert.IsTrue(integers.Equals(1, Shade.Light));
        Assert.AreEqual(0, texts.GetHashCode(null!));
        Assert.AreEqual(0, Components.FastEqualityComparer.Get(typeof(object)).GetHashCode(null!));
        Assert.IsFalse(Components.FastEqualityComparer.Get(typeof(Shade)).Equals(Shade.Light, 1));
        Assert.IsFalse(Components.FastEqualityComparer.Get(typeof(Point)).Equals(new Point(), 1));
    }

    [TestMethod]
    public void AnEqualityComparerOfBoxedValuesHashesNullAsZero()
    {
        var nullables = Components.FastEqualityComparer.Get(typeof(int?));
        Assert.AreEqual(0, nullables.GetHashCode(null!));
        Assert.AreEqual(1.GetHashCode(), nullables.GetHashCode(1));
        Assert.IsTrue(nullables.Equals(null, null));
        Assert.IsFalse(nullables.Equals(1, 1L));
        Assert.AreEqual(0, Components.FastEqualityComparer.Get(typeof(Point)).GetHashCode(null!));
    }

    [TestMethod]
    public void PropertiesTreatArgumentsAsReflectionDoes()
    {
        var subject = new Subject { Total = 4 };
        var total = typeof(Subject).GetProperty(nameof(Subject.Total))!;
        total.FastSetValue(subject, null);
        Assert.AreEqual(0, subject.Total);
        Assert.ThrowsException<ArgumentException>(() => total.FastSetValue(subject, 1L));
        Assert.ThrowsException<TargetException>(() => total.FastGetValue(null));
        Assert.ThrowsException<TargetException>(() => total.FastSetValue(null, 1));
    }
}
