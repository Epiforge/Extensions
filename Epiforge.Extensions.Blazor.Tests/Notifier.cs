namespace Epiforge.Extensions.Blazor.Tests;

public class Notifier :
    INotifyPropertyChanged
{
    PropertyChangedEventHandler? propertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => propertyChanged += value;
        remove => propertyChanged -= value;
    }

    public int PropertyChangedSubscribers =>
        propertyChanged?.GetInvocationList().Length ?? 0;

    public void Raise(string? propertyName) =>
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Raise(propertyName);
    }
}
