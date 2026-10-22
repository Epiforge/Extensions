namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// What each mechanism reports for a property whose change notifications are ignored when it is read through something which can move
/// </summary>
/// <remarks>
/// An ignored property is treated as a static property is, as something nothing announces a change to: each mechanism captures it, keeps what it captured for as long as what it is read through stays put, and captures it again when that moves. When the capture happens is not promised for a value nothing announces, which is the contract <see cref="FieldTargets"/> pins for a member read through something which cannot announce. The graph captures when its node for what the property is read through announces, which it does by the equality of that thing's type and whether or not anything is reading the property at the time, and two observations of one read share its node; the fast path captures when an evaluation reaches the read through a different object. Rows where those moments differ assert capture-once rather than agreement, and rows where they cannot differ assert the value
/// </remarks>
[TestClass]
public class IgnoredNotificationAgreement
{
    public class Piece(SubscriptionLog log) :
        INotifyPropertyChanged
    {
        Piece? child;
        PropertyChangedEventHandler? propertyChanged;
        int rank;
        int score;

        public Piece? Child
        {
            get => child;
            set
            {
                child = value;
                propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }

        public int Rank
        {
            get => rank;
            set
            {
                rank = value;
                propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rank)));
            }
        }

        public int Score
        {
            get => score;
            set
            {
                score = value;
                propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Score)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                log.Record(this, nameof(PropertyChanged), 1);
                propertyChanged += value;
            }
            remove
            {
                log.Record(this, nameof(PropertyChanged), -1);
                propertyChanged -= value;
            }
        }
    }

    /// <summary>
    /// A piece equal to every other of its kind, so that replacing one with another is not a change by the equality the graph's nodes compare with
    /// </summary>
    public sealed class AlikePiece(SubscriptionLog log) :
        Piece(log)
    {
        public override bool Equals(object? obj) =>
            obj is AlikePiece;

        public override int GetHashCode() =>
            0;
    }

    public sealed class Holder(SubscriptionLog log) :
        INotifyPropertyChanged
    {
        bool gateBroken;
        Piece? piece;
        bool pieceBroken;
        PropertyChangedEventHandler? propertyChanged;
        int score;

        public int Gate =>
            gateBroken ? throw new InvalidOperationException("the gate is broken") : 0;

        public Piece? Piece
        {
            get => pieceBroken ? throw new InvalidOperationException("the piece is broken") : piece;
            set
            {
                piece = value;
                Raise(nameof(Piece));
            }
        }

        public int Score
        {
            get => score;
            set
            {
                score = value;
                Raise(nameof(Score));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                log.Record(this, nameof(PropertyChanged), 1);
                propertyChanged += value;
            }
            remove
            {
                log.Record(this, nameof(PropertyChanged), -1);
                propertyChanged -= value;
            }
        }

        public void BreakGate()
        {
            gateBroken = true;
            Raise(nameof(Gate));
        }

        public void BreakPiece()
        {
            pieceBroken = true;
            Raise(nameof(Piece));
        }

        public void MendGate()
        {
            gateBroken = false;
            Raise(nameof(Gate));
        }

        public void MendPiece()
        {
            pieceBroken = false;
            Raise(nameof(Piece));
        }

        void Raise(string propertyName) =>
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// A holder which cannot notify, so that a piece read through it cannot move as far as either mechanism can know
    /// </summary>
    public sealed class SilentHolder
    {
        public Piece? Piece { get; set; }
    }

    static readonly PropertyInfo childProperty = typeof(Piece).GetProperty(nameof(Piece.Child))!;
    static readonly PropertyInfo rankProperty = typeof(Piece).GetProperty(nameof(Piece.Rank))!;

    static ExpressionObserverOptions Ignoring(params PropertyInfo[] properties)
    {
        var options = new ExpressionObserverOptions();
        foreach (var property in properties)
            options.AddIgnoredPropertyChangeNotification(property);
        return options;
    }

    static (IReadOnlyList<string> Built, IReadOnlyList<string> Moved, int Outstanding) AttachmentsOf(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var holder = new Holder(log) { Piece = new Piece(log) { Rank = 1 } };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        IReadOnlyList<string> built, moved;
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Score, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            built = log.Attachments();
            holder.Piece = new Piece(log) { Rank = 2 };
            Assert.AreEqual(2, expression.Evaluation.Result);
            moved = log.Attachments();
        }
        return (built, moved, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnIgnoredPropertyIsReadAgainOnlyWhenWhatItIsReadThroughMoves(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var first = new Piece(log) { Rank = 1 };
        var holder = new Holder(log) { Piece = first };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        var announcements = 0;
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Score, holder))
        {
            expression.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(expression.Evaluation))
                    ++announcements;
            };
            Assert.AreEqual(1, expression.Evaluation.Result);
            first.Rank = 5;
            Assert.AreEqual(1, expression.Evaluation.Result, "the property was read again when it announced");
            holder.Score = 1;
            Assert.AreEqual(2, expression.Evaluation.Result, "the property was read again when something else announced");
            var second = new Piece(log) { Rank = 7 };
            holder.Piece = second;
            Assert.AreEqual(8, expression.Evaluation.Result, "the property was not read again when what it is read through moved");
            second.Rank = 9;
            holder.Score = 2;
            Assert.AreEqual(9, expression.Evaluation.Result, "the property was read again after what it is read through had moved and something else announced");
            Assert.AreEqual(3, announcements, "the observation did not announce once for each change to its value");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// A replacement the graph's equality calls the same object, which the graph does not read through and the fast path, comparing objects, does
    /// </summary>
    /// <remarks>
    /// This asserts that each mechanism reports a value it read from one of the two pieces and keeps it until what the property is read through moves again, not which one. The graph's node for what the property is read through announces only when its evaluation changes by the equality of its type, and that is not particular to an ignored property: a property which is followed, read through such a replacement, is read from the replaced object by the graph and from the replacement by the fast path
    /// </remarks>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AReplacementEqualToWhatItReplacesIsCapturedOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var holder = new Holder(log) { Piece = new AlikePiece(log) { Rank = 1 } };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Score, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            var replacement = new AlikePiece(log) { Rank = 5 };
            holder.Piece = replacement;
            var captured = expression.Evaluation.Result;
            Assert.IsTrue(captured is 1 or 5, $"the property was read from neither piece, but gave {captured}");
            replacement.Rank = 6;
            holder.Score = 1;
            Assert.AreEqual(captured + 1, expression.Evaluation.Result, "the property was read again although nothing it is read through moved");
            holder.Piece = new Piece(log) { Rank = 7 };
            Assert.AreEqual(8, expression.Evaluation.Result, "the property was not read through a replacement unequal to what it replaced");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// A recovery to the object read through before the fault, after the property moved unannounced, which the graph reads through again and the fast path, seeing the same object, does not
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ARecoveryToTheSameObjectIsCapturedOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var first = new Piece(log) { Rank = 1 };
        var holder = new Holder(log) { Piece = first };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Score, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            first.Rank = 5;
            holder.BreakPiece();
            Assert.IsInstanceOfType<InvalidOperationException>(expression.Evaluation.Fault, "the observation did not report the fault of what the property is read through");
            holder.MendPiece();
            Assert.IsNull(expression.Evaluation.Fault, "the observation did not recover");
            var captured = expression.Evaluation.Result;
            Assert.IsTrue(captured is 1 or 5, $"the property was read as neither of its values, but gave {captured}");
            first.Rank = 9;
            holder.Score = 1;
            Assert.AreEqual(captured + 1, expression.Evaluation.Result, "the property was read again although nothing it is read through moved");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// A move made while the branch reading the property is not taken, which the graph reads through when it is made and the fast path when the branch is next taken
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AMoveWhileTheBranchIsNotTakenIsCapturedOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var holder = new Holder(log) { Piece = new Piece(log) { Rank = 1 }, Score = 1 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Score > 0 ? h.Piece!.Rank : -1, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            holder.Score = 0;
            Assert.AreEqual(-1, expression.Evaluation.Result);
            var second = new Piece(log) { Rank = 2 };
            holder.Piece = second;
            second.Rank = 3;
            holder.Score = 1;
            var captured = expression.Evaluation.Result;
            Assert.IsTrue(captured is 2 or 3, $"the property was not read through what it is read through now, but gave {captured}");
            second.Rank = 4;
            holder.Score = 2;
            Assert.AreEqual(captured, expression.Evaluation.Result, "the property was read again although nothing it is read through moved");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// A move made while an earlier operand faults, which the graph reads through when it is made and the fast path when the earlier operand recovers
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AMoveWhileAnEarlierOperandFaultsIsCapturedOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var holder = new Holder(log) { Piece = new Piece(log) { Rank = 1 } };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Gate + h.Piece!.Rank + h.Score, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            holder.BreakGate();
            Assert.IsInstanceOfType<InvalidOperationException>(expression.Evaluation.Fault);
            var second = new Piece(log) { Rank = 2 };
            holder.Piece = second;
            second.Rank = 3;
            holder.MendGate();
            Assert.IsNull(expression.Evaluation.Fault, "the observation did not recover");
            var captured = expression.Evaluation.Result;
            Assert.IsTrue(captured is 2 or 3, $"the property was not read through what it is read through now, but gave {captured}");
            second.Rank = 4;
            holder.Score = 1;
            Assert.AreEqual(captured + 1, expression.Evaluation.Result, "the property was read again although nothing it is read through moved");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ABranchNotYetTakenReadsThePropertyWhenFirstTaken(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var first = new Piece(log) { Rank = 1 };
        var holder = new Holder(log) { Piece = first };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Score > 0 ? h.Piece!.Rank : -1, holder))
        {
            Assert.AreEqual(-1, expression.Evaluation.Result);
            first.Rank = 4;
            var second = new Piece(log) { Rank = 5 };
            holder.Piece = second;
            second.Rank = 6;
            holder.Score = 1;
            Assert.AreEqual(6, expression.Evaluation.Result, "the property was read before the branch reading it was first taken");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnIgnoredPropertyReadThroughAnotherIsReadAgainOnlyWhenTheOuterTargetMoves(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var inner = new Piece(log) { Rank = 1 };
        var outer = new Piece(log) { Child = inner };
        var holder = new Holder(log) { Piece = outer };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty, childProperty));
        using (var expression = observer.Observe(h => h.Piece!.Child!.Rank + h.Score, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            inner.Rank = 2;
            outer.Child = new Piece(log) { Rank = 3 };
            holder.Score = 1;
            Assert.AreEqual(2, expression.Evaluation.Result, "an ignored property was read again although nothing it is read through moved");
            holder.Piece = new Piece(log) { Child = new Piece(log) { Rank = 4 } };
            Assert.AreEqual(5, expression.Evaluation.Result, "the ignored properties were not read again when the outermost thing they are read through moved");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    /// <summary>
    /// Two observations of one read, the later made after the property moved unannounced, which the graph serves from one cached node and the fast path reads for each
    /// </summary>
    /// <remarks>
    /// This asserts that each observation captures once and that both follow a move, not that the later reports what the earlier read
    /// </remarks>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoObservationsOfAReadThroughAMovingTargetEachCaptureOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var first = new Piece(log) { Rank = 1 };
        var holder = new Holder(log) { Piece = first };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        Expression<Func<Holder, int>> reading = h => h.Piece!.Rank + h.Score;
        using (var earlier = observer.Observe(reading, holder))
        {
            Assert.AreEqual(1, earlier.Evaluation.Result);
            first.Rank = 5;
            using (var later = observer.Observe(reading, holder))
            {
                var captured = later.Evaluation.Result;
                Assert.IsTrue(captured is 1 or 5, $"the later observation read the property as neither of its values, but gave {captured}");
                first.Rank = 9;
                holder.Score = 1;
                Assert.AreEqual(2, earlier.Evaluation.Result, "the earlier observation read the property again although nothing it is read through moved");
                Assert.AreEqual(captured + 1, later.Evaluation.Result, "the later observation read the property again although nothing it is read through moved");
                holder.Piece = new Piece(log) { Rank = 7 };
                Assert.AreEqual(8, earlier.Evaluation.Result);
                Assert.AreEqual(8, later.Evaluation.Result);
            }
        }
        Assert.AreEqual(0, log.Outstanding, "something the observations attached is still attached");
    }

    /// <summary>
    /// The same where nothing the property is read through can move
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoObservationsOfAReadThroughAnUnmovingTargetEachCaptureOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var piece = new Piece(log) { Rank = 1, Score = 1 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        Expression<Func<Piece, int>> reading = p => p.Rank + p.Score;
        using (var earlier = observer.Observe(reading, piece))
        {
            Assert.AreEqual(2, earlier.Evaluation.Result);
            piece.Rank = 5;
            using var later = observer.Observe(reading, piece);
            var captured = later.Evaluation.Result;
            Assert.IsTrue(captured is 2 or 6, $"the later observation read the property as neither of its values, but gave {captured}");
            piece.Rank = 9;
            piece.Score = 2;
            Assert.AreEqual(3, earlier.Evaluation.Result, "the earlier observation read the property again although nothing it is read through can move");
            Assert.AreEqual(captured + 1, later.Evaluation.Result, "the later observation read the property again although nothing it is read through can move");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observations attached is still attached");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnIgnoredPropertyReadThroughSomethingSilentIsReadOnce(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var piece = new Piece(log) { Rank = 1, Score = 1 };
        var holder = new SilentHolder { Piece = piece };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Piece!.Score, holder))
        {
            Assert.AreEqual(2, expression.Evaluation.Result);
            piece.Rank = 5;
            piece.Score = 2;
            Assert.AreEqual(3, expression.Evaluation.Result, "the property was read again although nothing it is read through can move");
        }
        Assert.AreEqual(0, log.Outstanding, "something the observation attached is still attached");
    }

    [TestMethod]
    public void TheMechanismsAttachAlikeForAnIgnoredPropertyReadThroughAMovingTarget()
    {
        var graph = AttachmentsOf(false);
        var fast = AttachmentsOf(true);
        CollectionAssert.AreEqual(graph.Built.ToArray(), fast.Built.ToArray(), $"the mechanisms attached differently when built: the graph [{string.Join(", ", graph.Built)}] and the fast path [{string.Join(", ", fast.Built)}]");
        CollectionAssert.AreEqual(graph.Moved.ToArray(), fast.Moved.ToArray(), $"the mechanisms attached differently after a move: the graph [{string.Join(", ", graph.Moved)}] and the fast path [{string.Join(", ", fast.Moved)}]");
        Assert.AreEqual(0, graph.Outstanding, "something the graph attached is still attached");
        Assert.AreEqual(0, fast.Outstanding, "something the fast path attached is still attached");
    }

    [TestMethod]
    public void AnIgnoredPropertyReadThroughAMovingTargetTakesTheFastPath()
    {
        var log = new SubscriptionLog();
        var holder = new Holder(log) { Piece = new Piece(log) { Rank = 1 } };
        var observer = ExpressionObserverHelpers.Create(true, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Score, holder))
        {
            Assert.AreEqual(1, expression.Evaluation.Result);
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused an ignored property read through something which can move");
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    public void AnIgnoredPropertyReadThroughSomethingSilentTakesTheFastPath()
    {
        var log = new SubscriptionLog();
        var holder = new SilentHolder { Piece = new Piece(log) { Rank = 1, Score = 1 } };
        var observer = ExpressionObserverHelpers.Create(true, Ignoring(rankProperty));
        using (var expression = observer.Observe(h => h.Piece!.Rank + h.Piece!.Score, holder))
        {
            Assert.AreEqual(2, expression.Evaluation.Result);
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer refused an ignored property read through something which cannot notify");
        }
        Assert.AreEqual(0, log.Outstanding);
    }
}
