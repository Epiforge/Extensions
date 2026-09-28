namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Expressions neither mechanism can observe, which are refused when the observation is requested
/// </summary>
[TestClass]
public class UnobservableExpressions
{
    /// <summary>
    /// A lambda passed as an argument, such as the predicate of <see cref="Enumerable.Count{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>, is refused by both mechanisms with a message naming the kind of expression refused
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ALambdaPassedAsAnArgumentIsRefusedByName(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var numbers = new ObservableRangeCollection<int>([1, -1]);
        var refusal = Assert.ThrowsExactly<NotSupportedException>(() => observer.Observe(() => numbers.Count(number => number > 0)));
        StringAssert.Contains(refusal.Message, nameof(ExpressionType.Lambda), $"the refusal does not name the kind of expression refused: {refusal.Message}");
    }
}
