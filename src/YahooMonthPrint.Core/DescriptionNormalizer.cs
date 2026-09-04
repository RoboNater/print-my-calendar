using System.Text;
using System.Text.RegularExpressions;

namespace YahooMonthPrint.Core;

public static partial class DescriptionNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = DecodeIcsText(value).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = decoded.Split('\n')
            .Select(line => InLineWhitespace().Replace(line, " ").Trim())
            .ToList();

        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var result = new List<string>(lines.Count);
        var previousWasBlank = false;
        foreach (var line in lines)
        {
            var isBlank = line.Length == 0;
            if (!isBlank || !previousWasBlank)
            {
                result.Add(line);
            }

            previousWasBlank = isBlank;
        }

        return string.Join(Environment.NewLine, result);
    }

    private static string DecodeIcsText(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index + 1 >= value.Length)
            {
                builder.Append(value[index]);
                continue;
            }

            var escaped = value[++index];
            builder.Append(escaped switch
            {
                'n' or 'N' => '\n',
                ',' => ',',
                ';' => ';',
                '\\' => '\\',
                _ => escaped,
            });
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"[\t\f\v ]+")]
    private static partial Regex InLineWhitespace();
}
