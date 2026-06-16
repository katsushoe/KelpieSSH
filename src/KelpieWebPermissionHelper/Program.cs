namespace KelpieWebPermissionHelper;

internal static class Program
{
    public static int Main(string[] args)
    {
        return PermissionHelper.Run(
            args,
            new LibcUnixPermissionOperations(),
            Console.Out,
            Console.Error);
    }
}
