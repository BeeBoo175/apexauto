namespace Dealership.Application.Common;

// One quoted CSV cell. A value a spreadsheet could run as a formula, or that hides behind control characters, is
// prefixed with an apostrophe so it opens as text.
public static class CsvCell
{
    public static string Quote(string value)
    {
        var index = 0;
        var controlPrefix = false;
        while (index < value.Length && (char.IsWhiteSpace(value[index]) || char.IsControl(value[index]) || value[index] == '﻿'))
        {
            controlPrefix |= char.IsControl(value[index]);
            index++;
        }

        if (controlPrefix || (index < value.Length && "=+-@".Contains(value[index])))
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
