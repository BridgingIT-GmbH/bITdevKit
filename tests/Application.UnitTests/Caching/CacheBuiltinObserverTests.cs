// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class CacheBuiltinObserverTests
{
    [Fact]
    public async Task FluentObservers_AppendAcrossGlobalCallsAndAreOptionalWithoutIntegrations()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithLogging().WithMetrics().WithProfiling().WithAuditing().WithLogging()
            .WithValueContract<string>("text").WithCollection<string>("items");
        await using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<CacheCatalog>();
        var binding = catalog.Collections["ITEMS"];
        binding.Options.Behaviors.CollectionOperations.ShouldBe(new[] { "logging", "metrics", "profiling", "auditing" });
        binding.Options.Behaviors.SourceExecution.ShouldBe(new[] { "logging", "metrics", "profiling" });
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        (await sut.GetOrLoadAsync(new("key"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("value"))))).IsSuccess.ShouldBeTrue();
        (await sut.GetAsync("key")).Value.Value.ShouldBe("value");
    }

    [Fact]
    public async Task Logging_OmitsKeysDimensionsValuesAndRawErrors()
    {
        var services = new ServiceCollection();
        var logger = new RecordingLogger();
        services.AddSingleton<ILogger<LoggingCacheBehavior>>(logger);
        services.AddApplicationCaching().WithLogging().WithValueContract<string>("text").WithCollection<string>("items");
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        (await sut.SetAsync(new("secret-key"), "secret-payload")).IsSuccess.ShouldBeTrue();
        (await sut.GetAsync("secret-key")).IsSuccess.ShouldBeTrue();

        logger.Messages.Count.ShouldBe(3);
        logger.Messages.Count(message => message.Contains("cache source transfer", StringComparison.Ordinal)).ShouldBe(1);
        string.Join('\n', logger.Messages).ShouldNotContain("secret-");
        logger.Messages.All(m => m.Contains("ITEMS", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task Profiling_OwnedSourceBeginsIndependentRoot()
    {
        var services = new ServiceCollection();
        var profiling = Substitute.For<IOperationProfiler>();
        var scope = Substitute.For<IProfilingOperationScope>();
        profiling.BeginOperation(Arg.Any<string>(), Arg.Any<OperationProfilingKind>()).Returns(scope);
        services.AddSingleton(profiling);
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.SourceExecution, new[] { "profiling" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        (await sut.GetOrLoadAsync(new("key"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("value"))))).IsSuccess.ShouldBeTrue();

        profiling.Received(1).BeginOperation("cache:ITEMS:source", OperationProfilingKind.Service);
        scope.Received(1).Complete();
        scope.Received(1).Dispose();
    }

    [Fact]
    public async Task Auditing_OnlyMutationsAreRecordedAndFinalizationFailurePreservesPublication()
    {
        var services = new ServiceCollection();
        var audit = Substitute.For<IAuditContext>();
        var scope = Substitute.For<IAuditScope>();
        audit.IsEnabled.Returns(true);
        audit.BeginScope(Arg.Any<AuditScopeOptions>()).Returns(scope);
        scope.DisposeAsync().Returns(_ => ValueTask.FromException(new IOException("observer unavailable")));
        services.AddSingleton(audit);
        services.AddApplicationCaching().WithAuditing().WithValueContract<string>("text").WithCollection<string>("items");
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        var written = await sut.SetAsync(new("secret-key"), "value");
        var read = await sut.GetAsync("secret-key");

        written.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        read.Value.Version.ShouldBe(written.Value.Version);
        audit.Received(1).BeginScope(Arg.Is<AuditScopeOptions>(o => !o.Descriptor.Key.Contains("secret")));
        scope.Received(1).Complete(AuditOutcome.Succeeded);
    }

    private sealed class RecordingLogger : ILogger<LoggingCacheBehavior>
    {
        internal List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            this.Messages.Add(formatter(state, exception));
    }
}