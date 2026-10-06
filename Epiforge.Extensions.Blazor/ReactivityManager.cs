namespace Epiforge.Extensions.Blazor;

sealed class ReactivityManager :
    IReactivityManager
{
    struct Resolution
    {
        public MemberPath Closure;
        public Type ContainerType;
        public MemberPath? Path;
        public object? Value;
    }

    const int indexThreshold = 8;
    const string invalidExpressionMessage = "Expression is not a valid member access chain expression.";
    const string unresolvableMessage = "Expression path is not resolvable due to some part of it being null.";

    static void Append<T>(ref T[] array, ref int count, T item)
    {
        if (count == array.Length)
            Array.Resize(ref array, count == 0 ? 4 : count * 2);
        array[count++] = item;
    }

    static void Descend(MemberExpression member, ref Resolution resolution)
    {
        switch (member.Expression)
        {
            case MemberExpression inner:
                Descend(inner, ref resolution);
                break;
            case ConstantExpression constant:
                resolution.Closure = MemberPath.Closures;
                resolution.ContainerType = constant.Type;
                resolution.Value = constant.Value;
                break;
            default:
                throw new ArgumentException(invalidExpressionMessage, "valueAccessor");
        }
        if (resolution.Path is not null)
        {
            resolution.Path = resolution.Path.Child(member.Member);
            return;
        }
        var closureStep = resolution.Closure.Child(member.Member);
        if (resolution.Value is { } container && closureStep.IsReadFromCompilerGenerated(resolution.ContainerType))
        {
            var value = closureStep.Read(container);
            resolution.Closure = closureStep;
            resolution.ContainerType = value?.GetType() ?? typeof(object);
            resolution.Value = value;
            return;
        }
        resolution.Path = MemberPath.Observations.Child(member.Member);
    }

    static bool Resolve(Expression body, out object? root, [NotNullWhen(true)] out MemberPath? path)
    {
        if (body is MemberExpression member)
        {
            var resolution = new Resolution();
            Descend(member, ref resolution);
            root = resolution.Value;
            path = resolution.Path;
            return path is not null;
        }
        if (body is ConstantExpression constant)
        {
            root = constant.Value;
            path = null;
            return false;
        }
        throw new ArgumentException(invalidExpressionMessage, "valueAccessor");
    }

    int collectionCount;
    IReactiveComponent? component;
    int cycle = 1;
    bool isDisposed;
    int observationCount;
    Dictionary<ObservationKey, Observation>? observationIndex;
    Observation[] observations = [];
    int slotCount;
    int sourceCount;
    Dictionary<object, ObservedSource>? sourceIndex;
    ObservedSource[] sources = [];
    int touchedCollections;
    int touchedObservations;
    int touchedSlots;

    [MemberNotNull(nameof(component))]
    void AssertUsable()
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(ReactivityManager));
        if (component is null)
            throw new InvalidOperationException("Reactivity manager is not initialized.");
    }

    public IObservedBinding<T> Binding<T>(Expression<Func<T>> valueAccessor)
    {
        ArgumentNullException.ThrowIfNull(valueAccessor);
        AssertUsable();
        if (!Resolve(valueAccessor.Body, out var root, out var path))
            return new ObservedBinding<T>(valueAccessor, root, null);
        Traverse(root, path);
        return new ObservedBinding<T>(valueAccessor, root, path);
    }

    public IObservedBinding<TTarget> Binding<TSource, TTarget>(Expression<Func<TSource>> valueAccessor, Func<TSource, TTarget> converter, Func<TTarget, TSource> reverseConverter)
    {
        ArgumentNullException.ThrowIfNull(valueAccessor);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(reverseConverter);
        return new ConvertedObservedBinding<TSource, TTarget>(Binding(valueAccessor), converter, reverseConverter);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;
        isDisposed = true;
        for (var i = 0; i < sourceCount; ++i)
            sources[i].Dispose();
        sources = [];
        sourceCount = 0;
        sourceIndex = null;
        observations = [];
        observationCount = 0;
        observationIndex = null;
    }

    Observation GetObservation(object root, MemberPath path)
    {
        if (observationIndex is not null)
        {
            ref var indexed = ref CollectionsMarshal.GetValueRefOrAddDefault(observationIndex, new ObservationKey(root, path), out var exists);
            if (exists)
                return indexed!;
            indexed = new Observation(root, path);
            Append(ref observations, ref observationCount, indexed);
            return indexed;
        }
        for (var i = 0; i < observationCount; ++i)
        {
            var observation = observations[i];
            if (ReferenceEquals(observation.Root, root) && ReferenceEquals(observation.Path, path))
                return observation;
        }
        var added = new Observation(root, path);
        Append(ref observations, ref observationCount, added);
        if (observationCount > indexThreshold)
        {
            observationIndex = new(observationCount * 2);
            for (var i = 0; i < observationCount; ++i)
            {
                var observation = observations[i];
                observationIndex.Add(new ObservationKey(observation.Root, observation.Path), observation);
            }
        }
        return added;
    }

    ObservedSource GetSource(object target)
    {
        if (sourceIndex is not null)
        {
            ref var indexed = ref CollectionsMarshal.GetValueRefOrAddDefault(sourceIndex, target, out var exists);
            if (exists)
                return indexed!;
            indexed = new ObservedSource(this, target);
            Append(ref sources, ref sourceCount, indexed);
            return indexed;
        }
        for (var i = 0; i < sourceCount; ++i)
        {
            var source = sources[i];
            if (ReferenceEquals(source.Target, target))
                return source;
        }
        var added = new ObservedSource(this, target);
        Append(ref sources, ref sourceCount, added);
        if (sourceCount > indexThreshold)
        {
            sourceIndex = new(sourceCount * 2, ReferenceEqualityComparer.Instance);
            for (var i = 0; i < sourceCount; ++i)
                sourceIndex.Add(sources[i].Target, sources[i]);
        }
        return added;
    }

    public void Initialize<TComponent>(TComponent component)
        where TComponent : IReactiveComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        if (isDisposed)
            throw new ObjectDisposedException(nameof(ReactivityManager));
        if (this.component is not null)
            throw new InvalidOperationException("Reactivity manager is already initialized.");
        this.component = component;
    }

    public void NotifyCycleEnded()
    {
        AssertUsable();
        component.ConfigureBindings();
        if (touchedObservations != observationCount)
            SweepObservations();
        if (touchedSlots != slotCount || touchedCollections != collectionCount)
            SweepSources();
        touchedCollections = 0;
        touchedObservations = 0;
        touchedSlots = 0;
        ++cycle;
    }

    public T Observed<T>(Expression<Func<T>> valueAccessor)
    {
        ArgumentNullException.ThrowIfNull(valueAccessor);
        AssertUsable();
        if (!Resolve(valueAccessor.Body, out var root, out var path))
            return (T)root!;
        return path.Steps[^1].Read<T>(Traverse(root, path).target);
    }

    public T ObservedCollection<T>(Expression<Func<T>> valueAccessor)
    {
        ArgumentNullException.ThrowIfNull(valueAccessor);
        AssertUsable();
        if (!Resolve(valueAccessor.Body, out var root, out var path))
        {
            var constant = (T)root!;
            if (constant is INotifyCollectionChanged constantCollection)
                TouchCollection(null, constantCollection);
            return constant;
        }
        var (observation, target) = Traverse(root, path);
        var value = path.Steps[^1].Read<T>(target);
        if (value is INotifyCollectionChanged collection)
            TouchCollection(observation, collection);
        return value;
    }

    internal void ObservedChanged()
    {
        if (!isDisposed)
            component?.StateHasChanged();
    }

    [Obsolete("Components no longer notify their reactivity manager of being rendered; a component implementing IReactiveComponent directly calls NotifyCycleEnded each time it renders")]
    public void OnAfterRender()
    {
    }

    void SweepObservations()
    {
        var kept = 0;
        for (var i = 0; i < observationCount; ++i)
        {
            var observation = observations[i];
            if (observation.Stamp == cycle)
                observations[kept++] = observation;
            else
                observationIndex?.Remove(new ObservationKey(observation.Root, observation.Path));
        }
        Array.Clear(observations, kept, observationCount - kept);
        observationCount = kept;
    }

    void SweepSources()
    {
        var kept = 0;
        for (var i = 0; i < sourceCount; ++i)
        {
            var source = sources[i];
            slotCount -= source.SweepSlots(cycle);
            if (source.IsCollectionSubscribed && source.CollectionStamp != cycle)
            {
                source.UnsubscribeCollection();
                --collectionCount;
            }
            if (source.IsSubscribed)
                sources[kept++] = source;
            else
            {
                source.IsLive = false;
                sourceIndex?.Remove(source.Target);
            }
        }
        Array.Clear(sources, kept, sourceCount - kept);
        sourceCount = kept;
    }

    void TouchCollection(Observation? observation, INotifyCollectionChanged collection)
    {
        var source = observation?.CollectionSource;
        if (source is null || !source.IsLive || !ReferenceEquals(source.Target, collection))
        {
            source = GetSource(collection);
            if (observation is not null)
                observation.CollectionSource = source;
        }
        if (!source.IsCollectionSubscribed)
        {
            source.SubscribeCollection();
            ++collectionCount;
        }
        if (source.CollectionStamp != cycle)
        {
            source.CollectionStamp = cycle;
            ++touchedCollections;
        }
    }

    void TouchSlot(Observation observation, int index, object target, string name)
    {
        var slot = observation.GetSlot(index);
        if (slot is null || !slot.IsLive || !ReferenceEquals(slot.Source.Target, target))
        {
            var source = GetSource(target);
            slot = source.FindSlot(name);
            if (slot is null)
            {
                slot = source.AddSlot(name);
                ++slotCount;
            }
            observation.SetSlot(index, slot);
        }
        if (slot.Stamp != cycle)
        {
            slot.Stamp = cycle;
            ++touchedSlots;
        }
    }

    /// <summary>
    /// Touches the observation of a chain of members read from a root, subscribing to every object along the chain which announces property changes before reading from it, and returns the object from which the last member is read
    /// </summary>
    (Observation observation, object target) Traverse(object? root, MemberPath path)
    {
        if (root is null)
            throw new ArgumentException(unresolvableMessage, "valueAccessor");
        var observation = GetObservation(root, path);
        if (observation.Stamp != cycle)
        {
            observation.Stamp = cycle;
            ++touchedObservations;
        }
        var steps = path.Steps;
        var last = steps.Length - 1;
        var target = root;
        for (var i = 0; ; ++i)
        {
            var step = steps[i];
            if (target is INotifyPropertyChanged)
                TouchSlot(observation, i, target, step.Name);
            if (i == last)
                return (observation, target);
            target = step.Read(target) ?? throw new ArgumentException(unresolvableMessage, "valueAccessor");
        }
    }
}
