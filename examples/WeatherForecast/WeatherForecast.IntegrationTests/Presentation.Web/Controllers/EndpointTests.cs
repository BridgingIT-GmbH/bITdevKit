// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.IntegrationTests;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;
using BridgingIT.DevKit.Presentation.Web;
using DevKit.Presentation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("WeatherForecast.Presentation")]
[Module("Core")]
public class EndpointTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture = fixture.WithOutput(output);

    [Fact]
    public async Task EmailOutboxTest()
    {
        // arrang/act
        var emailService = this.fixture.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "<p>This is a test email</p>",
            IsHtml = true,
            Priority = EmailMessagePriority.Normal,
            Attachments =
            [
                new EmailAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "test.txt",
                    ContentType = "text/plain",
                    Content = System.Text.Encoding.UTF8.GetBytes("Test content"),
                    IsEmbedded = false
                }
            ]
        };

        // Act
        var result = await emailService.SendAsync(
            message, new NotificationSendOptions { SendImmediately = false }, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();

        await Task.Delay(55 * 1000); // Wait for the outbox processing to complete

        // Assert
        using var scope = this.fixture.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var storedMessage = await context.NotificationsEmails
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailMessageStatus.Sent);
        storedMessage.SentAt.ShouldNotBeNull();
        storedMessage.Subject.ShouldBe("Test Email");
        storedMessage.Body.ShouldBe("<p>This is a test email</p>");
        storedMessage.IsHtml.ShouldBeTrue();
        storedMessage.Attachments.ShouldNotBeEmpty();
        storedMessage.Attachments.First().FileName.ShouldBe("test.txt");
    }

    [Theory]
    [InlineData("api/_system/echo")]
    public async Task SystemEchoGetTest(string route)
    {
        // arrang/act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient()).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent("*echo*");
    }

    [Theory]
    [InlineData("api/_system/info")]
    public async Task SystemInfoGetTest(string route)
    {
        // Arrange & Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient()).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should()
            .Satisfy<SystemInfo>(model =>
            {
                model.ShouldNotBeNull();
                model.Runtime.ShouldNotBeNull();
                model.Runtime.Count.ShouldBeGreaterThan(0);
                model.Request.ShouldNotBeNull();
                model.Request.Count.ShouldBeGreaterThan(0);
            });
    }

    [Theory]
    [InlineData("api/core/forecasts")]
    public async Task ForecastGetAllTest(string route)
    {
        // Arrange & Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient(FakeUsers.Starwars[0])).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    [Theory]
    [InlineData("api/core/forecasts/auth")]
    public async Task ForecastGetAllWithAuthTest(string route)
    {
        // Arrange & Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient(FakeUsers.Starwars[0])).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    private async Task<HttpClient> CreateClient(FakeUser user = null)
    {
        var client = this.fixture.CreateClient();

        if (user != null)
        {
            var provider = this.fixture.Services.GetRequiredService<IFakeIdentityProvider>();
            var response = await provider.HandlePasswordGrantAsync(null, user.Email, user.Password, null);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.AccessToken);
        }

        return client;
    }
}