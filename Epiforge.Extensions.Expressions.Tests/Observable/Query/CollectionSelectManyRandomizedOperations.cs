namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionSelectManyRandomizedOperations
{
    static ObservableRangeCollection<TestPerson> MakePeople(Random random) =>
        new(Enumerable.Range(0, random.Next(0, 4)).Select(index => new TestPerson($"P{index:D2}")));

    static TestTeam MakeTeam(Random random) =>
        new(random.Next(8) == 0 ? null : MakePeople(random));

    static string MutateSource(Random random, ObservableRangeCollection<TestTeam> source)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 14)
        {
            var index = random.Next(count + 1);
            var added = Enumerable.Range(0, random.Next(1, 4)).Select(_ => count > 0 && random.Next(4) == 0 ? source[random.Next(count)] : MakeTeam(random)).ToList();
            source.InsertRange(index, added);
            return $"insert {added.Count} teams at {index}";
        }
        if (choice < 26)
        {
            var removeCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} teams at {index}";
        }
        if (choice < 36)
        {
            var replaceCount = random.Next(1, Math.Min(3, count) + 1);
            var index = random.Next(count - replaceCount + 1);
            var replacements = Enumerable.Range(0, random.Next(1, 3)).Select(_ => MakeTeam(random)).ToList();
            source.ReplaceRange(index, replaceCount, replacements);
            return $"replace {replaceCount} teams at {index} with {replacements.Count}";
        }
        if (choice < 50)
        {
            var moveCount = random.Next(1, Math.Min(3, count) + 1);
            var oldIndex = random.Next(count - moveCount + 1);
            var newIndex = random.Next(count - moveCount + 1);
            source.MoveRange(oldIndex, newIndex, moveCount);
            return $"move {moveCount} teams from {oldIndex} to {newIndex}";
        }
        if (choice < 54)
        {
            var replacement = Enumerable.Range(0, random.Next(0, 6)).Select(_ => MakeTeam(random)).ToList();
            source.Reset(replacement);
            return $"reset the source to {replacement.Count} teams";
        }
        if (choice < 62)
        {
            var team = source[random.Next(count)];
            team.People = random.Next(6) switch
            {
                0 => null,
                1 => source[random.Next(count)].People,
                _ => MakePeople(random)
            };
            return $"replace a team's people with {(team.People is null ? "null" : $"{team.People.Count} people")}";
        }
        var withPeople = source.Where(team => team.People is not null).ToList();
        if (withPeople.Count == 0)
            return "nothing, since no team has people";
        var people = withPeople[random.Next(withPeople.Count)].People!;
        if (choice < 76)
        {
            var index = random.Next(people.Count + 1);
            people.Insert(index, new TestPerson($"N{random.Next(100):D2}"));
            return $"insert a person at {index} of {people.Count - 1}";
        }
        if (choice < 88)
        {
            if (people.Count == 0)
                return "nothing, since the chosen team is empty";
            var index = random.Next(people.Count);
            people.RemoveAt(index);
            return $"remove the person at {index}";
        }
        if (choice < 96)
        {
            if (people.Count < 2)
                return "nothing, since the chosen team is too small to reorder";
            var oldIndex = random.Next(people.Count);
            var newIndex = random.Next(people.Count);
            people.Move(oldIndex, newIndex);
            return $"move a person from {oldIndex} to {newIndex}";
        }
        var newPeople = Enumerable.Range(0, random.Next(0, 4)).Select(index => new TestPerson($"R{index:D2}")).ToList();
        people.Reset(newPeople);
        return $"reset a team's people to {newPeople.Count}";
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomStructuralAndInnerChangesAgreeWithADirectSelectMany()
    {
        for (var seed = 0; seed < 20; ++seed)
            RunSeed(seed);
    }

    static void RunSeed(int seed)
    {
        var random = new Random(seed);
        var source = new ObservableRangeCollection<TestTeam>(Enumerable.Range(0, 6).Select(_ => MakeTeam(random)));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var selectManyQuery = sourceQuery.ObserveSelectMany(team => team.People!))
            {
                var mirror = new List<TestPerson>(selectManyQuery);
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            for (var i = 0; i < e.NewItems!.Count; ++i)
                                mirror.Insert(e.NewStartingIndex + i, (TestPerson)e.NewItems[i]!);
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                            for (var i = 0; i < e.NewItems!.Count; ++i)
                                mirror.Insert(e.OldStartingIndex + i, (TestPerson)e.NewItems[i]!);
                            break;
                        case NotifyCollectionChangedAction.Move:
                            var moved = mirror.GetRange(e.OldStartingIndex, e.OldItems!.Count);
                            mirror.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                            mirror.InsertRange(e.NewStartingIndex, moved);
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            mirror.Clear();
                            mirror.AddRange(selectManyQuery);
                            break;
                    }
                }
                selectManyQuery.CollectionChanged += collectionChanged;
                for (var step = 0; step < 150; ++step)
                {
                    var operation = MutateSource(random, source);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = source.SelectMany(team => (IEnumerable<TestPerson>?)team.People ?? []).ToList();
                    CollectionAssert.AreEqual(expected, selectManyQuery.ToList(), $"{context}: contents diverged");
                    CollectionAssert.AreEqual(expected, mirror, $"{context}: the notification mirror diverged");
                    Assert.AreEqual(expected.Count, selectManyQuery.Count, $"{context}: count diverged");
                    if (expected.Count > 0)
                    {
                        var probe = random.Next(expected.Count);
                        Assert.AreSame(expected[probe], selectManyQuery[probe], $"{context}: the indexer diverged at {probe}");
                    }
                }
                selectManyQuery.CollectionChanged -= collectionChanged;
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
