This library has useful tools for dealing with expressions:
* `ExpressionEqualityComparer` - Defines methods to support the comparison of expression trees for equality
* `ExpressionExtensions`, providing:
  * `Duplicate` - Duplicates the specified expression tree
  * `SubstituteMethods` - Recursively scans an expression tree to replace invocations of specific methods with replacement methods

# Observable
This library accepts a `LambdaExpression` and arguments to pass to it, dissects the `LambdaExpression`'s body, and hooks into change notification events for properties (`INotifyPropertyChanged`), collections (`INotifyCollectionChanged`), and dictionaries (`Epiforge.Extensions.Collections.INotifyDictionaryChanged`).

```csharp
// Employee implements INotifyPropertyChanged
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr subscribed to elizabeth's PropertyChanged
```

Then, as changes involving any elements of the expression occur, a chain of automatic re-evaluation will get kicked off, possibly causing the observable expression's `Evaluation` property to change.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr.Evaluation.Result == 9
elizabeth.Name = "Lizzy";
// expr.Evaluation.Result == 5
```

Also, since exceptions may be encountered after an observable expression was created due to subsequent element changes, observable expressions include a `Fault` property in their evaluations, which will be set to the exception that was encountered during evaluation.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
var expr = observer.Observe(e => e.Name.Length, elizabeth);
// expr.Evaluation.Fault is null
elizabeth.Name = null;
// expr.Evaluation.Fault is NullReferenceException
```

Observable expressions raise property change events of their own, so listen for those (kinda the whole point)!

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
            // Whoops
        }
        else
        {
            // Do something with result
        }
    }
};
```

While an expression is working out its new value it can pass through results that were never simultaneously true of its inputs; an addition whose two operands both derive from the same property has to compute one of them before the other. You are not told about those. Every event you receive carries a value the expression genuinely held, so a subscriber that redraws or broadcasts on one does that work once rather than twice, the second time only to correct the first.

Nor are you told anything at all when a change leaves the value where it found it. That is decided by a comparison, using the same equality the expression uses everywhere else, and it happens before `PropertyChanging` rather than after — so a handler for that event still reads the previous value, and a pair of events always means the value really moved.

When you dispose of your observable expression, it will disconnect from all the events.

```csharp
var elizabeth = Employee.GetByName("Elizabeth");
var observer = new ExpressionObserver();
using (var expr = observer.Observe(e => e.Name.Length, elizabeth))
{
    // expr subscribed to elizabeth's PropertyChanged
}
// expr unsubcribed from elizabeth's PropertyChanged
```

## How an Expression Gets Observed
`Observe` takes a shortcut when it can and builds a graph when it cannot, deciding once when the observation is created. You receive the same values through the same events either way; the shortcut is just faster and lighter.

The shortcut handles an expression built from these:

* the argument, constants, and captured locals
* fields, on anything above — including static fields
* properties and indexers whose target is one of the above
* static properties
* operators, where one resolved to a method needs a return type nothing could dispose — `==` on strings qualifies
* method calls, on that same condition, where the target and every argument are themselves handled — `string.IsNullOrEmpty(e.Name)` qualifies

Everything else builds the graph: `?:`, `&&`, `||` and `??`; anything read through a property, such as `e => e.Name.Length`; a call to the get method of a property or an indexer, which is how an indexer written in C# arrives; object and collection construction; and anything you have configured the observer to ignore notifications for or to dispose.

To find out about a particular expression, ask:

```csharp
var analysis = new DirectSubscriptionAnalyzer(options).Analyze(expression.Body);
// analysis.IsEligible is false
// analysis.Ineligibility is DirectSubscriptionIneligibility.DeferredBranch
// analysis.IneligibleExpression is the part responsible
```

Hand the analyzer the same options you hand the observer, since some of them decide what gets subscribed to at all. Set `UseDirectSubscription` to `false` if you would rather always have the graph; it is `true` by default.

### Fields Are Read Once
Whatever a field held when an observation began is what that observation goes on using — a captured local, a field of your own class, and a static field alike. Assigning it afterward does not reach an observation that already exists. Static properties behave the same way, so `e => e.Hired < DateTime.Now` compares against the moment it was created for as long as it lives.

```csharp
var threshold = low;
using var expr = observer.Observe(e => e.Salary > threshold.Amount, elizabeth);
threshold = high;    // expr is still comparing against low
low.Amount = 50000;  // expr re-evaluates
high.Amount = 90000; // expr does not
```

If you want the comparison to follow the value, do not assign the field — make the thing it points at a property of an object that notifies, and read that instead.

Observable expressions will also try to automatically dispose of disposable objects they create in the course of their evaluation when and where it makes sense. Use the `ExpressionObserverOptions` class for more direct control over this behavior.
You can use the `Optimizer` property to specify an optimization method to invoke automatically during the observable expression creation process.
We recommend Tuomas Hietanen's [Linq.Expression.Optimizer](https://thorium.github.io/Linq.Expression.Optimizer), the utilization of which would look like so:

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

# Observable Queries
This library provides re-implementations of LINQ operations, but instead of returning `IEnumerable<T>`s and simple values, these return `IObservableCollectionQuery<T>`s, `IObservableDictionaryQuery<TKey, TValue>`s, and `IObservableScalarQuery<T>`s.
This is because, unlike traditional LINQ operations, these implementations continuously update their results until those results are disposed.
What they hand back is a read-only view of the source: change the source, and the query brings itself up to date. Queries do not implement the mutating range collection and dictionary interfaces, because a query result is not somewhere you put things.

But... what could cause those updates?

* the source is enumerable, implements `INotifyCollectionChanged`, and raises a `CollectionChanged` event
* the source is a dictionary, implements `Epiforge.Extensions.Collections.INotifyDictionaryChanged<TKey, TValue>`, and raises a `DictionaryChanged` event
* the elements in the enumerable (or the values in the dictionary) implement `INotifyPropertyChanged` and raise a `PropertyChanged` event
* a reference enclosed by a selector or a predicate passed to the method implements `INotifyCollectionChanged`, `Epiforge.Extensions.Collections.INotifyDictionaryChanged<TKey, TValue>`, or `INotifyPropertyChanged` and raises one of their events

That last one might be a little surprising, but this is because all selectors and predicates passed to Observable Query methods become Observable Expressions (see above).
This means that you will not be able to pass one that an `ExpressionObserver` cannot observe (e.g. a lambda expression that can't be converted to an expression tree or that contains nodes that are unsupported).
But, in exchange for this, you get all kinds of notification plumbing that's just handled for you behind the scenes.

Suppose, for example, you're working on an app that displays a list of notes and you want the notes to be shown in descending order of when they were last edited.

```csharp
var notes = new ObservableCollection<Note>();
var collectionObserver = new CollectionObserver();

var observedNotes = collectionObserver.ObserveReadOnlyList(notes);
var orderedNotes = observedNotes.ObserveOrderBy(note => note.LastEdited, isDescending: true);
notesViewControl.ItemsSource = orderedNotes;
```

From then on, as you add `Note`s to the `notes` observable collection, the `IObservableCollectionQuery<Note>` named `orderedNotes` will be kept ordered so that `notesViewControl` displays them in the preferred order.

Since `IObservableCollectionQuery<T>`'s are automatically subscribing to events for you, you do need to call `Dispose` on them when you don't need them any more.

```csharp
void Page_Unload(object? sender, EventArgs e)
{
    orderedNotes.Dispose();
    observedNotes.Dispose();
}
```

Ahh, but what about exceptions?
Well, Observable Expressions contain a `Fault` element in their `Evaluation` properties, but... you don't really see those Observable Expressions as an Observable Query caller, do ya?
For that reason, Observable Queries all have `OperationFault` properties.
You may subscribe to their `PropertyChanging` and `PropertyChanged` events to be notified when an Observable Expression or the overall Observable Query runs into a problem.
If there is more than one fault in play, the value of `OperationFault` will be an `AggregateException`.

Dictionary queries adopt the key comparer of the dictionary they observe, discovering it through `Epiforge.Extensions.Collections.Generic.IHashKeys<TKey>` or a `Dictionary<TKey, TValue>`'s own `Comparer`, so a query over a case-insensitive dictionary is itself case-insensitive.

`ObserveGroupBy`, `ObserveToLookup`, and `ObserveDistinct` do not order their results the way LINQ does.
Groupings are ordered by when they were created and the elements of a grouping by when they were added, rather than by where they occur in the source.
This is deliberate: holding a grouping at the position of its key's first occurrence would mean moving that grouping every time an element was inserted ahead of it, announcing a change to something whose membership did not change, which is the opposite of what an Observable Query is for.
Call `ObserveOrderBy` on the query, or on a grouping, when you want a defined order.

Reach for `foreach` rather than the indexer, because the difference between them is larger than it looks and grows with the collection.
An enumeration takes the query's lock once and then walks a list, while the indexer takes that lock again for every element you ask for; on a large collection it must also find each one in a tree, because a query keeps its elements' positions in one so that a change repairs only what it touched.
A query does remember the position it handed out last and searches outward from there, so asking for positions in order, or near one another, costs a fraction of asking for them at random, and what remains is mostly the repeated locking rather than the search.
Walking ten thousand elements by index instead of by enumerator measured between thirty and fifty times slower in order, and around two hundred times out of order; at a hundred elements it was about fifteen, and there the repeated locking is the whole of it.
Where you do need elements by position more than once, copy the query's contents and index the copy.

Since the `ExpressionObserver` has a number of options governing its behavior, you may optionally pass one you've made to the constructor of `CollectionObserver` to ensure those options are obeyed when Observable Expressions are created to enable your Observable Queries.

## How Observable Queries Work and When to Use Them
It is worth being plain about what kind of thing this is, because "LINQ, but observable" undersells it and sets the wrong expectations.

A LINQ query is a description of a computation you run. Run it again and it does all of the work again. An Observable Query is not re-run. It is a small machine that holds the answer and repairs it, so when something changes, only the parts of the answer that depended on that thing are recomputed. The work is proportional to what changed rather than to how much data you have. If you want the name the literature uses for this idea, it is incremental, or self-adjusting, computation.

Three things that might otherwise look like arbitrary restrictions fall straight out of that:
1. Your selectors and predicates have to be expression trees rather than delegates because the machine has to read them to find out what they depend on. A delegate is opaque; there is nothing in it to subscribe to.
2. You have to dispose of a query because it is holding subscriptions to everything it depends on, and those subscriptions are the entire reason the answer stays right.
3. Faults reach you through `OperationFault` instead of being thrown, because the evaluation that failed happened later, on whatever thread raised the change. By then there is no call of yours left on the stack to throw out of.

What is not free is construction. Building the machine means building an observable expression for every element the query touches, and that is proportional to the size of the collection. So build a query once and hold onto it. Do not build one per frame, per request, or per keystroke. The bargain is that you pay up front and then stop paying to read.

Reading is also cheaper than being told. A query subscribes to the one it is built on only while something is subscribed to it, and a filtered query works out where a change landed, and describes it, only when something will receive that description. So subscribe when you need to be told what changed, and simply read the query when you only need its answer to be right.

Which is also how to decide whether you want one. If you compute a result once and move on, plain LINQ is cheaper and simpler, and you should use it. If a result has to stay correct across a long run of small changes, such as a list someone is looking at, a running total, or a filter someone is typing into, that is what these are for.

## Which of These Should I Use?
The nearest thing to this in .NET is [DynamicData](https://github.com/reactivemarbles/DynamicData), and it is a good library. Both keep a derived collection correct as your data changes — filter, sort, group, project, aggregate — and both update the result when an element's property changes rather than only when the collection does. Here is how to tell which one you want.

**Start with what you already know.** If you know `INotifyPropertyChanged`, `ObservableCollection<T>` and LINQ, this library asks you to learn almost nothing else: you point it at the collection you already have, write `ObserveWhere(person => person.Rank > 0)`, and bind the result. If you already know Rx, or you use ReactiveUI, DynamicData will feel like home and this library will feel like an unfamiliar dialect — and it is probably already somewhere in your dependency graph. Most of the rest follows from that one answer.

| What you will actually run into | This library | DynamicData |
|---|---|---|
| Where your data lives | The `ObservableCollection<T>` you already have | A `SourceCache` or `SourceList`; adapting an existing collection is possible but much slower |
| Saying which property to watch | Read out of your expression | You name it with `AutoRefresh` — forget it and your view goes quietly stale |
| A property change that does not change the result | Costs nothing | Materializes a change set each time |
| When your projection throws | A fault you can bind to; the query keeps working | Ends the subscription, as Rx does, unless you use `TransformSafe` |
| Combining collections | `ObserveConcat`, chained | Also union, intersection, difference, and merging a changing set of sources |
| Showing only what is on screen | A fixed slice that stays correct | Live paging and virtualization driven by a stream of requests |
| Composing with anything else reactive | Not applicable | Everything in Rx composes with it |
| An expression it cannot analyze | Falls back to a slower path, says so in your log, results unchanged | Not applicable |

**Use DynamicData if** you are already in Rx; you need to combine several collections by set operations; you need live paging, virtualization, size limits or expiry; you need asynchronous projections; or you want the reassurance of a large and long-established user base.

**Use this library if** you want a live view of a collection you already have, with the least new vocabulary, and you care about what an individual property change costs.

### What It Costs
These are from the benchmarks in this repository, against DynamicData 9.4.33 at a thousand elements unless stated otherwise. Each propagation figure is per property change, above what the same changes cost with nothing observing them at all.

| | This library | DynamicData |
|---|---|---|
| A property change that does not alter a filtered view | **0 B**, **7.3 ns** | 608 B, 192.4 ns |
| An element changing group | **578 B**, **236.9 ns** | 1,891 B, 604.8 ns |
| An element moving in a sorted view | **292 B**, 1,259.5 ns | 414 B, **984.4 ns** |
| Building a filtered view | **965 KB**, **294 μs** | 4,119 KB, 2,169 μs |
| What a live filtered view holds | **934 B** per element | 1,865 B per element |

The zero is exact rather than rounded: a property change that does not move an element in or out of a filtered view allocates nothing here, at a thousand, ten thousand and a hundred thousand elements alike. This library re-evaluates the predicate in place and stays silent when the answer has not moved; DynamicData's model is a stream of change sets, so a refresh has to materialize one. Neither is a defect. **One library pays per change and the other pays per change that matters.**

**Two of those rows move with the size of the view, in opposite directions, and this is the part worth reading twice.**

- **Sorting.** DynamicData's cost per move grows with the collection while this library's barely does, so the two cross at about **1,400** elements. Below that DynamicData is 1.28x faster; at four thousand this library is 1.72x faster and at ten thousand 2.98x.
- **Grouping.** The reverse. DynamicData's cost per migration is flat while this library's grows, so the two cross at about **7,900** elements. Below that this library is 2.55x faster; at ten thousand DynamicData is 1.18x faster.

**What decides both is the size of the view the operator sees, not the size of your collection.** Filter ten thousand elements down to a thousand and then sort, and you are on the small-view side of the sorting crossover, where DynamicData wins; grouping that same thousand puts you well on this library's side of the grouping one.

**Allocation does not cross.** At every size measured, this library allocates less for the same work: nothing at all for a filtered view, about a third of DynamicData's for grouping, about seven tenths for sorting.

**Composition behaves.** Ordering or grouping a filtered view costs each library close to the sum of its parts rather than more, so a chain does not change which one to prefer — only the size of the view arriving at each stage does.

Two more things worth knowing before you weigh any of the above.

**A live view is not free in either library.** One over ten thousand elements holds about 9 MB here and about 19 MB in DynamicData, against 960 KB for the elements themselves. Building a view is likewise proportional to the size of the collection in both. Build one and keep it; neither library rewards building views casually.

**`ToObservableChangeSet()` over an existing `ObservableCollection<T>` costs DynamicData about 210x what its own `SourceCache` does** for the same property changes. That is the path you land on if you adopt it without changing where your data lives, and it is worth knowing about before you do.

These comparisons were written by someone who does not use DynamicData, which is a real limitation on them. The harness is in this repository, the workloads are ordinary ones, and corrections are welcome.
