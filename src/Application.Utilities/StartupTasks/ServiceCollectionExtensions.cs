// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Linq.Expressions;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using Extensions;

public static partial class ServiceCollectionExtensions
{
    private static StartupTaskServiceOptions contextOptions;

    /// <summary>
    /// Adds startup tasks.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext AddStartupTasks(this IServiceCollection services)
    {
        return services.AddStartupTasks(options: null);
    }

    /// <summary>
    /// Adds startup tasks.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext AddStartupTasks(
        this IServiceCollection services,
        Builder<StartupTaskServiceOptionsBuilder, StartupTaskServiceOptions> optionsBuilder)
    {
        return services.AddStartupTasks(optionsBuilder(new StartupTaskServiceOptionsBuilder()).Build());
    }

    /// <summary>
    /// Adds startup tasks.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext AddStartupTasks(
        this IServiceCollection services,
        StartupTaskServiceOptions options)
    {
        contextOptions ??= options ?? new StartupTaskServiceOptions();

        services.TryAddSingleton(contextOptions);
        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService<StartupTasksService>();
            services.TryAddBackgroundServiceHealthCheck<StartupTasksService>(
                nameof(StartupTasksService),
                tags: ["background", "startup-tasks"]);
        }

        return new StartupTasksBuilderContext(services);
    }

    /// <summary>
    /// Represents with task.
    /// </summary>
    /// <typeparam name="TTask">The task type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    public static StartupTasksBuilderContext WithTask<TTask>(this StartupTasksBuilderContext context)
        where TTask : class, IStartupTask
    {
        return context.WithTask<TTask>(new StartupTaskOptions());
    }

    /// <summary>
    /// Represents with task.
    /// </summary>
    /// <typeparam name="TTask">The task type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public static StartupTasksBuilderContext WithTask<TTask>(
        this StartupTasksBuilderContext context,
        Builder<StartupTaskOptionsBuilder, StartupTaskOptions> optionsBuilder)
        where TTask : class, IStartupTask
    {
        return context.WithTask<TTask>(optionsBuilder(new StartupTaskOptionsBuilder()).Build());
    }

    /// <summary>
    /// Represents with task.
    /// </summary>
    /// <typeparam name="TTask">The task type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    public static StartupTasksBuilderContext WithTask<TTask>(
        this StartupTasksBuilderContext context,
        StartupTaskOptions options)
        where TTask : class, IStartupTask
    {
        context.Services.AddSingleton(sp =>
            new StartupTaskDefinition { TaskType = typeof(TTask), Options = options ?? new StartupTaskOptions() });
        context.Services.AddScoped<TTask>();

        return context;
    }

    /// <summary>
    /// Executes the with task operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="implementationFactory">The implementation factory used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext WithTask(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTask> implementationFactory)
    {
        return context.WithTask(implementationFactory, new StartupTaskOptions());
    }

    /// <summary>
    /// Executes the with task operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="implementationFactory">The implementation factory used by the operation.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext WithTask(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTask> implementationFactory,
        Builder<StartupTaskOptionsBuilder, StartupTaskOptions> optionsBuilder)
    {
        return context.WithTask(implementationFactory, optionsBuilder(new StartupTaskOptionsBuilder()).Build());
    }

    /// <summary>
    /// Executes the with task operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="implementationFactory">The implementation factory used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext WithTask(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTask> implementationFactory,
        StartupTaskOptions options)
    {
        if (implementationFactory is not null)
        {
            // Temporarily create an instance to infer the type
            using var serviceProvider = context.Services.BuildServiceProvider();
            var instance = implementationFactory(serviceProvider);
            var implementationType = instance.GetType();

            context.Services.AddSingleton(sp =>
                new StartupTaskDefinition
                {
                    TaskType = implementationType,
                    Options = options ?? new StartupTaskOptions()
                });
            context.Services.AddScoped(implementationType, implementationFactory);
        }

        return context;
    }

    /// <summary>
    /// Provides with behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="behavior">The behavior used by the operation.</param>
    public static StartupTasksBuilderContext WithBehavior<TBehavior>(
        this StartupTasksBuilderContext context,
        IStartupTaskBehavior behavior = null)
        where TBehavior : class, IStartupTaskBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IStartupTaskBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IStartupTaskBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="implementationFactory">The implementation factory used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext WithBehavior(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTaskBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IStartupTaskBehavior), implementationFactory);
        }

        return context;
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="behavior">The behavior used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static StartupTasksBuilderContext WithBehavior(
        this StartupTasksBuilderContext context,
        IStartupTaskBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IStartupTaskBehavior), behavior);
        }

        return context;
    }

    private static Type GetImplementationType(Func<IServiceProvider, IStartupTask> factory)
    {
        // Create an expression representing the delegate
        Expression<Func<IServiceProvider, IStartupTask>> expression = sp => factory(sp);

        // Extract the body of the expression
        if (expression.Body is MethodCallExpression methodCall)
        {
            // Handle the case where the body is a method call
            if (methodCall.Method.ReturnType != typeof(IStartupTask))
            {
                throw new InvalidOperationException("The delegate does not return IStartupTask.");
            }

            // Analyze the method call to get the return type
            return methodCall.Method.ReturnType;
        }

        if (expression.Body is NewExpression newExpression)
        {
            // Handle the case where the body is a new expression
            return newExpression.Type;
        }

        if (expression.Body is MemberInitExpression memberInitExpression)
        {
            // Handle the case where the body is a member initialization expression
            return memberInitExpression.NewExpression.Type;
        }

        throw new InvalidOperationException("Unable to determine the implementation type.");
    }
}
