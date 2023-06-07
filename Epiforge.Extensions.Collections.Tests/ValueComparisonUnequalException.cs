namespace Epiforge.Extensions.Collections.Tests;

[TestClass]
public class ValueComparisonUnequalException
{
    [TestMethod]
    public void ConstructionWithMessage()
    {
        var exception = new Collections.ValueComparisonUnequalException("message");
        Assert.AreEqual("message", exception.Message);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    public void ConstructionWithInnerException()
    {
        var innerException = new Exception();
        var exception = new Collections.ValueComparisonUnequalException("message", innerException);
        Assert.AreEqual("message", exception.Message);
        Assert.AreSame(innerException, exception.InnerException);
    }
}
