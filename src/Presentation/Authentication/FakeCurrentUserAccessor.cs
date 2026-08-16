// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Security.Claims;
using Common;

/// <summary>
/// Represents fake current user accessor.
/// </summary>
public class FakeCurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly Dictionary<string, FakeUser> UserStore = [];
    private static readonly Random Random = new();

    static FakeCurrentUserAccessor()
    {
        foreach (var user in FakeUsers.Starwars)
        {
            UserStore[user.Id] = user;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <c>FakeCurrentUserAccessor</c> class.
    /// </summary>
    /// <param name="users">The users used by the operation.</param>
    public FakeCurrentUserAccessor(FakeUser[] users)
    {
        UserStore.Clear();
        foreach (var user in users.SafeNull())
        {
            UserStore[user.Id] = user;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <c>FakeCurrentUserAccessor</c> class.
    /// </summary>
    /// <param name="userId">The user id used by the operation.</param>
    public FakeCurrentUserAccessor(string userId = null)
    {
        var user = GetUser(userId);
        this.UserId = user.Id;
        this.UserName = user.Name;
        this.Email = user.Email;
        this.Roles = user.Roles;
    }

    /// <summary>
    /// Stores the principal.
    /// </summary>
    public ClaimsPrincipal Principal
    {
        get
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, this.UserId ?? string.Empty),
                new(ClaimTypes.Name, this.UserName ?? string.Empty),
                new(ClaimTypes.Email, this.Email ?? string.Empty)
            };

            if (this.Roles != null)
            {
                claims.AddRange(this.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }

            var identity = new ClaimsIdentity(claims, "Fake");

            return new ClaimsPrincipal(identity);
        }
    }

    /// <summary>
    /// Gets the is authenticated.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(this.UserId);

    /// <summary>
    /// Gets the user id.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets the user name.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// Gets the email.
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets the roles.
    /// </summary>
    public string[] Roles { get; }

    private static FakeUser GetUser(string userId = null)
    {
        if (!userId.IsNullOrEmpty() && UserStore.TryGetValue(userId, out var value)) // try to find by id
        {
            return value;
        }

        if (!userId.IsNullOrEmpty()) // try to find by email
        {
            var user = UserStore.FirstOrDefault(e => e.Value.Email.SafeEquals(userId)).Value;
            if (user != null)
            {
                return user;
            }
        }

        return UserStore.FirstOrDefault(e => e.Value.IsDefault).Value; // return default user if no match
    }
}
