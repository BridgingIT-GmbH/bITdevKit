// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>Deterministic type/member naming shared by descriptors and scopes.</summary>
/// <example><code>var key = AuditNamingConvention.Default.GetKey(typeof(Customer), "Rename");</code></example>
public class AuditNamingConvention
{
    /// <summary>The convention used without dependency injection.</summary>
    /// <example><code>var naming = AuditNamingConvention.Default;</code></example>
    public static AuditNamingConvention Default { get; } = new();

    /// <summary>Returns a qualified type/member identity without instance identifiers.</summary>
    /// <remarks>Constructed generic arguments retain qualified type names without assembly-version metadata.</remarks>
    /// <example><code>var key = naming.GetKey(typeof(Customer), "Rename");</code></example>
    public virtual string GetKey(Type type, string operation = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = type.IsConstructedGenericType ? type.ToString() : type.FullName ?? type.Name;
        return name + (string.IsNullOrWhiteSpace(operation) ? "" : "." + operation);
    }

    /// <summary>Returns a readable label, removing conventional type/member suffixes.</summary>
    /// <example><code>var label = naming.GetDisplayName(typeof(Customer), "RenameAsync");</code></example>
    public virtual string GetDisplayName(Type type, string operation = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = Regex.Replace(type.Name.Split('`')[0], "(?:CommandHandler|QueryHandler|NotificationHandler|Command|Query|Notification|Handler)$", "", RegexOptions.CultureInvariant);
        return Humanize(name) + (string.IsNullOrWhiteSpace(operation) ? "" : ": " + Humanize(Regex.Replace(operation, "Async$", "", RegexOptions.CultureInvariant)));
    }

    /// <summary>Returns the resource type alias; override for a stable business vocabulary.</summary>
    /// <example><code>var resourceType = naming.GetResourceType(typeof(Customer));</code></example>
    public virtual string GetResourceType(Type type) => this.GetKey(type);

    /// <summary>Converts a caller-selected identifier using invariant formatting.</summary>
    /// <example><code>var id = naming.ConvertId(customerId);</code></example>
    public virtual string ConvertId(object id) => AuditStructuralValidation.Required(id switch
    {
        null => null,
        string text => text,
        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
        _ => id.ToString()
    }, 256, nameof(id));

    /// <summary>Produces the shared invariant readable label.</summary>
    /// <example><code>var label = AuditNamingConvention.Humanize("RenameCustomer");</code></example>
    public static string Humanize(string text)
    {
        var words = Regex.Replace(text ?? "", "([a-z0-9])([A-Z])", "$1 $2", RegexOptions.CultureInvariant).Replace('_', ' ').Replace('.', ' ');
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant();
    }
}
