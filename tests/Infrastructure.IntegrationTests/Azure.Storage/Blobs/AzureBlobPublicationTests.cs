namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using System.Net;
using System.Text;
using Application.Storage;
using global::Azure.Core.Pipeline;
using global::Azure.Storage.Blobs;
using Infrastructure.Azure;

public sealed class AzureBlobPublicationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PreserveUpload_WithPublicationRaces_UsesBoundedConditionsWithoutReplayingSource(int conflicts)
    {
        using var transport = new PublicationHandler(conflicts);
        using var http = new HttpClient(transport);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        options.Retry.MaxRetries = 0;
        var sut = new AzureBlobStoreProvider(new BlobServiceClient(new Uri("https://account.blob.core.windows.net"), options));
        using var source = new SinglePassStream([1, 2, 3]);
        var result = await sut.UploadAsync(new StoredBlobUpload { Key = new("reports", "item"), Content = source });
        transport.CommitAttempts.ShouldBe(Math.Min(conflicts + 1, 3));
        source.BytesRead.ShouldBe(3);
        if (conflicts == 3)
        {
            result.HasError<BlobStoreConflictError>().ShouldBeTrue();
            transport.Published.ShouldBeFalse();
        }
        else
        {
            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.Properties.Get<int>("generation").ShouldBe(conflicts);
            result.Value.TransformMetadata.Count.ShouldBe(0);
            transport.Published.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CreateOnly_WithConflict_DoesNotRetryOrWeakenCondition()
    {
        using var transport = new PublicationHandler(3);
        using var http = new HttpClient(transport);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        options.Retry.MaxRetries = 0;
        var sut = new AzureBlobStoreProvider(new BlobServiceClient(new Uri("https://account.blob.core.windows.net"), options));
        var result = await sut.UploadAsync(new StoredBlobUpload { Key = new("reports", "item"), Content = new MemoryStream([1]), OverwriteMode = BlobOverwriteMode.FailIfExists });
        result.HasError<BlobStoreConflictError>().ShouldBeTrue();
        transport.CommitAttempts.ShouldBe(1);
        transport.Published.ShouldBeFalse();
    }

    [Fact]
    public async Task EncodedProperties_ExceedingMetadataLimit_FailWithoutPublication()
    {
        using var transport = new PublicationHandler(0);
        using var http = new HttpClient(transport);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        var sut = new AzureBlobStoreProvider(new BlobServiceClient(new Uri("https://account.blob.core.windows.net"), options));
        var result = await sut.UploadAsync(new StoredBlobUpload { Key = new("reports", "item"), Content = new MemoryStream([1]), Properties = new() { ["value"] = new string('x', 8192) } });
        result.HasError<BlobStoreSizeLimitExceededError>().ShouldBeTrue();
        transport.CommitAttempts.ShouldBe(0);
    }

    private sealed class PublicationHandler(int conflicts) : HttpMessageHandler
    {
        private int generation;
        private Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

        public int CommitAttempts { get; private set; }

        public bool Published { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri.Query;
            if (query.Contains("comp=blocklist", StringComparison.Ordinal))
            {
                this.CommitAttempts++;
                if (this.CommitAttempts <= conflicts)
                {
                    this.generation++;
                    var failure = new HttpResponseMessage(HttpStatusCode.PreconditionFailed) { Content = new StringContent("<Error><Code>ConditionNotMet</Code><Message>changed</Message></Error>") };
                    failure.Headers.Add("x-ms-error-code", "ConditionNotMet");
                    return failure;
                }

                request.Headers.GetValues("If-Match").Single().ShouldBe($"\"v{this.generation}\"");
                this.Published = true;
                this.metadata = request.Headers.Where(header => header.Key.StartsWith("x-ms-meta-", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(header => header.Key[10..], header => header.Value.Single(), StringComparer.OrdinalIgnoreCase);
            }

            if (query.Contains("comp=block&", StringComparison.Ordinal))
            {
                _ = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(request.Method == HttpMethod.Head || request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Created)
            {
                Content = new StringContent(query.Contains("comp=tags", StringComparison.Ordinal) ? "<Tags><TagSet /></Tags>" : string.Empty)
            };
            response.Headers.ETag = new($"\"v{this.generation}\"");
            response.Content.Headers.LastModified = DateTimeOffset.UtcNow;
            response.Headers.Add("x-ms-creation-time", DateTimeOffset.UtcNow.ToString("R"));
            if (request.Method == HttpMethod.Head)
            {
                response.Content.Headers.ContentLength = 3;
                var current = this.Published ? this.metadata : new Dictionary<string, string>
                {
                    ["bdk_properties"] = Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(BlobPropertyValues.Serialize(new() { ["generation"] = this.generation })))
                };
                foreach (var item in current) response.Headers.Add("x-ms-meta-" + item.Key, item.Value);
            }

            return response;
        }
    }

    private sealed class SinglePassStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int BytesRead { get; private set; }

        public override bool CanSeek => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken);
            this.BytesRead += read;
            return read;
        }
    }
}
