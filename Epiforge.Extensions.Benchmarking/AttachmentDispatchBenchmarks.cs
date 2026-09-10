namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Puts the attachment list a source keeps today against a name-indexed candidate, in isolation, before either is wired into anything
/// </summary>
/// <remarks>
/// <c>2026-09-09-the-index-that-cost-more-than-it-saved.md</c> records an index which was justified by measuring only what it removed, built, wired into two query classes, and reverted the same day. The lesson it names is that the instrument which would have prevented it is two candidate implementations measured head to head before either is wired in. This is that instrument, for the name index which <c>2026-09-09-what-a-raise-costs-many-sources.md</c> left justified by one shape and unpriced on its own cost
/// </remarks>
/// <remarks>
/// What is compared is the structure and nothing else: an attachment here counts a notification rather than evaluating anything, so no observation, expression or propagation is in the figures. Both candidates carry the same attachment objects and the same wanting test, and both maintain their structure on every attach and detach, which is the half of the substitution the reverted index never measured
/// </remarks>
/// <remarks>
/// <see cref="Names" /> is how many distinct properties the attachments are spread across. One is the fan-out shape, where every attachment wants the same name and an index can divide nothing on a wanted raise; six is a row of columns. <see cref="Attachments" /> crosses the range between a grid's handful and the fan-out instrument's thousand, which is where the threshold below which no index should be built has to be read from
/// </remarks>
[MemoryDiagnoser]
public class AttachmentDispatchBenchmarks
{
    const int raises = 1000;
    const string unwantedName = "Unwanted";

    static readonly string[] names = ["Amount", "Duration", "End", "Rate", "Start", "Weight"];

    sealed class MimicAttachment
    {
        internal MimicAttachment(string propertyName) =>
            PropertyName = propertyName;

        internal volatile bool IsRemoved;
        internal MimicAttachment? Next;
        internal MimicAttachment? NextInBucket;
        internal int Notifications;
        internal MimicAttachment? Previous;
        internal MimicAttachment? PreviousInBucket;
        internal readonly string PropertyName;

        internal void Notify() =>
            ++Notifications;

        internal bool Wants(string? reportedName) =>
            string.IsNullOrEmpty(reportedName) || reportedName == PropertyName;
    }

    /// <summary>
    /// The structure a source keeps today, which is one list walked to the first attachment wanting the reported name and then to the end
    /// </summary>
    sealed class MimicListSource
    {
        MimicAttachment? firstAttachment;
        MimicAttachment? lastAttachment;

        internal void Attach(MimicAttachment attachment)
        {
            attachment.Previous = lastAttachment;
            if (lastAttachment is null)
                Volatile.Write(ref firstAttachment, attachment);
            else
                lastAttachment.Next = attachment;
            lastAttachment = attachment;
        }

        internal void Detach(MimicAttachment attachment)
        {
            if (attachment.IsRemoved)
                return;
            attachment.IsRemoved = true;
            if (attachment.Previous is null)
                Volatile.Write(ref firstAttachment, attachment.Next);
            else
                attachment.Previous.Next = attachment.Next;
            if (attachment.Next is null)
                lastAttachment = attachment.Previous;
            else
                attachment.Next.Previous = attachment.Previous;
            attachment.Previous = null;
        }

        internal void Raise(string reportedName)
        {
            var current = Volatile.Read(ref firstAttachment);
            while (current is not null && (current.IsRemoved || !current.Wants(reportedName)))
                current = current.Next;
            if (current is null)
                return;
            while (current is not null)
            {
                var following = current.Next;
                if (!current.IsRemoved && current.Wants(reportedName))
                    current.Notify();
                current = following;
            }
        }
    }

    /// <summary>
    /// The candidate, which keeps the same list for the report naming every property and a bucket per name for the reports naming one
    /// </summary>
    sealed class MimicIndexedSource
    {
        sealed class Bucket
        {
            internal MimicAttachment? First;
            internal MimicAttachment? Last;
        }

        readonly Dictionary<string, Bucket> buckets = [];
        MimicAttachment? firstAttachment;
        MimicAttachment? lastAttachment;

        internal void Attach(MimicAttachment attachment)
        {
            attachment.Previous = lastAttachment;
            if (lastAttachment is null)
                Volatile.Write(ref firstAttachment, attachment);
            else
                lastAttachment.Next = attachment;
            lastAttachment = attachment;
            if (!buckets.TryGetValue(attachment.PropertyName, out var bucket))
            {
                bucket = new Bucket();
                buckets.Add(attachment.PropertyName, bucket);
            }
            attachment.PreviousInBucket = bucket.Last;
            if (bucket.Last is null)
                bucket.First = attachment;
            else
                bucket.Last.NextInBucket = attachment;
            bucket.Last = attachment;
        }

        internal void Detach(MimicAttachment attachment)
        {
            if (attachment.IsRemoved)
                return;
            attachment.IsRemoved = true;
            if (attachment.Previous is null)
                Volatile.Write(ref firstAttachment, attachment.Next);
            else
                attachment.Previous.Next = attachment.Next;
            if (attachment.Next is null)
                lastAttachment = attachment.Previous;
            else
                attachment.Next.Previous = attachment.Previous;
            attachment.Previous = null;
            if (!buckets.TryGetValue(attachment.PropertyName, out var bucket))
                return;
            if (attachment.PreviousInBucket is null)
                bucket.First = attachment.NextInBucket;
            else
                attachment.PreviousInBucket.NextInBucket = attachment.NextInBucket;
            if (attachment.NextInBucket is null)
                bucket.Last = attachment.PreviousInBucket;
            else
                attachment.NextInBucket.PreviousInBucket = attachment.PreviousInBucket;
            attachment.PreviousInBucket = null;
            if (bucket.First is null)
                buckets.Remove(attachment.PropertyName);
        }

        internal void Raise(string reportedName)
        {
            if (string.IsNullOrEmpty(reportedName))
            {
                var everything = Volatile.Read(ref firstAttachment);
                while (everything is not null)
                {
                    var following = everything.Next;
                    if (!everything.IsRemoved)
                        everything.Notify();
                    everything = following;
                }
                return;
            }
            if (!buckets.TryGetValue(reportedName, out var bucket))
                return;
            var current = bucket.First;
            while (current is not null)
            {
                var following = current.NextInBucket;
                if (!current.IsRemoved)
                    current.Notify();
                current = following;
            }
        }
    }

    MimicIndexedSource indexed = null!;
    MimicListSource list = null!;

    [Params(4, 16, 64, 256, 1024)]
    public int Attachments { get; set; }

    [Params(1, 6)]
    public int Names { get; set; }

    string NameOf(int which) =>
        names[which % Names];

    [Benchmark(Baseline = true)]
    public void ListAttachAndDetach()
    {
        var source = new MimicListSource();
        var attaching = new MimicAttachment[Attachments];
        for (var i = 0; i < Attachments; ++i)
            source.Attach(attaching[i] = new MimicAttachment(NameOf(i)));
        for (var i = 0; i < Attachments; ++i)
            source.Detach(attaching[i]);
    }

    [Benchmark]
    public void IndexedAttachAndDetach()
    {
        var source = new MimicIndexedSource();
        var attaching = new MimicAttachment[Attachments];
        for (var i = 0; i < Attachments; ++i)
            source.Attach(attaching[i] = new MimicAttachment(NameOf(i)));
        for (var i = 0; i < Attachments; ++i)
            source.Detach(attaching[i]);
    }

    [Benchmark]
    public void ListWantedRaise()
    {
        for (var i = 0; i < raises; ++i)
            list.Raise(names[0]);
    }

    [Benchmark]
    public void IndexedWantedRaise()
    {
        for (var i = 0; i < raises; ++i)
            indexed.Raise(names[0]);
    }

    [Benchmark]
    public void ListUnwantedRaise()
    {
        for (var i = 0; i < raises; ++i)
            list.Raise(unwantedName);
    }

    [Benchmark]
    public void IndexedUnwantedRaise()
    {
        for (var i = 0; i < raises; ++i)
            indexed.Raise(unwantedName);
    }

    [GlobalSetup]
    public void Setup()
    {
        indexed = new MimicIndexedSource();
        list = new MimicListSource();
        for (var i = 0; i < Attachments; ++i)
        {
            list.Attach(new MimicAttachment(NameOf(i)));
            indexed.Attach(new MimicAttachment(NameOf(i)));
        }
    }
}
