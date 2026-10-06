namespace Epiforge.Extensions.Blazor.Workbench.Components.Pages;

public partial class Verify
{
    sealed record Result(string Name, bool Passed, string Detail);

    const int columns = 4;
    const int rows = 25;

    bool complete;
    TableModel epiforgeModel = new(rows, columns);
    int epiforgeRows;
    bool epiforgeShowType = true;
    TableModel phorkModel = new(rows, columns);
    int phorkRows;
    readonly List<Result> results = [];
    bool running;

    static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds > timeoutMilliseconds)
                return false;
            await Task.Delay(10);
        }
        return true;
    }

    int Renders(Binder binder) =>
        Ledger.RendersOf(binder, Implementations.Epiforge);

    async Task RunAsync()
    {
        running = true;
        complete = false;
        results.Clear();
        epiforgeModel = new(rows, columns);
        phorkModel = new(rows, columns);
        epiforgeRows = rows;
        epiforgeShowType = true;
        phorkRows = 0;
        StateHasChanged();
        await Task.Yield();
        await ScenarioAsync("Every cell renders once on entering the table", async () =>
        {
            var rendered = await WaitUntilAsync(() => epiforgeModel.Binders.All(binder => Renders(binder) == 1));
            return (rendered && epiforgeModel.Binders.All(binder => binder.Subscribers == 1), $"renders {string.Join(",", epiforgeModel.Binders.Select(Renders).Distinct())}; subscribers {string.Join(",", epiforgeModel.Binders.Select(binder => binder.Subscribers).Distinct())}");
        });
        await ScenarioAsync("An observed change re-renders its cell once", async () =>
        {
            var binder = epiforgeModel.Rows[0][0];
            binder.Value += 1000;
            var rendered = await WaitUntilAsync(() => Renders(binder) == 2);
            await Task.Delay(100);
            return (rendered && Renders(binder) == 2 && epiforgeModel.Rows[0][1] is var neighbor && Renders(neighbor) == 1, $"renders {Renders(binder)}, neighbor {Renders(epiforgeModel.Rows[0][1])}");
        });
        await ScenarioAsync("An unobserved change re-renders nothing", async () =>
        {
            var binder = epiforgeModel.Rows[1][0];
            var before = Renders(binder);
            binder.Unrelated += 1;
            await Task.Delay(200);
            return (Renders(binder) == before, $"renders {before} then {Renders(binder)}");
        });
        await ScenarioAsync("A change from another thread renders on the circuit", async () =>
        {
            var binder = epiforgeModel.Rows[2][0];
            var before = Renders(binder);
            var offBefore = Ledger.RendersOffTheCircuit;
            await Task.Run(() => binder.Value += 1000);
            var rendered = await WaitUntilAsync(() => Renders(binder) > before);
            await Task.Delay(100);
            return (rendered && Ledger.RendersOffTheCircuit == offBefore, $"renders {before} then {Renders(binder)} for a change announcing two properties; renders off the circuit {Ledger.RendersOffTheCircuit - offBefore}");
        });
        await ScenarioAsync("Every cell changed at once renders once more", async () =>
        {
            var before = epiforgeModel.Binders.ToDictionary(binder => binder, Renders);
            epiforgeModel.ChangeEveryValue();
            var rendered = await WaitUntilAsync(() => epiforgeModel.Binders.All(binder => Renders(binder) >= before[binder] + 1));
            await Task.Delay(100);
            var extra = epiforgeModel.Binders.Count(binder => Renders(binder) != before[binder] + 1);
            return (rendered && extra == 0, $"{extra} cells rendered other than once");
        });
        await ScenarioAsync("Hiding a column's markup releases that column", async () =>
        {
            epiforgeShowType = false;
            StateHasChanged();
            var released = await WaitUntilAsync(() => epiforgeModel.Columns.All(column => column.Subscribers == 0));
            var before = epiforgeModel.Binders.ToDictionary(binder => binder, Renders);
            epiforgeModel.Columns[0].DisplayDataType = TypeCode.String;
            await Task.Delay(200);
            var reRendered = epiforgeModel.Binders.Count(binder => Renders(binder) != before[binder]);
            return (released && reRendered == 0, $"column subscribers {string.Join(",", epiforgeModel.Columns.Select(column => column.Subscribers))}; cells re-rendered by the hidden column {reRendered}");
        });
        await ScenarioAsync("Showing it again observes it again", async () =>
        {
            epiforgeShowType = true;
            StateHasChanged();
            var observed = await WaitUntilAsync(() => epiforgeModel.Columns.All(column => column.Subscribers == rows));
            var cell = epiforgeModel.Rows[0][0];
            var before = Renders(cell);
            epiforgeModel.Columns[0].DisplayDataType = TypeCode.Int32;
            var rendered = await WaitUntilAsync(() => Renders(cell) == before + 1);
            return (observed && rendered, $"column subscribers {string.Join(",", epiforgeModel.Columns.Select(column => column.Subscribers))}");
        });
        await ScenarioAsync("Rows leaving the table release what their cells observed", async () =>
        {
            epiforgeRows = 0;
            StateHasChanged();
            var released = await WaitUntilAsync(() => epiforgeModel.Binders.All(binder => binder.Subscribers == 0) && epiforgeModel.Columns.All(column => column.Subscribers == 0));
            return (released, $"binder subscribers {epiforgeModel.Binders.Sum(binder => binder.Subscribers)}; column subscribers {epiforgeModel.Columns.Sum(column => column.Subscribers)}");
        });
        await ScenarioAsync("Rows re-entering the table observe again", async () =>
        {
            epiforgeRows = rows;
            StateHasChanged();
            var observed = await WaitUntilAsync(() => epiforgeModel.Binders.All(binder => binder.Subscribers == 1));
            var binder = epiforgeModel.Rows[3][3];
            var before = Renders(binder);
            binder.Value += 1;
            var rendered = await WaitUntilAsync(() => Renders(binder) == before + 1);
            return (observed && rendered, $"binder subscribers {string.Join(",", epiforgeModel.Binders.Select(binder => binder.Subscribers).Distinct())}");
        });
        await ScenarioAsync("The layout observes what it reads", async () =>
        {
            var before = Ledger.RendersOf(State, Implementations.Epiforge);
            State.Title = "Workbench (verifying)";
            var rendered = await WaitUntilAsync(() => Ledger.RendersOf(State, Implementations.Epiforge) == before + 1);
            State.Title = "Workbench";
            await WaitUntilAsync(() => Ledger.RendersOf(State, Implementations.Epiforge) == before + 2);
            return (rendered, $"layout renders {before} then {Ledger.RendersOf(State, Implementations.Epiforge)}");
        });
        await ScenarioAsync("Changing every value renders no more cells than Phork does", async () =>
        {
            phorkRows = rows;
            StateHasChanged();
            await WaitUntilAsync(() => phorkModel.Binders.All(binder => Ledger.RendersOf(binder, Implementations.Phork) == 1));
            var phorkBefore = Ledger.Total(Implementations.Phork);
            var epiforgeBefore = Ledger.Total(Implementations.Epiforge);
            phorkModel.ChangeEveryValue();
            epiforgeModel.ChangeEveryValue();
            await Task.Delay(500);
            var phork = Ledger.Total(Implementations.Phork) - phorkBefore;
            var epiforge = Ledger.Total(Implementations.Epiforge) - epiforgeBefore;
            phorkRows = 0;
            StateHasChanged();
            return (epiforge == rows * columns && epiforge <= phork, $"Epiforge rendered cells {epiforge} times and Phork {phork} times for {rows * columns} cells each announcing two changes");
        });
        complete = true;
        running = false;
    }

    async Task ScenarioAsync(string name, Func<Task<(bool passed, string detail)>> scenario)
    {
        try
        {
            var (passed, detail) = await scenario();
            results.Add(new(name, passed, detail));
        }
        catch (Exception exception)
        {
            results.Add(new(name, false, $"{exception.GetType().Name}: {exception.Message}"));
        }
        StateHasChanged();
        await Task.Yield();
    }
}
