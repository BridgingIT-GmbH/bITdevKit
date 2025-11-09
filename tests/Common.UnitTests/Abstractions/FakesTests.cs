// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

public class FakesTests(ITestOutputHelper output)
{
    [Fact]
    public void Fakes_PrintUserIdsAndEmails()
    {
        foreach (var user in FakeUsers.Starwars)
        {
            output.WriteLine($"Id: {user.Id}, Email: {user.Email}");
        }
    }

    [Fact]
    public void Fakes_ValidateUserIdsAndEmails()
    {
        foreach (var user in FakeUsers.Starwars)
        {
            Assert.False(string.IsNullOrEmpty(user.Id), $"User ID should not be null or empty for {user.Email}");
            Assert.False(string.IsNullOrEmpty(user.Email), $"User Email should not be null or empty for {user.Id}");
        }
    }
}