namespace Epiforge.Extensions.Collections.Tests.Generic;

[TestClass]
public class ReversedComparer
{
    [TestMethod]
    public void Test()
    {
        var reversedStringComparer = ReversedComparer<string>.Default;
        Assert.AreEqual(1, reversedStringComparer.Compare("a", "b"));
        Assert.AreEqual(0, reversedStringComparer.Compare("a", "a"));
        Assert.AreEqual(-1, reversedStringComparer.Compare("b", "a"));
    }
}
