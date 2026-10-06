namespace Epiforge.Extensions.Blazor.Tests;

public class RacingPerson :
    Notifier
{
    public Action? Reading { get; set; }

    public string Racy
    {
        get
        {
            Reading?.Invoke();
            return "racy";
        }
    }
}
