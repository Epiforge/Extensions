namespace Epiforge.Extensions.Collections.Tests.Specialized;

[TestClass]
public class EquatableList
{
    [TestMethod]
    public void Count() =>
        Assert.AreEqual(5, new EquatableList<int>(Enumerable.Range(0, 5)).Count);

    [TestMethod]
    public void Equality()
    {
        var list1 = new EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = new EquatableList<int>(Enumerable.Range(0, 5));
        Assert.IsTrue(list1 == list2);
    }

    [TestMethod]
    public void Equals()
    {
        var list1 = new EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = new EquatableList<int>(Enumerable.Range(0, 5));
        Assert.AreEqual(list1, list2);
    }

    [TestMethod]
    public void EqualsWithEqualityComparer()
    {
        var list1 = new EquatableList<string>(new string[] { "a", "b", "c" }, StringComparer.OrdinalIgnoreCase);
        var list2 = new EquatableList<string>(new string[] { "A", "B", "C" }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(list1, list2);
    }

    [TestMethod]
    public void GetEnumerator()
    {
        var sum = 0;
        foreach (int item in (IEnumerable)new EquatableList<int>(Enumerable.Range(0, 5)))
            sum += item;
        Assert.AreEqual(10, sum);
    }

    [TestMethod]
    public void GetEnumeratorGeneric()
    {
        var sum = 0;
        foreach (var item in new EquatableList<int>(Enumerable.Range(0, 5)))
            sum += item;
        Assert.AreEqual(10, sum);
    }

    [TestMethod]
    public void GetHashCodeTest()
    {
        var list1 = new EquatableList<string>(new string[] { "a", "b", "c" }, StringComparer.OrdinalIgnoreCase);
        var list2 = new EquatableList<string>(new string[] { "A", "B", "C" }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(list1.GetHashCode(), list2.GetHashCode());
    }

    [TestMethod]
    public void Index() =>
        Assert.AreEqual(1, new EquatableList<int>(Enumerable.Range(0, 5))[1]);

    [TestMethod]
    public void Inequality()
    {
        var list1 = new EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = new EquatableList<int>(Enumerable.Range(0, 5));
        Assert.IsFalse(list1 != list2);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullEqualityComparer() =>
        new EquatableList<string>(new string[0], null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullSequence() =>
        new EquatableList<int>(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullSequence2() =>
        new EquatableList<string>(null!, StringComparer.OrdinalIgnoreCase);

    [TestMethod]
    public void ObjectEquals()
    {
        var list1 = (object)new EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = (object)new EquatableList<int>(Enumerable.Range(0, 5));
        Assert.AreEqual(list1, list2);
    }
}
