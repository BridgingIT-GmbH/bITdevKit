// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Common;

public class FakeCurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly Dictionary<string, FakeUser> UserStore = [];
    private static readonly Random Random = new();

    static FakeCurrentUserAccessor()
    {
        foreach (var user in Fakes.Users)
        {
            UserStore[user.Id] = user;
        }
    }

    public FakeCurrentUserAccessor(string userId = null)
    {
        var user = GetUser(userId);
        this.UserId = user.Id;
        this.UserName = user.Name;
        this.Email = user.Email;
        this.Roles = user.Roles;
    }

    public string UserId { get; }

    public string UserName { get; }

    public string Email { get; }

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