// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using EntitiesDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Entities.Dashboard.DashboardEndpoints;
using EntitiesDashboardPageProvider = BridgingIT.DevKit.Presentation.Web.Entities.Dashboard.DashboardPageProvider;

[UnitTest("Presentation")]
public class EntitiesDashboardEndpointTests
{
    private const string BasePath = "/_bdk/dashboard/entities";

    private static readonly string CustomerKey = typeof(Customer).FullName;

    private static readonly Guid Id1 = EntitiesDashboardTestData.CustomerGuid1;

    private static readonly Guid Id2 = EntitiesDashboardTestData.CustomerGuid2;

    [Fact]
    public async Task GetPage_ReadOnlyUser_Returns200WithNoStoreCaching()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync(BasePath);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        (await response.Content.ReadAsStringAsync()).ShouldContain("Customer");
    }

    [Fact]
    public async Task GetPage_WithSelectedEntity_RendersRowsWithoutMaskedOrHiddenValues()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}?entity={CustomerKey}");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Ann");
        html.ShouldNotContain("secret-token");
        html.ShouldNotContain("internal");
    }

    [Fact]
    public async Task GetPage_UnknownEntity_Returns404()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}?entity=Nope.Missing");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRows_ReadOnlyUser_Returns200WithHtmlRows()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/rows?entity={CustomerKey}");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        html.ShouldContain("Ann");
        html.ShouldContain("Bob");
        html.ShouldNotContain("secret-token");
    }

    [Fact]
    public async Task GetRows_WithFilter_ReturnsOnlyMatchingRows()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/rows?entity={CustomerKey}&f=Name~eq~Bob");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Bob");
        html.ShouldNotContain("Ann");
    }

    [Fact]
    public async Task GetPage_WithSelectedEntity_RendersPageSizeSelectorAndDisabledLoadMore()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var html = await client.GetStringAsync($"{BasePath}?entity={CustomerKey}");

        // Assert
        html.ShouldContain("data-dashboard-batch-size");
        html.ShouldContain("entities-load-more-button");
        html.ShouldContain("2 rows loaded");
        System.Text.RegularExpressions.Regex.Match(html, "<button[^>]*id=\"entities-load-more-button\"[^>]*>").Value.ShouldContain("disabled");
        System.Text.RegularExpressions.Regex.Match(html, "<select[^>]*id=\"entities-page-size\"[^>]*>").Value.ShouldNotContain("disabled");
    }

    [Fact]
    public async Task GetRows_WithPageSize_LimitsTheRows()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var html = await client.GetStringAsync($"{BasePath}/rows?entity={CustomerKey}&size=1");

        // Assert
        html.ShouldContain("data-has-more=\"true\"");
    }

    [Fact]
    public async Task GetRows_UnknownEntity_Returns404()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/rows?entity=Nope.Missing");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDetail_ExistingRecord_Returns200()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/detail?entity={CustomerKey}&id={Id1}");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Ann");
        html.ShouldNotContain("secret-token");
    }

    [Fact]
    public async Task GetEditor_ReadOnlyUser_Returns403()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/editor?entity={CustomerKey}&id={Id1}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEditor_UserWithEditRights_Returns200()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");

        // Act
        var response = await client.GetAsync($"{BasePath}/editor?entity={CustomerKey}&id={Id1}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("secret-token");
    }

    [Fact]
    public async Task GetEditor_UserWithEditRights_RendersUnblockedDialog()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");

        // Act
        var response = await client.GetAsync($"{BasePath}/editor?entity={CustomerKey}&id={Id1}");

        // Assert
        (await response.Content.ReadAsStringAsync()).ShouldContain("data-blocked=\"false\"");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("update?preview=true")]
    [InlineData("delete")]
    [InlineData("bulk-delete")]
    public async Task Post_ReadOnlyUser_Returns403AndWritesNothing(string action)
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");
        var body = Body(new { entity = CustomerKey, id = Id1.ToString(), ids = new[] { Id1.ToString() }, values = new { Name = "Hacked" } });

        // Act
        var response = await client.PostAsync($"{BasePath}/{action}", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Customers(app).CountAsync()).ShouldBe(2);
        (await Customers(app).FindOneAsync(CustomerId.Create(Id1))).Name.ShouldBe("Ann");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("bulk-delete")]
    public async Task Post_AnonymousUser_Returns403(string action)
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        // Act
        var response = await client.PostAsync($"{BasePath}/{action}", Body(new { entity = CustomerKey }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_AuthenticatedUserWhenEditIsNotConfigured_Returns403()
    {
        // Arrange
        await using var app = await StartAsync(configure: o => o.EditRoles.Clear());
        using var client = app.GetTestClient().As("editor");

        // Act
        var response = await client.PostAsync($"{BasePath}/delete", Body(new { entity = CustomerKey, id = Id1.ToString() }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Customers(app).CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task PostCreate_UserWithEditRights_Returns201AndInsertsTheRecord()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");

        // Act
        var response = await client.PostAsync(
            $"{BasePath}/create", Body(new { entity = CustomerKey, values = new { Name = "Zed", Age = "7" } }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Customers(app).CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task PostUpdate_UserWithEditRights_SavesTheChange()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        var body = await UpdateBodyAsync(app, new { Name = "Anna" });

        // Act
        var response = await client.PostAsync($"{BasePath}/update", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Customers(app).FindOneAsync(CustomerId.Create(Id1))).Name.ShouldBe("Anna");
    }

    [Fact]
    public async Task PostUpdate_PreviewTrue_DoesNotWrite()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        var body = await UpdateBodyAsync(app, new { Name = "Anna" });

        // Act
        var response = await client.PostAsync($"{BasePath}/update?preview=true", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Customers(app).FindOneAsync(CustomerId.Create(Id1))).Name.ShouldBe("Ann");
    }

    [Fact]
    public async Task PostUpdate_StaleConcurrencyVersion_Returns409()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        var body = Body(new { entity = CustomerKey, id = Id1.ToString(), concurrencyVersion = Guid.NewGuid().ToString(), values = new { Name = "Late" } });

        // Act
        var response = await client.PostAsync($"{BasePath}/update", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostUpdate_InvalidValue_Returns422()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        var body = await UpdateBodyAsync(app, new { Age = "abc" });

        // Act
        var response = await client.PostAsync($"{BasePath}/update", body);

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)422);
    }

    [Fact]
    public async Task PostDelete_UserWithEditRights_RemovesTheRecord()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");

        // Act
        var response = await client.PostAsync($"{BasePath}/delete", Body(new { entity = CustomerKey, id = Id1.ToString() }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Customers(app).CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task PostBulkDelete_UserWithEditRights_RemovesTheRecords()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");

        // Act
        var response = await client.PostAsync(
            $"{BasePath}/bulk-delete", Body(new { entity = CustomerKey, ids = new[] { Id1.ToString(), Id2.ToString() } }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Customers(app).CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task PostCreate_BodyAbove256KiB_Returns413()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        var body = Body(new { entity = CustomerKey, values = new { Name = new string('x', 256 * 1024 + 1) } });

        // Act
        var response = await client.PostAsync($"{BasePath}/create", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await Customers(app).CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task PostCreate_BodyAbove256KiBWithoutContentLength_Returns413()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        var payload = JsonSerializer.Serialize(new { entity = CustomerKey, values = new { Name = new string('x', 256 * 1024 + 1) } });
        using var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(payload)));
        content.Headers.ContentType = new("application/json");
        content.Headers.ContentLength = null;

        // Act
        var response = await client.PostAsync($"{BasePath}/create", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task PostCreate_InvalidJson_Returns400()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("editor");
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"{BasePath}/create", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Map_EntitiesListedInDisabledPageKeys_DoesNotMapTheEndpoints()
    {
        // Arrange
        await using var app = await StartAsync(dashboard: new DashboardEndpointsOptionsBuilder().AllowAnonymous().DisablePages("entities"));
        using var client = app.GetTestClient().As("reader");

        // Act
        var page = await client.GetAsync(BasePath);
        var rows = await client.GetAsync($"{BasePath}/rows?entity={CustomerKey}");

        // Assert
        page.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        rows.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Map_NoEntitySourcesRegistered_DoesNotMapTheEndpoints()
    {
        // Arrange
        await using var app = await StartAsync(registerRepositories: false);
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync(BasePath);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExport_Csv_ReturnsAttachmentWithBomAndFileName()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/export?entity={CustomerKey}&format=csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileName.ShouldStartWith("Customer-");
        response.Content.Headers.ContentDisposition.FileName.ShouldEndWith(".csv");
        bytes.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
        response.Headers.GetValues("X-Entities-Export-Truncated").Single().ShouldBe("false");
        Encoding.UTF8.GetString(bytes).ShouldNotContain("secret-token");
    }

    [Fact]
    public async Task GetExport_Json_ReturnsJsonArray()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/export?entity={CustomerKey}&format=json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        json.RootElement.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task GetExport_MoreRowsThanTheLimit_SetsTheTruncationHeader()
    {
        // Arrange
        await using var app = await StartAsync(configure: o => o.MaxExportRows = 1);
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/export?entity={CustomerKey}&format=json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-Entities-Export-Truncated").Single().ShouldBe("true");
        json.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task GetExport_InvalidFormat_Returns400()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/export?entity={CustomerKey}&format=xml");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetExport_UnknownEntity_Returns404()
    {
        // Arrange
        await using var app = await StartAsync();
        using var client = app.GetTestClient().As("reader");

        // Act
        var response = await client.GetAsync($"{BasePath}/export?entity=Nope.Missing&format=csv");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void PageProvider_WithSources_ContributesTheEntitiesPage()
    {
        // Arrange
        using var provider = EntitiesDashboardTestData.CreateProvider(services: s => s.AddRepository<Customer>());
        var sut = new EntitiesDashboardPageProvider(new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build());

        // Act
        var pages = sut.GetPages(EntitiesDashboardTestData.CreateHttpContext(provider)).ToList();

        // Assert
        var page = pages.ShouldHaveSingleItem();
        page.Key.ShouldBe("entities");
        page.Title.ShouldBe("Entities");
        page.Url.ShouldBe(BasePath);
    }

    [Fact]
    public void PageProvider_WithoutSources_ContributesNoPage()
    {
        // Arrange
        using var provider = EntitiesDashboardTestData.CreateProvider(o => o.Include<Customer>());
        var sut = new EntitiesDashboardPageProvider(new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build());

        // Act
        var pages = sut.GetPages(EntitiesDashboardTestData.CreateHttpContext(provider)).ToList();

        // Assert
        pages.ShouldBeEmpty();
    }

    [Fact]
    public void PageProvider_EntitiesInDisabledPageKeys_ContributesNoPage()
    {
        // Arrange
        using var provider = EntitiesDashboardTestData.CreateProvider(services: s => s.AddRepository<Customer>());
        var sut = new EntitiesDashboardPageProvider(new DashboardEndpointsOptionsBuilder().AllowAnonymous().DisablePages("entities").Build());

        // Act
        var pages = sut.GetPages(EntitiesDashboardTestData.CreateHttpContext(provider)).ToList();

        // Assert
        pages.ShouldBeEmpty();
    }

    private static StringContent Body(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static IGenericRepository<Customer> Customers(WebApplication app) =>
        app.Services.GetRequiredService<IGenericRepository<Customer>>();

    private static async Task<StringContent> UpdateBodyAsync(WebApplication app, object values)
    {
        var current = await Customers(app).FindOneAsync(CustomerId.Create(Id1));

        return Body(new
        {
            entity = CustomerKey,
            id = Id1.ToString(),
            concurrencyVersion = current.ConcurrencyVersion.ToString(),
            values,
            replace = Array.Empty<string>()
        });
    }

    private static async Task<WebApplication> StartAsync(
        Action<EntitiesDashboardOptions> configure = null,
        DashboardEndpointsOptionsBuilder dashboard = null,
        bool registerRepositories = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        var options = (dashboard ?? new DashboardEndpointsOptionsBuilder().AllowAnonymous()).Build();
        builder.Services.AddSingleton(options);
        if (registerRepositories)
        {
            builder.Services.AddRepository(
                EntitiesDashboardTestData.NewCustomer(Id1, "Ann", 41),
                EntitiesDashboardTestData.NewCustomer(Id2, "Bob", 29));
        }

        builder.Services.AddEntitiesDashboard(o =>
        {
            o.EditRoles.Add("editor");
            o.Entity<Customer>(e => e.Mask(c => c.ApiToken, "***").Hide(c => c.InternalNote));
            configure?.Invoke(o);
        });

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Role", out var role))
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, role.ToString())],
                    "Test");
                context.User = new ClaimsPrincipal(identity);
            }

            await next();
        });
        new EntitiesDashboardEndpoints(options).Map(app);
        await app.StartAsync();

        return app;
    }
}

internal static class EntitiesDashboardClientExtensions
{
    public static HttpClient As(this HttpClient client, string role)
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", role);

        return client;
    }
}
