using Xunit;

namespace KelpieSSH.Application.Tests.McpServer;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NamedPipeTestCollection
{
    public const string Name = "Named pipe tests";
}
