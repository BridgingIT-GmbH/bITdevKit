// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Collections.Concurrent;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public sealed class AuditNotifierTests
{
    [Fact]
    public async Task ConcurrentAwaitedHandlers_RetainSeparateParentsInsideOneDispatch()
    {
        using var host = Create();
        using var services = host.CreateScope();
        var state = host.GetRequiredService<State>();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = audit.BeginScope(builder => builder.Key("customers.announce"));
        var publication = services.ServiceProvider.GetRequiredService<INotifier>().PublishAsync(new CustomerRenamedNotification(), new() { ExecutionMode = ExecutionMode.Concurrent });
        try
        {
            await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            audit.Current.ShouldBeSameAs(root);
        }
        finally
        {
            state.Release.TrySetResult();
        }

        (await publication).IsSuccess.ShouldBeTrue();
        root.Complete();
        await root.DisposeAsync();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        var dispatch = record.Actions.Single(action => action.Kind == "Notification");
        var handlers = record.Actions.Where(action => action.Kind == "NotificationHandler").ToArray();
        handlers.Length.ShouldBe(2);
        handlers.All(action => action.ParentActionId == dispatch.Id && action.Attempt == 1).ShouldBeTrue();
        var children = record.Actions.Where(action => action.Key.StartsWith("business.", StringComparison.Ordinal)).ToArray();
        children.Length.ShouldBe(2);
        children.Select(action => action.ParentActionId).Distinct().Count().ShouldBe(2);
        children.All(child => handlers.Any(handler => handler.Id == child.ParentActionId)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetachedHandlers_ResolveFreshScopesAndOwnIndependentRoots(bool suppressOrigin)
    {
        using var host = Create();
        var state = host.GetRequiredService<State>();
        Guid initiatingId;
        Guid initiatingDependency;
        using (var services = host.CreateScope())
        {
            initiatingDependency = services.ServiceProvider.GetRequiredService<ScopedDependency>().Id;
            var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
            var root = audit.BeginScope(builder => builder.Key("customers.initiate"));
            initiatingId = root.AuditId;
            using (suppressOrigin ? audit.Suppress() : null)
            {
                (await services.ServiceProvider.GetRequiredService<INotifier>().PublishAsync(new CustomerRenamedNotification(), new() { ExecutionMode = ExecutionMode.FireAndForget })).IsSuccess.ShouldBeTrue();
            }

            root.Complete();
            await root.DisposeAsync();
        }

        try
        {
            await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            state.Dependencies.ShouldNotContain(initiatingDependency);
            state.Dependencies.Distinct().Count().ShouldBe(2);
        }
        finally
        {
            state.Release.TrySetResult();
        }

        await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var plan = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var summaries = (await provider.SearchAsync(plan)).Value.ToArray();
        summaries.Length.ShouldBe(3);
        var initiating = (await provider.FindAsync(initiatingId)).Value;
        initiating.Actor.UserId.ShouldBe("U-42");
        var workers = await Task.WhenAll(summaries.Where(summary => summary.AuditId != initiatingId).Select(summary => provider.FindAsync(summary.AuditId)));
        foreach (var worker in workers.Select(result => result.Value))
        {
            worker.Kind.ShouldBe("NotificationHandler");
            worker.Actor.Kind.ShouldBe("Service");
            worker.Actor.UserId.ShouldBeNull();
            worker.ModuleName.ShouldBe("Notifications");
            worker.Actions.Single().Key.ShouldStartWith("business.");
            if (suppressOrigin)
            {
                worker.OriginAuditId.ShouldBe(worker.AuditId);
                worker.CausationAuditId.ShouldBeNull();
                worker.InitiatingActor.ShouldBeNull();
            }
            else
            {
                worker.OriginAuditId.ShouldBe(initiatingId);
                worker.CausationAuditId.ShouldBe(initiatingId);
                worker.CausationActionId.ShouldBe(initiating.Actions.Single().Id);
                worker.CorrelationId.ShouldBe(initiating.CorrelationId);
                worker.InitiatingActor.UserId.ShouldBe("U-42");
                worker.InitiatingModuleName.ShouldBe("Customers");
            }
        }

        state.DisposedDependencyUsed.ShouldBeFalse();
    }

    [Fact]
    public async Task NoHandlers_RecordsOnlyTheObservedDispatch()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier().WithAuditing();
        services.AddAuditing();
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<INotifier>().PublishAsync(new NoHandlerNotification())).IsSuccess.ShouldBeTrue();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var plan = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var record = (await provider.FindAsync((await provider.SearchAsync(plan)).Value.Single().AuditId)).Value;
        record.Details["source.executionStatus"].Data.GetString().ShouldBe("NoHandler");
        record.Actions.ShouldBeEmpty();
    }

    public sealed class NoHandlerNotification : NotificationBase;

    private static ServiceProvider Create()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<State>();
        services.AddScoped<ScopedDependency>();
        services.AddSingleton<IAuditModuleContextAccessor, SourceModule>();
        var user = Substitute.For<ICurrentUserAccessor>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("U-42");
        user.UserName.Returns("Customer editor");
        services.AddSingleton(user);
        services.AddNotifier().AddHandler<CustomerRenamedNotification, FirstHandler>().AddHandler<CustomerRenamedNotification, SecondHandler>().WithAuditing().WithAuditing();
        services.AddAuditing();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public sealed class State
    {
        private int calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<Guid> Dependencies { get; } = [];
        public bool DisposedDependencyUsed { get; private set; }
        public async Task<Result> HandleAsync(IAuditContext audit, ScopedDependency dependency, string name)
        {
            await using var business = audit.BeginScope(builder => builder.Key("business." + name));
            this.Dependencies.Add(dependency.Id);
            if (Interlocked.Increment(ref this.calls) == 2) this.Entered.TrySetResult();
            await this.Release.Task;
            if (dependency.Disposed) this.DisposedDependencyUsed = true;
            business.Complete();
            return Result.Success();
        }
    }
    public sealed class ScopedDependency : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Disposed { get; private set; }
        public void Dispose() => this.Disposed = true;
    }
    public sealed class SourceModule : IAuditModuleContextAccessor
    {
        public string Find(Type contextType) => contextType == typeof(CustomerRenamedNotification) ? "Customers"
            : contextType == typeof(FirstHandler) || contextType == typeof(SecondHandler) ? "Notifications" : null;
    }
    public sealed class CustomerRenamedNotification : NotificationBase;
    public sealed class FirstHandler(IAuditContext audit, State state, ScopedDependency dependency) : INotificationHandler<CustomerRenamedNotification>
    {
        public Task<Result> HandleAsync(CustomerRenamedNotification notification, PublishOptions options, CancellationToken cancellationToken) => state.HandleAsync(audit, dependency, "first");
    }
    public sealed class SecondHandler(IAuditContext audit, State state, ScopedDependency dependency) : INotificationHandler<CustomerRenamedNotification>
    {
        public Task<Result> HandleAsync(CustomerRenamedNotification notification, PublishOptions options, CancellationToken cancellationToken) => state.HandleAsync(audit, dependency, "second");
    }
}
