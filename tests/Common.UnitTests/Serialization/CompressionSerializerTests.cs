// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

[UnitTest("Common")]
public class CompressionSerializerTests : SerializerTestsBase
{
    public CompressionSerializerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public override void CanRoundTripStream_Test()
    {
        base.CanRoundTripStream_Test();
    }

    [Fact]
    public override void CanRoundTripStream_Benchmark()
    {
        base.CanRoundTripStream_Benchmark();
    }

    [Fact]
    public override void CanRoundTripPrivateConstructorStream_Test()
    {
        base.CanRoundTripPrivateConstructorStream_Test();
    }

    [Fact]
    public override void CanRoundTripEmptyStream_Test()
    {
        base.CanRoundTripEmptyStream_Test();
    }

    [Fact]
    public override void CanRoundTripBytes_Test()
    {
        base.CanRoundTripBytes_Test();
    }

    [Fact]
    public override void CanRoundTripString_Test()
    {
        base.CanRoundTripString_Test();
    }

    [Fact(Skip = "Skip benchmarks for now")]
    public virtual void RunBenchmarks()
    {
        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<JsonNetSerializerBenchmark>();
    }

    protected override ISerializer GetSerializer()
    {
        return new CompressionSerializer(new JsonNetSerializer());
    }
}

public class CompressionSerializerBenchmark : SerializerBenchmarkBase
{
    public CompressionSerializerBenchmark(ITestOutputHelper output)
        : base(output)
    {
    }

    protected override ISerializer GetSerializer()
    {
        return new CompressionSerializer(new JsonNetSerializer());
    }
}