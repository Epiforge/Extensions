namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class DictionaryWhere
{
    [TestMethod]
    public void ElementResultChanges()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var counts = new BlockingCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4))
            {
                counts.Add(whereQuery.Count);
                source[0].Name = "Johnny";
                counts.Add(whereQuery.Count);
                source[1].Name = "Emilia";
                counts.Add(whereQuery.Count);
                source[12].Name = "Jack";
                counts.Add(whereQuery.Count);
            }
            Assert.IsTrue(new int[] { 2, 1, 1, 2 }.SequenceEqual(counts));
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementsAdded()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var counts = new BlockingCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4))
            {
                counts.Add(whereQuery.Count);
                source.Add(14, new TestPerson("Jack"));
                counts.Add(whereQuery.Count);
                source.Add(15, new TestPerson("Chuck"));
                counts.Add(whereQuery.Count);
                source.AddRange(new KeyValuePair<int, TestPerson>[]
                {
                    new KeyValuePair<int, TestPerson>(16, new TestPerson("Jill")),
                    new KeyValuePair<int, TestPerson>(17, new TestPerson("Nick"))
                });
                counts.Add(whereQuery.Count);
                source.AddRange(new KeyValuePair<int, TestPerson>[]
                {
                    new KeyValuePair<int, TestPerson>(18, new TestPerson("Clint")),
                    new KeyValuePair<int, TestPerson>(19, new TestPerson("Harry"))
                });
                counts.Add(whereQuery.Count);
                source.AddRange(new KeyValuePair<int, TestPerson>[]
                {
                    new KeyValuePair<int, TestPerson>(20, new TestPerson("Dana")),
                    new KeyValuePair<int, TestPerson>(21, new TestPerson("Ray"))
                });
                counts.Add(whereQuery.Count);
                source[7] = new TestPerson("Tony");
                counts.Add(whereQuery.Count);
            }
            Assert.IsTrue(new int[] { 2, 3, 3, 5, 5, 6, 7 }.SequenceEqual(counts));
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementsRemoved()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var counts = new BlockingCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 5))
            {
                counts.Add(whereQuery.Count);
                source.Remove(1);
                counts.Add(whereQuery.Count);
                source.Remove(0);
                counts.Add(whereQuery.Count);
                source.RemoveRange(new int[] { 11, 12 });
                counts.Add(whereQuery.Count);
                source.RemoveRange(new int[] { 10, 13 });
                counts.Add(whereQuery.Count);
            }
            Assert.IsTrue(new int[] { 6, 5, 5, 3, 2 }.SequenceEqual(counts));
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
