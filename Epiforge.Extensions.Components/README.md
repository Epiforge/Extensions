# Property Change Notification
This library offers the `PropertyChangeNotifier` class, which you may inherit from to quickly get all the property utilities we're all tired of copying and pasting everywhere.
Just call the protected `OnPropertyChanged` and `OnPropertyChanging` methods at the appropriate times from setters and compiler services will figure out what property you're in.
Or, if all you need to do is set the value of a field, `SetBackedProperty` couldn't make it any easier or convenient to handle that as efficiently as possible.
`DynamicPropertyChangeNotifier` is also available if your class needs to be dynamic.

Be sure to set the protected `Logger` property if you want the abstract class to log what's going on with property change notification.

# Disposal
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

# Reflection
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

Use of these methods in .NET 7 or later will simply call the built-in methods, as they are now optimized.

This library also offers `FastComparer` and `FastEqualityComparer`, which implement `IComparer` and `IEqualityComaprer`, respectively, but quickly use the methods of `Comparer<>.Default` and `EqualityComaprer<>.Default`, respectively, to do their work.

In addition (pun intended), this library offers `GenericAddition`, `GenericSubtraction`, `GenericMultiplication`, and `GenericDivision`, which will produce delegates that will perform the respective operations with values of supplied generic type arguments.

# Exceptions
This library provides extension methods for dealing with exceptions:

* `GetFullDetails` - creates a representation of an exception and all of its inner exceptions, including exception types, messages, and stack traces, and traversing multiple inner exceptions in the case of `AggregateException` and `ReflectionTypeLoadException`

# Threading
This library provides classes for use in threading scenarios:

* `AsyncSynchronizationContext` - A SynchronizationContext that uses the Task Parallel Library (TPL) to process callbacks asynchronously