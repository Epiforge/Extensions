namespace Epiforge.Extensions.Collections.Tests.Generic;

[TestClass]
public class PrefixWeightedSequence
{
    static void CheckAgainstReference(PrefixWeightedSequence<int> sequence, List<(int Item, int Weight)> reference, List<PrefixWeightedSequenceNode<int>> nodes, Random random, string context)
    {
        Assert.AreEqual(reference.Count, sequence.Count, $"{context}: count diverged");
        Assert.AreEqual(reference.Sum(entry => entry.Weight), sequence.TotalWeight, $"{context}: total weight diverged");

        var traversed = new List<(int Item, int Weight)>();
        for (var node = sequence.FirstNode; node is not null; node = sequence.Next(node))
            traversed.Add((node.Item, node.Weight));
        CollectionAssert.AreEqual(reference, traversed, $"{context}: traversal diverged");

        if (reference.Count == 0)
        {
            Assert.IsNull(sequence.NodeAtWeight(0), $"{context}: an empty sequence yielded a node");
            return;
        }

        var position = random.Next(reference.Count);
        Assert.AreSame(nodes[position], sequence.NodeAt(position), $"{context}: NodeAt({position}) diverged");
        Assert.AreEqual(position, sequence.IndexOf(nodes[position]), $"{context}: IndexOf diverged at {position}");

        var boundary = random.Next(reference.Count + 1);
        var expectedPrefix = 0;
        for (var i = 0; i < boundary; ++i)
            expectedPrefix += reference[i].Weight;
        Assert.AreEqual(expectedPrefix, sequence.PrefixWeightBefore(boundary), $"{context}: PrefixWeightBefore({boundary}) diverged");

        var totalWeight = reference.Sum(entry => entry.Weight);
        if (totalWeight > 0)
        {
            var offset = random.Next(totalWeight);
            var spanned = 0;
            var expectedIndex = -1;
            for (var i = 0; i < reference.Count; ++i)
            {
                if (offset < spanned + reference[i].Weight)
                {
                    expectedIndex = i;
                    break;
                }
                spanned += reference[i].Weight;
            }
            Assert.AreSame(nodes[expectedIndex], sequence.NodeAtWeight(offset), $"{context}: NodeAtWeight({offset}) diverged");
        }
        Assert.IsNull(sequence.NodeAtWeight(totalWeight), $"{context}: an offset past the total weight yielded a node");
    }

    [TestMethod]
    [Timeout(300000)]
    public void RandomOperationsAgreeWithAListOfTheSameContent()
    {
        for (var seed = 0; seed < 15; ++seed)
            RunSeed(seed, 2000);
    }

    static void RunSeed(int seed, int operations)
    {
        var random = new Random(seed);
        var sequence = new PrefixWeightedSequence<int>();
        var reference = new List<(int Item, int Weight)>();
        var nodes = new List<PrefixWeightedSequenceNode<int>>();
        var nextItem = 0;

        for (var operation = 0; operation < operations; ++operation)
        {
            var count = reference.Count;
            var choice = random.Next(100);
            string performed;
            if (count == 0 || choice < 45)
            {
                var index = random.Next(count + 1);
                var weight = random.Next(4);
                var node = sequence.Insert(index, nextItem, weight);
                reference.Insert(index, (nextItem, weight));
                nodes.Insert(index, node);
                performed = $"insert item {nextItem} weighing {weight} at {index}";
                ++nextItem;
            }
            else if (choice < 70)
            {
                var index = random.Next(count);
                var removed = sequence.RemoveAt(index);
                Assert.AreEqual(reference[index].Item, removed.Item, $"seed {seed}, operation {operation}: RemoveAt({index}) returned the wrong node");
                reference.RemoveAt(index);
                nodes.RemoveAt(index);
                performed = $"remove at {index}";
            }
            else if (choice < 85)
            {
                var index = random.Next(count);
                var weight = random.Next(4);
                sequence.SetWeight(nodes[index], weight);
                reference[index] = (reference[index].Item, weight);
                performed = $"set the weight at {index} to {weight}";
            }
            else
            {
                var moveCount = random.Next(1, Math.Min(5, count) + 1);
                var oldIndex = random.Next(count - moveCount + 1);
                var newIndex = random.Next(count - moveCount + 1);
                sequence.MoveRange(oldIndex, newIndex, moveCount);
                var movedEntries = reference.GetRange(oldIndex, moveCount);
                reference.RemoveRange(oldIndex, moveCount);
                reference.InsertRange(newIndex, movedEntries);
                var movedNodes = nodes.GetRange(oldIndex, moveCount);
                nodes.RemoveRange(oldIndex, moveCount);
                nodes.InsertRange(newIndex, movedNodes);
                performed = $"move {moveCount} from {oldIndex} to {newIndex}";
            }
            CheckAgainstReference(sequence, reference, nodes, random, $"seed {seed}, operation {operation}, after {performed}");
        }
    }

    [TestMethod]
    [Timeout(300000)]
    public void ClearingEmptiesTheSequence()
    {
        var sequence = new PrefixWeightedSequence<int>();
        for (var i = 0; i < 500; ++i)
            sequence.Insert(i, i, i % 2);
        Assert.AreEqual(500, sequence.Count);
        Assert.AreEqual(250, sequence.TotalWeight);
        sequence.Clear();
        Assert.AreEqual(0, sequence.Count);
        Assert.AreEqual(0, sequence.TotalWeight);
        Assert.IsNull(sequence.FirstNode);
        Assert.IsNull(sequence.NodeAtWeight(0));
        var reinserted = sequence.Insert(0, 42, 3);
        Assert.AreEqual(1, sequence.Count);
        Assert.AreEqual(3, sequence.TotalWeight);
        Assert.AreEqual(0, sequence.IndexOf(reinserted));
        Assert.AreSame(reinserted, sequence.FirstNode);
    }

    [TestMethod]
    [Timeout(300000)]
    public void WeightsOfZeroAreNeverSpannedByAnOffset()
    {
        var sequence = new PrefixWeightedSequence<int>();
        var carrying = new List<PrefixWeightedSequenceNode<int>>();
        for (var i = 0; i < 200; ++i)
        {
            var node = sequence.Insert(i, i, i % 3 == 0 ? 1 : 0);
            if (i % 3 == 0)
                carrying.Add(node);
        }
        Assert.AreEqual(carrying.Count, sequence.TotalWeight);
        for (var offset = 0; offset < carrying.Count; ++offset)
            Assert.AreSame(carrying[offset], sequence.NodeAtWeight(offset), $"the offset {offset} landed on the wrong node");
        Assert.IsNull(sequence.NodeAtWeight(carrying.Count));
    }

    [TestMethod]
    [Timeout(300000)]
    public void PositionsRemainCorrectAfterManyInsertionsAtTheFront()
    {
        var sequence = new PrefixWeightedSequence<int>();
        var nodes = new List<PrefixWeightedSequenceNode<int>>();
        for (var i = 0; i < 5000; ++i)
            nodes.Insert(0, sequence.Insert(0, i, 1));
        Assert.AreEqual(5000, sequence.Count);
        Assert.AreEqual(5000, sequence.TotalWeight);
        for (var probe = 0; probe < 5000; probe += 137)
        {
            Assert.AreEqual(probe, sequence.IndexOf(nodes[probe]), $"the node at {probe} reported the wrong position");
            Assert.AreEqual(probe, sequence.PrefixWeightBefore(probe), $"the prefix weight before {probe} was wrong");
        }
    }
}
