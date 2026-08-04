namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Creates command-processing providers used by Kelpie.
/// </summary>
public static class CommandProcessingProviderCatalog
{
    /// <summary>
    /// Creates the default command-processing provider set.
    /// </summary>
    /// <returns>The default providers.</returns>
    public static IReadOnlyCollection<ICommandProcessingProvider> CreateDefault()
    {
        return
        [
            new CommonDiagnosticCommandProvider(),
            new NginxServiceConfigCommandProvider(),
            new WebPublicFileCommandProvider(),
            new AlpineApkCommandProvider(),
            new DebianAptCommandProvider(),
            new DebianNginxCommandProvider(),
            new RhelDnfCommandProvider(),
            new RhelNginxCommandProvider(),
        ];
    }
}
