namespace KelpieWebPermissionHelper;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "bulk-write", StringComparison.Ordinal))
        {
            return BulkWebTransferCommand.Write(
                args,
                Console.OpenStandardInput(),
                Console.Out,
                Console.Error);
        }

        if (args.Length > 0 && string.Equals(args[0], "bulk-commit", StringComparison.Ordinal))
        {
            return BulkWebTransferCommand.Commit(args, Console.Out, Console.Error);
        }

        if (args.Length > 0 && string.Equals(args[0], "bulk-rollback", StringComparison.Ordinal))
        {
            return BulkWebTransferCommand.Rollback(args, Console.Out, Console.Error);
        }

        return PermissionHelper.Run(
            args,
            new LibcUnixPermissionOperations(),
            Console.Out,
            Console.Error);
    }
}
