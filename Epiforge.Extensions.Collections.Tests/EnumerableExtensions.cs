namespace Epiforge.Extensions.Collections.Tests;

[TestClass]
public class EnumerableExtensions
{
    [TestMethod]
    public void FindIndexWithArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var index = array.FindIndex(i => i == 3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void FindIndexWithEnumerable()
    {
        var enumerable = Enumerable.Range(1, 5);
        var index = enumerable.FindIndex(i => i == 3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void FindIndexWithEnumerableNotFound()
    {
        var enumerable = Enumerable.Range(1, 5);
        var index = enumerable.FindIndex(i => i == 6);
        Assert.AreEqual(-1, index);
    }

    [TestMethod]
    public void FindIndexWithListAsEnumerable()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var index = ((IEnumerable<int>)list).FindIndex(i => i == 3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void FindIndexWithNullSource() =>
        ((IEnumerable<int>)null!).FindIndex(i => i == 3);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void FindIndexWithNullPredicate() =>
        Enumerable.Range(1, 5).FindIndex(null!);

    [TestMethod]
    public void FindLastIndexWithArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var index = array.FindLastIndex(i => i == 3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void FindLastIndexWithEnumerable()
    {
        var enumerable = Enumerable.Range(1, 5);
        var index = enumerable.FindLastIndex(i => i == 3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void FindLastIndexWithEnumerableNotFound()
    {
        var enumerable = Enumerable.Range(1, 5);
        var index = enumerable.FindLastIndex(i => i == 6);
        Assert.AreEqual(-1, index);
    }

    [TestMethod]
    public void FindLastIndexWithListAsEnumerable()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var index = ((IEnumerable<int>)list).FindLastIndex(i => i == 3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void FindLastIndexWithNullSource() =>
        ((IEnumerable<int>)null!).FindLastIndex(i => i == 3);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void FindLastIndexWithNullPredicate() =>
        Enumerable.Range(1, 5).FindLastIndex(null!);

    [TestMethod]
    public void FindIndiciesWithArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var indicies = array.FindIndicies(i => i % 2 == 0).ToList();
        Assert.AreEqual(2, indicies.Count);
        Assert.AreEqual(1, indicies[0]);
        Assert.AreEqual(3, indicies[1]);
    }

    [TestMethod]
    public void FindIndiciesWithEnumerable()
    {
        var enumerable = Enumerable.Range(1, 5);
        var indicies = enumerable.FindIndicies(i => i % 2 == 0).ToList();
        Assert.AreEqual(2, indicies.Count);
        Assert.AreEqual(1, indicies[0]);
        Assert.AreEqual(3, indicies[1]);
    }

    [TestMethod]
    public void FindIndiciesWithList()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var indicies = list.FindIndicies(i => i % 2 == 0).ToList();
        Assert.AreEqual(2, indicies.Count);
        Assert.AreEqual(1, indicies[0]);
        Assert.AreEqual(3, indicies[1]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void FindIndiciesWithNullSource() =>
        ((IEnumerable<int>)null!).FindIndicies(i => i % 2 == 0);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void FindIndiciesWithNullPredicate() =>
        Enumerable.Range(1, 5).FindIndicies(null!);

    [TestMethod]
    public void IndexOfWithArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var index = array.IndexOf(3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void IndexOfWithEnumerable()
    {
        var enumerable = Enumerable.Range(1, 5);
        var index = enumerable.IndexOf(3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void IndexOfWithListAsEnumerable()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var index = ((IEnumerable<int>)list).IndexOf(3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IndexOfWithNullSource() =>
        ((IEnumerable<int>)null!).IndexOf(3);

    [TestMethod]
    public void LastIndexOfWithArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var index = array.LastIndexOf(3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void LastIndexOfWithEnumerable()
    {
        var enumerable = Enumerable.Range(1, 5);
        var index = enumerable.LastIndexOf(3);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void LastIndexOfWithNullSource() =>
        ((IEnumerable<int>)null!).LastIndexOf(3);

    [TestMethod]
    public void IndiciesOfWithArray()
    {
        var array = new[] { 1, 2, 3, 4, 5 };
        var indicies = array.IndiciesOf(3).ToList();
        Assert.AreEqual(1, indicies.Count);
        Assert.AreEqual(2, indicies[0]);
    }

    [TestMethod]
    public void IndiciesOfWithEnumerable()
    {
        var enumerable = Enumerable.Range(1, 5);
        var indicies = enumerable.IndiciesOf(3).ToList();
        Assert.AreEqual(1, indicies.Count);
        Assert.AreEqual(2, indicies[0]);
    }

    [TestMethod]
    public void IndiciesOfWithList()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var indicies = list.IndiciesOf(3).ToList();
        Assert.AreEqual(1, indicies.Count);
        Assert.AreEqual(2, indicies[0]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IndiciesOfWithNullSource() =>
        ((IEnumerable<int>)null!).IndiciesOf(3);
}
