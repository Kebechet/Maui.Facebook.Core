using Maui.Facebook.Core.Services;

namespace Maui.Facebook.Core.Extensions;

/// <summary>
/// DI extension methods for registering the Facebook Core wrapper.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IFacebookCoreService"/> (backed by <see cref="FacebookCoreService"/>)
    /// as a singleton. Consumers should depend on the interface, not the concrete type.
    /// </summary>
    public static IServiceCollection AddFacebookCore(this IServiceCollection services)
    {
        services.AddSingleton<IFacebookCoreService, FacebookCoreService>();

        return services;
    }
}
