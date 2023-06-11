namespace Epiforge.Extensions.Collections.Tests.Specialized;

[TestClass]
public class NotifyDictionaryChangedEventArgs
{
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void NonResetWithNoItems() =>
        new NotifyDictionaryChangedEventArgs<string, string>(NotifyDictionaryChangedAction.Add);

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void NonAddRemoveWithItems() =>
        new NotifyDictionaryChangedEventArgs<string, string>(NotifyDictionaryChangedAction.Reset, new[] { new KeyValuePair<string, string>("key", "value") });
}
