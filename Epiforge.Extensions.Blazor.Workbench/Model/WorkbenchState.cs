namespace Epiforge.Extensions.Blazor.Workbench.Model;

public sealed class WorkbenchState :
    INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs titleChanged = new(nameof(Title));

    string title = "Workbench";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title
    {
        get => title;
        set
        {
            title = value;
            PropertyChanged?.Invoke(this, titleChanged);
        }
    }
}
