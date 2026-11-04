namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public class DashboardIndexTests
{
    [Theory]
    [InlineData(null, "bdk")]
    [InlineData("unknown", "bdk")]
    [InlineData("CORE", "Core")]
    public void Groups_SelectRequestedOrFirstGroup_AndOrderCards(string requested, string expected)
    {
        var sut = new DashboardIndexModel([
            new("Last core") { Group = "Core", GroupOrder = 10, Order = 2 },
            new("Toolkit") { Group = "bdk", GroupOrder = 0 },
            new("First core") { Group = "Core", GroupOrder = 10, Order = 1 }
        ], requested);

        sut.Groups.ShouldBe(["bdk", "Core"]);
        sut.SelectedGroup.ShouldBe(expected);
        sut.Cards.Select(card => card.Title).ShouldBe(expected == "bdk" ? ["Toolkit"] : ["First core", "Last core"]);
    }

    [Fact]
    public void Groups_EmptyAndUnnamedCards_HaveUsableFallbacks()
    {
        var sut = new DashboardIndexModel([]);
        sut.Groups.ShouldBeEmpty();
        sut.Cards.ShouldBeEmpty();
        sut.SelectedGroup.ShouldBeNull();

        sut = new DashboardIndexModel([new("Unnamed"), new("Also unnamed") { Group = " " }]);
        sut.Groups.ShouldBe(["Dashboard"]);
        sut.Cards.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Routes_CustomRoot_SelectGroupAndRefreshOnlyItsCards()
    {
        const string group = "Core & Ops";
        var reads = 0;
        var provider = Substitute.For<IDashboardPageProvider>();
        provider.GetPages(Arg.Any<HttpContext>()).Returns([
            new DashboardPage("test.toolkit", "Toolkit card", "house", "/ops/toolkit") { Group = "bdk" },
            new DashboardPage("test.core", "Application card", "house", "/ops/core")
            {
                Group = group,
                Card = _ =>
                {
                    reads++;
                    return ValueTask.FromResult(new DashboardPageCard("Application card") { Group = group, Url = "/ops/core", Value = reads.ToString() });
                }
            }
        ]);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops"));
        builder.Services.AddSingleton(provider);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var client = app.GetTestClient();
        var query = "?group=" + Uri.EscapeDataString(group);

        var html = await client.GetStringAsync("/ops/" + query);

        html.ShouldContain("aria-label=\"Overview groups\"");
        html.ShouldContain("aria-current=\"page\"");
        html.ShouldContain("/ops" + query);
        html.ShouldContain("/ops/content" + query);
        html.ShouldContain(">Application card</h6>");
        html.ShouldNotContain(">Toolkit card</h6>");
        html.IndexOf("aria-label=\"Overview groups\"", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf("id=\"dashboard-index-content\"", StringComparison.Ordinal));
        reads.ShouldBe(1);

        var fragment = await client.GetStringAsync("/ops/content" + query);
        fragment.ShouldContain(">Application card</h6>");
        fragment.ShouldNotContain(">Toolkit card</h6>");
        fragment.ShouldNotContain("Overview groups");
        reads.ShouldBe(2);

        var fallback = await client.GetStringAsync("/ops/content?group=unknown");
        fallback.ShouldContain(">Toolkit card</h6>");
        fallback.ShouldNotContain(">Application card</h6>");
    }
}
