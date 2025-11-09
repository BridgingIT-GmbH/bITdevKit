// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Common;
using System.Security.Claims;

public class FakeRandomCurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly Dictionary<string, FakeUser> UserStore = [];
    private static readonly Random Random = new();

    static FakeRandomCurrentUserAccessor()
    {
        foreach (var user in FakeUsers.Starwars)
        {
            UserStore[user.Id] = user;
        }
    }

    public FakeRandomCurrentUserAccessor()
    {
        var user = GetRandomUser();
        this.UserId = user.Key;
        this.UserName = user.Value.Name;
        this.Email = user.Value.Email;
        this.Roles = user.Value.Roles;
        this.Principal = CreatePrincipal(user.Value);
    }

    public ClaimsPrincipal Principal { get; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(this.UserId);

    public string UserId { get; }

    public string UserName { get; }

    public string Email { get; }

    public string[] Roles { get; }

    private static KeyValuePair<string, FakeUser> GetRandomUser()
    {
        var index = Random.Next(UserStore.Count);

        return UserStore.ElementAt(index);
    }

    private static ClaimsPrincipal CreatePrincipal(FakeUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email)
        };

        if (user.Roles is not null)
        {
            claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        if (user.Claims is not null)
        {
            claims.AddRange(user.Claims.Select(c => new Claim(c.Key, c.Value)));
        }

        var identity = new ClaimsIdentity(claims, "Fake");

        return new ClaimsPrincipal(identity);
    }
}