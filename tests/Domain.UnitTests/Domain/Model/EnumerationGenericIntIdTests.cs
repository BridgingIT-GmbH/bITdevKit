// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using DevKit.Domain.Model;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationGenericIntIdTests
{
    [Fact]
    public void GetAll_ShouldReturnAllUserRoles()
    {
        // Arrange & Act
        var allRoles = StubUserRoles.GetAll<StubUserRoles>()
            .ToList();

        // Assert
        allRoles.Count.ShouldBe(3);
        allRoles.ShouldContain(r => r.Id == 1 && r.Value.Name == "User");
        allRoles.ShouldContain(r => r.Id == 2 && r.Value.Name == "Moderator");
        allRoles.ShouldContain(r => r.Id == 3 && r.Value.Name == "Administrator");
    }

    [Fact]
    public void FromId_WithValidId_ShouldReturnCorrectRole()
    {
        // Arrange
        const int moderatorRoleId = 2;

        // Act
        var result = StubUserRoles.FromId<StubUserRoles>(moderatorRoleId);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(moderatorRoleId);
        result.Value.Name.ShouldBe("Moderator");
    }

    [Fact]
    public void FromId_WithInvalidId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const int invalidId = 4;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
                StubUserRoles.FromId<StubUserRoles>(invalidId))
            .Message.ShouldBe($"'{invalidId}' is not a valid id for {typeof(StubUserRoles)}");
    }

    [Fact]
    public void FromValue_WithValidValue_ShouldReturnCorrectRole()
    {
        // Arrange
        var adminRole = new StubRoleDetails("Administrator", true, true);

        // Act
        var result = StubUserRoles.FromValue<StubUserRoles>(adminRole);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(3);
        result.Value.ShouldBe(adminRole);
    }

    [Fact]
    public void FromValue_WithInvalidValue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidRole = new StubRoleDetails("Invalid", false, false);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
                StubUserRoles.FromValue<StubUserRoles>(invalidRole))
            .Message.ShouldContain("is not a valid value for");
    }

    [Fact]
    public void Equals_WithSameRole_ShouldReturnTrue()
    {
        // Arrange
        var role1 = StubUserRoles.Moderator;
        var role2 = StubUserRoles.FromId<StubUserRoles>(2);

        // Act & Assert
        role1.Equals(role2)
            .ShouldBeTrue();
        (role1 == role2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentRole_ShouldReturnFalse()
    {
        // Arrange
        var userRole = StubUserRoles.User;
        var adminRole = StubUserRoles.Administrator;

        // Act & Assert
        userRole.Equals(adminRole)
            .ShouldBeFalse();
        (userRole == adminRole).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var role1 = StubUserRoles.Administrator;
        var role2 = StubUserRoles.FromId<StubUserRoles>(3);

        // Act & Assert
        role1.GetHashCode()
            .ShouldBe(role2.GetHashCode());
    }
}

public class StubUserRoles(int id, StubRoleDetails value)
    : Enumeration<StubRoleDetails>(id, value)
{
    public static StubUserRoles User = new(1, new StubRoleDetails("User", false, false));
    public static StubUserRoles Moderator = new(2, new StubRoleDetails("Moderator", true, false));
    public static StubUserRoles Administrator = new(3, new StubRoleDetails("Administrator", true, true));
}

public class StubRoleDetails(string name, bool canModerate, bool canAdminister)
    : IEquatable<StubRoleDetails>, IComparable
{
    public string Name { get; set; } = name;

    public bool CanModerate { get; set; } = canModerate;

    public bool CanAdminister { get; set; } = canAdminister;

    public bool Equals(StubRoleDetails other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.Name == other.Name &&
            this.CanModerate == other.CanModerate &&
            this.CanAdminister == other.CanAdminister;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((StubRoleDetails)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.Name, this.CanModerate, this.CanAdminister);
    }

    public int CompareTo(object obj)
    {
        if (obj == null)
        {
            return 1;
        }

        if (!(obj is StubRoleDetails other))
        {
            throw new ArgumentException("Object is not a StubRoleDetails");
        }

        return string.Compare(this.Name, other.Name, StringComparison.Ordinal);
    }
}