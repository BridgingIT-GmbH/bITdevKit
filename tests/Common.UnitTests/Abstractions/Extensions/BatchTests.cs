// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class BatchTests
{
    [Fact]
    public void Batch_ItemsExceedBatchSize_ReturnsCompleteAndPartialBatches()
    {
        var result = Enumerable.Range(1, 5)
            .Batch(2)
            .Select(batch => batch.ToArray())
            .ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBe([1, 2]);
        result[1].ShouldBe([3, 4]);
        result[2].ShouldBe([5]);
    }

    [Fact]
    public void Batch_NullSource_ReturnsEmptySequence()
    {
        var source = GetNull<IEnumerable<int>>();

        source.Batch(2).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Batch_NonPositiveBatchSize_ThrowsArgumentException(int batchSize)
    {
        Should.Throw<ArgumentException>(() => Array.Empty<int>().Batch(batchSize))
            .ParamName.ShouldBe(nameof(batchSize));
    }

    private static T GetNull<T>() => default;
}