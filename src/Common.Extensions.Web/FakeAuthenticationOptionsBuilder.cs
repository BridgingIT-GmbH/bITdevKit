// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

public class FakeAuthenticationOptionsBuilder
{
    private readonly List<FakeUser> users = [];
    private readonly List<Claim> claims = [];

    /// <summary>
    /// Adds a new fake user to the configuration.
    /// </summary>
    /// <param name="email">User's email (used as unique identifier).</param>
    /// <param name="name">Display name.</param>
    /// <param name="roles">User's roles.</param>
    /// <param name="isDefault">Whether this is the default user.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddUser(
    ///     "john@example.com",
    ///     "John Doe",
    ///     new[] { "Admin", "User" },
    ///     isDefault: true);
    /// </code>
    /// </example>
    public FakeAuthenticationOptionsBuilder AddUser(
        string email,
        string name,
        string[] roles = null,
        string password = null,
        Dictionary<string, string> claims = null,
        bool isDefault = false)
    {
        this.users.Add(new FakeUser(email, name, roles, password, claims, isDefault));
        return this;
    }

    /// <summary>
    /// Adds a collection of predefined fake users.
    /// </summary>
    /// <param name="users">Collection of fake users.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.WithUsers(Fakes.Users);
    /// </code>
    /// </example>
    public FakeAuthenticationOptionsBuilder WithUsers(IEnumerable<FakeUser> users)
    {
        this.users.AddRange(users ?? throw new ArgumentNullException(nameof(users)));
        return this;
    }

    /// <summary>
    /// Adds a single claim that will be added to any authenticated user.
    /// </summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The claim value.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddClaim("tenant", "test-tenant");
    /// </code>
    /// </example>
    public FakeAuthenticationOptionsBuilder AddClaim(string type, string value)
    {
        this.claims.Add(new Claim(type, value));
        return this;
    }

    /// <summary>
    /// Adds multiple claims that will be added to any authenticated user.
    /// </summary>
    /// <param name="claims">Array of claim type and value tuples.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.WithClaims(
    ///     ("tenant", "test"),
    ///     ("culture", "en-US"));
    /// </code>
    /// </example>
    public FakeAuthenticationOptionsBuilder WithClaims(params (string Type, string Value)[] claims)
    {
        foreach (var (type, value) in claims)
        {
            this.claims.Add(new Claim(type, value));
        }
        return this;
    }

    public FakeAuthenticationOptions Build()
    {
        if (!this.users.Any())
        {
            throw new InvalidOperationException("At least one user must be configured.");
        }

        if (this.users.Count(u => u.IsDefault) > 1)
        {
            throw new InvalidOperationException("Only one user can be marked as default.");
        }

        return new FakeAuthenticationOptions
        {
            Users = this.users,
            Claims = this.claims
        };
    }
}
