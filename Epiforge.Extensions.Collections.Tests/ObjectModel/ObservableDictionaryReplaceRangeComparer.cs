namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableDictionaryReplaceRangeComparer
{
    static void AssertANewKeyEqualToARemovedOneUnderTheComparerIsAccepted(IRangeDictionary<string, int> dictionary)
    {
        var removed = dictionary.ReplaceRange(["alpha"], [new KeyValuePair<string, int>("ALPHA", 2)]);
        CollectionAssert.AreEqual(new[] { "alpha" }, removed.ToArray());
        Assert.AreEqual(2, dictionary.Count());
        Assert.AreEqual(2, dictionary["alpha"]);
        Assert.AreEqual(20, dictionary["beta"]);
    }

    static void AssertKeysEqualUnderTheComparerAreRemovedOnce<TDictionary>(TDictionary dictionary)
        where TDictionary : IRangeDictionary<string, int>, INotifyDictionaryChanged<string, int>, System.ComponentModel.INotifyPropertyChanged
    {
        var announcements = new List<string>();
        var counts = 0;
        dictionary.DictionaryChanged += (sender, e) => announcements.Add($"{e.Action}:{string.Join("+", e.OldItems.Select(pair => pair.Key))}");
        dictionary.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == "Count")
                ++counts;
        };
        var removed = dictionary.ReplaceRange(["alpha", "ALPHA"], []);
        Assert.AreEqual(1, removed.Count, "one key was reported removed more than once");
        Assert.AreEqual(1, dictionary.Count());
        Assert.AreEqual(1, announcements.Count);
        Assert.AreEqual(1, announcements[0].Split(':')[1].Split('+').Length, "one removed element was announced more than once");
        Assert.AreEqual(1, counts);
    }

    [TestMethod]
    public void TheDictionaryAcceptsANewKeyEqualToARemovedOne() =>
        AssertANewKeyEqualToARemovedOneUnderTheComparerIsAccepted(new ObservableDictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "alpha", 1 }, { "beta", 20 } });

    [TestMethod]
    public void TheDictionaryRemovesKeysEqualUnderItsComparerOnce() =>
        AssertKeysEqualUnderTheComparerAreRemovedOnce(new ObservableDictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "alpha", 1 }, { "beta", 20 } });

    [TestMethod]
    public void TheSortedDictionaryAcceptsANewKeyEqualToARemovedOne() =>
        AssertANewKeyEqualToARemovedOneUnderTheComparerIsAccepted(new ObservableSortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "alpha", 1 }, { "beta", 20 } });

    [TestMethod]
    public void TheSortedDictionaryRemovesKeysEqualUnderItsComparerOnce() =>
        AssertKeysEqualUnderTheComparerAreRemovedOnce(new ObservableSortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "alpha", 1 }, { "beta", 20 } });
}
