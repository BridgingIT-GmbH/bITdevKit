namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Mcp;

using System.Text.Json;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Cli;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[UnitTest("Presentation")]
public sealed class ProfilingMcpHandlerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task HandleAsync_DimensionAndRequestFilters_ReadsSharedRetainedRecordings()
    {
        await using var services = await CreateServices();
        var id = Capture(services, "/cities/{id}", "HttpRequest", "west", method: "GET");
        Capture(services, "/cities/{id}", "HttpRequest", "east", method: "GET");
        Capture(services, "/cities/{id}", "HttpRequest", "west", method: "POST");
        await FlushAsync(services);

        var result = await CallAsync(services, "query", """
            {"kind":"HttpRequest","httpMethod":"GET","dimensions":[{"key":"region","value":{"type":"String","scalar":"west"}}]}
            """);

        result.Available.ShouldBeTrue();
        var data = Data(result);
        data.GetProperty("totalCount").GetInt64().ShouldBe(1);
        data.GetProperty("operations")[0].GetProperty("id").GetGuid().ShouldBe(id);
        data.GetProperty("operations")[0].GetProperty("nodeId").GetGuid().ShouldNotBe(Guid.Empty);
        data.GetProperty("operations")[0].TryGetProperty("segments", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_GroupedRequests_KeepsMethodsSeparate()
    {
        await using var services = await CreateServices();
        Capture(services, "/cities", "HttpRequest", method: "GET");
        Capture(services, "/cities", "HttpRequest", method: "POST");
        await FlushAsync(services);

        var result = await CallAsync(services, "query", """{"grouped":true,"kind":"HttpRequest"}""");

        result.Available.ShouldBeTrue();
        Data(result).GetProperty("totalGroupCount").GetInt64().ShouldBe(2);
        Data(result).GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("httpMethod").GetString()).ShouldBe(["GET", "POST"], ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_Paging_PreservesBoundaryAndRejectsChangedFilters()
    {
        await using var services = await CreateServices();
        Capture(services, "job:work");
        Capture(services, "job:work");
        await FlushAsync(services);
        var first = await CallAsync(services, "query", """{"take":1,"kind":"Job"}""");
        var cursor = Data(first).GetProperty("nextCursor").GetString();
        var next = await CallAsync(services, "query", JsonSerializer.Serialize(new { take = 1, kind = "Job", cursor }));
        var invalid = await CallAsync(services, "query", JsonSerializer.Serialize(new { take = 1, kind = "HttpRequest", cursor }));

        first.Truncated.ShouldBeTrue();
        next.Available.ShouldBeTrue();
        Data(first).GetProperty("operations")[0].GetProperty("id").GetGuid().ShouldNotBe(Data(next).GetProperty("operations")[0].GetProperty("id").GetGuid());
        invalid.Code.ShouldBe("profiling_boundary_invalid");
    }

    [Fact]
    public async Task HandleAsync_Details_ReturnsDimensionsAndBoundedRealSegments()
    {
        await using var services = await CreateServices();
        var id = Capture(services, "job:work", region: "west");
        await FlushAsync(services);

        var result = await CallAsync(services, "details", JsonSerializer.Serialize(new { id, segmentTake = 1 }));

        result.Available.ShouldBeTrue();
        result.Truncated.ShouldBeTrue();
        Data(result).GetProperty("operation").GetProperty("id").GetGuid().ShouldBe(id);
        Data(result).GetProperty("dimensions")[0].GetProperty("value").GetProperty("scalar").GetString().ShouldBe("west");
        Data(result).GetProperty("segments").GetArrayLength().ShouldBe(1);
        Data(result).GetProperty("segmentCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_Insights_RanksWholeSelectionAndSeparatesFailedLatency()
    {
        await using var services = await CreateServices();
        Capture(services, "job:many");
        Capture(services, "job:many", failed: true);
        Capture(services, "job:other");
        await FlushAsync(services);

        var result = await CallAsync(services, "insights", """{"take":1,"rankBy":"count"}""");

        result.Available.ShouldBeTrue();
        result.Truncated.ShouldBeTrue();
        var data = Data(result);
        data.GetProperty("count").GetInt64().ShouldBe(3);
        data.GetProperty("groupCount").GetInt32().ShouldBe(2);
        var group = data.GetProperty("groups")[0];
        group.GetProperty("key").GetString().ShouldBe("job:many");
        group.GetProperty("count").GetInt64().ShouldBe(2);
        group.GetProperty("latency").GetProperty("count").GetInt64().ShouldBe(1);
        group.GetProperty("outcomes").GetProperty("Failed").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_Comparison_SelectsSameOperationUnderIndependentDimensions()
    {
        await using var services = await CreateServices();
        Capture(services, "job:work", region: "west");
        Capture(services, "job:work", region: "east");
        Capture(services, "job:work", region: "east");
        Capture(services, "job:other", region: "east");
        await FlushAsync(services);

        var result = await CallAsync(services, "compare", """
            {"key":"job:work","kind":"Job",
             "baseline":{"dimensions":[{"key":"region","value":{"type":"String","scalar":"west"}}]},
             "candidate":{"dimensions":[{"key":"region","value":{"type":"String","scalar":"east"}}]}}
            """);

        result.Available.ShouldBeTrue();
        Data(result).GetProperty("baseline").GetProperty("count").GetInt64().ShouldBe(1);
        Data(result).GetProperty("candidate").GetProperty("count").GetInt64().ShouldBe(2);
    }

    [Theory]
    [InlineData("query", "{\"take\":0}")]
    [InlineData("query", "{\"take\":101}")]
    [InlineData("query", "{\"outcomes\":[\"unknown\"]}")]
    [InlineData("query", "{\"nodeId\":\"invalid\"}")]
    [InlineData("query", "{\"fromUtc\":\"2026-10-12T12:00:00+02:00\"}")]
    [InlineData("query", "{\"fromUtc\":\"2026-10-12T12:00:00Z\",\"toUtc\":\"2026-10-12T11:00:00Z\"}")]
    [InlineData("query", "{\"dimensions\":null}")]
    [InlineData("query", "{\"delete\":true}")]
    [InlineData("insights", "{\"rankBy\":\"other\"}")]
    [InlineData("details", "{\"id\":\"invalid\"}")]
    [InlineData("compare", "{\"key\":\"work\",\"kind\":\"Job\",\"baseline\":{},\"candidate\":{\"key\":\"different\"}}")]
    public async Task HandleAsync_InvalidSelectors_ReturnsStructuredArgumentError(string operation, string arguments)
    {
        await using var services = await CreateServices();

        var result = await CallAsync(services, operation, arguments);

        result.Available.ShouldBeFalse();
        result.Code.ShouldBe("invalid_arguments");
    }

    [Fact]
    public async Task HandleAsync_AnalysisBoundExceeded_RejectsPartialRankings()
    {
        await using var services = await CreateServices(analysisLimit: 1);
        Capture(services, "job:work");
        Capture(services, "job:work");
        await FlushAsync(services);

        var result = await CallAsync(services, "insights", "{}");

        result.Code.ShouldBe("profiling_limit_exceeded");
    }

    [Fact]
    public async Task HandleAsync_MissingDisabledAndUnretainedEvidence_ReturnsExplicitAvailability()
    {
        await using var empty = new ServiceCollection().BuildServiceProvider();
        (await CallAsync(empty, "query", "{}")).Code.ShouldBe(McpErrorCode.FeatureUnavailable);
        await using var services = await CreateServices();
        var missing = await CallAsync(services, "details", JsonSerializer.Serialize(new { id = Guid.NewGuid() }));
        missing.Code.ShouldBe("profiling_not_found");
        services.GetRequiredService<ProfilingOptions>().Enabled = false;
        (await CallAsync(services, "query", "{}")).Code.ShouldBe("profiling_disabled");
    }

    [Theory]
    [InlineData("correlation.inspect")]
    [InlineData("investigate.correlation")]
    public async Task HandleAsync_CorrelationWithoutLogs_ReturnsIndependentProfilingEvidence(string operation)
    {
        await using var services = await CreateServices();
        Capture(services, "job:work", correlationId: "test-correlation");
        Capture(services, "job:other", correlationId: "unrelated");
        await FlushAsync(services);
        var result = await new LogEntryMcpHandler(services).HandleAsync(new(operation, McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { correlationId = "test-correlation" })), CancellationToken.None);

        result.Available.ShouldBeTrue();
        Data(result).GetProperty("logsAvailable").GetBoolean().ShouldBeFalse();
        Data(result).GetProperty("profiling").GetProperty("available").GetBoolean().ShouldBeTrue();
        Data(result).GetProperty("profiling").GetProperty("data").GetProperty("totalCount").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_CorrelationWithUnavailableProfiling_PreservesLogsAndContinuation()
    {
        var logs = Substitute.For<ILogEntryService>();
        logs.QueryAsync(Arg.Any<LogEntryQueryRequest>(), Arg.Any<CancellationToken>()).Returns(new LogEntryQueryResponse { Items = [new() { Level = "Error", CorrelationId = "test" }], ContinuationToken = "more" });
        await using var services = new ServiceCollection().AddSingleton(logs).BuildServiceProvider();

        var result = await new LogEntryMcpHandler(services).HandleAsync(new("correlation.inspect", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { correlationId = "test" })), CancellationToken.None);

        result.Available.ShouldBeTrue();
        result.Truncated.ShouldBeTrue();
        Data(result).GetProperty("errors").GetArrayLength().ShouldBe(1);
        Data(result).GetProperty("continuationToken").GetString().ShouldBe("more");
        Data(result).GetProperty("profiling").GetProperty("available").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_CorrelationWithFailedLogRead_PreservesProfilingEvidence()
    {
        var logs = Substitute.For<ILogEntryService>();
        logs.QueryAsync(Arg.Any<LogEntryQueryRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<LogEntryQueryResponse>(new InvalidOperationException("private backend error")));
        await using var services = await CreateServices();
        Capture(services, "job:work", correlationId: "test");
        await FlushAsync(services);
        var registrations = new ServiceCollection();
        registrations.AddSingleton(logs);
        registrations.AddSingleton(services.GetRequiredService<IOperationProfilingQueryService>());
        await using var combined = registrations.BuildServiceProvider();

        var result = await new LogEntryMcpHandler(combined).HandleAsync(new("correlation.inspect", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { correlationId = "test" })), CancellationToken.None);

        result.Available.ShouldBeTrue();
        Data(result).GetProperty("logsAvailable").GetBoolean().ShouldBeFalse();
        Data(result).GetProperty("profiling").GetProperty("available").GetBoolean().ShouldBeTrue();
        JsonSerializer.Serialize(result).ShouldNotContain("private backend error");
    }

    [Fact]
    public async Task HandleAsync_CorrelationWithFailedProfilingRead_PreservesLogEvidence()
    {
        var logs = Substitute.For<ILogEntryService>();
        logs.QueryAsync(Arg.Any<LogEntryQueryRequest>(), Arg.Any<CancellationToken>()).Returns(new LogEntryQueryResponse { Items = [new() { Level = "Information", CorrelationId = "test" }] });
        var queries = Substitute.For<IOperationProfilingQueryService>();
        queries.QueryAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<IResult<OperationProfilingPage>>(new InvalidOperationException("private backend error")));
        await using var services = new ServiceCollection().AddSingleton(logs).AddSingleton(queries).BuildServiceProvider();

        var result = await new LogEntryMcpHandler(services).HandleAsync(new("correlation.inspect", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { correlationId = "test" })), CancellationToken.None);

        result.Available.ShouldBeTrue();
        Data(result).GetProperty("logs").GetArrayLength().ShouldBe(1);
        Data(result).GetProperty("profiling").GetProperty("code").GetString().ShouldBe("profiling_unavailable");
        JsonSerializer.Serialize(result).ShouldNotContain("private backend error");
    }

    [Fact]
    public async Task HandleAsync_Reads_DoNotCaptureOrForcePendingPersistence()
    {
        await using var services = await CreateServices();
        Capture(services, "job:pending");
        var queries = services.GetRequiredService<IOperationProfilingQueryService>();
        var before = queries.GetHealth();

        var result = await CallAsync(services, "query", "{}");

        result.Available.ShouldBeTrue();
        Data(result).GetProperty("totalCount").GetInt64().ShouldBe(0);
        queries.GetHealth().AdmittedOperations.ShouldBe(before.AdmittedOperations);
        queries.GetHealth().LastSuccessfulFlushUtc.ShouldBe(before.LastSuccessfulFlushUtc);
    }

    [Fact]
    public async Task HandleAsync_CanceledToken_PropagatesCancellation()
    {
        await using var services = await CreateServices();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () => await new ProfilingMcpHandler(services).HandleAsync(new("profiling.query", McpToolset.Diagnostics, JsonSerializer.SerializeToElement(new { })), cancellation.Token));
    }

    [Fact]
    public void DevKitBuilder_DefaultLocalMcp_RegistersProfilingDiagnostics()
    {
        var builder = DevKitWebApplication.CreateBuilder([], options => options.Cli(cli => cli.AllowOutsideDevelopmentForTests()));

        builder.Services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IMcpHandler) && descriptor.ImplementationType == typeof(ProfilingMcpHandler));
        new ProfilingMcpHandler(new ServiceCollection().BuildServiceProvider()).Capabilities.ShouldAllBe(capability => capability.Toolset == McpToolset.Diagnostics);
    }

    [Fact]
    public void McpToolCatalog_ProfilingSchemas_MatchesRuntimeCapabilities()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var catalog = new McpToolCatalog();
        foreach (var capability in new ProfilingMcpHandler(services).Capabilities)
        {
            var tool = catalog.Tools.Single(tool => tool.Name == "bdk_" + capability.Name.Replace('.', '_'));
            JsonSerializer.Serialize(tool.InputSchema).ShouldBe(JsonSerializer.Serialize(capability.ArgumentSchema));
            var schema = JsonSerializer.SerializeToElement(capability.ArgumentSchema);
            schema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
            schema.GetProperty("properties").EnumerateObject().ShouldNotBeEmpty();
        }

        var correlation = new LogEntryMcpHandler(services).Capabilities.Single(capability => capability.Name == "correlation.inspect");
        JsonSerializer.Serialize(correlation.ArgumentSchema).ShouldBe(JsonSerializer.Serialize(catalog.Tools.Single(tool => tool.Name == "bdk_correlation_inspect").InputSchema));
    }

    [Fact]
    public async Task McpToolExecutor_ProfilingTools_ForwardsOverLocalIpcToSharedRuntimeServices()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var directory = Directory.CreateTempSubdirectory("bdk-profiling-mcp-");
        await using var services = await CreateServices();
        var id = Capture(services, "job:work");
        await FlushAsync(services);
        var endpoints = new LocalIpcEndpointState(new() { RuntimeId = directory.Name, RegistryPath = directory.FullName });
        var endpoint = endpoints.GetOrCreate("mcp");
        var dispatcher = new McpDispatcher(services, services.GetRequiredService<ILogger<McpDispatcher>>());
        using var server = new McpIpcServer(endpoints, dispatcher, services.GetRequiredService<ILogger<McpIpcServer>>());
        await server.StartAsync(deadline.Token);
        try
        {
            var descriptor = new HostRuntimeDescriptor
            {
                RuntimeId = directory.Name, ApplicationName = "ProfilingTests", EnvironmentName = "Development", WorkspacePath = directory.FullName,
                ContentRootPath = directory.FullName, ProcessId = Environment.ProcessId, StartedAt = DateTimeOffset.UtcNow,
                Assembly = new() { Name = "ProfilingTests", Version = "1.0.0" }, Features = { ["mcp"] = endpoint }
            };
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, descriptor.RuntimeId + ".json"), JsonSerializer.Serialize(descriptor), deadline.Token);
            var registry = new HostRegistryOptions { RuntimePath = directory.FullName, SelectionPath = directory.FullName };
            var workspace = new WorkspaceResolver().Resolve(directory.FullName);
            var runtime = new McpRuntimeTools(new(workspace, new(), registry), new(registry), new(registry), new());
            var catalog = new McpToolCatalog();
            var executor = new McpToolExecutor(catalog, runtime, new(Substitute.For<IMcpDocumentationSource>()), new(Substitute.For<IMcpApiReferenceSource>()), new());
            var arguments = new Dictionary<string, object>
            {
                ["query"] = new { key = "job:work" }, ["details"] = new { id }, ["insights"] = new { kind = "Job" },
                ["compare"] = new { key = "job:work", kind = "Job", baseline = new { }, candidate = new { } }
            };
            foreach (var (name, argument) in arguments)
            {
                var tool = "bdk_profiling_" + name;
                catalog.Contains(tool).ShouldBeTrue();
                var result = await executor.ExecuteAsync(tool, JsonSerializer.SerializeToElement(argument), new(), deadline.Token);
                result.Available.ShouldBeTrue(result.Reason);
                Data(result).ValueKind.ShouldBe(JsonValueKind.Object);
            }

            var unauthorized = await dispatcher.DispatchAsync(new("profiling.query", McpToolset.Admin, JsonSerializer.SerializeToElement(new { })), deadline.Token);
            unauthorized.Code.ShouldBe(McpErrorCode.UnauthorizedToolset);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    private static async Task<ServiceProvider> CreateServices(int analysisLimit = 10000)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling(options => options.FlushInterval(TimeSpan.FromMinutes(1)));
        services.AddMcpHandler<ProfilingMcpHandler>();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ProfilingOptions>().Queries.MaximumAnalysisRecords = analysisLimit;
        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        provider.GetRequiredService<IOperationProfilingQueryService>().GetHealth().WriterActive.ShouldBeTrue();
        return provider;
    }

    private static Guid Capture(IServiceProvider services, string key, string kind = "Job", string region = "west", string method = null, bool failed = false, string correlationId = "test")
    {
        var profiler = services.GetRequiredService<IOperationProfiler>();
        using var operation = profiler.BeginOperation(new() { Key = key, Kind = kind, CorrelationId = correlationId });
        operation.IsRecording.ShouldBeTrue();
        operation.SetDimension("region", region);
        if (method is not null) { operation.SetHttpMetadata(new() { Method = method, Route = key, Path = "/cities/actual", StatusCode = 200 }); }

        using (var segment = operation.BeginSegment("Read")) { segment.Complete(); }

        using (var segment = operation.BeginSegment("Write")) { segment.Complete(); }

        if (failed) { operation.Fail(new ProfilingFailureDescriptor { Source = "Domain", Code = "Rejected" }); }
        else { operation.Complete(); }

        return operation.Id;
    }

    private static Task FlushAsync(IServiceProvider services) => services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
    private static JsonElement Data(McpResponse response) => JsonSerializer.SerializeToElement(response.Data, Json);
    private static async Task<McpResponse> CallAsync(IServiceProvider services, string operation, string arguments) =>
        await new ProfilingMcpHandler(services).HandleAsync(new("profiling." + operation, McpToolset.Diagnostics, JsonDocument.Parse(arguments).RootElement.Clone()), CancellationToken.None);
}
