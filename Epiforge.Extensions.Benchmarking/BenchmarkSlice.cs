namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Stands in for a row of a grid, whose columns are separate properties of one object and whose observations are therefore few per object and many objects wide
/// </summary>
/// <remarks>
/// The fan-out subject is the opposite shape: one object carrying a thousand attachments. An application laying out thousands of rows of half a dozen columns each carries a handful of attachments on each of thousands of objects, and what is cheap in the first shape can be dear in the second. This subject exists so that both shapes are measured rather than one of them assumed
/// </remarks>
/// <remarks>
/// It carries twelve observable columns so that the width of a row can be varied, because both mechanisms were found to charge per attachment on a source and the ratio between them therefore has a slope to confirm rather than a single point. It carries one property nothing observes, which is how a raise no column wants is priced
/// </remarks>
/// <remarks>
/// Its arguments are pre-allocated, so a raise allocates nothing and what the arms report is the mechanisms' own cost
/// </remarks>
public sealed class BenchmarkSlice :
    PropertyChangeNotifier
{
    static readonly PropertyChangedEventArgs amountChanged = new(nameof(Amount));
    static readonly PropertyChangingEventArgs amountChanging = new(nameof(Amount));
    static readonly PropertyChangedEventArgs billableChanged = new(nameof(Billable));
    static readonly PropertyChangingEventArgs billableChanging = new(nameof(Billable));
    static readonly PropertyChangedEventArgs costChanged = new(nameof(Cost));
    static readonly PropertyChangingEventArgs costChanging = new(nameof(Cost));
    static readonly PropertyChangedEventArgs durationChanged = new(nameof(Duration));
    static readonly PropertyChangingEventArgs durationChanging = new(nameof(Duration));
    static readonly PropertyChangedEventArgs endChanged = new(nameof(End));
    static readonly PropertyChangingEventArgs endChanging = new(nameof(End));
    static readonly PropertyChangedEventArgs idleChanged = new(nameof(Idle));
    static readonly PropertyChangingEventArgs idleChanging = new(nameof(Idle));
    static readonly PropertyChangedEventArgs marginChanged = new(nameof(Margin));
    static readonly PropertyChangingEventArgs marginChanging = new(nameof(Margin));
    static readonly PropertyChangedEventArgs offsetChanged = new(nameof(Offset));
    static readonly PropertyChangingEventArgs offsetChanging = new(nameof(Offset));
    static readonly PropertyChangedEventArgs rateChanged = new(nameof(Rate));
    static readonly PropertyChangingEventArgs rateChanging = new(nameof(Rate));
    static readonly PropertyChangedEventArgs startChanged = new(nameof(Start));
    static readonly PropertyChangingEventArgs startChanging = new(nameof(Start));
    static readonly PropertyChangedEventArgs taxChanged = new(nameof(Tax));
    static readonly PropertyChangingEventArgs taxChanging = new(nameof(Tax));
    static readonly PropertyChangedEventArgs totalChanged = new(nameof(Total));
    static readonly PropertyChangingEventArgs totalChanging = new(nameof(Total));
    static readonly PropertyChangedEventArgs weightChanged = new(nameof(Weight));
    static readonly PropertyChangingEventArgs weightChanging = new(nameof(Weight));

    long amount;
    long billable;
    long cost;
    long duration;
    long end;
    long idle;
    long margin;
    long offset;
    long rate;
    long start;
    long tax;
    long total;
    long weight;

    public long Amount
    {
        get => amount;
        set => SetBackedProperty(ref amount, in value, amountChanging, amountChanged);
    }

    public long Billable
    {
        get => billable;
        set => SetBackedProperty(ref billable, in value, billableChanging, billableChanged);
    }

    public long Cost
    {
        get => cost;
        set => SetBackedProperty(ref cost, in value, costChanging, costChanged);
    }

    public long Duration
    {
        get => duration;
        set => SetBackedProperty(ref duration, in value, durationChanging, durationChanged);
    }

    /// <summary>
    /// The column every arm raises, which one of the observations of a slice watches and the rest do not
    /// </summary>
    public long End
    {
        get => end;
        set => SetBackedProperty(ref end, in value, endChanging, endChanged);
    }

    /// <summary>
    /// A property no column observes, which is how a raise nothing wants is priced in this shape
    /// </summary>
    public long Idle
    {
        get => idle;
        set => SetBackedProperty(ref idle, in value, idleChanging, idleChanged);
    }

    public long Margin
    {
        get => margin;
        set => SetBackedProperty(ref margin, in value, marginChanging, marginChanged);
    }

    public long Offset
    {
        get => offset;
        set => SetBackedProperty(ref offset, in value, offsetChanging, offsetChanged);
    }

    public long Rate
    {
        get => rate;
        set => SetBackedProperty(ref rate, in value, rateChanging, rateChanged);
    }

    public long Start
    {
        get => start;
        set => SetBackedProperty(ref start, in value, startChanging, startChanged);
    }

    public long Tax
    {
        get => tax;
        set => SetBackedProperty(ref tax, in value, taxChanging, taxChanged);
    }

    public long Total
    {
        get => total;
        set => SetBackedProperty(ref total, in value, totalChanging, totalChanged);
    }

    public long Weight
    {
        get => weight;
        set => SetBackedProperty(ref weight, in value, weightChanging, weightChanged);
    }
}
