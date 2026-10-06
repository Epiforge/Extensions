namespace Epiforge.Extensions.Blazor.Tests;

public sealed class CellHost :
    ComponentBase
{
    public List<NameCell> Cells { get; } = [];

    [Parameter]
    public Person? Decliner { get; set; }

    [Parameter]
    public IReadOnlyList<Person> People { get; set; } = [];

    public void Refresh() =>
        StateHasChanged();

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        foreach (var person in People)
        {
            builder.OpenComponent<NameCell>(0);
            builder.AddAttribute(1, nameof(NameCell.Person), person);
            builder.AddAttribute(2, nameof(NameCell.DeclinesToRerender), ReferenceEquals(person, Decliner));
            builder.AddComponentReferenceCapture(3, cell => Cells.Add((NameCell)cell));
            builder.CloseComponent();
        }
    }
}
