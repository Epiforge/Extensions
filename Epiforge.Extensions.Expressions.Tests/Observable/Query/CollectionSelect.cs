namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSelect
{
    static int ElementFaultCount(Exception? operationFault) =>
        operationFault switch
        {
            null => 0,
            AggregateException aggregateException => aggregateException.InnerExceptions.OfType<EvaluationFaultException>().Count(),
            EvaluationFaultException => 1,
            _ => -1
        };

    [TestMethod]
    public void ElementFaultsAccumulateAcrossSourceChanges()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                Assert.IsNull(selectQuery.OperationFault);
                var firstFaulting = new TestPerson();
                var secondFaulting = new TestPerson();
                source.Add(firstFaulting);
                Assert.AreEqual(1, ElementFaultCount(selectQuery.OperationFault));
                source.Add(secondFaulting);
                Assert.AreEqual(2, ElementFaultCount(selectQuery.OperationFault));
                source.Remove(firstFaulting);
                Assert.AreEqual(1, ElementFaultCount(selectQuery.OperationFault));
                source.Remove(secondFaulting);
                Assert.IsNull(selectQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementFaultsAreCountedPerOccurrence()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                var faulting = new TestPerson();
                source.Add(faulting);
                source.Add(faulting);
                Assert.AreEqual(2, ElementFaultCount(selectQuery.OperationFault));
                source.Remove(faulting);
                Assert.AreEqual(1, ElementFaultCount(selectQuery.OperationFault));
                source.Remove(faulting);
                Assert.IsNull(selectQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementFaultsAreExchangedByReplacement()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                Assert.IsNull(selectQuery.OperationFault);
                source[0] = new TestPerson();
                Assert.AreEqual(1, ElementFaultCount(selectQuery.OperationFault));
                source[0] = new TestPerson("Bill");
                Assert.IsNull(selectQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementFaultsDoNotDependOnTheOrderOfChanges()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                var faulting = new TestPerson();
                source.Add(faulting);
                source.Add(faulting);
                Assert.AreEqual(2, ElementFaultCount(selectQuery.OperationFault));
                faulting.Name = "Bill";
                Assert.IsNull(selectQuery.OperationFault);
                faulting.Name = null;
                Assert.AreEqual(2, ElementFaultCount(selectQuery.OperationFault));
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementFaultsSurviveUnrelatedRemoval()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                var faulting = new TestPerson();
                source.Add(faulting);
                Assert.AreEqual(1, ElementFaultCount(selectQuery.OperationFault));
                source.RemoveAt(0);
                Assert.AreEqual(1, ElementFaultCount(selectQuery.OperationFault));
                source.Remove(faulting);
                Assert.IsNull(selectQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void SourceManipulation()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectQuery = sourceQuery.ObserveSelect(person => person.Name!.Length))
            {
                Assert.IsNull(selectQuery.OperationFault);
                void checkValues(params int[] values) =>
                    Assert.IsTrue(values.SequenceEqual(selectQuery));
                checkValues(4, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Add(source.First());
                checkValues(4, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5, 4);
                source[0].Name = "Johnny";
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5, 6);
                source.RemoveAt(source.Count - 1);
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Move(0, 1);
                checkValues(5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Insert(0, source[0]);
                checkValues(5, 5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.RemoveAt(1);
                checkValues(5, 6, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.Move(1, 0);
                checkValues(6, 5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
                source.RemoveAt(0);
                checkValues(5, 7, 4, 5, 6, 3, 5, 7, 7, 6, 5, 5, 5);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
