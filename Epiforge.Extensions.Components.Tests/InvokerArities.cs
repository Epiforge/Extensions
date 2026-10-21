namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class InvokerArities
{
    public sealed class Subject
    {
        public Subject()
        {
        }

        public Subject(int first, int second, int third) =>
            Total = first + second + third;

        public Subject(int first, int second, int third, int fourth) =>
            Total = first + second + third + fourth;

        public int Total { get; }

        public int Four(int first, int second, int third, int fourth) =>
            Total + first + second + third + fourth;

        public int Three(int first, int second, int third) =>
            Total + first + second + third;

        public static string Joined(string first, string second, string third, string fourth) =>
            string.Concat(first, second, third, fourth);

        public static int Quotient(int first, int second, int third) =>
            first / second / third;
    }

    [TestMethod]
    public void AnInvokerTakesThreeAndFourArgumentsDirectly()
    {
        var subject = new Subject(1, 2, 3);
        Assert.AreEqual(12, typeof(Subject).GetMethod(nameof(Subject.Three))!.GetFastInvoker().Invoke(subject, 1, 2, 3));
        Assert.AreEqual(16, typeof(Subject).GetMethod(nameof(Subject.Four))!.GetFastInvoker().Invoke(subject, 1, 2, 3, 4));
        Assert.AreEqual("abcd", typeof(Subject).GetMethod(nameof(Subject.Joined))!.GetFastInvoker().Invoke(null, "a", "b", "c", "d"));
        Assert.AreEqual(6, ((Subject)typeof(Subject).GetConstructor([typeof(int), typeof(int), typeof(int)])!.GetFastInvoker().Invoke(null, 1, 2, 3)!).Total);
        Assert.AreEqual(10, ((Subject)typeof(Subject).GetConstructor([typeof(int), typeof(int), typeof(int), typeof(int)])!.GetFastInvoker().Invoke(null, 1, 2, 3, 4)!).Total);
    }

    [TestMethod]
    public void AnInvokerTakesThreeAndFourArgumentsInAnArray()
    {
        var subject = new Subject(1, 2, 3);
        Assert.AreEqual(12, typeof(Subject).GetMethod(nameof(Subject.Three))!.GetFastInvoker().Invoke(subject, new object?[] { 1, 2, 3 }));
        Assert.AreEqual(16, typeof(Subject).GetMethod(nameof(Subject.Four))!.GetFastInvoker().Invoke(subject, new object?[] { 1, 2, 3, 4 }));
        Assert.AreEqual(16, typeof(Subject).GetMethod(nameof(Subject.Four))!.FastInvoke(subject, 1, 2, 3, 4));
    }

    [TestMethod]
    public void AnInvokerOfThreeOrFourRefusesTheWrongNumberOfArguments()
    {
        var subject = new Subject();
        var three = typeof(Subject).GetMethod(nameof(Subject.Three))!.GetFastInvoker();
        var four = typeof(Subject).GetMethod(nameof(Subject.Four))!.GetFastInvoker();
        Assert.ThrowsExactly<ArgumentException>(() => three.Invoke(subject, 1, 2));
        Assert.ThrowsExactly<ArgumentException>(() => three.Invoke(subject, 1, 2, 3, 4));
        Assert.ThrowsExactly<ArgumentException>(() => four.Invoke(subject, 1, 2, 3));
        Assert.ThrowsExactly<ArgumentException>(() => four.Invoke(subject, new object?[] { 1, 2, 3, 4, 5 }));
    }

    [TestMethod]
    public void AnInvokerOfThreeOrFourCastsAndThrowsAsACallWould()
    {
        var subject = new Subject();
        Assert.ThrowsExactly<DivideByZeroException>(() => typeof(Subject).GetMethod(nameof(Subject.Quotient))!.GetFastInvoker().Invoke(null, 6, 0, 1));
        Assert.ThrowsExactly<InvalidCastException>(() => typeof(Subject).GetMethod(nameof(Subject.Three))!.GetFastInvoker().Invoke(subject, 1, 2, "three"));
        Assert.ThrowsExactly<NullReferenceException>(() => typeof(Subject).GetMethod(nameof(Subject.Four))!.GetFastInvoker().Invoke(subject, 1, 2, 3, null));
        Assert.ThrowsExactly<NullReferenceException>(() => typeof(Subject).GetMethod(nameof(Subject.Four))!.GetFastInvoker().Invoke(null, 1, 2, 3, 4));
    }
}
