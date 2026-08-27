namespace Epiforge.Extensions.Platforms.Windows;

/// <summary>
/// Represents the current Windows theme
/// </summary>
public sealed class Theme :
    SyncDisposable
{
    /// <summary>
    /// Instantiates a new instance of the <see cref="Theme"/> class
    /// </summary>
    public Theme()
    {
        synchronizationContext = SynchronizationContext.Current;
        try
        {
            colorKey = Registry.Users.OpenSubKey(colorKeyName) ?? throw new PlatformNotSupportedException($"The DWM key (\"{colorKeyName}\") could not be found");
            color = FetchColor();
            var colorWatching = WatchRegistryValue(colorKeyName, colorValueName, ColorKeyWatcherEventArrived, ColorKeyPollTimerTick);
            colorKeyWatcher = colorWatching.watcher;
            colorKeyPollTimer = colorWatching.pollTimer;
            isDarkKey = Registry.Users.OpenSubKey(isDarkKeyName) ?? throw new PlatformNotSupportedException($"The Personalize key (\"{isDarkKeyName}\") could not be found");
            isDark = FetchIsDark();
            var isDarkWatching = WatchRegistryValue(isDarkKeyName, isDarkValueName, IsDarkKeyWatcherEventArrived, IsDarkKeyPollTimerTick);
            isDarkKeyWatcher = isDarkWatching.watcher;
            isDarkKeyPollTimer = isDarkWatching.pollTimer;
        }
        catch
        {
            ReleaseResources();
            throw;
        }
    }

    Color color;
    readonly RegistryKey colorKey;
    readonly string colorKeyName = $@"{WindowsIdentity.GetCurrent().User}\Software\Microsoft\Windows\DWM";
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed", Justification = "This field will be disposed by the base class, the analyzer just doesn't see that.")]
    readonly ManagementEventWatcher? colorKeyWatcher;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed", Justification = "This field will be disposed by the base class, the analyzer just doesn't see that.")]
    readonly Timer? colorKeyPollTimer;
    readonly string colorValueName = "ColorizationColor";
    readonly int defaultColorValue = unchecked((int)0xc42947cc);
    readonly int defaultIsDarkValue = 1;
    bool isDark;
    readonly RegistryKey isDarkKey;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed", Justification = "This field will be disposed by the base class, the analyzer just doesn't see that.")]
    readonly Timer? isDarkKeyPollTimer;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed", Justification = "This field will be disposed by the base class, the analyzer just doesn't see that.")]
    readonly ManagementEventWatcher? isDarkKeyWatcher;
    readonly string isDarkKeyName = $@"{WindowsIdentity.GetCurrent().User}\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    readonly string isDarkValueName = "AppsUseLightTheme";
    readonly SynchronizationContext? synchronizationContext;

    /// <summary>
    /// Gets the accent color of the current Windows theme
    /// </summary>
    public Color Color
    {
        get => color;
        private set => SetBackedProperty(ref color, in value);
    }

    /// <summary>
    /// Gets whether the current Windows theme is dark
    /// </summary>
    public bool IsDark
    {
        get => isDark;
        private set => SetBackedProperty(ref isDark, in value);
    }

    void ColorKeyPollTimerTick(object? state) =>
        UsingContext(() =>
        {
            try
            {
                Color = FetchColor();
            }
            catch (ObjectDisposedException)
            {
                // do nothing
            }
        });

    void ColorKeyWatcherEventArrived(object? sender, EventArrivedEventArgs e) =>
        UsingContext(() =>
        {
            try
            {
                Color = FetchColor();
            }
            catch (ObjectDisposedException)
            {
                // do nothing
            }
        });

    /// <summary>
    /// Frees, releases, or resets unmanaged resources
    /// </summary>
    /// <param name="disposing"><c>false</c> if invoked by the finalizer because the object is being garbage collected; otherwise, <c>true</c></param>
    /// <returns><c>true</c> if disposal completed; otherwise, <c>false</c></returns>
    protected override bool Dispose(bool disposing)
    {
        if (disposing)
            ReleaseResources();
        return true;
    }

    Color FetchColor() =>
        Color.FromArgb((int)(colorKey?.GetValue(colorValueName) ?? defaultColorValue));

    bool FetchIsDark() =>
        (int)(isDarkKey?.GetValue(isDarkValueName) ?? defaultIsDarkValue) == 0;

    void IsDarkKeyPollTimerTick(object? state) =>
        UsingContext(() =>
        {
            try
            {
                IsDark = FetchIsDark();
            }
            catch (ObjectDisposedException)
            {
                // do nothing
            }
        });

    void IsDarkKeyWatcherEventArrived(object sender, EventArrivedEventArgs e) =>
        UsingContext(() =>
        {
            try
            {
                IsDark = FetchIsDark();
            }
            catch (ObjectDisposedException)
            {
                // do nothing
            }
        });

    void ReleaseResources()
    {
        colorKeyPollTimer?.Dispose();
        isDarkKeyPollTimer?.Dispose();
        colorKeyWatcher?.Dispose();
        isDarkKeyWatcher?.Dispose();
        colorKey?.Dispose();
        isDarkKey?.Dispose();
    }

    void UsingContext(Action action)
    {
        if (synchronizationContext != null)
            synchronizationContext.Post(state => action(), null);
        else
            action();
    }

    static readonly TimeSpan pollingInterval = TimeSpan.FromSeconds(5);

    static string Sanitize(string value) =>
        value.Replace(@"\", @"\\", StringComparison.OrdinalIgnoreCase).Replace("'", @"\'", StringComparison.OrdinalIgnoreCase);

    static (ManagementEventWatcher? watcher, Timer? pollTimer) WatchRegistryValue(string keyName, string valueName, EventArrivedEventHandler eventArrived, TimerCallback pollTimerTick)
    {
        ManagementEventWatcher? watcher = null;
        try
        {
            watcher = new ManagementEventWatcher(new WqlEventQuery("RegistryValueChangeEvent") { Condition = $"Hive = '{Sanitize(Registry.Users.Name)}' AND KeyPath = '{Sanitize(keyName)}' AND ValueName = '{Sanitize(valueName)}'" });
            watcher.EventArrived += eventArrived;
            watcher.Start();
            return (watcher, null);
        }
        catch (Exception ex) when (ex is ManagementException or TypeInitializationException)
        {
            watcher?.Dispose();
            return (null, new Timer(pollTimerTick, null, pollingInterval, pollingInterval));
        }
    }
}
