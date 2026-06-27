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

    [Fact]
    public void Write_ShouldRenderFullScreenTerminalRefresh()
    {
        var screen = new TerminalScreenBuffer(20, 4);

        screen.Write("old line 1\nold line 2\nold line 3");
        screen.Write("\u001b[?1049h\u001b[?25l\u001b[H\u001b[2Jtop - 12:00:00\nTasks: 2 total\n%Cpu(s): 1.0 us\u001b[?25h");

        var snapshot = screen.Capture();
        snapshot.Lines.Should().Equal(
            "top - 12:00:00",
            "Tasks: 2 total",
            "%Cpu(s): 1.0 us",
            string.Empty);
        snapshot.CursorRow.Should().Be(2);
        snapshot.CursorColumn.Should().Be(15);
    }

    [Fact]
    public void Write_ShouldRenderPagerStatusLineRefresh()
    {
        var screen = new TerminalScreenBuffer(24, 3);

        screen.Write("line 1\nline 2\n\u001b[7m--More--(50%)\u001b[m");
        screen.Write("\r\u001b[K\u001b[7m--More--(75%)\u001b[m");

        var snapshot = screen.Capture();
        snapshot.Lines.Should().Equal(
            "line 1",
            "line 2",
            "--More--(75%)");
        snapshot.CursorRow.Should().Be(2);
        snapshot.CursorColumn.Should().Be(13);
    }

    [Fact]
    public void Write_ShouldKeepTailFollowOutputOnScreenAfterScroll()
    {
        var screen = new TerminalScreenBuffer(16, 3);

        screen.Write("log 1\nlog 2\nlog 3\nlog 4\nlog 5");

        var snapshot = screen.Capture();
        snapshot.Lines.Should().Equal("log 3", "log 4", "log 5");
        snapshot.CursorRow.Should().Be(2);
        snapshot.CursorColumn.Should().Be(5);
    }
}

