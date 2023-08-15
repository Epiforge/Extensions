namespace Epiforge.Extensions.Platforms.Windows;

/// <summary>
/// Provides information relating to Windows Activation
/// </summary>
[SuppressMessage("Naming", "CA1724: Type names should not match namespaces")]
public static class Activation
{
    static readonly Lazy<string> lazyProductKey = new(ProductKeyDecoder.GetWindowsProductKeyFromRegistry);

    /// <summary>
    /// Gets the Windows product key
    /// </summary>
    public static string ProductKey =>
        lazyProductKey.Value;
}
