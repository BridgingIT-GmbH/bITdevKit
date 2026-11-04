namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

/// <summary>Verifies the retained-message data contract consumed by the dialog.</summary>
public class BrokerMessageContextTests
{
    /// <summary>Handler metadata keeps the field names and values read by Processing.</summary>
    [Fact]
    public async Task Render_MessageHandlers_UsesFieldsReadByTheProcessingDialog()
    {
        // Arrange
        var processedAt = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var message = new BrokerMessageInfo
        {
            Id = Guid.NewGuid(),
            MessageId = "invoice-message",
            Type = "Commerce.InvoiceSubmitted, Commerce",
            CreatedDate = processedAt.AddMinutes(-1),
            Handlers =
            [
                new BrokerMessageHandlerInfo
                {
                    SubscriptionKey = "accounting",
                    HandlerType = "Commerce.Accounting.InvoiceHandler",
                    Status = BrokerMessageHandlerStatus.Failed,
                    AttemptCount = 3,
                    ProcessedDate = processedAt,
                    LastError = "Cannot process <invoice>"
                }
            ]
        };
        var service = Substitute.For<IMessageBrokerService>();
        service.GetMessageStatsAsync().ReturnsForAnyArgs(new BrokerMessageStats());
        service.GetWaitingMessagesAsync().ReturnsForAnyArgs(Array.Empty<BrokerMessageInfo>());
        service.SearchMessagesAsync(Arg.Any<FilterModel>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<BrokerMessageInfo>.Success([message], 1, 1, 51));
        service.GetMessageAsync(message.Id, true, Arg.Any<CancellationToken>()).Returns(message);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(service);
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/messaging/content";
        context.Request.QueryString = new("?view=messages");
        context.Response.Body = body;
        var sut = Results.RazorSlice<Data>();

        // Act
        await sut.ExecuteAsync(context);

        // Assert
        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();
        var attribute = Regex.Match(html, "data-messaging-message-context=\"([^\"]*)\"");
        attribute.Success.ShouldBeTrue();
        using var payload = JsonDocument.Parse(WebUtility.HtmlDecode(attribute.Groups[1].Value));
        var handler = payload.RootElement.GetProperty("handlers").EnumerateArray().Single();
        handler.GetProperty("subscriptionKey").GetString().ShouldBe("accounting");
        handler.GetProperty("handlerType").GetString().ShouldBe("Commerce.Accounting.InvoiceHandler");
        handler.GetProperty("status").GetString().ShouldBe("Failed");
        handler.GetProperty("attemptCount").GetInt32().ShouldBe(3);
        handler.GetProperty("processedDate").GetDateTimeOffset().ShouldBe(processedAt);
        handler.GetProperty("lastError").GetString().ShouldBe("Cannot process <invoice>");
    }
}
