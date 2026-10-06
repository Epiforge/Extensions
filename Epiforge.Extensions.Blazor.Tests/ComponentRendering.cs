namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class ComponentRendering
{
    static ParameterView CellParameters(Person person, bool showAge = false) =>
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(NameCell.Person)] = person, [nameof(NameCell.ShowAge)] = showAge });

    [TestMethod]
    public async Task AChangeFromAnotherThreadIsRenderedOnTheDispatcher()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        var (cell, _) = await renderer.RenderAsync<NameCell>(CellParameters(person));
        await Task.Run(() => person.Name = "Grace");
        await renderer.SettleUntilAsync(() => cell.Renders > 1);
        Assert.AreEqual(2, cell.Renders);
        Assert.AreEqual("Grace", cell.LastName);
        Assert.IsFalse(cell.RenderedOffTheDispatcher);
    }

    [TestMethod]
    public async Task ALayoutObservesAndRendersItsBody()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        RenderFragment body = builder => builder.AddContent(0, "body");
        var (layout, _) = await renderer.RenderAsync<NameLayout>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(NameLayout.Person)] = person, [nameof(NameLayout.Body)] = body }));
        Assert.AreSame(body, layout.Body);
        person.Name = "Grace";
        await renderer.SettleAsync();
        Assert.AreEqual(2, layout.Renders);
    }

    [TestMethod]
    public async Task AnObservedChangeReRendersTheComponent()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        var (cell, _) = await renderer.RenderAsync<NameCell>(CellParameters(person));
        Assert.AreEqual(1, cell.Renders);
        person.Name = "Grace";
        await renderer.SettleAsync();
        Assert.AreEqual(2, cell.Renders);
        Assert.AreEqual("Grace", cell.LastName);
    }

    [TestMethod]
    public async Task AnUnobservedChangeDoesNotReRenderTheComponent()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        var (cell, _) = await renderer.RenderAsync<NameCell>(CellParameters(person));
        person.Age = 36;
        await renderer.SettleAsync();
        Assert.AreEqual(1, cell.Renders);
    }

    [TestMethod]
    public async Task ChangesFromAnotherThreadWhileTheDispatcherIsIdleRenderTheLastValueOnTheDispatcher()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        var (cell, _) = await renderer.RenderAsync<NameCell>(CellParameters(person));
        await Task.Run(() =>
        {
            for (var i = 0; i < 100; ++i)
                person.Name = $"Name {i}";
        });
        await renderer.SettleUntilAsync(() => cell.LastName == "Name 99");
        Assert.AreEqual("Name 99", cell.LastName);
        Assert.IsTrue(cell.Renders is > 1 and <= 101);
        Assert.IsFalse(cell.RenderedOffTheDispatcher);
    }

    [TestMethod]
    public async Task ChangesFromAnotherThreadWhileTheDispatcherIsBusyRenderOnce()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        var (cell, _) = await renderer.RenderAsync<NameCell>(CellParameters(person));
        using var entered = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        var blocker = Task.Run(() => renderer.Dispatcher.InvokeAsync(() =>
        {
            entered.Set();
            gate.Wait();
        }));
        entered.Wait();
        for (var i = 0; i < 100; ++i)
            person.Name = $"Name {i}";
        gate.Set();
        await blocker;
        await renderer.SettleUntilAsync(() => cell.Renders > 1);
        Assert.AreEqual(2, cell.Renders);
        Assert.AreEqual("Name 99", cell.LastName);
    }

    [TestMethod]
    public async Task ChangesWithinOneDispatcherWorkItemRenderOnce()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        var (cell, _) = await renderer.RenderAsync<NameCell>(CellParameters(person));
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            for (var i = 0; i < 100; ++i)
                person.Name = $"Name {i}";
        });
        await renderer.SettleAsync();
        Assert.AreEqual(2, cell.Renders);
        Assert.AreEqual("Name 99", cell.LastName);
    }

    static async Task<(TestRenderer renderer, CellHost host, List<Person> people)> RenderHostAsync(int count, bool firstDeclines = false)
    {
        var renderer = new TestRenderer();
        var people = Enumerable.Range(0, count).Select(i => new Person { Name = $"Person {i}" }).ToList();
        var (host, _) = await renderer.RenderAsync<CellHost>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(CellHost.People)] = people, [nameof(CellHost.Decliner)] = firstDeclines ? people[0] : null }));
        Assert.AreEqual(count, host.Cells.Count);
        return (renderer, host, people);
    }

    [TestMethod]
    public async Task ComponentsChangedFromAnotherThreadWhileTheDispatcherIsBusyRenderInOneBatch()
    {
        var (renderer, host, people) = await RenderHostAsync(50);
        using (renderer)
        {
            var batches = renderer.Batches;
            using var entered = new ManualResetEventSlim();
            using var gate = new ManualResetEventSlim();
            var blocker = Task.Run(() => renderer.Dispatcher.InvokeAsync(() =>
            {
                entered.Set();
                gate.Wait();
            }));
            entered.Wait();
            foreach (var person in people)
                person.Name += " changed";
            gate.Set();
            await blocker;
            await renderer.SettleUntilAsync(() => renderer.Batches > batches);
            Assert.AreEqual(batches + 1, renderer.Batches);
            foreach (var cell in host.Cells)
                Assert.AreEqual(2, cell.Renders);
        }
    }

    [TestMethod]
    public async Task ComponentsChangedInOneDispatcherWorkItemRenderInOneBatch()
    {
        var (renderer, host, people) = await RenderHostAsync(50);
        using (renderer)
        {
            var batches = renderer.Batches;
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                foreach (var person in people)
                    person.Name += " changed";
            });
            await renderer.SettleAsync();
            Assert.AreEqual(batches + 1, renderer.Batches);
            for (var i = 0; i < people.Count; ++i)
            {
                Assert.AreEqual(2, host.Cells[i].Renders);
                Assert.AreEqual(people[i].Name, host.Cells[i].LastName);
            }
        }
    }

    [TestMethod]
    public async Task ComponentsChangedTogetherShareABatchWhenTheFirstDeclinesToRender()
    {
        var (renderer, host, people) = await RenderHostAsync(10, true);
        using (renderer)
        {
            var batches = renderer.Batches;
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                foreach (var person in people)
                    person.Name += " changed";
            });
            await renderer.SettleAsync();
            Assert.AreEqual(batches + 1, renderer.Batches);
            Assert.AreEqual(1, host.Cells[0].Renders);
            foreach (var cell in host.Cells.Skip(1))
                Assert.AreEqual(2, cell.Renders);
        }
    }

    [TestMethod]
    public async Task APendingComponentRenderedByItsParentDoesNotRenderAgainForTheSameChange()
    {
        var (renderer, host, people) = await RenderHostAsync(3);
        using (renderer)
        {
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                people[1].Name = "Changed";
                host.Refresh();
            });
            await renderer.SettleAsync();
            Assert.AreEqual(2, host.Cells[1].Renders);
            Assert.AreEqual("Changed", host.Cells[1].LastName);
            people[1].Name = "Changed again";
            await renderer.SettleAsync();
            Assert.AreEqual(3, host.Cells[1].Renders);
            Assert.AreEqual("Changed again", host.Cells[1].LastName);
        }
    }

    [TestMethod]
    public async Task ConditionalMarkupReleasesWhatItNoLongerRenders()
    {
        using var renderer = new TestRenderer();
        var person = new Person { Name = "Ada", Age = 36 };
        var (cell, componentId) = await renderer.RenderAsync<NameCell>(CellParameters(person, true));
        await renderer.SetParametersAsync(componentId, CellParameters(person, false));
        Assert.AreEqual(2, cell.Renders);
        person.Age = 85;
        await renderer.SettleAsync();
        Assert.AreEqual(2, cell.Renders);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Name = "Grace";
        await renderer.SettleAsync();
        Assert.AreEqual(3, cell.Renders);
    }

    [TestMethod]
    public async Task DisposingTheRendererReleasesWhatTheComponentObserved()
    {
        var renderer = new TestRenderer();
        var person = new Person { Name = "Ada" };
        await renderer.RenderAsync<NameCell>(CellParameters(person));
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        await renderer.Dispatcher.InvokeAsync(renderer.Dispose);
        Assert.AreEqual(0, person.PropertyChangedSubscribers);
    }
}
