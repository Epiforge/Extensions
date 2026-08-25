namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionWhere
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
            using (var whereQuery = sourceQuery.ObserveWhere(p => p.Name!.Length == 4))
            {
                Assert.IsNull(whereQuery.OperationFault);
                var firstFaulting = new TestPerson();
                var secondFaulting = new TestPerson();
                source.Add(firstFaulting);
                Assert.AreEqual(1, ElementFaultCount(whereQuery.OperationFault));
                source.Add(secondFaulting);
                Assert.AreEqual(2, ElementFaultCount(whereQuery.OperationFault));
                source.Remove(firstFaulting);
                Assert.AreEqual(1, ElementFaultCount(whereQuery.OperationFault));
                source.Remove(secondFaulting);
                Assert.IsNull(whereQuery.OperationFault);
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
            using (var whereQuery = sourceQuery.ObserveWhere(p => p.Name!.Length == 4))
            {
                var faulting = new TestPerson();
                source.Add(faulting);
                Assert.AreEqual(1, ElementFaultCount(whereQuery.OperationFault));
                source.RemoveAt(0);
                Assert.AreEqual(1, ElementFaultCount(whereQuery.OperationFault));
                source.Remove(faulting);
                Assert.IsNull(whereQuery.OperationFault);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementResultChanges()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(p => p.Name!.Length == 4))
            {
                Assert.IsNull(whereQuery.OperationFault);
                Assert.IsTrue(new string[] { "John", "Erin" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source[0].Name = "Johnny";
                Assert.IsTrue(new string[] { "Erin" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source[1].Name = "Emilia";
                Assert.IsTrue(new string[] { "Erin" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source[12].Name = "Jack";
                Assert.IsTrue(new string[] { "Erin", "Jack" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                using var whereQuery2 = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
                Assert.AreNotSame(whereQuery, whereQuery2);
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementsAdded()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(p => p.Name!.Length == 4))
            {
                Assert.IsNull(whereQuery.OperationFault);
                Assert.IsTrue(new string[] { "John", "Erin" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.Add(new TestPerson("Jack"));
                Assert.IsTrue(new string[] { "John", "Erin", "Jack" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.Add(new TestPerson("Chuck"));
                Assert.IsTrue(new string[] { "John", "Erin", "Jack" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.AddRange(new TestPerson[] { new TestPerson("Jill"), new TestPerson("Nick") });
                Assert.IsTrue(new string[] { "John", "Erin", "Jack", "Jill", "Nick" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.AddRange(new TestPerson[] { new TestPerson("Clint"), new TestPerson("Harry") });
                Assert.IsTrue(new string[] { "John", "Erin", "Jack", "Jill", "Nick" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.AddRange(new TestPerson[] { new TestPerson("Dana"), new TestPerson("Ray") });
                Assert.IsTrue(new string[] { "John", "Erin", "Jack", "Jill", "Nick", "Dana" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source[14] = new TestPerson("Tony");
                Assert.IsTrue(new string[] { "John", "Erin", "Tony", "Jill", "Nick", "Dana" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                using var whereQuery2 = sourceQuery.ObserveWhere(p => p.Name!.Length == 4);
                Assert.AreNotSame(whereQuery, whereQuery2);
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ElementsRemoved()
    {
        var source = TestPerson.CreatePeopleCollection();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var whereQuery = sourceQuery.ObserveWhere(p => p.Name!.Length == 5))
            {
                Assert.IsNull(whereQuery.OperationFault);
                Assert.IsTrue(new string[] { "Emily", "Cliff", "Craig", "Bryan", "James", "Steve" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.RemoveAt(1);
                Assert.IsTrue(new string[] { "Cliff", "Craig", "Bryan", "James", "Steve" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.RemoveAt(0);
                Assert.IsTrue(new string[] { "Cliff", "Craig", "Bryan", "James", "Steve" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.RemoveRange(9, 2);
                Assert.IsTrue(new string[] { "Cliff", "Craig", "Steve" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                source.RemoveRange(8, 2);
                Assert.IsTrue(new string[] { "Cliff", "Craig" }.SequenceEqual(whereQuery.Select(person => person.Name)));
                using var whereQuery2 = sourceQuery.ObserveWhere(p => p.Name!.Length == 5);
                Assert.AreNotSame(whereQuery, whereQuery2);
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(1, collectionObserver.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
