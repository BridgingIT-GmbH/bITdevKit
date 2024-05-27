// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Security.Cryptography;
using System;
using BridgingIT.DevKit.Common;

public class FakeCurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly List<User> Users =
    [
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly
        new User("john.doe@example.com", "John Doe", ["Admin"]),
        new User("mary.jane@example.com", "Mary Jane", ["User"]),
        new User("eva.woods@example.com", "Eva Woods", ["User"]),
        new User("david.daniels@example.com", "David Daniels", ["Admin"]),
        new User("frank.hill@example.com", "Frank Hill", ["User"]),
        new User("grace.lee@example.com", "Grace Lee", ["User"]),
        new User("hannah.miller@example.com", "Hannah Miller", ["User"]),
        new User("ivy.johnson@example.com", "Ivy Johnson", ["Admin"]),
        new User("jack.smith@example.com", "Jack Smith", ["User"])
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
    ];

    private static readonly Dictionary<string, User> UserStore =[];
    private static readonly Random Random = new();

    static FakeCurrentUserAccessor()
    {
        foreach (var user in Users)
        {
            var userId = GenerateDeterministicGuid(user.Email).ToString();

            user.Id = userId; // Assign ID to user
            UserStore[userId] = user;
        }
    }

    public FakeCurrentUserAccessor()
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

    private static KeyValuePair<string, User> GetRandomUser()
    {
        var index = Random.Next(UserStore.Count);
        return UserStore.ElementAt(index);
    }

    private static Guid GenerateDeterministicGuid(string input) =>
        new Guid(MD5.HashData(Encoding.UTF8.GetBytes(input)));

    public class User(string email, string name, string[] roles)
    {
        public string Id { get; set; }

        public string Email { get; } = email;

        public string Name { get; } = name;

        public string[] Roles { get; } = roles;
    }
}
