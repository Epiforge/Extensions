# Blazor reactivity against Phork

*2026-10-06: the first record of Epiforge.Extensions.Blazor, re-rendering, churning and fanning out a table of 1,000 observing cells against Phork.Blazor.Reactivity 1.1.3*

Run on 6 October 2026 by `BlazorRerenderBenchmarks`, `BlazorRowChurnBenchmarks` and `BlazorFanOutBenchmarks`, the first record of `Epiforge.Extensions.Blazor` 1.0.0 (unreleased), against Phork.Blazor.Reactivity 1.1.3. Intel Core Ultra 9 275HX, .NET 10.0.12, job `current` (three launches of ten 250 ms iterations after four warmups).

## The instruments

Every class renders a grid of 1,000 cells, 100 rows of 10, through a renderer that sends nothing to a display and counts batches. Each cell reads three values, as the consumer's table cells do: `binder.Value`, `binder.TextValue` and `column.DisplayDataType`. The binder announces `Value` and then `TextValue` whenever its value is set, with arguments made once.

- **Plain** is a `ComponentBase` cell reading the values directly: what Blazor costs.
- **Trees** is a `ComponentBase` cell which also builds the three expression trees the reactive cells pass, through a non-inlined sink: what the C# compiler costs, whatever library receives the trees.
- **Epiforge** and **Phork** are the same cell over each library's `ReactiveComponentBase`.

Each setup runs its operation once and throws unless every cell rendered the expected number of times; the fan-out setup also requires 1 batch for Plain and Epiforge and 2,000 for Phork.

## Re-rendering every cell, each observing what it observed last time

| Method | Mean | StdDev | Allocated |
|---|---:|---:|---:|
| Plain | 175.8 μs | 20.09 μs | 136 B |
| Trees | 549.3 μs | 59.94 μs | 1,064,141 B |
| Epiforge | 603.1 μs | 103.75 μs | 984,136 B |
| Phork | 191,914.2 μs | 36,167.80 μs | 22,377,688 B |

## Every row entering the table and leaving it again

| Method | Mean | StdDev | Allocated |
|---|---:|---:|---:|
| Plain | 682.3 μs | 21.47 μs | 818.01 KB |
| Trees | 1,028.3 μs | 87.45 μs | 1,778.95 KB |
| Epiforge | 1,333.5 μs | 90.86 μs | 2,661.76 KB |
| Phork | 507,745.0 μs | 32,694.86 μs | 47,015.13 KB |

## Every cell's value changing in one dispatcher work item

| Method | Mean | StdDev | Allocated | Batches |
|---|---:|---:|---:|---:|
| Plain (re-rendering the table once) | 184.4 μs | 3.50 μs | 31.38 KB | 1 |
| Epiforge | 897.4 μs | 76.87 μs | 992.74 KB | 1 |
| Phork | 293,613.8 μs | 14,379.10 μs | 43,721.40 KB | 2,000 |

## Against the predictions

Predicted before the run, from sandbox exploration:

- **Re-render, Epiforge's allocation equal to Trees' to the byte: wrong.** Epiforge allocated 984,136 B and Trees 1,064,141 B, 80.0 bytes per cell less, which the two arms were built not to differ by. The library's own allocation on this path is zero by the test `ObservingTheSameChainAgainAllocatesNothing`, which passed on every framework in the same session, so the difference lies in the cells, not the library; why the Trees cell allocates 80 bytes more per render than the Epiforge cell has not been found. A guess, not a finding: .NET 10's escape analysis stack-allocating an object in one cell's render and not the other's. Until it is explained, the Trees arm is a ceiling on what the compiler's trees cost in these cells, not an exact figure, and nothing here is quoted as "equal".
- **Re-render, Epiforge within 1.5x of Trees' time: held**, at 1.10x.
- **Re-render, Phork at least 50x Epiforge's time and 20x its allocation: held**, at 318x and 22.7x.
- **Churn, Epiforge minus Trees about 875 KB (±5%): held**, at 882.8 KB.
- **Churn, Phork at least 100x Epiforge's time: held**, at 381x; allocation 17.7x.
- **Fan-out, Phork at least 100x Epiforge's time: held**, at 327x; allocation 44.0x.
- **Fan-out, Epiforge about 1.05 MB per operation: 992.74 KB**, 0.95x the prediction, which named no tolerance.

## What the figures say

Re-rendering costs this library about what building the expression trees costs; the trees, not the library, are the remaining price of an `Observed` call. A row entering and leaving costs this library about 883 bytes per cell above the trees, for three observations and the subscriptions behind them. Phork compiles a lambda per `Observed` call per render and delegates per member per new component, which is where its 300x to 400x goes.

The fan-out arms do not price what a batch costs on a real display: this renderer sends nothing. On Blazor Server and in a hybrid application each batch is a message across a boundary, so the 2,000 against 1 matters more there than these times show.
