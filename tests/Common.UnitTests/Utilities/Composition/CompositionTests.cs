// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Collections.Concurrent;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Composition;

[UnitTest("Common")]
public class CompositionTests
{
    [Fact]
    public void AddComposition_CalledMultipleTimes_ResolvesRegistrationsFromBothCalls()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<IPingService>()
            .Use<PingService>()
            .RegisterScoped();

        services.AddComposition()
            .Strategies<IFormatter>()
            .Add<UpperFormatter>("upper")
            .WithDefault("upper");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPingService>().Ping().ShouldBe("pong");
        scope.ServiceProvider.GetRequiredService<IStrategyResolver<IFormatter>>().ResolveDefault().Format("pong").ShouldBe("PONG");
    }

    [Fact]
    public void RegisterScoped_TryRegister_ServiceExists_KeepsExistingImplementation()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<IPingService>()
            .Use<PingService>()
            .RegisterScoped();

        services.AddComposition()
            .For<IPingService>()
            .Use<OtherPingService>()
            .TryRegister()
            .RegisterScoped();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPingService>().Ping().ShouldBe("pong");
    }

    [Fact]
    public void RegisterScoped_ReplaceExisting_ReplacesExistingImplementation()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<IPingService>()
            .Use<PingService>()
            .RegisterScoped();

        services.AddComposition()
            .For<IPingService>()
            .Use<OtherPingService>()
            .ReplaceExisting()
            .RegisterScoped();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPingService>().Ping().ShouldBe("other");
    }

    [Fact]
    public void RegisterScoped_AddAdditional_AppendsAdditionalRegistration()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<IPingService>()
            .Use<PingService>()
            .RegisterScoped();

        services.AddComposition()
            .For<IPingService>()
            .Use<OtherPingService>()
            .AddAdditional()
            .RegisterScoped();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetServices<IPingService>().Count().ShouldBe(2);
    }

    [Fact]
    public void Decorate_WithOrderedDecorators_ExecutesOutermostFirst()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<ISequenceService>()
            .Use<SequenceService>()
            .Decorate(d => d
                .With<DecoratorA>()
                .With<DecoratorB>())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();
        var log = new InvocationLog();

        provider.GetRequiredService<ISequenceService>().Execute(log);

        log.Entries.ShouldBe(["decorator-a-before", "decorator-b-before", "implementation", "decorator-b-after", "decorator-a-after"]);
    }

    [Fact]
    public void Decorate_InvalidDecoratorConstructor_ThrowsClearException()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddComposition()
                .For<ISequenceService>()
                .Use<SequenceService>()
                .Decorate(d => d.With<InvalidDecorator>()));

        exception.Message.ShouldContain("Decorator");
        exception.Message.ShouldContain(nameof(ISequenceService));
    }

    [Fact]
    public void Adapt_RegisteredAdapter_ResolvesTargetContract()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Adapt<LegacyWeatherClient>()
            .To<IWeatherService>()
            .Using<LegacyWeatherClientAdapter>()
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWeatherService>().GetCity().ShouldBe("Berlin");
    }

    [Fact]
    public void Adapt_FactoryBasedAdapter_AdaptsExplicitSourceInstance()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Adapt<LegacyWeatherClient>()
            .To<IWeatherService>()
            .Using<LegacyWeatherClientAdapter>()
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();
        var adapterFactory = provider.GetRequiredService<IAdapterFactory>();

        adapterFactory.Adapt<LegacyWeatherClient, IWeatherService>(new LegacyWeatherClient("Hamburg")).GetCity().ShouldBe("Hamburg");
    }

    [Fact]
    public void Adapt_InvalidAdapterConstructor_ThrowsClearExceptionOnResolution()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Adapt<LegacyWeatherClient>()
            .To<IWeatherService>()
            .Using<InvalidLegacyWeatherClientAdapter>()
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IWeatherService>());
        exception.Message.ShouldContain("Adapter");
    }

    [Fact]
    public async Task Intercept_ExplicitInterceptorAndRuntimeBehavior_ExecuteInOfficialOrder()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<IInterceptedService>()
            .Use<InterceptedService>()
            .Intercept(i => i
                .With<OrderedInterceptor>()
                .WithBehavior<OrderedInterceptionBehavior>())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();
        var log = new InvocationLog();

        await provider.GetRequiredService<IInterceptedService>().RunAsync(log);

        log.Entries.ShouldBe(["interceptor-before", "behavior-before", "implementation", "behavior-after", "interceptor-after"]);
    }

    [Fact]
    public async Task Intercept_RuntimeProxy_ReturnShapes_ArePreserved()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<IReturnShapeService>()
            .Use<ReturnShapeService>()
            .Intercept(i => i.WithBehavior<NoOpReturnShapeBehavior>())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IReturnShapeService>();
        var log = new InvocationLog();

        service.RunVoid(log);
        service.RunString(log).ShouldBe("string");
        await service.RunTask(log);
        (await service.RunTaskString(log)).ShouldBe("task-string");
        await service.RunValueTask(log);
        (await service.RunValueTaskString(log)).ShouldBe("valuetask-string");
        service.RunResult(log).IsSuccess.ShouldBeTrue();
        service.RunResultString(log).Value.ShouldBe("result-string");
        (await service.RunTaskResult(log)).IsSuccess.ShouldBeTrue();
        (await service.RunTaskResultString(log)).Value.ShouldBe("task-result-string");
        (await service.RunValueTaskResult(log)).IsSuccess.ShouldBeTrue();
        (await service.RunValueTaskResultString(log)).Value.ShouldBe("valuetask-result-string");
    }

    [Fact]
    public async Task Intercept_CancellationTokenPresent_CapturesDetectedToken()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<ICancellableService>()
            .Use<CancellableService>()
            .Intercept(i => i.WithBehavior<CaptureCancellationTokenBehavior>())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();
        var capture = new TokenCapture();
        using var cts = new CancellationTokenSource();

        await provider.GetRequiredService<ICancellableService>().ExecuteAsync(capture, cts.Token);

        capture.Token.ShouldBe(cts.Token);
        capture.CanBeCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task Intercept_WithTimeout_LongRunningInvocation_ThrowsTimeoutException()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<ISlowService>()
            .Use<SlowService>()
            .Intercept(i => i.WithTimeout(TimeSpan.FromMilliseconds(25)))
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        await Should.ThrowAsync<TimeoutException>(() => provider.GetRequiredService<ISlowService>().DelayAsync(TimeSpan.FromMilliseconds(150)));
    }

    [Fact]
    public async Task Intercept_WithRetry_ThrownException_RetriesConfiguredNumberOfTimes()
    {
        var services = new ServiceCollection();
        var counter = new Counter();
        services.AddSingleton(counter);

        services.AddComposition()
            .For<IThrowingService>()
            .Use<ThrowingService>()
            .Intercept(i => i.WithRetry(3))
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        await Should.ThrowAsync<AggregateException>(() => provider.GetRequiredService<IThrowingService>().FailAsync());
        counter.Count.ShouldBe(3);
    }

    [Fact]
    public void Intercept_WithLogging_FailedResult_LogsFailureState()
    {
        var services = new ServiceCollection();
        var loggerFactory = new RecordingLoggerFactory();
        services.AddSingleton<ILoggerFactory>(loggerFactory);

        services.AddComposition()
            .For<IResultService>()
            .Use<ResultService>()
            .Intercept(i => i.WithLogging())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IResultService>().Fail().IsFailure.ShouldBeTrue();

        loggerFactory.Messages.ShouldContain(m => m.Contains("success=False", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Intercept_WithRetry_FailedResult_NotRetriedByDefault()
    {
        var services = new ServiceCollection();
        var counter = new Counter();
        services.AddSingleton(counter);

        services.AddComposition()
            .For<IResultCounterService>()
            .Use<ResultCounterService>()
            .Intercept(i => i.WithRetry(3))
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IResultCounterService>().Fail().IsFailure.ShouldBeTrue();
        counter.Count.ShouldBe(1);
    }

    [Fact]
    public void Intercept_NonInterfaceRuntimeInterception_ThrowsClearException()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .For<ConcreteOnlyService>()
            .Use<ConcreteOnlyService>()
            .Intercept(i => i.WithLogging())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<ConcreteOnlyService>());
        exception.Message.ShouldContain("interface service contract");
    }

    [Fact]
    public void Strategies_ResolveTryResolveAndDefault_WorkAsExpected()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Strategies<IFormatter>()
            .Add<UpperFormatter>("upper")
            .Add<LowerFormatter>("lower")
            .WithDefault("upper");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IStrategyResolver<IFormatter>>();

        resolver.Resolve("lower").Format("TeSt").ShouldBe("test");
        resolver.ResolveDefault().Format("TeSt").ShouldBe("TEST");
        resolver.TryResolve("missing", out var missing).ShouldBeFalse();
        missing.ShouldBeNull();
        resolver.Keys.Count.ShouldBe(2);
    }

    [Fact]
    public void Strategies_DuplicateKey_ThrowsClearException()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddComposition()
                .Strategies<IFormatter>()
                .Add<UpperFormatter>("upper")
                .Add<LowerFormatter>("upper"));

        exception.Message.ShouldContain("already registered");
    }

    [Fact]
    public void Strategies_MissingDefaultRegistration_ThrowsOnResolveDefault()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Strategies<IFormatter>()
            .Add<UpperFormatter>("upper")
            .WithDefault("lower");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Should.Throw<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IStrategyResolver<IFormatter>>().ResolveDefault());

        exception.Message.ShouldContain("not registered");
    }

    [Fact]
    public void Composite_ConfiguredChildrenOnly_DoesNotInjectCompositeIntoItself()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Composite<INotificationChannel, CompositeNotificationChannel>(c => c
                .With<EmailNotificationChannel>()
                .With<TeamsNotificationChannel>())
            .RegisterTransient();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<INotificationChannel>().Describe().ShouldBe("2:True");
    }

    [Fact]
    public async Task Chain_DefaultRegistration_ExecutesHandlersInOrderAndStopsWhenHandled()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Chain<IImportHandler, ImportContext>(chain => chain
                .With<CsvImportHandler>()
                .With<JsonImportHandler>());

        using var provider = services.BuildServiceProvider();
        var context = new ImportContext { FileName = "orders.csv" };

        var result = await provider.GetRequiredService<IChainExecutor<ImportContext>>().ExecuteAsync(context);

        result.Handled.ShouldBeTrue();
        context.Log.ShouldBe(["csv"]);
    }

    [Fact]
    public async Task Chain_NoHandlerHandles_ReturnsUnhandledResult()
    {
        var services = new ServiceCollection();

        services.AddComposition()
            .Chain<IImportHandler, ImportContext>(chain => chain
                .With<CsvImportHandler>()
                .With<JsonImportHandler>());

        using var provider = services.BuildServiceProvider();
        var context = new ImportContext { FileName = "orders.xml" };

        var result = await provider.GetRequiredService<IChainExecutor<ImportContext>>().ExecuteAsync(context);

        result.Handled.ShouldBeFalse();
        context.Log.ShouldBeEmpty();
    }

}

internal sealed class InvocationLog
{
    public IList<string> Entries { get; } = new List<string>();
}

internal interface IPingService
{
    string Ping();
}

internal sealed class PingService : IPingService
{
    public string Ping() => "pong";
}

internal sealed class OtherPingService : IPingService
{
    public string Ping() => "other";
}

internal interface ISequenceService
{
    void Execute(InvocationLog log);
}

internal sealed class SequenceService : ISequenceService
{
    public void Execute(InvocationLog log)
    {
        log.Entries.Add("implementation");
    }
}

internal sealed class DecoratorA(ISequenceService inner) : ISequenceService
{
    public void Execute(InvocationLog log)
    {
        log.Entries.Add("decorator-a-before");
        inner.Execute(log);
        log.Entries.Add("decorator-a-after");
    }
}

internal sealed class DecoratorB(ISequenceService inner) : ISequenceService
{
    public void Execute(InvocationLog log)
    {
        log.Entries.Add("decorator-b-before");
        inner.Execute(log);
        log.Entries.Add("decorator-b-after");
    }
}

internal sealed class InvalidDecorator : ISequenceService
{
    public void Execute(InvocationLog log)
    {
        log.Entries.Add("invalid");
    }
}

internal sealed class LegacyWeatherClient(string city = "Berlin")
{
    public string GetCity() => city;
}

internal interface IWeatherService
{
    string GetCity();
}

internal sealed class LegacyWeatherClientAdapter(LegacyWeatherClient client) : IWeatherService
{
    public string GetCity() => client.GetCity();
}

internal sealed class InvalidLegacyWeatherClientAdapter : IWeatherService
{
    public string GetCity() => "invalid";
}

internal interface IInterceptedService
{
    Task RunAsync(InvocationLog log);
}

internal sealed class InterceptedService : IInterceptedService
{
    public Task RunAsync(InvocationLog log)
    {
        log.Entries.Add("implementation");
        return Task.CompletedTask;
    }
}

internal sealed class OrderedInterceptor(IInterceptedService inner) : IInterceptedService
{
    public async Task RunAsync(InvocationLog log)
    {
        log.Entries.Add("interceptor-before");
        await inner.RunAsync(log);
        log.Entries.Add("interceptor-after");
    }
}

internal sealed class OrderedInterceptionBehavior : IInterceptionBehavior<IInterceptedService>
{
    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<IInterceptedService> context,
        CancellationToken cancellationToken)
    {
        var log = (InvocationLog)context.Arguments[0];
        log.Entries.Add("behavior-before");
        var result = await context.Next();
        log.Entries.Add("behavior-after");
        return result;
    }
}

internal interface IReturnShapeService
{
    void RunVoid(InvocationLog log);

    string RunString(InvocationLog log);

    Task RunTask(InvocationLog log);

    Task<string> RunTaskString(InvocationLog log);

    ValueTask RunValueTask(InvocationLog log);

    ValueTask<string> RunValueTaskString(InvocationLog log);

    Result RunResult(InvocationLog log);

    Result<string> RunResultString(InvocationLog log);

    Task<Result> RunTaskResult(InvocationLog log);

    Task<Result<string>> RunTaskResultString(InvocationLog log);

    ValueTask<Result> RunValueTaskResult(InvocationLog log);

    ValueTask<Result<string>> RunValueTaskResultString(InvocationLog log);
}

internal sealed class ReturnShapeService : IReturnShapeService
{
    public void RunVoid(InvocationLog log) => log.Entries.Add("void");

    public string RunString(InvocationLog log)
    {
        log.Entries.Add("string");
        return "string";
    }

    public Task RunTask(InvocationLog log)
    {
        log.Entries.Add("task");
        return Task.CompletedTask;
    }

    public Task<string> RunTaskString(InvocationLog log)
    {
        log.Entries.Add("task-string");
        return Task.FromResult("task-string");
    }

    public ValueTask RunValueTask(InvocationLog log)
    {
        log.Entries.Add("valuetask");
        return ValueTask.CompletedTask;
    }

    public ValueTask<string> RunValueTaskString(InvocationLog log)
    {
        log.Entries.Add("valuetask-string");
        return ValueTask.FromResult("valuetask-string");
    }

    public Result RunResult(InvocationLog log)
    {
        log.Entries.Add("result");
        return Result.Success();
    }

    public Result<string> RunResultString(InvocationLog log)
    {
        log.Entries.Add("result-string");
        return Result<string>.Success("result-string");
    }

    public Task<Result> RunTaskResult(InvocationLog log)
    {
        log.Entries.Add("task-result");
        return Task.FromResult(Result.Success());
    }

    public Task<Result<string>> RunTaskResultString(InvocationLog log)
    {
        log.Entries.Add("task-result-string");
        return Task.FromResult(Result<string>.Success("task-result-string"));
    }

    public ValueTask<Result> RunValueTaskResult(InvocationLog log)
    {
        log.Entries.Add("valuetask-result");
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result<string>> RunValueTaskResultString(InvocationLog log)
    {
        log.Entries.Add("valuetask-result-string");
        return ValueTask.FromResult(Result<string>.Success("valuetask-result-string"));
    }
}

internal sealed class NoOpReturnShapeBehavior : IInterceptionBehavior<IReturnShapeService>
{
    public ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<IReturnShapeService> context,
        CancellationToken cancellationToken)
    {
        return context.Next();
    }
}

internal sealed class TokenCapture
{
    public CancellationToken Token { get; set; }

    public bool CanBeCanceled { get; set; }
}

internal interface ICancellableService
{
    Task ExecuteAsync(TokenCapture capture, CancellationToken cancellationToken);
}

internal sealed class CancellableService : ICancellableService
{
    public Task ExecuteAsync(TokenCapture capture, CancellationToken cancellationToken)
    {
        capture.CanBeCanceled = cancellationToken.CanBeCanceled;
        return Task.CompletedTask;
    }
}

internal sealed class CaptureCancellationTokenBehavior : IInterceptionBehavior<ICancellableService>
{
    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<ICancellableService> context,
        CancellationToken cancellationToken)
    {
        var capture = (TokenCapture)context.Arguments[0];
        capture.Token = context.CancellationToken;
        capture.CanBeCanceled = context.CancellationToken.CanBeCanceled;
        return await context.Next();
    }
}

internal interface ISlowService
{
    Task DelayAsync(TimeSpan delay);
}

internal sealed class SlowService : ISlowService
{
    public Task DelayAsync(TimeSpan delay)
    {
        return Task.Delay(delay);
    }
}

internal sealed class Counter
{
    public int Count;
}

internal interface IThrowingService
{
    Task FailAsync();
}

internal sealed class ThrowingService(Counter counter) : IThrowingService
{
    public Task FailAsync()
    {
        Interlocked.Increment(ref counter.Count);
        throw new InvalidOperationException("fail");
    }
}

internal interface IResultService
{
    Result Fail();
}

internal sealed class ResultService : IResultService
{
    public Result Fail() => Result.Failure("failed");
}

internal interface IResultCounterService
{
    Result Fail();
}

internal sealed class ResultCounterService(Counter counter) : IResultCounterService
{
    public Result Fail()
    {
        Interlocked.Increment(ref counter.Count);
        return Result.Failure("failed");
    }
}

internal sealed class ConcreteOnlyService
{
    public string Name => "concrete";
}

internal interface IFormatter
{
    string Format(string value);
}

internal sealed class UpperFormatter : IFormatter
{
    public string Format(string value) => value.ToUpperInvariant();
}

internal sealed class LowerFormatter : IFormatter
{
    public string Format(string value) => value.ToLowerInvariant();
}

internal interface INotificationChannel
{
    string Describe();
}

internal sealed class EmailNotificationChannel : INotificationChannel
{
    public string Describe() => "email";
}

internal sealed class TeamsNotificationChannel : INotificationChannel
{
    public string Describe() => "teams";
}

internal sealed class CompositeNotificationChannel(IEnumerable<INotificationChannel> channels) : INotificationChannel
{
    public string Describe()
    {
        var list = channels.ToList();
        return $"{list.Count}:{list.All(c => c is not CompositeNotificationChannel)}";
    }
}

internal sealed class ImportContext
{
    public string FileName { get; set; }

    public IList<string> Log { get; } = new List<string>();
}

internal interface IImportHandler : IChainHandler<ImportContext>;

internal sealed class CsvImportHandler : IImportHandler
{
    public ValueTask<ChainResult> HandleAsync(
        ImportContext context,
        ChainExecutionDelegate<ImportContext> next,
        CancellationToken cancellationToken)
    {
        if (!context.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return next(context, cancellationToken);
        }

        context.Log.Add("csv");
        return ValueTask.FromResult(new ChainResult { Handled = true, Result = Result.Success() });
    }
}

internal sealed class JsonImportHandler : IImportHandler
{
    public ValueTask<ChainResult> HandleAsync(
        ImportContext context,
        ChainExecutionDelegate<ImportContext> next,
        CancellationToken cancellationToken)
    {
        if (!context.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return next(context, cancellationToken);
        }

        context.Log.Add("json");
        return ValueTask.FromResult(new ChainResult { Handled = true, Result = Result.Success() });
    }
}

internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    private readonly RecordingLogger logger = new();

    public IReadOnlyCollection<string> Messages => this.logger.Messages;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => this.logger;

    public void Dispose()
    {
    }
}

internal sealed class RecordingLogger : ILogger
{
    private readonly ConcurrentQueue<string> messages = new();

    public IReadOnlyCollection<string> Messages => this.messages.ToArray();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        this.messages.Enqueue(formatter(state, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
