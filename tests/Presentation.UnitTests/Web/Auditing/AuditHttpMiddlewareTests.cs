// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Text;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditHttpMiddlewareTests
{
    [Theory]
    [InlineData(200, AuditOutcome.Succeeded)]
    [InlineData(401, AuditOutcome.Rejected)]
    [InlineData(504, AuditOutcome.Failed)]
    public async Task InvokeAsync_ObservedStatusAndNestedWork_RecordOneSanitizedRoot(int status, AuditOutcome outcome)
    {
        using var services = AuditWebTestServices.Create(builder => builder.WithHttpRequests());
        using var scope = services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var http = AuditWebTestServices.Context(scope.ServiceProvider);
        http.Request.Method = "POST";
        http.Request.Path = "/customers/PRIVATE-IDENTIFIER/rename";
        http.Request.QueryString = new("?secret=query-secret");
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("body-secret"));
        http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/customers/{id}/rename"), 0, new(), "Rename customer"));
        var middleware = new AuditHttpMiddleware(async context =>
        {
            await using var action = audit.BeginScope(builder => builder.Key("customers.apply-name"));
            action.Complete();
            context.Response.StatusCode = status;
        }, services.GetRequiredService<AuditHttpOptions>(), services.GetRequiredService<AuditRuntime>());

        await middleware.InvokeAsync(http, audit);

        var record = await ReadSingleAsync(services);
        record.Kind.ShouldBe("Http");
        record.Outcome.ShouldBe(outcome);
        record.Http.Status.ShouldBe(status);
        record.Http.Path.ShouldBe("/customers/{id}/rename");
        record.Actions.ShouldHaveSingleItem().Key.ShouldBe("customers.apply-name");
        var encoded = Encoding.UTF8.GetString(AuditRecordCodec.Encode(record));
        encoded.ShouldNotContain("PRIVATE-IDENTIFIER");
        encoded.ShouldNotContain("query-secret");
        encoded.ShouldNotContain("body-secret");
        audit.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData("GET", false, false, false)]
    [InlineData("GET", true, false, true)]
    [InlineData("POST", false, true, false)]
    [InlineData("POST", false, false, true)]
    public async Task InvokeAsync_BusinessReadSelection_RecordsOnlySelectedRequests(string method, bool selected, bool markedRead, bool recorded)
    {
        using var services = AuditWebTestServices.Create(builder => builder.WithHttpRequests(options => options.SelectBusinessRead = _ => selected));
        using var scope = services.CreateScope();
        var http = AuditWebTestServices.Context(scope.ServiceProvider);
        http.Request.Method = method;
        if (markedRead) http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new(new AuditHttpReadMetadata()), "Customer search"));
        var called = false;
        var middleware = new AuditHttpMiddleware(_ => { called = true; return Task.CompletedTask; }, services.GetRequiredService<AuditHttpOptions>(), services.GetRequiredService<AuditRuntime>());

        await middleware.InvokeAsync(http, scope.ServiceProvider.GetRequiredService<IAuditContext>());

        called.ShouldBeTrue();
        var plan = services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        (await services.GetRequiredService<IAuditingStorageProvider>().SearchAsync(plan)).TotalCount.ShouldBe(recorded ? 1 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeAsync_ExcludedBoundary_SuppressesRequiresNewAndNestedCapture(bool metadata)
    {
        using var services = AuditWebTestServices.Create(builder => builder.WithHttpRequests());
        using var scope = services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var http = AuditWebTestServices.Context(scope.ServiceProvider);
        http.Request.Method = "POST";
        http.Request.Path = metadata ? "/customers/review" : "/health/business";
        if (metadata) http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new(new AuditSuppressionMetadata()), "Review"));
        var calls = 0;
        var middleware = new AuditHttpMiddleware(async _ =>
        {
            calls++;
            await using var nested = audit.BeginScope(builder => builder.Key("excluded.business").Mode(AuditScopeMode.RequiresNew));
            nested.IsRecording.ShouldBeFalse();
        }, services.GetRequiredService<AuditHttpOptions>(), services.GetRequiredService<AuditRuntime>());

        await middleware.InvokeAsync(http, audit);

        calls.ShouldBe(1);
        var plan = services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        (await services.GetRequiredService<IAuditingStorageProvider>().SearchAsync(plan)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public void WithHttpRequests_SharedBlacklistDefaultsAndConfiguredArrays_AreCopied()
    {
        new RequestLoggingOptions().PathBlackListPatterns.ShouldBe(new AuditHttpOptions().PathBlackListPatterns);
        var patterns = new[] { "/private*" };
        using var services = AuditWebTestServices.Create(builder => builder.WithHttpRequests(options => options.PathBlackListPatterns = patterns));
        patterns[0] = "/changed*";
        services.GetRequiredService<AuditHttpOptions>().PathBlackListPatterns.ShouldBe(["/private*"]);
        var defaults = new AuditHttpOptions();
        defaults.PathBlackListPatterns[0] = "/changed*";
        new AuditHttpOptions().PathBlackListPatterns[0].ShouldBe("/*.js");
    }

    private static async Task<AuditRecord> ReadSingleAsync(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IAuditingStorageProvider>();
        var plan = services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        return (await provider.FindAsync((await provider.SearchAsync(plan)).Value.Single().AuditId)).Value;
    }
}
