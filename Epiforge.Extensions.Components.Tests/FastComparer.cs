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
    public void CompareNullReferences()
    {
        var comparer = new Components.FastComparer(typeof(string));
        Assert.AreEqual(0, comparer.Compare(null, null));
        Assert.IsTrue(comparer.Compare(null, "a") < 0);
        Assert.IsTrue(comparer.Compare("a", null) > 0);
    }

    [TestMethod]
    public void CompareReferenceType()
    {
        var comparer = new Components.FastComparer(typeof(string));
        Assert.AreEqual(0, comparer.Compare("a", "a"));
        Assert.IsTrue(comparer.Compare("a", "b") < 0);
        Assert.IsTrue(comparer.Compare("b", "a") > 0);
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
