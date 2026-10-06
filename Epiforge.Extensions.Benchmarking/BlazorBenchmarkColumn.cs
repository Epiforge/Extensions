namespace Epiforge.Extensions.Benchmarking;

sealed class BlazorBenchmarkColumn :
    INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    public TypeCode DisplayDataType { get; set; } = TypeCode.Int32;
}
