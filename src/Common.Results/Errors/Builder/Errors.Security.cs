// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class Security
    {
        /// <summary>Creates an <see cref="UnauthorizedError"/> for authentication failures (401).</summary>
        public static UnauthorizedError Unauthorized(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="ForbiddenError"/> for authorization failures (403).</summary>
        public static ForbiddenError Forbidden(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="AccessDeniedError"/> for resource-specific access denial.</summary>
        public static AccessDeniedError AccessDenied(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="InsufficientPermissionsError"/> for missing specific permissions.</summary>
        public static InsufficientPermissionsError InsufficientPermissions(string message = null, string requiredPermission = null)
            => new(message, requiredPermission);

        /// <summary>Creates a <see cref="DecryptionError"/> for decryption failures.</summary>
        public static DecryptionError Decryption(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="SecurityError"/> for general authentication and authorization errors.</summary>
        public static SecurityError Error(string message = null)
            => new(message);
    }
}

/// <summary>
/// Represents a general authentication or authorization error.
/// </summary>
/// <param name="message">The error message that describes the security error. If null, a default message is used.</param>
public class SecurityError(string message = null) : ResultErrorBase(message ?? "Security error")
{
    public SecurityError() : this(null)
    {
    }
}