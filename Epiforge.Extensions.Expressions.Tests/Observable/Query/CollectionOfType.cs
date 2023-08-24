namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionOfType
{
    [TestMethod]
    public void SourceManipulation()
    {
        var source = new ObservableRangeCollection<object>(new object[]
        {
            0,
            false,
            "John",
            DateTime.Now,
            "Emily",
            Guid.NewGuid(),
            "Charles",
            TimeSpan.Zero,
            new object()
        });
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var ofTypeQuery = sourceQuery.ObserveOfType<string>())
            {
                void checkStrings(params string[] against) =>
                    Assert.IsTrue(ofTypeQuery.OrderBy(s => s).SequenceEqual(against));
                checkStrings("Charles", "Emily", "John");
                source.Add("Bridget");
                source.Remove("John");
                source.Move(source.Count - 1, 0);
                checkStrings("Bridget", "Charles", "Emily");
                source.Reset(new object[]
                {
                    new object(),
                    TimeSpan.Zero,
                    "George",
                    Guid.NewGuid(),
                    "Craig",
                    DateTime.Now,
                    "Cliff",
                    false,
                    0
                });
                checkStrings("Cliff", "Craig", "George");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
