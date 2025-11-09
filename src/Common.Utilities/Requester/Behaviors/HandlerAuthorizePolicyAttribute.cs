// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Declares one or more authorization policies that must all succeed
/// before the annotated request/handler is executed.
/// </summary>
/// <remarks>
/// - Apply to request or handler classes in the application layer.
/// - AllowMultiple = false; provide multiple policies via constructor params.
/// - Semantics: AND across all provided policies (each must succeed).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class HandlerAuthorizePolicyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerAuthorizePolicyAttribute"/> class.
    /// </summary>
    /// <param name="policies">
    /// One or more policy names to require (all must succeed).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when no policies are provided or all provided values are empty/whitespace.
    /// </exception>
    public HandlerAuthorizePolicyAttribute(params string[] policies)
    {
        if (policies is null || policies.Length == 0)
            throw new ArgumentException("At least one policy must be provided.", nameof(policies));

        this.Policies = policies
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (this.Policies.Length == 0)
            throw new ArgumentException("At least one non-empty policy must be provided.", nameof(policies));
    }

    /// <summary>
    /// Gets the normalized list of policy names to evaluate (AND semantics).
    /// </summary>
    public string[] Policies { get; }
}
