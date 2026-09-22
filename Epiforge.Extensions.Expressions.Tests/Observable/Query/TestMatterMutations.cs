namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Applies one random mutation to a collection of matters, drawing from every kind of collection change and from changes to a member's hours, including inserting a member already present so that an element can occur more than once
/// </summary>
public static class TestMatterMutations
{
    public static string Mutate(Random random, ObservableRangeCollection<TestMatter> source, ref int nextName)
    {
        var count = source.Count;
        var choice = random.Next(100);
        if (count == 0 || choice < 20)
        {
            var index = random.Next(count + 1);
            var added = new List<TestMatter>();
            for (int i = 0, ii = random.Next(1, 3); i < ii; ++i)
                added.Add(new TestMatter($"m{nextName++}", random.Next(6)));
            source.InsertRange(index, added);
            return $"insert {added.Count} at {index}";
        }
        if (choice < 27)
        {
            var duplicate = source[random.Next(count)];
            var index = random.Next(count + 1);
            source.Insert(index, duplicate);
            return $"insert {duplicate} again at {index}";
        }
        if (choice < 42)
        {
            var removeCount = random.Next(1, Math.Min(2, count) + 1);
            var index = random.Next(count - removeCount + 1);
            source.RemoveRange(index, removeCount);
            return $"remove {removeCount} at {index}";
        }
        if (choice < 50)
        {
            var index = random.Next(count);
            var replacement = new TestMatter($"m{nextName++}", random.Next(6));
            source[index] = replacement;
            return $"replace at {index} with {replacement}";
        }
        if (choice < 57)
        {
            var oldIndex = random.Next(count);
            var newIndex = random.Next(count);
            source.Move(oldIndex, newIndex);
            return $"move from {oldIndex} to {newIndex}";
        }
        if (choice < 60)
        {
            var replacement = new List<TestMatter>();
            for (int i = 0, ii = random.Next(0, 6); i < ii; ++i)
                replacement.Add(new TestMatter($"m{nextName++}", random.Next(6)));
            source.Reset(replacement);
            return $"reset to {replacement.Count}";
        }
        var changed = source[random.Next(count)];
        var hours = random.Next(6);
        changed.Hours = hours;
        return $"set {changed.Name} to {hours}";
    }

    public static ObservableRangeCollection<TestMatter> Create(Random random, ref int nextName)
    {
        var matters = new List<TestMatter>();
        for (var i = 0; i < 6; ++i)
            matters.Add(new TestMatter($"m{nextName++}", random.Next(6)));
        return new(matters);
    }
}
