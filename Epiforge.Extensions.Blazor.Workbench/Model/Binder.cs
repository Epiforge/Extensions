namespace Epiforge.Extensions.Blazor.Workbench.Model;

/// <summary>
/// A value bound to a table cell, which announces its text whenever it announces its value, as a formatted value does
/// </summary>
public sealed class Binder :
    INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs textValueChanged = new(nameof(TextValue));
    static readonly PropertyChangedEventArgs unrelatedChanged = new(nameof(Unrelated));
    static readonly PropertyChangedEventArgs valueChanged = new(nameof(Value));

    PropertyChangedEventHandler? propertyChanged;
    int unrelated;
    int value;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => propertyChanged += value;
        remove => propertyChanged -= value;
    }

    public int Subscribers =>
        propertyChanged?.GetInvocationList().Length ?? 0;

    public string TextValue { get; private set; } = "0";

    public int Unrelated
    {
        get => unrelated;
        set
        {
            unrelated = value;
            propertyChanged?.Invoke(this, unrelatedChanged);
        }
    }

    public int Value
    {
        get => value;
        set
        {
            this.value = value;
            TextValue = value.ToString("N0");
            propertyChanged?.Invoke(this, valueChanged);
            propertyChanged?.Invoke(this, textValueChanged);
        }
    }
}
