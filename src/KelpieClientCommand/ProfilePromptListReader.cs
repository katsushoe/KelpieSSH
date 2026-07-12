namespace KelpieClientCommand;

/// <summary>
/// Reads optional multi-value profile template prompts.
/// </summary>
public static class ProfilePromptListReader
{
    /// <summary>
    /// Reads one optional list prompt. Pressing Enter at the first prompt keeps the provided defaults.
    /// </summary>
    /// <param name="reader">The input reader.</param>
    /// <param name="writer">The prompt writer.</param>
    /// <param name="title">The prompt title.</param>
    /// <param name="defaultValues">The default values.</param>
    /// <returns>The selected values.</returns>
    public static IReadOnlyList<string> Read(
        TextReader reader,
        TextWriter writer,
        string title,
        IReadOnlyCollection<string> defaultValues)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(defaultValues);

        var defaults = defaultValues.ToArray();
        var values = new List<string>();
        writer.Write(CreatePrompt(title, defaults));
        var firstValue = reader.ReadLine();
        if (firstValue is null)
        {
            return defaults;
        }

        var trimmedFirstValue = firstValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmedFirstValue))
        {
            return defaults;
        }

        if (string.Equals(trimmedFirstValue, "-", StringComparison.Ordinal))
        {
            return [];
        }

        values.Add(trimmedFirstValue);

        while (true)
        {
            writer.Write($"{title} [Return to finish]: ");
            var value = reader.ReadLine();
            if (value is null)
            {
                break;
            }

            var trimmed = value.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }

            if (string.Equals(trimmed, "-", StringComparison.Ordinal))
            {
                return [];
            }

            values.Add(trimmed);
        }

        return values;
    }

    private static string CreatePrompt(string title, IReadOnlyCollection<string> defaultValues)
    {
        if (defaultValues.Count == 0)
        {
            return $"{title} [Return to skip]: ";
        }

        return $"{title} [{string.Join(", ", defaultValues)}; '-' to clear]: ";
    }
}
