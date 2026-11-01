// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Builds immutable descriptors with optional explicit business vocabulary.</summary>
/// <example><code>var descriptor = AuditDescriptorBuilder.For&lt;Customer&gt;().Reason("Correction").Build();</code></example>
public sealed class AuditDescriptorBuilder
{
    private Type contextType;
    private string key;
    private string displayName;
    private string reason;
    private readonly List<AuditResourceReference> resources = [];
    private readonly Dictionary<string, AuditValue> details = new(StringComparer.OrdinalIgnoreCase);
    private AuditDescriptorBuilder(Type type) => this.contextType = type;

    /// <summary>Establishes a type context without resolving an instance.</summary>
    /// <example><code>var builder = AuditDescriptorBuilder.For&lt;Customer&gt;();</code></example>
    public static AuditDescriptorBuilder For<T>() => For(typeof(T));

    /// <summary>Establishes a runtime type context.</summary>
    /// <example><code>var builder = AuditDescriptorBuilder.For(typeof(Customer));</code></example>
    public static AuditDescriptorBuilder For(Type type) => new(type ?? throw new ArgumentNullException(nameof(type)));

    /// <summary>Overrides the derived business key.</summary>
    /// <example><code>builder.Key("customers.rename");</code></example>
    public AuditDescriptorBuilder Key(string value) { this.key = value; return this; }

    /// <summary>Overrides the derived label when nonempty.</summary>
    /// <example><code>builder.DisplayName("Rename customer");</code></example>
    public AuditDescriptorBuilder DisplayName(string value) { this.displayName = value; return this; }

    /// <summary>Adds an explicit business reason.</summary>
    /// <example><code>builder.Reason("Correction");</code></example>
    public AuditDescriptorBuilder Reason(string value) { this.reason = value; return this; }

    /// <summary>Adds a typed resource with invariant identifier conversion.</summary>
    /// <example><code>builder.Resource&lt;Customer&gt;(customerId);</code></example>
    public AuditDescriptorBuilder Resource<T>(object id, string displayName = null) { this.resources.Add(new(AuditNamingConvention.Default.GetResourceType(typeof(T)), AuditNamingConvention.Default.ConvertId(id), displayName, typeof(T))); return this; }

    /// <summary>Adds an external or explicitly named resource.</summary>
    /// <example><code>builder.Resource("Customer", "C-123");</code></example>
    public AuditDescriptorBuilder Resource(string type, object id, string displayName = null) { this.resources.Add(new(type, AuditNamingConvention.Default.ConvertId(id), displayName)); return this; }

    /// <summary>Adds or replaces an explicit projected detail.</summary>
    /// <example><code>builder.Detail("count", AuditValue.From(1L));</code></example>
    public AuditDescriptorBuilder Detail(string name, AuditValue value) { this.details[AuditStructuralValidation.DetailKey(name)] = value ?? throw new ArgumentNullException(nameof(value)); return this; }

    /// <summary>Validates and copies all collections.</summary>
    /// <example><code>var descriptor = builder.Build();</code></example>
    public AuditDescriptor Build() => new(this.key ?? AuditNamingConvention.Default.GetKey(this.contextType),
        string.IsNullOrWhiteSpace(this.displayName) ? AuditNamingConvention.Default.GetDisplayName(this.contextType) : this.displayName,
        this.reason, this.resources, this.details, this.contextType, keyDerived: this.key is null, displayNameDerived: string.IsNullOrWhiteSpace(this.displayName));
}
