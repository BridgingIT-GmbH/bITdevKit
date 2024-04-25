// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

[UnitTest("Common")]
public class ForeachTests
{
    [Fact]
    public async Task ForEachAsync()
    {
        // Arrange
        var items = new ConcurrentBag<int>();

        // Act
        await Enumerable.Range(1, 100).ForEachAsync(async i =>
        {
            await Task.Yield();
            items.Add(i);
        });

        // Assert
        items.Count.ShouldBe(100);
    }
}