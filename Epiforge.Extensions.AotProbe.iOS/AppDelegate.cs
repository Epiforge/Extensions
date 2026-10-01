namespace Epiforge.Extensions.AotProbe;

/// <summary>
/// Runs the probe once the application has launched and writes its report to the application's documents folder, where a script collects it
/// </summary>
[Register(nameof(AppDelegate))]
public sealed class AppDelegate :
    UIApplicationDelegate
{
    /// <inheritdoc/>
    public override UIWindow? Window { get; set; }

    /// <inheritdoc/>
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "aot-probe.txt");
        File.WriteAllText(report, "started\n");
        var lines = Probe.Run($"ios {configuration}");
        foreach (var line in lines)
            Console.WriteLine(line);
        File.WriteAllLines(report, lines);
        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = new UIViewController()
        };
        Window.MakeKeyAndVisible();
        return true;
    }
}
