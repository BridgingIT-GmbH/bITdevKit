// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

[DebuggerDisplay("Id={Id}, Email={Email}")]
public class FakeUser(string email, string name, string[] roles = null, string password = null, Dictionary<string, string> claims = null, bool isDefault = false)
{
    public string Id { get; } = GenerateDeterministicGuid(email);

    public bool IsEnabled { get; } = true;

    public bool IsDefault { get; } = isDefault;

    public string Email { get; } = email;

    public string Name { get; } = name;

    public string Password { get; } = password;

    public string[] Roles { get; } = roles;

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