namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Text;
using System.Text.Json;

public sealed class PresentationProblemDetailsOpenApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_DocumentedProblems_MatchTheSerializedContract(bool disclose)
    {
        await using var fixture = PresentationProblemDetailsHostTests.CreateHost("Mixed", disclose);
        using var client = fixture.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/problem-details.json"));
        var root = document.RootElement;
        foreach (var path in new[] { "throw/validation", "result/validation", "throw/server", "result/server" })
        {
            var expected = path.EndsWith("validation", StringComparison.Ordinal) ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError;
            var route = "/api/problem-details/" + path;
            var schema = ResponseSchema(root, route, "get", (int)expected);
            var properties = schema.GetProperty("properties");
            if (schema.TryGetProperty("required", out var required))
            {
                var names = required.EnumerateArray().Select(item => item.GetString()).ToArray();
                names.ShouldNotContain("data");
                names.ShouldNotContain("exceptionDetails");
            }

            var data = properties.GetProperty("data");
            var map = Resolve(root, data).GetProperty("properties").GetProperty("errors");
            map.GetProperty("type").GetString().ShouldBe("object");
            var messages = map.GetProperty("additionalProperties");
            messages.GetProperty("type").GetString().ShouldBe("array");
            messages.GetProperty("items").GetProperty("type").GetString().ShouldBe("string");
            var diagnostics = properties.GetProperty("exceptionDetails");
            diagnostics.GetProperty("type").GetString().ShouldBe("array");
            var entry = Resolve(root, diagnostics.GetProperty("items"));
            var entryFields = entry.GetProperty("properties");
            foreach (var field in new[] { "type", "message" })
            {
                entry.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ShouldContain(field);
                entryFields.GetProperty(field).GetProperty("type").GetString().ShouldBe("string");
            }

            entryFields.GetProperty("innerExceptions").GetProperty("items").GetProperty("$ref").GetString()
                .ShouldBe(diagnostics.GetProperty("items").GetProperty("$ref").GetString());

            using var response = await client.GetAsync(route);
            using var live = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, expected);
            if (expected == HttpStatusCode.BadRequest)
            {
                live.RootElement.GetProperty("data").GetProperty("errors").ValueKind.ShouldBe(JsonValueKind.Object);
            }
            else
            {
                live.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBe(disclose);
            }
        }

        using var unsupportedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/problem-details/result/server");
        unsupportedRequest.Headers.Add("Accept", "application/xml");
        using var unsupportedResponse = await client.SendAsync(unsupportedRequest);
        using var fallback = await PresentationProblemDetailsHostTests.ReadProblemAsync(unsupportedResponse, HttpStatusCode.InternalServerError);
        fallback.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();

        const string mvc = "/api/problem-details/mvc/model-state";
        var nativeSchema = ResponseSchema(root, mvc, "post", 400);
        nativeSchema.GetProperty("properties").TryGetProperty("errors", out _).ShouldBeTrue();
        using var invalidModel = await client.PostAsync(mvc, new StringContent("{}", Encoding.UTF8, "application/json"));
        using var native = await PresentationProblemDetailsHostTests.ReadProblemAsync(invalidModel, HttpStatusCode.BadRequest);
        native.RootElement.GetProperty("errors").ValueKind.ShouldBe(JsonValueKind.Object);
        native.RootElement.TryGetProperty("data", out _).ShouldBeFalse();
    }

    private static JsonElement ResponseSchema(JsonElement document, string path, string method, int status) =>
        Resolve(document, document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses")
            .GetProperty(status.ToString(System.Globalization.CultureInfo.InvariantCulture)).GetProperty("content")
            .GetProperty("application/problem+json").GetProperty("schema"));

    private static JsonElement Resolve(JsonElement document, JsonElement schema)
    {
        while (schema.TryGetProperty("$ref", out var reference))
        {
            schema = reference.GetString()!.Split('/').Skip(1).Aggregate(document, (node, name) => node.GetProperty(name));
        }

        return schema;
    }
}
