// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.Extensions.DependencyInjection;

public sealed class BlobStorageMutationEndpointsTests : IAsyncLifetime
{
    private const string Route = "/_bdk/api/storage/blobs/reports/blobs";
    private readonly BlobStorageEndpointsApplication factory;
    private readonly HttpClient sut;
    private readonly IServiceScope scope;
    private readonly IBlobStoreClient blobs;

    public BlobStorageMutationEndpointsTests()
    {
        this.factory = new BlobStorageEndpointsApplication();
        this.sut = this.factory.CreateClient();
        this.scope = this.factory.Services.CreateScope();
        this.blobs = this.scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        this.sut.Dispose();
        this.scope.Dispose();
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task PostAsync_WithExistingKey_ReturnsConflictAndPreservesCreatedBlob()
    {
        // Arrange
        var created = await this.UploadAsync(HttpMethod.Post, "create.txt", "original");
        var info = await created.Content.ReadFromJsonAsync<BlobStorageBlobInfoModel>();

        // Act
        var duplicate = await this.UploadAsync(HttpMethod.Post, "create.txt", "replacement");

        // Assert
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        created.Headers.Location.ShouldNotBeNull();
        created.Headers.ETag.ShouldNotBeNull();
        created.Headers.ETag.Tag.ShouldStartWith("\"bdk.");
        HttpETag.ReadCondition(created.Headers.ETag.ToString()).Value.ShouldBe(info.ETag);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await this.ReadContentAsync("create.txt")).ShouldBe("original");
        (await this.blobs.GetPropertiesAsync(new("reports", "create.txt"))).Value.ETag.ShouldBe(info.ETag);
    }

    [Fact]
    public async Task PutAsync_WithVersionHeader_RejectsStaleWritesAndAllowsExplicitUnconditionalReplacement()
    {
        // Arrange
        var created = await this.UploadAsync(HttpMethod.Post, "edit.txt", "original");
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var originalTag = created.Headers.ETag.ToString();

        // Act
        var updated = await this.UploadAsync(HttpMethod.Put, "edit.txt", "newer", originalTag);
        var stale = await this.UploadAsync(HttpMethod.Put, "edit.txt", "stale draft", originalTag);
        var read = await this.sut.GetAsync(PropertiesUrl("edit.txt"));

        // Assert
        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        updated.Headers.ETag.ShouldNotBeNull();
        updated.Headers.ETag.ToString().ShouldNotBe(originalTag);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        read.Headers.ETag.ToString().ShouldBe(updated.Headers.ETag.ToString());
        (await this.ReadContentAsync("edit.txt")).ShouldBe("newer");

        var overwritten = await this.UploadAsync(HttpMethod.Put, "edit.txt", "forced");
        overwritten.StatusCode.ShouldBe(HttpStatusCode.OK);
        overwritten.Headers.ETag.ToString().ShouldNotBe(updated.Headers.ETag.ToString());
        (await this.ReadContentAsync("edit.txt")).ShouldBe("forced");
    }

    [Fact]
    public async Task GetContentAsync_WithCommittedBlob_ReturnsVersionThatCanGuardContentReplacement()
    {
        // Arrange
        var created = await this.UploadAsync(HttpMethod.Post, "download-edit.txt", "original");
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        // Act
        var properties = await this.sut.GetAsync(PropertiesUrl("download-edit.txt"));
        var content = await this.sut.GetAsync("/_bdk/api/storage/blobs/reports/content?container=reports&name=download-edit.txt");

        // Assert
        properties.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).ShouldBe("original");
        content.Headers.ETag.ShouldNotBeNull();
        content.Headers.ETag.ToString().ShouldBe(created.Headers.ETag.ToString());
        content.Headers.ETag.ToString().ShouldBe(properties.Headers.ETag.ToString());

        var replaced = await this.UploadAsync(HttpMethod.Put, "download-edit.txt", "edited", content.Headers.ETag.ToString());
        replaced.StatusCode.ShouldBe(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
        replaced.Headers.ETag.ToString().ShouldNotBe(content.Headers.ETag.ToString());
        (await this.ReadContentAsync("download-edit.txt")).ShouldBe("edited");
    }

    [Fact]
    public async Task PatchAsync_WithCurrentVersion_InvalidatesContentEditTokenAndReturnsFreshVersion()
    {
        // Arrange
        var created = await this.UploadAsync(HttpMethod.Post, "metadata.txt", "original");
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var originalTag = created.Headers.ETag.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Patch, Route + "/properties")
        {
            Content = JsonContent.Create(new BlobStorageUpdatePropertiesRequestModel
            {
                Container = "reports",
                Name = "metadata.txt",
                ContentType = "text/plain",
                Properties = new() { ["reviewed"] = true, ["count"] = 7, ["label"] = "approved" }
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", originalTag);

        // Act
        var patched = await this.sut.SendAsync(request);
        var stale = await this.UploadAsync(HttpMethod.Put, "metadata.txt", "stale", originalTag);

        // Assert
        patched.StatusCode.ShouldBe(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());
        patched.Headers.ETag.ToString().ShouldNotBe(originalTag);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var stored = await this.blobs.GetPropertiesAsync(new("reports", "metadata.txt"));
        stored.Value.ETag.ShouldBe(HttpETag.ReadCondition(patched.Headers.ETag.ToString()).Value);
        stored.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
        stored.Value.Properties.Get<long>("count").ShouldBe(7);
        (await this.ReadContentAsync("metadata.txt")).ShouldBe("original");

        var saved = await this.UploadAsync(HttpMethod.Put, "metadata.txt", "accepted", patched.Headers.ETag.ToString());
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAsync_WithVersionHeader_RetainsNewerBlobAndStaleSaveCannotRecreateDeletedBlob()
    {
        // Arrange
        var created = await this.UploadAsync(HttpMethod.Post, "delete.txt", "original");
        var updated = await this.UploadAsync(HttpMethod.Put, "delete.txt", "newer", created.Headers.ETag.ToString());

        // Act
        var staleDelete = await this.DeleteAsync("delete.txt", created.Headers.ETag.ToString());
        var retained = await this.ReadContentAsync("delete.txt");
        var deleted = await this.DeleteAsync("delete.txt", updated.Headers.ETag.ToString());
        var staleSave = await this.UploadAsync(HttpMethod.Put, "delete.txt", "stale", updated.Headers.ETag.ToString());
        var repeatedDelete = await this.DeleteAsync("delete.txt", updated.Headers.ETag.ToString());

        // Assert
        staleDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        retained.ShouldBe("newer");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        staleSave.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        repeatedDelete.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await this.blobs.ExistsAsync(new("reports", "delete.txt"))).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task PatchAsync_WithDifferentHeaderAndBodyVersions_ReturnsBadRequestAndKeepsLegacyBodySupport()
    {
        // Arrange
        var created = await this.UploadAsync(HttpMethod.Post, "legacy.txt", "original");
        var original = await created.Content.ReadFromJsonAsync<BlobStorageBlobInfoModel>();
        using var request = new HttpRequestMessage(HttpMethod.Patch, Route + "/properties")
        {
            Content = JsonContent.Create(new BlobStorageUpdatePropertiesRequestModel
            {
                Container = "reports", Name = "legacy.txt", IfMatchETag = "different", Properties = new() { ["owner"] = "wrong" }
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", created.Headers.ETag.ToString());

        // Act
        var mismatch = await this.sut.SendAsync(request);
        var legacy = await this.sut.PatchAsJsonAsync(Route + "/properties", new BlobStorageUpdatePropertiesRequestModel
        {
            Container = "reports", Name = "legacy.txt", IfMatchETag = original.ETag, Properties = new() { ["owner"] = "legacy" }
        });

        // Assert
        mismatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        legacy.StatusCode.ShouldBe(HttpStatusCode.OK, await legacy.Content.ReadAsStringAsync());
        var stored = await this.blobs.GetPropertiesAsync(new("reports", "legacy.txt"));
        stored.Value.Properties.Get<string>("owner").ShouldBe("legacy");
        var nativeHeader = await this.UploadAsync(HttpMethod.Put, "legacy.txt", "native", stored.Value.ETag);
        nativeHeader.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"*\"")]
    [InlineData("\"one\", \"two\"")]
    [InlineData("\"bdk.!\"")]
    [InlineData("\"bdk._w\"")]
    [InlineData("\"bdk.\"")]
    [InlineData("\"bdk.Kg\"")]
    public async Task MutationsAsync_WithInvalidVersionHeader_ReturnBadRequestWithoutMutation(string condition)
    {
        // Arrange
        await this.UploadAsync(HttpMethod.Post, "invalid.txt", "original");

        // Act
        var put = await this.UploadAsync(HttpMethod.Put, "invalid.txt", "wrong", condition);
        var delete = await this.DeleteAsync("invalid.txt", condition);
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, Route + "/properties")
        {
            Content = JsonContent.Create(new BlobStorageUpdatePropertiesRequestModel
            {
                Container = "reports", Name = "invalid.txt", Properties = new() { ["wrong"] = true }
            })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", condition);
        var patch = await this.sut.SendAsync(patchRequest);

        // Assert
        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        delete.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await this.ReadContentAsync("invalid.txt")).ShouldBe("original");
    }

    [Fact]
    public async Task PutAsync_WithoutVersionAndMissingTarget_CreatesBlob()
    {
        // Act
        var response = await this.UploadAsync(HttpMethod.Put, "upsert.txt", "created");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.ETag.ShouldNotBeNull();
        (await this.ReadContentAsync("upsert.txt")).ShouldBe("created");
    }

    [Fact]
    public async Task PostAsync_WithIfMatchHeader_ReturnsBadRequestWithoutCreatingBlob()
    {
        // Act
        var response = await this.UploadAsync(HttpMethod.Post, "conditional-create.txt", "wrong", "\"version\"");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await this.blobs.ExistsAsync(new("reports", "conditional-create.txt"))).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task PostAsync_WithScalarMetadataAndExpiration_PersistsPortablePropertiesAndMimeType()
    {
        // Arrange
        var expiration = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var response = await this.UploadAsync(HttpMethod.Post, "scalars.txt", "data", properties:
            "{\"reviewed\":true,\"count\":7,\"price\":2.5,\"label\":\"ready\",\"optional\":null}", expiresAt: expiration);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var stored = await this.blobs.GetPropertiesAsync(new("reports", "scalars.txt"));
        stored.Value.ContentType?.MimeType().ShouldBe("text/plain");
        stored.Value.ExpiresAt.ShouldBe(expiration);
        stored.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
        stored.Value.Properties.Get<long>("count").ShouldBe(7);
        stored.Value.Properties.Get<decimal>("price").ShouldBe(2.5m);
        stored.Value.Properties.Get<string>("label").ShouldBe("ready");
        stored.Value.Properties.Get<object>("optional").ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"nested\":{\"value\":1}}")]
    [InlineData("{\"array\":[1,2]}")]
    [InlineData("invalid json")]
    [InlineData("{\"overflow\":1e400}")]
    [InlineData("{\"overflow\":-1e400}")]
    public async Task PostAsync_WithInvalidMetadata_ReturnsBadRequestWithoutCreatingBlob(string properties)
    {
        // Act
        var response = await this.UploadAsync(HttpMethod.Post, "invalid-metadata.txt", "wrong", properties: properties);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await this.blobs.ExistsAsync(new("reports", "invalid-metadata.txt"))).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task PostAsync_WithEmptyBinaryBodyAndNoContentType_CreatesEmptyBlob()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BlobUrl("empty.bin"))
        {
            Content = new ByteArrayContent([])
        };

        var response = await this.sut.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.ETag.ShouldNotBeNull();
        (await this.blobs.GetPropertiesAsync(new("reports", "empty.bin"))).Value.Length.ShouldBe(0);
        (await this.ReadContentAsync("empty.bin")).ShouldBeEmpty();
    }

    private async Task<HttpResponseMessage> UploadAsync(
        HttpMethod method, string name, string content, string condition = null, string properties = null, DateTimeOffset? expiresAt = null)
    {
        var url = BlobUrl(name);
        if (expiresAt.HasValue)
        {
            url += "&expiresAt=" + Uri.EscapeDataString(expiresAt.Value.ToString("O"));
        }

        using var request = new HttpRequestMessage(method, url) { Content = new StringContent(content, Encoding.UTF8, "text/plain") };
        if (condition is not null) request.Headers.TryAddWithoutValidation("If-Match", condition);
        if (properties is not null) request.Headers.TryAddWithoutValidation("X-Storage-Properties", properties);
        return await this.sut.SendAsync(request);
    }

    private async Task<HttpResponseMessage> DeleteAsync(string name, string condition)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BlobUrl(name));
        request.Headers.TryAddWithoutValidation("If-Match", condition);
        return await this.sut.SendAsync(request);
    }

    private async Task<string> ReadContentAsync(string name)
    {
        var download = await this.blobs.DownloadAsync(new("reports", name));
        download.IsSuccess.ShouldBeTrue();
        await using var content = download.Value;
        using var reader = new StreamReader(content.Content);
        return await reader.ReadToEndAsync();
    }

    private static string BlobUrl(string name) => Route + "?container=reports&name=" + Uri.EscapeDataString(name);

    private static string PropertiesUrl(string name) => Route + "/properties?container=reports&name=" + Uri.EscapeDataString(name);
}
