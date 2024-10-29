// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System.Collections.Concurrent;

[UnitTest("Common")]
public class ParallelForeachTests
{
    [Fact]
    public async Task ParallelForEachAsync()
    {
        var items = new ConcurrentBag<int>();
        await Enumerable.Range(1, 100)
            .ParallelForEachAsync(async i =>
            {
                await Task.Yield();
                items.Add(i);
            });

        items.Count.ShouldBe(100);
    }
}