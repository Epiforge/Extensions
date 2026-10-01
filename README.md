<picture>
<source media="(prefers-color-scheme: dark)" srcset="Extensions-dark.png">
<img src="Extensions.png" alt="Extensions Logo" width="218" height="236">
</picture> 

General utilities to help with stuff in .NET Development, from Epiforge.

Supports `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`.


![Azure Pipelines](https://dev.azure.com/epiforge/extensions/_apis/build/status/epiforge.extensions)
![Build](https://img.shields.io/azure-devops/build/epiforge/extensions/4.svg?logo=azuredevops&logoColor=white)
![Tests](https://img.shields.io/azure-devops/tests/epiforge/extensions/4.svg?compact_message=&logo=azuredevops&logoColor=white)
![Coverage](https://img.shields.io/azure-devops/coverage/epiforge/extensions/4?logo=azuredevops)

- [Libraries](#libraries)
  - [ Components](#-components)
    - [Property Change Notification](#property-change-notification)
    - [Disposal](#disposal)
    - [Reflection](#reflection)
    - [Exceptions](#exceptions)
    - [Threading](#threading)
  - [ Collections](#-collections)
    - [Generic](#generic)
    - [ObjectModel](#objectmodel)
    - [Specialized](#specialized)
  - [ Expressions](#-expressions)
    - [Observable Expressions](#observable-expressions)
      - [Following Changes](#following-changes)
      - [When Evaluation Fails](#when-evaluation-fails)
      - [Disposing of an Observation](#disposing-of-an-observation)
      - [Changes From Other Threads](#changes-from-other-threads)
      - [Fields Are Read Once](#fields-are-read-once)
      - [Optimizing Expressions](#optimizing-expressions)
      - [How an Expression Gets Observed](#how-an-expression-gets-observed)
    - [Observable Queries](#observable-queries)
      - [How Observable Queries Work and When to Use Them](#how-observable-queries-work-and-when-to-use-them)
      - [When a Query Faults](#when-a-query-faults)
      - [Keys and Order](#keys-and-order)
      - [Enumerate Rather Than Index](#enumerate-rather-than-index)
      - [Choosing a Library](#choosing-a-library)
      - [Measured Against Other Libraries](#measured-against-other-libraries)
    - [Ahead-of-Time Compilation](#ahead-of-time-compilation)
  - [Platforms](#platforms)
    - [ Windows](#-windows)
- [License](#license)
- [Contributing](#contributing)
- [Acknowledgements](#acknowledgements)

# Libraries

## <img src="Epiforge.Extensions.Components/NuGet.png" width="64" height="64" alt="Components" style="float: left !important; padding-right: 0.5em !important;"> Components
[![Epiforge.Extensions.Components Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Components.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.components)](https://www.nuget.org/packages/Epiforge.Extensions.Components)

### Property Change Notification
This library offers the `PropertyChangeNotifier` class, which you may inherit from to quickly get all the property utilities we're all tired of copying and pasting everywhere.
Just call the protected `OnPropertyChanged` and `OnPropertyChanging` methods at the appropriate times from setters and compiler services will figure out what property you're in.
Or, if all you need to do is set the value of a field, `SetBackedProperty` couldn't make it any easier or convenient to handle that as efficiently as possible.
`DynamicPropertyChangeNotifier` is also available if your class needs to be dynamic.

Be sure to set the protected `Logger` property if you want the abstract class to log what's going on with property change notification.

### Disposal
This library features base classes that handle things we've written a thousand times over, this time involving disposal.
If you want to go with an implementation of the tried and true `IDisposable`, just inherit from `SyncDisposable`.
Want a taste of the new `IAsyncDisposable`? Then, inherit from `AsyncDisposable`.
Or, if you want to support both, there's `Disposable`.
Additionally, if your object needs to be dynamic, you can use `DynamicSyncDisposable`, `DynamicAsyncDisposable`, or `DynamicDisposable`.
Each of these features abstract methods to actually do your disposal.
On `Disposable` and `DynamicDisposable`, the asynchronous one, `DisposeAsyncCore`, is virtual and defaults to the synchronous `Dispose(bool)`, so a type whose cleanup is entirely synchronous only writes it once.
On `AsyncDisposable` and `DynamicAsyncDisposable` it stays abstract, because there is no synchronous path for it to delegate to.
But all of the base classes feature:

* proper implementation of the finalizer and use of `GC.SuppressFinalize`
* monitored access to disposal to ensure it can't happen twice
* the ability to override or "cancel" disposal by returning false from the abstract methods (e.g. you're reference counting and only want to dispose when your counter reaches zero)
* a protected `ThrowIfDisposed` method you can call to before doing anything that requires you haven't been disposed
* an `IsDisposed` property the value (and change notifications) of which are handled for you

This library provides the `IDisposalStatus` interface, which defines the `IsDisposed` property and all the base classes implement it.
This library also provides the `INotifyDisposing`, `INotifyDisposed`, and `INotifyDisposalOverridden` interfaces, which add events that notify of these occurrences.

Be sure to set the protected `Logger` property if you want the abstract class to log what's going on with disposal.

### Reflection
This library has useful tools for when you can't be certain of some things at compile time, such as types, methods, etc.
While .NET reflection is immensely powerful, prior to .NET 7, it's not very quick.
To address this, this library offers the following extension methods which will emit IL, generate delegates, and cache them for expedited use of Reflection:

* `ConstructorInfo.FastInvoke`: call a constructor only known at runtime quickly
* `MethodInfo.FastInvoke`: call a method only known at runtime quickly
* `PropertyInfo.FastGetValue`: get the value of a property only known at runtime quickly
* `PropertyInfo.FastSetValue`: set the value of a property only known at runtime quickly
* `Type.FastDefault`: get the default value of a type only known at runtime quickly
* `Type.GetImplementationEvents`: searches for the events of a type, including interfaces and interface inheritance
* `Type.GetImplementationMethods`: searches for the methods of a type, including interfaces and interface inheritance
* `Type.GetImplementationProperties`: searches for the properties of a type, including interfaces and interface inheritance

`FastGetValue` and `FastSetValue` simply call the built-in methods, which are optimized from .NET 7 on.

This library also offers `FastComparer` and `FastEqualityComparer`, which implement `IComparer` and `IEqualityComparer`, respectively, but quickly use the methods of `Comparer<>.Default` and `EqualityComparer<>.Default`, respectively, to do their work.

Where the runtime cannot generate code, as under Native AOT and on iOS, these methods, `FastComparer` and `FastEqualityComparer` invoke, construct and compare through reflection instead, with the same results, including comparisons which honor a type's own `IComparable<T>` or `IEquatable<T>` as the default comparers do; `GetImplementationEvents`, `GetImplementationMethods` and `GetImplementationProperties` are annotated as reflecting over members which trimming may remove.

In addition (pun intended), this library offers `GenericAddition`, `GenericSubtraction`, `GenericMultiplication`, and `GenericDivision`, which will produce delegates that will perform the respective operations with values of supplied generic type arguments.

### Exceptions
This library provides extension methods for dealing with exceptions:

* `GetFullDetails` - creates a representation of an exception and all of its inner exceptions, including exception types, messages, and stack traces, and traversing multiple inner exceptions in the case of `AggregateException` and `ReflectionTypeLoadException`

### Threading
This library provides classes for use in threading scenarios:

* `AsyncSynchronizationContext` - A SynchronizationContext that uses the Task Parallel Library (TPL) to process callbacks asynchronously

---

## <img src="Epiforge.Extensions.Collections/NuGet.png" width="64" height="64" alt="Collections" style="float: left !important; padding-right: 0.5em !important;"> Collections
[![Epiforge.Extensions.Collections Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Collections.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.collections)](https://www.nuget.org/packages/Epiforge.Extensions.Collections)

This library provides a number of extension methods for collections and dictionaries:

* `EnumerableExtensions`, providing:
  * `FindIndex` - Finds the index of the first element in the source that satisfies the specified predicate
  * `FindLastIndex` - Finds the index of the last element in the source that satisfies the specified predicate
  * `FindIndicies` - Finds the indicies of the elements in the source that satisfy the specified predicate
  * `IndexOf` - Finds the first index of the specified item in the source
  * `LastIndexOf` - Finds the last index of the specified item in the source
  * `IndiciesOf` - Finds the indicies of the specified item in the source
* `DictionaryExtensions`, providing:
  * `GetOrAdd` - Adds a key/value pair to the specified `IDictionary` or `IDictionary<TKey, TValue>` by using the specified function if the key does not already exist (returns the new value, or the existing value if the key exists)
  * `TryRemove` - Attempts to remove and return the value that has the specified key from the specified `IDictionary` or `IDictionary<TKey, TValue>`

### Generic
* `ReadOnlyDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IReadOnlyDictionary<TKey, TValue>`
* `ReadOnlyRangeDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IReadOnlyRangeDictionary<TKey, TValue>`
* `ReadOnlyConcurrentDictionary<TKey, TValue>` is a read-only wrapper for `ObservableConcurrentDictionary<TKey, TValue>`
* `ReversedComparer<T>` is a comparer that reverses the comparison of another comparer (this is useful when you want to sort a list in the opposite order of the default sort order)
* `IHashKeys<TKey>` is implemented by keyed data structures that use an `IEqualityComparer<TKey>` to decide key equality, so that a consumer can discover the comparer a dictionary is actually using rather than assume the default (the observable queries in `Epiforge.Extensions.Expressions` do exactly this)
* `PrefixWeightedSequence<T>` is a sequence in which every position carries a weight. Insertion, removal, movement, and changing a weight are all logarithmic in the number of positions, as is finding a position by index, by the sum of the weights before it, or by which position a given offset falls within. `PrefixWeightedSequenceNode<T>` is the handle to a position and remains valid for as long as its item remains in the sequence, so you can hold onto one instead of re-finding an index after every change.

### ObjectModel
* `ObservableDictionary<TKey, TValue>`, `ObservableSortedDictionary<TKey, TValue>`, `ObservableConcurrentDictionary<TKey, TValue>` are counterparts to the BCL's `Dictionary<TKey, TValue>`, `SortedDictionary<TKey, TValue>`, and `ConcurrentDictionary<TKey, TValue>`, respectively, that implement the also included `IRangeDictionary<TKey, TValue>` and `INotifyDictionaryChanged<TKey, TValue>`. Ever want to add multiple items to a dictionary at once... or keep an eye on what's being done to it? Now you can.
* `ObservableRangeCollection<T>` is a counterpart to the BCL's `ObservableCollection<T>` which implements:
  * `AddRange` - Adds objects to the end of the collection
  * `GetAndRemoveAll` - Removes all object from the collection that satisfy a predicate
  * `GetAndRemoveAt` - Gets the element at the specified index and removes it from the collection
  * `GetRange` - Gets the elements in the range starting at the specified index and of the specified length
  * `InsertRange` - Inserts elements into the collection at the specified index
  * `MoveRange` - Moves the items at the specified index to a new location in the collection
  * `RemoveAll` - Removes all object from the collection that satisfy a predicate
  * `RemoveRange` - Removes the specified items from the collection *or* removes the specified range of items from the collection
  * `ReplaceAll` - Replace all items in the collection with the items in the specified collection
  * `ReplaceRange` - Replaces the specified range of items from the collection with the items in the specified collection
  * `Reset` - Resets the collection with the specified collection of items
* `ReadOnlyObservableRangeDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IReadOnlyObservableRangeDictionary<TKey, TValue>`. It subscribes to what it wraps, so dispose of it when you are done with it.
* `ReadOnlyObservableRangeCollection<T>` is a read-only wrapper for any classes implementing `IReadOnlyObservableRangeCollection<T>`. It subscribes to what it wraps, so dispose of it when you are done with it.

### Specialized
* `EquatableList<T>` is an immutable list of items which may be compared with other instances of the same type and produces a hash code based on the permutation of its contents
* `NullableKeyDictionary<TKey, TValue>` and `NullableKeySortedDictionary<TKey, TValue>` are very slim implementations of `IDictionary<TKey, TValue>` that allow a single null key (useful for some edge cases in which a null key is simply going to happen and you need to be able to deal with it; otherwise, use other dictionary classes)
* `OrderedHashSet<T>` is a counterpart to the BCL's `HashSet<T>` that maintains the order of the elements in the set. All operations are still *O(1)*, just like the original, but if you enumerate over it you will get elements in the exact order they were added. There are also methods for manipulating the order

---

## <img src="Epiforge.Extensions.Expressions/NuGet.png" width="64" height="64" alt="Expressions" style="float: left !important; padding-right: 0.5em !important;"> Expressions
[![Epiforge.Extensions.Expressions Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Expressions.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.expressions)](https://www.nuget.org/packages/Epiforge.Extensions.Expressions)

This library has useful tools for dealing with expressions:

* `ExpressionEqualityComparer` - Defines methods to support the comparison of expression trees for equality
* `ExpressionExtensions`, providing:
  * `Duplicate` - Duplicates the specified expression tree
  * `SubstituteMethods` - Recursively scans an expression tree to replace invocations of specific methods with replacement methods

### Observable Expressions
An `ExpressionObserver` accepts a `LambdaExpression` and arguments to pass to it, dissects the lambda's body, and hooks into change notification events for properties (`INotifyPropertyChanged`), collections (`INotifyCollectionChanged`), and dictionaries (`Epiforge.Extensions.Collections.INotifyDictionaryChanged`).

```csharp
// Employee implements INotifyPropertyChanged
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr subscribed to elizabeth's PropertyChanged
```

#### Following Changes
As anything the expression reads changes, the expression re-evaluates, and its `Evaluation` property may change with it.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr.Evaluation.Result == 9
elizabeth.Name = "Lizzy";
// expr.Evaluation.Result == 5
```

An observable expression raises property change events of its own, and listening for them is the point of having one.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
expr.PropertyChanged += (sender, e) =>
{
    if (e.PropertyName == "Evaluation")
    {
        var (fault, result) = expr.Evaluation;
        if (fault is not null)
        {
            // handle the fault
        }
        else
        {
            // use the result
        }
    }
};
```

While an expression is working out its new value it can pass through results that were never simultaneously true of its inputs; an addition whose two operands both derive from the same property has to compute one of them before the other. You are not told about those. Every event you receive carries a value the expression genuinely held, so a subscriber that redraws or broadcasts on one does that work once rather than twice, the second time only to correct the first.

Nor are you told anything at all when a change leaves the value where it found it. That is decided by a comparison, using the same equality the expression uses everywhere else, and it happens before `PropertyChanging` rather than after — so a handler for that event still reads the previous value, and a pair of events always means the value really moved.

#### When Evaluation Fails
An exception can arise long after an observable expression was created, because something it reads changed. It is not thrown; it becomes the `Fault` of the expression's evaluation.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr.Evaluation.Fault is null
elizabeth.Name = null;
// expr.Evaluation.Fault is NullReferenceException
```

An expression tree built at run time can also catch a fault with `Expression.TryCatch`, which C# will not write in a lambda. The body is evaluated first; while it faults, the first catch block whose type the fault is supplies the value instead, and that block is evaluated only then. A catch block whose body is `Expression.Rethrow` lets the fault through, so faults which should always spread can be listed ahead of one catching `Exception`. Catch blocks with a variable or a filter, and finally and fault blocks, are not supported.

#### Disposing of an Observation
When you dispose of an observable expression, it disconnects from all the events it subscribed to.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
using (var expr = observer.Observe(e => e.Name.Length, elizabeth))
{
    // expr subscribed to elizabeth's PropertyChanged
}
// expr unsubscribed from elizabeth's PropertyChanged
```

Observable expressions also try to dispose of disposable objects they create in the course of their evaluation, when and where it makes sense. Use the `ExpressionObserverOptions` class for more direct control over this behavior.

#### Changes From Other Threads
A source can change on any thread, including while an observation of it is being built or evaluated on another. No change is lost: once the changes stop, every observation settles on what its expression gives over its sources as they then stand. You can dispose of an observation on any thread, too, even while another thread is evaluating it.

An observation is evaluated on the thread which raised the change, unless another thread is evaluating it at that moment, in which case that thread evaluates it again before it finishes and the thread which raised the change carries on without waiting. So the `PropertyChanged` which follows a change can arrive on a different thread from the change, and a setter can return before an observation reflects it.

Two things stay yours. While changes arrive on several threads at once, an observation can briefly report a value computed from reads taken at different moments, as any code reading shared state without a lock would, and it settles once they stop. And the observer reads your sources on whichever thread is evaluating, so a source must be safe to read while another thread changes it. `ObservableCollection<T>` is not: read in the middle of a change, it can throw, the observation reports that fault until the change's notification has it read again, and a `ConditionAsync` waiting on it completes with the fault.

If nothing an observer's observations read changes while another thread builds, evaluates or disposes of one of them, as when all of it happens on a user interface thread, you can say so by setting `IsThreadSafe` to `false` in the options you hand the observer. It then evaluates without the interlocked operations the promises above cost: a raise against a thousand observations of one object costs 0.50x what it otherwise would, and a property change in a filtered view of a thousand 0.73x. It is `true` by default, and setting it where changes do cross threads gives those promises up: a change can be lost, an observation can be evaluated before it is built, and observations disposed of on several threads at once can leave parts of themselves cached.

Queries keep the same promise once a `CollectionObserver` is observing what they are built over. When you ask a `CollectionObserver` to begin observing a sequence, whether an enumerable, a list or a dictionary, make sure nothing changes it until the call returns. From then on it can change on any thread, and a query built over it, or over another query, while it does misses none of those changes. So that what a query reads always matches what it has been told, observing a sequence which announces its changes keeps a copy of it, which costs 8 B per element, a reference to each, and, for a change far from the end of a long list, the time to move the rest of the copy: inserting and removing at the front of ten thousand elements costs 2.3 μs more, 1.6x what the list itself spends moving them. A list must announce its changes in the order it makes them, which one writer at a time ensures. A dictionary need not, because the observer reads each key a change names from the dictionary itself, so an `ObservableConcurrentDictionary<TKey, TValue>` can be changed by several threads at once. A dictionary's copy costs about 28 B per key. The sequences an `ObserveSelectMany` selector returns which announce their changes are observed the same way from the moment the query first reads them, which is when it is built or when the selector first returns one, so one must not change while that happens. A second operand of `ObserveConcat` which is not a query of this library is observed the same way from the moment the concatenation is built, so it must not change while that happens.

A query's results change on whichever thread evaluated the change, so reading them on another thread while they change is a race of yours rather than the query's: enumerating, indexing or copying one can throw or miss elements. To read a query on another thread, observe it through `ObserveUsingSyncRoot` and take the same lock while you read, or through `ObserveUsingSynchronizationContext` so that it changes on the thread which reads it. A query announces its changes while it and the queries it is built over hold the locks under which they change, so a handler of those announcements must not wait on another thread which may change any of them, as sending work to a user interface thread and waiting for it does. `ObserveUsingSynchronizationContext`, `ObserveUsingSyncRoot` and `ObserveUsingSynchronizationCallback` wait on their context, lock or callback only once those locks are released, so the thread they wait on can change what the query is built over while a change from another thread waits to be applied, and a change made within a handler of another query's announcements reaches them once that announcement is over. A query can be built over one synchronized with a context, eventually or not, on any thread.

#### Fields Are Read Once
Whatever a field held when an observation began is what that observation goes on using — a captured local, a field of your own class, and a static field alike. Assigning it afterward does not reach an observation that already exists. Static properties behave the same way, so `e => e.Hired < DateTime.Now` compares against the moment it was created for as long as it lives.

```csharp
var threshold = low;
using var expr = observer.Observe(e => e.Salary > threshold.Amount, elizabeth);
threshold = high;    // expr is still comparing against low
low.Amount = 50000;  // expr re-evaluates
high.Amount = 90000; // expr does not
```

If you want the comparison to follow the value, do not assign the field — make the thing it points at a property of an object that notifies, and read that instead.

#### Optimizing Expressions
The `Optimizer` property of `ExpressionObserverOptions` specifies an optimization method to invoke automatically while an observable expression is being created.
We recommend Tuomas Hietanen's [Linq.Expression.Optimizer](https://thorium.github.io/Linq.Expression.Optimizer), the use of which looks like this:

```csharp
var options = new ExpressionObserverOptions { Optimizer = ExpressionOptimizer.tryVisit };

var a = Expression.Parameter(typeof(bool));
var b = Expression.Parameter(typeof(bool));

var lambda = Expression.Lambda<Func<bool, bool, bool>>
(
    Expression.AndAlso
    (
        Expression.Not(a),
        Expression.Not(b)
    ),
    a,
    b
); // lambda explicitly defined as (a, b) => !a && !b

var observer = new ExpressionObserver(options);
var expr = observer.Observe<bool>(lambda, false, false);
// optimizer has intervened and defined expr as (a, b) => !(a || b)
// (because Augustus De Morgan said they're essentially the same thing, but this involves less steps)
```

Linq.Expression.Optimizer does not handle `Expression.TryCatch`, and `tryVisit` leaves any lambda containing one entirely unoptimized.

#### How an Expression Gets Observed
`Observe` takes a shortcut when it can and builds a graph when it cannot, deciding once when the observation is created. You receive the same values through the same events either way; the shortcut is just faster and lighter.

The shortcut handles an expression built from these:

* the argument, constants, and captured locals
* fields, on anything above — including static fields
* static properties
* properties and indexers whose target is one of the above
* a property read through something which can change, such as `e => e.Name.Length` or `e => e.Manager.Rank`, which follows the value as it moves and re-subscribes where it lands
* `?:`, `&&`, `||` and `??`, whose deferred operands take their subscriptions the first time an evaluation reaches them, which is where the graph attaches its nodes for them
* a try built with `Expression.TryCatch`, whose catch blocks take their subscriptions the first time a fault selects them
* a call to the get method of a property or an indexer, which is how an indexer written in C# arrives, read as the member or index access it stands for
* object construction, object initializers and array initializers, including construction of a value the observer disposes of when nothing the constructor is given can change, made once and disposed once
* an invocation of a literal lambda, as a formula or rule engine building expression trees at run time commonly emits, reduced to the body it would have evaluated when each parameter is read exactly once and, unless its argument is a constant or the argument, not inside a deferred operand or a try
* method calls and operators resolved to a method, unless the observer disposes of what one returned and what it is made on or given can change
* a property whose change notifications you have told the observer to ignore, when nothing it is read through can change, read once and kept

What builds the graph instead: an invocation of a literal lambda whose parameter is read other than exactly once, or is read inside a deferred operand or a try when its argument could fault, because the graph evaluates every argument first; an indexer whose target can change; a member read on a value type which can notify; a call or operator whose return value the observer disposes of and whose target or arguments can change; a construction whose value the observer disposes of and whose arguments can change; a read of a property or an indexer you have registered for disposal, whatever it is read through, because the property can announce and the graph replaces and disposes of its value when it does; a read of an ignored property through something which can change; and an expression deferring more than 64 operands.

Neither mechanism observes a lambda passed as an argument, such as the predicate handed to `Enumerable.Count`, or an array built from bounds, and asking to observe an expression containing either throws `NotSupportedException`.

To find out about a particular expression, ask:

```csharp
var analysis = new DirectSubscriptionAnalyzer(options).Analyze(expression.Body);
// analysis.IsEligible says whether the shortcut handles it
// analysis.Ineligibility says why not, such as DirectSubscriptionIneligibility.ValueRequiresDisposal
// analysis.IneligibleExpression is the part responsible
```

Hand the analyzer the same options you hand the observer, since some of them decide what gets subscribed to at all. Set `UseDirectSubscription` to `false` if you would rather always have the graph; it is `true` by default.

### Observable Queries
This library provides re-implementations of LINQ operations, but instead of returning `IEnumerable<T>`s and simple values, these return `IObservableCollectionQuery<T>`s, `IObservableDictionaryQuery<TKey, TValue>`s, and `IObservableScalarQuery<T>`s.
This is because, unlike traditional LINQ operations, these implementations continuously update their results until those results are disposed.
What they hand back is a read-only view of the source: change the source, and the query brings itself up to date. Queries do not implement the mutating range collection and dictionary interfaces, because a query result is not somewhere you put things.

A query updates when:

* the source is enumerable, implements `INotifyCollectionChanged`, and raises a `CollectionChanged` event
* the source is a dictionary, implements `Epiforge.Extensions.Collections.INotifyDictionaryChanged<TKey, TValue>`, and raises a `DictionaryChanged` event
* the elements in the enumerable (or the values in the dictionary) implement `INotifyPropertyChanged` and raise a `PropertyChanged` event
* a reference enclosed by a selector or a predicate passed to the method implements `INotifyCollectionChanged`, `Epiforge.Extensions.Collections.INotifyDictionaryChanged<TKey, TValue>`, or `INotifyPropertyChanged` and raises one of their events

That last one might be a little surprising, but it is because every selector and predicate passed to an Observable Query method becomes an observable expression (see above).
This means that you cannot pass one that an `ExpressionObserver` cannot observe (for example, a lambda expression that cannot be converted to an expression tree or that contains nodes that are unsupported).
In exchange, all of the notification plumbing is handled for you.

Suppose, for example, you're working on an app that displays a list of notes and you want the notes to be shown in descending order of when they were last edited.

```csharp
var notes = new ObservableCollection<Note>();
var collectionObserver = new CollectionObserver();

var observedNotes = collectionObserver.ObserveReadOnlyList(notes);
var orderedNotes = observedNotes.ObserveOrderBy(note => note.LastEdited, isDescending: true);
notesViewControl.ItemsSource = orderedNotes;
```

From then on, as you add `Note`s to the `notes` observable collection, the `IObservableCollectionQuery<Note>` named `orderedNotes` will be kept ordered so that `notesViewControl` displays them in the preferred order.

Since queries subscribe to events for you, you need to call `Dispose` on them when you no longer need them.

```csharp
void Page_Unload(object? sender, EventArgs e)
{
    orderedNotes.Dispose();
    observedNotes.Dispose();
}
```

Since the `ExpressionObserver` has a number of options governing its behavior, you may pass one you've made to the constructor of `CollectionObserver` to ensure those options are obeyed when observable expressions are created for your queries.

#### How Observable Queries Work and When to Use Them
It is worth being plain about what kind of thing this is, because "LINQ, but observable" undersells it and sets the wrong expectations.

A LINQ query is a description of a computation you run. Run it again and it does all of the work again. An Observable Query is not re-run. It is a small machine that holds the answer and repairs it, so when something changes, only the parts of the answer that depended on that thing are recomputed. The work is proportional to what changed rather than to how much data you have. If you want the name the literature uses for this idea, it is incremental, or self-adjusting, computation.

Three things that might otherwise look like arbitrary restrictions fall straight out of that:
1. Your selectors and predicates have to be expression trees rather than delegates because the machine has to read them to find out what they depend on. A delegate is opaque; there is nothing in it to subscribe to.
2. You have to dispose of a query because it is holding subscriptions to everything it depends on, and those subscriptions are the entire reason the answer stays right.
3. Faults reach you through `OperationFault` instead of being thrown, because the evaluation that failed happened later, on whichever thread evaluated the change. By then there is no call of yours left on the stack to throw out of.

What is not free is construction. Building the machine means building an observable expression for every element the query touches, and that is proportional to the size of the collection. So build a query once and hold onto it. Do not build one per frame, per request, or per keystroke. The bargain is that you pay up front and then stop paying to read.

The same goes for the lambdas you hand it. The observer optimizes, analyzes and compiles a lambda once and remembers the result by the instance you gave it, not by what the lambda says, and a lambda written inline is a new instance every time that line runs. So when you build queries over many collections, a query per row or per entity, keep each selector and predicate in a `static readonly` field and pass the same one every time. Over 256 one-element collections, `ObserveWhere` given a held predicate took 135 μs, and given the same predicate written inline, 14,801 μs, a figure which varied by about a third from one process to the next where the held one barely moved.

Reading is also cheaper than being told. A query subscribes to the one it is built on only while something is subscribed to it, and a filtered query works out where a change landed, and describes it, only when something will receive that description. So subscribe when you need to be told what changed, and simply read the query when you only need its answer to be right.

Which is also how to decide whether you want one. If you compute a result once and move on, plain LINQ is cheaper and simpler, and you should use it. If a result has to stay correct across a long run of small changes, such as a list someone is looking at, a running total, or a filter someone is typing into, that is what these are for.

#### When a Query Faults
The observable expressions a query builds are not yours to see, so their faults reach you through the query instead: every Observable Query has an `OperationFault` property.
Subscribe to its `PropertyChanging` and `PropertyChanged` events to be told when one of its observable expressions, or the query as a whole, runs into a problem.
If there is more than one fault in play, the value of `OperationFault` is an `AggregateException`.

#### Keys and Order
Dictionary queries adopt the key comparer of the dictionary they observe, discovering it through `Epiforge.Extensions.Collections.Generic.IHashKeys<TKey>` or a `Dictionary<TKey, TValue>`'s own `Comparer`, so a query over a case-insensitive dictionary is itself case-insensitive.

`ObserveGroupBy`, `ObserveToLookup`, and `ObserveDistinct` do not order their results the way LINQ does.
Groupings are ordered by when they were created and the elements of a grouping by when they were added, rather than by where they occur in the source.
This is deliberate: holding a grouping at the position of its key's first occurrence would mean moving that grouping every time an element was inserted ahead of it, announcing a change to something whose membership did not change, which is the opposite of what an Observable Query is for.
Call `ObserveOrderBy` on the query, or on a grouping, when you want a defined order.

#### Enumerate Rather Than Index
Reach for `foreach` rather than the indexer, because the difference between them is larger than it looks and grows with the collection.
An enumeration takes the query's lock once and then walks a list, while the indexer takes that lock again for every element you ask for; on a large collection it must also find each one in a tree, because a query keeps its elements' positions in one so that a change repairs only what it touched.
A query does remember the position it handed out last and searches outward from there, so asking for positions in order, or near one another, costs a fraction of asking for them at random, and what remains is mostly the repeated locking rather than the search.
Walking ten thousand elements by index instead of by enumerator measured 25x to 50x slower in order, and 120x to 150x out of order; at a hundred elements it was 10x to 15x, and there the repeated locking is the whole of it.
Where you do need elements by position more than once, copy the query's contents and index the copy.

#### Choosing a Library
Three other .NET libraries keep a derived collection correct as your data changes: [DynamicData](https://github.com/reactivemarbles/DynamicData), [NMF Expressions](https://github.com/NMFCode/NMF) and [ObservableComputations](https://github.com/IgorBuchelnikov/ObservableComputations). All four filter, sort, group, project and aggregate, and all four update the result when an element's property changes rather than only when the collection does. They are all good at what they set out to do, and they set out to do different things. Everything this section says one of them does is asserted by a test in `Epiforge.Extensions.Comparisons.Tests`, which exercises all four the same way, so a statement here that stops being true fails a test; what each offers beyond that is taken from its own documentation and public API.

**Start with what you already know.** If you know `INotifyPropertyChanged`, `ObservableCollection<T>` and LINQ, this library, NMF Expressions and ObservableComputations all ask you to learn little else: each reads what to watch out of an expression you write over the collection you already have. This library wants you to observe the collection first, NMF Expressions wants you to call `WithUpdates()` on it and to give the result a successor before it follows anything, and ObservableComputations wants you to bind each computation to an `OcConsumer` whose disposal tears it down. If you already know Rx, or you use ReactiveUI, DynamicData will feel like home — and it is probably already somewhere in your dependency graph.

| What you will actually run into | This library | DynamicData | NMF Expressions | ObservableComputations |
|---|---|---|---|---|
| Where your data lives | The `ObservableCollection<T>` you already have | A `SourceCache` or `SourceList`; adapting an existing collection is possible but much slower | The collection you already have, through `WithUpdates()` | The collection you already have |
| Saying which property to watch | Read out of your expression | You name it with `AutoRefresh` — forget it and your view goes quietly stale | Read out of your expression | Read out of your expression |
| An element announcing that all its properties changed, without naming one | Followed | Followed only when told to refresh on any property | Ignored | Ignored |
| A predicate or selector which throws while the view is built | The element is left out and the fault reported on the view; it returns once it stops throwing | The view ends | Building the view throws | Building the view throws |
| A change which makes it throw | Your setter returns, the fault is reported on the view, and the view recovers | The view ends, and your setter throws if nothing handles its error; `TransformSafe` catches a projection's | Your setter throws; the view recovers | Your setter throws; the view afterward misses elements added to the collection |
| A change on another thread while your handler is still handling the view's last change | Waits until your handler returns | Waits until your handler returns | Applied to the view underneath your handler | Returns at once, and is applied once your handler returns |
| Values the view makes, when they are replaced or removed | Disposed of without being asked | Disposed of when asked, with `DisposeMany` | Not reliably disposed of | Disposed of when asked, with `CollectionDisposing` |
| Combining collections | `ObserveConcat`, chained | Union, intersection, difference, and merging a changing set of sources | Concatenation, union, intersection, difference and joins | Concatenation, union, intersection, difference and zipping |
| Showing only what is on screen | A fixed slice that stays correct | Live paging and virtualization driven by a stream of requests | The first elements of an ordering, with `TopX` | Paging |
| Composing with anything else reactive | Through its change events | Everything in Rx composes with it | Through its change events | Through its change events |

When a view observes a single value rather than a collection, three of them observe an expression and differ in what they announce:

| An observed expression which | This library | NMF Expressions | ObservableComputations |
|---|---|---|---|
| reads one value twice, when that value changes | Announces the new result once | Announces a result the expression never had, then the new one | Announces the new result once |
| does not change when what it reads does | Announces nothing | Announces nothing | Announces its unchanged result |
| reads an element of a list by index | Follows the element there | Follows the element there | Does not follow it |
| catches an exception with `try` and `catch` | Observes the try | Refuses it | Observes the try |
| contains a query, such as a `Count` with a predicate | Refuses it; use a query such as `ObserveCount` instead | Follows it over a sequence given `WithUpdates()` | Follows its own operators, such as `Filtering(…).Count`, but not LINQ's |

**Use DynamicData if** you are already in Rx; you need to combine several collections by set operations; you need live paging, virtualization, size limits or expiry; you need asynchronous projections; or you want the reassurance of a large and long-established user base.

**Use NMF Expressions if** your sorted views are large and change far more often than anyone reads them all, or if you need what it offers beyond the others: transactions which apply several changes as one, a parallel execution engine, expressions which can be written back through, and incremental functions you can reuse.

**Use ObservableComputations if** sorting views of about a thousand elements is where your time goes and the code your views run never throws; or if you want its dispatching of computations to other threads, pausing, paging or zipping.

**Use this library if** you want a live view of a collection you already have with the least new vocabulary; if the code your views run may throw and your application has to keep going; if changes come from more than one thread; or if you care most about what a filtered or grouped view costs, whether for an individual property change or for each element it holds.

#### Measured Against Other Libraries
These are from the benchmarks in this repository, against DynamicData 9.4.33, NMF Expressions 2.3.0 and ObservableComputations 2.3.0, at a thousand elements unless stated otherwise, all on one machine. Each propagation figure is per property change, above what the same changes cost with nothing observing them at all. The best in each row is in bold.

| | This library | DynamicData | NMF Expressions | ObservableComputations |
|---|---|---|---|---|
| A property change that does not alter a filtered view | **0 B**, **13.2 ns** | 608 B, 201.9 ns | 129 B, 31.1 ns | **0 B**, 13.7 ns |
| The same at a hundred thousand | **0 B**, **66.7 ns** | 608 B, 367.6 ns | 128 B, 370.1 ns | **0 B**, 97.3 ns |
| An element moving into or out of a filtered view | **72 B**, **108.8 ns**, or 0 B and 76.5 ns with nothing handling its events | 704 B, 306.7 ns | 592 B, 146.3 ns | 84 B, 573.5 ns |
| The same at ten thousand | **72 B**, **135.7 ns**, or 0 B and 100.1 ns with nothing handling its events | 704 B, 604.7 ns | 592 B, 200.3 ns | 84 B, 6,753.2 ns |
| Building a filtered view | **856 KB**, **306 μs** | 4,120 KB, 2,513 μs | 2,023 KB, 456 μs | 1,071 KB, 314 μs |
| An element moving in a sorted view | 87 B, 338.3 ns | 414 B, 999.2 ns | 556 B, 340.5 ns | **72 B**, **277.8 ns** |
| The same at ten thousand | 96 B, 450.4 ns | 432 B, 9,231.5 ns | 556 B, **363.8 ns** | **72 B**, 5,498.4 ns |
| An element moving to a random place in a sorted view | 96 B, 684.1 ns | 432 B, 1,098.5 ns | 603 B, **583.4 ns** | **72 B**, 655.5 ns |
| The same at ten thousand | 96 B, 1,491.7 ns | 432 B, 10,811.4 ns | 603 B, **1,176.9 ns** | **72 B**, 10,227.4 ns |
| Building a sorted view | **999 KB**, 346 μs | 3,938 KB, 2,347 μs | 1,857 KB, 402 μs | 1,094 KB, **317 μs** |
| An element changing group | 168 B, **128.9 ns** | 1,936 B, 633.2 ns | 640 B, 174.5 ns | **154 B**, 131.4 ns |
| The same at ten thousand | 168 B, **204.6 ns** | 1,936 B, 648.7 ns | 640 B, 607.5 ns | **153 B**, 308.3 ns |
| The same at 65,536 | 168 B, **233.1 ns** | 1,936 B, 755.7 ns | 640 B, 11,073.5 ns | **153 B**, 1,297.0 ns |
| Building a grouped view | **942 KB**, **265 μs** | 4,119 KB, 2,392 μs | 2,118 KB, 519 μs | 1,118 KB, 366 μs |
| What a live filtered view holds, per element | **823 B** | 1,865 B | 1,281 B | 886 B |
| What a live sorted view holds, per element | 905 B | 1,922 B | 1,193 B | **898 B** |
| What a live grouped view holds, per element | **831 B** | 2,002 B | 1,298 B | 860 B |

**Filtering is this library's, at every size.** A property change which does not move an element in or out of a filtered view allocates nothing here, at a thousand, ten thousand and a hundred thousand elements alike: the predicate is re-evaluated in place and the view stays silent when the answer has not moved. ObservableComputations comes within 8% of it up to ten thousand elements, also allocating nothing, and falls to 1.46x its time at a hundred thousand. When the change does move the element in or out of the view, this library still leads, at 108.8 ns and 72 B a change with something handling the view's events and 76.5 ns and nothing without. ObservableComputations allocates only 84 B for it but takes 573.5 ns at a thousand elements and 6,753.2 ns at ten thousand, 11.78x the time for ten times the elements; NMF Expressions takes 146.3 ns and 592 B, and DynamicData 306.7 ns and 704 B. DynamicData's model is a stream of change sets, so a refresh has to materialize one; neither is a defect. **One library pays per change and another pays per change that matters.**

**Sorting has no leader.** ObservableComputations moves an element in a sorted view of a thousand in 0.82x this library's time, allocating 72 B against 87 B, but its cost grows fastest with the view, to 12.2x this library's at ten thousand. NMF Expressions' cost barely grows at all, which makes it the fastest from a few thousand elements on, but it allocates 556 B a change and rebuilds its order whenever it is read: one walk of a sorted view of ten thousand allocates 371 KB there, against 78 KB here and nothing in either of the others, and a view of ten thousand read after every thousand changes costs this library 0.79x NMF Expressions' time. This library sits between them, level with NMF Expressions at a thousand elements and at 1.40x its time for four thousand and 1.24x for ten thousand, and ahead of DynamicData at every size measured. When an element moves to a random place rather than a couple of places, NMF Expressions is the fastest, in 0.85x this library's time among a thousand elements and 0.79x among ten thousand; ObservableComputations takes 0.96x this library's time among a thousand and 6.86x among ten thousand, and DynamicData 1.61x and 7.25x.

**Grouping is level at a thousand elements and this library's above that.** It moves an element between groups level with ObservableComputations at a thousand elements, 0.98x its time in one instrument and 1.03x in another, then in 0.96x at four thousand, 0.66x at ten thousand and 0.18x at 65,536, allocating 168 B against its 153 to 198. NMF Expressions takes 1.35x this library's time at a thousand and falls further behind from there, to 47.5x at 65,536. This library's cost grows 1.81x from a thousand elements to 65,536, against ObservableComputations' 9.87x and NMF Expressions' 63.5x, for the reason below; DynamicData's does not grow at all, but it starts high enough that it takes 3.24x this library's time at 65,536.

**Against DynamicData, the size of the view moves the two rows differently.**

- **Sorting.** DynamicData's cost per move grows with the collection while this library's barely does, so this library's lead widens with it: 2.92x at a thousand elements, 9.34x at four thousand and 20.5x at ten thousand.
- **Grouping.** DynamicData's cost per migration is flat and this library's grows slowly enough that the two no longer cross at any size measured: this library is faster by 4.91x at a thousand elements, 3.17x at ten thousand and 3.24x at 65,536, and DynamicData holds 2.42x the memory, 1,954 B per element against 806 at ten thousand.

**The grouping cost depends on where in its group an element sits, and the benchmarks favor where they put it.** A grouping here keeps its elements in the order they were added, so moving one out of its old group means finding it first. It searches from both ends of the group at once, a comparison of references for each element passed, so an element which joined its group lately or long ago is found almost at once; the benchmarks move the same elements back and forth, so the element leaving a group is always one which joined it lately. One in the middle of a large group still costs a search of half of it. DynamicData's groups are keyed rather than positional, so a removal is a dictionary operation and costs the same wherever the element sits. If you need the elements of a group in a stable order, that search is what you are paying for. **A lookup built with `ObserveToLookup` is the same shape as a grouping here and behaves the same way.**

**What decides both is the size of the view the operator sees, not the size of your collection.** Filter ten thousand elements down to a thousand and then sort, and you are on the small-view side of every sorting comparison above.

**Against DynamicData, allocation does not cross.** At every size measured, this library allocates less for the same work: nothing at all for a filtered view, 0.09x DynamicData's for grouping, 0.21x for sorting. ObservableComputations allocates less than this library for sorting, and for grouping at a thousand and ten thousand elements but not at four thousand.

**The propagation advantage is largest at the sizes most applications use, and it narrows above them.** Per property change above the floor, this library costs 13.9 ns at a thousand elements, 15.5 ns at ten thousand and 66.7 ns at a hundred thousand, against DynamicData's 212.1, 233.5 and 367.6 ns — a lead of 15.2x, then 15.1x, then 5.5x. The allocation figure is unchanged across all three sizes; the time figure is not. A hundred thousand observations do not fit in cache, and a library which has driven its own per-change work to near zero has nothing left to hide a cache miss behind. The advantage shrinks from very large to large.

**Composition behaves.** Ordering or grouping a filtered view costs this library and DynamicData close to the sum of its parts rather than more, so a chain does not change which one to prefer — only the size of the view arriving at each stage does.

Two more things worth knowing before you weigh any of the above.

**A live view is not free in any library.** A filtered view over ten thousand elements holds 8.1 MB here, 9.0 MB in ObservableComputations, 12.7 MB in NMF Expressions and 18.6 MB in DynamicData, against 960 KB for the elements themselves. This library's filtered and grouped views hold the least of the four, 823 B per element filtering a thousand and 831 B grouping them against ObservableComputations' 886 B and 860 B, and ObservableComputations' sorted views hold the least at a thousand elements, 898 B per element against this library's 905 B, while at four thousand and ten thousand this library's hold less, 842 B and 894 B against its 894 B and 903 B. DynamicData's sorted and grouped figures are above its cache, which holds another 55 B per element. Building a view is likewise proportional to the size of the collection in all four. Build one and keep it; none of them rewards building views casually.

**`ToObservableChangeSet()` over an existing `ObservableCollection<T>` costs DynamicData about 160x what its own `SourceCache` does** for the same property changes. That is the path you land on if you adopt it without changing where your data lives, and it is worth knowing about before you do.

These comparisons were written by someone who uses none of the other three, which is a real limitation on them. The harness and the tests are in this repository, the workloads are ordinary ones, and corrections are welcome.

### Ahead-of-Time Compilation
Observable expressions and queries work where the runtime cannot generate code, as under Native AOT and on iOS. Both ways an expression gets observed work there, the shortcut and the graph, and they give the same values through the same events as they do with a JIT. This was checked under Native AOT on macOS on Apple Silicon and on Linux on x64, and in the iOS 26.5 simulator, where a .NET for iOS app is compiled ahead of time by Mono in Debug as well as in Release. Before 7.1.0, nearly every observation threw `PlatformNotSupportedException` in all of them. An app which turns on Mono's interpreter, as .NET MAUI does for Debug builds, can generate code by interpreting it, and it worked before 7.1.0 too.

The package is marked as compatible with ahead-of-time compilation, so trimming and AOT analysis report nothing for it. Where it rebuilds a part of an expression you hand it, whatever that part needs from trimming or code generation, your own expression needed first.

Without code generation, an observation costs more, because the observer evaluates the expressions it would otherwise compile, reaching each property by reflection and each method without a generated method, and interprets any it cannot evaluate that way. These figures come from the same benchmarks run with dynamic code turned off, which takes the paths an application compiled ahead of time takes while still running on the JIT, so they price the paths rather than any one compiler. Each change is above what the same changes cost with nothing observing them.

| | With dynamic code | Without |
|---|---|---|
| An evaluation on the shortcut | 12.5 ns, 0 B | 44.4 ns, 24 B |
| A property change in a filtered view of a thousand, on the shortcut | 13.2 ns, 0 B | 49.8 ns, 24 B |
| The same on the graph | 35.7 ns, 24 B | 569.1 ns, 72 B |
| Building and disposing of a filtered view of a thousand | 290 μs, 856 KB | 583 μs, 903 KB |

Without code generation, the shortcut costs 3.6x to 3.8x what it does with a JIT, the graph 15.9x, and building a view 2.01x. Under Native AOT itself, an evaluation on the shortcut costs 38.4 ns and 24 B. So the shortcut matters more there: an expression on the graph costs 11.4x what one on the shortcut does, against 2.7x with a JIT. `DirectSubscriptionAnalyzer` tells you which one an expression gets.

---

## Platforms

### <img src="Epiforge.Extensions.Platforms.Windows/NuGet.png" width="64" height="64" alt="Platforms.Windows" style="float: left !important; padding-right: 0.5em !important;"> Windows
[![Epiforge.Extensions.Platforms.Windows Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Platforms.Windows.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.platforms.windows)](https://www.nuget.org/packages/Epiforge.Extensions.Platforms.Windows)

This library includes utilities for interoperation with Microsoft Windows, including:

* `Activation` - provides information relating to Windows Activation
* `ConsoleAssist` - provides methods for interacting with consoles
* `Cursor` - wraps Win32 API methods dealing with the cursor
* `Shell` - wraps methods of the WScript.Shell COM object (specifically useful for invoking its `CreateShortcut` function)
* `Theme` - represents the current Windows theme (its `Color` and `IsDark` properties report what Windows says and are not settable)
* `User` - provides properties concerning the user, including `IdleTime`; `GetIdleTime` returns the same figure along with whether it is exact, which it is except when the session could not supply an absolute last-input timestamp and the system tick count has already wrapped
* `WindowingSystem` - provides methods for dealing with the windowing system, including reading and setting the foreground window, reading its position, and flashing windows

Also provides extension methods for dealing with processes, including:

* `CloseMainWindowAsync` - close the main window of the specified process
* `GetParentProcess` - gets the parent process of the specified process

---

# License
[Apache 2.0 License](LICENSE)

# Contributing
[Click here](CONTRIBUTING.md) to learn how to contribute.

# Acknowledgements
Makes use of the following excellent libraries:
* [AsyncEx](https://github.com/StephenCleary/AsyncEx) by Stephen Cleary
* [Ben.Demystifier](https://github.com/benaadams/Ben.Demystifier) by Ben Adams
* [PolySharp](https://github.com/Sergio0694/PolySharp) by Sergio Pedri