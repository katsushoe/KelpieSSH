using System.Text;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>A write-only receiver with a fixed allocation; overflow is signalled off the SSH callback.</summary>
internal sealed class BoundedSshOutput(int capacity, TaskCompletionSource overflow) : Stream
{
    private readonly byte[] _bytes = new byte[capacity];
    private readonly object _sync = new();
    private int _length;
    internal bool Exceeded { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length { get { lock (_sync) return _length; } }
    public override long Position { get => Length; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (_sync)
        {
            if (Exceeded) return;
            if (buffer.Length > _bytes.Length - _length)
            {
                Exceeded = true;
                overflow.TrySetResult();
                return;
            }
            buffer.CopyTo(_bytes.AsSpan(_length));
            _length += buffer.Length;
        }
    }
    internal string Decode()
    {
        lock (_sync)
        {
            if (Exceeded) throw new SshCommandOutputLimitException();
            return new UTF8Encoding(false, true).GetString(_bytes, 0, _length);
        }
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
