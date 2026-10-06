namespace Epiforge.Extensions.Blazor.Workbench.Model;

public sealed class TableModel
{
    public TableModel(int rows, int columns)
    {
        Columns = Enumerable.Range(0, columns).Select(column => new Column($"Column {column + 1}")).ToArray();
        Rows = Enumerable.Range(0, rows).Select(row => Enumerable.Range(0, columns).Select(column => new Binder { Value = row * columns + column }).ToArray()).ToArray();
    }

    public IEnumerable<Binder> Binders =>
        Rows.SelectMany(row => row);

    public Column[] Columns { get; }

    public Binder[][] Rows { get; }

    public void ChangeEveryValue()
    {
        foreach (var binder in Binders)
            ++binder.Value;
    }
}
