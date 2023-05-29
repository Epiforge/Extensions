namespace Epiforge.Extensions.Components.Tests;

#pragma warning disable CS0618

[TestClass]
public class HashCode
{
    [TestMethod]
    public void Add()
    {
        var hashCode1 = new Components.HashCode();
        var hashCode2 = new Components.HashCode();
        hashCode1.Add(1);
        hashCode2.Add(1);
        hashCode1.Add(2);
        hashCode2.Add(2);
        Assert.AreEqual(hashCode1.ToHashCode(), hashCode2.ToHashCode());
    }

    [TestMethod]
    public void Combine()
    {
        var hash1 = Components.HashCode.Combine(1);
        var hash2 = Components.HashCode.Combine(1);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine2()
    {
        var hash1 = Components.HashCode.Combine(1, 2);
        var hash2 = Components.HashCode.Combine(1, 2);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine3()
    {
        var hash1 = Components.HashCode.Combine(1, 2, 3);
        var hash2 = Components.HashCode.Combine(1, 2, 3);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine4()
    {
        var hash1 = Components.HashCode.Combine(1, 2, 3, 4);
        var hash2 = Components.HashCode.Combine(1, 2, 3, 4);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine5()
    {
        var hash1 = Components.HashCode.Combine(1, 2, 3, 4, 5);
        var hash2 = Components.HashCode.Combine(1, 2, 3, 4, 5);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine6()
    {
        var hash1 = Components.HashCode.Combine(1, 2, 3, 4, 5, 6);
        var hash2 = Components.HashCode.Combine(1, 2, 3, 4, 5, 6);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine7()
    {
        var hash1 = Components.HashCode.Combine(1, 2, 3, 4, 5, 6, 7);
        var hash2 = Components.HashCode.Combine(1, 2, 3, 4, 5, 6, 7);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void Combine8()
    {
        var hash1 = Components.HashCode.Combine(1, 2, 3, 4, 5, 6, 7, 8);
        var hash2 = Components.HashCode.Combine(1, 2, 3, 4, 5, 6, 7, 8);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void Equals()
    {
        var hashCode1 = new Components.HashCode();
        var hashCode2 = new Components.HashCode();
        hashCode1.Equals(hashCode2);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public new void GetHashCode()
    {
        var hashCode1 = new Components.HashCode();
        hashCode1.GetHashCode();
    }
}
