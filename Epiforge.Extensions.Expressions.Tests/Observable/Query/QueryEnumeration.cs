namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryEnumeration
{
    [TestMethod]
    public void CollectionEnumerationIsASnapshot()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4))
            {
                var enumerated = new List<TestPerson>();
                foreach (var person in whereQuery)
                {
                    enumerated.Add(person);
                    if (enumerated.Count == 1)
                        source.Add(new TestPerson("Dale"));
                }
                Assert.HasCount(2, enumerated);
                Assert.AreEqual(3, whereQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    [Timeout(30000)]
    public void CollectionEnumerationReleasesTheQueryLock()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(person => person.Name!.Length == 4))
            {
                using var enumerator = whereQuery.GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());
                using var mutated = new ManualResetEventSlim(false);
                var mutation = Task.Run(() =>
                {
                    source.Add(new TestPerson("Dale"));
                    mutated.Set();
                });
                Assert.IsTrue(mutated.Wait(TimeSpan.FromSeconds(10)));
                mutation.Wait();
                Assert.IsTrue(enumerator.MoveNext());
                Assert.IsFalse(enumerator.MoveNext());
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void DictionaryEnumerationIsASnapshot()
    {
        var source = TestPerson.CreatePeopleDictionary();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere((key, value) => value.Name!.Length == 4))
            {
                var enumerated = new List<KeyValuePair<int, TestPerson>>();
                foreach (var keyValuePair in whereQuery)
                {
                    enumerated.Add(keyValuePair);
                    if (enumerated.Count == 1)
                        source.Add(source.Count, new TestPerson("Dale"));
                }
                Assert.HasCount(2, enumerated);
                Assert.AreEqual(3, whereQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void OrderByEnumerationIsASnapshot()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                var enumerated = new List<TestPerson>();
                foreach (var person in orderByQuery)
                {
                    enumerated.Add(person);
                    if (enumerated.Count == 1)
                        source.Add(new TestPerson("Aaron"));
                }
                Assert.HasCount(14, enumerated);
                Assert.AreEqual(15, orderByQuery.Count);
                Assert.IsFalse(enumerated.Any(person => person.Name == "Aaron"));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SelectManyEnumerationIsASnapshot()
    {
        var source = new ObservableRangeCollection<TestTeam>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectManyQuery = sourceQuery.ObserveSelectMany(team => team.People!))
            {
                var management = new TestTeam();
                management.People!.AddRange([new TestPerson("Charles"), new TestPerson("Michael")]);
                source.Add(management);
                var enumerated = new List<TestPerson>();
                foreach (var person in selectManyQuery)
                {
                    enumerated.Add(person);
                    if (enumerated.Count == 1)
                        management.People!.Add(new TestPerson("Dale"));
                }
                Assert.AreEqual("CharlesMichael", string.Join(string.Empty, enumerated.Select(person => person.Name)));
                Assert.AreEqual(3, selectManyQuery.Count);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
