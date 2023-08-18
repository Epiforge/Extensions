using System.Reflection.Metadata;

namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class ReflectionExtensions
{
    #region Test Types

    interface IOnlyHaveAnEvent
    {
        event EventHandler? SomeEvent;
    }

    interface IOnlyHaveAMethod
    {
        void SomeMethod();
    }

    interface IOnlyHaveAProperty
    {
        int SomeProperty { get; set; }
    }

    interface IHaveThemAll :
        IOnlyHaveAnEvent,
        IOnlyHaveAMethod,
        IOnlyHaveAProperty
    {
    }

    #endregion Test Types

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
    public void GetImplementationEvents()
    {
        Assert.IsTrue(typeof(ObservableCollection<int>).GetImplementationEvents().Select(@event => @event.Name).Contains("CollectionChanged"));
        Assert.IsTrue(typeof(IHaveThemAll).GetImplementationEvents().Select(@event => @event.Name).Contains("SomeEvent"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetImplementationEventsNullType() =>
        ((Type?)null!).GetImplementationEvents();

    [TestMethod]
    public void GetImplementationMethods()
    {
        Assert.IsTrue(typeof(ObservableCollection<int>).GetImplementationMethods().Select(method => method.Name).Contains("Add"));
        Assert.IsTrue(typeof(IHaveThemAll).GetImplementationMethods().Select(method => method.Name).Contains("SomeMethod"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetImplementationMethodsNullType() =>
        ((Type?)null!).GetImplementationMethods();

    [TestMethod]
    public void GetImplementationProperties()
    {
        Assert.IsTrue(typeof(ObservableCollection<int>).GetImplementationProperties().Select(property => property.Name).Contains("Count"));
        Assert.IsTrue(typeof(IHaveThemAll).GetImplementationProperties().Select(property => property.Name).Contains("SomeProperty"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetImplementationPropertiesNullType() =>
        ((Type?)null!).GetImplementationProperties();

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
    public void ToObjectLiteralBoolean()
    {
        Assert.AreEqual("false", false.ToObjectLiteral());
        Assert.AreEqual("true", true.ToObjectLiteral());
    }

    [TestMethod]
    public void ToObjectLiteralArray() =>
        Assert.AreEqual("new Int32[] { 1, 2, 3 }", new int[] { 1, 2, 3 }.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralByte() =>
        Assert.AreEqual("(byte)0x00", ((byte)0).ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralCharacter() =>
        Assert.AreEqual("'a'", 'a'.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralDateTime() =>
        Assert.AreEqual("new DateTime(0L, DateTimeKind.Unspecified)", DateTime.MinValue.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralDateTimeOffset() =>
        Assert.AreEqual("new DateTimeOffset(0L, new TimeSpan(0L))", DateTimeOffset.MinValue.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteraDecimal() =>
        Assert.AreEqual("0M", 0M.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralDictionary() =>
        Assert.AreEqual("new Dictionary<String, Int32> { { \"a\", 1 }, { \"b\", 2 } }", new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralDoubleFloatingPoint() =>
        Assert.AreEqual("0D", 0D.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralEnum() =>
        Assert.AreEqual("DayOfWeek.Sunday", DayOfWeek.Sunday.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralEnumerable() =>
        Assert.AreEqual("new List<Int32> { 1, 2, 3 }", new List<int> { 1, 2, 3 }.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralEscapedCharacter() =>
        Assert.AreEqual("'\\n'", '\n'.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralFloatingPoint() =>
        Assert.AreEqual("0F", 0F.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralGuid() =>
        Assert.AreEqual("new Guid(\"00000000-0000-0000-0000-000000000000\")", Guid.Empty.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralInteger() =>
        Assert.AreEqual("0", 0.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralLongInteger() =>
        Assert.AreEqual("0L", 0L.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralShort() =>
        Assert.AreEqual("(short)0", ((short)0).ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralSignedByte() =>
        Assert.AreEqual("(sbyte)0x00", ((sbyte)0).ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralTimeSpan() =>
        Assert.AreEqual("new TimeSpan(0L)", TimeSpan.Zero.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralUnprintableCharacter() =>
        Assert.AreEqual("\'\\u007F'", '\u007F'.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralUnsignedInteger() =>
        Assert.AreEqual("0U", 0U.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralUnsignedLongInteger() =>
        Assert.AreEqual("0UL", 0UL.ToObjectLiteral());

    [TestMethod]
    public void ToObjectLiteralUnsignedShort() =>
        Assert.AreEqual("(ushort)0", ((ushort)0).ToObjectLiteral());

    [TestMethod]
    public void VoidMethodInfoFastInvoke()
    {
        var instance = new Stopwatch();
        var method = typeof(Stopwatch).GetMethod(nameof(Stopwatch.Start), Type.EmptyTypes)!;
        method.FastInvoke(instance);
        Assert.IsTrue(instance.IsRunning);
    }
}
