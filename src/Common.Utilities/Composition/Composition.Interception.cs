// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Utilities.Composition;

using System.Reflection;
using System.Runtime.ExceptionServices;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public interface IInterceptionBuilder<TService>
    where TService : class
{
    /// <summary>
    /// Adds an explicit interceptor wrapper to the interception chain.
    /// </summary>
    /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
    /// <returns>The interception builder.</returns>
    IInterceptionBuilder<TService> With<TInterceptor>()
        where TInterceptor : class, TService;

    /// <summary>
    /// Adds a runtime interception behavior to the interception chain.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type.</typeparam>
    /// <returns>The interception builder.</returns>
    IInterceptionBuilder<TService> WithBehavior<TBehavior>()
        where TBehavior : class, IInterceptionBehavior<TService>;
}

/// <summary>
/// Provides convenience methods for interception builders.
/// </summary>
public static class InterceptionBuilderExtensions
{
    /// <summary>
    /// Adds logging interception behavior.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <param name="builder">The interception builder.</param>
    /// <returns>The interception builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .Intercept(i =&gt; i.WithLogging())
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    public static IInterceptionBuilder<TService> WithLogging<TService>(
        this IInterceptionBuilder<TService> builder)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return GetInternalBuilder(builder).AddBehaviorFactory(sp =>
            new LoggingInterceptionBehavior<TService>(sp.GetService<ILoggerFactory>()));
    }

    /// <summary>
    /// Adds timeout interception behavior.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <param name="builder">The interception builder.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>The interception builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .Intercept(i =&gt; i.WithTimeout(TimeSpan.FromSeconds(5)))
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    public static IInterceptionBuilder<TService> WithTimeout<TService>(
        this IInterceptionBuilder<TService> builder,
        TimeSpan timeout)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return GetInternalBuilder(builder).AddBehaviorFactory(_ => new TimeoutInterceptionBehavior<TService>(timeout));
    }

    /// <summary>
    /// Adds retry interception behavior.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <param name="builder">The interception builder.</param>
    /// <param name="attempts">The retry attempts.</param>
    /// <returns>The interception builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .Intercept(i =&gt; i.WithRetry(3))
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    public static IInterceptionBuilder<TService> WithRetry<TService>(
        this IInterceptionBuilder<TService> builder,
        int attempts)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return GetInternalBuilder(builder).AddBehaviorFactory(_ => new RetryInterceptionBehavior<TService>(attempts));
    }

    /// <summary>
    /// Adds metrics interception behavior.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <param name="builder">The interception builder.</param>
    /// <returns>The interception builder.</returns>
    public static IInterceptionBuilder<TService> WithMetrics<TService>(
        this IInterceptionBuilder<TService> builder)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return GetInternalBuilder(builder).AddBehaviorFactory(sp =>
            new MetricsInterceptionBehavior<TService>(sp.GetService<ILoggerFactory>()));
    }

    /// <summary>
    /// Adds authorization interception behavior.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <param name="builder">The interception builder.</param>
    /// <returns>The interception builder.</returns>
    public static IInterceptionBuilder<TService> WithAuthorization<TService>(
        this IInterceptionBuilder<TService> builder)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return GetInternalBuilder(builder).AddBehaviorFactory(sp =>
            new AuthorizationInterceptionBehavior<TService>(sp.GetServices<IInterceptionAuthorizer<TService>>()));
    }

    /// <summary>
    /// Adds lazy interception behavior.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <param name="builder">The interception builder.</param>
    /// <returns>The interception builder.</returns>
    public static IInterceptionBuilder<TService> WithLazy<TService>(
        this IInterceptionBuilder<TService> builder)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return GetInternalBuilder(builder).AddBehaviorFactory(_ => new LazyInterceptionBehavior<TService>());
    }

    private static IInterceptionBuilderInternal<TService> GetInternalBuilder<TService>(IInterceptionBuilder<TService> builder)
        where TService : class
    {
        return builder as IInterceptionBuilderInternal<TService>
            ?? throw new InvalidOperationException("Unknown interception builder implementation.");
    }
}

/// <summary>
/// Represents a runtime interception behavior.
/// </summary>
/// <typeparam name="TService">The intercepted service contract.</typeparam>
public interface IInterceptionBehavior<TService>
    where TService : class
{
    /// <summary>
    /// Invokes the interception behavior.
    /// </summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invocation result.</returns>
    ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the invocation context used by runtime interception behaviors.
/// </summary>
/// <typeparam name="TService">The intercepted service contract.</typeparam>
public sealed class InterceptionInvocationContext<TService>
    where TService : class
{
    /// <summary>
    /// Gets the intercepted inner service.
    /// </summary>
    public required TService Inner { get; init; }

    /// <summary>
    /// Gets the intercepted method.
    /// </summary>
    public required MethodInfo Method { get; init; }

    /// <summary>
    /// Gets the invocation arguments.
    /// </summary>
    public required object[] Arguments { get; init; }

    /// <summary>
    /// Gets the current service provider.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Gets the detected cancellation token.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the next invocation delegate in the interception chain.
    /// </summary>
    public required Func<ValueTask<object>> Next { get; init; }
}

/// <summary>
/// Represents an authorization hook used by the built-in authorization interception behavior.
/// </summary>
/// <typeparam name="TService">The intercepted service contract.</typeparam>
public interface IInterceptionAuthorizer<TService>
    where TService : class
{
    /// <summary>
    /// Authorizes an invocation.
    /// </summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization result.</returns>
    ValueTask<IResult> AuthorizeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the first stage of adapter registration.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>

internal interface IInterceptionBuilderInternal<TService>
    where TService : class
{
    IInterceptionBuilder<TService> AddBehaviorFactory(Func<IServiceProvider, IInterceptionBehavior<TService>> factory);
}

internal sealed class InterceptionBuilder<TService>(
    ICollection<Type> interceptors,
    ICollection<Func<IServiceProvider, IInterceptionBehavior<TService>>> behaviorFactories)
    : IInterceptionBuilder<TService>, IInterceptionBuilderInternal<TService>
    where TService : class
{
    public IInterceptionBuilder<TService> With<TInterceptor>()
        where TInterceptor : class, TService
    {
        CompositionValidation.ValidateWrapper(typeof(TInterceptor), typeof(TService), "Interceptor");
        interceptors.Add(typeof(TInterceptor));
        return this;
    }

    public IInterceptionBuilder<TService> WithBehavior<TBehavior>()
        where TBehavior : class, IInterceptionBehavior<TService>
    {
        behaviorFactories.Add(sp => ActivatorUtilities.CreateInstance<TBehavior>(sp));
        return this;
    }

    public IInterceptionBuilder<TService> AddBehaviorFactory(Func<IServiceProvider, IInterceptionBehavior<TService>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        behaviorFactories.Add(factory);
        return this;
    }
}


internal static class RuntimeInterceptionHostFactory
{
    public static TService Create<TService>(
        IServiceProvider services,
        Func<TService> innerFactory,
        IReadOnlyList<Func<IServiceProvider, IInterceptionBehavior<TService>>> behaviorFactories)
        where TService : class
    {
        if (!typeof(TService).IsInterface)
        {
            throw new InvalidOperationException("Interception composition requires an interface service contract in this version.");
        }

        var proxy = DispatchProxy.Create<TService, DispatchProxyInterceptionHost<TService>>();
        ((DispatchProxyInterceptionHost<TService>)(object)proxy).Configure(
            services,
            innerFactory,
            behaviorFactories.Select(factory => factory(services)).ToArray());
        return proxy;
    }
}

internal class DispatchProxyInterceptionHost<TService> : DispatchProxy
    where TService : class
{
    private IServiceProvider services;
    private Lazy<TService> inner;
    private IReadOnlyList<IInterceptionBehavior<TService>> behaviors = Array.Empty<IInterceptionBehavior<TService>>();

    public void Configure(
        IServiceProvider services,
        Func<TService> innerFactory,
        IReadOnlyList<IInterceptionBehavior<TService>> behaviors)
    {
        this.services = services;
        this.inner = new Lazy<TService>(innerFactory);
        this.behaviors = behaviors;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        var invocation = this.InvokeCoreAsync(targetMethod, args);
        return InterceptionReturnValueAdapter.ToDeclaredReturn(targetMethod.ReturnType, invocation);
    }

    private async ValueTask<object> InvokeCoreAsync(MethodInfo method, object[] args)
    {
        var innerService = this.inner.Value;
        var token = InterceptionReturnValueAdapter.DetectCancellationToken(args);
        var index = -1;

        async ValueTask<object> Next()
        {
            index++;
            if (index < this.behaviors.Count)
            {
                var context = new InterceptionInvocationContext<TService>
                {
                    Inner = innerService,
                    Method = method,
                    Arguments = args,
                    Services = this.services,
                    CancellationToken = token,
                    Next = Next
                };

                return await this.behaviors[index].InvokeAsync(context, token).ConfigureAwait(false);
            }

            return await InterceptionReturnValueAdapter.InvokeTargetAsync(innerService, method, args).ConfigureAwait(false);
        }

        return await Next().ConfigureAwait(false);
    }
}

internal static class InterceptionReturnValueAdapter
{
    public static CancellationToken DetectCancellationToken(object[] arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument is CancellationToken token)
            {
                return token;
            }
        }

        return CancellationToken.None;
    }

    public static async ValueTask<object> InvokeTargetAsync(object target, MethodInfo method, object[] arguments)
    {
        try
        {
            var raw = method.Invoke(target, arguments);
            return await FromDeclaredReturnAsync(method.ReturnType, raw).ConfigureAwait(false);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    public static object ToDeclaredReturn(Type returnType, ValueTask<object> invocation)
    {
        if (returnType == typeof(void))
        {
            invocation.AsTask().GetAwaiter().GetResult();
            return null;
        }

        if (returnType == typeof(Task))
        {
            return AwaitTask(invocation);
        }

        if (returnType == typeof(ValueTask))
        {
            return new ValueTask(AwaitTask(invocation));
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return typeof(InterceptionReturnValueAdapter)
                .GetMethod(nameof(AwaitTaskGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(returnType.GenericTypeArguments[0])
                .Invoke(null, [invocation]);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            return typeof(InterceptionReturnValueAdapter)
                .GetMethod(nameof(AwaitValueTaskGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(returnType.GenericTypeArguments[0])
                .Invoke(null, [invocation]);
        }

        return invocation.AsTask().GetAwaiter().GetResult();
    }

    private static async Task AwaitTask(ValueTask<object> invocation)
    {
        _ = await invocation.ConfigureAwait(false);
    }

    private static async Task<TResult> AwaitTaskGeneric<TResult>(ValueTask<object> invocation)
    {
        var value = await invocation.ConfigureAwait(false);
        return value is null ? default : (TResult)value;
    }

    private static async ValueTask<TResult> AwaitValueTaskGeneric<TResult>(ValueTask<object> invocation)
    {
        var value = await invocation.ConfigureAwait(false);
        return value is null ? default : (TResult)value;
    }

    private static async ValueTask<object> FromDeclaredReturnAsync(Type returnType, object rawValue)
    {
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(Task))
        {
            await ((Task)rawValue).ConfigureAwait(false);
            return null;
        }

        if (returnType == typeof(ValueTask))
        {
            await ((ValueTask)rawValue).ConfigureAwait(false);
            return null;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            await ((Task)rawValue).ConfigureAwait(false);
            return returnType.GetProperty("Result")!.GetValue(rawValue);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            return await (ValueTask<object>)typeof(InterceptionReturnValueAdapter)
                .GetMethod(nameof(ReadValueTaskGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(returnType.GenericTypeArguments[0])
                .Invoke(null, [rawValue]);
        }

        return rawValue;
    }

    private static async ValueTask<object> ReadValueTaskGeneric<TResult>(ValueTask<TResult> task)
    {
        return await task.ConfigureAwait(false);
    }
}

internal static class InterceptionResultHelper
{
    public static bool TryGetResultState(object value, out bool isSuccess)
    {
        if (value is IResult result)
        {
            isSuccess = result.IsSuccess;
            return true;
        }

        isSuccess = false;
        return false;
    }

    public static bool IsResultFailure(object value)
    {
        return value is IResult result && result.IsFailure;
    }

    public static object CreateFailureResult(Type returnType, IResult source)
    {
        if (returnType == typeof(Result))
        {
            return Result.Failure().WithMessages(source.Messages).WithErrors(source.Errors);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            return typeof(InterceptionResultHelper)
                .GetMethod(nameof(CreateGenericFailureResult), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(returnType.GenericTypeArguments[0])
                .Invoke(null, [source]);
        }

        throw new UnauthorizedAccessException(
            source.Messages.FirstOrDefault() ?? source.Errors.FirstOrDefault()?.Message ?? "The invocation is not authorized.");
    }

    private static Result<T> CreateGenericFailureResult<T>(IResult source)
    {
        return Result<T>.Failure().WithMessages(source.Messages).WithErrors(source.Errors);
    }
}

internal sealed class LoggingInterceptionBehavior<TService>(ILoggerFactory loggerFactory) : IInterceptionBehavior<TService>
    where TService : class
{
    private readonly ILogger logger = loggerFactory?.CreateLogger($"{typeof(TService).FullName}.Interception.Logging");

    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken)
    {
        this.logger?.LogInformation(
            "[COMPOSITION] invoking {Service}.{Method}",
            typeof(TService).Name,
            context.Method.Name);

        try
        {
            var result = await context.Next().ConfigureAwait(false);
            if (InterceptionResultHelper.TryGetResultState(result, out var isSuccess))
            {
                this.logger?.LogInformation(
                    "[COMPOSITION] completed {Service}.{Method} (success={Success})",
                    typeof(TService).Name,
                    context.Method.Name,
                    isSuccess);
            }
            else
            {
                this.logger?.LogInformation(
                    "[COMPOSITION] completed {Service}.{Method}",
                    typeof(TService).Name,
                    context.Method.Name);
            }

            return result;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(
                ex,
                "[COMPOSITION] failed {Service}.{Method}",
                typeof(TService).Name,
                context.Method.Name);
            throw;
        }
    }
}

internal sealed class TimeoutInterceptionBehavior<TService>(TimeSpan timeout) : IInterceptionBehavior<TService>
    where TService : class
{
    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken)
    {
        var handler = new TimeoutHandler(timeout);
        return await handler.ExecuteAsync(
            _ => context.Next().AsTask(),
            cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class RetryInterceptionBehavior<TService>(int attempts, bool retryFailedResults = false) : IInterceptionBehavior<TService>
    where TService : class
{
    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken)
    {
        if (attempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts), "Retry attempts must be at least 1.");
        }

        if (!retryFailedResults)
        {
            var retryer = new Retryer(attempts, TimeSpan.Zero);
            return await retryer.ExecuteAsync(
                _ => context.Next().AsTask(),
                cancellationToken).ConfigureAwait(false);
        }

        Exception lastException = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var result = await context.Next().ConfigureAwait(false);
                if (!InterceptionResultHelper.IsResultFailure(result) || attempt == attempts)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == attempts)
                {
                    throw;
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Retry interception failed to produce a result.");
    }
}

internal sealed class MetricsInterceptionBehavior<TService>(ILoggerFactory loggerFactory) : IInterceptionBehavior<TService>
    where TService : class
{
    private readonly ILogger logger = loggerFactory?.CreateLogger($"{typeof(TService).FullName}.Interception.Metrics");

    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken)
    {
        var stopwatch = ValueStopwatch.StartNew();

        try
        {
            var result = await context.Next().ConfigureAwait(false);
            this.logger?.LogInformation(
                "[COMPOSITION] duration {Service}.{Method} -> {DurationMs}ms",
                typeof(TService).Name,
                context.Method.Name,
                stopwatch.GetElapsedMilliseconds());
            return result;
        }
        catch
        {
            this.logger?.LogWarning(
                "[COMPOSITION] duration failed {Service}.{Method} -> {DurationMs}ms",
                typeof(TService).Name,
                context.Method.Name,
                stopwatch.GetElapsedMilliseconds());
            throw;
        }
    }
}

internal sealed class AuthorizationInterceptionBehavior<TService>(IEnumerable<IInterceptionAuthorizer<TService>> authorizers)
    : IInterceptionBehavior<TService>
    where TService : class
{
    private readonly IReadOnlyList<IInterceptionAuthorizer<TService>> authorizers = authorizers?.ToArray() ?? [];

    public async ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken)
    {
        foreach (var authorizer in this.authorizers)
        {
            var result = await authorizer.AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return InterceptionResultHelper.CreateFailureResult(context.Method.ReturnType, result);
            }
        }

        return await context.Next().ConfigureAwait(false);
    }
}

internal sealed class LazyInterceptionBehavior<TService> : IInterceptionBehavior<TService>
    where TService : class
{
    public ValueTask<object> InvokeAsync(
        InterceptionInvocationContext<TService> context,
        CancellationToken cancellationToken)
    {
        return context.Next();
    }
}
