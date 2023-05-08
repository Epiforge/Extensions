namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class ReflectionExtensions
{
    [TestMethod]
    public void ConstructorInfoFastInvoke()
    {
        var constructor = typeof(Guid).GetConstructor(new Type[] { typeof(string) })!;
        var result = constructor.FastInvoke("{85D27B78-67DA-4EC7-A968-40564E33F3AA}");
        Assert.IsInstanceOfType(result, typeof(Guid));
    }

    [TestMethod]
    public void DefaultInteger()
    {
        var result = typeof(int).FastDefault();
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void DefaultString()
    {
        var result = typeof(string).FastDefault();
        Assert.IsNull(result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DefaultNullType() =>
        ((Type?)null!).FastDefault();

    [TestMethod]
    public void IndexerPropertyInfoFastGetValue()
    {
        var instance = new List<int> { 1, 2, 3 };
        var property = typeof(List<int>).GetProperty("Item")!;
        var result = property.FastGetValue(instance, new object[] { 1 });
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void IndexerPropertyInfoFastSetValue()
    {
        var instance = new List<int> { 1, 2, 3 };
        var property = typeof(List<int>).GetProperty("Item")!;
        property.FastSetValue(instance, 10, new object[] { 1 });
        Assert.AreEqual(10, instance[1]);
    }

    [TestMethod]
    public void MethodInfoFastInvoke()
    {
        var instance = "Some Text";
        var method = typeof(string).GetMethod(nameof(string.Equals), new Type[] { typeof(string) })!;
        var result = method.FastInvoke(instance, new object[] { "Some Text" });
        Assert.IsTrue((bool)result!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullConstructorInfoFastInvoke() =>
        ((ConstructorInfo?)null!).FastInvoke();

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullMethodInfoFastInvoke() =>
        ((MethodInfo?)null!).FastInvoke(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyInfoFastGetValue() =>
        ((PropertyInfo?)null!).FastGetValue(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyInfoFastSetValue() =>
        ((PropertyInfo?)null!).FastSetValue(null!, null!);

    [TestMethod]
    public void PropertyInfoFastGetValue()
    {
        var instance = "Some Text";
        var property = typeof(string).GetProperty(nameof(string.Length))!;
        var result = property.FastGetValue(instance);
        Assert.AreEqual(9, result);
    }

    [TestMethod]
    public void PropertyInfoFastSetValue()
    {
        var instance = new Rectangle(0, 0, 1, 1);
        var property = typeof(Rectangle).GetProperty(nameof(Rectangle.X))!;
        var boxed = (object)instance;
        property.FastSetValue(boxed, 10);
        instance = (Rectangle)boxed;
        Assert.AreEqual(10, instance.X);
    }

    [TestMethod]
    public void StaticMethodInfoFastInvoke()
    {
        var method = typeof(Guid).GetMethod(nameof(Guid.Parse), new Type[] { typeof(string) })!;
        var result = method.FastInvoke(null, "{75E0D24E-B00F-4D53-9CAE-CC14D5E92CBE}");
        Assert.IsInstanceOfType(result, typeof(Guid));
    }

    [TestMethod]
    public void VoidMethodInfoFastInvoke()
    {
        var instance = new Stopwatch();
        var method = typeof(Stopwatch).GetMethod(nameof(Stopwatch.Start), Type.EmptyTypes)!;
        method.FastInvoke(instance);
        Assert.IsTrue(instance.IsRunning);
    }
}
