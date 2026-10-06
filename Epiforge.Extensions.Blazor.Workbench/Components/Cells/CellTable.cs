namespace Epiforge.Extensions.Blazor.Workbench.Components.Cells;

/// <summary>
/// Renders the rows of a table model as cells of one component type, and renders again only when its own parameters change, so that what re-renders a cell is what the cell observes rather than its page
/// </summary>
public sealed class CellTable<TCell> :
    ComponentBase
    where TCell : IComponent
{
    TableModel? renderedModel;
    int renderedRows = -1;
    bool renderedShowType;

    [Parameter]
    public TableModel? Model { get; set; }

    [Parameter]
    public int Rows { get; set; }

    [Parameter]
    public bool ShowType { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedModel = Model;
        renderedRows = Rows;
        renderedShowType = ShowType;
        if (Model is not { } model)
            return;
        builder.OpenElement(0, "table");
        builder.AddAttribute(1, "class", "cells");
        builder.OpenElement(2, "tbody");
        for (int row = 0, rows = Math.Min(Rows, model.Rows.Length); row < rows; ++row)
        {
            var binders = model.Rows[row];
            builder.OpenElement(3, "tr");
            builder.SetKey(binders);
            for (var column = 0; column < binders.Length; ++column)
            {
                builder.OpenComponent<TCell>(4);
                builder.AddAttribute(5, "Binder", binders[column]);
                builder.AddAttribute(6, "Column", model.Columns[column]);
                builder.AddAttribute(7, "ShowType", ShowType);
                builder.CloseComponent();
            }
            builder.CloseElement();
        }
        builder.CloseElement();
        builder.CloseElement();
    }

    protected override bool ShouldRender() =>
        !ReferenceEquals(Model, renderedModel) || Rows != renderedRows || ShowType != renderedShowType;
}
