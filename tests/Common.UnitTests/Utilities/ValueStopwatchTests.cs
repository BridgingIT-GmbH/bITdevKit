// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics;
using System.Threading.Tasks;

[UnitTest("Common")]
public class ValueStopwatchTests(ITestOutputHelper output) : TestsBase(output)
{
    [Fact]
    public void StartNew_IsNotActive_ReturnsFalse()
    {
        // Arrange & Act/Assert
        default(ValueStopwatch).IsActive.ShouldBeFalse();
    }

    [Fact]
    public void StartNew_IsActive_ReturnsTrue()
    {
        // Arrange & Act/Assert
        ValueStopwatch.StartNew().IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetElapsedTime_Start_ReturnsTimeElapsedSinceStart()
    {
        // Arrange
        var watch = new Stopwatch();
        var stopwatch = ValueStopwatch.StartNew();

        // Act
        watch.Start();
        await Task.Delay(200);
        watch.Stop();
        var elapsed = stopwatch.GetElapsedTime();

        // Assert
        elapsed.TotalMilliseconds.ShouldBeInRange(watch.ElapsedMilliseconds - 10, watch.ElapsedMilliseconds + 10);
    }

    [Fact]
    public async Task GetElapsedMilliseconds_Start_ReturnsMillisecondsSinceStart()
    {
        // Arrange
        var watch = new Stopwatch();
        var stopwatch = ValueStopwatch.StartNew();

        // Act
        watch.Start();
        await Task.Delay(200);
        watch.Stop();
        var elapsed = stopwatch.GetElapsedMilliseconds();

        // Assert
        elapsed.ShouldBeInRange(watch.ElapsedMilliseconds - 10, watch.ElapsedMilliseconds + 10);
    }
}
