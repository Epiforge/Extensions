namespace Epiforge.Extensions.Collections;

static class CommonPropertyChangeNotificationEventArgs
{
    public static readonly PropertyChangedEventArgs CountChanged = new("Count");
    public static readonly PropertyChangingEventArgs CountChanging = new("Count");
}
