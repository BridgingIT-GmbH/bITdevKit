// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

public sealed class AuditConsoleCommandTests
{
    [Fact]
    public void AddConsoleCommands_ExplicitRepeatedSelection_RegistersOnlyReadCommandsOnce()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.AddConsoleCommands(false);
        services.Any(service => service.ServiceType == typeof(IConsoleCommand)).ShouldBeFalse();
        builder.AddConsoleCommands().AddConsoleCommands();
        using var provider = services.BuildServiceProvider();

        var commands = provider.GetServices<IConsoleCommand>().OfType<IGroupedConsoleCommand>().ToArray();

        commands.Select(command => command.Name).ShouldBe(["list", "show", "related", "history", "stats", "diagnostics"], ignoreOrder: true);
        commands.All(command => command.GroupName == "auditing" && command.GroupAliases.Contains("audit")).ShouldBeTrue();
        services.Any(service => service.ServiceType == typeof(AuditingEndpointsOptions)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("list --json")]
    [InlineData("list --action nested.rename --truncated --failed-actions --json")]
    [InlineData("history Customer C-42 --json")]
    [InlineData("stats --field Key --json")]
    public async Task ExecuteAsync_SearchFamilies_PreserveTrustedScope(string command)
    {
        var fixture = await CreateAsync();
        using var services = fixture.Services;

        var output = await ExecuteAsync(services, "auditing " + command);

        output.ShouldContain("customers.rename");
        output.ShouldNotContain("other-tenant");
        output.ShouldNotContain("private-value");
        await AssertNoWritesAsync(fixture.Storage);
    }

    [Fact]
    public async Task ExecuteAsync_ShowAndRelated_UseExactScopedReadsAndExposure()
    {
        var fixture = await CreateAsync();
        using var services = fixture.Services;

        var detail = await ExecuteAsync(services, $"audit show {fixture.OwnId:D} --json");
        var denied = await ExecuteAsync(services, $"auditing show {fixture.OtherId:D} --json");
        var related = await ExecuteAsync(services, $"auditing related {fixture.OwnId:D} --json");

        detail.ShouldContain("Redacted");
        detail.ShouldContain("NotProtected");
        detail.ShouldNotContain("private-value");
        detail.ShouldNotContain("other-tenant");
        denied.ShouldContain("AuditNotFound");
        related.ShouldContain("customers.followup");
        related.ShouldNotContain("other-tenant");
        await AssertNoWritesAsync(fixture.Storage);
    }

    [Fact]
    public async Task ExecuteAsync_HumanOutput_EscapesMarkupAndTerminalControlCharacters()
    {
        var fixture = await CreateAsync();
        using var services = fixture.Services;

        var output = await ExecuteAsync(services, $"auditing show {fixture.OwnId:D}");

        output.ShouldContain("[red]");
        output.ShouldContain("\\u001b");
        output.ShouldNotContain("\u001b");
        output.ShouldContain("NotProtected");
        output.ShouldNotContain("private-value");
    }

    [Fact]
    public async Task ExecuteAsync_AllCommands_RestoreSurroundingScopeAndPerformNoWrites()
    {
        var fixture = await CreateAsync();
        using var services = fixture.Services;
        using var scope = services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        using var root = audit.BeginScope(builder => builder.Key("surrounding.business"));
        foreach (var command in new[] { "list", $"show {fixture.OwnId:D}", $"related {fixture.OwnId:D}", "history Customer C-42", "stats", "diagnostics" })
        {
            var output = await ExecuteAsync(scope.ServiceProvider, "auditing " + command);
            output.ShouldNotContain("AuditReadUnavailable");
            audit.Current.ShouldBeSameAs(root);
        }

        ((AuditScope)root).Collector.Snapshot().Actions.ShouldBeEmpty();
        await AssertNoWritesAsync(fixture.Storage);
    }

    [Theory]
    [InlineData("list --take 201")]
    [InlineData("list --page 0")]
    [InlineData("list --from invalid")]
    [InlineData("list --lifecycle Deleted")]
    [InlineData("stats --take 101")]
    [InlineData("show 00000000-0000-0000-0000-000000000000")]
    public async Task ExecuteAsync_InvalidInput_DoesNotReadTheProvider(string command)
    {
        var fixture = await CreateAsync();
        using var services = fixture.Services;

        var output = await ExecuteAsync(services, "auditing " + command);

        output.ShouldContain("AuditConsoleArgumentsInvalid");
        fixture.Storage.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("list")]
    [InlineData("show {id}")]
    [InlineData("related {id}")]
    [InlineData("history Customer C-42")]
    [InlineData("stats")]
    public async Task ExecuteAsync_UnavailableReadScope_DoesNotReadTheProvider(string command)
    {
        var fixture = await CreateAsync(deny: true);
        using var services = fixture.Services;

        var output = await ExecuteAsync(services, "auditing " + command.Replace("{id}", fixture.OwnId.ToString("D")));

        output.ShouldContain("AuditReadScopeUnavailable");
        fixture.Storage.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FilterOptions_BindToProviderSelectionAndPaging()
    {
        var fixture = await CreateAsync();
        using var services = fixture.Services;

        var output = await ExecuteAsync(services, "auditing list --key customers.rename --actor user-42 --module Customers --correlation corr-42 --outcome Succeeded --from 2026-01-01 --to 2027-01-01 --page 2 --take 1 --lifecycle All --json");

        // The configured query range remains authoritative; use a bounded real-time range for the successful read.
        output.ShouldContain("AuditQueryInvalid");
        fixture.Storage.ReceivedCalls().ShouldBeEmpty();
        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var to = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var result = await ExecuteAsync(services, $"auditing list --key customers.rename --actor user-42 --module Customers --correlation corr-42 --outcome Succeeded --from {from} --to {to} --page 1 --take 1 --lifecycle All --json");
        result.ShouldContain("customers.rename");
        result.ShouldContain("\"totalCount\": 1");
        result.ShouldContain("\"pageSize\": 1");
    }

    private static async Task<(ServiceProvider Services, IAuditingStorageProvider Storage, Guid OwnId, Guid OtherId)> CreateAsync(bool deny = false)
    {
        var source = new InMemoryAuditStorageProvider(new(), new AuditFieldCatalog([new() { Name = "tenantId", ExposeValue = false }]));
        var storage = Substitute.For<IAuditingStorageProvider>();
        storage.FindStoredAsync(Arg.Any<Guid>(), Arg.Any<AuditPredicate>(), Arg.Any<CancellationToken>()).Returns(call => source.FindStoredAsync(call.ArgAt<Guid>(0), call.ArgAt<AuditPredicate>(1), call.ArgAt<CancellationToken>(2)));
        storage.SearchAsync(Arg.Any<AuditSearchPlan>(), Arg.Any<CancellationToken>()).Returns(call => source.SearchAsync(call.ArgAt<AuditSearchPlan>(0), call.ArgAt<CancellationToken>(1)));
        storage.FindRelatedAsync(Arg.Any<AuditRelatedExecutionsQuery>(), Arg.Any<CancellationToken>()).Returns(call => source.FindRelatedAsync(call.ArgAt<AuditRelatedExecutionsQuery>(0), call.ArgAt<CancellationToken>(1)));
        storage.GetResourceHistoryAsync(Arg.Any<AuditSearchPlan>(), Arg.Any<AuditResourceReference>(), Arg.Any<CancellationToken>()).Returns(call => source.GetResourceHistoryAsync(call.ArgAt<AuditSearchPlan>(0), call.ArgAt<AuditResourceReference>(1), call.ArgAt<CancellationToken>(2)));
        storage.AggregateAsync(Arg.Any<AuditAggregateQuery>(), Arg.Any<CancellationToken>()).Returns(call => source.AggregateAsync(call.ArgAt<AuditAggregateQuery>(0), call.ArgAt<CancellationToken>(1)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditing(options => options.Configure(value => value with { CapturePolicy = value.CapturePolicy with { RedactNames = value.CapturePolicy.RedactNames.Add("privateNote") } }))
            .WithStorageProvider(_ => storage).WithSearchableDetail(new() { Name = "tenantId", ExposeValue = false })
            .WithReadScope((_, _) => Task.FromResult(deny ? Result<AuditReadScope>.Failure() : Result<AuditReadScope>.Success(new(new() { Filters = [new("Details.tenantId", FilterOperator.Equal, "A")] }, "tenant-v1"))))
            .AddConsoleCommands();
        var own = Record("customers.rename", "A");
        var other = Record("other-tenant", "B") with { OriginAuditId = own.AuditId, CausationAuditId = own.AuditId };
        var linked = Record("customers.followup", "A") with { OriginAuditId = own.AuditId, CausationAuditId = own.AuditId };
        foreach (var record in new[] { own, other, linked }) (await source.AppendAsync(record)).IsSuccess.ShouldBeTrue();
        storage.ClearReceivedCalls();
        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), storage, own.AuditId, other.AuditId);
    }

    private static AuditRecord Record(string key, string tenant)
    {
        var id = Guid.NewGuid();
        return new()
        {
            AuditId = id, OriginAuditId = id, Key = key, DisplayName = "Captured [red]label\u001b", Outcome = AuditOutcome.Succeeded,
            Actor = new() { UserId = "user-42" }, ModuleName = "Customers", CorrelationId = "corr-42",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5), CompletedUtc = DateTimeOffset.UtcNow.AddMinutes(-4),
            Resources = [new("Customer", "C-42")],
            Completeness = AuditCompleteness.Truncated, Notes = [new() { Code = "AuditValueTooLarge" }], HasFailedActions = true,
            Details = ImmutableDictionary<string, AuditValue>.Empty.Add("tenantId", AuditValue.From(tenant)).Add("privateNote", AuditValue.From("private-value")),
            Actions = [new() { Id = Guid.NewGuid(), Key = "nested.rename", Resources = [new("Customer", "C-42")], Outcome = AuditOutcome.Failed }]
        };
    }

    private static async Task<string> ExecuteAsync(IServiceProvider services, string command)
    {
        using var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors });
        console.Profile.Width = 240;
        var result = await new ConsoleCommandExecutor().ExecuteAsync(command, console, services, ConsoleCommandExecutionSource.Terminal);
        result.Succeeded.ShouldBeTrue();
        return output.ToString();
    }

    private static async Task AssertNoWritesAsync(IAuditingStorageProvider storage)
    {
        await storage.DidNotReceiveWithAnyArgs().AppendAsync(default, default);
        await storage.DidNotReceiveWithAnyArgs().FlushAsync(default);
        await storage.DidNotReceiveWithAnyArgs().ArchiveBeforeAsync(default, default, default);
        await storage.DidNotReceiveWithAnyArgs().RemoveArchivedBeforeAsync(default, default, default);
        await storage.DidNotReceiveWithAnyArgs().OpenExtractionAsync(default, default);
    }
}
