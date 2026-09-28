namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// An element every library is given alike, which announces each property it changes by name and can be made to change without announcing, to announce every property at once, or to hold the thread reading it
/// </summary>
public sealed class Person(string name, int rank) :
    INotifyPropertyChanged
{
    int rank = rank;
    int score = 1;

    public string Name { get; } = name;

    public int Rank
    {
        get => rank;
        set
        {
            if (rank == value)
                return;
            rank = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rank)));
        }
    }

    public int Score
    {
        get => score;
        set
        {
            if (score == value)
                return;
            score = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Score)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Announces that every property may have changed, naming none, as <see cref="PropertyChangedEventArgs"/> allows with <see langword="null"/> or <see cref="string.Empty"/>
    /// </summary>
    public void AnnounceEveryProperty(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public static ObservableCollection<Person> Many(params int[] ranks) =>
        [.. ranks.Select((rank, index) => new Person($"P{index}", rank))];

    public static string Names(IEnumerable<Person> people) =>
        string.Join(",", people.Select(person => person.Name));

    /// <summary>
    /// Changes <see cref="Rank"/> without announcing it
    /// </summary>
    public void SetRankSilently(int value) =>
        rank = value;

    public override string ToString() =>
        Name;
}
