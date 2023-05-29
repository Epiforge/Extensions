namespace Epiforge.Extensions.Collections.Tests;

[TestClass]
public class EquatableList
{
    [TestMethod]
    public void Count() =>
        new Collections.EquatableList<int>(Enumerable.Range(0, 5)).Count.ToString();

    [TestMethod]
    public void Equality()
    {
        var list1 = new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        Assert.IsTrue(list1 == list2);
    }

    [TestMethod]
    public void Equals()
    {
        var list1 = new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        Assert.AreEqual(list1, list2);
    }

    [TestMethod]
    public void EqualsWithEqualityComparer()
    {
        var list1 = new Collections.EquatableList<string>(new string[] { "a", "b", "c" }, StringComparer.OrdinalIgnoreCase);
        var list2 = new Collections.EquatableList<string>(new string[] { "A", "B", "C" }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(list1, list2);
    }

    [TestMethod]
    public void GetEnumerator()
    {
        var sum = 0;
        foreach (int item in (IEnumerable)new Collections.EquatableList<int>(Enumerable.Range(0, 5)))
            sum += item;
        Assert.AreEqual(10, sum);
    }

    [TestMethod]
    public void GetEnumeratorGeneric()
    {
        var sum = 0;
        foreach (var item in new Collections.EquatableList<int>(Enumerable.Range(0, 5)))
            sum += item;
        Assert.AreEqual(10, sum);
    }

    [TestMethod]
    public new void GetHashCode()
    {
        var list1 = new Collections.EquatableList<string>(new string[] { "a", "b", "c" }, StringComparer.OrdinalIgnoreCase);
        var list2 = new Collections.EquatableList<string>(new string[] { "A", "B", "C" }, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(list1.GetHashCode(), list2.GetHashCode());
    }

    [TestMethod]
    public void Index() =>
        new Collections.EquatableList<int>(Enumerable.Range(0, 5))[1].ToString();

    [TestMethod]
    public void Inequality()
    {
        var list1 = new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        Assert.IsFalse(list1 != list2);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullEqualityComparer() =>
        new Collections.EquatableList<string>(new string[0], null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullSequence() =>
        new Collections.EquatableList<int>(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullSequence2() =>
        new Collections.EquatableList<string>(null!, StringComparer.OrdinalIgnoreCase);

    [TestMethod]
    public void ObjectEquals()
    {
        var list1 = (object)new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        var list2 = (object)new Collections.EquatableList<int>(Enumerable.Range(0, 5));
        Assert.AreEqual(list1, list2);
    }
}
