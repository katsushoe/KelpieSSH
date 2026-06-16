using Xunit;

namespace KelpieSSH.Application.Tests.Logging;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableTestCollection
{
    public const string Name = "Environment variable tests";
}
