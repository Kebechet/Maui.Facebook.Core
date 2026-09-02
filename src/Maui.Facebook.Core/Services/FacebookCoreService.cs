using Microsoft.Extensions.Logging;

namespace Maui.Facebook.Core.Services;

/// <inheritdoc cref="IFacebookCoreService"/>
public partial class FacebookCoreService : IFacebookCoreService
{
    private readonly ILogger<FacebookCoreService> _logger;

    /// <summary>
    /// Creates a new <see cref="FacebookCoreService"/>. Resolved by DI when registered via
    /// <c>IServiceCollectionExtensions.AddFacebookCore</c>.
    /// </summary>
    public FacebookCoreService(ILogger<FacebookCoreService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public partial void Initialize(string appId, string clientToken);

    /// <inheritdoc/>
    public bool IsInitialized => IsInitializedMethod();

    /// <inheritdoc/>
    public string? SdkVersion => SdkVersionMethod();

    /// <inheritdoc/>
    public string? AnonymousId => AnonymousIdMethod();

    /// <inheritdoc/>
    public string? UserId
    {
        get => GetUserIdMethod();
        set => SetUserIdMethod(value);
    }

    /// <inheritdoc/>
    public partial void SetAutoLogAppEventsEnabled(bool isEnabled);

    /// <inheritdoc/>
    public partial void SetAdvertiserIdCollectionEnabled(bool isEnabled);

    /// <inheritdoc/>
    public partial void SetAdvertiserTrackingEnabled(bool isEnabled);

    /// <inheritdoc/>
    public partial void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null, double? valueToSum = null);

    /// <inheritdoc/>
    public partial void LogPurchase(decimal amount, string currencyCode, IReadOnlyDictionary<string, object>? parameters = null);

    /// <inheritdoc/>
    public partial void Flush();

    private partial bool IsInitializedMethod();
    private partial string? SdkVersionMethod();
    private partial string? AnonymousIdMethod();
    private partial string? GetUserIdMethod();
    private partial void SetUserIdMethod(string? userId);
}
