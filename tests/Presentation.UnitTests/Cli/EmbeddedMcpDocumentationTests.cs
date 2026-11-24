namespace BridgingIT.DevKit.Presentation.UnitTests.Cli;

using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Cli;
using BridgingIT.DevKit.Common;

[UnitTest("Presentation")]
public sealed class EmbeddedMcpDocumentationTests
{
    private const string ResourcePrefix = "BridgingIT.DevKit.Presentation.UnitTests.EmbeddedDocumentation/";
    private const string DocumentationOnlyPrefix = "BridgingIT.DevKit.Presentation.UnitTests.DocumentationOnly/";
    private const string DocumentPath = "docs/features-harbor.md";
    private const string BindUid = "BridgingIT.DevKit.Common.Result.Bind(System.Action)";

    [Fact]
    public async Task ListAsync_EmbeddedMarkdown_ReturnsCanonicalPathsAndContent()
    {
        // Arrange
        var sut = new EmbeddedMcpDocumentationSource(CreateResources());

        // Act
        var documents = await sut.ListAsync(CancellationToken.None);

        // Assert
        documents.Select(document => document.Source).ShouldBe(new[]
        {
            DocumentPath,
            "docs/features-results.md"
        });
        documents.Single(document => document.Source == DocumentPath).Content.ShouldContain("# Harbor jobs");
        sut.Name.ShouldBe("embedded");
    }

    [Theory]
    [InlineData(DocumentPath)]
    [InlineData("https://github.com/bridgingit/bitdevkit/blob/main/docs/features-harbor.md")]
    [InlineData("https://github.com/BridgingIT-GmbH/bITdevKit/blob/v1.2.3/docs/features-harbor.md")]
    [InlineData("https://github.com/bridgingit/bitdevkit/blob/releases/older-framework/docs/features-harbor.md?plain=1#usage")]
    [InlineData("https://raw.githubusercontent.com/bridgingit/bitdevkit/main/docs/features-harbor.md")]
    [InlineData("https://raw.githubusercontent.com/bridgingit-gmbh/bitdevkit/releases/older-framework/docs/features-harbor.md")]
    public async Task GetAsync_OfficialPathAlias_ReturnsInstalledEmbeddedDocument(string source)
    {
        // Arrange
        var sut = new EmbeddedMcpDocumentationSource(CreateResources());

        // Act
        var document = await sut.GetAsync(source, CancellationToken.None);

        // Assert
        document.ShouldNotBeNull();
        document.Source.ShouldBe(DocumentPath);
        document.Content.ShouldContain("Harbor jobs process orders");
    }

    [Theory]
    [InlineData("https://example.test/bridgingit/bitdevkit/blob/main/docs/features-harbor.md")]
    [InlineData("https://github.com/another-owner/bitdevkit/blob/main/docs/features-harbor.md")]
    [InlineData("https://github.com/bridgingit/another-repo/blob/main/docs/features-harbor.md")]
    [InlineData("docs/../features-harbor.md")]
    [InlineData("docs/%2e%2e/features-harbor.md")]
    [InlineData("https://github.com/bridgingit/bitdevkit/blob/main/docs/%2e%2e/features-harbor.md")]
    [InlineData("/tmp/docs/features-harbor.md")]
    [InlineData("C:\\docs\\features-harbor.md")]
    [InlineData("file:///docs/features-harbor.md")]
    [InlineData("docs/specs/private.md")]
    public async Task GetAsync_UnsupportedSource_ReturnsUnavailableWithoutAnotherSource(string source)
    {
        // Arrange
        var sut = new McpDocumentationTools(new EmbeddedMcpDocumentationSource(CreateResources()));

        // Act
        var response = await sut.GetAsync(Arguments(new { source }), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.DocumentationUnavailable);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task SearchAsync_DocumentResultBoundary_ReportsActualAdditionalMatches(int limit, bool truncated)
    {
        // Arrange
        var sut = new McpDocumentationTools(new EmbeddedMcpDocumentationSource(CreateResources()));

        // Act
        var response = await sut.SearchAsync(Arguments(new { query = "harbor", limit }), CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        response.Truncated.ShouldBe(truncated);
        Data(response).GetProperty("results").GetArrayLength().ShouldBe(limit);
    }

    [Fact]
    public async Task GetAsync_Continuation_ReconstructsWholeDocumentAndEndsAtEof()
    {
        // Arrange
        var source = new EmbeddedMcpDocumentationSource(CreateResources());
        var expected = await source.GetAsync(DocumentPath, CancellationToken.None);
        var sut = new McpDocumentationTools(source);
        var received = new StringBuilder();
        var offset = 0;
        const int maxChars = 17;

        // Act
        while (true)
        {
            var response = await sut.GetAsync(Arguments(new { source = DocumentPath, offset, maxChars }), CancellationToken.None);
            response.Available.ShouldBeTrue();
            var data = Data(response);
            data.GetProperty("offset").GetInt32().ShouldBe(offset);
            data.GetProperty("totalChars").GetInt32().ShouldBe(expected.Content.Length);
            var chunk = data.GetProperty("content").GetString();
            chunk.Length.ShouldBeLessThanOrEqualTo(maxChars);
            received.Append(chunk);
            var next = data.GetProperty("nextOffset");
            if (next.ValueKind == JsonValueKind.Null)
            {
                response.Truncated.ShouldBeFalse();
                break;
            }

            response.Truncated.ShouldBeTrue();
            var call = response.Next.Single(nextCall => nextCall.Tool == "bdk_docs_get");
            var nextArguments = JsonSerializer.SerializeToElement(call.Arguments);
            nextArguments.GetProperty("source").GetString().ShouldBe(DocumentPath);
            nextArguments.GetProperty("maxChars").GetInt32().ShouldBe(maxChars);
            offset = next.GetInt32();
            nextArguments.GetProperty("offset").GetInt32().ShouldBe(offset);
        }

        // Assert
        received.ToString().ShouldBe(expected.Content);
        var eof = await sut.GetAsync(Arguments(new { source = DocumentPath, offset = expected.Content.Length }), CancellationToken.None);
        Data(eof).GetProperty("content").GetString().ShouldBe(string.Empty);
        Data(eof).GetProperty("nextOffset").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetAsync_SupplementaryCharacterContinuation_PreservesJsonRoundTrip()
    {
        // Arrange
        const string expected = "a😀b";
        const int maxChars = 2;
        var sut = new McpDocumentationTools(new SingleDocumentSource(expected));
        var received = new StringBuilder();
        var offset = 0;

        // Act
        while (true)
        {
            var response = await sut.GetAsync(Arguments(new { source = DocumentPath, offset, maxChars }), CancellationToken.None);
            response.Available.ShouldBeTrue();
            var data = Data(response);
            received.Append(data.GetProperty("content").GetString());
            var next = data.GetProperty("nextOffset");
            if (next.ValueKind == JsonValueKind.Null)
            {
                break;
            }

            var nextOffset = next.GetInt32();
            nextOffset.ShouldBeGreaterThan(offset);
            offset = nextOffset;
        }

        // Assert
        received.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    public async Task GetAsync_OffsetOrLimitSplitsSurrogatePair_ReturnsOperationFailed(int offset, int maxChars)
    {
        // Arrange
        var sut = new McpDocumentationTools(new SingleDocumentSource("a😀b"));

        // Act
        var response = await sut.GetAsync(Arguments(new { source = DocumentPath, offset, maxChars }), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.OperationFailed);
        Data(response).TryGetProperty("content", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("999999")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("\"0\"")]
    [InlineData("null")]
    [InlineData("true")]
    public async Task GetAsync_InvalidOffset_ReturnsOperationFailed(string offset)
    {
        // Arrange
        var sut = new McpDocumentationTools(new EmbeddedMcpDocumentationSource(CreateResources()));
        using var arguments = JsonDocument.Parse($"{{\"source\":\"{DocumentPath}\",\"offset\":{offset}}}");

        // Act
        var response = await sut.GetAsync(arguments.RootElement, CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.OperationFailed);
        Data(response).TryGetProperty("content", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_LongDocument_UsesDefaultAndMaximumCharacterLimits()
    {
        // Arrange
        var sut = new McpDocumentationTools(new SingleDocumentSource(new string('x', 25001)));

        // Act
        var defaultResponse = await sut.GetAsync(Arguments(new { source = DocumentPath }), CancellationToken.None);
        var maximumResponse = await sut.GetAsync(Arguments(new { source = DocumentPath, maxChars = 999999 }), CancellationToken.None);

        // Assert
        Data(defaultResponse).GetProperty("content").GetString().Length.ShouldBe(6000);
        Data(maximumResponse).GetProperty("content").GetString().Length.ShouldBe(20000);
        defaultResponse.Truncated.ShouldBeTrue();
        maximumResponse.Truncated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSymbolAsync_IndividualSymbolDetail_PreservesUidAndRemarks()
    {
        // Arrange
        var sut = new EmbeddedMcpApiReferenceSource(CreateResources());

        // Act
        var symbol = await sut.GetSymbolAsync("Fixture.Result", CancellationToken.None);

        // Assert
        symbol.ShouldNotBeNull();
        symbol.Uid.ShouldBe("Fixture.Result");
        symbol.Remarks.ShouldBe("An individual symbol detail.");
    }

    [Theory]
    [InlineData("Fixture.MissingDetail")]
    [InlineData("Fixture.AbsentFromPage")]
    [InlineData("Fixture.Unknown")]
    public async Task GetAsync_MissingSymbolContent_ReturnsUnavailableInsteadOfIndexSummary(string uid)
    {
        // Arrange
        var sut = new McpApiReferenceTools(new EmbeddedMcpApiReferenceSource(CreateResources()));

        // Act
        var response = await sut.GetAsync(Arguments(new { uid }), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.DocumentationUnavailable);
    }

    [Theory]
    [InlineData("MissingDetail", 1, false)]
    [InlineData("Result", 1, true)]
    public async Task SearchAsync_ApiResultBoundary_ReportsActualAdditionalMatches(string query, int limit, bool truncated)
    {
        // Arrange
        var sut = new McpApiReferenceTools(new EmbeddedMcpApiReferenceSource(CreateResources()));

        // Act
        var response = await sut.SearchAsync(Arguments(new { query, limit }), CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        response.Truncated.ShouldBe(truncated);
        Data(response).GetProperty("results").GetArrayLength().ShouldBe(limit);
    }

    [Fact]
    public async Task SearchAsync_MissingApiIndex_DoesNotDisableMarkdownOrGuidance()
    {
        // Arrange
        var resources = CreateResources(DocumentationOnlyPrefix);
        var sut = new McpApiReferenceTools(new EmbeddedMcpApiReferenceSource(resources));
        var docs = new McpDocumentationTools(new EmbeddedMcpDocumentationSource(resources));
        var guidance = new McpGuidanceTools();

        // Act
        var apiResponse = await sut.SearchAsync(Arguments(new { query = "Result" }), CancellationToken.None);
        var docsResponse = await docs.GetAsync(Arguments(new { source = "docs/features-only.md" }), CancellationToken.None);
        var guidanceResponse = guidance.Get(Arguments(new { topic = "jobs" }));

        // Assert
        apiResponse.Available.ShouldBeFalse();
        apiResponse.Code.ShouldBe(McpErrorCode.DocumentationUnavailable);
        docsResponse.Available.ShouldBeTrue();
        guidanceResponse.Available.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_ConcurrentRequests_ReusesTheLoadedDocumentSet()
    {
        // Arrange
        var sut = new EmbeddedMcpDocumentationSource(CreateResources());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sut.ListAsync(CancellationToken.None)));

        // Assert
        foreach (var result in results)
        {
            result.ShouldBeSameAs(results[0]);
        }
    }

    [Fact]
    public async Task GetIndexAsync_ConcurrentRequests_ReusesTheLoadedIndex()
    {
        // Arrange
        var sut = new EmbeddedMcpApiReferenceSource(CreateResources());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sut.GetIndexAsync(CancellationToken.None)));

        // Assert
        foreach (var result in results)
        {
            result.ShouldBeSameAs(results[0]);
        }
    }

    [Fact]
    public async Task ListAsync_CanceledFirstRequest_DoesNotPoisonFollowingRequest()
    {
        // Arrange
        var sut = new EmbeddedMcpDocumentationSource(CreateResources());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ListAsync(cancellation.Token));
        var documents = await sut.ListAsync(CancellationToken.None);

        // Assert
        documents.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetIndexAsync_CanceledFirstRequest_DoesNotPoisonFollowingRequest()
    {
        // Arrange
        var sut = new EmbeddedMcpApiReferenceSource(CreateResources());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetIndexAsync(cancellation.Token));
        var symbol = await sut.GetSymbolAsync(BindUid, CancellationToken.None);

        // Assert
        symbol.ShouldNotBeNull();
        symbol.Uid.ShouldBe(BindUid);
    }

    [Fact]
    public async Task SelfTestAsync_NoRuntime_StillReportsReadableCliDocumentation()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "bdk-embedded-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var options = new HostRegistryOptions
        {
            RuntimePath = Path.Combine(root, "runtimes"),
            SelectionPath = Path.Combine(root, "selections")
        };
        var workspace = new WorkspaceResolver().Resolve(root);
        var sut = new McpRuntimeTools(
            new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options),
            new HostRuntimeDiscovery(options),
            new HostSelectionStore(options),
            new McpIpcClient());

        try
        {
            // Act
            var status = await sut.GetStatusAsync(new McpCliOptions(), CancellationToken.None);
            var response = await sut.SelfTestAsync(new McpCliOptions(), CancellationToken.None);

            // Assert
            var statusDocumentation = Data(status).GetProperty("documentation");
            statusDocumentation.GetProperty("source").GetString().ShouldBe("embedded");
            statusDocumentation.GetProperty("available").GetBoolean().ShouldBeTrue();
            var selfTest = Data(response);
            selfTest.GetProperty("documentation").GetProperty("available").GetBoolean().ShouldBeTrue();
            selfTest.GetProperty("documentation").GetProperty("data").GetProperty("source").GetString().ShouldBe("embedded");
            selfTest.GetProperty("runtime").GetProperty("available").GetBoolean().ShouldBeFalse();
            selfTest.GetProperty("ipc").GetProperty("connected").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static EmbeddedMcpResources CreateResources(string prefix = ResourcePrefix)
        => new(typeof(EmbeddedMcpDocumentationTests).Assembly, prefix);

    private static JsonElement Arguments(object value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Data(McpResponse response)
        => JsonSerializer.SerializeToElement(response.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class SingleDocumentSource(string content) : IMcpDocumentationSource
    {
        public string Name => "fixture";

        public Task<IReadOnlyList<McpDocumentationDocument>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<McpDocumentationDocument>>([new(DocumentPath, "https://example.test/docs", content)]);

        public Task<McpDocumentationDocument> GetAsync(string source, CancellationToken cancellationToken)
            => Task.FromResult(new McpDocumentationDocument(DocumentPath, "https://example.test/docs", content));
    }
}
