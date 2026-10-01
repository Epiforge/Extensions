namespace Epiforge.Extensions.AotProbe;

/// <summary>
/// A subject whose properties announce their changes
/// </summary>
public sealed class ProbePerson :
    PropertyChangeNotifier
{
    ProbePerson? manager;
    string name = string.Empty;
    int rank;
    int score;
    TimeSpan span;

    /// <summary>
    /// Gets or sets the person's manager
    /// </summary>
    public ProbePerson? Manager
    {
        get => manager;
        set => SetBackedProperty(ref manager, in value);
    }

    /// <summary>
    /// Gets or sets the person's name
    /// </summary>
    public string Name
    {
        get => name;
        set => SetBackedProperty(ref name, in value);
    }

    /// <summary>
    /// Gets or sets the person's rank
    /// </summary>
    public int Rank
    {
        get => rank;
        set => SetBackedProperty(ref rank, in value);
    }

    /// <summary>
    /// Gets or sets the person's score
    /// </summary>
    public int Score
    {
        get => score;
        set => SetBackedProperty(ref score, in value);
    }

    /// <summary>
    /// Gets or sets a span of time
    /// </summary>
    public TimeSpan Span
    {
        get => span;
        set => SetBackedProperty(ref span, in value);
    }
}
