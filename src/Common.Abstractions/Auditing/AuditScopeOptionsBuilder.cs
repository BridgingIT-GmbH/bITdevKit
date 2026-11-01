// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Derives consistent scope names and validates immutable options.</summary>
/// <example><code>var options = AuditScopeOptionsBuilder.Create&lt;Customer&gt;("Rename").Build();</code></example>
public sealed class AuditScopeOptionsBuilder
{
    private Type contextType;
    private string operation;
    private string key;
    private string displayName;
    private string kind = "Business";
    private AuditScopeMode mode;
    private AuditDescriptor described;

    /// <summary>Merges copied explicit metadata after conventions and before builder overrides.</summary>
    /// <example><code>builder.Describe(command.DescribeAudit());</code></example>
    public AuditScopeOptionsBuilder Describe(AuditDescriptor value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.described = new AuditDescriptor(value.Key, value.DisplayName, value.Reason ?? this.described?.Reason,
            (this.described?.Resources ?? []).Concat(value.Resources),
            (this.described?.Details ?? new Dictionary<string, AuditValue>()).Concat(value.Details),
            value.ContextType ?? this.contextType, this.operation, value.KeyDerived, value.DisplayNameDerived);
        return this;
    }

    /// <summary>Creates an unconfigured or explicitly keyed builder.</summary>
    /// <example><code>var builder = AuditScopeOptionsBuilder.Create("customers.rename");</code></example>
    public static AuditScopeOptionsBuilder Create(string key = null) => new() { key = key };

    /// <summary>Creates a builder from a type/member identity.</summary>
    /// <example><code>var builder = AuditScopeOptionsBuilder.Create&lt;Customer&gt;("Rename");</code></example>
    public static AuditScopeOptionsBuilder Create<T>(string operation = null) => Create(typeof(T), operation);

    /// <summary>Creates a runtime type/member builder.</summary>
    /// <example><code>var builder = AuditScopeOptionsBuilder.Create(typeof(Customer), "Rename");</code></example>
    public static AuditScopeOptionsBuilder Create(Type type, string operation = null) => new AuditScopeOptionsBuilder().For(type, operation);

    /// <summary>Supplies typed naming context.</summary>
    /// <example><code>builder.For&lt;Customer&gt;("Rename");</code></example>
    public AuditScopeOptionsBuilder For<T>(string operation = null) => this.For(typeof(T), operation);

    /// <summary>Supplies runtime naming context.</summary>
    /// <example><code>builder.For(typeof(Customer), "Rename");</code></example>
    public AuditScopeOptionsBuilder For(Type type, string operation = null) { this.contextType = type ?? throw new ArgumentNullException(nameof(type)); this.operation = operation; return this; }

    /// <summary>Overrides the derived stable key.</summary>
    /// <example><code>builder.Key("customers.rename");</code></example>
    public AuditScopeOptionsBuilder Key(string value) { this.key = value; return this; }

    /// <summary>Overrides a derived label when nonempty.</summary>
    /// <example><code>builder.DisplayName("Rename customer");</code></example>
    public AuditScopeOptionsBuilder DisplayName(string value) { this.displayName = value; return this; }

    /// <summary>Selects a bounded kind, defaulting to Business.</summary>
    /// <example><code>builder.Kind("Service");</code></example>
    public AuditScopeOptionsBuilder Kind(string value) { this.kind = value; return this; }

    /// <summary>Selects join-or-start or independent execution ownership.</summary>
    /// <example><code>builder.Mode(AuditScopeMode.RequiresNew);</code></example>
    public AuditScopeOptionsBuilder Mode(AuditScopeMode value) { this.mode = value; return this; }

    /// <summary>Validates shared defaults and copies a descriptor snapshot.</summary>
    /// <example><code>var options = builder.Build();</code></example>
    public AuditScopeOptions Build()
    {
        var naming = AuditNamingConvention.Default;
        var key = this.key ?? this.described?.Key ?? (this.contextType is null ? null : naming.GetKey(this.contextType, this.operation));
        var label = string.IsNullOrWhiteSpace(this.displayName) ? this.described?.DisplayName ?? (this.contextType is null ? AuditNamingConvention.Humanize(key) : naming.GetDisplayName(this.contextType, this.operation)) : this.displayName;
        return new(new AuditDescriptor(key, label, this.described?.Reason, this.described?.Resources, this.described?.Details,
            contextType: this.described?.ContextType ?? this.contextType, operation: this.operation,
            keyDerived: this.key is null && (this.described?.KeyDerived ?? true),
            displayNameDerived: string.IsNullOrWhiteSpace(this.displayName) && (this.described?.DisplayNameDerived ?? true)), this.kind, this.mode);
    }
}
