using FluentAssertions;
using KelpieMCPServer;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class TerminalScreenBufferTests
{
    [Fact]
    public void Write_ShouldCapturePlainScreenText()
    {
        var screen = new TerminalScreenBuffer(10, 3);

        screen.Write("hello\nworld");

        var snapshot = screen.Capture();
        snapshot.Columns.Should().Be(10);
        snapshot.Rows.Should().Be(3);
        snapshot.Lines.Should().Equal("hello", "world", string.Empty);
        snapshot.CursorRow.Should().Be(1);
        snapshot.CursorColumn.Should().Be(5);
    }

    [Fact]
    public void Write_ShouldApplyCursorMovementAndClearLine()
    {
        var screen = new TerminalScreenBuffer(10, 3);

        screen.Write("abcde\u001b[1GZ\u001b[K");

        var snapshot = screen.Capture();
        snapshot.Lines[0].Should().Be("Z");
        snapshot.CursorRow.Should().Be(0);
        snapshot.CursorColumn.Should().Be(1);
    }

    [Fact]
    public void Write_ShouldScrollWhenOutputExceedsRows()
    {
        var screen = new TerminalScreenBuffer(10, 2);

        screen.Write("one\ntwo\nthree");

        var snapshot = screen.Capture();
        snapshot.Lines.Should().Equal("two", "three");
        snapshot.CursorRow.Should().Be(1);
        snapshot.CursorColumn.Should().Be(5);
    }

    [Fact]
    public void Write_ShouldHandleSplitEscapeSequence()
    {
        var screen = new TerminalScreenBuffer(10, 2);

        screen.Write("abc\u001b[");
        screen.Write("2Jdone");

        var snapshot = screen.Capture();
        snapshot.Lines.Should().Equal("done", string.Empty);
    }
}

