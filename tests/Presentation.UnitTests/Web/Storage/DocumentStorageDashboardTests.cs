// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using DocumentDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.DashboardEndpoints;
using DocumentDashboardPageProvider = BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.DashboardPageProvider;

public sealed class DocumentStorageDashboardTests
{
    [Fact]
    public void GetPages_WhenDocumentStorageIsNotRegistered_HidesDocumentPage()
    {
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection().AddSingleton(options);
        var sut = new DocumentDashboardPageProvider(options);

        var pages = sut.GetPages(CreateHttpContext(services)).ToArray();

        pages.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPages_WhenDocumentClientIsRegistered_ExposesDocumentPageAndCard()
    {
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection().AddSingleton(options);
        services.AddDocumentStorage()
            .WithProvider<PersonStub>(_ => new InMemoryDocumentStoreProvider(), name: "default");
        var context = CreateHttpContext(services);
        var sut = new DocumentDashboardPageProvider(options);

        var page = sut.GetPages(context).Single();
        var card = await page.Card(context);

        page.Key.ShouldBe("storage.documents");
        page.Url.ShouldBe("/_bdk/dashboard/storage/documents");
        page.RememberedViews["explorer"].ShouldBe(page.Url);
        page.RememberedViews["diagnostics"].ShouldBe(page.Url + "/diagnostics");
        card.Value.ShouldBe("1");
        card.Url.ShouldBe(page.Url);
    }

    [Fact]
    public async Task Map_WithScopeValidationEnabled_DoesNotResolveScopedDocumentFactoryFromRootProvider()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(dashboardOptions);
        builder.Services.AddDocumentStorage()
            .WithProvider<PersonStub>(_ => new InMemoryDocumentStoreProvider(), name: "default");

        await using var app = builder.Build();
        var sut = new DocumentDashboardEndpoints(dashboardOptions);

        var action = () => sut.Map(app);

        action.ShouldNotThrow();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToArray();
        routes.ShouldContain(route => route.EndsWith("/storage/documents/diagnostics", StringComparison.Ordinal));
        routes.ShouldContain(route => route.EndsWith("/storage/documents/diagnostics/data", StringComparison.Ordinal));
        routes.ShouldContain(route => route.EndsWith("/storage/documents/actions/delete", StringComparison.Ordinal));
        routes.ShouldNotContain(route => route.EndsWith("/storage/documents/actions/delete-batch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("delete")]
    public async Task EditAction_WithoutOriginalEtag_ReturnsBadRequest(string action)
    {
        // Arrange
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();

        // Act
        var status = await DispatchActionAsync(action, accessor, Fields());

        // Assert
        status.ShouldBe(StatusCodes.Status400BadRequest);
        accessor.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("save")]
    [InlineData("delete")]
    public async Task EditAction_WithStaleEtag_ReturnsConflict(string action)
    {
        // Arrange
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        accessor.UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Any<DocumentWriteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure(new DocumentStoreConflictError())));
        accessor.DeleteAsync(Arg.Any<DocumentKey>(), Arg.Any<DocumentDeleteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure(new DocumentStoreConflictError())));
        var form = Fields();
        form["etag"] = "original-version";

        // Act
        var status = await DispatchActionAsync(action, accessor, form);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        if (action == "save")
            await accessor.Received(1).UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Is<DocumentWriteOptions>(options => options.IfMatchETag == "original-version"), Arg.Any<CancellationToken>());
        else
            await accessor.Received(1).DeleteAsync(Arg.Any<DocumentKey>(), Arg.Is<DocumentDeleteOptions>(options => options.IfMatchETag == "original-version"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAction_WithForceOverwrite_OmitsVersionCondition()
    {
        // Arrange
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        accessor.UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Any<DocumentWriteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        var form = Fields();
        form["etag"] = "stale-version";
        form["forceOverwrite"] = "true";

        // Act
        var status = await DispatchActionAsync("save", accessor, form);

        // Assert
        status.ShouldBe(StatusCodes.Status200OK);
        await accessor.Received(1).UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Is<DocumentWriteOptions>(options => options.IfMatchETag == null && !options.CreateOnly), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAction_CreateOnlyAndForceOverwrite_ReturnsBadRequest()
    {
        // Arrange
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        var form = Fields();
        form["mode"] = "new";
        form["forceOverwrite"] = "true";

        // Act
        var status = await DispatchActionAsync("save", accessor, form);

        // Assert
        status.ShouldBe(StatusCodes.Status400BadRequest);
        accessor.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("1e-29", 1e-29)]
    [InlineData("-1e-100", -1e-100)]
    public async Task SaveAction_WithSmallNumericProperty_PreservesValueAndExistingDateGuidInterpretation(string number, double expected)
    {
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        accessor.UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Any<DocumentWriteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        var form = Fields();
        form["etag"] = "original-version";
        form["properties"] = "{\"amount\":" + number + ",\"recordedAt\":\"2030-01-02T03:04:05Z\",\"owner\":\"019a4e77-1111-7777-9999-123456789abc\"}";

        var status = await DispatchActionAsync("save", accessor, form);

        status.ShouldBe(StatusCodes.Status200OK);
        await accessor.Received(1).UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(),
            Arg.Is<DocumentWriteOptions>(options => options.Properties.Get("amount") is double && options.Properties.Get<double>("amount") == expected
                && options.Properties.Get("recordedAt") is DateTimeOffset && options.Properties.Get("owner") is Guid), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAction_WithUnrepresentableNumericProperty_DoesNotWriteDocument()
    {
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        var form = Fields();
        form["etag"] = "original-version";
        form["properties"] = "{\"amount\":1e-400}";

        var status = await DispatchActionAsync("save", accessor, form);

        status.ShouldBe(StatusCodes.Status400BadRequest);
        accessor.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, "edit", -1)]
    [InlineData("{}", "edit", 0)]
    [InlineData("""{"approved":{"type":"boolean","value":false}}""", "new", 1)]
    public async Task SaveAction_WithOptionalDimensions_PreservesOmittedAndForwardsExplicitChanges(
        string dimensions, string mode, int expectedCount)
    {
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        DocumentWriteOptions saved = null;
        accessor.UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Do<DocumentWriteOptions>(options => saved = options),
            Arg.Any<CancellationToken>()).Returns(Result.Success());
        var form = Fields();
        form["mode"] = mode;
        if (mode == "edit") form["etag"] = "original-version";
        if (dimensions is not null) form["dimensions"] = dimensions;

        var status = await DispatchActionAsync("save", accessor, form);

        status.ShouldBe(StatusCodes.Status200OK);
        saved.CreateOnly.ShouldBe(mode == "new");
        (saved.Dimensions?.Count ?? -1).ShouldBe(expectedCount);
        if (expectedCount == 1) saved.Dimensions["approved"].Value.ShouldBe(false);
    }

    [Fact]
    public async Task SaveAction_WithDuplicateDimensionNames_DoesNotWriteDocument()
    {
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        var form = Fields();
        form["mode"] = "new";
        form["dimensions"] = """{"tag":{"type":"string","value":"one"},"Tag":{"type":"string","value":"two"}}""";

        var status = await DispatchActionAsync("save", accessor, form);

        status.ShouldBe(StatusCodes.Status400BadRequest);
        await accessor.DidNotReceive().UpsertJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Any<DocumentWriteOptions>(),
            Arg.Any<CancellationToken>());
    }

    private static Dictionary<string, string> Fields() => new()
    {
        ["clientId"] = "records", ["partitionKey"] = "records", ["rowKey"] = "42", ["mode"] = "edit", ["content"] = "{\"Name\":\"replacement\"}"
    };

    private static async Task<int> DispatchActionAsync(string action, IDocumentStoreClientAccessor accessor, Dictionary<string, string> fields)
    {
        var builder = WebApplication.CreateBuilder();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(options);
        builder.Services.AddDocumentStorage().WithProvider<PersonStub>(_ => new InMemoryDocumentStoreProvider());
        var factory = Substitute.For<IDocumentStoreClientFactory>();
        factory.Create("records").Returns(accessor);
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();
        new DocumentDashboardEndpoints(options).Map(app);
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(item => item.RoutePattern.RawText.EndsWith($"/storage/documents/actions/{action}", StringComparison.Ordinal));
        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST";
        context.Request.Path = endpoint.RoutePattern.RawText;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(fields.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)));
        using var body = new MemoryStream();
        context.Response.Body = body;
        await endpoint.RequestDelegate(context);
        return context.Response.StatusCode;
    }

    private static DefaultHttpContext CreateHttpContext(IServiceCollection services) => new()
    {
        RequestServices = services.BuildServiceProvider()
    };

    public sealed class PersonStub
    {
        public string Name { get; set; }
    }
}
