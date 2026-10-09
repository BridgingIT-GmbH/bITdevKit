// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Compiles and executes the supported public setup and instrumentation examples.</summary>
/// <example>Run with the Profiling test filter before changing the feature guides.</example>
public sealed class ProfilingPublicApiExamplesTests
{
    /// <summary>Minimal API and controller injection enrich the single HTTP owner and preserve the response.</summary>
    [Theory]
    [InlineData("minimal", "catalog:read")]
    [InlineData("controller", "catalog:controller")]
    public async Task Http_Injection_EnrichesMiddlewareOwner(string path, string key)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProfilingExampleController).Assembly);
        builder.Services.AddProfiling(options => options.Enabled())
            .WithOperationProfiling()
            .WithRequestProfiling(options => options.StripPathPrefix("/api"));
        await using var app = builder.Build();
        app.UseRequestProfiling();
        app.UseRequestProfilingExceptionObserver();
        app.UseRouting();
        app.MapControllers();
        app.MapGet("/api/examples/minimal", async (IOperationProfiler profiling, CancellationToken token) =>
        {
            profiling.SetKey("catalog:read");
            profiling.SetDimension("itemCount", 3);
            var result = await profiling.RunSegmentAsync("Query", (_, _) => Task.FromResult(Result<int>.Success(3)),
                token, value => OperationProfilingHelpers.ClassifyResult(value));
            return Microsoft.AspNetCore.Http.Results.Ok(result.Value);
        });
        await app.StartAsync();
        var response = await app.GetTestClient().GetAsync("/api/examples/" + path);
        response.EnsureSuccessStatusCode();
        (await response.Content.ReadAsStringAsync()).ShouldBe("3");
        var id = Guid.Parse(response.Headers.GetValues(RequestProfilingMiddleware.HeaderName).Single());
        var record = await StoredAsync(app.Services, id);

        record.Key.ShouldBe(key);
        record.Kind.ShouldBe(OperationProfilingKind.HttpRequest.ToString());
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Dimensions.Single(dimension => dimension.Key == "itemCount").Value.Type.ShouldBe(ProfilingValueType.Int64);
        record.Segments.Single().Path.Components.ShouldBe(["Query"]);
        record.Http.Method.ShouldBe("GET");
        record.Http.StatusCode.ShouldBe(200);
        record.Http.ResponseBytes.ShouldNotBeNull();
        record.Http.ResponseBytes.Value.ShouldBeGreaterThan(0);
    }

    /// <summary>Service and Blazor-style interaction scopes aggregate loops and nested segments without HTTP.</summary>
    [Fact]
    public async Task Service_ExplicitScopes_AggregateRepeatedNestedWork()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        var id = Guid.Empty;
        var value = await profiling.RunOperationAsync("catalog:refresh", OperationProfilingKind.Service, async (operation, token) =>
        {
            id = operation.Id;
            operation.SetDimension("source", "catalog");
            for (var index = 0; index < 3; index++)
            {
                using var load = operation.BeginSegment("Load");
                load.SetMeasurement("items", 10, "count", MeasurementAggregation.Sum);
                await load.RunSegmentAsync("Transform", (_, _) => Task.CompletedTask, token);
                load.Complete();
            }

            return Result<int>.Success(30);
        }, classify: result => OperationProfilingHelpers.ClassifyResult(result));
        value.Value.ShouldBe(30);
        var record = await StoredAsync(provider, id);
        record.Http.ShouldBeNull();
        record.Segments.Single(segment => segment.Key == "Load").Statistics.Count.ShouldBe(3);
        record.Segments.Single(segment => segment.Key == "Transform").Statistics.Count.ShouldBe(3);
        record.Segments.Single(segment => segment.Key == "Transform").Path.Components.ShouldBe(["Load", "Transform"]);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    /// <summary>Memory and EF providers share the same facade, and inherited Set requires no property boilerplate.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Providers_FluentAlternatives_ResolveIndependentFacets(bool entityFramework)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var setup = services.AddProfiling(options => options.Enabled()).WithRuntimeProfiling().WithOperationProfiling();
        if (entityFramework)
        {
            // Model/setup proof only; the real engine contract suite exercises database I/O.
            services.AddDbContext<ProfilingExampleDbContext>(options => options.UseSqlite("Data Source=:memory:"));
            setup.WithEntityFrameworkProvider<ProfilingExampleDbContext>();
        }
        else { setup.WithInMemoryProvider(); }

        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProfilingStorageProvider>().ShouldNotBeNull();
        provider.GetRequiredService<IRuntimeProfilingStore>().ShouldNotBeNull();
        provider.GetRequiredService<IOperationProfilingStore>().ShouldNotBeNull();
        if (entityFramework)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ProfilingExampleDbContext>();
            context.Set<RuntimeProfilingSnapshotEntity>().EntityType.ShouldNotBeNull();
            context.Set<OperationProfilingEntity>().EntityType.FindProperty(nameof(OperationProfilingEntity.RecordJson)).ShouldNotBeNull();
        }
    }

    /// <summary>Sampling is all requests by default and switchable through a shared singleton strategy.</summary>
    [Theory]
    [InlineData("AllRequests")]
    [InlineData("Probability")]
    [InlineData("RateLimit")]
    public void Sampling_FluentSelection_UsesSingleton(string strategy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling().WithRequestProfiling(options =>
        {
            if (strategy == "Probability") { options.WithSampling(sampling => sampling.Probability(0.1)); }
            else if (strategy == "RateLimit") { options.WithSampling(sampling => sampling.RateLimit(25, 50)); }
        });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<RequestProfilingOptions>().StrategyKey.ShouldBe(strategy);
        provider.GetRequiredService<RequestProfilingRuntime>().Sampler.ShouldBeSameAs(provider.GetRequiredService<RequestProfilingRuntime>().Sampler);
    }

    /// <summary>Feature behaviors construct without profiling registration and do not register it implicitly.</summary>
    [Fact]
    public void Behaviors_ProfilingOmitted_ConstructWithOptionalDependency()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPipelines().WithPipeline<ProfilingExamplePipelineContext>("catalog-import", definition =>
            definition.AddStep(() => { }).AddBehavior<PipelineProfilingBehavior>());
        using var provider = services.BuildServiceProvider();
        ActivatorUtilities.CreateInstance<JobProfilingBehavior>(provider).ShouldNotBeNull();
        ActivatorUtilities.CreateInstance<PipelineProfilingBehavior>(provider).ShouldNotBeNull();
        ActivatorUtilities.CreateInstance<OrchestrationProfilingBehavior>(provider).ShouldNotBeNull();
        provider.GetService<IOperationProfiler>().ShouldBeNull();
    }

    private static async Task<OperationProfilingRecord> StoredAsync(IServiceProvider provider, Guid id)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
            var result = await provider.GetRequiredService<IOperationProfilingStore>().FindAsync(id);
            if (result.IsSuccess && result.Value is not null) { return result.Value; }

            await Task.Delay(5);
        }

        throw new InvalidOperationException("Example completion was not persisted within its bounded wait.");
    }
}

/// <summary>Demonstrates controller injection enriching the middleware-owned operation.</summary>
/// <example>GET /api/examples/controller returns 3 and stores a Query segment.</example>
[ApiController]
[Route("api/examples/controller")]
public sealed class ProfilingExampleController(IOperationProfiler profiling) : ControllerBase
{
    /// <summary>Captures typed dimensions without recording the result payload.</summary>
    /// <example>GET /api/examples/controller</example>
    [HttpGet]
    public async Task<ActionResult<int>> Get(CancellationToken token)
    {
        profiling.SetKey("catalog:controller");
        profiling.SetDimension("itemCount", 3);
        var result = await profiling.RunSegmentAsync("Query", (_, _) => Task.FromResult(Result<int>.Success(3)),
            token, value => OperationProfilingHelpers.ClassifyResult(value));
        return this.Ok(result.Value);
    }
}

/// <summary>Demonstrates an application-owned EF model with JSON aggregates and inherited set access.</summary>
/// <example>Register with AddDbContext and WithEntityFrameworkProvider.</example>
public sealed class ProfilingExampleDbContext(DbContextOptions<ProfilingExampleDbContext> options) : DbContext(options), IProfilingDbContext
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureProfiling();
    }
}

/// <summary>Supplies the typed context required by the inline pipeline registration example.</summary>
/// <example>Use WithPipeline&lt;ProfilingExamplePipelineContext&gt;("catalog-import", configure).</example>
public sealed class ProfilingExamplePipelineContext : PipelineContextBase;
