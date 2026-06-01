
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.UnitTests.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;
public sealed class OrchestrationDiagramTests : OrchestrationTestBase, IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IOrchestrationDiagramService diagramService;
    private readonly IOrchestrationQueryService queryService;
    private readonly IOrchestrationService orchestrationService;
    private readonly OrchestrationDefinitionDiagramProjector definitionProjector;
    private readonly OrchestrationInstanceDiagramProjector instanceProjector;
    private readonly IDiagramRendererFactory rendererFactory;

    public OrchestrationDiagramTests(ITestOutputHelper output)
        : base(output)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddOrchestrations()
            .WithOrchestration<OrderApprovalOrchestration>()
            .WithOrchestration<TelephoneCallOrchestration>()
            .WithOrchestration<ParallelDiagramOrchestration>()
            .WithOrchestration<TimeoutDiagramOrchestration>();

        this.provider = services.BuildServiceProvider();
        this.diagramService = this.provider.GetRequiredService<IOrchestrationDiagramService>();
        this.queryService = this.provider.GetRequiredService<IOrchestrationQueryService>();
        this.orchestrationService = this.provider.GetRequiredService<IOrchestrationService>();
        this.definitionProjector = this.provider.GetRequiredService<OrchestrationDefinitionDiagramProjector>();
        this.instanceProjector = this.provider.GetRequiredService<OrchestrationInstanceDiagramProjector>();
        this.rendererFactory = this.provider.GetRequiredService<IDiagramRendererFactory>();
    }

    public void Dispose()
    {
        this.provider.Dispose();
    }

    [Fact]
    public async Task GetDefinitionDiagramAsync_OrderApproval_ShouldRenderExpectedText()
    {
        var result = await this.diagramService.GetDefinitionDiagramAsync(nameof(OrderApprovalOrchestration));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(
            "stateDiagram-v2\n" +
            "    [*] --> Created\n" +
            "    Created --> AwaitingApproval\n" +
            "    Created --> PaymentReservation\n" +
            "    AwaitingApproval --> PaymentReservation: signal_OrderApproved\n" +
            "    AwaitingApproval --> Rejected: signal_OrderRejected\n" +
            "    AwaitingApproval --> Rejected: timeout\n" +
            "    PaymentReservation --> Confirmed\n" +
            "    Confirmed --> [*]\n" +
            "    Rejected --> [*]");
    }

    [Fact]
    public async Task GetDefinitionDiagramAsync_TelephoneCall_ShouldRenderExpectedText()
    {
        var result = await this.diagramService.GetDefinitionDiagramAsync(nameof(TelephoneCallOrchestration));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(
            "stateDiagram-v2\n" +
            "    [*] --> OffHook\n" +
            "    OffHook --> Ringing: signal_CallDialed\n" +
            "    Ringing --> OffHook: signal_HungUp\n" +
            "    Ringing --> Connected: signal_CallConnected\n" +
            "    Connected --> OffHook: signal_LeftMessage\n" +
            "    Connected --> OffHook: signal_HungUp\n" +
            "    Connected --> OnHold: signal_PlacedOnHold\n" +
            "    OnHold --> Connected: signal_TakenOffHold\n" +
            "    OnHold --> OffHook: signal_HungUp\n" +
            "    OnHold --> PhoneDestroyed: signal_PhoneHurledAgainstWall\n" +
            "    PhoneDestroyed --> [*]");
    }

    [Fact]
    public async Task GetDefinitionDiagramAsync_WhenSvgRequested_ShouldRenderSvgPayload()
    {
        var result = await this.diagramService.GetDefinitionDiagramAsync(nameof(OrderApprovalOrchestration), DiagramRenderFormat.Svg);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Format.ShouldBe(DiagramRenderFormat.Svg);
        result.Value.ContentType.ShouldBe("image/svg+xml; charset=utf-8");
        result.Value.GetText().ShouldContain("<svg");
        result.Value.GetText().ShouldContain("aria-label=\"State diagram\"");
    }

    [Fact]
    public async Task GetInstanceDiagramAsync_WhenApprovalIsPending_ShouldHighlightCurrentState()
    {
        var dispatch = await this.orchestrationService.DispatchAndWaitAsync<OrderApprovalOrchestration, OrderApprovalData>(
            new OrderApprovalData
            {
                OrderId = "42",
                CustomerId = "customer-1",
                OrderAmount = 150m,
            },
            WaitFor.State("AwaitingApproval"),
            TimeSpan.FromSeconds(1));

        var result = await this.diagramService.GetInstanceDiagramAsync(dispatch.Value.InstanceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(
            "stateDiagram-v2\n" +
            "    [*] --> Created\n" +
            "    Created --> AwaitingApproval\n" +
            "    Created --> PaymentReservation\n" +
            "    AwaitingApproval --> PaymentReservation: signal_OrderApproved\n" +
            "    AwaitingApproval --> Rejected: signal_OrderRejected\n" +
            "    AwaitingApproval --> Rejected: timeout\n" +
            "    PaymentReservation --> Confirmed\n" +
            "    Confirmed --> [*]\n" +
            "    Rejected --> [*]");
        result.Value.ShouldContain("note right of AwaitingApproval");
        result.Value.ShouldContain("current_state");
        result.Value.ShouldContain("visited: Created -> AwaitingApproval");
    }

    [Fact]
    public async Task GetInstanceDiagramAsync_WhenOrderIsApproved_ShouldRenderSignalDrivenPath()
    {
        var instanceId = await DispatchToAwaitingApprovalAsync(this.orchestrationService);

        (await this.orchestrationService.SignalAsync(instanceId, "OrderApproved", new OrderApprovedSignal { ApprovedBy = "alice" })).IsSuccess.ShouldBeTrue();
        await WaitForStatusAsync(this.queryService, instanceId, nameof(OrchestrationStatus.Completed));

        var result = await this.diagramService.GetInstanceDiagramAsync(instanceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("Created --> PaymentReservation");
        result.Value.ShouldContain("AwaitingApproval --> PaymentReservation: signal_OrderApproved");
        result.Value.ShouldContain("AwaitingApproval --> Rejected: signal_OrderRejected");
        result.Value.ShouldContain("Confirmed --> [*]");
        result.Value.ShouldContain("note right of Confirmed");
        result.Value.ShouldContain("current_state");
        result.Value.ShouldContain("visited: Created -> AwaitingApproval -> PaymentReservation -> Confirmed");
    }

    [Fact]
    public async Task GetInstanceDiagramAsync_WhenOrderIsRejected_ShouldRenderTerminalPath()
    {
        var instanceId = await DispatchToAwaitingApprovalAsync(this.orchestrationService);

        (await this.orchestrationService.SignalAsync(instanceId, "OrderRejected", new OrderRejectedSignal { Reason = "rejected" })).IsSuccess.ShouldBeTrue();
        await WaitForStatusAsync(this.queryService, instanceId, nameof(OrchestrationStatus.Terminated));

        var result = await this.diagramService.GetInstanceDiagramAsync(instanceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("Created --> PaymentReservation");
        result.Value.ShouldContain("AwaitingApproval --> Rejected: signal_OrderRejected");
        result.Value.ShouldContain("Rejected --> [*]");
        result.Value.ShouldContain("note right of Rejected");
        result.Value.ShouldContain("current_state");
        result.Value.ShouldContain("visited: Created -> AwaitingApproval -> Rejected");
    }

    [Fact]
    public void Project_InstanceDiagram_WhenTimeoutHistoryExists_ShouldRenderTimeoutEdge()
    {
        var definition = this.definitionProjector.Project(new TimeoutDiagramOrchestration().GetDefinition());
        var document = this.instanceProjector.Project(
            definition,
            new OrchestrationInstanceModel
            {
                InstanceId = Guid.NewGuid(),
                CurrentState = "Expired",
                Status = nameof(OrchestrationStatus.Terminated),
            },
            [
                new OrchestrationHistoryModel { EventType = "StateEntered", State = "Created" },
                new OrchestrationHistoryModel { EventType = "StateEntered", State = "Waiting" },
                new OrchestrationHistoryModel { EventType = "TimerConsumed", State = "Waiting", Message = "StateTimeout" },
                new OrchestrationHistoryModel { EventType = "StateEntered", State = "Expired" },
                new OrchestrationHistoryModel { EventType = "Terminated", State = "Expired" },
            ],
            [],
            [],
            new OrchestrationDiagramOptions());

        var result = this.rendererFactory.Render(document, DiagramRenderFormat.Mermaid).GetText();

        result.ShouldContain("Waiting --> Expired: timeout");
        result.ShouldContain("Expired --> [*]");
    }

    [Fact]
    public void Project_DefinitionDiagram_WhenParallelDefinitionExists_ShouldRenderParallelBranches()
    {
        var document = this.definitionProjector.Project(new ParallelDiagramOrchestration().GetDefinition());

        var result = this.rendererFactory.Render(document, DiagramRenderFormat.Mermaid).GetText();

        result.ShouldBe(
            "stateDiagram-v2\n" +
            "    [*] --> Created\n" +
            "    Created --> Parallel_JoinWork_Left: branch_Left\n" +
            "    Parallel_JoinWork_Left --> ParallelJoin_JoinWork: completed\n" +
            "    Created --> Parallel_JoinWork_Right: branch_Right\n" +
            "    Parallel_JoinWork_Right --> ParallelJoin_JoinWork: completed\n" +
            "    ParallelJoin_JoinWork --> [*]: join_all");
    }

    [Fact]
    public void Project_InstanceDiagram_WhenOnlyOneParallelBranchStarted_ShouldRenderOnlyStartedBranch()
    {
        var definition = this.definitionProjector.Project(new ParallelDiagramOrchestration().GetDefinition());
        var document = this.instanceProjector.Project(
            definition,
            new OrchestrationInstanceModel
            {
                InstanceId = Guid.NewGuid(),
                CurrentState = "Created",
                Status = nameof(OrchestrationStatus.Completed),
            },
            [
                new OrchestrationHistoryModel { EventType = "StateEntered", State = "Created" },
                new OrchestrationHistoryModel { EventType = "ParallelBranchActivityExecuted", State = "Created", Activity = "JoinWork", Message = "Left" },
                new OrchestrationHistoryModel { EventType = "ParallelBranchCompleted", State = "Created", Activity = "JoinWork", Message = "Left" },
                new OrchestrationHistoryModel { EventType = "ParallelJoinResolved", State = "Created", Activity = "JoinWork", Message = "All" },
                new OrchestrationHistoryModel { EventType = "Completed", State = "Created" },
            ],
            [],
            [],
            new OrchestrationDiagramOptions());

        var result = this.rendererFactory.Render(document, DiagramRenderFormat.Mermaid).GetText();

        result.ShouldContain("Created --> Parallel_JoinWork_Left: branch_Left");
        result.ShouldContain("Parallel_JoinWork_Left --> ParallelJoin_JoinWork: completed");
        result.ShouldContain("Created --> Parallel_JoinWork_Right: branch_Right");
        result.ShouldContain("Parallel_JoinWork_Right --> ParallelJoin_JoinWork: completed");
    }

    private static async Task<Guid> DispatchToAwaitingApprovalAsync(IOrchestrationService orchestrationService)
    {
        var dispatch = await orchestrationService.DispatchAndWaitAsync<OrderApprovalOrchestration, OrderApprovalData>(
            new OrderApprovalData
            {
                OrderId = "42",
                CustomerId = "customer-1",
                OrderAmount = 150m,
            },
            WaitFor.State("AwaitingApproval"),
            TimeSpan.FromSeconds(1));

        dispatch.IsSuccess.ShouldBeTrue();
        return dispatch.Value.InstanceId;
    }

    private static async Task WaitForStatusAsync(IOrchestrationQueryService queryService, Guid instanceId, string status)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var snapshot = await queryService.GetAsync(instanceId);
            if (snapshot.IsSuccess && string.Equals(snapshot.Value.Status, status, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new InvalidOperationException($"Orchestration instance '{instanceId}' did not reach status '{status}'.");
    }

    private sealed class ParallelDiagramData : IOrchestrationData
    {
        public int LeftCount { get; set; }

        public int RightCount { get; set; }
    }

    private sealed class ParallelDiagramOrchestration : Orchestration<ParallelDiagramData>
    {
        protected override void Define(IOrchestrationBuilder<ParallelDiagramData> builder)
        {
            builder.State("Created", state => state
                .Parallel(parallel => parallel
                    .Branch("Left", branch => branch.Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()), "LeftStep"))
                    .Branch("Right", branch => branch.Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()), "RightStep"))
                    .JoinAll(),
                    "JoinWork")
                .Complete());
        }
    }

    private sealed class TimeoutDiagramData : IOrchestrationData
    {
    }

    private sealed class TimeoutDiagramOrchestration : Orchestration<TimeoutDiagramData>
    {
        protected override void Define(IOrchestrationBuilder<TimeoutDiagramData> builder)
        {
            builder
                .State("Created", state => state.TransitionTo("Waiting"))
                .State("Waiting", state => state.TimeoutAfter(TimeSpan.FromSeconds(1)).TransitionTo("Expired"))
                .State("Expired", state => state.Terminate());
        }
    }
}
