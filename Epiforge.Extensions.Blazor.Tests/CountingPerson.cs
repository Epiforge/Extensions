namespace Epiforge.Extensions.Blazor.Tests;

public class CountingPerson :
    Notifier
{
    int partnerReads;

    public Person? Partner
    {
        get
        {
            Interlocked.Increment(ref partnerReads);
            return field;
        }
        set => field = value;
    }

    public int PartnerReads =>
        Volatile.Read(ref partnerReads);

    public Person Throwing =>
        throw new InvalidOperationException("thrown by a getter");
}
