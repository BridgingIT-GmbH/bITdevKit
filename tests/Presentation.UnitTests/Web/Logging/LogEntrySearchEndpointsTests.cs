namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Text;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class LogEntrySearchEndpointsTests
{
    [Fact]
    public async Task Search_ShouldAcceptAliasesWithoutHostJsonConfiguration()
    {
        var service = Substitute.For<ILogEntryService>();
        service.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<LogEntryModel>.Success([], 0, 1, 50));
        await using var app = await CreateApp(service);
        using var client = app.GetTestClient();
        using var body = new StringContent("""{"filters":[{"field":"Level","operator":"eq","value":"Error"}],"orderings":[{"field":"Id","direction":"desc"}]}""", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/_bdk/api/logentries/search", body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await service.Received(1).SearchAsync(Arg.Is<FilterModel>(filter =>
            filter.Filters.Single().Operator == FilterOperator.Equal && filter.Orderings.Single().Direction == OrderDirection.Descending), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_UnknownField_ShouldReturnBadRequest()
    {
        var service = Substitute.For<ILogEntryService>();
        service.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<LogEntryModel>.Failure().WithError(new ValidationError("Unknown search field 'Unknown'.")));
        await using var app = await CreateApp(service);
        using var client = app.GetTestClient();
        using var body = new StringContent("""{"filters":[{"field":"Unknown","operator":"eq","value":"value"}]}""", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/_bdk/api/logentries/search", body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<WebApplication> CreateApp(ILogEntryService service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(new LogEntryEndpointsOptions { RequireAuthorization = false });
        builder.Services.AddEndpoints<LogEntryEndpoints>();
        var app = builder.Build();
        app.UseRouting();
        app.MapEndpoints();
        await app.StartAsync();
        return app;
    }
}
