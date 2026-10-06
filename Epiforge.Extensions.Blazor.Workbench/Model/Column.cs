namespace Epiforge.Extensions.Blazor.Workbench.Model;

public sealed class Column(string name) :
    INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs displayDataTypeChanged = new(nameof(DisplayDataType));

    TypeCode displayDataType = TypeCode.Int32;
    PropertyChangedEventHandler? propertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => propertyChanged += value;
        remove => propertyChanged -= value;
    }

    public TypeCode DisplayDataType
    {
        get => displayDataType;
        set
        {
            displayDataType = value;
            propertyChanged?.Invoke(this, displayDataTypeChanged);
        }
    }

    public string Name { get; } = name;

    public int Subscribers =>
        propertyChanged?.GetInvocationList().Length ?? 0;
}
