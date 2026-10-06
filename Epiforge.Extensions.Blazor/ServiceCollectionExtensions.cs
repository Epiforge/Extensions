namespace Epiforge.Extensions.Blazor;

/// <summary>
/// Provides extension methods for registering reactivity services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IReactivityManager"/> as a transient service, for components implementing <see cref="IReactiveComponent"/> directly
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection</returns>
    /// <remarks>Components deriving from <see cref="ReactiveComponentBase"/> make their own reactivity managers and do not need this</remarks>
    public static IServiceCollection AddEpiforgeBlazorReactivity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IReactivityManager, ReactivityManager>();
        return services;
    }
}
