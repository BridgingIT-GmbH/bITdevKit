// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies independently scoped broker overview and message-list routes.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~BrokerMessageViewTests</code></example>
public class BrokerMessageViewTests
{
    /// <summary>Overview skips retained-message searches, while Messages and its fragment retain bounded filter scope.</summary>
    [Theory]
    [InlineData("messaging", "", false)]
    [InlineData("queueing", "", false)]
    [InlineData("messaging", "/messages", true)]
    [InlineData("queueing", "/messages", true)]
    [InlineData("messaging", "/content", true)]
    [InlineData("queueing", "/content", true)]
    public async Task Views_OnlyMessagesSearchesRetainedItems(string feature, string route, bool messages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops")
            .WithPluginAssemblyContaining<Presentation.Web.Messaging.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<Presentation.Web.Queueing.Dashboard.DashboardEndpoints>());
        FilterModel captured = null;
        bool? statsArchive = null;
        if (feature == "messaging")
        {
            var service = Substitute.For<IMessageBrokerService>();
            service.GetMessageStatsAsync().ReturnsForAnyArgs(call =>
            {
                statsArchive = call.ArgAt<bool?>(2);
                return new BrokerMessageStats();
            });
            service.GetSubscriptionsAsync().ReturnsForAnyArgs(Array.Empty<BrokerMessageSubscriptionInfo>());
            service.GetWaitingMessagesAsync().ReturnsForAnyArgs(Array.Empty<BrokerMessageInfo>());
            service.SearchMessagesAsync(Arg.Any<FilterModel>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                captured = call.Arg<FilterModel>();
                return ResultPaged<BrokerMessageInfo>.Success([], 0, 1, 11);
            });
            builder.Services.AddSingleton(service);
        }
        else
        {
            var service = Substitute.For<IQueueBrokerService>();
            service.GetMessageStatsAsync().ReturnsForAnyArgs(call =>
            {
                statsArchive = call.ArgAt<bool?>(2);
                return new QueueMessageStats();
            });
            service.GetSubscriptionsAsync().ReturnsForAnyArgs(Array.Empty<QueueSubscriptionInfo>());
            service.GetWaitingMessagesAsync().ReturnsForAnyArgs(Array.Empty<QueueMessageInfo>());
            service.SearchMessagesAsync(Arg.Any<FilterModel>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                captured = call.Arg<FilterModel>();
                return ResultPaged<QueueMessageInfo>.Success([], 0, 1, 11);
            });
            builder.Services.AddSingleton(service);
        }

        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var cursor = DashboardHistoryCursor.Encode(DateTimeOffset.UtcNow, Guid.Parse("00000010-0000-0000-0000-000000000000"));
        var boundary = DashboardHistoryCursor.Encode(DateTimeOffset.UtcNow, Guid.Parse("00000020-0000-0000-0000-000000000000"));

        var html = await app.GetTestClient().GetStringAsync($"/ops/{feature}{route}?view=messages&type=Invoice&status=Failed&archive=archived&take=10&cursor={cursor}&boundary={boundary}");

        html.ShouldNotContain("data-dashboard-query-error>");
        if (messages)
        {
            statsArchive.ShouldBe(true);
            captured.ShouldNotBeNull();
            captured.Page.ShouldBe(1);
            captured.PageSize.ShouldBe(11);
            captured.Filters.ShouldContain(filter => filter.Field == "Type" && filter.Operator == FilterOperator.Contains && (string)filter.Value == "Invoice");
            captured.Filters.ShouldContain(filter => filter.Field == "Status" && filter.Value.ToString() == "Failed");
            captured.Orderings.Select(order => order.Field).ShouldBe(new[] { "CreatedDate", "Id" });
            captured.Filters.Count(filter => filter.Filters.Count > 0).ShouldBe(2);
            html.ShouldContain("class=\"dashboard-list-footer\"");
            html.ShouldContain("0 rows loaded");
            html.ShouldNotContain("<span>Subscriptions</span>");
            if (route == "/messages") html.ShouldContain("class=\"dashboard-filter-panel\"");
        }
        else
        {
            statsArchive.ShouldBe(false);
            captured.ShouldBeNull();
            html.ShouldContain("<span>Subscriptions</span>");
            html.ShouldNotContain("class=\"dashboard-list-footer\"");
            html.ShouldNotContain("class=\"dashboard-filter-panel\"");
        }
    }
}
