namespace Epiforge.Extensions.Collections.Tests.Specialized;

[TestClass]
public class OrderedHashSet
{
    [TestMethod]
    public void AddFirst()
    {
        var set = new OrderedHashSet<int>();
        Assert.IsTrue(set.AddFirst(1));
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void AddFirstDuplicate()
    {
        var set = new OrderedHashSet<int>();
        Assert.IsTrue(set.AddFirst(1));
        Assert.IsFalse(set.AddFirst(1));
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void AddLast()
    {
        var set = new OrderedHashSet<int>();
        Assert.IsTrue(set.AddLast(1));
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void AddLastDuplicate()
    {
        var set = new OrderedHashSet<int>();
        Assert.IsTrue(set.AddLast(1));
        Assert.IsFalse(set.AddLast(1));
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void Clear()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        set.Clear();
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void CollectionAdd()
    {
        var set = new OrderedHashSet<int>();
        ((ICollection<int>)set).Add(1);
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void Construction()
    {
        var set = new OrderedHashSet<int>();
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void ConstructionWithCapacity()
    {
        var set = new OrderedHashSet<int>(10);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void ConstructionWithCapacityAndComparer()
    {
        var set = new OrderedHashSet<int>(10, EqualityComparer<int>.Default);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void ConstructionWithComparer()
    {
        var set = new OrderedHashSet<int>(EqualityComparer<int>.Default);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void ConstructionWithCollection()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.AreEqual(3, set.Count);
        Assert.IsTrue(set.Contains(1));
        Assert.IsTrue(set.Contains(2));
        Assert.IsTrue(set.Contains(3));
    }

    [TestMethod]
    public void ConstructionWithCollectionAndComparer()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 }, EqualityComparer<int>.Default);
        Assert.AreEqual(3, set.Count);
        Assert.IsTrue(set.Contains(1));
        Assert.IsTrue(set.Contains(2));
        Assert.IsTrue(set.Contains(3));
    }

    [TestMethod]
    public void CopyTo()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        var array = new int[3];
        set.CopyTo(array);
        Assert.AreEqual(1, array[0]);
        Assert.AreEqual(2, array[1]);
        Assert.AreEqual(3, array[2]);
    }

    [TestMethod]
    public void CopyToWithArrayIndex()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        var array = new int[5];
        set.CopyTo(array, 1);
        Assert.AreEqual(0, array[0]);
        Assert.AreEqual(1, array[1]);
        Assert.AreEqual(2, array[2]);
        Assert.AreEqual(3, array[3]);
        Assert.AreEqual(0, array[4]);
    }

    [TestMethod]
    public void CopyToWithArrayIndexAndCount()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        var array = new int[5];
        set.CopyTo(array, 1, 2);
        Assert.AreEqual(0, array[0]);
        Assert.AreEqual(1, array[1]);
        Assert.AreEqual(2, array[2]);
        Assert.AreEqual(0, array[3]);
        Assert.AreEqual(0, array[4]);
    }

    [TestMethod]
    public void EnsureCapacity()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        set.EnsureCapacity(10);
        Assert.AreEqual(3, set.Count);
    }

    [TestMethod]
    public void ExceptWith()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        set.ExceptWith(new[] { 2, 3, 4 });
        Assert.AreEqual(1, set.Count);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ExceptWithNull() =>
        new OrderedHashSet<int>().ExceptWith(null!);

    [TestMethod]
    public void First()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.AreEqual(1, set.First);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void FirstEmpty() =>
        _ = new OrderedHashSet<int>().First;

    [TestMethod]
    public void GetEnumerator()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        var enumerator = set.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(1, enumerator.Current);
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(2, enumerator.Current);
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(3, enumerator.Current);
        Assert.IsFalse(enumerator.MoveNext());
    }

    [TestMethod]
    public void IntersectWith()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        set.IntersectWith(new[] { 2, 3, 4 });
        Assert.AreEqual(2, set.Count);
        Assert.IsTrue(set.Contains(2));
        Assert.IsTrue(set.Contains(3));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IntersectWithNull() =>
        new OrderedHashSet<int>().IntersectWith(null!);

    [TestMethod]
    public void IsProperSubsetOf()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.IsProperSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.IsFalse(set.IsProperSubsetOf(new[] { 1, 2, 4, 5 }));
        Assert.IsFalse(set.IsProperSubsetOf(new[] { 1, 2, 3 }));
        Assert.IsFalse(set.IsProperSubsetOf(new[] { 1, 2 }));
        Assert.IsFalse(set.IsProperSubsetOf(new[] { 1 }));
        Assert.IsFalse(set.IsProperSubsetOf(new int[0]));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IsProperSubsetOfNull() =>
        new OrderedHashSet<int>().IsProperSubsetOf(null!);

    [TestMethod]
    public void IsProperSupersetOf()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsFalse(set.IsProperSupersetOf(new[] { 1, 2, 3, 4 }));
        Assert.IsFalse(set.IsProperSupersetOf(new[] { 1, 2, 3 }));
        Assert.IsTrue(set.IsProperSupersetOf(new[] { 1, 2 }));
        Assert.IsTrue(set.IsProperSupersetOf(new[] { 1 }));
        Assert.IsTrue(set.IsProperSupersetOf(new int[0]));
        Assert.IsFalse(set.IsProperSupersetOf(new[] { 4 }));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IsProperSupersetOfNull() =>
        new OrderedHashSet<int>().IsProperSupersetOf(null!);

    [TestMethod]
    public void IsSubsetOf()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.IsSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.IsFalse(set.IsSubsetOf(new[] { 1, 2, 4, 5 }));
        Assert.IsTrue(set.IsSubsetOf(new[] { 1, 2, 3 }));
        Assert.IsFalse(set.IsSubsetOf(new[] { 1, 2 }));
        Assert.IsFalse(set.IsSubsetOf(new[] { 1 }));
        Assert.IsFalse(set.IsSubsetOf(new int[0]));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IsSubsetOfNull() =>
        new OrderedHashSet<int>().IsSubsetOf(null!);

    [TestMethod]
    public void IsSupersetOf()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsFalse(set.IsSupersetOf(new[] { 1, 2, 3, 4 }));
        Assert.IsTrue(set.IsSupersetOf(new[] { 1, 2, 3 }));
        Assert.IsTrue(set.IsSupersetOf(new[] { 1, 2 }));
        Assert.IsTrue(set.IsSupersetOf(new[] { 1 }));
        Assert.IsTrue(set.IsSupersetOf(new int[0]));
        Assert.IsFalse(set.IsSupersetOf(new[] { 4 }));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IsSupersetOfNull() =>
        new OrderedHashSet<int>().IsSupersetOf(null!);

    [TestMethod]
    public void Last()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.AreEqual(3, set.Last);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void LastEmpty() =>
        _ = new OrderedHashSet<int>().Last;

    [TestMethod]
    public void MoveToFirst()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.MoveToFirst(2));
        Assert.AreEqual(2, set.First);
    }

    [TestMethod]
    public void MoveToFirstNotFound()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsFalse(set.MoveToFirst(4));
        Assert.AreEqual(1, set.First);
    }

    [TestMethod]
    public void MoveToLast()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.MoveToLast(2));
        Assert.AreEqual(2, set.Last);
    }

    [TestMethod]
    public void MoveToLastNotFound()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsFalse(set.MoveToLast(4));
        Assert.AreEqual(3, set.Last);
    }

    [TestMethod]
    public void NonGenericGetEnumerator()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        var enumerator = ((IEnumerable)set).GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(1, enumerator.Current);
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(2, enumerator.Current);
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual(3, enumerator.Current);
        Assert.IsFalse(enumerator.MoveNext());
    }

    [TestMethod]
    public void Overlaps()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.Overlaps(new[] { 1, 2, 3, 4 }));
        Assert.IsTrue(set.Overlaps(new[] { 1, 2, 4, 5 }));
        Assert.IsTrue(set.Overlaps(new[] { 1, 2, 3 }));
        Assert.IsTrue(set.Overlaps(new[] { 1, 2 }));
        Assert.IsTrue(set.Overlaps(new[] { 1 }));
        Assert.IsFalse(set.Overlaps(new int[0]));
        Assert.IsFalse(set.Overlaps(new[] { 4 }));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void OverlapsNull() =>
        new OrderedHashSet<int>().Overlaps(null!);

    [TestMethod]
    public void RemoveFirst()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.RemoveFirst(out var value));
        Assert.AreEqual(1, value);
        Assert.AreEqual(2, set.First);
    }

    [TestMethod]
    public void RemoveFirstEmpty()
    {
        var set = new OrderedHashSet<int>();
        Assert.IsFalse(set.RemoveFirst(out var value));
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void RemoveLast()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.RemoveLast(out var value));
        Assert.AreEqual(3, value);
        Assert.AreEqual(2, set.Last);
    }

    [TestMethod]
    public void RemoveLastEmpty()
    {
        var set = new OrderedHashSet<int>();
        Assert.IsFalse(set.RemoveLast(out var value));
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void RemoveWhere()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.AreEqual(1, set.RemoveWhere(x => x % 2 == 0));
        Assert.AreEqual(2, set.Count);
        Assert.AreEqual(1, set.First);
        Assert.AreEqual(3, set.Last);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RemoveWhereNull() =>
        new OrderedHashSet<int>().RemoveWhere(null!);

    [TestMethod]
    public void SetEquals()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.SetEquals(new[] { 1, 2, 3 }));
        Assert.IsFalse(set.SetEquals(new[] { 1, 2, 3, 4 }));
        Assert.IsFalse(set.SetEquals(new[] { 1, 2, 4 }));
        Assert.IsFalse(set.SetEquals(new[] { 1, 2 }));
        Assert.IsFalse(set.SetEquals(new[] { 1 }));
        Assert.IsFalse(set.SetEquals(new int[0]));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetEqualsNull() =>
        new OrderedHashSet<int>().SetEquals(null!);

    [TestMethod]
    public void SetIsOrdered()
    {
        var set = new OrderedHashSet<int>(new[] { 5, 3, 2, 1 });
        Assert.AreEqual(4, set.Count);
        Assert.AreEqual(5, set.First);
        Assert.AreEqual(1, set.Last);
        set.Remove(5);
        Assert.AreEqual(3, set.First);
    }

    [TestMethod]
    public void SymmetricExceptWith()
    {
        var set = new OrderedHashSet<int>(new[] { 0, 1, 2, 3 });
        set.SymmetricExceptWith(new[] { 1, 2, 3, 4 });
        Assert.AreEqual(0, set.First);
        Assert.AreEqual(4, set.Last);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SymmetricExceptWithNull() =>
        new OrderedHashSet<int>().SymmetricExceptWith(null!);

    [TestMethod]
    public void TrimExcess()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        set.TrimExcess();
        Assert.AreEqual(3, set.Count);
        Assert.AreEqual(1, set.First);
        Assert.AreEqual(3, set.Last);
    }

    [TestMethod]
    public void TryGetValue()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsTrue(set.TryGetValue(2, out var value));
        Assert.AreEqual(2, value);
    }

    [TestMethod]
    public void TryGetValueNotFound()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        Assert.IsFalse(set.TryGetValue(4, out var value));
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void UnionWith()
    {
        var set = new OrderedHashSet<int>(new[] { 1, 2, 3 });
        set.UnionWith(new[] { 1, 2, 3, 4 });
        Assert.AreEqual(1, set.First);
        Assert.AreEqual(4, set.Last);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void UnionWithNull() =>
        new OrderedHashSet<int>().UnionWith(null!);
}
