// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
[ShortRunJob]
public abstract class SerializerBenchmarkBase(ITestOutputHelper output) : TestsBase(output)
{
    private readonly StubModel data = new()
    {
        IntProperty = 1,
        StringProperty = "test",
        ListProperty = [1],
        ObjectProperty = new StubModel { IntProperty = 1 }
    };

    private ISerializer serializer;

    private byte[] serializedData;

    [GlobalSetup]
    public void Setup()
    {
        this.serializer = this.GetSerializer();
        this.serializedData = this.serializer.SerializeToBytes(this.data);
    }

    [Benchmark]
    public byte[] Serialize()
    {
        return this.serializer.SerializeToBytes(this.data);
    }

    [Benchmark]
    public StubModel Deserialize()
    {
        return this.serializer.Deserialize<StubModel>(this.serializedData);
    }

    [Benchmark]
    public StubModel RoundTrip()
    {
        var data = this.serializer.SerializeToBytes(this.data);
        return this.serializer.Deserialize<StubModel>(data);
    }

    protected abstract ISerializer GetSerializer();
}