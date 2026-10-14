// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

public class RequesterCircuitBreakerTests
{
    [Fact]
    public async Task CircuitBreakerBehavior_WhenConsecutiveFailuresReachThreshold_OpensCircuitUntilBreakDurationElapsed()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        new RequesterBuilder(services)
            .AddHandlers()
            .WithBehavior(typeof(CircuitBreakerPipelineBehavior<,>));
        var requester = services.BuildServiceProvider().GetRequiredService<IRequester>();
        CircuitBreakerTestRequestHandler.Calls = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await requester.SendAsync(new CircuitBreakerTestRequest());

        // Assert (the handler is not invoked while the circuit is open for the 1 second break duration)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Success");
        CircuitBreakerTestRequestHandler.Calls.ShouldBe(4);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(900);
    }
}

public class CircuitBreakerTestRequest : RequestBase<string>;

[HandlerCircuitBreaker(3, 1, 20)]
public class CircuitBreakerTestRequestHandler : IRequestHandler<CircuitBreakerTestRequest, string>
{
    public static int Calls { get; set; }

    public async Task<Result<string>> HandleAsync(CircuitBreakerTestRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (++Calls <= 3)
        {
            throw new InvalidOperationException("Simulated failure");
        }

        return Result<string>.Success("Success");
    }
}
