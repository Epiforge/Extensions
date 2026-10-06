namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class DynamicCode
{
    [TestMethod]
    public void TheRuntimeReportsDynamicCodeAsThisProjectExpects()
    {
#if WITHOUT_DYNAMIC_CODE
        Assert.IsFalse(RuntimeFeature.IsDynamicCodeSupported);
#else
        Assert.IsTrue(RuntimeFeature.IsDynamicCodeSupported);
#endif
    }
}
