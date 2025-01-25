// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions.Web;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class FakeAuthenticationTests
{
    private readonly FakeUser[] testUsers =
    [
        new("test@example.com", "Test User", ["Admin"], isDefault: true),
        new("other@example.com", "Other User", ["User"])
    ];

    private FakeAuthenticationHandler CreateHandler(FakeAuthenticationOptions options = null)
    {
        var schemeOptions = new OptionsMonitor<AuthenticationSchemeOptions>(
        new OptionsFactory<AuthenticationSchemeOptions>([], []), [], new OptionsCache<AuthenticationSchemeOptions>());
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());

        return new FakeAuthenticationHandler(
            schemeOptions,
            loggerFactory,
            UrlEncoder.Default,
            options);
    }

    [Fact]
    public async Task Handler_WithValidAuthorizationHeader_ShouldAuthenticateUser()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "FakeUser test@example.com";

        var options = new FakeAuthenticationOptionsBuilder()
            .WithUsers(this.testUsers)
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        context.User = result.Principal;
        context.User.ShouldNotBeNull();
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task Handler_WithNoHeader_ShouldUseDefaultUser()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var options = new FakeAuthenticationOptionsBuilder()
            .WithUsers(this.testUsers)
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        context.User = result.Principal;
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
    }

    [Fact]
    public async Task Handler_WithInvalidHeader_ShouldFail()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Invalid header";

        var options = new FakeAuthenticationOptionsBuilder()
            .WithUsers(this.testUsers)
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handler_WithUnknownUser_ShouldFail()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "FakeUser unknown@example.com";

        var options = new FakeAuthenticationOptionsBuilder()
            .WithUsers(this.testUsers)
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task Handler_WithAdditionalClaims_ShouldIncludeThemInTicket()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "FakeUser test@example.com";

        var options = new FakeAuthenticationOptionsBuilder()
            .WithUsers(this.testUsers)
            .AddClaim("tenant", "test")
            .WithClaims(("culture", "en-US"))
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(
            new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)),
            context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        context.User = result.Principal;
        context.User.Claims.ShouldContain(c => c.Type == "tenant" && c.Value == "test");
        context.User.Claims.ShouldContain(c => c.Type == "culture" && c.Value == "en-US");
    }

    [Fact]
    public void Builder_WithMultipleDefaultUsers_ShouldThrowException()
    {
        // Arrange
        var users = new[]
        {
            new FakeUser("test1@example.com", "Test 1", ["Admin"], isDefault: true),
            new FakeUser("test2@example.com", "Test 2", ["User"], isDefault: true)
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new FakeAuthenticationOptionsBuilder()
                .WithUsers(users)
                .Build());
    }

    [Fact]
    public void Builder_WithNoUsers_ShouldThrowException()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new FakeAuthenticationOptionsBuilder()
                .Build());
    }

    [Fact]
    public void Builder_WithSingleUser_ShouldConfigureCorrectly()
    {
        // Arrange & Act
        var options = new FakeAuthenticationOptionsBuilder()
            .AddUser("test@example.com", "Test User", ["Admin"], isDefault: true)
            .Build();

        // Assert
        options.Users.Count.ShouldBe(1);
        var user = options.Users.First();
        user.Email.ShouldBe("test@example.com");
        user.Name.ShouldBe("Test User");
        user.Roles.ShouldContain("Admin");
        user.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Builder_WithMultipleUsersAndClaims_ShouldConfigureCorrectly()
    {
        // Arrange & Act
        var options = new FakeAuthenticationOptionsBuilder()
            .AddUser("admin@example.com", "Admin User", ["Admin"], isDefault: true)
            .AddUser("user@example.com", "Normal User", ["User"])
            .AddClaim("tenant", "test")
            .AddClaim("environment", "dev")
            .Build();

        // Assert
        options.Users.Count.ShouldBe(2);
        options.Claims.Count.ShouldBe(2);
        options.Claims.ShouldContain(c => c.Type == "tenant" && c.Value == "test");
        options.Claims.ShouldContain(c => c.Type == "environment" && c.Value == "dev");
    }

    [Fact]
    public async Task Handler_WithMultipleRoles_ShouldAuthenticateCorrectly()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "FakeUser admin@example.com";

        var options = new FakeAuthenticationOptionsBuilder()
            .AddUser("admin@example.com", "Admin", ["Admin", "User", "Manager"])
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        context.User = result.Principal;
        context.User.Claims.Count(c => c.Type == ClaimTypes.Role).ShouldBe(3);
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Manager");
    }

    [Fact]
    public async Task Handler_WithComplexClaimsCombination_ShouldAuthenticateCorrectly()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "FakeUser test@example.com";

        var options = new FakeAuthenticationOptionsBuilder()
            .AddUser("test@example.com", "Test User", ["Admin"])
            .AddClaim("tenant", "test")
            .AddClaim("environment", "dev")
            .WithClaims(
                ("region", "eu-west"),
                ("features", "beta"),
                ("features", "premium"))  // Multiple claims with same type
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        context.User = result.Principal;
        context.User.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        context.User.Claims.ShouldContain(c => c.Type == "tenant" && c.Value == "test");
        context.User.Claims.ShouldContain(c => c.Type == "environment" && c.Value == "dev");
        context.User.Claims.ShouldContain(c => c.Type == "region" && c.Value == "eu-west");
        context.User.Claims.Count(c => c.Type == "features").ShouldBe(2);
    }

    [Fact]
    public void Builder_ChainedConfiguration_ShouldConfigureCorrectly()
    {
        // Arrange & Act
        var options = new FakeAuthenticationOptionsBuilder()
            .WithUsers(this.testUsers)
            .AddUser("extra@example.com", "Extra User", ["Guest"])
            .AddClaim("tenant", "test")
            .WithClaims(("region", "eu-west"))
            .AddClaim("environment", "dev")
            .Build();

        // Assert
        options.Users.Count.ShouldBe(3);
        options.Claims.Count.ShouldBe(3);
        options.Users.ShouldContain(u => u.Email == "extra@example.com");
        options.Claims.ShouldContain(c => c.Type == "tenant" && c.Value == "test");
        options.Claims.ShouldContain(c => c.Type == "region" && c.Value == "eu-west");
        options.Claims.ShouldContain(c => c.Type == "environment" && c.Value == "dev");
    }

    [Fact]
    public async Task Handler_WithHierarchicalRoles_ShouldAuthenticateCorrectly()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "FakeUser admin@example.com";

        var options = new FakeAuthenticationOptionsBuilder()
            .AddUser("admin@example.com", "Super Admin", ["SuperAdmin", "Admin", "User"])
            .AddUser("manager@example.com", "Manager", ["Admin", "User"])
            .AddUser("user@example.com", "Basic User", ["User"])
            .Build();

        var handler = this.CreateHandler(options);
        await handler.InitializeAsync(new AuthenticationScheme("Fake", null, typeof(FakeAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        context.User = result.Principal;
        context.User.Claims.Count(c => c.Type == ClaimTypes.Role).ShouldBe(3);
        context.User.IsInRole("SuperAdmin").ShouldBeTrue();
        context.User.IsInRole("Admin").ShouldBeTrue();
        context.User.IsInRole("User").ShouldBeTrue();
    }
}
