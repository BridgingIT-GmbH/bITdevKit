// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.ObjectModel;

/// <summary>Copied business labels and explicit values; constructing it does not enable capture.</summary>
/// <example><code>var descriptor = AuditDescriptorBuilder.For&lt;Customer&gt;().Build();</code></example>
public sealed record AuditDescriptor
{
    /// <summary>Copies descriptor collections and validates structural fields.</summary>
    /// <example><code>var descriptor = new AuditDescriptor("customers.rename", "Rename customer");</code></example>
    public AuditDescriptor(string key, string displayName = null, string reason = null,
        IEnumerable<AuditResourceReference> resources = null, IEnumerable<KeyValuePair<string, AuditValue>> details = null,
        Type contextType = null, string operation = null, bool keyDerived = false, bool displayNameDerived = false)
    {
        this.Key = AuditStructuralValidation.Required(key, 512, nameof(key));
        this.DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
        this.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        this.Resources = Array.AsReadOnly((resources ?? []).ToArray());
        var copy = new Dictionary<string, AuditValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in details ?? []) copy[AuditStructuralValidation.DetailKey(item.Key)] = item.Value ?? throw new ArgumentException("Audit detail cannot be null.");
        this.Details = new ReadOnlyDictionary<string, AuditValue>(copy);
        this.ContextType = contextType;
        this.Operation = operation;
        this.KeyDerived = keyDerived;
        this.DisplayNameDerived = displayNameDerived;
    }

    /// <summary>The stable business key.</summary>
    /// <example><code>var key = descriptor.Key;</code></example>
    public string Key { get; }

    /// <summary>The frozen business label.</summary>
    /// <example><code>var label = descriptor.DisplayName;</code></example>
    public string DisplayName { get; }

    /// <summary>An explicitly supplied business reason.</summary>
    /// <example><code>var reason = descriptor.Reason;</code></example>
    public string Reason { get; }

    /// <summary>Copied resource references.</summary>
    /// <example><code>var count = descriptor.Resources.Count;</code></example>
    public IReadOnlyList<AuditResourceReference> Resources { get; }

    /// <summary>Copied explicitly projected details.</summary>
    /// <example><code>var count = descriptor.Details.Count;</code></example>
    public IReadOnlyDictionary<string, AuditValue> Details { get; }

    /// <summary>Optional naming context, resolved before capture is frozen.</summary>
    /// <example><code>var context = descriptor.ContextType;</code></example>
    public Type ContextType { get; }

    /// <summary>The explicit member identity used for derived naming.</summary>
    /// <example><code>var operation = descriptor.Operation;</code></example>
    public string Operation { get; }

    /// <summary>Whether configuration may replace the derived key.</summary>
    /// <example><code>var derived = descriptor.KeyDerived;</code></example>
    public bool KeyDerived { get; }

    /// <summary>Whether configuration may replace the derived display label.</summary>
    /// <example><code>var derived = descriptor.DisplayNameDerived;</code></example>
    public bool DisplayNameDerived { get; }
}
