namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class SourceIdentity
{
    class ValueEqualList :
        IReadOnlyList<string>
    {
        public ValueEqualList(params string[] elements) =>
            this.elements = [..elements];

        readonly List<string> elements;

        public string this[int index] =>
            elements[index];

        public int Count =>
            elements.Count;

        public void Add(string element) =>
            elements.Add(element);

        public override bool Equals(object? obj) =>
            obj is ValueEqualList other && elements.SequenceEqual(other.elements);

        public IEnumerator<string> GetEnumerator() =>
            elements.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            elements.GetEnumerator();

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            foreach (var element in elements)
                hashCode.Add(element);
            return hashCode.ToHashCode();
        }
    }

    [TestMethod]
    public void DistinctButEqualSourcesAreNotConflated()
    {
        var first = new ValueEqualList("John", "Emily");
        var second = new ValueEqualList("John", "Emily");
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var firstQuery = collectionObserver.ObserveReadOnlyList(first))
        using (var secondQuery = collectionObserver.ObserveReadOnlyList(second))
        {
            Assert.AreNotSame(firstQuery, secondQuery);
            Assert.AreEqual(2, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    [TestMethod]
    public void TheSameSourceIsShared()
    {
        var source = new ValueEqualList("John", "Emily");
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var firstQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var secondQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            Assert.AreNotSame(firstQuery, secondQuery);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    [TestMethod]
    public void ASourceMutatedWhileObservedIsStillReleased()
    {
        var source = new ValueEqualList("John", "Emily");
        var collectionObserver = CollectionObserverHelpers.Create();
        using (collectionObserver.ObserveReadOnlyList(source))
        {
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
            source.Add("Charles");
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    [TestMethod]
    public void ASourceMutatedWhileObservedIsStillFound()
    {
        var source = new ValueEqualList("John", "Emily");
        var collectionObserver = CollectionObserverHelpers.Create();
        using (collectionObserver.ObserveReadOnlyList(source))
        {
            source.Add("Charles");
            using (collectionObserver.ObserveReadOnlyList(source))
                Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }
}
