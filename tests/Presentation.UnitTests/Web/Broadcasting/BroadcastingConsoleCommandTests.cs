// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Broadcasting;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console;

public sealed class BroadcastingConsoleCommandTests
{
    [Fact]
    public void AddConsoleCommands_WhenCalledRepeatedly_RegistersOneOfEachBroadcastingCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddBroadcasting();

        // Act
        context.AddConsoleCommands();
        context.AddConsoleCommands();
        using var provider = services.BuildServiceProvider();
        var commands = provider
            .GetServices<IConsoleCommand>()
            .OfType<IGroupedConsoleCommand>()
            .Where(command => command.GroupName == "broadcasting")
            .ToArray();

        // Assert
        commands.Select(command => command.Name).ShouldBe(["list", "probe"], ignoreOrder: true);
    }

    [Fact]
    public async Task List_WhenRegistrationsExist_RendersNodeDiagnostics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBroadcasting().AddConsoleCommands();
        services.RemoveAll<IBroadcastingDiagnostics>();
        services.AddSingleton<IBroadcastingDiagnostics>(
            new StubBroadcastingDiagnostics(CreateSnapshot())
        );
        using var provider = services.BuildServiceProvider();
        var writer = new StringWriter();
        var console = CreateConsole(writer);

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "broadcasting list",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeTrue();
        var output = writer.ToString();
        output.ShouldContain("node-a");
        output.ShouldContain("default");
        output.ShouldContain("Active");
        output.ShouldNotContain("╭");
    }

    [Fact]
    public async Task Probe_WithoutScope_PublishesToDefaultScope()
    {
        // Arrange
        var broadcastService = new RecordingBroadcastService();
        var services = new ServiceCollection();
        services.AddBroadcasting().AddConsoleCommands();
        services.RemoveAll<IBroadcastService>();
        services.AddSingleton<IBroadcastService>(broadcastService);
        using var provider = services.BuildServiceProvider();
        var writer = new StringWriter();
        var console = CreateConsole(writer);

        // Act
        var result = await new ConsoleCommandExecutor().ExecuteAsync(
            "broadcasting probe",
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal
        );

        // Assert
        result.Succeeded.ShouldBeTrue();
        broadcastService.Payload.ShouldNotBeNull();
        broadcastService.TargetScopes.ShouldBeNull();
        broadcastService.Options.RequireAtLeastOneTarget.ShouldBeTrue();
        var output = writer.ToString();
        output.ShouldContain("default");
        output.ShouldContain("Accepted");
        output.ShouldNotContain("╭");
    }

    private static IAnsiConsole CreateConsole(TextWriter writer) =>
        AnsiConsole.Create(
            new AnsiConsoleSettings { Out = new StringWriterAnsiConsoleOutput(writer) }
        );

    private static BroadcastingDiagnosticSnapshot CreateSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            true,
            [
                new(
                    BroadcastingOptions.DefaultScope,
                    [
                        new()
                        {
                            NodeIdentity = "node-a",
                            Scopes = [BroadcastingOptions.DefaultScope],
                            RegisteredUtc = now,
                            ProcessStartedUtc = now,
                            IsActive = true,
                        },
                    ]
                ),
            ]
        );
    }

    private sealed class StubBroadcastingDiagnostics(BroadcastingDiagnosticSnapshot snapshot)
        : IBroadcastingDiagnostics
    {
        public Task<BroadcastingDiagnosticSnapshot> GetAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(snapshot);

        public Task<Result> RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result.Success());
    }

    private sealed class RecordingBroadcastService : IBroadcastService
    {
        public BroadcastProbe Payload { get; private set; }

        public IReadOnlyCollection<string> TargetScopes { get; private set; }

        public BroadcastPublishOptions Options { get; private set; }

        public Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
            TBroadcast payload,
            IEnumerable<string> targetScopes,
            BroadcastPublishOptions options = null,
            CancellationToken cancellationToken = default
        )
        {
            this.Payload = payload.ShouldBeOfType<BroadcastProbe>();
            this.TargetScopes = targetScopes?.ToArray();
            this.Options = options;
            return Task.FromResult(
                Result<BroadcastResult>.Success(
                    new()
                    {
                        BroadcastId = Guid.NewGuid(),
                        TargetScopes = [BroadcastingOptions.DefaultScope],
                        Nodes =
                        [
                            new(
                                "node-a",
                                BroadcastDeliveryOutcome.Accepted,
                                Duration: TimeSpan.FromMilliseconds(3)
                            ),
                        ],
                    }
                )
            );
        }
    }

    private sealed class StringWriterAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;

        public bool IsTerminal => false;

        public int Width => 120;

        public int Height => 32;

        public void SetEncoding(System.Text.Encoding encoding)
        {
        }
    }
}