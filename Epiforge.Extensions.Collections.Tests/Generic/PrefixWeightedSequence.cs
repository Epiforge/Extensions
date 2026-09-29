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

        var finger = random.Next(reference.Count);
        var fingerPrefix = 0;
        for (var i = 0; i < finger; ++i)
            fingerPrefix += reference[i].Weight;
        Assert.AreSame(nodes[position], sequence.NodeAtFrom(nodes[finger], finger, position), $"{context}: NodeAtFrom({finger}, {position}) diverged");

        var boundary = random.Next(reference.Count + 1);
        var expectedPrefix = 0;
        for (var i = 0; i < boundary; ++i)
            expectedPrefix += reference[i].Weight;
        Assert.AreEqual(expectedPrefix, sequence.PrefixWeightBefore(boundary), $"{context}: PrefixWeightBefore({boundary}) diverged");
        if (boundary < reference.Count)
            Assert.AreEqual(expectedPrefix, sequence.PrefixWeightBefore(nodes[boundary]), $"{context}: PrefixWeightBefore(node) diverged at {boundary}");

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
            Assert.AreSame(nodes[expectedIndex], sequence.NodeAtWeightFrom(nodes[finger], fingerPrefix, offset), $"{context}: NodeAtWeightFrom({finger}, {offset}) diverged");
        }
        Assert.IsNull(sequence.NodeAtWeight(totalWeight), $"{context}: an offset past the total weight yielded a node");
        Assert.IsNull(sequence.NodeAtWeightFrom(nodes[finger], fingerPrefix, totalWeight), $"{context}: an offset past the total weight yielded a node to a finger search");
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
                var precedingWeight = 0;
                for (var i = 0; i < index; ++i)
                    precedingWeight += reference[i].Weight;
                if (random.Next(2) == 0)
                    sequence.SetWeight(nodes[index], weight);
                else
                {
                    sequence.SetWeight(nodes[index], weight, out var reportedPrecedingWeight);
                    Assert.AreEqual(precedingWeight, reportedPrecedingWeight, $"seed {seed}, operation {operation}: SetWeight({index}, {weight}) reported the wrong preceding weight");
                }
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
    public void MovingANodeAgreesWithAListOfTheSameContent()
    {
        for (var seed = 0; seed < 15; ++seed)
        {
            var random = new Random(seed);
            var sequence = new PrefixWeightedSequence<int>();
            var reference = new List<(int Item, int Weight)>();
            var nodes = new List<PrefixWeightedSequenceNode<int>>();
            for (var item = 0; item < 200; ++item)
            {
                var weight = random.Next(4);
                nodes.Add(sequence.Insert(item, item, weight));
                reference.Add((item, weight));
            }
            for (var operation = 0; operation < 2000; ++operation)
            {
                var count = reference.Count;
                string performed;
                if (random.Next(10) == 0)
                {
                    var index = random.Next(count);
                    var weight = random.Next(4);
                    sequence.SetWeight(nodes[index], weight);
                    reference[index] = (reference[index].Item, weight);
                    performed = $"set the weight at {index} to {weight}";
                }
                else
                {
                    var oldIndex = random.Next(count);
                    var newIndex = random.Next(3) == 0 ? Math.Clamp(oldIndex + random.Next(-2, 3), 0, count - 1) : random.Next(count);
                    var node = nodes[oldIndex];
                    sequence.Move(node, newIndex);
                    var entry = reference[oldIndex];
                    reference.RemoveAt(oldIndex);
                    reference.Insert(newIndex, entry);
                    nodes.RemoveAt(oldIndex);
                    nodes.Insert(newIndex, node);
                    Assert.AreEqual(newIndex, sequence.IndexOf(node), $"seed {seed}, operation {operation}: the moved node is not where it was moved to");
                    performed = $"move the node at {oldIndex} to {newIndex}";
                }
                CheckAgainstReference(sequence, reference, nodes, random, $"seed {seed}, operation {operation}, after {performed}");
            }
        }
    }

    [TestMethod]
    public void MovingANodeValidatesItsArguments()
    {
        var sequence = new PrefixWeightedSequence<int>();
        var first = sequence.Insert(0, 1, 1);
        var second = sequence.Insert(1, 2, 1);
        Assert.ThrowsException<ArgumentNullException>(() => sequence.Move(null!, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => sequence.Move(first, -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => sequence.Move(first, 2));
        sequence.Move(first, 1);
        Assert.AreSame(second, sequence.NodeAt(0));
        Assert.AreSame(first, sequence.NodeAt(1));
        Assert.AreEqual(1, sequence.PrefixWeightBefore(first));
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

    [TestMethod]
    [Timeout(300000)]
    public void FingerSearchFromEveryStartingPointAgreesWithSearchFromTheRoot()
    {
        var random = new Random(31);
        var sequence = new PrefixWeightedSequence<int>();
        var nodes = new List<PrefixWeightedSequenceNode<int>>();
        var prefixWeights = new List<int>();
        var totalWeight = 0;
        for (var i = 0; i < 400; ++i)
        {
            var weight = random.Next(3);
            nodes.Add(sequence.Insert(i, i, weight));
            prefixWeights.Add(totalWeight);
            totalWeight += weight;
        }
        for (var finger = 0; finger < nodes.Count; ++finger)
        {
            for (var target = 0; target < nodes.Count; ++target)
                Assert.AreSame(nodes[target], sequence.NodeAtFrom(nodes[finger], finger, target), $"the finger at {finger} found the wrong node at {target}");
            for (var offset = 0; offset < totalWeight; ++offset)
                Assert.AreSame(sequence.NodeAtWeight(offset), sequence.NodeAtWeightFrom(nodes[finger], prefixWeights[finger], offset), $"the finger at {finger} found the wrong node at the offset {offset}");
            Assert.IsNull(sequence.NodeAtWeightFrom(nodes[finger], prefixWeights[finger], totalWeight), $"the finger at {finger} found a node past the total weight");
            Assert.IsNull(sequence.NodeAtWeightFrom(nodes[finger], prefixWeights[finger], -1), $"the finger at {finger} found a node before the sequence");
        }
    }

    [TestMethod]
    [Timeout(300000)]
    public void FingerSearchClimbsOverRunsOfZeroWeight()
    {
        var sequence = new PrefixWeightedSequence<int>();
        var carrying = new List<PrefixWeightedSequenceNode<int>>();
        for (var i = 0; i < 20000; ++i)
        {
            var node = sequence.Insert(i, i, i % 128 == 0 ? 1 : 0);
            if (i % 128 == 0)
                carrying.Add(node);
        }
        Assert.AreEqual(carrying.Count, sequence.TotalWeight);
        PrefixWeightedSequenceNode<int>? finger = null;
        var fingerOffset = -1;
        for (var offset = 0; offset < carrying.Count; ++offset)
        {
            var found = finger is null ? sequence.NodeAtWeight(offset) : sequence.NodeAtWeightFrom(finger, fingerOffset, offset);
            Assert.AreSame(carrying[offset], found, $"the offset {offset} landed on the wrong node");
            finger = found;
            fingerOffset = offset;
        }
        Assert.IsNull(sequence.NodeAtWeightFrom(finger!, fingerOffset, carrying.Count), "an offset past the total weight yielded a node");
    }

    [TestMethod]
    [Timeout(300000)]
    public void FingerSearchSurvivesMutationOfTheSequenceAroundTheFinger()
    {
        var random = new Random(97);
        var sequence = new PrefixWeightedSequence<int>();
        var nodes = new List<PrefixWeightedSequenceNode<int>>();
        for (var i = 0; i < 600; ++i)
            nodes.Add(sequence.Insert(i, i, 1));
        for (var round = 0; round < 400; ++round)
        {
            var index = random.Next(nodes.Count);
            if (random.Next(2) == 0)
            {
                sequence.RemoveAt(index);
                nodes.RemoveAt(index);
            }
            else
            {
                nodes.Insert(index, sequence.Insert(index, 1000 + round, 1));
            }
            var finger = random.Next(nodes.Count);
            var target = random.Next(nodes.Count);
            Assert.AreSame(nodes[target], sequence.NodeAtFrom(nodes[finger], finger, target), $"round {round}: the finger at {finger} found the wrong node at {target}");
            Assert.AreSame(nodes[target], sequence.NodeAtWeightFrom(nodes[finger], finger, target), $"round {round}: the finger at {finger} found the wrong node at the offset {target}");
        }
    }

    [TestMethod]
    [Timeout(300000)]
    public void APrefixWeightBeforeANullNodeIsRejected()
    {
        var sequence = new PrefixWeightedSequence<int>();
        Assert.ThrowsException<ArgumentNullException>(() => _ = sequence.PrefixWeightBefore(null!));
    }

    [TestMethod]
    public void CountingWhileATestHoldsAgreesWithAListOfTheSameContent()
    {
        var random = new Random(4242);
        for (var trial = 0; trial < 200; ++trial)
        {
            var sequence = new PrefixWeightedSequence<int>();
            var reference = new List<int>();
            var size = random.Next(200);
            for (var i = 0; i < size; ++i)
            {
                var value = random.Next(50);
                var index = reference.BinarySearch(value);
                if (index < 0)
                    index = ~index;
                reference.Insert(index, value);
                sequence.Insert(index, value, random.Next(3));
            }
            for (var threshold = -1; threshold <= 51; ++threshold)
            {
                Assert.AreEqual(reference.Count(item => item < threshold), sequence.CountWhile(static (bound, item) => item < bound, threshold), $"trial {trial}: fewer than {threshold}");
                Assert.AreEqual(reference.Count(item => item <= threshold), sequence.CountWhile(static (bound, item) => item <= bound, threshold), $"trial {trial}: at most {threshold}");
            }
        }
    }

    [TestMethod]
    public void CountingWhileValidatesItsArguments()
    {
        var sequence = new PrefixWeightedSequence<int>();
        Assert.AreEqual(0, sequence.CountWhile(static (_, _) => true, 0));
        sequence.Insert(0, 42, 1);
        Assert.ThrowsException<ArgumentNullException>(() => _ = sequence.CountWhile<int>(null!, 0));
        Assert.AreEqual(1, sequence.CountWhile(static (_, _) => true, 0));
        Assert.AreEqual(0, sequence.CountWhile(static (_, _) => false, 0));
    }

    [TestMethod]
    public void FingerSearchValidatesItsArguments()
    {
        var sequence = new PrefixWeightedSequence<int>();
        var only = sequence.Insert(0, 42, 1);
        Assert.ThrowsException<ArgumentNullException>(() => _ = sequence.NodeAtFrom(null!, 0, 0));
        Assert.ThrowsException<ArgumentNullException>(() => _ = sequence.NodeAtWeightFrom(null!, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = sequence.NodeAtFrom(only, 0, -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = sequence.NodeAtFrom(only, 0, 1));
        Assert.AreSame(only, sequence.NodeAtFrom(only, 0, 0));
        Assert.AreSame(only, sequence.NodeAtWeightFrom(only, 0, 0));
        Assert.IsNull(sequence.NodeAtWeightFrom(only, 0, -1));
        Assert.IsNull(sequence.NodeAtWeightFrom(only, 0, 1));
    }
}
