// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Linq;
using System.Threading.Tasks;
using Shouldly;

[UnitTest("Common")]
public class StreamExtensionsTests
{
    [Fact]
    public void ReadToEndTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5).ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var result = stream.ReadToEnd();

        result.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task ReadToEndAsyncTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5).ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var result = await stream.ReadToEndAsync();

        result.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public void TryReadAllTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5).ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var buffer = new byte[5];
        stream.TryReadAll(buffer, 0, 5);

        buffer.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task TryReadAllAsyncTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5).ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var buffer = new byte[5];
        await stream.TryReadAllAsync(buffer, 0, 5);

        buffer.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task ToMemoryStreamAsyncTest()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5).ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(1, SeekOrigin.Begin);

        using var copy = await stream.ToMemoryStreamAsync();

        copy.ToArray().ShouldBe([1, 2, 3, 4]);
    }
}