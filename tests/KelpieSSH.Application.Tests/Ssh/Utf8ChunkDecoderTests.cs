using System.Text;
using FluentAssertions;
using KelpieSSH.Infrastructure.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class Utf8ChunkDecoderTests
{
    [Fact]
    public void Decode_ShouldPreserveSplitMultiByteUtf8Character()
    {
        var decoder = new Utf8ChunkDecoder();
        var bytes = Encoding.UTF8.GetBytes("日");

        var first = decoder.Decode(bytes.AsSpan(0, 1));
        var second = decoder.Decode(bytes.AsSpan(1));

        first.Should().BeEmpty();
        second.Should().Be("日");
    }
}
