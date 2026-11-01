// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

using System.Text;

/// <summary>CEF:0 field escaping with UTF-8 byte limits. Header line breaks become spaces; extension line breaks are escaped.</summary>
/// <example><code>var safe = CefAuditMappingV1.Escape("Customer|Rename", header: true, 512, out var changed);</code></example>
public static class CefAuditMappingV1
{
    /// <summary>Escapes a single value without splitting Unicode scalars or escape sequences, reporting any loss.</summary>
    /// <example><code>var safe = CefAuditMappingV1.Escape(value, false, 1024, out var lossy);</code></example>
    public static string Escape(string value, bool header, int maximumBytes, out bool lossy)
    {
        if (maximumBytes is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var result = new StringBuilder(Math.Min(value?.Length ?? 0, maximumBytes));
        var bytes = 0; lossy = false;
        foreach (var rune in (value ?? string.Empty).EnumerateRunes())
        {
            var escaped = rune.Value switch
            {
                '\\' => "\\\\", '|' when header => "\\|", '=' when !header => "\\=",
                '\r' => header ? " " : "\\r", '\n' => header ? " " : "\\n",
                < 32 or 127 => " ", _ => rune.ToString()
            };
            if (rune.Value < 32 || rune.Value == 127) lossy |= header || rune.Value is not ('\r' or '\n');
            var length = Encoding.UTF8.GetByteCount(escaped);
            if (bytes + length > maximumBytes) { lossy = true; break; }

            result.Append(escaped); bytes += length;
        }

        return result.ToString();
    }
}
