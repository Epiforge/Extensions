namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class GenericArithmetic
{
    [TestMethod]
    public void Addition() =>
        Assert.AreEqual(new DateTimeOffset(2000, 1, 1, 2, 0, 0, TimeSpan.Zero), GenericAddition<DateTimeOffset, TimeSpan, DateTimeOffset>.Instance(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(2)));

    [TestMethod]
    public void Division() =>
        Assert.AreEqual(2, GenericDivision<int, int, int>.Instance(5, 2));

    [TestMethod]
    public void Multiplication() =>
        Assert.AreEqual(27.65, Math.Round(GenericMultiplication<double, double, double>.Instance(3.5, 7.9), 2));

    [TestMethod]
    public void Subtraction() =>
        Assert.AreEqual(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), GenericSubtraction<DateTimeOffset, TimeSpan, DateTimeOffset>.Instance(new DateTimeOffset(2000, 1, 1, 2, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(2)));
}
