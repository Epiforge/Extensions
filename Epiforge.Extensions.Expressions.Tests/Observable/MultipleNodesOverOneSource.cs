namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class MultipleNodesOverOneSource
{
    public sealed class Pair :
        INotifyPropertyChanged
    {
        int rank = 1;
        int score = 2;

        public int Rank =>
            rank;

        public int Score =>
            score;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetBothAndAnnounceOnce(int newRank, int newScore)
        {
            rank = newRank;
            score = newScore;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    [TestMethod]
    public void NoAnnouncedValueIsOneTheObjectWasNeverIn()
    {
        var pair = new Pair();
        var announced = new List<int>();
        var observer = ExpressionObserverHelpers.Create(new ExpressionObserverOptions { UseDirectSubscription = false });
        using (var expr = observer.Observe(p => p.Rank + p.Score, pair))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    announced.Add(expr.Evaluation.Result);
            }

            expr.PropertyChanged += propertyChanged;
            pair.SetBothAndAnnounceOnce(10, 20);
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual("30", string.Join(", ", announced));
    }
}
