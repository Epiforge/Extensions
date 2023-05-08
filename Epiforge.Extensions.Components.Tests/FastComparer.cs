namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class FastComparer
{
    [TestMethod]
    public void Cache()
    {
        var comparer1 = Components.FastComparer.Get(typeof(int));
        var comparer2 = Components.FastComparer.Get(typeof(int));
        Assert.AreSame(comparer1, comparer2);
    }

    [TestMethod]
    public void Compare()
    {
        var comparer = new Components.FastComparer(typeof(int));
        Assert.AreEqual(0, comparer.Compare(1, 1));
        Assert.AreEqual(-1, comparer.Compare(1, 2));
        Assert.AreEqual(1, comparer.Compare(2, 1));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullType() =>
        new Components.FastComparer(null!);

    [TestMethod]
    public void TypePropertyIsCorrect()
    {
        var comparer = new Components.FastComparer(typeof(int));
        Assert.AreEqual(typeof(int), comparer.Type);
    }
}
