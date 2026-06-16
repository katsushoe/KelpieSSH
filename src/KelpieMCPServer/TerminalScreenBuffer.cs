namespace KelpieMCPServer;

/// <summary>
/// Maintains a text terminal screen buffer from SSH PTY output.
/// </summary>
public sealed class TerminalScreenBuffer
{
    private readonly char[,] _cells;
    private int _cursorRow;
    private int _cursorColumn;
    private string _pendingEscapeSequence = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalScreenBuffer"/> class.
    /// </summary>
    /// <param name="columns">The screen columns.</param>
    /// <param name="rows">The screen rows.</param>
    public TerminalScreenBuffer(int columns, int rows)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        _cells = new char[Rows, Columns];
        Clear();
    }

    /// <summary>
    /// Gets the screen columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the screen rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Applies terminal output to the screen buffer.
    /// </summary>
    /// <param name="text">The terminal output.</param>
    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var input = _pendingEscapeSequence + text;
        _pendingEscapeSequence = string.Empty;
        for (var index = 0; index < input.Length; index++)
        {
            if (input[index] == '\u001b')
            {
                if (TryHandleEscapeSequence(input, index, out var nextIndex))
                {
                    index = nextIndex;
                    continue;
                }

                if (IsIncompleteEscapeSequence(input, index))
                {
                    _pendingEscapeSequence = input[index..];
                    break;
                }
            }

            WriteCharacter(input[index]);
        }
    }

    /// <summary>
    /// Captures the current screen.
    /// </summary>
    /// <returns>The terminal screen snapshot.</returns>
    public TerminalScreenSnapshot Capture()
    {
        var lines = new string[Rows];
        for (var row = 0; row < Rows; row++)
        {
            var chars = new char[Columns];
            for (var column = 0; column < Columns; column++)
            {
                chars[column] = _cells[row, column];
            }

            lines[row] = new string(chars).TrimEnd();
        }

        return new TerminalScreenSnapshot(
            Columns,
            Rows,
            _cursorRow,
            _cursorColumn,
            lines);
    }

    private void Clear()
    {
        for (var row = 0; row < Rows; row++)
        {
            ClearLineRange(row, 0, Columns - 1);
        }

        _cursorRow = 0;
        _cursorColumn = 0;
    }

    private void WriteCharacter(char ch)
    {
        switch (ch)
        {
            case '\r':
                _cursorColumn = 0;
                return;
            case '\n':
                MoveToNextLine();
                return;
            case '\b':
                _cursorColumn = Math.Max(0, _cursorColumn - 1);
                return;
            case '\t':
                WriteTab();
                return;
            default:
                if (char.IsControl(ch))
                {
                    return;
                }

                WritePrintableCharacter(ch);
                return;
        }
    }

    private void WritePrintableCharacter(char ch)
    {
        if (_cursorColumn >= Columns)
        {
            MoveToNextLine();
        }

        _cells[_cursorRow, _cursorColumn] = ch;
        _cursorColumn++;
    }

    private void WriteTab()
    {
        var nextTabColumn = Math.Min(Columns, ((_cursorColumn / 8) + 1) * 8);
        while (_cursorColumn < nextTabColumn)
        {
            WritePrintableCharacter(' ');
        }
    }

    private void MoveToNextLine()
    {
        if (_cursorRow >= Rows - 1)
        {
            ScrollUp();
        }
        else
        {
            _cursorRow++;
        }

        _cursorColumn = 0;
    }

    private void ScrollUp()
    {
        for (var row = 1; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                _cells[row - 1, column] = _cells[row, column];
            }
        }

        ClearLineRange(Rows - 1, 0, Columns - 1);
    }

    private bool TryHandleEscapeSequence(string text, int escapeIndex, out int nextIndex)
    {
        nextIndex = escapeIndex;
        if (escapeIndex + 1 >= text.Length)
        {
            return false;
        }

        var marker = text[escapeIndex + 1];
        if (marker is '(' or ')' or '*' or '+')
        {
            if (escapeIndex + 2 >= text.Length)
            {
                return false;
            }

            nextIndex = escapeIndex + 2;
            return true;
        }

        if (marker == ']')
        {
            return TryHandleOperatingSystemCommand(text, escapeIndex, out nextIndex);
        }

        if (marker is '7' or '8' or '=' or '>')
        {
            nextIndex = escapeIndex + 1;
            return true;
        }

        if (marker == 'c')
        {
            Clear();
            nextIndex = escapeIndex + 1;
            return true;
        }

        if (marker != '[')
        {
            return false;
        }

        var commandIndex = escapeIndex + 2;
        while (commandIndex < text.Length && !IsAnsiCommand(text[commandIndex]))
        {
            commandIndex++;
        }

        if (commandIndex >= text.Length)
        {
            return false;
        }

        var arguments = text.Substring(escapeIndex + 2, commandIndex - escapeIndex - 2);
        HandleAnsiCommand(arguments, text[commandIndex]);
        nextIndex = commandIndex;
        return true;
    }

    private static bool TryHandleOperatingSystemCommand(string text, int escapeIndex, out int nextIndex)
    {
        nextIndex = escapeIndex;
        for (var index = escapeIndex + 2; index < text.Length; index++)
        {
            if (text[index] == '\a')
            {
                nextIndex = index;
                return true;
            }

            if (text[index] == '\u001b'
                && index + 1 < text.Length
                && text[index + 1] == '\\')
            {
                nextIndex = index + 1;
                return true;
            }
        }

        return false;
    }

    private static bool IsIncompleteEscapeSequence(string text, int escapeIndex)
    {
        if (escapeIndex + 1 >= text.Length)
        {
            return true;
        }

        var marker = text[escapeIndex + 1];
        if (marker is '(' or ')' or '*' or '+')
        {
            return escapeIndex + 2 >= text.Length;
        }

        if (marker == ']')
        {
            return !TryHandleOperatingSystemCommand(text, escapeIndex, out _);
        }

        if (marker != '[')
        {
            return false;
        }

        var commandIndex = escapeIndex + 2;
        while (commandIndex < text.Length && !IsAnsiCommand(text[commandIndex]))
        {
            commandIndex++;
        }

        return commandIndex >= text.Length;
    }

    private static bool IsAnsiCommand(char ch)
    {
        return ch is '@' or 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or 'H'
            or 'J' or 'K' or 'P' or 'X' or 'd' or 'e' or 'f' or 'm' or 'r' or 's' or 'u'
            or 'h' or 'l';
    }

    private void HandleAnsiCommand(string arguments, char command)
    {
        var values = ParseAnsiArguments(arguments);
        switch (command)
        {
            case '@':
                InsertBlankCharacters(GetAnsiValue(values, 0, 1));
                return;
            case 'A':
                MoveCursor(rowOffset: -GetAnsiValue(values, 0, 1), columnOffset: 0);
                return;
            case 'B':
            case 'e':
                MoveCursor(rowOffset: GetAnsiValue(values, 0, 1), columnOffset: 0);
                return;
            case 'C':
                MoveCursor(rowOffset: 0, columnOffset: GetAnsiValue(values, 0, 1));
                return;
            case 'D':
                MoveCursor(rowOffset: 0, columnOffset: -GetAnsiValue(values, 0, 1));
                return;
            case 'E':
                MoveCursor(rowOffset: GetAnsiValue(values, 0, 1), columnOffset: -_cursorColumn);
                return;
            case 'F':
                MoveCursor(rowOffset: -GetAnsiValue(values, 0, 1), columnOffset: -_cursorColumn);
                return;
            case 'G':
                SetCursorPosition(_cursorRow, GetAnsiValue(values, 0, 1) - 1);
                return;
            case 'H':
            case 'f':
                SetCursorPosition(GetAnsiValue(values, 0, 1) - 1, GetAnsiValue(values, 1, 1) - 1);
                return;
            case 'J':
                ClearDisplay(GetAnsiValue(values, 0, 0));
                return;
            case 'K':
                ClearLine(GetAnsiValue(values, 0, 0));
                return;
            case 'P':
                DeleteCharacters(GetAnsiValue(values, 0, 1));
                return;
            case 'X':
                EraseCharacters(GetAnsiValue(values, 0, 1));
                return;
            case 'd':
                SetCursorPosition(GetAnsiValue(values, 0, 1) - 1, _cursorColumn);
                return;
            case 'm':
            case 'r':
            case 's':
            case 'u':
            case 'h':
            case 'l':
                return;
        }
    }

    private static int[] ParseAnsiArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        return arguments
            .Split(';')
            .Select(value => int.TryParse(value.TrimStart('?', '>', '=', '!'), out var parsed) ? parsed : 0)
            .ToArray();
    }

    private static int GetAnsiValue(IReadOnlyList<int> values, int index, int defaultValue)
    {
        return index < values.Count && values[index] != 0 ? values[index] : defaultValue;
    }

    private void MoveCursor(int rowOffset, int columnOffset)
    {
        SetCursorPosition(_cursorRow + rowOffset, _cursorColumn + columnOffset);
    }

    private void SetCursorPosition(int row, int column)
    {
        _cursorRow = Math.Clamp(row, 0, Rows - 1);
        _cursorColumn = Math.Clamp(column, 0, Columns - 1);
    }

    private void ClearDisplay(int mode)
    {
        if (mode == 2 || mode == 3)
        {
            Clear();
            return;
        }

        if (mode == 1)
        {
            ClearLineRange(_cursorRow, 0, _cursorColumn);
            for (var row = 0; row < _cursorRow; row++)
            {
                ClearLineRange(row, 0, Columns - 1);
            }

            return;
        }

        ClearLineRange(_cursorRow, _cursorColumn, Columns - 1);
        for (var row = _cursorRow + 1; row < Rows; row++)
        {
            ClearLineRange(row, 0, Columns - 1);
        }
    }

    private void ClearLine(int mode)
    {
        var start = mode == 1 ? 0 : _cursorColumn;
        var end = mode == 1 ? _cursorColumn : Columns - 1;
        if (mode == 2)
        {
            start = 0;
            end = Columns - 1;
        }

        ClearLineRange(_cursorRow, start, end);
    }

    private void InsertBlankCharacters(int count)
    {
        var safeCount = Math.Min(Math.Max(1, count), Columns - _cursorColumn);
        for (var column = Columns - 1; column >= _cursorColumn + safeCount; column--)
        {
            _cells[_cursorRow, column] = _cells[_cursorRow, column - safeCount];
        }

        ClearLineRange(_cursorRow, _cursorColumn, _cursorColumn + safeCount - 1);
    }

    private void DeleteCharacters(int count)
    {
        var safeCount = Math.Min(Math.Max(1, count), Columns - _cursorColumn);
        for (var column = _cursorColumn; column < Columns - safeCount; column++)
        {
            _cells[_cursorRow, column] = _cells[_cursorRow, column + safeCount];
        }

        ClearLineRange(_cursorRow, Columns - safeCount, Columns - 1);
    }

    private void EraseCharacters(int count)
    {
        var safeCount = Math.Min(Math.Max(1, count), Columns - _cursorColumn);
        ClearLineRange(_cursorRow, _cursorColumn, _cursorColumn + safeCount - 1);
    }

    private void ClearLineRange(int row, int start, int end)
    {
        for (var column = Math.Max(0, start); column <= Math.Min(end, Columns - 1); column++)
        {
            _cells[row, column] = ' ';
        }
    }
}

/// <summary>
/// Represents a captured text terminal screen.
/// </summary>
public sealed record TerminalScreenSnapshot(
    int Columns,
    int Rows,
    int CursorRow,
    int CursorColumn,
    string[] Lines);
