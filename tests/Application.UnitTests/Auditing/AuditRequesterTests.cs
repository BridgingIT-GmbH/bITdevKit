// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditRequesterTests
{
    [Theory]
    [InlineData("normal", 1, 1)]
    [InlineData("reject", 0, 0)]
    [InlineData("cache", 0, 0)]
    [InlineData("retry", 3, 3)]
    public async Task RealRequester_CapturesDispatchOutsidePoliciesAndOnlyActualHandlerAttempts(string mode, int calls, int attempts)
    {
        var services = Services();
        services.AddAuditing();
        services.AddRequester().AddHandler<RenameCustomerCommand, string, RenameCustomerHandler>()
            .WithBehavior(typeof(EntryPolicy<,>)).WithBehavior(typeof(RetryPipelineBehavior<,>)).WithAuditing().WithAuditing();
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = host.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = audit.BeginScope(builder => builder.Key("customers.process"));
        var command = new RenameCustomerCommand { Mode = mode };
        var result = await scope.ServiceProvider.GetRequiredService<IRequester>().SendAsync<RenameCustomerCommand, string>(command);
        result.IsSuccess.ShouldBe(mode != "reject");
        host.GetRequiredService<State>().Calls.ShouldBe(calls);
        audit.Current.ShouldBeSameAs(root);
        root.Complete();
        await root.DisposeAsync();
        var record = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        var dispatch = record.Actions.Single(action => action.Kind == "Command");
        dispatch.Key.ShouldBe("customers.rename");
        dispatch.Feature.ShouldBe("Requester");
        dispatch.HandlerType.ShouldBe(typeof(RenameCustomerHandler).FullName);
        dispatch.RequestType.ShouldBe(typeof(RenameCustomerCommand).FullName);
        dispatch.Details["source.requestId"].Data.GetString().ShouldBe(command.RequestId.ToString("D"));
        dispatch.Resources.Single().Id.ShouldBe("C-42");
        dispatch.Details["Email"].Marker.ShouldBe("Excluded");
        dispatch.Outcome.ShouldBe(mode == "reject" ? AuditOutcome.Rejected : AuditOutcome.Succeeded);
        dispatch.ExecutionStatus.ShouldBe(attempts == 0 ? "ShortCircuited" : "Invoked");
        var invoked = record.Actions.Where(action => action.Kind == "RequestHandler").OrderBy(action => action.Sequence).ToArray();
        invoked.Length.ShouldBe(attempts);
        invoked.Select(action => action.Attempt).ShouldBe(Enumerable.Range(1, attempts).Select(attempt => (int?)attempt));
        invoked.All(action => action.ParentActionId == dispatch.Id && action.HandlerType == typeof(RenameCustomerHandler).FullName).ShouldBeTrue();
        if (mode == "retry")
        {
            invoked.Select(action => action.Outcome).ShouldBe([AuditOutcome.Failed, AuditOutcome.Failed, AuditOutcome.Succeeded]);
            record.HasFailedActions.ShouldBeTrue();
            record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        }

        System.Text.Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private@example.test");
        host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations)
            .Where(item => item.Feature == "Requester").Select(item => item.Selection).ShouldBe(["selected", "selected"]);
    }

    [Fact]
    public async Task BusinessException_PreservesInstanceAndDoesNotExecuteTwice()
    {
        var expected = new InvalidOperationException("private business message");
        var services = Services();
        services.AddAuditing();
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var behavior = new AuditingRequestBehavior<RenameCustomerCommand, IResult<string>>(audit);
        var calls = 0;
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => behavior.HandleAsync(new(), null, typeof(RenameCustomerHandler), () =>
        {
            calls++;
            throw expected;
        }));
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var plan = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var record = (await provider.FindAsync((await provider.SearchAsync(plan)).Value.Single().AuditId)).Value;
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        System.Text.Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain(expected.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrAbsent_PassesThroughWithoutSelectionOrMetadataCallbacks(bool registered)
    {
        var services = Services();
        if (registered) services.AddAuditing(options => options.Enabled(false));
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        var callback = 0;
        var options = new RequesterAuditOptions { Select = _ => { callback++; throw new InvalidOperationException(); } };
        var behavior = new AuditingRequestBehavior<RenameCustomerCommand, IResult<string>>(scope.ServiceProvider.GetService<IAuditContext>(), captureOptions: options);
        var expected = Task.FromResult<IResult<string>>(Result<string>.Success("unchanged"));
        behavior.HandleAsync(new(), null, typeof(RenameCustomerHandler), () => expected).ShouldBeSameAs(expected);
        (await expected).IsSuccess.ShouldBeTrue();
        callback.ShouldBe(0);
    }

    [Fact]
    public async Task UnselectedRead_IsLocalAndAllowsNestedExplicitBusinessCapture()
    {
        var services = Services();
        services.AddAuditing();
        services.AddRequester().AddHandler<FindCustomerQuery, string, FindCustomerHandler>().WithAuditing();
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IRequester>().SendAsync<FindCustomerQuery, string>(new())).IsSuccess.ShouldBeTrue();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var plan = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var stored = (await provider.FindAsync((await provider.SearchAsync(plan)).Value.Single().AuditId)).Value;
        stored.Key.ShouldBe("customers.explicit-read");
        stored.Actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ThrowingSelector_ReportsSafeFailureAndPreservesOneBusinessInvocation()
    {
        var services = Services();
        services.AddAuditing();
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var behavior = new AuditingRequestBehavior<RenameCustomerCommand, IResult<string>>(audit,
            captureOptions: new() { Select = _ => throw new InvalidOperationException("private selection payload") });
        var calls = 0;
        var result = await behavior.HandleAsync(new(), null, typeof(RenameCustomerHandler), () =>
        {
            calls++;
            return Task.FromResult<IResult<string>>(Result<string>.Success("unchanged"));
        });
        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        host.GetRequiredService<AuditRuntime>().SnapshotFailures()["AuditSourceSelectionFailed"].ShouldBe(1);
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<State>();
        return services;
    }

    public sealed class State
    {
        public int Calls { get; set; }
    }
    public sealed class RenameCustomerCommand : RequestBase<string>, IAuditDetails
    {
        public string Mode { get; init; } = "normal";
        public AuditDescriptor DescribeAudit() => AuditDescriptorBuilder.For<RenameCustomerCommand>().Key("customers.rename")
            .Resource("Customer", "C-42").Detail("Email", AuditValue.From("private@example.test")).Build();
    }
    [HandlerRetry(2, 0)]
    public sealed class RenameCustomerHandler(State state) : IRequestHandler<RenameCustomerCommand, string>
    {
        public Task<Result<string>> HandleAsync(RenameCustomerCommand request, SendOptions options, CancellationToken cancellationToken)
        {
            state.Calls++;
            if (request.Mode == "retry" && state.Calls < 3) throw new InvalidOperationException("private transient payload");
            return Task.FromResult(Result<string>.Success("renamed"));
        }
    }
    public sealed class EntryPolicy<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : class where TResponse : IResult
    {
        public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
        {
            if (request is RenameCustomerCommand { Mode: "reject" }) return Task.FromResult((TResponse)(object)Result<string>.Failure());
            if (request is RenameCustomerCommand { Mode: "cache" }) return Task.FromResult((TResponse)(object)Result<string>.Success("cached"));
            return next();
        }
        public bool IsHandlerSpecific() => false;
    }
    public sealed class FindCustomerQuery : RequestBase<string>;
    public sealed class FindCustomerHandler(IAuditContext audit) : IRequestHandler<FindCustomerQuery, string>
    {
        public Task<Result<string>> HandleAsync(FindCustomerQuery request, SendOptions options, CancellationToken cancellationToken) => audit.RunAsync(
            builder => builder.Key("customers.explicit-read"), (_, _) => Task.FromResult(Result<string>.Success("found")), cancellationToken);
    }
}
