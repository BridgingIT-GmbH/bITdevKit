namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Executes mapped MVC errors and inspects their serialized JSON in tests.</summary>
public static class ResultActionResultExecution
{
    /// <summary>Executes the supplied action result and verifies the selected status.</summary>
    public static async Task<JsonElement> ExecuteAsync(ActionResult result, int status)
    {
        await using var services = new ServiceCollection().AddLogging().AddResultHttpProblemDetails().BuildServiceProvider();
        await using var output = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = output;
        await result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));
        context.Response.StatusCode.ShouldBe(status);
        output.Position = 0;
        using var document = await JsonDocument.ParseAsync(output);
        return document.RootElement.Clone();
    }
}
