// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;

[UnitTest("Common")]
public class GuidGeneratorTests(ITestOutputHelper output) : TestsBase(output)
{
    [Fact]
    public void Create_AsExpected()
    {
        // Arrange & Act
        var sut1 = GuidGenerator.Create("abcdefg");
        this.Output?.WriteLine($"generated: {sut1}");
        var sut2 = GuidGenerator.Create("abcdefg");
        this.Output?.WriteLine($"generated: {sut2}");
        var sut3 = GuidGenerator.Create("aaabbbd");
        this.Output?.WriteLine($"generated: {sut3}");
        var sut4 = GuidGenerator.Create(null);
        this.Output?.WriteLine($"generated: {sut4}");

        // Assert
        Assert.NotEqual(Guid.Empty, sut1);
        Assert.Equal(sut2.ToString(), sut1.ToString());
        Assert.NotEqual(sut3.ToString(), sut1.ToString());
        Assert.Equal(Guid.Empty.ToString(), sut4.ToString());
    }

    [Fact]
    public void CreateSequential_AsExpected()
    {
        // Arrange & Act
        var sut1 = GuidGenerator.CreateSequential();
        this.Output?.WriteLine($"generated: {sut1}");
        var sut2 = GuidGenerator.CreateSequential();
        this.Output?.WriteLine($"generated: {sut2}");
        var sut3 = GuidGenerator.CreateSequential();
        this.Output?.WriteLine($"generated: {sut3}");
        var sut4 = GuidGenerator.CreateSequential();
        this.Output?.WriteLine($"generated: {sut4}");

        // Assert
        Assert.NotEqual(Guid.Empty, sut1);
        Assert.NotEqual(sut2.ToString(), sut1.ToString());
        Assert.NotEqual(sut3.ToString(), sut1.ToString());
        Assert.NotEqual(Guid.Empty.ToString(), sut4.ToString());
    }

    [Fact]
    public void Create_Benchmark()
    {
        this.Benchmark(() => GuidGenerator.Create("abcdefg"), 10000);
    }

    [Fact]
    public void CreateSequential_Benchmark()
    {
        this.Benchmark(() => GuidGenerator.CreateSequential(), 10000);
    }
}
