namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public sealed class DashboardPageFilteringTests
{
    [Fact]
    public void GetDashboardPages_WithDisabledPageKey_HidesMatchingPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder()
            .DisablePages("metrics")
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<IDashboardPageProvider>(new TestDashboardPageProvider());

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        // Act
        var pages = context.GetDashboardPages();

        // Assert
        pages.Select(page => page.Key).ShouldBe(["health"]);
    }

    [Fact]
    public void DisablePages_WithDisabledFalse_DoesNotHideMatchingPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder()
            .DisablePages("metrics", false)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<IDashboardPageProvider>(new TestDashboardPageProvider());

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        // Act
        var pages = context.GetDashboardPages();

        // Assert
        pages.Select(page => page.Key).ShouldBe(["health", "metrics"]);
    }

    [Fact]
    public void GetDashboardPages_WithDisabledPageSetKey_HidesProjectSpecificPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder()
            .DisablePages("city-management")
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<IDashboardPageProvider>(new TestProjectDashboard(options));

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        // Act
        var pages = context.GetDashboardPages();

        // Assert
        pages.Select(page => page.Key).ShouldBe(["core-overview"]);
    }

    private sealed class TestDashboardPageProvider : IDashboardPageProvider
    {
        public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
        {
            yield return new DashboardPage("health", "Health", "heart-pulse", "/_bdk/dashboard/health");
            yield return new DashboardPage("metrics", "Metrics", "people", "/_bdk/dashboard/metrics");
        }
    }

    private sealed class TestProjectDashboard(DashboardEndpointsOptions options) : DashboardPageSet(options)
    {
        protected override void Configure(DashboardPageSetBuilder pages)
        {
            pages.Group("Core", 100)
                .Page("core-overview", "/app/core")
                    .Title("Overview")
                    .Icon("cloud-sun")
                .Page("city-management", "/app/core/cities")
                    .Title("City Management")
                    .Icon("building-add");
        }
    }
}
