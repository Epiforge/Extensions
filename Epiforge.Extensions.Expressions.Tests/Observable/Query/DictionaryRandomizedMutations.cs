namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

public sealed class DictionaryRandomizedMutations
{
    static readonly string[] names = ["John", "Emily", "Erin", "Cliff", "Ben", "Bridget", "Nanette", "Jo"];

    public DictionaryRandomizedMutations(Random random, bool allowNullNames)
    {
        this.random = random;
        this.allowNullNames = allowNullNames;
    }

    readonly bool allowNullNames;
    int nextKey;
    readonly Random random;

    public ObservableDictionary<int, TestPerson> CreateSource(int count)
    {
        var source = new ObservableDictionary<int, TestPerson>();
        for (var i = 0; i < count; ++i)
            source.Add(nextKey++, NextPerson());
        return source;
    }

    public string Mutate(ObservableDictionary<int, TestPerson> source)
    {
        var keys = source.Keys.ToList();
        var choice = random.Next(100);
        if (keys.Count == 0 || choice < 18)
        {
            var added = new List<KeyValuePair<int, TestPerson>>();
            for (int i = 0, ii = random.Next(1, 4); i < ii; ++i)
                added.Add(new(nextKey++, NextPerson()));
            source.AddRange(added);
            return $"adding {added.Count}";
        }
        if (choice < 34)
        {
            var removed = PickKeys(keys, random.Next(1, Math.Min(3, keys.Count) + 1));
            source.RemoveRange(removed);
            return $"removing {removed.Count}";
        }
        if (choice < 48)
        {
            var key = keys[random.Next(keys.Count)];
            source[key] = NextPerson();
            return $"replacing the value at {key}";
        }
        if (choice < 58)
        {
            var replaced = PickKeys(keys, random.Next(1, Math.Min(3, keys.Count) + 1));
            source.ReplaceRange(replaced.Select(key => new KeyValuePair<int, TestPerson>(key, NextPerson())).ToList());
            return $"replacing {replaced.Count} values in one batch";
        }
        if (choice < 68)
        {
            var removed = PickKeys(keys, random.Next(1, Math.Min(3, keys.Count) + 1));
            var added = new List<KeyValuePair<int, TestPerson>>();
            for (int i = 0, ii = random.Next(0, 4); i < ii; ++i)
                added.Add(new(nextKey++, NextPerson()));
            source.ReplaceRange(removed, added);
            return $"swapping {removed.Count} for {added.Count} in one batch";
        }
        if (choice < 73)
        {
            var replacement = new Dictionary<int, TestPerson>();
            for (int i = 0, ii = random.Next(0, 8); i < ii; ++i)
                replacement.Add(nextKey++, NextPerson());
            source.Reset(replacement);
            return $"resetting to {replacement.Count}";
        }
        if (choice < 90)
        {
            var key = keys[random.Next(keys.Count)];
            var name = NextName();
            source[key].Name = name;
            return $"renaming the value at {key} to {name ?? "null"}";
        }
        var from = keys[random.Next(keys.Count)];
        var to = keys[random.Next(keys.Count)];
        source[to] = source[from];
        return $"pointing {to} at the value of {from}";
    }

    string? NextName() =>
        allowNullNames && random.Next(8) == 0 ? null : names[random.Next(names.Length)];

    TestPerson NextPerson() =>
        new(NextName()!);

    List<int> PickKeys(IReadOnlyList<int> keys, int count)
    {
        var picked = new List<int>();
        while (picked.Count < count)
        {
            var key = keys[random.Next(keys.Count)];
            if (!picked.Contains(key))
                picked.Add(key);
        }
        return picked;
    }
}
