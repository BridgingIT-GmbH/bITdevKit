// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Common;

public class FakeRandomCurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly Dictionary<string, FakeUser> UserStore = [];
    private static readonly Random Random = new();

    static FakeRandomCurrentUserAccessor()
    {
        foreach (var user in Fakes.Users)
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
    }

    public string UserId { get; }

    public string UserName { get; }

    public string Email { get; }

    public string[] Roles { get; }

    private static KeyValuePair<string, FakeUser> GetRandomUser()
    {
        var index = Random.Next(UserStore.Count);

        return UserStore.ElementAt(index);
    }
}