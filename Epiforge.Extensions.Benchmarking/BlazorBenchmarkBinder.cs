namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// A value bound to a table cell, which announces its text whenever it announces its value, with arguments made once so that a change allocates nothing of its own
/// </summary>
sealed class BlazorBenchmarkBinder :
    INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs textValueChanged = new(nameof(TextValue));
    static readonly PropertyChangedEventArgs valueChanged = new(nameof(Value));

    int value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TextValue { get; private set; } = "0";

    public int Value
    {
        get => value;
        set
        {
            this.value = value;
            TextValue = value.ToString(CultureInfo.InvariantCulture);
            PropertyChanged?.Invoke(this, valueChanged);
            PropertyChanged?.Invoke(this, textValueChanged);
        }
    }
}
