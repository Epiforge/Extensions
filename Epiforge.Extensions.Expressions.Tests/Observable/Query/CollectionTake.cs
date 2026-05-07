namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionTake
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var takeQuery = sourceQuery.ObserveTake(6))
            {
                Assert.IsTrue(new int[] { 0, 1, 2, 3, 4, 5 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 1, 2, 3, 4, 5, 6 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 2, 3, 4, 5, 6, 7 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 3, 4, 5, 6, 7, 8 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 4, 5, 6, 7, 8, 9 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 5, 6, 7, 8, 9 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 6, 7, 8, 9 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 7, 8, 9 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 8, 9 }.SequenceEqual(takeQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 9 }.SequenceEqual(takeQuery));
                source.Add(10);
                Assert.IsTrue(new int[] { 9, 10 }.SequenceEqual(takeQuery));
                source.Add(11);
                Assert.IsTrue(new int[] { 9, 10, 11 }.SequenceEqual(takeQuery));
                source.Add(12);
                Assert.IsTrue(new int[] { 9, 10, 11, 12 }.SequenceEqual(takeQuery));
                source.Insert(0, 13);
                Assert.IsTrue(new int[] { 13, 9, 10, 11, 12 }.SequenceEqual(takeQuery));
                source.Insert(0, 14);
                Assert.IsTrue(new int[] { 14, 13, 9, 10, 11, 12 }.SequenceEqual(takeQuery));
                source.Insert(0, 15);
                Assert.IsTrue(new int[] { 15, 14, 13, 9, 10, 11 }.SequenceEqual(takeQuery));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
