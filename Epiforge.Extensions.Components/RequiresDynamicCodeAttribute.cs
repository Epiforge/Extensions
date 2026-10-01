#if !IS_NET_7_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Indicates that the member requires generating code at run time, for targets whose libraries do not expose this attribute
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
sealed class RequiresDynamicCodeAttribute(string message) :
    Attribute
{
    /// <summary>
    /// Gets why the member requires generating code
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets or sets a link to further information
    /// </summary>
    public string? Url { get; set; }
}
#endif
