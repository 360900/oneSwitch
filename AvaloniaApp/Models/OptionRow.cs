using System.Collections.Generic;

namespace AvaloniaApp.Models;

public record OptionRow(ISelectableOption Left, ISelectableOption? Right)
{
    public static IReadOnlyList<OptionRow> Chunk(IReadOnlyList<ISelectableOption> options)
    {
        var rows = new List<OptionRow>();
        for (int i = 0; i < options.Count; i += 2)
        {
            var right = i + 1 < options.Count ? options[i + 1] : null;
            rows.Add(new OptionRow(options[i], right));
        }
        return rows;
    }
}
