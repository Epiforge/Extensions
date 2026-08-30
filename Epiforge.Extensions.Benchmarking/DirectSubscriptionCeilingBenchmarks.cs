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

    DirectComparison[] directComparisons = null!;
    BenchmarkPerson[] directComparisonSubjects = null!;
    DirectSelector[] directSelectors = null!;
    BenchmarkPerson[] directSelectorSubjects = null!;
    IObservableExpression<BenchmarkPerson, bool>[] graphComparisons = null!;
    BenchmarkPerson[] graphComparisonSubjects = null!;
    IObservableExpression<BenchmarkPerson, int>[] graphSelectors = null!;
    BenchmarkPerson[] graphSelectorSubjects = null!;
    int notifications;
    BenchmarkPerson[] observeSubjects = null!;
    ExpressionObserver observer = null!;
    BenchmarkPerson threshold = null!;

    [Params(100, 1000)]
    public int Observations { get; set; }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < Observations; ++i)
        {
            graphSelectors[i].PropertyChanged -= Notified;
            graphSelectors[i].Dispose();
            graphComparisons[i].PropertyChanged -= Notified;
            graphComparisons[i].Dispose();
            directSelectors[i].PropertyChanged -= Notified;
            directSelectors[i].Dispose();
            directComparisons[i].PropertyChanged -= Notified;
            directComparisons[i].Dispose();
        }
    }

    [Benchmark]
    public void DirectComparisonChange()
    {
        for (var i = 0; i < Observations; ++i)
            directComparisonSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void DirectComparisonObserve()
    {
        var observations = new DirectComparison[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = new DirectComparison(observeSubjects[i], threshold);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    [Benchmark]
    public void DirectSelectorChange()
    {
        for (var i = 0; i < Observations; ++i)
            directSelectorSubjects[i].Rank ^= 1;
    }

    [Benchmark]
    public void DirectSelectorObserve()
    {
        var observations = new DirectSelector[Observations];
        for (var i = 0; i < Observations; ++i)
            observations[i] = new DirectSelector(observeSubjects[i]);
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
            observations[i] = observer.Observe(person => person.Rank > captured.Rank, observeSubjects[i]);
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
            observations[i] = observer.Observe(person => person.Rank, observeSubjects[i]);
        for (var i = 0; i < Observations; ++i)
            observations[i].Dispose();
    }

    void Notified(object? sender, PropertyChangedEventArgs e) =>
        ++notifications;

    [GlobalSetup]
    public void Setup()
    {
        observer = new ExpressionObserver();
        threshold = new BenchmarkPerson("threshold", 4);
        var captured = threshold;
        directComparisons = new DirectComparison[Observations];
        directComparisonSubjects = new BenchmarkPerson[Observations];
        directSelectors = new DirectSelector[Observations];
        directSelectorSubjects = new BenchmarkPerson[Observations];
        graphComparisons = new IObservableExpression<BenchmarkPerson, bool>[Observations];
        graphComparisonSubjects = new BenchmarkPerson[Observations];
        graphSelectors = new IObservableExpression<BenchmarkPerson, int>[Observations];
        graphSelectorSubjects = new BenchmarkPerson[Observations];
        observeSubjects = new BenchmarkPerson[Observations];
        for (var i = 0; i < Observations; ++i)
        {
            observeSubjects[i] = new BenchmarkPerson($"O{i}", 4);
            graphSelectorSubjects[i] = new BenchmarkPerson($"GS{i}", 4);
            graphSelectors[i] = observer.Observe(person => person.Rank, graphSelectorSubjects[i]);
            graphSelectors[i].PropertyChanged += Notified;
            graphComparisonSubjects[i] = new BenchmarkPerson($"GC{i}", 4);
            graphComparisons[i] = observer.Observe(person => person.Rank > captured.Rank, graphComparisonSubjects[i]);
            graphComparisons[i].PropertyChanged += Notified;
            directSelectorSubjects[i] = new BenchmarkPerson($"DS{i}", 4);
            directSelectors[i] = new DirectSelector(directSelectorSubjects[i]);
            directSelectors[i].PropertyChanged += Notified;
            directComparisonSubjects[i] = new BenchmarkPerson($"DC{i}", 4);
            directComparisons[i] = new DirectComparison(directComparisonSubjects[i], threshold);
            directComparisons[i].PropertyChanged += Notified;
        }
    }
}
