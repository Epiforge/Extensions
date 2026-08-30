namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1)]
public class DirectSubscriptionCeilingBenchmarks
{
    sealed class DirectComparison :
        IDisposable
    {
        static readonly Func<BenchmarkPerson, BenchmarkPerson, bool> evaluate = (person, threshold) => person.Rank > threshold.Rank;

        internal DirectComparison(BenchmarkPerson subject, BenchmarkPerson threshold)
        {
            this.subject = subject;
            this.threshold = threshold;
            value = evaluate(subject, threshold);
            subject.PropertyChanged += SourcePropertyChanged;
            threshold.PropertyChanged += SourcePropertyChanged;
        }

        readonly BenchmarkPerson subject;
        readonly BenchmarkPerson threshold;
        bool value;

        internal event PropertyChangedEventHandler? PropertyChanged;
        internal event PropertyChangingEventHandler? PropertyChanging;

        public void Dispose()
        {
            subject.PropertyChanged -= SourcePropertyChanged;
            threshold.PropertyChanged -= SourcePropertyChanged;
        }

        void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!(string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(BenchmarkPerson.Rank)))
                return;
            var current = evaluate(subject, threshold);
            if (EqualityComparer<bool>.Default.Equals(value, current))
                return;
            PropertyChanging?.Invoke(this, EvaluationChanging);
            value = current;
            PropertyChanged?.Invoke(this, EvaluationChanged);
        }
    }

    sealed class DirectSelector :
        IDisposable
    {
        static readonly Func<BenchmarkPerson, int> evaluate = person => person.Rank;

        internal DirectSelector(BenchmarkPerson subject)
        {
            this.subject = subject;
            value = evaluate(subject);
            subject.PropertyChanged += SubjectPropertyChanged;
        }

        readonly BenchmarkPerson subject;
        int value;

        internal event PropertyChangedEventHandler? PropertyChanged;
        internal event PropertyChangingEventHandler? PropertyChanging;

        public void Dispose() =>
            subject.PropertyChanged -= SubjectPropertyChanged;

        void SubjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!(string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(BenchmarkPerson.Rank)))
                return;
            var current = evaluate(subject);
            if (EqualityComparer<int>.Default.Equals(value, current))
                return;
            PropertyChanging?.Invoke(this, EvaluationChanging);
            value = current;
            PropertyChanged?.Invoke(this, EvaluationChanged);
        }
    }

    static readonly PropertyChangedEventArgs EvaluationChanged = new("Evaluation");
    static readonly PropertyChangingEventArgs EvaluationChanging = new("Evaluation");

    DirectComparison[] ceilingComparisons = null!;
    BenchmarkPerson[] ceilingComparisonSubjects = null!;
    DirectSelector[] ceilingSelectors = null!;
    BenchmarkPerson[] ceilingSelectorSubjects = null!;
    IObservableExpression<BenchmarkPerson, bool>[] fastComparisons = null!;
    BenchmarkPerson[] fastComparisonSubjects = null!;
    ExpressionObserver fastObserver = null!;
    IObservableExpression<BenchmarkPerson, int>[] fastSelectors = null!;
    BenchmarkPerson[] fastSelectorSubjects = null!;
    IObservableExpression<BenchmarkPerson, bool>[] graphComparisons = null!;
    BenchmarkPerson[] graphComparisonSubjects = null!;
    ExpressionObserver graphObserver = null!;
    IObservableExpression<BenchmarkPerson, int>[] graphSelectors = null!;
    BenchmarkPerson[] graphSelectorSubjects = null!;
    int notifications;
    BenchmarkPerson[] observeSubjects = null!;
    BenchmarkPerson threshold = null!;

    [Params(100, 1000)]
    public int Observations { get; set; }

    [Benchmark]
    public void CeilingComparisonChange()
    {
        for (var i = 0; i < Observations; ++i)
            ceilingComparisonSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void CeilingComparisonObserve()
    {
        var observations = new DirectComparison[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = new DirectComparison(observeSubjects[i], threshold);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    [Benchmark]
    public void CeilingSelectorChange()
    {
        for (var i = 0; i < Observations; ++i)
            ceilingSelectorSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void CeilingSelectorObserve()
    {
        var observations = new DirectSelector[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = new DirectSelector(observeSubjects[i]);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < Observations; ++i)
        {
            graphSelectors[i].Dispose();
            graphComparisons[i].Dispose();
            fastSelectors[i].Dispose();
            fastComparisons[i].Dispose();
            ceilingSelectors[i].Dispose();
            ceilingComparisons[i].Dispose();
        }
    }

    [Benchmark]
    public void FastComparisonChange()
    {
        for (var i = 0; i < Observations; ++i)
            fastComparisonSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void FastComparisonObserve()
    {
        var captured = threshold;
        var observations = new IObservableExpression<BenchmarkPerson, bool>[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = fastObserver.Observe(person => person.Rank > captured.Rank, observeSubjects[i]);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    [Benchmark]
    public void FastSelectorChange()
    {
        for (var i = 0; i < Observations; ++i)
            fastSelectorSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void FastSelectorObserve()
    {
        var observations = new IObservableExpression<BenchmarkPerson, int>[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = fastObserver.Observe(person => person.Rank, observeSubjects[i]);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    [Benchmark]
    public void GraphComparisonChange()
    {
        for (var i = 0; i < Observations; ++i)
            graphComparisonSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void GraphComparisonObserve()
    {
        var captured = threshold;
        var observations = new IObservableExpression<BenchmarkPerson, bool>[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = graphObserver.Observe(person => person.Rank > captured.Rank, observeSubjects[i]);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    [Benchmark(Baseline = true)]
    public void GraphSelectorChange()
    {
        for (var i = 0; i < Observations; ++i)
            graphSelectorSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void GraphSelectorObserve()
    {
        var observations = new IObservableExpression<BenchmarkPerson, int>[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = graphObserver.Observe(person => person.Rank, observeSubjects[i]);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    void Notified(object? sender, PropertyChangedEventArgs e) =>
        ++notifications;

    [GlobalSetup]
    public void Setup()
    {
        fastObserver = new ExpressionObserver();
        graphObserver = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = false });
        threshold = new BenchmarkPerson("threshold", 4);
        var captured = threshold;
        ceilingComparisons = new DirectComparison[Observations];
        ceilingComparisonSubjects = new BenchmarkPerson[Observations];
        ceilingSelectors = new DirectSelector[Observations];
        ceilingSelectorSubjects = new BenchmarkPerson[Observations];
        fastComparisons = new IObservableExpression<BenchmarkPerson, bool>[Observations];
        fastComparisonSubjects = new BenchmarkPerson[Observations];
        fastSelectors = new IObservableExpression<BenchmarkPerson, int>[Observations];
        fastSelectorSubjects = new BenchmarkPerson[Observations];
        graphComparisons = new IObservableExpression<BenchmarkPerson, bool>[Observations];
        graphComparisonSubjects = new BenchmarkPerson[Observations];
        graphSelectors = new IObservableExpression<BenchmarkPerson, int>[Observations];
        graphSelectorSubjects = new BenchmarkPerson[Observations];
        observeSubjects = new BenchmarkPerson[Observations];
        for (var i = 0; i < Observations; ++i)
        {
            observeSubjects[i] = new BenchmarkPerson($"O{i}", 4);
            graphSelectorSubjects[i] = new BenchmarkPerson($"GS{i}", 4);
            graphSelectors[i] = graphObserver.Observe(person => person.Rank, graphSelectorSubjects[i]);
            graphSelectors[i].PropertyChanged += Notified;
            graphComparisonSubjects[i] = new BenchmarkPerson($"GC{i}", 4);
            graphComparisons[i] = graphObserver.Observe(person => person.Rank > captured.Rank, graphComparisonSubjects[i]);
            graphComparisons[i].PropertyChanged += Notified;
            fastSelectorSubjects[i] = new BenchmarkPerson($"FS{i}", 4);
            fastSelectors[i] = fastObserver.Observe(person => person.Rank, fastSelectorSubjects[i]);
            fastSelectors[i].PropertyChanged += Notified;
            fastComparisonSubjects[i] = new BenchmarkPerson($"FC{i}", 4);
            fastComparisons[i] = fastObserver.Observe(person => person.Rank > captured.Rank, fastComparisonSubjects[i]);
            fastComparisons[i].PropertyChanged += Notified;
            ceilingSelectorSubjects[i] = new BenchmarkPerson($"CS{i}", 4);
            ceilingSelectors[i] = new DirectSelector(ceilingSelectorSubjects[i]);
            ceilingSelectors[i].PropertyChanged += Notified;
            ceilingComparisonSubjects[i] = new BenchmarkPerson($"CC{i}", 4);
            ceilingComparisons[i] = new DirectComparison(ceilingComparisonSubjects[i], threshold);
            ceilingComparisons[i].PropertyChanged += Notified;
        }
    }
}
