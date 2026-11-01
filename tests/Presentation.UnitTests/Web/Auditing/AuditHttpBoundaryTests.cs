// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Auditing;

public sealed class AuditHttpBoundaryTests
{
    [Theory]
    [InlineData("exception", AuditOutcome.Failed)]
    [InlineData("request-canceled", AuditOutcome.Canceled)]
    [InlineData("unrelated-cancellation", AuditOutcome.Failed)]
    public async Task UnhandledFailures_PreserveExceptionAndFinalizeDespiteRequestCancellation(string mode, AuditOutcome outcome)
    {
        var services = new ServiceCollection();
        services.AddAuditing().WithHttpRequests();
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception expected = mode == "exception" ? new InvalidOperationException("private source message") : new OperationCanceledException(cancellation.Token);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST";
        context.Request.Path = "/customers/change";
        if (mode == "request-canceled") context.RequestAborted = cancellation.Token;
        var runtime = root.GetRequiredService<AuditRuntime>();
        var middleware = new AuditHttpMiddleware(_ => throw expected, root.GetRequiredService<AuditHttpOptions>(), runtime);
        Exception actual = null;
        try { await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IAuditContext>()); }
        catch (Exception error) { actual = error; }

        actual.ShouldBeSameAs(expected);
        await runtime.WaitForIdleAsync();
        var provider = root.GetRequiredService<IAuditingStorageProvider>();
        var plan = root.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var record = (await provider.FindAsync((await provider.SearchAsync(plan)).Value.Single().AuditId)).Value;
        record.Outcome.ShouldBe(outcome);
        context.Features.Get<IAuditHttpErrorFeature>().ShouldBeNull();
        scope.ServiceProvider.GetRequiredService<IAuditContext>().Current.ShouldBeNull();
    }

    [Fact]
    public async Task AlreadyStartedResponse_RemainsUnchangedWhenBusinessThrows()
    {
        var services = new ServiceCollection();
        services.AddAuditing().WithHttpRequests(options => options.IncludeAuditIdHeader = true);
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var response = new StartedResponse { StatusCode = 202 };
        response.Headers["Existing"] = "preserved";
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Method = "POST";
        context.Request.Path = "/customers/change";
        var expected = new InvalidOperationException("private source message");
        var middleware = new AuditHttpMiddleware(_ => throw expected, root.GetRequiredService<AuditHttpOptions>(), root.GetRequiredService<AuditRuntime>());
        (await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IAuditContext>()))).ShouldBeSameAs(expected);
        response.StatusCode.ShouldBe(202);
        response.Headers["Existing"].ToString().ShouldBe("preserved");
        response.Headers.ContainsKey("X-Audit-Id").ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false, "late-user")]
    [InlineData(true, false, "initial-user")]
    [InlineData(false, true, null)]
    public async Task ActorRefresh_PreservesAuthenticatedIdentityAndDoesNotReplaceKnownOutcomeOnFailure(bool initial, bool fail, string expectedId)
    {
        var authenticated = initial;
        var userId = "initial-user";
        var user = Substitute.For<ICurrentUserAccessor>();
        user.IsAuthenticated.Returns(_ => authenticated);
        user.UserId.Returns(_ => fail && authenticated ? throw new InvalidOperationException("private actor source") : userId);
        user.UserName.Returns("Customer editor");
        var services = new ServiceCollection();
        services.AddSingleton(user);
        services.AddAuditing().WithHttpRequests();
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = "POST";
        var runtime = root.GetRequiredService<AuditRuntime>();
        var middleware = new AuditHttpMiddleware(_ =>
        {
            authenticated = true;
            userId = "late-user";
            return Task.CompletedTask;
        }, root.GetRequiredService<AuditHttpOptions>(), runtime);
        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IAuditContext>());
        var provider = root.GetRequiredService<IAuditingStorageProvider>();
        var plan = root.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var record = (await provider.FindAsync((await provider.SearchAsync(plan)).Value.Single().AuditId)).Value;
        record.Actor.UserId.ShouldBe(expectedId);
        record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        if (fail) runtime.SnapshotFailures()["AuditActorUnavailable"].ShouldBe(1);
    }

    public sealed class StartedResponse : IHttpResponseFeature
    {
        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = new MemoryStream();

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state) => throw new InvalidOperationException("A started response must not register response mutation.");

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
