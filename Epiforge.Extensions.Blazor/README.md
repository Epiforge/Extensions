# Reactive Components
This library re-renders Blazor components when the objects they read announce changes through `INotifyPropertyChanged` or `INotifyCollectionChanged`.
A component inherits from `ReactiveComponentBase` (or a layout from `ReactiveLayoutComponentBase`) and reads what it shows through `Observed`:

```razor
@inherits ReactiveComponentBase

@foreach (var line in ObservedCollection(() => Order.Lines))
{
    <tr>
        <td>@Observed(() => line.Product.Name)</td>
        <td>@Observed(() => line.Quantity)</td>
    </tr>
}
<p>Total: @Observed(() => Order.Total)</p>

@code {
    [Parameter]
    public Order Order { get; set; } = default!;
}
```

`Observed` returns the value its expression reads and remembers what it read.
When any object along the way announces a change to what was read from it, the component renders again.
`ObservedCollection` does the same and also watches the value it returns for changes to its contents.
Nothing has to be registered with dependency injection for either.

The API is modeled on [Phork.Blazor.Reactivity](https://github.com/phorks/phork-blazor-reactivity), so that moving from it is a matter of a namespace; see [Coming From Phork.Blazor.Reactivity](#coming-from-phorkblazorreactivity).

# What Can Be Observed
An expression passed to `Observed` is a chain of members, properties or fields, read from a constant: in practice a local the lambda captures, a loop variable, a pattern variable, or the component itself, as in `() => line.Product.Name` or `() => Order.Total`.
The closures the C# compiler generates to capture those locals are resolved rather than observed, and so are anonymous objects, so what an observation is is the object the chain starts from and the members it reads.
The same expression evaluated in two renders, through two different closures, over the same object is one observation; the same expression over two objects is two.

Every object along the chain which implements `INotifyPropertyChanged` is subscribed for the member read from it.
A change announced for that member's name, or with a null or empty name, which by convention means every property, re-renders the component; a change to anything else does not.
Each object is read only after it has been subscribed, so a change which lands between the subscription and the read cannot be missed.

Some expressions are refused with an `ArgumentException`: a method call, an indexer, an operator, a conversion such as boxing, or a chain starting from a static member.
So is a chain with a null partway along it, after the objects before the null have been subscribed, so that the component hears when the missing object arrives.

# What Stays Observed
Only what the latest render used.
When a render ends, whatever the component observed and did not observe again during that render is released, which is what makes conditional markup safe: a branch no longer rendered stops being watched.
What a component observes outside a render, in `OnParametersSet` for instance, survives the end of the next render and is released at the end of the one after it unless it is observed again.
Disposing of the component releases everything.

# When Things Change
Objects may announce changes from any thread; components always render on their renderer's dispatcher.

A component told of several changes before it gets to render renders once.
Components of one renderer told of changes in the same turn of its dispatcher render together, in one batch, which on Blazor Server is one message to the browser and in a hybrid application one message to its web view, rather than one apiece.
When changes come from another thread while the dispatcher is idle, rendering waits one turn so that a burst of changes from that thread can be rendered together; this is a matter of timing on that thread and not a guarantee.
A component waiting to render for a change which renders anyway, because its parent rendered it, does not render a second time for that change.

# The Component Lifecycle
`ReactiveComponentBase` implements `IComponent`, `IHandleEvent` and `IHandleAfterRender` itself, with the lifecycle of `ComponentBase` on each version of .NET this library supports, rather than deriving from `ComponentBase`.
That is how it knows where each render begins and ends without reaching into the framework's internals: there is no reflection over private fields, no `Reflection.Emit` it cannot do without, and nothing to break when the framework changes how `ComponentBase` works inside.
Everything a component deriving from `ComponentBase` overrides or calls is there: `OnInitialized`, `OnParametersSet`, `OnAfterRender` and their asynchronous forms, `ShouldRender`, `StateHasChanged`, `InvokeAsync`, `SetParametersAsync`, `BuildRenderTree`, `DispatchExceptionAsync` from .NET 8, and `RendererInfo`, `Assets` and `AssignedRenderMode` from .NET 9.
Tests run the same scenarios against a component deriving from `ComponentBase` and one deriving from this class and require identical results, down to the order of lifecycle calls and the number of batches rendered.

**The cost is that these components are not `ComponentBase`.** Code which tests whether a component is a `ComponentBase`, or which only accepts one, will not recognize them.

A component which re-implements `IDisposable` with a `Dispose` method of its own must call the base class's, or it will keep observing after it has been removed; overriding the protected `Dispose(bool)` is the safer course.

A component which implements `IReactiveComponent` directly rather than inheriting from the base class can get an `IReactivityManager` from dependency injection after calling `AddEpiforgeBlazorReactivity` on its service collection; it must call the manager's `Initialize` with itself and the manager's `NotifyCycleEnded` at the end of each render.

# Bindings
`Binding(() => person.Name)` observes like `Observed` and returns an `IObservedBinding<T>` whose `Value` reads and writes the member at the end of the chain.
An overload takes a converter and a reverse converter, for binding to a value of a different type.
Writing a binding over a read-only member, or over an expression which reads no member at all, throws an `InvalidOperationException`.
`ConfigureBindings`, which a component may override, is called at the end of each render cycle, before what the cycle did not use is released.

# What It Costs
**Nothing is compiled per call and nothing is compiled per component.**
A chain of members is resolved into a path shared by every expression in the process which reads the same members, however many components and closures evaluate it, and each member is given one reader for the life of the process, generated where the runtime can generate code and made through reflection where it cannot.
What a component remembers about its observations is kept in small arrays which are searched rather than hashed until there are enough of them for hashing to pay.
Rendering a component again which observes what it observed last time allocates nothing in this library; where the runtime cannot generate code, reading a value of a value type through reflection boxes it, and that is all.

What a render does allocate is the expression tree the C# compiler builds for every call to `Observed`, a few hundred bytes each, which no library accepting an `Expression<Func<T>>` can avoid.

This library publishes under Native AOT without warnings of its own and works there, reading members through reflection rather than generating code; its tests also run with dynamic code disabled on .NET 8 and later.

# Measured Against Phork.Blazor.Reactivity
These are from the benchmarks in this repository, against Phork.Blazor.Reactivity 1.1.3, on one machine. Each renders a table of 1,000 cells, 100 rows of 10, whose every cell observes three values: its bound value, that value's text, and its column's display type. Alongside the two libraries, the same table is rendered by cells deriving from `ComponentBase` which read the same values without observing anything, which is what Blazor itself costs, and by cells which also build the three expression trees the reactive cells pass, which is what the C# compiler costs whatever library receives them.

| | This library | Phork.Blazor.Reactivity | `ComponentBase`, observing nothing | `ComponentBase`, building the trees |
|---|---|---|---|---|
| Re-rendering every cell, each observing what it observed last time | **603.1 μs**, **961.1 KB** | 191,914.2 μs, 21,853.2 KB | 175.8 μs, 0.1 KB | 549.3 μs, 1,039.2 KB |
| Every row entering the table and leaving it again | **1,333.5 μs**, **2,661.8 KB** | 507,745.0 μs, 47,015.1 KB | 682.3 μs, 818.0 KB | 1,028.3 μs, 1,779.0 KB |
| Every cell's value changing in one turn of the dispatcher, each change announcing two properties a cell observes | **897.4 μs**, **992.7 KB**, in 1 batch | 293,613.8 μs, 43,721.4 KB, in 2,000 batches | 184.4 μs, 31.4 KB, re-rendering the table once, in 1 batch | |

**Re-rendering costs about what building the expression trees costs.** This library re-renders the table in 1.10x the time the cells take merely to build the trees they pass it, and allocates nothing beyond them; Phork takes 318x this library's time and 22.7x its allocation, because it compiles a lambda for every call to `Observed` on every render.

**Rows entering and leaving cost this library 883 KB per thousand cells above the trees**, which is what it remembers about three observations per cell and the subscriptions behind them, created and then released. Phork compiles delegates for every member of every chain in every new component, and takes 381x this library's time and 17.7x its allocation.

**When every value changes at once, this library renders each cell once, and all of them in one batch.** Phork renders a cell for each change announced, and outside an event handler each of those renders is a batch of its own: 2,000 batches for 1,000 cells, taking 327x this library's time. In a hybrid application or on Blazor Server a batch is a message to the web view or the browser, which this measurement, made with a renderer that sends nothing, does not price.

# Coming From Phork.Blazor.Reactivity
Replace the `Phork.Blazor` namespace with `Epiforge.Extensions.Blazor`.
`AddPhorkBlazorReactivity()` becomes `AddEpiforgeBlazorReactivity()`, which is now needed only by components implementing `IReactiveComponent` directly.
`ReactiveComponentBase`, `ReactiveLayoutComponentBase`, `Observed`, `ObservedCollection`, both `Binding` overloads, `ConfigureBindings`, `IReactiveComponent`, `IReactivityManager` and `IObservedBinding<T>` keep their names and their meanings; `IObservedBinding<T>` moves out of a namespace of its own.

What behaves differently:

- **The components are not `ComponentBase`**, for the reason given in [The Component Lifecycle](#the-component-lifecycle).
- **A change announced with an empty property name re-renders**, as one with a null name always has; Phork ignores it.
- **Renders are coalesced.** Phork asks for a render for every change announced, and outside an event handler, where the renderer is not already gathering a batch, each request renders at once: an object announcing two properties a component reads renders it twice, and a thousand components changed together are rendered in a thousand batches. Here each component renders once per turn of the dispatcher and the components of one renderer share a batch. A render which Phork performed at once, inside the work which announced the change, now happens after that work finishes.
- **The reactivity manager is made by the component** rather than injected, and `IReactivityManager.Initialize` accepts any `IReactiveComponent` rather than only a `ComponentBase`. Phork attaches itself to the end of a `ComponentBase`'s render by replacing the framework's private render fragment through reflection; a component implementing `IReactiveComponent` here calls `NotifyCycleEnded` itself.
- **Each member along a chain is read once per call.** Phork reads the objects along the chain to subscribe to them and then reads the whole chain again for the value.
- **An exception thrown by a member read along a chain arrives as thrown**, where Phork wrapped one thrown before the last member in a `TargetInvocationException`.

# Acknowledgements
The API and the meaning of an observation are modeled on [Phork.Blazor.Reactivity](https://github.com/phorks/phork-blazor-reactivity) by Phorks, which is licensed under the MIT License. No code from it is used here.

`ReactiveComponentBase` reproduces the lifecycle of `ComponentBase` from [ASP.NET Core](https://github.com/dotnet/aspnetcore), which carries the following notice:

> The MIT License (MIT)
>
> Copyright (c) .NET Foundation and Contributors
>
> All rights reserved.
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
