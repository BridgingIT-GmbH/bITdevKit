// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Represents a deterministic, in-memory user definition for development and demonstration authentication.
/// </summary>
/// <param name="email">The email address used both for sign-in data and deterministic identifier generation.</param>
/// <param name="name">The user's display name.</param>
/// <param name="roles">The optional role names assigned to the user.</param>
/// <param name="password">The optional development password.</param>
/// <param name="claims">Optional claim values exposed as read-only user metadata.</param>
/// <param name="isDefault">Whether this user should be treated as the default fake identity.</param>
[DebuggerDisplay("Id={Id}, Email={Email}")]
public class FakeUser(string email, string name, string[] roles = null, string password = null, Dictionary<string, string> claims = null, bool isDefault = false)
{
    /// <summary>Gets the stable lowercase GUID identifier derived from the email address.</summary>
    public string Id { get; } = GenerateDeterministicGuid(email);

    /// <summary>Gets a value indicating that the fake user is enabled.</summary>
    public bool IsEnabled { get; } = true;

    /// <summary>Gets a value indicating whether this is the default fake identity.</summary>
    public bool IsDefault { get; } = isDefault;

    /// <summary>Gets the email address supplied when the user was defined.</summary>
    public string Email { get; } = email;

    /// <summary>Gets the display name supplied when the user was defined.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the optional development password.</summary>
    public string Password { get; } = password;

    /// <summary>Gets the optional role names assigned to the user.</summary>
    public string[] Roles { get; } = roles;

    /// <summary>Gets the user claims, or an empty dictionary when none were supplied.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; } = claims ?? [];

    //private const string DefaultAvatar = """
    //    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
    //        <circle cx="12" cy="8" r="5"/>
    //        <path d="M3 21v-2a7 7 0 0 1 7-7h4a7 7 0 0 1 7 7v2"/>
    //    </svg>
    //    """;

    private static string GenerateDeterministicGuid(string value
)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash).ToString("N");
    }
}
