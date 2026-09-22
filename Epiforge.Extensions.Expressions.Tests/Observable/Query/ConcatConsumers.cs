namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Drives the queries which read a concatenation, a prepending or an appending, over projections whose indexed reads carry a penalty, and requires each to agree with LINQ after every change
/// </summary>
/// <remarks>
/// The comparison, element and filter queries and the list operations choose between reading their source by index and enumerating it by the penalty their source reports. A concatenation, a prepending and an appending reported none until 22 September whatever their sources charged, so these consumers read them by index; they now enumerate them. This exists so that the path taken since then is checked across additions, removals, replacements, moves and resets on either side, and so is the empty case the comparison has to fault on
/// </remarks>
[TestClass]
public class ConcatConsumers
{
    static readonly Expression<Func<int, int>> doubled = value => value * 2;
    static readonly Expression<Func<int, bool>> multipleOfThree = value => value % 3 == 0;

    static string MutateSource(Random random, ObservableRangeCollection<int> source, ref int nextValue)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 25)
        {
            var index = random.Next(count + 1);
            var added = new List<int>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(nextValue = (nextValue * 7 + 3) % 1000);
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 50)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 70)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = new List<int>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                replacements.Add(nextValue = (nextValue * 7 + 3) % 1000);
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} at {index} with {replacements.Count}";
        }
        if (choice < 90)
        {
            var moveCount = random.Next(1, Math.Min(3, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} from {oldIndex} to {newIndex}";
        }
        var replacement = new List<int>();
        for (int i = 0, ii = random.Next(0, 5); i < ii; ++i)
            replacement.Add(nextValue = (nextValue * 7 + 3) % 1000);
        source.Reset(replacement);
        return $"reset to {replacement.Count}";
    }

    static void AssertConsumersAgree(IObservableCollectionQuery<int> query, IReadOnlyList<int> expected, IObservableScalarQuery<int> max, IObservableScalarQuery<int> min, IObservableScalarQuery<int> third, IObservableCollectionQuery<int> filtered, string context)
    {
        if (expected.Count == 0)
        {
            Assert.IsNotNull(max.Evaluation.Fault, $"{context}: the maximum of nothing did not fault");
            Assert.IsNotNull(min.Evaluation.Fault, $"{context}: the minimum of nothing did not fault");
        }
        else
        {
            Assert.IsNull(max.Evaluation.Fault, $"{context}: the maximum faulted");
            Assert.AreEqual(expected.Max(), max.Evaluation.Result, $"{context}: the maximum diverged");
            Assert.IsNull(min.Evaluation.Fault, $"{context}: the minimum faulted");
            Assert.AreEqual(expected.Min(), min.Evaluation.Result, $"{context}: the minimum diverged");
        }
        Assert.AreEqual(expected.ElementAtOrDefault(3), third.Evaluation.Result, $"{context}: the element at 3 diverged");
        CollectionAssert.AreEquivalent(expected.Where(value => value % 3 == 0).ToList(), filtered.ToList(), $"{context}: the filter diverged");
        if (expected.Count > 0)
        {
            var sought = expected[expected.Count / 2];
            Assert.IsTrue(query.Contains(sought), $"{context}: Contains missed an element");
            Assert.AreEqual(IndexOf(expected, sought), query.IndexOf(sought), $"{context}: IndexOf diverged");
        }
        Assert.IsFalse(query.Contains(-7), $"{context}: Contains found an absent element");
    }

    static int IndexOf(IReadOnlyList<int> list, int value)
    {
        for (var i = 0; i < list.Count; ++i)
            if (list[i] == value)
                return i;
        return -1;
    }

    [TestMethod]
    [DataRow("concat")]
    [DataRow("prepend")]
    [DataRow("append")]
    [Timeout(300000)]
    public void ConsumersAgreeWithLinqAcrossRandomOperations(string shape)
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(shape, seed);
    }

    static void RunSeed(string shape, int seed)
    {
        var random = new Random(seed);
        var nextValue = seed + 1;
        var first = new ObservableRangeCollection<int>(Enumerable.Range(0, 5));
        var second = new ObservableRangeCollection<int>(Enumerable.Range(10, 5));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var firstQuery = collectionObserver.ObserveReadOnlyList(first))
        using (var secondQuery = collectionObserver.ObserveReadOnlyList(second))
        using (var firstSelect = firstQuery.ObserveSelect(doubled))
        using (var secondSelect = secondQuery.ObserveSelect(doubled))
        using (var query = shape switch
        {
            "concat" => firstSelect.ObserveConcat(secondSelect),
            "prepend" => firstSelect.ObservePrepend(-2),
            _ => firstSelect.ObserveAppend(-2)
        })
        using (var max = query.ObserveMax())
        using (var min = query.ObserveMin())
        using (var third = query.ObserveElementAtOrDefault(3))
        using (var filtered = query.ObserveWhere(multipleOfThree))
        {
            List<int> Expected() =>
                shape switch
                {
                    "concat" => [.. first.Select(value => value * 2).Concat(second.Select(value => value * 2))],
                    "prepend" => [.. first.Select(value => value * 2).Prepend(-2)],
                    _ => [.. first.Select(value => value * 2).Append(-2)]
                };
            AssertConsumersAgree(query, Expected(), max, min, third, filtered, $"{shape}, seed {seed}, before any change");
            for (var step = 0; step < 150; ++step)
            {
                var mutatingFirst = shape != "concat" || random.Next(2) == 0;
                var operation = MutateSource(random, mutatingFirst ? first : second, ref nextValue);
                AssertConsumersAgree(query, Expected(), max, min, third, filtered, $"{shape}, seed {seed}, step {step}, after {(mutatingFirst ? "first" : "second")} {operation}");
            }
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }
}
