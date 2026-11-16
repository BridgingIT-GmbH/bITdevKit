// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using System.Globalization;
using System.Text;
using Application.Storage;
using global::Azure;
using global::Azure.Core;
using global::Azure.Core.Pipeline;
using global::Azure.Storage.Blobs;
using Infrastructure.Azure;

[IntegrationTest("Infrastructure")]
public sealed class AzureBlobStoreBackendConcurrencyTests
{
    private static readonly BlobKey Key = new("reports", "item.txt");

    [Theory]
    [InlineData("properties")]
    [InlineData("metadata")]
    public async Task UpdatePropertiesAsync_WhenAnotherWriterIntervenes_RollbackPreservesNewerVersion(string intervention)
    {
        // Arrange
        var transport = new StorageTransport { InterveneAfter = intervention };
        var sut = CreateProvider(transport);

        // Act
        var result = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = Key,
            IfMatchETag = "\"v1\"",
            Properties = new PropertyBag { ["owner"] = "draft" }
        });

        // Assert
        result.HasError<BlobStorePartialUpdateError>().ShouldBeTrue();
        transport.ETag.ShouldBe("\"competitor\"");
        transport.Metadata["owner"].ShouldBe(PropertyBagScalarCodec.Encode("competitor"));
        transport.Mutations.Last().Condition.ShouldNotBe(transport.ETag);
        transport.PropertyReads.ShouldBe(1);
        transport.TagReadConditions.ShouldBe(["\"v1\""]);
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WhenAnotherWriterFollowsTags_ReturnsOwnCommittedSnapshot()
    {
        // Arrange
        var transport = new StorageTransport { InterveneAfter = "tags" };
        var sut = CreateProvider(transport);

        // Act
        var result = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = Key,
            IfMatchETag = "\"v1\"",
            Properties = new PropertyBag { ["owner"] = "draft" }
        });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(x => x.Message)));
        result.Value.ETag.ShouldBe("\"v3\"");
        result.Value.Properties.Get<string>("owner").ShouldBe("draft");
        transport.ETag.ShouldBe("\"competitor\"");
        transport.PropertyReads.ShouldBe(1);
        transport.Mutations.Select(x => x.Condition).ShouldBe(["\"v1\"", "\"v2\"", "\"v3\""]);
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WithStaleETag_RejectsBeforeMutation()
    {
        // Arrange
        var transport = new StorageTransport();
        var sut = CreateProvider(transport);

        // Act
        var result = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = Key,
            IfMatchETag = "\"old\"",
            Properties = new PropertyBag { ["owner"] = "draft" }
        });

        // Assert
        result.HasError<BlobStoreConflictError>().ShouldBeTrue();
        transport.ETag.ShouldBe("\"v1\"");
        transport.Mutations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadAsync_WhenVersionChangesDuringStaging_EnforcesConditionAtCommit(bool removeTarget)
    {
        // Arrange
        var transport = new StorageTransport { ChangeDuringStaging = true, RemoveDuringStaging = removeTarget };
        var sut = CreateProvider(transport);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("new"));

        // Act
        var result = await sut.UploadAsync(new BlobUpload { Key = Key, Content = content, IfMatchETag = "\"v1\"" });

        // Assert
        result.HasError<BlobStoreConflictError>().ShouldBeTrue();
        transport.CommitCondition.ShouldBe("\"v1\"");
        transport.Commits.ShouldBe(0);
        transport.Exists.ShouldBe(!removeTarget);
        transport.ETag.ShouldBe("\"competitor\"");
    }

    [Fact]
    public async Task UploadAsync_WhenAnotherWriterFollowsCommit_ReturnsCommittedVersion()
    {
        // Arrange
        var transport = new StorageTransport { ChangeAfterCommit = true };
        var sut = CreateProvider(transport);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("new"));

        // Act
        var result = await sut.UploadAsync(new BlobUpload { Key = Key, Content = content, IfMatchETag = "\"v1\"" });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(x => x.Message)));
        result.Value.ETag.ShouldBe("\"v2\"");
        result.Value.Length.ShouldBe(3);
        result.Value.CreatedAt.ShouldBeNull();
        transport.ETag.ShouldBe("\"competitor\"");
    }

    private static AzureBlobStoreProvider CreateProvider(StorageTransport transport)
    {
        var options = new BlobClientOptions { Transport = transport };
        options.Retry.MaxRetries = 0;
        return new AzureBlobStoreProvider(new BlobServiceClient(new Uri("https://storage.invalid/"), options));
    }

    private sealed class StorageTransport : HttpPipelineTransport
    {
        private static readonly DateTimeOffset Timestamp = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        private int version = 1;

        public string ETag { get; private set; } = "\"v1\"";

        public bool Exists { get; private set; } = true;

        public Dictionary<string, string> Metadata { get; private set; } = new()
        {
            ["bdk_contenthash"] = "sha256:" + new string('0', 64),
            ["owner"] = PropertyBagScalarCodec.Encode("original")
        };

        public string InterveneAfter { get; init; }

        public bool ChangeDuringStaging { get; init; }

        public bool RemoveDuringStaging { get; init; }

        public bool ChangeAfterCommit { get; init; }

        public int PropertyReads { get; private set; }

        public int Commits { get; private set; }

        public string CommitCondition { get; private set; }

        public List<(string Operation, string Condition)> Mutations { get; } = [];

        public List<string> TagReadConditions { get; } = [];

        public override Request CreateRequest() => HttpClientTransport.Shared.CreateRequest();

        public override void Process(HttpMessage message) => message.Response = this.Respond(message.Request);

        public override ValueTask ProcessAsync(HttpMessage message)
        {
            this.Process(message);
            return ValueTask.CompletedTask;
        }

        private Response Respond(Request request)
        {
            var query = request.Uri.ToUri().Query;
            var conditionHeader = query.Contains("comp=tags", StringComparison.Ordinal)
                ? "x-ms-blob-if-match"
                : "If-Match";
            request.Headers.TryGetValue(conditionHeader, out var condition);
            if (query.Contains("restype=container", StringComparison.Ordinal))
            {
                return new StorageResponse(201);
            }

            if (query.Contains("comp=blocklist", StringComparison.Ordinal))
            {
                this.CommitCondition = condition;
            }
            else if (query.Contains("comp=block", StringComparison.Ordinal))
            {
                if (this.ChangeDuringStaging)
                {
                    this.Intervene();
                    this.Exists = !this.RemoveDuringStaging;
                }

                return new StorageResponse(201);
            }

            if (request.Method == RequestMethod.Head)
            {
                this.PropertyReads++;
            }
            else if (request.Method == RequestMethod.Put)
            {
                this.Mutations.Add((query, condition));
            }

            if (!this.Exists)
            {
                return new StorageResponse(404, "BlobNotFound");
            }

            if (condition is not null && condition != this.ETag)
            {
                return new StorageResponse(412, "ConditionNotMet");
            }

            if (request.Method == RequestMethod.Head)
            {
                var properties = this.SuccessResponse(200);
                properties.SetHeader("Content-Length", "3");
                properties.SetHeader("Content-Type", "text/plain");
                properties.SetHeader("x-ms-blob-type", "BlockBlob");
                properties.SetHeader("x-ms-creation-time", Timestamp.ToString("R", CultureInfo.InvariantCulture));
                foreach (var (key, value) in this.Metadata)
                {
                    properties.SetHeader("x-ms-meta-" + key, value);
                }

                return properties;
            }

            if (request.Method == RequestMethod.Get && query.Contains("comp=tags", StringComparison.Ordinal))
            {
                this.TagReadConditions.Add(condition);
                return new StorageResponse(200, body: "<?xml version=\"1.0\" encoding=\"utf-8\"?><Tags><TagSet /></Tags>");
            }

            if (request.Method == RequestMethod.Put)
            {
                var isTags = query.Contains("comp=tags", StringComparison.Ordinal);
                if (!isTags)
                {
                    this.ETag = $"\"v{++this.version}\"";
                }

                if (query.Contains("comp=metadata", StringComparison.Ordinal) || query.Contains("comp=blocklist", StringComparison.Ordinal))
                {
                    this.Metadata = request.Headers.Where(x => x.Name.StartsWith("x-ms-meta-", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(x => x.Name[10..], x => x.Value, StringComparer.OrdinalIgnoreCase);
                }

                var committed = query.Contains("comp=blocklist", StringComparison.Ordinal);
                if (committed)
                {
                    this.Commits++;
                }

                var response = this.SuccessResponse(committed ? 201 : isTags ? 204 : 200);
                if ((!string.IsNullOrEmpty(this.InterveneAfter) && query.Contains("comp=" + this.InterveneAfter, StringComparison.Ordinal)) ||
                    (committed && this.ChangeAfterCommit))
                {
                    this.Intervene();
                }

                return response;
            }

            throw new InvalidOperationException($"Unexpected storage request: {request.Method} {query}");
        }

        private StorageResponse SuccessResponse(int status)
        {
            var response = new StorageResponse(status);
            response.SetHeader("ETag", this.ETag);
            response.SetHeader("Last-Modified", Timestamp.ToString("R", CultureInfo.InvariantCulture));
            return response;
        }

        private void Intervene()
        {
            this.ETag = "\"competitor\"";
            this.Metadata = new() { ["owner"] = PropertyBagScalarCodec.Encode("competitor") };
        }
    }

    private sealed class StorageResponse : Response
    {
        private readonly Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);

        public StorageResponse(int status, string errorCode = null, string body = null)
        {
            this.Status = status;
            if (errorCode is not null)
            {
                this.headers["x-ms-error-code"] = errorCode;
                body = $"<Error><Code>{errorCode}</Code><Message>Conditional storage request rejected.</Message></Error>";
            }

            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            this.ContentStream = new MemoryStream(bytes);
            this.headers["Content-Length"] = bytes.Length.ToString(CultureInfo.InvariantCulture);
            this.headers["Content-Type"] = "application/xml";
        }

        public override int Status { get; }

        public override string ReasonPhrase => this.Status < 400 ? "Success" : "Failure";

        public override Stream ContentStream { get; set; }

        public override string ClientRequestId { get; set; }

        public void SetHeader(string name, string value) => this.headers[name] = value;

        public override void Dispose() => this.ContentStream?.Dispose();

        protected override bool TryGetHeader(string name, out string value) => this.headers.TryGetValue(name, out value);

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            var found = this.headers.TryGetValue(name, out var value);
            values = found ? [value] : [];
            return found;
        }

        protected override bool ContainsHeader(string name) => this.headers.ContainsKey(name);

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => this.headers.Select(x => new HttpHeader(x.Key, x.Value));
    }
}
