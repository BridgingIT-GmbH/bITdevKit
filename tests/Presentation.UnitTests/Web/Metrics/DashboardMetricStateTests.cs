// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Metrics;

using BridgingIT.DevKit.Presentation.Web.Metrics.Dashboard.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

/// <summary>Verifies that missing observations are not presented as successful zero measurements.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardMetricStateTests</code></example>
public class DashboardMetricStateTests
{
    /// <summary>An unregistered metrics source produces an unavailable state without fabricated counters.</summary>
    /// <example><code>await suite.Content_MissingServices_RendersUnavailableWithoutZeroSummary();</code></example>
    [Fact]
    public async Task Content_MissingServices_RendersUnavailableWithoutZeroSummary()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = body;

        await Results.RazorSlice<Content>().ExecuteAsync(context);

        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();
        html.ShouldContain("Metrics unavailable");
        html.ShouldNotContain(">App Success</div>");
        html.ShouldNotContain("id=\"metrics-app\"");
    }
}
