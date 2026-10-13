// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public sealed class OperationProfilingHttpMetadataTests
{
    [Fact]
    public void HttpMetadata_DeclaresAndObservesBytesSeparately_WithSamplingEvidence()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var operation = profiler.BeginOperation("/reports", OperationProfilingKind.HttpRequest);
        var metadata = new HttpRequestProfilingMetadata
        {
            Method = "POST", Path = "/reports", Route = "/reports", StatusCode = 201, ApplicationRequestId = "request-1",
            DeclaredRequestBytes = 100, RequestBytes = 50, RequestBytesQuality = ProfilingObservationQuality.Partial,
            DeclaredResponseBytes = 200, ResponseBytes = 100, ResponseBytesQuality = ProfilingObservationQuality.Complete,
            ActiveSelectedRequestsAtEntry = 3, SamplingStrategyKey = "Probability", SamplingConfigurationKey = "half", SamplingInclusionProbability = 0.5,
        };

        // Act
        operation.SetHttpMetadata(metadata);
        operation.Complete();
        operation.Dispose();

        // Assert
        var http = sink.Records.Single().Http;
        http.ShouldBe(metadata);
        http.RequestBytes.ShouldNotBe(http.DeclaredRequestBytes);
        http.ResponseBytes.ShouldNotBe(http.DeclaredResponseBytes);
        http.SamplingInclusionProbability.ShouldBe(0.5);
    }

    [Theory]
    [InlineData("probability")]
    [InlineData("declared")]
    [InlineData("status")]
    [InlineData("selected")]
    [InlineData("policy")]
    public void HttpMetadata_InvalidReplacement_PreservesAcceptedProjection(string invalid)
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        var operation = profiler.BeginOperation("http");
        var accepted = new HttpRequestProfilingMetadata { StatusCode = 200, SamplingStrategyKey = "All" };
        operation.SetHttpMetadata(accepted);
        var rejected = invalid switch
        {
            "probability" => accepted with { SamplingInclusionProbability = double.NaN },
            "declared" => accepted with { DeclaredResponseBytes = -1 },
            "status" => accepted with { StatusCode = 600 },
            "selected" => accepted with { ActiveSelectedRequestsAtEntry = 0 },
            _ => accepted with { SamplingConfigurationKey = new string('x', 129) },
        };

        // Act
        operation.SetHttpMetadata(rejected);
        operation.Complete();
        operation.Dispose();

        // Assert
        sink.Records.Single().Http.ShouldBe(accepted);
        sink.Records.Single().Quality.RejectedMetadataCount.ShouldBe(1);
    }
}
