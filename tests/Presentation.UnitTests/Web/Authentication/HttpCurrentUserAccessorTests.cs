// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Presentation.Web;

public class HttpCurrentUserAccessorTests
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly HttpCurrentUserAccessor sut;
    private readonly Faker faker;

    public HttpCurrentUserAccessorTests()
    {
        this.httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        this.sut = new HttpCurrentUserAccessor(this.httpContextAccessor);
        this.faker = new Faker();
    }

    [Fact]
    public void UserId_WhenUserHasNameIdentifierClaim_ReturnsClaimValue()
    {
        // Arrange
        var expectedUserId = this.faker.Random.Guid()
            .ToString();
        this.SetupHttpContext(ClaimTypes.NameIdentifier, expectedUserId);

        // Act
        var result = this.sut.UserId;

        // Assert
        result.ShouldBe(expectedUserId);
    }

    [Fact]
    public void UserName_WhenUserHasNameClaim_ReturnsClaimValue()
    {
        // Arrange
        var expectedUserName = this.faker.Internet.UserName();
        this.SetupHttpContext(ClaimTypes.Name, expectedUserName);

        // Act
        var result = this.sut.UserName;

        // Assert
        result.ShouldBe(expectedUserName);
    }

    [Fact]
    public void Email_WhenUserHasEmailClaim_ReturnsClaimValue()
    {
        // Arrange
        var expectedEmail = this.faker.Internet.Email();
        this.SetupHttpContext(ClaimTypes.Email, expectedEmail);

        // Act
        var result = this.sut.Email;

        // Assert
        result.ShouldBe(expectedEmail);
    }

    [Fact]
    public void Roles_WhenUserHasRoleClaims_ReturnsArrayOfRoles()
    {
        // Arrange
        var expectedRoles = new string[] { Role.Administrators, Role.Users, Role.Writers };
        this.SetupHttpContextWithRoleClaims(expectedRoles);

        // Act
        var result = this.sut.Roles;

        // Assert
        result.ShouldBe(expectedRoles);
    }

    [Fact]
    public void UserId_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        this.httpContextAccessor.HttpContext.Returns((HttpContext)null);

        // Act
        var result = this.sut.UserId;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void UserName_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        this.httpContextAccessor.HttpContext.Returns((HttpContext)null);

        // Act
        var result = this.sut.UserName;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Email_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        this.httpContextAccessor.HttpContext.Returns((HttpContext)null);

        // Act
        var result = this.sut.Email;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Roles_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        this.httpContextAccessor.HttpContext.Returns((HttpContext)null);

        // Act
        var result = this.sut.Roles;

        // Assert
        result.ShouldBeNull();
    }

    private void SetupHttpContext(string claimType, string claimValue)
    {
        var claims = new[] { new Claim(claimType, claimValue) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);

        this.httpContextAccessor.HttpContext.Returns(httpContext);
    }

    private void SetupHttpContextWithRoleClaims(string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);

        this.httpContextAccessor.HttpContext.Returns(httpContext);
    }
}