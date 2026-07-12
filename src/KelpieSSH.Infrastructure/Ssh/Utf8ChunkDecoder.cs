using System.Text;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>
/// Decodes UTF-8 byte chunks while preserving incomplete multi-byte sequences.
/// </summary>
public sealed class Utf8ChunkDecoder
{
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    /// <summary>
    /// Decodes one byte chunk.
    /// </summary>
    /// <param name="bytes">The byte chunk.</param>
    /// <param name="flush">Whether to flush pending decoder state.</param>
    /// <returns>The decoded text.</returns>
    public string Decode(ReadOnlySpan<byte> bytes, bool flush = false)
    {
        if (bytes.Length == 0 && !flush)
        {
            return string.Empty;
        }

        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        _decoder.Convert(
            bytes,
            chars,
            flush,
            out _,
            out var charsUsed,
            out _);
        if (charsUsed == 0)
        {
            return string.Empty;
        }

        return new string(chars, 0, charsUsed);
    }
}
