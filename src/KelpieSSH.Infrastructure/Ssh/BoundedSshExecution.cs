using System.Globalization;
using System.Text;
using KelpieSSH.Application.Ssh;
using Renci.SshNet;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>Opt-in non-PTY shell transport whose output bypasses SshCommand's growing PipeStream.</summary>
internal static class BoundedSshExecution
{
    private const int FrameReserve = 96;

    internal static SshCommandResult Execute(SshClient client, SshCommandRequest request, string commandText,
        string? standardInput, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var limits = request.ExecutionLimits!;
        limits.Validate();
        if (request.BinaryStandardInput is not null) throw new ArgumentException("Bounded SSH requires text input.");
        var bytes = new UTF8Encoding(false, true).GetBytes(standardInput ?? "");
        if (bytes.Length > limits.StandardInputBytes) throw new ArgumentException("Bounded SSH input exceeded its byte limit.");
        var nonce = Guid.NewGuid().ToString("N");
        var script = BuildInput(commandText, bytes, nonce);
        using var input = new FiniteShellInput(Encoding.UTF8.GetBytes(script));
        var overflow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var output = new BoundedSshOutput(limits.StandardOutputBytes + FrameReserve, overflow);
        using var error = new BoundedSshOutput(limits.StandardErrorBytes, overflow);
        using var shell = client.CreateShellNoTerminal(input, output, error, bufferSize: 4096);
        shell.Stopped += (_, _) => stopped.TrySetResult();
        shell.ErrorOccurred += (_, args) =>
        {
            // Closing the channel disposes input before the upstream listener exits.
            // A missing completion frame still rejects any incomplete remote execution.
            if (args.Exception is not FiniteShellInput.EndOfInputException && !input.IsClosed)
                stopped.TrySetException(new IOException("Bounded SSH channel failed."));
        };
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            shell.Start();
            Task.WhenAny(stopped.Task, overflow.Task).WaitAsync(cancellationToken).GetAwaiter().GetResult();
            if (overflow.Task.IsCompleted) throw new SshCommandOutputLimitException();
            stopped.Task.GetAwaiter().GetResult();
            var (text, exitCode) = ParseOutput(output.Decode(), nonce, limits.StandardOutputBytes);
            return new SshCommandResult(request.CommandName, request.CommandText, exitCode, text, error.Decode(),
                startedAt, DateTimeOffset.UtcNow, TimedOut: false);
        }
        finally
        {
            // Closing only this dedicated channel avoids waiting on an unbounded remote stream.
            if (shell.IsStarted) shell.Stop();
        }
    }

    internal static string BuildInput(string commandText, byte[] input, string nonce)
    {
        if (commandText.Contains('\0') || !Guid.TryParseExact(nonce, "N", out _)) throw new ArgumentException("Invalid bounded SSH frame.");
        // Input is data, never shell source. The command itself comes from the validated command catalog.
        return "printf '%s' '" + Convert.ToBase64String(input) + "' | /usr/bin/base64 -d | (\n" + commandText
            + "\n)\n__kelpie_rc=$?\nprintf '\\nKELPIE_EXIT_" + nonce + ":%s\\n' \"$__kelpie_rc\"\nexit \"$__kelpie_rc\"\n";
    }

    internal static (string Output, int ExitCode) ParseOutput(string output, string nonce, int maxBytes)
    {
        var marker = "\nKELPIE_EXIT_" + nonce + ":";
        var index = output.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || !output.EndsWith('\n')) throw new IOException("Bounded SSH completion frame is missing.");
        var status = output.AsSpan(index + marker.Length, output.Length - index - marker.Length - 1);
        if (!int.TryParse(status, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code is < 0 or > 255)
            throw new IOException("Bounded SSH completion frame is invalid.");
        var text = output[..index];
        if (Encoding.UTF8.GetByteCount(text) > maxBytes) throw new SshCommandOutputLimitException();
        return (text, code);
    }

    // SSH.NET's shell listener sends zero-length data instead of ending on EOF. End
    // its input listener before channel close, so its synchronous close wait cannot
    // race a final zero-byte SendData against session/channel disposal.
    internal sealed class FiniteShellInput(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        private bool _closed;
        internal bool IsClosed => Volatile.Read(ref _closed);
        internal sealed class EndOfInputException : IOException;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsClosed) return Task.FromException<int>(new EndOfInputException());
            if (Position < Length) return Task.FromResult(Read(buffer, offset, count));
            return Task.FromException<int>(new EndOfInputException());
        }
        protected override void Dispose(bool disposing)
        {
            Volatile.Write(ref _closed, true);
            base.Dispose(disposing);
        }
    }
}
