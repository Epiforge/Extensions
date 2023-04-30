![Extensions Logo](Extensions.jpg) 

<h1>Extensions</h1>

General utilities to help with stuff in .NET Development, from Epiforge.

Supports `netstandard2.0`.

![Azure Pipelines](https://dev.azure.com/epiforge/extensions/_apis/build/status/epiforge.extensions)
![Build](https://img.shields.io/azure-devops/build/epiforge/extensions/4.svg?logo=microsoft&logoColor=white)
![Tests](https://img.shields.io/azure-devops/tests/epiforge/extensions/4.svg?compact_message=&logo=microsoft&logoColor=white)

- [Libraries](#libraries)
  - [Components](#components)
    - [Property Change Notification](#property-change-notification)
    - [Disposal](#disposal)
- [License](#license)
- [Contributing](#contributing)
- [Acknowledgements](#acknowledgements)

# Libraries

## Components

[![Epiforge.Extensions.Components Nuget](https://img.shields.io/nuget/v/Epiforge.Extensions.Components.svg)](https://www.nuget.org/packages/Epiforge.Extensions.Components)

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

# License

[Apache 2.0 License](LICENSE)

# Contributing

[Click here](CONTRIBUTING.md) to learn how to contribute.

# Acknowledgements

Makes use of the glorious [AsyncEx](https://github.com/StephenCleary/AsyncEx) library by Stephen Cleary and the [Poly#](https://github.com/Sergio0694/PolySharp/) NuGet package by Sergio Pedri because it's just great.