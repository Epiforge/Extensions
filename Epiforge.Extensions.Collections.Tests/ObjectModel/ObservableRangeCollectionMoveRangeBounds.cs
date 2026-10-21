namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

[TestClass]
public class ObservableRangeCollectionMoveRangeBounds
{
    [TestMethod]
    [DataRow(5, 0, 1, false)]
    [DataRow(5, 0, 1, true)]
    [DataRow(-1, 0, 1, false)]
    [DataRow(-1, 0, 1, true)]
    [DataRow(0, 5, 1, false)]
    [DataRow(0, 5, 1, true)]
    [DataRow(4, 0, 2, false)]
    [DataRow(4, 0, 2, true)]
    public void AnIndexOutsideTheCollectionIsRefusedAsAnArgumentOutOfRange(int oldStartIndex, int newStartIndex, int count, bool individually)
    {
        var collection = new ObservableRangeCollection<int>(Enumerable.Range(0, 5), individually);
        var announcements = 0;
        collection.CollectionChanged += (sender, e) => ++announcements;
        Exception? refusal = null;
        try
        {
            collection.MoveRange(oldStartIndex, newStartIndex, count);
        }
        catch (Exception ex)
        {
            refusal = ex;
        }
        Assert.IsInstanceOfType<ArgumentOutOfRangeException>(refusal);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, collection.ToArray());
        Assert.AreEqual(0, announcements);
    }
}
