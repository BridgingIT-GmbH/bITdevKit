// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Threading;

[UnitTest("Common")]
public class IdGeneratorTests : TestsBase
{
    public IdGeneratorTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void Create_AsExpected()
    {
        // Arrange & Act
        var sut1 = IdGenerator.Create();
        this.Output?.WriteLine($"generated: {sut1}");
        var sut2 = IdGenerator.Create();
        this.Output?.WriteLine($"generated: {sut2}");
        var sut3 = IdGenerator.Create();
        this.Output?.WriteLine($"generated: {sut3}");
        Thread.Sleep(150);
        var sut4 = IdGenerator.Create();
        this.Output?.WriteLine($"generated: {sut4}");

        // Assert
        Assert.NotEqual(string.Empty, sut1);
        sut1.Length.ShouldBe(20);
        Assert.NotEqual(string.Empty, sut2);
        sut2.Length.ShouldBe(20);
        Assert.NotEqual(string.Empty, sut3);
        sut3.Length.ShouldBe(20);
        Assert.NotEqual(string.Empty, sut4);
        sut4.Length.ShouldBe(20);
        Assert.NotEqual(sut1, sut2);
        Assert.NotEqual(sut3, sut1);

        sut2.ShouldBeGreaterThan(sut1);
        sut3.ShouldBeGreaterThan(sut2);
        sut4.ShouldBeGreaterThan(sut3);
    }

    [Fact]
    public void Create_Benchmark()
    {
        this.Benchmark(() => { var id = IdGenerator.Create(); }, 10000);
    }
}
