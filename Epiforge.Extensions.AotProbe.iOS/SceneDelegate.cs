namespace Epiforge.Extensions.AotProbe;

/// <summary>
/// Shows an empty window when the application's scene connects, as applications built with the iOS 27 SDK must
/// </summary>
[Register(nameof(SceneDelegate))]
public sealed class SceneDelegate :
    UIResponder,
    IUIWindowSceneDelegate
{
    /// <summary>
    /// Gets or sets the scene's window
    /// </summary>
    [Export("window")]
    public UIWindow? Window { get; set; }

    /// <summary>
    /// Creates the scene's window
    /// </summary>
    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
            return;
        Window = new UIWindow(windowScene)
        {
            RootViewController = new UIViewController()
        };
        Window.MakeKeyAndVisible();
    }
}
