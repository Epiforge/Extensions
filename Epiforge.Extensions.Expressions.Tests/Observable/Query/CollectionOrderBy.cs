namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionOrderBy
{
    [TestMethod]
    public void NoSelectors()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy())
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("JohnEmilyCharlesErinCliffHunterBenCraigBridgetNanetteGeorgeBryanJamesSteve");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SelectorsDirections()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy((person => person.Name!.Length, true), (person => person.Name!, false)))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("BridgetCharlesNanetteGeorgeHunterBryanCliffCraigEmilyJamesSteveErinJohnBen");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        source.Add(source[0]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy(person => person.Name!))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnJohnNanetteSteve");
                source.Add(source[0]);
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnJohnJohnNanetteSteve");
                source[0].Name = "Buddy";
                checkMergedNames("BenBridgetBryanBuddyBuddyBuddyCharlesCliffCraigEmilyErinGeorgeHunterJamesNanetteSteve");
                source[0].Name = "John";
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnJohnJohnNanetteSteve");
                source.RemoveRange(source.Count - 2, 2);
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJohnNanetteSteve");
                source.Add(new TestPerson("Javon"));
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonJohnNanetteSteve");
                source[0].Name = null;
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Insert(0, new TestPerson());
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.RemoveRange(0, 2);
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Add(new TestPerson());
                checkMergedNames("BenBridgetBryanCharlesCliffCraigEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Add(new TestPerson("Daniel"));
                checkMergedNames("BenBridgetBryanCharlesCliffCraigDanielEmilyErinGeorgeHunterJamesJavonNanetteSteve");
                source.Reset(new TestPerson[] { new TestPerson("Kelly") });
                checkMergedNames("Kelly");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulationMultipleSelectors()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy((person => person.Name!.Length, false), (person => person.Name!, false)))
            {
                void checkMergedNames(string against) =>
                    Assert.AreEqual(against, string.Join(string.Empty, orderByQuery!.Select(person => person.Name)));
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                source[0].Name = "J";
                checkMergedNames("JBenErinBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                source[0].Name = "Johnathon";
                checkMergedNames("BenErinBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanetteJohnathon");
                Assert.IsNull(orderByQuery.OperationFault);
                source[0].Name = null;
                Assert.IsNotNull(orderByQuery.OperationFault);
                source[0].Name = "John";
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                Assert.IsNull(orderByQuery.OperationFault);
                source.Add(new TestPerson("Daniel"));
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveDanielGeorgeHunterBridgetCharlesNanette");
                source.RemoveAt(source.Count - 1);
                checkMergedNames("BenErinJohnBryanCliffCraigEmilyJamesSteveGeorgeHunterBridgetCharlesNanette");
                source.Reset(new TestPerson[] { new TestPerson("Kelly") });
                checkMergedNames("Kelly");
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
