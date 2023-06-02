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
  - [ Collections](#-collections)
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

### Exceptions
This library provides extension methods for dealing with exceptions:

* `GetFullDetails` - creates a representation of an exception and all of its inner exceptions, including exception types, messages, and stack traces, and traversing multiple inner exceptions in the case of `AggregateException` and `ReflectionTypeLoadException`

---

## <img src="Epiforge.Extensions.Collections/NuGet.jpg" alt="Collections" style="float: left !important; padding-right: 0.5em !important;"> Collections
[![Epiforge.Extensions.Collections Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Collections.svg?logo=nuget) ![Downloads](https://img.shields.io/nuget/dt/epiforge.extensions.collections)](https://www.nuget.org/packages/Epiforge.Extensions.Collections)

This library provides a number of utilities surrounding collections:

* `EquatableList<T>` is an immutable list of items which may be compared with other instances of the same type and produces a hash code based on the permutation of its contents; and,
* `NullableKeyDictionary<TKey, TValue>` and `NullableKeySortedDictionary<TKey, TValue>` are very slim implementations of `IDictionary<TKey, TValue>` that allow a single null key (useful for some edge cases in which a null key is simply going to happen and you need to be able to deal with it; otherwise, use other dictionary classes);
* 
# License

[Apache 2.0 License](LICENSE)

# Contributing

[Click here](CONTRIBUTING.md) to learn how to contribute.

# Acknowledgements

Makes use of the following excellent libraries:
* [AsyncEx](https://github.com/StephenCleary/AsyncEx) by Stephen Cleary
* [Ben.Demystifier](https://github.com/benaadams/Ben.Demystifier) by Ben Adams
* [PolySharp](https://github.com/Sergio0694/PolySharp) by Sergio Pedri