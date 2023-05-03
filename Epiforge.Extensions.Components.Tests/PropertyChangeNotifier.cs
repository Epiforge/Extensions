namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class PropertyChangeNotifier
{
    public class Derivation :
        Components.PropertyChangeNotifier
    {
        static readonly PropertyChangedEventArgs propertyChangedEventArgs = new(nameof(Text));
        static readonly PropertyChangingEventArgs propertyChangingEventArgs = new(nameof(Text));

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

        public string? NullableTexttWithNullEqualityComparer
        {
            get => nullableText;
            set => SetBackedProperty(ref nullableText, in value, (IEqualityComparer<string?>)null!);
        }

        public string? NullableTextWithoutIn
        {
            get => nullableText;
            set => SetBackedProperty(ref nullableText, value);
        }

        public string Text
        {
            get => text;
            set => SetBackedProperty(ref text, in value);
        }

        public string TextWithCachedEventArgs
        {
            get => text;
            set => SetBackedProperty(ref text, in value, propertyChangingEventArgs, propertyChangedEventArgs);
        }

        public string TextWithoutInAndCachedEventArgs
        {
            get => text;
            set => SetBackedProperty(ref text, value, propertyChangingEventArgs, propertyChangedEventArgs);
        }

        public string TextWithCachedChangedEventArgsOnly
        {
            get => text;
            set => SetBackedProperty(ref text, in value, null!, propertyChangedEventArgs);
        }

        public string TextWithCachedChangingEventArgsOnly
        {
            get => text;
            set => SetBackedProperty(ref text, in value, propertyChangingEventArgs, null!);
        }

        public string TextWithCachedEventArgsAndNullComparer
        {
            get => text;
            set => SetBackedProperty(ref text, in value, null!, propertyChangingEventArgs, propertyChangedEventArgs);
        }

        public string TextWithoutIn
        {
            get => text;
            set => SetBackedProperty(ref text, value);
        }

        public string TextWithoutInAndNullComparer
        {
            get => text;
            set => SetBackedProperty(ref text, value, (IEqualityComparer<string>)null!);
        }

        public string TextWithoutInWithCachedChangedEventArgsOnly
        {
            get => text;
            set => SetBackedProperty(ref text, value, null!, propertyChangedEventArgs);
        }

        public string TextWithoutInWithCachedChangingEventArgsOnly
        {
            get => text;
            set => SetBackedProperty(ref text, value, propertyChangingEventArgs, null!);
        }

        public string TextWithoutInWithCachedEventArgsAndNullComparer
        {
            get => text;
            set => SetBackedProperty(ref text, value, null!, propertyChangingEventArgs, propertyChangedEventArgs);
        }

        public void NullPropertyChangedEventArgs() =>
            OnPropertyChanged((PropertyChangedEventArgs)null!);

        public void NullPropertyChangedName() =>
            OnPropertyChanged((string?)null);

        public void NullPropertyChangingEventArgs() =>
            OnPropertyChanging((PropertyChangingEventArgs)null!);

        public void NullPropertyChangingName() =>
            OnPropertyChanging((string?)null);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullEqualityComparer()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            NullableTexttWithNullEqualityComparer = "bruh"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullEqualityComparerWithoutIn()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithoutInAndNullComparer = "bruh"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangedEventArgs()
    {
        var derivation = new Derivation(new Mock<ILogger<Derivation>>().Object, "text");
        derivation.NullPropertyChangedEventArgs();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangedName()
    {
        var derivation = new Derivation(new Mock<ILogger<Derivation>>().Object, "text");
        derivation.NullPropertyChangedName();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangingEventArgs()
    {
        var derivation = new Derivation(new Mock<ILogger<Derivation>>().Object, "text");
        derivation.NullPropertyChangingEventArgs();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangingName()
    {
        var derivation = new Derivation(new Mock<ILogger<Derivation>>().Object, "text");
        derivation.NullPropertyChangingName();
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

        derivation.Text = "text";
        Assert.AreEqual(0, mockLogger.Invocations.Count);
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

    [TestMethod]
    public void PropertyChangesWithCachedEventArgs()
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

        derivation.TextWithCachedEventArgs = "text";
        Assert.AreEqual(0, mockLogger.Invocations.Count);
        derivation.TextWithCachedEventArgs = "other text";
        mockLogger.VerifyLogDebug("\"Text\" property is changing from \"text\" to \"other text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"Text\"");

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }

    [TestMethod]
    public void PropertyChangesWithoutInWithCachedEventArgs()
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

        derivation.TextWithoutInAndCachedEventArgs = "text";
        Assert.AreEqual(0, mockLogger.Invocations.Count);
        derivation.TextWithoutInAndCachedEventArgs = "other text";
        mockLogger.VerifyLogDebug("\"Text\" property is changing from \"text\" to \"other text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"Text\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"Text\"");

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithCachedChangedEventArgsOnly()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithCachedChangedEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithCachedChangingEventArgsOnly()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithCachedChangingEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithCachedEventArgsAndNullComparer()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithCachedEventArgsAndNullComparer = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithoutInWithCachedChangedEventArgsOnly()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithoutInWithCachedChangedEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithoutInWithCachedChangingEventArgsOnly()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithoutInWithCachedChangingEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithoutInWithCachedEventArgsAndNullComparer()
    {
        new Derivation(new Mock<ILogger<Derivation>>().Object, "text")
        {
            TextWithoutInWithCachedEventArgsAndNullComparer = "text"
        };
    }

    [TestMethod]
    public void PropertyChangesWithoutIn()
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

        derivation.TextWithoutIn = "text";
        Assert.AreEqual(0, mockLogger.Invocations.Count);
        derivation.TextWithoutIn = "other text";
        mockLogger.VerifyLogDebug("\"TextWithoutIn\" property is changing from \"text\" to \"other text\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"TextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"TextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"TextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"TextWithoutIn\"");
        mockLogger.Invocations.Clear();
        derivation.NullableTextWithoutIn = "Suprise!";
        mockLogger.VerifyLogDebug("\"NullableTextWithoutIn\" property is changing from \"(null)\" to \"Suprise!\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"NullableTextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"NullableTextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"NullableTextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"NullableTextWithoutIn\"");
        mockLogger.Invocations.Clear();
        derivation.NullableTextWithoutIn = null;
        mockLogger.VerifyLogDebug("\"NullableTextWithoutIn\" property is changing from \"Suprise!\" to \"(null)\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanging event for property \"NullableTextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanging event for property \"NullableTextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raising PropertyChanged event for property \"NullableTextWithoutIn\"");
        mockLogger.VerifyLogDebug("Raised PropertyChanged event for property \"NullableTextWithoutIn\"");
        mockLogger.Invocations.Clear();

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }
}