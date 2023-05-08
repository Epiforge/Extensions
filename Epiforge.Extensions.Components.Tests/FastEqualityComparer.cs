namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class FastEqualityComparer
{
    [TestMethod]
    public void Cache()
    {
        var fastEqualityComparer1 = Components.FastEqualityComparer.Get(typeof(string));
        var fastEqualityComparer2 = Components.FastEqualityComparer.Get(typeof(string));
        Assert.AreSame(fastEqualityComparer1, fastEqualityComparer2);
    }

    [TestMethod]
    public void Equals()
    {
        var fastEqualityComparer = new Components.FastEqualityComparer(typeof(string));
        Assert.IsTrue(fastEqualityComparer.Equals("a", "a"));
        Assert.IsFalse(fastEqualityComparer.Equals("a", "b"));
    }

    [TestMethod]
    public new void GetHashCode()
    {
        var fastEqualityComparer = new Components.FastEqualityComparer(typeof(string));
        Assert.AreEqual("a".GetHashCode(), fastEqualityComparer.GetHashCode("a"));
        Assert.AreNotEqual("a".GetHashCode(), fastEqualityComparer.GetHashCode("b"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullType() =>
        new Components.FastEqualityComparer(null!);

    [TestMethod]
    public void TypePropertyIsCorrect()
    {
        var fastEqualityComparer = new Components.FastEqualityComparer(typeof(string));
        Assert.AreEqual(typeof(string), fastEqualityComparer.Type);
    }
}
