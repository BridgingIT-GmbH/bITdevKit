// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class Domain
    {
        /// <summary>Creates a <see cref="BusinessRuleError"/> for general business rule violations.</summary>
        public static BusinessRuleError BusinessRule(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="StateTransitionError"/> for invalid state transitions.</summary>
        public static StateTransitionError StateTransition(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DependencyError"/> for missing or invalid dependencies.</summary>
        public static DependencyError Dependency(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="PreconditionError"/> when operation preconditions are not met.</summary>
        public static PreconditionError Precondition(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="InvalidOperationError"/> for operations invalid in current context.</summary>
        public static InvalidOperationError InvalidOperation(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="ConflictError"/> for conflicting resources or operations.</summary>
        public static ConflictError Conflict(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DomainPolicyError"/> for domain policy violations.</summary>
        public static DomainPolicyError DomainPolicy(IEnumerable<string> messages = null)
            => new(messages);

        /// <summary>Creates an <see cref="EntityNotFoundError"/> when an entity cannot be found.</summary>
        public static EntityNotFoundError EntityNotFound(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DomainError"/> for general domain or business logic errors.</summary>
        public static DomainError Error(string message = null)
            => new(message);
    }
}

/// <summary>
/// Represents a general domain or business logic error.
/// </summary>
/// <param name="message">The error message that describes the domain error. If null, a default message is used.</param>
public class DomainError(string message = null) : ResultErrorBase(message ?? "Domain error")
{
    public DomainError() : this(null)
    {
    }
}