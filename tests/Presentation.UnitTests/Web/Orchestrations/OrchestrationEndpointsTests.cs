
using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Orchestrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;
internal sealed class TestDefinitionData : IOrchestrationData
{
}

internal sealed class TestDefinitionOrchestration : Orchestration<TestDefinitionData>
{
    protected override void Define(IOrchestrationBuilder<TestDefinitionData> builder)
    {
        builder
            .State("Created", state => state.TransitionTo("Done"))
            .State("Done", state => state.Complete());
    }
}

public class OrchestrationEndpointsApplication(ITestOutputHelper output) : WebApplicationFactory<OrchestrationEndpointsTests>
{
    public IOrchestrationService OrchestrationService { get; } = Substitute.For<IOrchestrationService>();

    public IOrchestrationQueryService OrchestrationQueryService { get; } = Substitute.For<IOrchestrationQueryService>();

    public IOrchestrationAdministrationService OrchestrationAdministrationService { get; } = Substitute.For<IOrchestrationAdministrationService>();

    public IOrchestrationDiagramService OrchestrationDiagramService { get; } = Substitute.For<IOrchestrationDiagramService>();

    public OrchestrationRegistrationStore Registrations { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        appBuilder.Services.AddTransient<TestDefinitionOrchestration>();
        appBuilder.Services.AddSingleton(this.OrchestrationService);
        appBuilder.Services.AddSingleton(this.OrchestrationQueryService);
        appBuilder.Services.AddSingleton(this.OrchestrationAdministrationService);
        appBuilder.Services.AddSingleton(this.OrchestrationDiagramService);
        appBuilder.Services.AddSingleton(this.Registrations);
        appBuilder.Services.AddSingleton(new OrchestrationEndpointsOptions());
        appBuilder.Services.AddOrchestrationEndpoints();

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }
}

public class OrchestrationEndpointsTests : IAsyncDisposable
{
    private readonly OrchestrationEndpointsApplication factory;
    private readonly HttpClient client;
    private readonly IOrchestrationService orchestrationService;
    private readonly IOrchestrationQueryService orchestrationQueryService;
    private readonly IOrchestrationAdministrationService orchestrationAdministrationService;
    private readonly IOrchestrationDiagramService orchestrationDiagramService;
    private readonly OrchestrationRegistrationStore registrations;

    public OrchestrationEndpointsTests(ITestOutputHelper output)
    {
        this.factory = new OrchestrationEndpointsApplication(output);
        this.client = this.factory.CreateClient();
        this.orchestrationService = this.factory.OrchestrationService;
        this.orchestrationQueryService = this.factory.OrchestrationQueryService;
        this.orchestrationAdministrationService = this.factory.OrchestrationAdministrationService;
        this.orchestrationDiagramService = this.factory.OrchestrationDiagramService;
        this.registrations = this.factory.Registrations;
    }

    public async ValueTask DisposeAsync()
    {
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetInstances_ShouldReturnPagedResult()
    {
        this.orchestrationQueryService.QueryAsync(Arg.Any<OrchestrationQueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<OrchestrationInstanceModel>.Success(
            [
                new OrchestrationInstanceModel
                {
                    InstanceId = Guid.NewGuid(),
                    OrchestrationName = "OrderApproval",
                    Status = nameof(OrchestrationStatus.Waiting),
                    CurrentState = "AwaitingApproval",
                    CorrelationId = "corr-1",
                    StartedUtc = DateTimeOffset.UtcNow,
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                }
            ],
            count: 1,
            page: 1,
            pageSize: 50));

        var response = await this.client.GetAsync("/_bdk/api/orchestrations?statuses=Waiting&take=50");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldContain("OrderApproval");
        content.ShouldContain("AwaitingApproval");
    }

    [Fact]
    public async Task GetInstances_WithInvalidStatus_ShouldReturnBadRequestProblem()
    {
        this.orchestrationQueryService.QueryAsync(Arg.Any<OrchestrationQueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<OrchestrationInstanceModel>.Failure().WithError(new Error("Unknown orchestration status 'Bogus'.")));

        var response = await this.client.GetAsync("/_bdk/api/orchestrations?statuses=Bogus");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Type.ShouldBe("/problems/orchestrations/validation");
    }

    [Fact]
    public async Task GetInstance_WhenNotFound_ShouldReturnPlainTextNotFound()
    {
        var instanceId = Guid.NewGuid();
        this.orchestrationQueryService.GetAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<OrchestrationInstanceModel>.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found.")));

        var response = await this.client.GetAsync($"/_bdk/api/orchestrations/{instanceId}");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        content.ShouldContain(instanceId.ToString());
    }

    [Fact]
    public async Task GetDefinitions_ShouldReturnRegisteredDefinitionNames()
    {
        this.registrations.Add(typeof(TestDefinitionOrchestration));

        var response = await this.client.GetAsync("/_bdk/api/orchestrations/definitions");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldContain(nameof(TestDefinitionOrchestration));
    }

    [Fact]
    public async Task GetDefinitionDiagram_ShouldReturnPlainTextDiagram()
    {
        this.orchestrationDiagramService.GetDefinitionDiagramAsync(nameof(TestDefinitionOrchestration), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("stateDiagram-v2\n    [*] --> Created"));

        var response = await this.client.GetAsync($"/_bdk/api/orchestrations/definitions/{nameof(TestDefinitionOrchestration)}/diagram");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
        content.ShouldContain("stateDiagram-v2");
    }

    [Fact]
    public async Task GetDefinitionDiagramSvg_ShouldReturnSvgMarkup()
    {
        this.orchestrationDiagramService.GetDefinitionDiagramAsync(nameof(TestDefinitionOrchestration), DiagramRenderFormat.Svg, Arg.Any<CancellationToken>())
            .Returns(Result<DiagramRenderResult>.Success(DiagramRenderResult.FromText(DiagramRenderFormat.Svg, "<svg aria-label=\"State diagram\"></svg>")));

        var response = await this.client.GetAsync($"/_bdk/api/orchestrations/definitions/{nameof(TestDefinitionOrchestration)}/diagram/svg");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/svg+xml");
        content.ShouldContain("<svg");
    }

    [Fact]
    public async Task GetInstanceDiagram_WhenNotFound_ShouldReturnPlainTextNotFound()
    {
        var instanceId = Guid.NewGuid();
        this.orchestrationDiagramService.GetInstanceDiagramAsync(instanceId, Arg.Any<OrchestrationDiagramOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found.")));

        var response = await this.client.GetAsync($"/_bdk/api/orchestrations/{instanceId}/diagram");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        content.ShouldContain(instanceId.ToString());
    }

    [Fact]
    public async Task GetInstanceDiagramSvg_WhenNotFound_ShouldReturnPlainTextNotFound()
    {
        var instanceId = Guid.NewGuid();
        this.orchestrationDiagramService.GetInstanceDiagramAsync(instanceId, DiagramRenderFormat.Svg, Arg.Any<OrchestrationDiagramOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result<DiagramRenderResult>.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found.")));

        var response = await this.client.GetAsync($"/_bdk/api/orchestrations/{instanceId}/diagram/svg");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        content.ShouldContain(instanceId.ToString());
    }

    [Fact]
    public async Task Signal_WhenRequestIsInvalid_ShouldReturnBadRequestProblem()
    {
        var instanceId = Guid.NewGuid();

        var response = await this.client.PostAsJsonAsync($"/_bdk/api/orchestrations/{instanceId}/signal", new { signalName = "" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Type.ShouldBe("/problems/orchestrations/validation");
    }

    [Fact]
    public async Task Pause_WhenInstanceIsAlreadyPaused_ShouldReturnConflictProblem()
    {
        var instanceId = Guid.NewGuid();
        this.orchestrationService.PauseAsync(instanceId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is already paused.")));

        var response = await this.client.PostAsJsonAsync($"/_bdk/api/orchestrations/{instanceId}/pause", new { reason = "hold" });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        problem.ShouldNotBeNull();
        problem.Type.ShouldBe("/problems/orchestrations/invalid-state");
    }

    [Fact]
    public async Task Archive_ShouldInvokeAdministrationService()
    {
        var instanceId = Guid.NewGuid();
        this.orchestrationAdministrationService.ArchiveAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success($"Orchestration instance '{instanceId}' was archived."));

        var response = await this.client.PostAsync($"/_bdk/api/orchestrations/{instanceId}/archive", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.orchestrationAdministrationService.Received(1).ArchiveAsync(instanceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseLease_WhenInstanceIsMissing_ShouldReturnNotFound()
    {
        var instanceId = Guid.NewGuid();
        this.orchestrationAdministrationService.ReleaseLeaseAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found.")));

        var response = await this.client.PostAsync($"/_bdk/api/orchestrations/{instanceId}/repair/release-lease", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purge_ShouldPassFiltersToAdministrationService()
    {
        this.orchestrationAdministrationService.PurgeAsync(Arg.Any<OrchestrationPurgeRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrchestrationPurgeResult>.Success(new OrchestrationPurgeResult { PurgedInstanceCount = 1 }));

        var response = await this.client.DeleteAsync("/_bdk/api/orchestrations?olderThan=2026-01-01T00:00:00Z&statuses=Completed&isArchived=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this.orchestrationAdministrationService.Received(1).PurgeAsync(
            Arg.Is<OrchestrationPurgeRequest>(request =>
                request.OlderThan == DateTimeOffset.Parse("2026-01-01T00:00:00Z") &&
                request.Statuses.SequenceEqual(new[] { nameof(OrchestrationStatus.Completed) }) &&
                request.IsArchived == true),
            Arg.Any<CancellationToken>());
    }
}
