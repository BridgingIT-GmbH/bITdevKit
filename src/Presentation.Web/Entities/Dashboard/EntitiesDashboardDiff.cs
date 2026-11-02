// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>One line of a value-object JSON diff.</summary>
/// <param name="Kind">The line kind: <c>same</c>, <c>added</c> or <c>removed</c>.</param>
/// <param name="Text">The line text.</param>
internal sealed record EntitiesDashboardDiffLine(string Kind, string Text);

/// <summary>The change of one property in a diff.</summary>
/// <param name="Property">The property name.</param>
/// <param name="Masked">True when the property is masked; no values are included.</param>
/// <param name="Before">The stored display value (null for masked properties or null values).</param>
/// <param name="After">The new display value (null for masked properties or null values).</param>
/// <param name="Lines">The indented JSON line diff for value-object properties, otherwise an empty list.</param>
internal sealed record EntitiesDashboardDiffEntry(
    string Property,
    bool Masked,
    string Before,
    string After,
    IReadOnlyList<EntitiesDashboardDiffLine> Lines);

/// <summary>The result of comparing stored and submitted values.</summary>
/// <param name="Entries">The changed properties.</param>
internal sealed record EntitiesDashboardDiffResult(IReadOnlyList<EntitiesDashboardDiffEntry> Entries)
{
    /// <summary>Gets a value indicating whether at least one property changed.</summary>
    public bool HasChanges => this.Entries.Count > 0;
}

/// <summary>The payload of an update outcome.</summary>
/// <param name="Id">The id text of the record.</param>
/// <param name="Changed">True when at least one property changed.</param>
/// <param name="Diff">The changed properties.</param>
internal sealed record EntitiesDashboardUpdatePayload(string Id, bool Changed, IReadOnlyList<EntitiesDashboardDiffEntry> Diff);

/// <summary>Builds the before/after diff shown on the update review step. Masked values are never included.</summary>
internal static class EntitiesDashboardDiff
{
    private const int MAXLINES = 2000;

    /// <summary>Builds the diff for a set of changes; hidden properties are skipped and masked properties only report that they changed.</summary>
    /// <param name="changes">The changes.</param>
    /// <returns>The diff; <see cref="EntitiesDashboardDiffResult.HasChanges"/> is false when nothing changed.</returns>
    public static EntitiesDashboardDiffResult Compute(IEnumerable<EntitiesDashboardChange> changes)
    {
        var entries = new List<EntitiesDashboardDiffEntry>();
        foreach (var change in changes.Where(c => !c.Property.IsHidden))
        {
            var property = change.Property;
            if (property.IsMasked)
            {
                entries.Add(new EntitiesDashboardDiffEntry(property.Name, true, null, null, []));

                continue;
            }

            if (property.EditorKind == EntitiesDashboardEditorKind.Json)
            {
                var before = EntitiesDashboardMasking.MaskJson(property, change.Before);
                var after = EntitiesDashboardMasking.MaskJson(property, change.After);
                entries.Add(new EntitiesDashboardDiffEntry(property.Name, false, before, after, DiffLines(before, after)));

                continue;
            }

            entries.Add(new EntitiesDashboardDiffEntry(
                property.Name,
                false,
                EntitiesDashboardValueConverter.Format(change.Before),
                EntitiesDashboardValueConverter.Format(change.After),
                []));
        }

        return new EntitiesDashboardDiffResult(entries);
    }

    /// <summary>Computes a line based diff between two texts.</summary>
    /// <param name="before">The old text.</param>
    /// <param name="after">The new text.</param>
    /// <returns>The diff lines in order.</returns>
    public static IReadOnlyList<EntitiesDashboardDiffLine> DiffLines(string before, string after)
    {
        var a = Split(before);
        var b = Split(after);
        var result = new List<EntitiesDashboardDiffLine>();

        if (a.Length > MAXLINES || b.Length > MAXLINES)
        {
            result.AddRange(a.Select(l => new EntitiesDashboardDiffLine("removed", l)));
            result.AddRange(b.Select(l => new EntitiesDashboardDiffLine("added", l)));

            return result;
        }

        var lengths = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                result.Add(new EntitiesDashboardDiffLine("same", a[x]));
                x++;
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                result.Add(new EntitiesDashboardDiffLine("removed", a[x++]));
            }
            else
            {
                result.Add(new EntitiesDashboardDiffLine("added", b[y++]));
            }
        }

        while (x < a.Length)
        {
            result.Add(new EntitiesDashboardDiffLine("removed", a[x++]));
        }

        while (y < b.Length)
        {
            result.Add(new EntitiesDashboardDiffLine("added", b[y++]));
        }

        return result;
    }

    private static string[] Split(string text) =>
        string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
