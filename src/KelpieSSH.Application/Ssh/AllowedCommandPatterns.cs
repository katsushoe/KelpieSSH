namespace KelpieSSH.Application.Ssh;

internal static class AllowedCommandPatterns
{
    public const string TcpPort = "^(6553[0-5]|655[0-2][0-9]|65[0-4][0-9]{2}|6[0-4][0-9]{3}|[1-5][0-9]{4}|[1-9][0-9]{0,3})$";
}
