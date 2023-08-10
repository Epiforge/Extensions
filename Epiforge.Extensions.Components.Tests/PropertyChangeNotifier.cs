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
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            NullableTexttWithNullEqualityComparer = "bruh"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullEqualityComparerWithoutIn()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithoutInAndNullComparer = "bruh"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangedEventArgs()
    {
        var derivation = new Derivation(Substitute.For<ILogger<Derivation>>(), "text");
        derivation.NullPropertyChangedEventArgs();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangedName()
    {
        var derivation = new Derivation(Substitute.For<ILogger<Derivation>>(), "text");
        derivation.NullPropertyChangedName();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangingEventArgs()
    {
        var derivation = new Derivation(Substitute.For<ILogger<Derivation>>(), "text");
        derivation.NullPropertyChangingEventArgs();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullPropertyChangingName()
    {
        var derivation = new Derivation(Substitute.For<ILogger<Derivation>>(), "text");
        derivation.NullPropertyChangingName();
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var propertiesChanged = new List<string>();
        var propertiesChanging = new List<string>();
        var logger = Substitute.For<MockLogger<Derivation>>();
        var derivation = new Derivation(logger, "text");

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
        Assert.AreEqual(0, logger.ReceivedCalls().Count());
        derivation.Text = "other text";
        logger.ReceivedLogDebug("\"Text\" property is changing from \"text\" to \"other text\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"Text\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"Text\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"Text\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"Text\"");
        logger.ClearReceivedCalls();
        derivation.NullableText = "Suprise!";
        logger.ReceivedLogDebug("\"NullableText\" property is changing from \"(null)\" to \"Suprise!\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"NullableText\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"NullableText\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"NullableText\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"NullableText\"");
        logger.ClearReceivedCalls();
        derivation.NullableText = null;
        logger.ReceivedLogDebug("\"NullableText\" property is changing from \"Suprise!\" to \"(null)\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"NullableText\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"NullableText\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"NullableText\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"NullableText\"");
        logger.ClearReceivedCalls();

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }

    [TestMethod]
    public void PropertyChangesWithCachedEventArgs()
    {
        var propertiesChanged = new List<string>();
        var propertiesChanging = new List<string>();
        var logger = Substitute.For<MockLogger<Derivation>>();
        var derivation = new Derivation(logger, "text");

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
        Assert.AreEqual(0, logger.ReceivedCalls().Count());
        derivation.TextWithCachedEventArgs = "other text";
        logger.ReceivedLogDebug("\"Text\" property is changing from \"text\" to \"other text\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"Text\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"Text\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"Text\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"Text\"");

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }

    [TestMethod]
    public void PropertyChangesWithoutInWithCachedEventArgs()
    {
        var propertiesChanged = new List<string>();
        var propertiesChanging = new List<string>();
        var logger = Substitute.For<MockLogger<Derivation>>();
        var derivation = new Derivation(logger, "text");

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
        Assert.AreEqual(0, logger.ReceivedCalls().Count());
        derivation.TextWithoutInAndCachedEventArgs = "other text";
        logger.ReceivedLogDebug("\"Text\" property is changing from \"text\" to \"other text\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"Text\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"Text\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"Text\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"Text\"");

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithCachedChangedEventArgsOnly()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithCachedChangedEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithCachedChangingEventArgsOnly()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithCachedChangingEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithCachedEventArgsAndNullComparer()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithCachedEventArgsAndNullComparer = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithoutInWithCachedChangedEventArgsOnly()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithoutInWithCachedChangedEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithoutInWithCachedChangingEventArgsOnly()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithoutInWithCachedChangingEventArgsOnly = "text"
        };
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PropertyChangesWithoutInWithCachedEventArgsAndNullComparer()
    {
        new Derivation(Substitute.For<ILogger<Derivation>>(), "text")
        {
            TextWithoutInWithCachedEventArgsAndNullComparer = "text"
        };
    }

    [TestMethod]
    public void PropertyChangesWithoutIn()
    {
        var propertiesChanged = new List<string>();
        var propertiesChanging = new List<string>();
        var logger = Substitute.For<MockLogger<Derivation>>();
        var derivation = new Derivation(logger, "text");

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
        Assert.AreEqual(0, logger.ReceivedCalls().Count());
        derivation.TextWithoutIn = "other text";
        logger.ReceivedLogDebug("\"TextWithoutIn\" property is changing from \"text\" to \"other text\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"TextWithoutIn\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"TextWithoutIn\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"TextWithoutIn\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"TextWithoutIn\"");
        logger.ClearReceivedCalls();
        derivation.NullableTextWithoutIn = "Suprise!";
        logger.ReceivedLogDebug("\"NullableTextWithoutIn\" property is changing from \"(null)\" to \"Suprise!\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"NullableTextWithoutIn\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"NullableTextWithoutIn\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"NullableTextWithoutIn\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"NullableTextWithoutIn\"");
        logger.ClearReceivedCalls();
        derivation.NullableTextWithoutIn = null;
        logger.ReceivedLogDebug("\"NullableTextWithoutIn\" property is changing from \"Suprise!\" to \"(null)\"");
        logger.ReceivedLogDebug("Raising PropertyChanging event for property \"NullableTextWithoutIn\"");
        logger.ReceivedLogDebug("Raised PropertyChanging event for property \"NullableTextWithoutIn\"");
        logger.ReceivedLogDebug("Raising PropertyChanged event for property \"NullableTextWithoutIn\"");
        logger.ReceivedLogDebug("Raised PropertyChanged event for property \"NullableTextWithoutIn\"");
        logger.ClearReceivedCalls();

        derivation.PropertyChanged -= propertyChanged;
        derivation.PropertyChanging -= propertyChanging;
    }
}