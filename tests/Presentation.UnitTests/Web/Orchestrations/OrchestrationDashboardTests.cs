// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies the definition catalog and independently scoped orchestration views.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~OrchestrationDashboardTests</code></example>
public class OrchestrationDashboardTests
{
    /// <summary>Overview shows every registered definition even when no instances exist or stale filters are present.</summary>
    [Fact]
    public async Task Overview_ShowsAllRegisteredDefinitionsWithoutQueryingInstances()
    {
        var builder = CreateBuilder();
        var registrations = new OrchestrationRegistrationStore();
        var query = Substitute.For<IOrchestrationQueryService>();
        var arguments = new[] { typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal), typeof(string), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Uri) };
        foreach (var argument in arguments)
        {
            var type = typeof(DashboardDefinition<>).MakeGenericType(argument);
            registrations.Add(type);
            builder.Services.AddTransient(type);
        }

        builder.Services.AddSingleton(registrations);
        builder.Services.AddSingleton(query);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var html = await app.GetTestClient().GetStringAsync("/ops/orchestrations?view=instances&name=excluded&status=Failed&take=1");

        foreach (var argument in arguments) html.ShouldContain($"Definition {argument.Name}");
        html.ShouldContain("data-orchestration-definitions");
        html.ShouldNotContain("id=\"orchestrations-status-filter\"");
        html.ShouldNotContain("id=\"orchestrations-realtime-graph\"");
        query.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Instances apply the same query scope on full routes and refreshed fragments; diagnostics ignores it.</summary>
    [Theory]
    [InlineData("instances", true)]
    [InlineData("content?view=instances", true)]
    [InlineData("diagnostics", false)]
    public async Task Views_KeepInstanceFiltersSeparateFromDiagnostics(string route, bool instances)
    {
        var builder = CreateBuilder();
        var query = Substitute.For<IOrchestrationQueryService>();
        FilterModel captured = null;
        OrchestrationMetricsRequest metrics = null;
        query.GetMetricsAsync(Arg.Any<OrchestrationMetricsRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            metrics = call.Arg<OrchestrationMetricsRequest>();
            return Result<OrchestrationMetricsModel>.Success(new());
        });
        query.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<FilterModel>();
            return ResultPaged<OrchestrationInstanceModel>.Success([], 0, 1, 25);
        });
        builder.Services.AddSingleton(query);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var separator = route.Contains('?') ? '&' : '?';
        var html = await app.GetTestClient().GetStringAsync($"/ops/orchestrations/{route}{separator}name=Approval&status=Failed&state=Review&take=25");

        html.ShouldNotContain("data-dashboard-query-error>");
        if (instances)
        {
            metrics.ShouldNotBeNull();
            captured.ShouldNotBeNull();
            captured.PageSize.ShouldBe(26);
            captured.Filters.ShouldContain(filter => filter.Field == "OrchestrationName" && filter.Operator == FilterOperator.Equal && (string)filter.Value == "Approval");
            captured.Filters.ShouldContain(filter => filter.Field == "Status" && (string)filter.Value == "Failed");
            captured.Filters.ShouldContain(filter => filter.Field == "CurrentState" && (string)filter.Value == "Review");
            html.ShouldNotContain("data-orchestration-definitions");
            html.ShouldNotContain("id=\"orchestrations-realtime-graph\"");
        }
        else
        {
            captured.ShouldBeNull();
            metrics.ShouldBeNull();
            html.ShouldContain("orchestrations-metric-chart-3");
            html.ShouldContain("Retained orchestration history");
            html.ShouldContain("id=\"orchestrations-realtime-graph\"");
            html.ShouldNotContain("id=\"orchestrations-status-filter\"");
        }
    }

    [Fact]
    public async Task Overview_StateMetadata_IsEncodedAndDoesNotExecuteActivitiesOrConditions()
    {
        var builder = CreateBuilder();
        var registrations = new OrchestrationRegistrationStore();
        registrations.Add(typeof(RichDashboardDefinition));
        registrations.Add(typeof(BrokenDashboardDefinition));
        builder.Services.AddSingleton(registrations);
        builder.Services.AddTransient<RichDashboardDefinition>();
        builder.Services.AddTransient<BrokenDashboardDefinition>();
        var query = Substitute.For<IOrchestrationQueryService>();
        builder.Services.AddSingleton(query);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var html = await app.GetTestClient().GetStringAsync("/ops/orchestrations");

        html.ShouldContain("Order &lt;review&gt;");
        html.ShouldContain("Reserve &lt;stock&gt;");
        html.ShouldContain("3 states");
        html.ShouldContain("Wait for Approve");
        html.ShouldContain("Transition");
        html.ShouldContain("Initial");
        html.ShouldContain("Complete");
        html.ShouldContain("Definition details could not be loaded.");
        html.ShouldContain("BrokenDashboardDefinition");
        query.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task DefinitionDiagram_CustomRoot_UsesExistingServiceWithoutManagementEndpoints()
    {
        var builder = CreateBuilder();
        var diagrams = Substitute.For<IOrchestrationDiagramService>();
        diagrams.GetDefinitionDiagramAsync("Order & review", DiagramRenderFormat.Svg, Arg.Any<CancellationToken>())
            .Returns(Result<DiagramRenderResult>.Success(DiagramRenderResult.FromText(DiagramRenderFormat.Svg, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")));
        builder.Services.AddSingleton(diagrams);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/ops/orchestrations/definitions/diagram?name=Order%20%26%20review");

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType.MediaType.ShouldBe("image/svg+xml");
        (await response.Content.ReadAsStringAsync()).ShouldContain("<svg");
        await diagrams.Received(1).GetDefinitionDiagramAsync("Order & review", DiagramRenderFormat.Svg, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DefinitionDiagram_MissingService_ReturnsUnavailable()
    {
        await using var app = CreateBuilder().Build();
        app.MapEndpoints();
        await app.StartAsync();
        var response = await app.GetTestClient().GetAsync("/ops/orchestrations/definitions/diagram?name=Unavailable");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("Order <review>")]
    [InlineData("Retired & obsolete")]
    public async Task Instances_OrchestrationSelector_UsesRegisteredNamesAndPreservesDeepLinks(string selectedName)
    {
        var builder = CreateBuilder();
        var registrations = new OrchestrationRegistrationStore();
        registrations.Add(typeof(RichDashboardDefinition));
        registrations.Add(typeof(BrokenDashboardDefinition));
        builder.Services.AddSingleton(registrations);
        builder.Services.AddTransient<RichDashboardDefinition>();
        builder.Services.AddTransient<BrokenDashboardDefinition>();
        var query = Substitute.For<IOrchestrationQueryService>();
        FilterModel captured = null;
        query.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<FilterModel>();
            return ResultPaged<OrchestrationInstanceModel>.Success([], 0, 1, 51);
        });
        query.GetMetricsAsync(Arg.Any<OrchestrationMetricsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrchestrationMetricsModel>.Success(new()));
        builder.Services.AddSingleton(query);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var html = await app.GetTestClient().GetStringAsync("/ops/orchestrations/instances?name=" + Uri.EscapeDataString(selectedName));

        var selector = System.Text.RegularExpressions.Regex.Match(html, "<select[^>]*id=\"orchestrations-name-filter\"[\\s\\S]*?</select>").Value;
        selector.ShouldContain("All orchestrations");
        selector.ShouldContain("Order &lt;review&gt;");
        selector.ShouldContain("BrokenDashboardDefinition");
        selector.ShouldContain(System.Net.WebUtility.HtmlEncode(selectedName));
        html.ShouldNotContain("<input id=\"orchestrations-name-filter\"");
        html.ShouldNotContain("data-dashboard-query-error>");
        captured.Filters.ShouldContain(filter => filter.Field == "OrchestrationName" && filter.Operator == FilterOperator.Equal && (string)filter.Value == selectedName);
        if (selectedName.StartsWith("Retired")) selector.ShouldContain("(not registered)");
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops")
            .WithPluginAssemblyContaining<Presentation.Web.Orchestrations.Dashboard.DashboardEndpoints>());
        return builder;
    }
}

internal sealed class RichDashboardDefinition : Orchestration<TestDefinitionData>
{
    public override string Name => "Order <review>";

    protected override void Define(IOrchestrationBuilder<TestDefinitionData> builder) => builder
        .State("Start", state => state
            .Activity((context, token) => throw new InvalidOperationException("Activity must not run"), name: "Reserve <stock>")
            .TransitionTo("Waiting", context => throw new InvalidOperationException("Condition must not run")))
        .State("Waiting", state => state.WaitForSignal("Approve", signal => signal.TransitionTo("Done")))
        .State("Done", state => state.Complete());
}

internal sealed class BrokenDashboardDefinition : Orchestration<TestDefinitionData>
{
    protected override void Define(IOrchestrationBuilder<TestDefinitionData> builder) => throw new InvalidOperationException("Invalid definition");
}

internal sealed class DashboardDefinition<T> : Orchestration<TestDefinitionData>
{
    public override string Name => $"Definition {typeof(T).Name}";

    protected override void Define(IOrchestrationBuilder<TestDefinitionData> builder) => builder.State("Done", state => state.Complete());
}
