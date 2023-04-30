namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class PropertyChangeNotifier
{
    public class Derivation :
        Components.PropertyChangeNotifier
    {
        public Derivation(ILogger<Derivation> logger, string text)
        {
            Logger = logger;
            this.text = text;
        }

        string? nullableText;
        string text;

        public string? NullableText
        {
            get => nullableText;
            set => SetBackedProperty(ref nullableText, in value);
        }

        public string Text
        {
            get => text;
            set => SetBackedProperty(ref text, in value);
        }
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var propertiesChanged = new List<string>();
        var propertiesChanging = new List<string>();
        var mockLogger = new Mock<ILogger<Derivation>>();
        var derivation = new Derivation(mockLogger.Object, "text");

        void propertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var propertyName = e.PropertyName;
            Assert.IsNotNull(propertyName);
            Assert.AreEqual(propertiesChanging.Count - 1, propertiesChanged.Count);
            Assert.AreEqual(propertyName, propertiesChanging.Last());
            propertiesChanged.Add(propertyName!);
        }

        void propertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            var propertyName = e.PropertyName;
            Assert.IsNotNull(propertyName);
            Assert.AreEqual(propertiesChanged.Count, propertiesChanged.Count);
            propertiesChanging.Add(propertyName!);
        }

        derivation.PropertyChanged += propertyChanged;
        derivation.PropertyChanging += propertyChanging;

        derivation.Text = "other text";
        mockLogger.VerifyLogDebug("\"Text\" property is changing from \"text\" to \"other text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"Text\"");
        mockLogger.Invocations.Clear();
        derivation.NullableText = "Suprise!";
        mockLogger.VerifyLogDebug("\"NullableText\" property is changing from \"(null)\" to \"Suprise!\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"NullableText\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"NullableText\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"NullableText\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"NullableText\"");
        mockLogger.Invocations.Clear();
        derivation.NullableText = null;
        mockLogger.VerifyLogDebug("\"NullableText\" property is changing from \"Suprise!\" to \"(null)\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"NullableText\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"NullableText\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"NullableText\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"NullableText\"");
        mockLogger.Invocations.Clear();

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }
}