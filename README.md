![Extensions Logo](Extensions.jpg) 

<h1>Extensions</h1>

General utilities to help with stuff in .NET Development, from Epiforge.

Supports `net462`, `net6.0`, `net7.0`, and `netstandard2.1`.

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
    - [Observable](#observable)
    - [Observable Queries](#observable-queries)
  - [Platforms](#platforms)
    - [ Windows](#-windows)
  - [Frameworks](#frameworks)
    - [ WPF](#-wpf)
- [License](#license)
- [Contributing](#contributing)
- [Acknowledgements](#acknowledgements)

# Libraries

## <img src="Epiforge.Extensions.Components/NuGet.jpg" alt="Components" style="float: left !important; padding-right: 0.5em !important;"> Components
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

Use of these methods in .NET 7 or later will simply call the built-in methods, as they are now optimized.

This library also offers `FastComparer` and `FastEqualityComparer`, which implement `IComparer` and `IEqualityComaprer`, respectively, but quickly use the methods of `Comparer<>.Default` and `EqualityComaprer<>.Default`, respectively, to do their work.

In addition (pun intended), this library offers `GenericAddition`, `GenericSubtraction`, `GenericMultiplication`, and `GenericDivision`, which will produce delegates that will perform the respective operations with values of supplied generic type arguments.

### Exceptions
This library provides extension methods for dealing with exceptions:

* `GetFullDetails` - creates a representation of an exception and all of its inner exceptions, including exception types, messages, and stack traces, and traversing multiple inner exceptions in the case of `AggregateException` and `ReflectionTypeLoadException`

### Threading
This library provides classes for use in threading scenarios:

* `AsyncSynchronizationContext` - A SynchronizationContext that uses the Task Parallel Library (TPL) to process callbacks asynchronously

---

## <img src="Epiforge.Extensions.Collections/NuGet.jpg" alt="Collections" style="float: left !important; padding-right: 0.5em !important;"> Collections
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
* `ReadOnlyRangeDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IRangeDictionary<TKey, TValue>`
* `ReadOnlyConcurrentDictionary<TKey, TValue>` is a read-only wrapper for `ObservableConcurrentDictionary<TKey, TValue>`
* `ReversedComparer<T>` is a comparer that reverses the comparison of another comparer (this is useful when you want to sort a list in the opposite order of the default sort order)

### ObjectModel
* `ObservableDictionary<TKey, TValue>`, `ObservableSortedDictionary<TKey, TValue>`, `ObservableConcurrentDictionary<TKey, TValue>` are counterparts to the BCL's `Dictionary<TKey, TValue>`, `SortedDictionary<TKey, TValue>`, and `ConcurrentDictionary<TKey, TValue>`, respectively, that implement the also included `IRangeDictionary<TKey, TValue>` and `INotifyDictionaryChanged<TKey, TValue>`. Ever want to add multiple items to a dictionary at once... or keep an eye on what's being done to it? Now you can.
* `RangeObservableCollection<T>` is a counterpart to the BCL's `ObservableCollection<T>` which implements:
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
* `ReadOnlyObservableRangeDictionary<TKey, TValue>` is a read-only wrapper for any classes implementing `IObservableRangeDictionary<TKey, TValue>`.

### Specialized
* `EquatableList<T>` is an immutable list of items which may be compared with other instances of the same type and produces a hash code based on the permutation of its contents
* `NullableKeyDictionary<TKey, TValue>` and `NullableKeySortedDictionary<TKey, TValue>` are very slim implementations of `IDictionary<TKey, TValue>` that allow a single null key (useful for some edge cases in which a null key is simply going to happen and you need to be able to deal with it; otherwise, use other dictionary classes)
* `OrderedHashSet<T>` is a counterpart to the BCL's `HashSet<T>` that maintains the order of the elements in the set. All operations are still *O(1)*, just like the original, but if you enumerate over it you will get elements in the exact order they were added. There are also methods for manipulating the order

---

## <img src="Epiforge.Extensions.Expressions/NuGet.jpg" alt="Expressions" style="float: left !important; padding-right: 0.5em !important;"> Expressions
[![Epiforge.Extensions.Expressions Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Expressions.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.expressions)](https://www.nuget.org/packages/Epiforge.Extensions.Expressions)

This library has useful tools for dealing with expressions:

* `ExpressionEqualityComparer` - Defines methods to support the comparison of expression trees for equality
* `ExpressionExtensions`, providing:
  * `Duplicate` - Duplicates the specified expression tree
  * `SubstituteMethods` - Recursively scans an expression tree to replace invocations of specific methods with replacement methods

### Observable
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

Observable expressions will also try to automatically dispose of disposable objects they create in the course of their evaluation when and where it makes sense. Use the `ExpressionObserverOptions` class for more direct control over this behavior.
You can use the `Optimizer` property to specify an optimization method to invoke automatically during the observable expression creation process.
We recommend Tuomas Hietanen's [Linq.Expression.Optimizer](https://thorium.github.io/Linq.Expression.Optimizer), the utilization of which would like like so:

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

### Observable Queries
This library provides re-implementations of LINQ operations, but instead of returning `Enumerable<T>`s and simple values, these return `IObservableCollectionQuery<T>`s, `IObservableDictionaryQuery<TKey, TValue>`s, and `IObservableScalarQuery<T>`s.
This is because, unlike traditional LINQ operations, these implementations continuously update their results until those results are disposed.

But... what could cause those updates?

* the source is enumerable, implements `INotifyCollectionChanged`, and raises a `CollectionChanged` event
* the source is a dictionary, implements `Epiforge.Extensions.Collections.INotifyDictionaryChanged<TKey, TValue>`, and raises a `DictionaryChanged` event
* the elements in the enumerable (or the values in the dictionary) implement `INotifyPropertyChanged` and raise a `PropertyChanged` event
* a reference enclosed by a selector or a predicate passed to the method implements `INotifyCollectionChanged`, `Cogs.Collections.INotifyDictionaryChanged<TKey, TValue>`, or `INotifyPropertyChanged` and raises one of their events

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

Since the `ExpressionObserver` has a number of options governing its behavior, you may optionally pass one you've made to the constructor of `CollectionObserver` to ensure those options are obeyed when Observable Expressions are created to enable your Observable Queries.

---

## Platforms

### <img src="Epiforge.Extensions.Platforms.Windows/NuGet.jpg" alt="Platforms.Windows" style="float: left !important; padding-right: 0.5em !important;"> Windows
[![Epiforge.Extensions.Platforms.Windows Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Platforms.Windows.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.platforms.windows)](https://www.nuget.org/packages/Epiforge.Extensions.Platforms.Windows)

This library includes utilities for interoperation with Microsoft Windows, including:

* `Activation` - provides information relating to Windows Activation
* `ConsoleAssist` - provides methods for interacting with consoles
* `Cursor` - wraps Win32 API methods dealing with the cursor
* `Shell` - wraps methods of the WScript.Shell COM object (specifically useful for invoking its `CreateShortcut` function)
* `Theme` - represents the current Windows theme

Also provides extension methods for dealing with processes, including:

* `CloseMainWindowAsync` - close the main window of the specified process
* `GetParentProcess` - gets the parent process of the specified process

---

## Frameworks

### <img src="Epiforge.Extensions.Frameworks.WPF/NuGet.jpg" alt="Frameworks.WPF" style="float: left !important; padding-right: 0.5em !important;"> WPF
[![Epiforge.Extensions.Frameworks.WPF Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Frameworks.WPF.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.frameworks.wpf)](https://www.nuget.org/packages/Epiforge.Extensions.Frameworks.WPF)

This library includes utilities for Windows Presentation Foundation, including:

* `ActionCommand` - a command that can be manipulated by its caller
* `ControlAssist` - provides attached dependency properties to enhance the functionality of controls (e.g. `AdditionalInputBindings`)
* `Screen` - represents a display device or multiple display devices on a single system
* `WindowAssist` - provides attached dependency properties to enhance the functionality of windows (e.g. `AutoActivation`, `BlurBehind`, `IsBlurredBehind`, `IsCaption`, `SendSystemCommand`, `SetDefaultWindowStyleOnSystemCommands`, `ShowSystemMenu`)

Also includes extension methods for visuals:

* `GetVisualAncestor` - gets the first ancestor of a reference in the Visual Tree, or <c>null</c> if none could be found
* `GetVisualDescendent` - gets the first member of a Visual Tree descending from a reference, or <c>null</c> if none could be found

Also includes extension methods for windows:

* `IsInSafePosition` - gets whether the specified window is completely contained within the closest working area
* `SafeguardPosition` - moves the specified window the minimum amount to be completely contained within the closest working area

Also includes behaviors:

* `ComboBoxDataVirtualization` & `ListBoxDataVirtualization` - sets the items source of a combo box or list box (including list views), respectively, to a collection that loads elements as they are needed for display and keeps selected elements loaded (requires .NET Core 3.1 or later)
* `DelayedFocus` - focuses an element after a specified delay
* `DeselectAllOnEmptySpaceClicked` - feselects all items when empty space in a list view is clicked
* `OpenNavigateUri` - opens the `Hyperlink`'s `NavigateUri` when it is clicked
* `PasswordBindingTarget` - allows binding to `PasswordBox.Password`

Also includes controls:

* `UrlAwareTextBlock` - provides a lightweight control for displaying small amounts of flow content which finds URLs and makes them clickable hyperlinks

Also includes input gestures:

* `MouseWheelDownGesture` - defines a mouse wheel down gesture that can be used to invoke a command
* `MouseWheelUpGesture` - defines a mouse wheel up gesture that can be used to invoke a command

Also includes validation rules:

* `InvalidCharactersValidationRule` - provides a way to create a rule in order to check that user input does not contain any invalid characters
* `StringNotEmptyValidationRule` - provides a way to create a rule in order to check that user input is not an empty string
* `ValidFileNameValidationRule` - provides a way to create a rule in order to check that user input does not contain any invalid file name characters
* `ValidPathValidationRule` - provides a way to create a rule in order to check that user input does not contain any invalid file system path characters

Also includes a wide array of value converters. Please see a package explorer for details.

# License
[Apache 2.0 License](LICENSE)

# Contributing
[Click here](CONTRIBUTING.md) to learn how to contribute.

# Acknowledgements
Makes use of the following excellent libraries:
* [AsyncEx](https://github.com/StephenCleary/AsyncEx) by Stephen Cleary
* [Ben.Demystifier](https://github.com/benaadams/Ben.Demystifier) by Ben Adams
* [PolySharp](https://github.com/Sergio0694/PolySharp) by Sergio Pedri