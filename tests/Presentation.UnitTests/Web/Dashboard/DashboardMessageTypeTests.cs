// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queueing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using MessagingData = BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard.Pages.Data;
using QueueingData = BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard.Pages.Data;

/// <summary>Checks that retained-message lists display the message type rather than its assembly name.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardMessageTypeTests</code></example>
[UnitTest("Presentation")]
public sealed class DashboardMessageTypeTests
{
    /// <summary>Preserves identifying type names and the original type token in tooltips.</summary>
    /// <example><code>await suite.Render_QualifiedMessageType_PreservesTypeName("messaging", "Northwind.Orders.OrderSubmitted, Northwind.Application");</code></example>
    [Theory]
    [InlineData("messaging", "Northwind.CustomerManagement.Application.Modules.Orders.OrderSubmittedMessage, Northwind.CustomerManagement.Application")]
    [InlineData("queueing", "Northwind.CustomerManagement.Application.Modules.Orders.OrderSubmittedMessage, Northwind.CustomerManagement.Application")]
    [InlineData("messaging", "Northwind.CustomerManagement.Application.Modules.Orders.OrderSubmittedMessage, Northwind.CustomerManagement.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
    [InlineData("queueing", "Northwind.CustomerManagement.Application.Modules.Orders.OrderSubmittedMessage, Northwind.CustomerManagement.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
    [InlineData("messaging", "Northwind.CustomerManagement.Application.Modules.Orders.OrderSubmittedMessage")]
    [InlineData("queueing", "Northwind.CustomerManagement.Application.Modules.Orders.OrderSubmittedMessage")]
    [InlineData("messaging", "OrderSubmittedMessage")]
    [InlineData("queueing", "OrderSubmittedMessage")]
    [InlineData("messaging", "OrderSubmittedMessage`1[[T, A]], Northwind.CustomerManagement.Application")]
    [InlineData("queueing", "OrderSubmittedMessage`1[[T, A]], Northwind.CustomerManagement.Application")]
    public async Task Render_QualifiedMessageType_PreservesTypeName(string feature, string type)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (feature == "messaging")
        {
            var service = Substitute.For<IMessageBrokerService>();
            var message = new BrokerMessageInfo { Id = Guid.NewGuid(), Type = type, MessageId = "message-1" };
            service.GetMessageStatsAsync().ReturnsForAnyArgs(new BrokerMessageStats());
            service.SearchMessagesAsync(Arg.Any<FilterModel>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .ReturnsForAnyArgs(ResultPaged<BrokerMessageInfo>.Success([message], 1, 1, 50));
            service.GetSubscriptionsAsync().ReturnsForAnyArgs(Array.Empty<BrokerMessageSubscriptionInfo>());
            service.GetWaitingMessagesAsync().ReturnsForAnyArgs(new[] { message });
            services.AddSingleton(service);
        }
        else
        {
            var service = Substitute.For<IQueueBrokerService>();
            var message = new QueueMessageInfo { Id = Guid.NewGuid(), Type = type, MessageId = "message-1" };
            service.GetMessageStatsAsync().ReturnsForAnyArgs(new QueueMessageStats());
            service.SearchMessagesAsync(Arg.Any<FilterModel>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
                .ReturnsForAnyArgs(ResultPaged<QueueMessageInfo>.Success([message], 1, 1, 50));
            service.GetSubscriptionsAsync().ReturnsForAnyArgs(Array.Empty<QueueSubscriptionInfo>());
            service.GetWaitingMessagesAsync().ReturnsForAnyArgs(new[] { message });
            services.AddSingleton(service);
        }

        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = $"/{feature}/content";
        context.Request.QueryString = new("?view=messages");
        context.Response.Body = body;

        if (feature == "messaging")
        {
            await Results.RazorSlice<MessagingData>().ExecuteAsync(context);
        }
        else
        {
            await Results.RazorSlice<QueueingData>().ExecuteAsync(context);
        }

        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();
        var labels = Regex.Matches(html, $"title=\"{Regex.Escape(HtmlEncoder.Default.Encode(type))}\">([^<]+)<")
            .Select(match => match.Groups[1].Value).ToArray();
        labels.Length.ShouldBe(2);
        foreach (var label in labels)
        {
            if (type.StartsWith("OrderSubmittedMessage`1", StringComparison.Ordinal))
            {
                label.ShouldBe("OrderSubmittedMessage`1[[T, A]]");
            }
            else
            {
                label.ShouldEndWith("OrderSubmittedMessage");
                label.ShouldNotContain(",");
            }
        }
    }
}
