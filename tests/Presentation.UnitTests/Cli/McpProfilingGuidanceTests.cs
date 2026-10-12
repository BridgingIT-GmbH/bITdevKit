namespace BridgingIT.DevKit.Presentation.UnitTests.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Cli;

[UnitTest("Presentation")]
public sealed class McpProfilingGuidanceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Guidance_ProfilingTopic_IsDiscoverableAndAdvertisedBySchema()
    {
        var listed = new McpGuidanceTools().List(JsonSerializer.SerializeToElement(new { }));
        var topics = JsonSerializer.SerializeToElement(listed.Data, Json).GetProperty("topics");
        topics.EnumerateArray().ShouldContain(topic => topic.GetProperty("topic").GetString() == "profiling");

        var schema = JsonSerializer.SerializeToElement(new McpToolCatalog().Tools.Single(tool => tool.Name == "bdk_guidance_get").InputSchema, Json);
        schema.GetProperty("properties").GetProperty("topic").GetProperty("enum").EnumerateArray().ShouldContain(topic => topic.GetString() == "profiling");
    }

    [Fact]
    public void Get_ProfilingTopic_ReturnsCodingLifecycleAndExamplesWithoutRuntimeDependencies()
    {
        var response = new McpGuidanceTools().Get(JsonSerializer.SerializeToElement(new { topic = "profiling" }));

        response.Available.ShouldBeTrue();
        var data = JsonSerializer.SerializeToElement(response.Data, Json);
        data.GetProperty("topic").GetString().ShouldBe("profiling");
        data.GetProperty("docs").GetString().ShouldBe("docs/features-profiling.md");
        var steps = string.Join("\n", data.GetProperty("steps").EnumerateArray().Select(step => step.GetString()));
        steps.ShouldContain("JoinOrStartAsync");
        steps.ShouldContain("Root and segment outcomes are separate");
        steps.ShouldContain("bounded queue");
        steps.ShouldContain("do not use entity IDs");
        steps.ShouldContain("classify");
        steps.ShouldContain("When a runtime is available");
        var examples = data.GetProperty("examples");
        examples.GetArrayLength().ShouldBe(3);
        examples.EnumerateArray().ShouldAllBe(example => example.GetProperty("language").GetString() == "csharp");
        var code = string.Join("\n", examples.EnumerateArray().Select(example => example.GetProperty("code").GetString()));
        code.ShouldContain("WithOperationProfiling");
        code.ShouldContain("RunSegmentAsync");
        code.ShouldContain("CorrelationId.Current");
        code.ShouldContain("OperationProfilingHelpers.ClassifyResult");
        code.ShouldContain("segment.Complete()");
        code.ShouldContain("operation.Cancel()");
        code.ShouldContain("operation.Fail(exception)");
        response.Next.ShouldContain(call => call.Tool == "bdk_docs_get");
        var api = response.Next.Single(call => call.Tool == "bdk_api_search");
        var arguments = JsonSerializer.SerializeToElement(api.Arguments, Json);
        arguments.GetProperty("query").GetString().ShouldBe("IOperationProfiler");
        arguments.TryGetProperty("topic", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("how do operation recordings work?")]
    [InlineData("how can I use nested segments?")]
    [InlineData("add HTTP request profiling with IOperationProfiler")]
    [InlineData("when should I use RunSegmentAsync or JoinOrStartAsync?")]
    public void Get_ProfilingOrSegmentQuery_InfersProfilingGuidance(string query)
    {
        var response = new McpGuidanceTools().Get(JsonSerializer.SerializeToElement(new { query }));

        response.Available.ShouldBeTrue();
        JsonSerializer.SerializeToElement(response.Data, Json).GetProperty("topic").GetString().ShouldBe("profiling");
    }

    [Fact]
    public void Get_JobAndPipelineProfilingQuery_CombinesFeatureGuidanceWithoutDuplicateTopics()
    {
        var response = new McpGuidanceTools().Get(JsonSerializer.SerializeToElement(new { query = "add operation profiling and nested segments to a job that runs a pipeline" }));

        response.Available.ShouldBeTrue();
        var topics = JsonSerializer.SerializeToElement(response.Data, Json).GetProperty("topics").EnumerateArray().ToArray();
        topics.Select(topic => topic.GetProperty("topic").GetString()).ShouldBe(["jobs", "pipelines", "profiling"], ignoreOrder: true);
        topics.Single(topic => topic.GetProperty("topic").GetString() == "profiling").GetProperty("examples").GetArrayLength().ShouldBe(3);
        response.Next.Count(call => call.Tool == "bdk_docs_get" && JsonSerializer.Serialize(call.Arguments).Contains("features-profiling.md")).ShouldBe(1);
    }
}
