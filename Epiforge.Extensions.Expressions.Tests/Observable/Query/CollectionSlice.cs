namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSlice
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var sliceQuery = sourceQuery.ObserveSlice(2..8))
            {
                Assert.IsTrue(new int[] { 2, 3, 4, 5, 6, 7 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 3, 4, 5, 6, 7, 8 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 4, 5, 6, 7, 8, 9 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 5, 6, 7, 8, 9 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 6, 7, 8, 9 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 7, 8, 9 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 8, 9 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { 9 }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { }.SequenceEqual(sliceQuery));
                source.RemoveAt(0);
                Assert.IsTrue(new int[] { }.SequenceEqual(sliceQuery));
                source.Add(10);
                Assert.IsTrue(new int[] { }.SequenceEqual(sliceQuery));
                source.Add(11);
                Assert.IsTrue(new int[] { 11 }.SequenceEqual(sliceQuery));
                source.Add(12);
                Assert.IsTrue(new int[] { 11, 12 }.SequenceEqual(sliceQuery));
                source.Insert(0, 13);
                Assert.IsTrue(new int[] { 10, 11, 12 }.SequenceEqual(sliceQuery));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
