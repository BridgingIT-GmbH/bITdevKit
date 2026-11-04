namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Mcp;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class McpDashboardTabsTests
{
    [Theory]
    [InlineData("/_bdk/dashboard")]
    [InlineData("/ops")]
    public async Task Routes_McpRegistered_SeparateViewsAndRefreshWithinDashboardRoot(string root)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath(root));
        builder.Services.AddSingleton(new HostDescriptorOptions { RuntimeId = "test-runtime" });
        builder.Services.AddSingleton<LocalIpcEndpointState>();
        builder.Services.AddSingleton<McpDispatcher>();
        builder.Services.AddMcpHandler<TestHandler>();
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var sut = app.GetTestClient();

        // Act / Assert
        foreach (var view in new[] { "overview", "connections", "handlers", "operations" })
        {
            var path = root + "/mcp" + (view == "overview" ? "" : "/" + view);
            var html = await sut.GetStringAsync(path);
            html.ShouldContain($"data-dashboard-view=\"{view}\"");
            html.ShouldContain($"contentUrl: '{path}/content'");
            html.ShouldContain($"href=\"{root}/mcp/operations\"");
            var fragment = await sut.GetStringAsync(path + "/content");
            fragment.ShouldNotContain("MCP views");
            AssertView(fragment, view);
        }

        var provider = app.Services.GetServices<IDashboardPageProvider>().OfType<McpDashboardPageProvider>().Single();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        var page = provider.GetPages(context).Single();
        page.RememberedViews.Count.ShouldBe(4);
        page.RememberedViews["overview"].ShouldBe(root + "/mcp");
        page.RememberedViews["connections"].ShouldBe(root + "/mcp/connections");
        page.RememberedViews["handlers"].ShouldBe(root + "/mcp/handlers");
        page.RememberedViews["operations"].ShouldBe(root + "/mcp/operations");
    }

    [Fact]
    public async Task Routes_McpUnavailable_DoNotExposeAnyView()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var sut = app.GetTestClient();

        foreach (var suffix in new[] { "", "/connections", "/handlers", "/operations" })
        {
            (await sut.GetAsync("/_bdk/dashboard/mcp" + suffix)).StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
            (await sut.GetAsync("/_bdk/dashboard/mcp" + suffix + "/content")).StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        }
    }

    private static void AssertView(string html, string view)
    {
        html.Contains("registered types").ShouldBe(view == "overview");
        html.Contains("Host Bridge").ShouldBe(view == "connections");
        html.Contains("MCP Server Sessions").ShouldBe(view == "connections");
        html.Contains("No active <code>bdk mcp</code>").ShouldBe(view == "connections");
        html.Contains("<th>Handler</th>").ShouldBe(view is "handlers" or "operations");
        html.Contains("data-schema=").ShouldBe(view == "operations");
        html.ShouldNotContain("v@viewModel.ProtocolVersion");
    }

    private sealed class TestHandler : IMcpHandler
    {
        public IReadOnlyCollection<McpCapability> Capabilities { get; } =
            [new("test.inspect", McpToolset.Diagnostics, "test", "Inspect a test item.")];

        public ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(McpResponse.Success("Handled."));
    }
}
