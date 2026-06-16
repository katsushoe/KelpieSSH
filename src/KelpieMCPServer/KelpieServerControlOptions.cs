namespace KelpieMCPServer;

/// <summary>
/// Defines local control channel settings for the Kelpie MCP server process.
/// </summary>
/// <param name="PipeName">The NamedPipe name used for local shutdown requests.</param>
public sealed record KelpieServerControlOptions(string PipeName);
