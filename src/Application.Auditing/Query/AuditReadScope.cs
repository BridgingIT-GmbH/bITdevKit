// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Retains an independent snapshot of trusted host constraints and their stable revision.</summary>
/// <remarks>An explicit unrestricted scope represents a successful host decision. Missing scopes and failed resolution must fail before a provider read.</remarks>
/// <example><code>var scope = new AuditReadScope(new FilterModel { Filters = [new("TenantId", FilterOperator.Equal, "tenant-42")] }, "tenant-policy-v1");</code></example>
public sealed class AuditReadScope
{
    private readonly FilterModel constraints;

    /// <summary>Validates and snapshots server-owned constraints without consulting a provider.</summary>
    /// <param name="constraints">Required filter criteria for a restricted scope, or null or an empty model for an unrestricted scope.</param>
    /// <param name="revision">A nonempty Unicode revision of at most 128 characters without control characters.</param>
    /// <param name="unrestricted">Whether the host explicitly permits reads without row constraints.</param>
    /// <exception cref="ArgumentException">The revision, constraints, or restriction state is invalid.</exception>
    /// <example><code>var scope = new AuditReadScope(new FilterModel { Filters = [new("ModuleName", FilterOperator.Equal, "Sales")] }, "sales-v2");</code></example>
    public AuditReadScope(FilterModel constraints, string revision, bool unrestricted = false)
    {
        this.Revision = AuditStructuralValidation.Required(revision, 128, nameof(revision));
        this.constraints = AuditFilterSnapshot.Copy(constraints);
        if (unrestricted)
        {
            if (HasConstraints(this.constraints))
            {
                throw new ArgumentException("An unrestricted audit scope cannot contain constraints.");
            }
        }
        else if (this.constraints?.Filters is null || !this.constraints.Filters.Any(filter => filter is not null))
        {
            throw new ArgumentException("A restricted audit scope requires filter criteria.");
        }

        this.IsUnrestricted = unrestricted;
    }

    /// <summary>Gets whether the host explicitly selected unrestricted row access.</summary>
    /// <example><code>var unrestricted = scope.IsUnrestricted;</code></example>
    public bool IsUnrestricted { get; }

    /// <summary>Gets the stable host revision used to bind effective query scope.</summary>
    /// <example><code>var revision = scope.Revision;</code></example>
    public string Revision { get; }

    /// <summary>Gets a fresh deep copy of the constraints, preserving a null unrestricted model.</summary>
    /// <example><code>var isolatedConstraints = scope.Constraints;</code></example>
    public FilterModel Constraints => AuditFilterSnapshot.Copy(this.constraints);

    /// <summary>Creates an explicit successful scope without row constraints.</summary>
    /// <param name="revision">The stable host revision, defaulting to unrestricted.</param>
    /// <returns>An unrestricted scope with null constraints.</returns>
    /// <example><code>var scope = AuditReadScope.Unrestricted("global-operator-v1");</code></example>
    public static AuditReadScope Unrestricted(string revision = "unrestricted") => new(null, revision, true);

    private static bool HasConstraints(FilterModel source) => source is not null &&
        (source.Filters?.Count > 0 || source.Orderings?.Count > 0 || source.Includes?.Count > 0 ||
        !string.IsNullOrEmpty(source.Hierarchy) || source.Page != 0 || source.PageSize != 0 ||
        !source.NoTracking || source.HierarchyMaxDepth != 5);
}