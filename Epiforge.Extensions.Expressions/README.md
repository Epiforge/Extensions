This library has useful tools for dealing with expressions:

* `ExpressionEqualityComparer` - Defines methods to support the comparison of expression trees for equality
* `ExpressionExtensions`, providing:
  * `Duplicate` - Duplicates the specified expression tree
  * `SubstituteMethods` - Recursively scans an expression tree to replace invocations of specific methods with replacement methods

# Observable Expressions
An `ExpressionObserver` accepts a `LambdaExpression` and arguments to pass to it, dissects the lambda's body, and hooks into change notification events for properties (`INotifyPropertyChanged`), collections (`INotifyCollectionChanged`), and dictionaries (`Epiforge.Extensions.Collections.INotifyDictionaryChanged`).

```csharp
// Employee implements INotifyPropertyChanged
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr subscribed to elizabeth's PropertyChanged
```

## Following Changes
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

## When Evaluation Fails
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

## Disposing of an Observation
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

## Changes From Other Threads
A source can change on any thread, including while an observation of it is being built or evaluated on another. No change is lost: once the changes stop, every observation settles on what its expression gives over its sources as they then stand. You can dispose of an observation on any thread, too, even while another thread is evaluating it.

An observation is evaluated on the thread which raised the change, unless another thread is evaluating it at that moment, in which case that thread evaluates it again before it finishes and the thread which raised the change carries on without waiting. So the `PropertyChanged` which follows a change can arrive on a different thread from the change, and a setter can return before an observation reflects it.

Two things stay yours. While changes arrive on several threads at once, an observation can briefly report a value computed from reads taken at different moments, as any code reading shared state without a lock would, and it settles once they stop. And the observer reads your sources on whichever thread is evaluating, so a source must be safe to read while another thread changes it. `ObservableCollection<T>` is not: read in the middle of a change, it can throw, the observation reports that fault until the change's notification has it read again, and a `ConditionAsync` waiting on it completes with the fault.

If nothing an observer's observations read changes while another thread builds, evaluates or disposes of one of them, as when all of it happens on a user interface thread, you can say so by setting `IsThreadSafe` to `false` in the options you hand the observer. It then evaluates without the interlocked operations the promises above cost: a raise against a thousand observations of one object costs 0.50x what it otherwise would, and a property change in a filtered view of a thousand 0.73x. It is `true` by default, and setting it where changes do cross threads gives those promises up: a change can be lost, an observation can be evaluated before it is built, and observations disposed of on several threads at once can leave parts of themselves cached.

Queries keep the same promise once a `CollectionObserver` is observing what they are built over. When you ask a `CollectionObserver` to begin observing a sequence, whether an enumerable, a list or a dictionary, make sure nothing changes it until the call returns. From then on it can change on any thread, and a query built over it, or over another query, while it does misses none of those changes. So that what a query reads always matches what it has been told, observing a sequence which announces its changes keeps a copy of it, which costs 8 B per element, a reference to each, and, for a change far from the end of a long list, the time to move the rest of the copy: inserting and removing at the front of ten thousand elements costs 2.3 μs more, 1.6x what the list itself spends moving them. A list must announce its changes in the order it makes them, which one writer at a time ensures. A dictionary need not, because the observer reads each key a change names from the dictionary itself, so an `ObservableConcurrentDictionary<TKey, TValue>` can be changed by several threads at once. A dictionary's copy costs about 28 B per key. The sequences an `ObserveSelectMany` selector returns which announce their changes are observed the same way from the moment the query first reads them, which is when it is built or when the selector first returns one, so one must not change while that happens. A second operand of `ObserveConcat` which is not a query of this library is observed the same way from the moment the concatenation is built, so it must not change while that happens.

A query's results change on whichever thread evaluated the change, so reading them on another thread while they change is a race of yours rather than the query's: enumerating, indexing or copying one can throw or miss elements. To read a query on another thread, observe it through `ObserveUsingSyncRoot` and take the same lock while you read, or through `ObserveUsingSynchronizationContext` so that it changes on the thread which reads it. A query announces its changes while it and the queries it is built over hold the locks under which they change, so a handler of those announcements must not wait on another thread which may change any of them, as sending work to a user interface thread and waiting for it does. `ObserveUsingSynchronizationContext`, `ObserveUsingSyncRoot` and `ObserveUsingSynchronizationCallback` wait on their context, lock or callback only once those locks are released, so the thread they wait on can change what the query is built over while a change from another thread waits to be applied, and a change made within a handler of another query's announcements reaches them once that announcement is over. A query can be built over one synchronized with a context, eventually or not, on any thread.

## Fields Are Read Once
Whatever a field held when an observation began is what that observation goes on using — a captured local, a field of your own class, and a static field alike. Assigning it afterward does not reach an observation that already exists. Static properties behave the same way, so `e => e.Hired < DateTime.Now` compares against the moment it was created for as long as it lives.

```csharp
var threshold = low;
using var expr = observer.Observe(e => e.Salary > threshold.Amount, elizabeth);
threshold = high;    // expr is still comparing against low
low.Amount = 50000;  // expr re-evaluates
high.Amount = 90000; // expr does not
```

If you want the comparison to follow the value, do not assign the field — make the thing it points at a property of an object that notifies, and read that instead.

## Optimizing Expressions
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

## How an Expression Gets Observed
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

# Observable Queries
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

## How Observable Queries Work and When to Use Them
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

## When a Query Faults
The observable expressions a query builds are not yours to see, so their faults reach you through the query instead: every Observable Query has an `OperationFault` property.
Subscribe to its `PropertyChanging` and `PropertyChanged` events to be told when one of its observable expressions, or the query as a whole, runs into a problem.
If there is more than one fault in play, the value of `OperationFault` is an `AggregateException`.

## Keys and Order
Dictionary queries adopt the key comparer of the dictionary they observe, discovering it through `Epiforge.Extensions.Collections.Generic.IHashKeys<TKey>` or a `Dictionary<TKey, TValue>`'s own `Comparer`, so a query over a case-insensitive dictionary is itself case-insensitive.

`ObserveGroupBy`, `ObserveToLookup`, and `ObserveDistinct` do not order their results the way LINQ does.
Groupings are ordered by when they were created and the elements of a grouping by when they were added, rather than by where they occur in the source.
This is deliberate: holding a grouping at the position of its key's first occurrence would mean moving that grouping every time an element was inserted ahead of it, announcing a change to something whose membership did not change, which is the opposite of what an Observable Query is for.
Call `ObserveOrderBy` on the query, or on a grouping, when you want a defined order.

## Enumerate Rather Than Index
Reach for `foreach` rather than the indexer, because the difference between them is larger than it looks and grows with the collection.
An enumeration takes the query's lock once and then walks a list, while the indexer takes that lock again for every element you ask for; on a large collection it must also find each one in a tree, because a query keeps its elements' positions in one so that a change repairs only what it touched.
A query does remember the position it handed out last and searches outward from there, so asking for positions in order, or near one another, costs a fraction of asking for them at random, and what remains is mostly the repeated locking rather than the search.
Walking ten thousand elements by index instead of by enumerator measured 25x to 50x slower in order, and 120x to 150x out of order; at a hundred elements it was 10x to 15x, and there the repeated locking is the whole of it.
Where you do need elements by position more than once, copy the query's contents and index the copy.

## Choosing a Library
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

## Measured Against Other Libraries
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
| Building a sorted view | **999 KB**, 346 μs | 3,938 KB, 2,347 μs | 1,857 KB, 402 μs | 1,094 KB, **317 μs** |
| An element changing group | 168 B, **128.9 ns** | 1,936 B, 633.2 ns | 640 B, 174.5 ns | **154 B**, 131.4 ns |
| The same at ten thousand | 168 B, **204.6 ns** | 1,936 B, 648.7 ns | 640 B, 607.5 ns | **153 B**, 308.3 ns |
| The same at 65,536 | 168 B, **233.1 ns** | 1,936 B, 755.7 ns | 640 B, 11,073.5 ns | **153 B**, 1,297.0 ns |
| Building a grouped view | **942 KB**, **265 μs** | 4,119 KB, 2,392 μs | 2,118 KB, 519 μs | 1,118 KB, 366 μs |
| What a live filtered view holds, per element | **823 B** | 1,865 B | 1,281 B | 886 B |
| What a live sorted view holds, per element | 905 B | 1,922 B | 1,193 B | **898 B** |
| What a live grouped view holds, per element | **831 B** | 2,002 B | 1,298 B | 860 B |

**Filtering is this library's, at every size.** A property change which does not move an element in or out of a filtered view allocates nothing here, at a thousand, ten thousand and a hundred thousand elements alike: the predicate is re-evaluated in place and the view stays silent when the answer has not moved. ObservableComputations comes within 8% of it up to ten thousand elements, also allocating nothing, and falls to 1.46x its time at a hundred thousand. When the change does move the element in or out of the view, this library still leads, at 108.8 ns and 72 B a change with something handling the view's events and 76.5 ns and nothing without. ObservableComputations allocates only 84 B for it but takes 573.5 ns at a thousand elements and 6,753.2 ns at ten thousand, 11.78x the time for ten times the elements; NMF Expressions takes 146.3 ns and 592 B, and DynamicData 306.7 ns and 704 B. DynamicData's model is a stream of change sets, so a refresh has to materialize one; neither is a defect. **One library pays per change and another pays per change that matters.**

**Sorting has no leader.** ObservableComputations moves an element in a sorted view of a thousand in 0.82x this library's time, allocating 72 B against 87 B, but its cost grows fastest with the view, to 12.2x this library's at ten thousand. NMF Expressions' cost barely grows at all, which makes it the fastest from a few thousand elements on, but it allocates 556 B a change and rebuilds its order whenever it is read: one walk of a sorted view of ten thousand allocates 371 KB there, against 78 KB here and nothing in either of the others, and a view of ten thousand read after every thousand changes costs this library 0.79x NMF Expressions' time. This library sits between them, level with NMF Expressions at a thousand elements and at 1.40x its time for four thousand and 1.24x for ten thousand, and ahead of DynamicData at every size measured.

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

