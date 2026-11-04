// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

/// <summary>Checks empty successful storage listings retain their footer and native batch-size field.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~StorageListFooterTests</code></example>
public class StorageListFooterTests
{
    /// <summary>Permalink results retain an exhausted footer outside the native filter form.</summary>
    /// <example><code>await suite.Permalinks_EmptyPage_RetainsFooter();</code></example>
    [Fact]
    public async Task Permalinks_EmptyPage_RetainsFooter()
    {
        var service = Substitute.For<IStoragePermalinkMaintenanceService>();
        service.ListPageAsync(Arg.Any<StoragePermalinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<StoragePermalinkPage>.Success(new() { Items = [] }));
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton(service); services.AddSingleton(new DashboardEndpointsOptions());
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider }; context.Response.Body = body;

        await Results.RazorSlice<BridgingIT.DevKit.Presentation.Web.Storage.Permalinks.Dashboard.Pages.Index>().ExecuteAsync(context);

        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();
        html.ShouldContain("id=\"permalinks-load-more-button\"");
        html.ShouldContain("disabled=\"disabled\">Load more</button>");
        html.ShouldContain("form=\"permalink-filter-form\"");
        html.ShouldContain("Rows per load");
        html.ShouldContain("0 rows loaded");
    }

    /// <summary>An exhausted list keeps Load more disabled and its selector outside the filter panel.</summary>
    /// <example><code>await suite.Content_EmptyProvider_RetainsFooter(true);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Content_EmptyProvider_RetainsFooter(bool documents)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton(new DashboardEndpointsOptions());
        if (documents) services.AddDocumentStorage().WithProvider<FooterDocument>(_ => new InMemoryDocumentStoreProvider(), name: "default");
        else services.AddBlobStorage().WithInMemoryClient("default");
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.QueryString = documents ? new("?partitionKey=test&pageSize=25") : new("?container=test&prefix=test&take=25");
        context.Response.Body = body;

        if (documents) await Results.RazorSlice<BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.Pages.Content>().ExecuteAsync(context);
        else await Results.RazorSlice<BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.Pages.Content>().ExecuteAsync(context);

        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();
        var key = documents ? "documents" : "blobs";
        html.ShouldContain($"id=\"{key}-load-more-button\"");
        html.ShouldContain("disabled=\"disabled\">Load more</button>");
        html.ShouldContain("Rows per load");
        html.ShouldContain("0 rows loaded");
        html.ShouldContain($"form=\"{key}-filter-form\"");
        html.IndexOf("data-dashboard-list-footer", StringComparison.Ordinal).ShouldBeGreaterThan(html.IndexOf("</form>", StringComparison.Ordinal));
        html.ShouldNotContain($"data-{key}-command=\"{(documents ? "page" : "next")}\"");
        var errorStart = html.IndexOf("data-dashboard-query-error", StringComparison.Ordinal);
        errorStart.ShouldBe(-1, errorStart >= 0 ? html.Substring(errorStart, Math.Min(700, html.Length - errorStart)) : null);
    }

    /// <summary>Fictional payload for a registered empty document collection.</summary>
    /// <example><code>new FooterDocument { Name = "sample" };</code></example>
    public record FooterDocument
    {
        /// <summary>Gets or sets the fictional display name.</summary>
        public string Name { get; set; }
    }
}
