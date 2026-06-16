namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides registered application configuration path providers.
/// </summary>
public sealed class ServiceConfigPathsProviderCatalog
{
    private readonly Dictionary<string, IServiceConfigPathsProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceConfigPathsProviderCatalog"/> class.
    /// </summary>
    /// <param name="providers">The registered providers.</param>
    public ServiceConfigPathsProviderCatalog(IEnumerable<IServiceConfigPathsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToDictionary(provider => provider.ServiceKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the default application configuration path provider catalog.
    /// </summary>
    /// <returns>The default provider catalog.</returns>
    public static ServiceConfigPathsProviderCatalog CreateDefault()
    {
        return new ServiceConfigPathsProviderCatalog(
        [
            new NginxConfigPathsProvider(),
        ]);
    }

    /// <summary>
    /// Attempts to get a provider by service key.
    /// </summary>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="provider">The matched provider.</param>
    /// <returns><c>true</c> when a provider exists.</returns>
    public bool TryGet(string serviceKey, out IServiceConfigPathsProvider provider)
    {
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            provider = default!;
            return false;
        }

        return _providers.TryGetValue(serviceKey, out provider!);
    }
}
