// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console;

public sealed class ProfilingConsoleCommandTests
{
    [Fact]
    public void AddConsoleCommands_WhenCalledRepeatedly_RegistersOneOfEachProfilingCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddProfiling();

        // Act
        context.AddConsoleCommands();
        context.AddConsoleCommands();
        using var provider = services.BuildServiceProvider();
        var commands = provider
            .GetServices<IConsoleCommand>()
            .OfType<IGroupedConsoleCommand>()
            .Where(command => command.GroupName == "profiling")
            .ToArray();

        // Assert
        commands
            .Select(command => command.Name)
            .ShouldBe(
                ["status", "start", "stop", "snapshot", "gc", "mark", "clear", "analyze", "export", "import"],
                ignoreOrder: true
            );
        commands.ShouldAllBe(command => command.GroupAliases.Contains("prof"));
    }

    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("2s", 2000)]
    [InlineData("1.5m", 90000)]
    [InlineData("1h", 3600000)]
    [InlineData("00:00:30", 30000)]
    public void DurationParser_WithSupportedValue_ParsesExpectedDuration(
        string value,
        double expectedMilliseconds
    )
    {
        // Act
        var parsed = ProfilingDurationParser.TryParse(value, out var duration);

        // Assert
        parsed.ShouldBeTrue();
        duration.TotalMilliseconds.ShouldBe(expectedMilliseconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("30seconds")]
    [InlineData("NaNms")]
    [InlineData("1e300h")]
    public void DurationParser_WithUnsupportedValue_ReturnsFalse(string value)
    {
        ProfilingDurationParser.TryParse(value, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Start_WithFriendlyOptions_DelegatesParsedOverridesToCoreService()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        ProfilingStartRequest capturedRequest = null;
        control
            .StartAsync(Arg.Do<ProfilingStartRequest>(request => capturedRequest = request), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(new(CreateSession(), true, [])));
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "prof start --name warm-up --interval 500ms --duration 30s",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Name.ShouldBe("warm-up");
        capturedRequest.SamplingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
        capturedRequest.Duration.ShouldBe(TimeSpan.FromSeconds(30));
        writer.ToString().ShouldContain("sess0001");
    }

    [Fact]
    public async Task Start_WithoutOptions_LeavesDefaultsForCoreService()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        ProfilingStartRequest capturedRequest = null;
        control
            .StartAsync(Arg.Do<ProfilingStartRequest>(request => capturedRequest = request), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingControlResult>.Success(new(CreateSession(), true, [])));
        using var provider = CreateProvider(control: control);
        var (console, _) = CreateConsole();

        // Act
        await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling start",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Name.ShouldBeNull();
        capturedRequest.SamplingInterval.ShouldBeNull();
        capturedRequest.Duration.ShouldBeNull();
    }

    [Fact]
    public async Task Start_WithInvalidDuration_FailsBeforeCallingCoreService()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling start --duration invalid",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeFalse();
        writer.ToString().ShouldContain("Invalid --duration");
        await control.DidNotReceive().StartAsync(Arg.Any<ProfilingStartRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Analyze_WithOneSnapshotOption_IsRejectedWithoutEvaluation()
    {
        // Arrange
        var queries = Substitute.For<IProfilingQueryService>();
        using var provider = CreateProvider(queries: queries);
        var (console, writer) = CreateConsole();

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling analyze --session sess0001 --node node0001 --snapshot-a snap0001",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeFalse();
        writer.ToString().ShouldContain("must be supplied together");
        await queries.DidNotReceive().EvaluateAsync(Arg.Any<ProfilingEvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, null, ProfilingEvaluationMode.NodeSession)]
    [InlineData("snap0001", "snap0002", ProfilingEvaluationMode.TwoSnapshots)]
    public async Task Analyze_WithValidSelection_DelegatesTimelineOrPair(
        string snapshotA,
        string snapshotB,
        ProfilingEvaluationMode expectedMode
    )
    {
        // Arrange
        var queries = Substitute.For<IProfilingQueryService>();
        ProfilingEvaluationRequest capturedRequest = null;
        queries
            .EvaluateAsync(Arg.Do<ProfilingEvaluationRequest>(request => capturedRequest = request), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingEvaluationResult>.Success(CreateEvaluation(expectedMode)));
        using var provider = CreateProvider(queries: queries);
        var (console, writer) = CreateConsole();
        var commandLine = "profiling analyze --session sess0001 --node node0001"
            + (snapshotA is null ? string.Empty : $" --snapshot-a {snapshotA} --snapshot-b {snapshotB}");

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            commandLine,
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeTrue();
        capturedRequest.ShouldBe(new("sess0001", "node0001", snapshotA, snapshotB));
        writer.ToString().ShouldContain(expectedMode.ToString());
    }

    [Fact]
    public async Task Analyze_WithJson_WritesExactComputedContractWithoutPersistingIt()
    {
        // Arrange
        var evaluation = CreateEvaluation(ProfilingEvaluationMode.NodeSession);
        var queries = Substitute.For<IProfilingQueryService>();
        queries
            .EvaluateAsync(Arg.Any<ProfilingEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingEvaluationResult>.Success(evaluation));
        var services = new ServiceCollection().AddSingleton(queries).BuildServiceProvider();
        var (console, writer) = CreateConsole();
        var command = new ProfilingAnalyzeConsoleCommand
        {
            SessionKey = "sess0001",
            NodeKey = "node0001",
            Json = true,
        };

        // Act
        await command.ExecuteAsync(console, services);

        // Assert
        var expected = JsonSerializer.Serialize(
            evaluation,
            BridgingIT.DevKit.Common.DefaultJsonSerializerOptions.Create()
        );
        writer.ToString().TrimEnd().ShouldBe(expected);
        await queries.Received(1).EvaluateAsync(
            new ProfilingEvaluationRequest("sess0001", "node0001"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExportAndImport_WithFiles_DelegateToArchiveServiceAndReportImportedSession()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"bitdevkit-prof-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var exportPath = Path.Combine(directory, "session.json");
        var archives = Substitute.For<IProfilingArchiveService>();
        archives
            .ExportSessionAsync("sess0001", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<Stream>(1).Write("{}"u8);
                return Result.Success();
            });
        archives
            .ImportAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingArchiveImportResult>.Success(
                    new("newsess1", new Dictionary<string, string>(), new Dictionary<string, string>())
                )
            );
        using var provider = CreateProvider(archives: archives);
        var (console, writer) = CreateConsole();

        try
        {
            // Act
            var exportResult = await new ConsoleCommandExecutor().ExecuteAsync(
                $"profiling export --session sess0001 --output \"{exportPath}\"",
                console,
                provider,
                ConsoleCommandExecutionSource.Terminal
            );
            var importResult = await new ConsoleCommandExecutor().ExecuteAsync(
                $"profiling import --file \"{exportPath}\"",
                console,
                provider,
                ConsoleCommandExecutionSource.Terminal
            );

            // Assert
            exportResult.Succeeded.ShouldBeTrue();
            importResult.Succeeded.ShouldBeTrue();
            File.ReadAllText(exportPath).ShouldBe("{}");
            writer.ToString().ShouldContain("newsess1");
            await archives.Received(1).ExportSessionAsync(
                "sess0001",
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            );
            await archives.Received(1).ImportAsync(
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            );
        }
        finally
        {
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }

            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task Export_WithPerfettoFormat_WritesVisualizationTraceWithoutUsingArchiveService()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"bitdevkit-prof-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var exportPath = Path.Combine(directory, "session.perfetto.json");
        var archives = Substitute.For<IProfilingArchiveService>();
        var perfetto = Substitute.For<IProfilingPerfettoExportService>();
        perfetto
            .ExportSessionAsync("sess0001", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<Stream>(1).Write("{\"traceEvents\":[]}"u8);
                return Result.Success();
            });
        using var provider = CreateProvider(archives: archives, perfetto: perfetto);
        var (console, writer) = CreateConsole();

        try
        {
            // Act
            var result = await new ConsoleCommandExecutor().ExecuteAsync(
                $"profiling export --session sess0001 --format perfetto --output \"{exportPath}\"",
                console,
                provider,
                ConsoleCommandExecutionSource.Terminal
            );

            // Assert
            result.Succeeded.ShouldBeTrue();
            File.ReadAllText(exportPath).ShouldBe("{\"traceEvents\":[]}");
            writer.ToString().ShouldContain("Perfetto trace");
            await perfetto.Received(1).ExportSessionAsync(
                "sess0001",
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            );
            await archives.DidNotReceiveWithAnyArgs()
                .ExportSessionAsync(default, default, default);
        }
        finally
        {
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }

            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task Clear_WithoutYes_ChangesNothingAndExplainsConfirmation()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling clear",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        writer.ToString().ShouldContain("No data was changed");
        writer.ToString().ShouldContain("--yes");
        await control.DidNotReceive().ClearAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mark_WithoutActiveSession_WritesCoreStateFailure()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        control
            .AddPhaseMarkerAsync("load", Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingPhaseMarker>
                    .Failure()
                    .WithError(new ProfilingInvalidStateError("No profiling session is active."))
            );
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling mark --name load",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        writer.ToString().ShouldContain("No profiling session is active");
    }

    [Fact]
    public async Task Start_WhenDisabled_WritesSafeTypedFailure()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        control
            .StartAsync(Arg.Any<ProfilingStartRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingControlResult>.Failure().WithError(new ProfilingDisabledError())
            );
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling start",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        writer.ToString().ShouldContain("Profiling collection is disabled");
    }

    [Fact]
    public async Task Status_WhenControlServiceMissing_WritesUnavailableWithoutThrowing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IConsoleCommand, ProfilingStatusConsoleCommand>();
        using var provider = services.BuildServiceProvider();
        var (console, writer) = CreateConsole();

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling status",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeTrue();
        writer.ToString().ShouldContain("Profiling unavailable");
    }

    [Fact]
    public async Task Start_WhenCancelled_ForwardsCancellationAndInvokesCoreOnlyOnce()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var control = Substitute.For<IProfilingControlService>();
        control
            .StartAsync(Arg.Any<ProfilingStartRequest>(), cancellation.Token)
            .Returns(Task.FromCanceled<Result<ProfilingControlResult>>(cancellation.Token));
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling start",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal,
            cancellation.Token
        );

        // Assert
        result.Succeeded.ShouldBeFalse();
        writer.ToString().ShouldContain("Command cancelled");
        await control.Received(1).StartAsync(Arg.Any<ProfilingStartRequest>(), cancellation.Token);
    }

    [Fact]
    public async Task Snapshot_WithAcceptedNode_UsesImmediateOutcomeTerminology()
    {
        // Arrange
        var control = Substitute.For<IProfilingControlService>();
        control
            .SnapshotAsync(null, Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingControlResult>.Success(
                    new(
                        CreateSession(),
                        false,
                        [new("node0001", BroadcastDeliveryOutcome.Accepted, Duration: TimeSpan.FromMilliseconds(3))]
                    )
                )
            );
        using var provider = CreateProvider(control: control);
        var (console, writer) = CreateConsole();

        // Act
        await new ConsoleCommandExecutor().ExecuteAsync(
            "profiling snapshot",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        var output = writer.ToString();
        output.ShouldContain("Immediate outcome");
        output.ShouldContain("Accepted");
        output.ShouldContain("does not mean local execution completed");
        output.ShouldNotContain("Accepted as completed");
        output.ShouldNotContain("╭");
    }

    [Fact]
    public async Task DiagPerf_WithProfilingCommandsRegistered_RemainsLocalPointInTimeCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddConsoleCommandsInteractive();
        services.AddProfiling().AddConsoleCommands();
        using var provider = services.BuildServiceProvider();
        var (console, writer) = CreateConsole();

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "diag perf",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeTrue();
        var output = writer.ToString();
        output.ShouldContain("Point-in-time performance snapshot");
        output.ShouldContain("CPU%");
        output.ShouldNotContain("sess0001");
    }

    private static ServiceProvider CreateProvider(
        IProfilingControlService control = null,
        IProfilingQueryService queries = null,
        IProfilingArchiveService archives = null,
        IProfilingPerfettoExportService perfetto = null
    )
    {
        var services = new ServiceCollection();
        services.AddProfiling().AddConsoleCommands();
        if (control is not null)
        {
            services.RemoveAll<IProfilingControlService>();
            services.AddSingleton(control);
        }

        if (queries is not null)
        {
            services.RemoveAll<IProfilingQueryService>();
            services.AddSingleton(queries);
        }

        if (archives is not null)
        {
            services.RemoveAll<IProfilingArchiveService>();
            services.AddSingleton(archives);
        }

        if (perfetto is not null)
        {
            services.RemoveAll<IProfilingPerfettoExportService>();
            services.AddSingleton(perfetto);
        }

        return services.BuildServiceProvider();
    }

    private static ProfilingSession CreateSession() =>
        new()
        {
            Identity = new(Guid.NewGuid(), "sess0001"),
            Name = "test",
            State = ProfilingSessionState.Running,
            StartedUtc = DateTimeOffset.Parse("2026-08-07T10:00:00Z"),
            EndsUtc = DateTimeOffset.Parse("2026-08-07T10:00:30Z"),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(30),
        };

    private static ProfilingEvaluationResult CreateEvaluation(ProfilingEvaluationMode mode) =>
        new(
            new(
                mode,
                "sess0001",
                "node0001",
                mode == ProfilingEvaluationMode.TwoSnapshots ? ["snap0001", "snap0002"] : [],
                DateTimeOffset.Parse("2026-08-07T10:00:00Z"),
                DateTimeOffset.Parse("2026-08-07T10:00:10Z"),
                mode == ProfilingEvaluationMode.TwoSnapshots ? 2 : 11,
                false
            ),
            new()
            {
                Sufficiency = ProfilingDataSufficiency.Sufficient,
                AvailableInputs = ["cpu", "memory"],
                SamplingCoveragePercent = 100,
                CaptureDurationP95 = TimeSpan.FromMilliseconds(4),
            },
            [new("cpu-average", 42.5, "percent")],
            [
                new(
                    "managed-memory-growth",
                    ProfilingSignalLabel.Notable,
                    "Managed memory increased during the evaluated period.",
                    [new("managed-memory-delta", 1024, "bytes")],
                    ProfilingSignalConfidence.Medium,
                    "Inspect allocation-heavy code."
                ),
            ],
            ["Post-GC evidence was unavailable."]
        );

    private static (IAnsiConsole Console, StringWriter Writer) CreateConsole()
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(
            new AnsiConsoleSettings { Out = new StringWriterAnsiConsoleOutput(writer) }
        );
        return (console, writer);
    }

    private sealed class StringWriterAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;

        public bool IsTerminal => false;

        public int Width => 160;

        public int Height => 40;

        public void SetEncoding(System.Text.Encoding encoding)
        {
        }
    }
}
