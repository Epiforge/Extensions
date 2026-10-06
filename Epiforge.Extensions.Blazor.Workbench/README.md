# Epiforge.Extensions.Blazor Workbench

A Blazor Web App, running interactively on the server, for watching `Epiforge.Extensions.Blazor` work in a real renderer and checking that it does. It is not a benchmark; the classes named `Blazor*Benchmarks` in `Epiforge.Extensions.Benchmarking` are where costs are measured.

## Running it

```
dotnet run --project Epiforge.Extensions.Blazor.Workbench
```

Then open `http://localhost:5187`.

## Pages

**Grid** renders a table of cells shaped like a data-entry grid's: every cell observes its bound value, that value's text and its column's display type. The same table can be rendered by cells built on this library, on Phork.Blazor.Reactivity 1.1.3, or on `ComponentBase` with no observation at all. Buttons show and hide the rows, change every value on the circuit or from another thread, hide and show the column types, and rename the layout, which observes its title. After each operation the page reports how many cells rendered and over how long.

**Verify** runs scenarios against real components on the circuit and reports a verdict for each: a cell renders once on entering, an observed change re-renders only its own cell, an unobserved change re-renders nothing, a change from another thread renders on the circuit, every cell changed at once renders once more, hidden markup releases what it observed and shown markup observes it again, rows leaving the table release everything and rows re-entering observe again, the layout observes what it reads, and changing every value renders no more cells than Phork does.

## Verifying from a script

`verify.py` builds and runs the workbench, drives the Verify page in headless Chromium through Playwright, and writes the verdicts to a file:

```
python3 Epiforge.Extensions.Blazor.Workbench/verify.py [results-file]
```

The results file defaults to `TestResults/workbench-verification.json` at the root of the repository, with the workbench's own output beside it as `workbench-verification.log`. The script exits with a non-zero status when a scenario fails or the page cannot be driven. It needs Python 3 with the `playwright` package and its Chromium installed.
