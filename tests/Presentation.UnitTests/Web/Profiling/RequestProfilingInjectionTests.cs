// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks controller and minimal API injection with enabled, disabled and omitted registration.</summary>
/// <example>Exercises binding and DI through real routed HTTP endpoints.</example>
public sealed class RequestProfilingInjectionTests
{
    /// <summary>Checks optional minimal API injection and installed middleware tolerate omitted registration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MinimalApi_OptionalInjection_PreservesExecutionWithOmittedOrDisabledSetup(bool registered)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (registered) { builder.Services.AddProfiling(o => o.Enabled(false)).WithOperationProfiling().WithRequestProfiling(); }

        await using var sut = builder.Build();
        sut.UseRequestProfiling();
        sut.UseRequestProfilingExceptionObserver();
        sut.UseRouting();
        sut.MapGet("/optional", ([FromServices] IOperationProfiler profiling = null) =>
        {
            profiling?.SetKey("Optional");
            using var segment = profiling?.BeginSegment("Work");
            segment?.Complete();
            return "executed";
        });
        await sut.StartAsync();
        var response = await sut.GetTestClient().GetAsync("/optional");
        (await response.Content.ReadAsStringAsync()).ShouldBe("executed");
        response.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
    }

    /// <summary>Checks a controller enriches the middleware-owned root through the same injected façade.</summary>
    [Fact]
    public async Task Controller_InjectedFacade_EnrichesExistingRequestRoot()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProfilingInjectionController).Assembly);
        builder.Services.AddProfiling(o => o.Enabled()).WithOperationProfiling().WithRequestProfiling();
        await using var sut = builder.Build();
        sut.UseRequestProfiling();
        sut.UseRequestProfilingExceptionObserver();
        sut.UseRouting();
        sut.MapControllers();
        await sut.StartAsync();
        await sut.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var response = await sut.GetTestClient().GetAsync("/profiling-injection?category=books");
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsByteArrayAsync();
        var id = Guid.Parse(response.Headers.GetValues(RequestProfilingMiddleware.HeaderName).Single());
        IResult<OperationProfilingRecord> result = null;
        for (var i = 0; i < 200; i++)
        {
            await sut.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
            result = await sut.Services.GetRequiredService<IOperationProfilingStore>().FindAsync(id);
            if (result.IsSuccess && result.Value is not null) { break; }

            await Task.Delay(5);
        }

        result.IsSuccess.ShouldBeTrue();
        result.Value.Key.ShouldBe("Products.List");
        result.Value.Dimensions.ShouldHaveSingleItem().Key.ShouldBe("category");
        result.Value.Http.Route.ShouldBe("profiling-injection");
    }
}

/// <summary>Provides the executable controller enrichment example used by HTTP integration tests.</summary>
/// <example><code>profiling.SetKey("Products.List"); profiling.SetDimension("category", category);</code></example>
[ApiController]
[Route("profiling-injection")]
public sealed class ProfilingInjectionController(IOperationProfiler profiling) : ControllerBase
{
    /// <summary>Enriches the existing root instead of starting another operation for the request.</summary>
    /// <example><code>GET /profiling-injection?category=books</code></example>
    [HttpGet]
    public IActionResult List([FromQuery] string category)
    {
        profiling.SetKey("Products.List");
        profiling.SetDimension("category", category);
        return this.Ok();
    }
}
