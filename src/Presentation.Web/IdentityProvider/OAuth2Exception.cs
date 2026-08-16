// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents o auth2 exception.
/// </summary>
/// <param name="error">The error used by the operation.</param>
/// <param name="description">The description used by the operation.</param>
public class OAuth2Exception(string error, string description) : Exception(description)
{
    /// <summary>
    /// Gets the error.
    /// </summary>
    public string Error { get; } = error;

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    /// Represents errors.
    /// </summary>
    public static class Errors
    {
        /// <summary>
        /// Defines the invalid request value.
        /// </summary>
        public const string InvalidRequest = "invalid_request";
        /// <summary>
        /// Defines the invalid client value.
        /// </summary>
        public const string InvalidClient = "invalid_client";
        /// <summary>
        /// Defines the invalid grant value.
        /// </summary>
        public const string InvalidGrant = "invalid_grant";
        /// <summary>
        /// Defines the unauthorized client value.
        /// </summary>
        public const string UnauthorizedClient = "unauthorized_client";
        /// <summary>
        /// Defines the unsupported grant type value.
        /// </summary>
        public const string UnsupportedGrantType = "unsupported_grant_type";
        /// <summary>
        /// Defines the invalid scope value.
        /// </summary>
        public const string InvalidScope = "invalid_scope";
    }
}
