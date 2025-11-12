// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Declares one or more roles; the current principal must be in at least
/// one of the specified roles before the annotated request/handler executes.
/// </summary>
/// <remarks>
/// - Apply to request or handler classes in the application layer.
/// - AllowMultiple = false; provide multiple roles via constructor params.
/// - Semantics: OR across provided roles (any one role suffices).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class HandlerAuthorizeRolesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerAuthorizeRolesAttribute"/> class.
    /// </summary>
    /// <param name="roles">
    /// One or more role names to allow (the user must be in any one of them).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when no roles are provided or all provided values are empty/whitespace.
    /// </exception>
    public HandlerAuthorizeRolesAttribute(params string[] roles)
    {
        if (roles is null || roles.Length == 0)
            throw new ArgumentException("At least one role must be provided.", nameof(roles));

        this.Roles = roles
            .Select(r => r?.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (this.Roles.Length == 0)
            throw new ArgumentException("At least one non-empty role must be provided.", nameof(roles));
    }

    /// <summary>
    /// Gets the normalized list of role names (OR semantics).
    /// </summary>
    public string[] Roles { get; }
}