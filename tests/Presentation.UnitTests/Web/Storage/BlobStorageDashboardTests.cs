// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using BlobDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardEndpoints;
using BlobDashboardPageProvider = BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardPageProvider;

public sealed class BlobStorageDashboardTests
{
    [Fact]
    public void GetPages_WhenBlobStorageIsNotRegistered_HidesBlobPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        var context = CreateHttpContext(services);
        var sut = new BlobDashboardPageProvider(options);

        // Act
        var pages = sut.GetPages(context).ToArray();

        // Assert
        pages.ShouldBeEmpty();
    }

    [Fact]
    public void GetPages_WhenBlobClientIsRegistered_ShowsBlobPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        var context = CreateHttpContext(services);
        var sut = new BlobDashboardPageProvider(options);

        // Act
        var page = sut.GetPages(context).Single();

        // Assert
        page.Key.ShouldBe("storage.blobs");
        page.Title.ShouldBe("Blobs");
        page.Url.ShouldBe("/_bdk/dashboard/storage/blobs");
        page.RememberedViews["explorer"].ShouldBe(page.Url);
        page.RememberedViews["diagnostics"].ShouldBe(page.Url + "/diagnostics");
        page.Group.ShouldBe("bdk");
        page.Card.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPages_WhenBlobClientIsRegistered_ExposesReadableIndexCard()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        var context = CreateHttpContext(services);
        var sut = new BlobDashboardPageProvider(options);
        var page = sut.GetPages(context).Single();

        // Act
        var card = await page.Card(context);

        // Assert
        card.Title.ShouldBe("Blobs");
        card.Subtitle.ShouldBe("Blob clients");
        card.Value.ShouldBe("1");
        card.Detail.ShouldBe("reports");
        card.Url.ShouldBe("/_bdk/dashboard/storage/blobs");
    }

    [Fact]
    public async Task Map_WithScopeValidationEnabled_DoesNotResolveScopedBlobFactoryFromRootProvider()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(dashboardOptions);
        builder.Services.AddBlobStorage()
            .WithInMemoryClient("reports");

        await using var app = builder.Build();
        var sut = new BlobDashboardEndpoints(dashboardOptions);

        // Act
        var action = () => sut.Map(app);

        // Assert
        action.ShouldNotThrow();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToArray();
        routes.ShouldContain(route => route.EndsWith("/storage/blobs/diagnostics", StringComparison.Ordinal));
        routes.ShouldContain(route => route.EndsWith("/storage/blobs/diagnostics/data", StringComparison.Ordinal));
        routes.ShouldContain(route => route.EndsWith("/storage/blobs/actions/upload", StringComparison.Ordinal));
        routes.ShouldContain(route => route.EndsWith("/storage/blobs/actions/delete", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("expiration")]
    [InlineData("metadata")]
    [InlineData("delete")]
    public async Task EditAction_WithoutOriginalEtag_ReturnsBadRequest(string action)
    {
        // Arrange
        var client = Substitute.For<IBlobStoreClient>();

        // Act
        var status = await DispatchActionAsync(action, client, Fields());

        // Assert
        status.ShouldBe(StatusCodes.Status400BadRequest);
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("expiration")]
    [InlineData("metadata")]
    [InlineData("delete")]
    public async Task EditAction_WithStaleEtag_ReturnsConflict(string action)
    {
        // Arrange
        var client = Substitute.For<IBlobStoreClient>();
        client.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreConflictError("The version changed."))));
        client.GetPropertiesAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<BlobInfo>.Success(new() { Key = new("records", "42.txt") })));
        client.UpdatePropertiesAsync(Arg.Any<BlobPropertiesUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreConflictError("The version changed."))));
        client.DeleteAsync(Arg.Any<BlobKey>(), Arg.Any<BlobDeleteOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure(new BlobStoreConflictError("The version changed."))));
        var form = Fields();
        form["etag"] = "original-version";

        // Act
        var status = await DispatchActionAsync(action, client, form);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        if (action == "upload")
            await client.Received(1).UploadAsync(Arg.Is<BlobUpload>(upload => upload.IfMatchETag == "original-version"), Arg.Any<CancellationToken>());
        else if (action is "expiration" or "metadata")
            await client.Received(1).UpdatePropertiesAsync(Arg.Is<BlobPropertiesUpdate>(update => update.IfMatchETag == "original-version"), Arg.Any<CancellationToken>());
        else
            await client.Received(1).DeleteAsync(Arg.Any<BlobKey>(), Arg.Is<BlobDeleteOptions>(options => options.IfMatchETag == "original-version"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAction_WithForceOverwrite_OmitsVersionCondition()
    {
        // Arrange
        var client = Substitute.For<IBlobStoreClient>();
        client.GetPropertiesAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<BlobInfo>.Success(new() { Key = new("records", "42.txt") })));
        client.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<BlobInfo>.Success(new() { Key = new("records", "42.txt") })));
        var form = Fields();
        form["etag"] = "stale-version";
        form["forceOverwrite"] = "true";

        // Act
        var status = await DispatchActionAsync("upload", client, form);

        // Assert
        status.ShouldBe(StatusCodes.Status200OK);
        await client.Received(1).UploadAsync(Arg.Is<BlobUpload>(upload => upload.IfMatchETag == null && upload.OverwriteMode == BlobOverwriteMode.Overwrite), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAction_CreateOnlyAndForceOverwrite_ReturnsBadRequest()
    {
        // Arrange
        var client = Substitute.For<IBlobStoreClient>();
        var form = Fields();
        form["mode"] = "new";
        form["forceOverwrite"] = "true";

        // Act
        var status = await DispatchActionAsync("upload", client, form);

        // Assert
        status.ShouldBe(StatusCodes.Status400BadRequest);
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadAction_ReplacingContent_PreservesCurrentPropertiesAndExpiration(bool forceOverwrite)
    {
        // Arrange
        var client = new BlobStoreClient("inmemory", new InMemoryBlobStoreProvider());
        var key = new BlobKey("records", "42.txt");
        var properties = new PropertyBag();
        properties.Set("classification", "original");
        var original = await client.UploadAsync(new BlobUpload
        {
            Key = key, Content = new MemoryStream([4, 5, 6]), Properties = properties,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        original.IsSuccess.ShouldBeTrue();
        if (forceOverwrite)
        {
            properties.Set("classification", "newer metadata");
            (await client.UpdatePropertiesAsync(new BlobPropertiesUpdate
            {
                Key = key, Properties = properties, ExpiresAt = DateTimeOffset.UtcNow.AddDays(2), IfMatchETag = original.Value.ETag
            })).IsSuccess.ShouldBeTrue();
        }

        var expected = (await client.GetPropertiesAsync(key)).Value;
        var form = Fields();
        form["etag"] = original.Value.ETag;
        form["forceOverwrite"] = forceOverwrite.ToString();
        form["expiresAt"] = string.Empty;

        // Act
        var status = await DispatchActionAsync("upload", client, form);

        // Assert
        status.ShouldBe(StatusCodes.Status200OK);
        var current = await client.GetPropertiesAsync(key);
        current.Value.ETag.ShouldNotBe(expected.ETag);
        current.Value.ExpiresAt.ShouldBe(expected.ExpiresAt);
        current.Value.Properties.Get<string>("classification").ShouldBe(expected.Properties.Get<string>("classification"));
        var download = await client.DownloadAsync(key);
        await using var content = download.Value;
        using var bytes = new MemoryStream();
        await content.Content.CopyToAsync(bytes);
        bytes.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task UploadAction_WithConcurrentMetadataChange_KeepsOriginalConditionAndRejectsReplacement()
    {
        // Arrange
        var stored = new BlobStoreClient("inmemory", new InMemoryBlobStoreProvider());
        var key = new BlobKey("records", "42.txt");
        var original = await stored.UploadAsync(new BlobUpload { Key = key, Content = new MemoryStream([4, 5, 6]) });
        original.IsSuccess.ShouldBeTrue();
        var client = Substitute.For<IBlobStoreClient>();
        client.GetPropertiesAsync(key, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            var snapshot = await stored.GetPropertiesAsync(key);
            var properties = new PropertyBag();
            properties.Set("owner", "other writer");
            (await stored.UpdatePropertiesAsync(new BlobPropertiesUpdate { Key = key, Properties = properties, IfMatchETag = snapshot.Value.ETag })).IsSuccess.ShouldBeTrue();
            return snapshot;
        });
        client.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>())
            .Returns(call => stored.UploadAsync(call.ArgAt<BlobUpload>(0), call.ArgAt<CancellationToken>(1)));
        var form = Fields();
        form["etag"] = original.Value.ETag;

        // Act
        var status = await DispatchActionAsync("upload", client, form);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        (await stored.GetPropertiesAsync(key)).Value.Properties.Get<string>("owner").ShouldBe("other writer");
        var download = await stored.DownloadAsync(key);
        await using var content = download.Value;
        using var bytes = new MemoryStream();
        await content.Content.CopyToAsync(bytes);
        bytes.ToArray().ShouldBe(new byte[] { 4, 5, 6 });
    }

    [Fact]
    public async Task UploadAction_WhenMetadataCannotBeRead_DoesNotReplaceContent()
    {
        var client = Substitute.For<IBlobStoreClient>();
        client.GetPropertiesAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreProviderError("Unavailable"))));
        var form = Fields();
        form["etag"] = "original-version";

        var status = await DispatchActionAsync("upload", client, form);

        status.ShouldBe(StatusCodes.Status500InternalServerError);
        await client.DidNotReceive().UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAction_WhenTargetWasDeleted_DoesNotRecreateWithoutExplicitForce()
    {
        var client = new BlobStoreClient("inmemory", new InMemoryBlobStoreProvider());
        var form = Fields();
        form["etag"] = "deleted-version";

        var status = await DispatchActionAsync("upload", client, form);

        status.ShouldBe(StatusCodes.Status409Conflict);
        (await client.ExistsAsync(new("records", "42.txt"))).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task UploadAction_NewUpload_StoresPropertiesAndExpirationAndRemainsCreateOnly()
    {
        var client = new BlobStoreClient("inmemory", new InMemoryBlobStoreProvider());
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);
        var form = Fields();
        form["mode"] = "new";
        form["expiresAt"] = expiresAt.ToString("O");
        form["properties"] = """{"classification":"internal","approved":true,"year":2026}""";

        var created = await DispatchActionAsync("upload", client, form);
        var duplicate = await DispatchActionAsync("upload", client, form);

        created.ShouldBe(StatusCodes.Status200OK);
        duplicate.ShouldBe(StatusCodes.Status409Conflict);
        var info = (await client.GetPropertiesAsync(new("records", "42.txt"))).Value;
        info.ExpiresAt.ShouldBe(expiresAt);
        info.Properties.Get<string>("classification").ShouldBe("internal");
        info.Properties.Get<bool>("approved").ShouldBeTrue();
        info.Properties.Get<long>("year").ShouldBe(2026L);
    }

    [Theory]
    [InlineData(null, "archive", "archive/42.txt")]
    [InlineData("", "archive", "archive/42.txt")]
    [InlineData("  ", "archive", "archive/42.txt")]
    [InlineData("/", "archive", "archive/42.txt")]
    [InlineData("", "", "42.txt")]
    [InlineData("report.txt", "archive", "archive/report.txt")]
    public async Task UploadAction_NewUpload_ResolvesNameWithinPrefix(string name, string prefix, string expectedName)
    {
        var client = new BlobStoreClient("inmemory", new InMemoryBlobStoreProvider());
        var form = Fields();
        form["mode"] = "new";
        form["prefix"] = prefix;
        form.Remove("name");
        if (name is not null) form["name"] = name;

        var status = await DispatchActionAsync("upload", client, form);

        status.ShouldBe(StatusCodes.Status200OK);
        (await client.ExistsAsync(new("records", expectedName))).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAction_WithTypedDimensions_ForwardsTheirTypesAndValues()
    {
        var client = Substitute.For<IBlobStoreClient>();
        client.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>()).Returns(Result<BlobInfo>.Success(new()));
        var form = Fields();
        form["mode"] = "new";
        form["dimensions"] = """{"year":{"type":"integer","value":2026},"amount":{"type":"decimal","value":"999999999999.999999"},"tag":{"type":"null","value":null}}""";

        var status = await DispatchActionAsync("upload", client, form);

        status.ShouldBe(StatusCodes.Status200OK);
        await client.Received(1).UploadAsync(Arg.Is<BlobUpload>(upload =>
            Equals(upload.Dimensions["year"].Value, 2026)
            && Equals(upload.Dimensions["amount"].Value, 999999999999.999999m)
            && upload.Dimensions["tag"].ValueType == BlobDimensionValueType.Null), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("properties", """{"tag":["invalid"]}""")]
    [InlineData("dimensions", """{"year":{"type":"integer","value":"2026"}}""")]
    public async Task UploadAction_WithInvalidMetadata_DoesNotUpload(string field, string value)
    {
        var client = Substitute.For<IBlobStoreClient>();
        var form = Fields();
        form["mode"] = "new";
        form[field] = value;

        var status = await DispatchActionAsync("upload", client, form);

        status.ShouldBe(StatusCodes.Status400BadRequest);
        await client.DidNotReceive().UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MetadataAction_CombinedEdit_SavesUnderOneVersionAndKeepsContent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection().AddDbContext<MetadataBlobContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MetadataBlobContext>().Database.EnsureCreatedAsync();
        }

        var provider = new EntityFrameworkBlobStoreProvider<MetadataBlobContext>(services.GetRequiredService<IServiceScopeFactory>());
        var sut = new BlobStoreClient("reports", provider, decorate: inner => new CompressionBlobStoreClientBehavior(inner));
        var key = new BlobKey("records", "42.txt");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var owner = Guid.NewGuid();
        await using var stream = new MemoryStream("report"u8.ToArray());
        var initial = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = stream,
            ContentType = ContentType.TXT,
            ExpiresAt = expiresAt,
            Properties = new PropertyBag { ["owner"] = owner },
            Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2025 }).Value
        });
        initial.IsSuccess.ShouldBeTrue();

        var form = Fields();
        form["etag"] = initial.Value.ETag;
        form["properties"] = $$"""{"owner":"{{owner}}","source":"reviewed"}""";
        form["dimensions"] = """{"year":{"type":"integer","value":2026}}""";
        (await DispatchActionAsync("metadata", sut, form)).ShouldBe(StatusCodes.Status200OK);

        var edited = (await sut.GetPropertiesAsync(key)).Value;
        edited.ETag.ShouldNotBe(initial.Value.ETag);
        edited.Properties["owner"].ShouldBe(owner);
        edited.Properties["source"].ShouldBe("reviewed");
        edited.Dimensions["year"].Value.ShouldBe(2026);
        edited.ContentType.ShouldBe(ContentType.TXT);
        edited.ContentHash.ShouldBe(initial.Value.ContentHash);
        edited.ExpiresAt.ShouldBe(expiresAt);

        form["properties"] = "{}";
        form["dimensions"] = "{}";
        form["expiresAt"] = "";
        (await DispatchActionAsync("metadata", sut, form)).ShouldBe(StatusCodes.Status409Conflict);
        (await sut.GetPropertiesAsync(key)).Value.ETag.ShouldBe(edited.ETag);

        form["etag"] = edited.ETag;
        (await DispatchActionAsync("metadata", sut, form)).ShouldBe(StatusCodes.Status200OK);
        var cleared = (await sut.GetPropertiesAsync(key)).Value;
        cleared.Properties.ShouldBeEmpty();
        cleared.Dimensions.ShouldBeEmpty();
        cleared.ExpiresAt.ShouldBeNull();
        await using var download = (await sut.DownloadAsync(key)).Value;
        using var reader = new StreamReader(download.Content);
        (await reader.ReadToEndAsync()).ShouldBe("report");
    }

    [Theory]
    [InlineData("properties", """{"owner":[]}""")]
    [InlineData("dimensions", """{"year":{"type":"integer","value":"2026"}}""")]
    [InlineData("expiresAt", "invalid")]
    public async Task MetadataAction_WithInvalidField_RejectsTheEntireEdit(string field, string value)
    {
        var sut = Substitute.For<IBlobStoreClient>();
        var form = Fields();
        form["etag"] = "original-version";
        form[field] = value;

        var status = await DispatchActionAsync("metadata", sut, form);

        status.ShouldBe(StatusCodes.Status400BadRequest);
        sut.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MetadataAction_WithoutDimensionSupport_AllowsPropertyEditsAndRejectsDimensions()
    {
        var sut = new BlobStoreClient("inmemory", new InMemoryBlobStoreProvider());
        var form = Fields();
        form["mode"] = "new";
        (await DispatchActionAsync("upload", sut, form)).ShouldBe(StatusCodes.Status200OK);
        var key = new BlobKey("records", "42.txt");
        var initial = (await sut.GetPropertiesAsync(key)).Value;
        form["mode"] = "edit";
        form["etag"] = initial.ETag;
        form["properties"] = """{"source":"reviewed"}""";
        form["dimensions"] = "{}";

        (await DispatchActionAsync("metadata", sut, form)).ShouldBe(StatusCodes.Status400BadRequest);
        (await sut.GetPropertiesAsync(key)).Value.ETag.ShouldBe(initial.ETag);
        form.Remove("dimensions");
        (await DispatchActionAsync("metadata", sut, form)).ShouldBe(StatusCodes.Status200OK);
        (await sut.GetPropertiesAsync(key)).Value.Properties["source"].ShouldBe("reviewed");
    }

    private static Dictionary<string, string> Fields() => new()
    {
        ["store"] = "reports", ["container"] = "records", ["name"] = "42.txt", ["mode"] = "edit"
    };

    private static async Task<int> DispatchActionAsync(string action, IBlobStoreClient client, Dictionary<string, string> fields)
    {
        var builder = WebApplication.CreateBuilder();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(options);
        builder.Services.AddBlobStorage().WithInMemoryClient("reports");
        var factory = Substitute.For<IBlobStoreClientFactory>();
        factory.CreateClient("reports").Returns(client);
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();
        new BlobDashboardEndpoints(options).Map(app);
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(item => item.RoutePattern.RawText.EndsWith($"/storage/blobs/actions/{action}", StringComparison.Ordinal));
        using var scope = app.Services.CreateScope();
        using var fileContent = new MemoryStream([1, 2, 3]);
        var files = new FormFileCollection
        {
            new FormFile(fileContent, 0, fileContent.Length, "file", "42.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" }
        };
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST";
        context.Request.Path = endpoint.RoutePattern.RawText;
        context.Request.ContentType = "multipart/form-data";
        context.Request.Form = new FormCollection(fields.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)), files);
        using var body = new MemoryStream();
        context.Response.Body = body;
        await endpoint.RequestDelegate(context);
        return context.Response.StatusCode;
    }

    private static DefaultHttpContext CreateHttpContext(IServiceCollection services) =>
        new()
        {
            RequestServices = services.BuildServiceProvider()
        };

    private sealed class MetadataBlobContext(DbContextOptions<MetadataBlobContext> options) : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
    }
}
