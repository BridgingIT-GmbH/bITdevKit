// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;

[UnitTest("Common")]
public class AsyncEnumerableExtensionsTests
{
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private sealed record Dummy(string Value);
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter

    [Fact]
    public async Task AnyAsyncTest()
    {
        (await this.CreateStubs<int>().AnyAsync()).ShouldBeFalse();
        (await this.CreateStubs(1, 2, 3).AnyAsync(item => item == 4)).ShouldBeFalse();
        (await this.CreateStubs(1, 2, 3).AnyAsync()).ShouldBeTrue();
        (await this.CreateStubs(1, 2, 3).AnyAsync(item => item == 2)).ShouldBeTrue();
    }

    [Fact]
    public async Task ContainsAsyncTest()
    {
        (await this.CreateStubs<int>().ContainsAsync(1)).ShouldBeFalse();
        (await this.CreateStubs(1, 2, 3).ContainsAsync(4)).ShouldBeFalse();
        (await this.CreateStubs(1, 2, 3).ContainsAsync(2)).ShouldBeTrue();
        (await this.CreateStubs("A").ContainsAsync("a", StringComparer.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public async Task CountAsyncTest()
    {
        (await this.CreateStubs<int>().CountAsync()).ShouldBe(0);
        (await this.CreateStubs(1, 2, 3).CountAsync(item => item == 4)).ShouldBe(0);
        (await this.CreateStubs(1, 2, 3).CountAsync()).ShouldBe(3);
        (await this.CreateStubs(1, 2, 3).CountAsync(item => item >= 2)).ShouldBe(2);
    }

    [Fact]
    public async Task DistinctAsyncTest()
    {
        (await this.CreateStubs<int>().DistinctAsync().ToListAsync()).ShouldBeEmpty();
        (await this.CreateStubs(1, 2, 1, 1, 2).DistinctAsync().ToListAsync()).ShouldBe(new[] { 1, 2 });
        (await this.CreateStubs("a", "A", "B", "b", "b").DistinctAsync(StringComparer.OrdinalIgnoreCase).ToListAsync()).ShouldBe(new[] { "a", "B" });
    }

    [Fact]
    public async Task DistinctByAsyncTest()
    {
        (await this.CreateStubs<Dummy>().DistinctByAsync(item => item.Value).ToListAsync()).ShouldBeEmpty();
        (await this.CreateStubs("a", "A", "B", "b", "b").SelectAsync(item => new Dummy(item)).DistinctByAsync(item => item.Value.ToUpperInvariant()).ToListAsync()).ShouldBe(new[] { new Dummy("a"), new Dummy("B") });
    }

    [Fact]
    public async Task FirstAsyncTest()
    {
        await new Func<Task>(async () => await this.CreateStubs<int>().FirstAsync()).ShouldThrowAsync<InvalidOperationException>();
        await new Func<Task>(async () => await this.CreateStubs(1, 2).FirstAsync(item => item == 3)).ShouldThrowAsync<InvalidOperationException>();

        (await this.CreateStubs<int>().FirstOrDefaultAsync()).ShouldBe(0);
        (await this.CreateStubs(1, 2, 3).FirstOrDefaultAsync()).ShouldBe(1);
        (await this.CreateStubs(1, 2, 3).FirstOrDefaultAsync(i => i == 2)).ShouldBe(2);
    }

    [Fact]
    public async Task LastAsyncTest()
    {
        await new Func<Task>(async () => await this.CreateStubs<int>().LastAsync()).ShouldThrowAsync<InvalidOperationException>();
        await new Func<Task>(async () => await this.CreateStubs(1, 2).LastAsync(item => item == 3)).ShouldThrowAsync<InvalidOperationException>();

        (await this.CreateStubs<int>().LastOrDefaultAsync()).ShouldBe(0);
        (await this.CreateStubs(1, 2, 3).LastOrDefaultAsync()).ShouldBe(3);
        (await this.CreateStubs(1, 2, 3).LastOrDefaultAsync(i => i == 2)).ShouldBe(2);
    }

    [Fact]
    public async Task WhereAsyncTest()
    {
        (await this.CreateStubs(1, 2, 3, 4).WhereAsync(item => item < 3).ToListAsync()).ShouldBe(new[] { 1, 2 });
        (await this.CreateStubs("a", null, string.Empty, " ", "A", "b").WhereNotNull().ToListAsync()).ShouldBe(new[] { "a", string.Empty, " ", "A", "b" });
        (await this.CreateStubs("a", null, string.Empty, " ", "A", "b").WhereNotNullOrEmpty().ToListAsync()).ShouldBe(new[] { "a", " ", "A", "b" });
        (await this.CreateStubs("a", null, string.Empty, " ", "A", "b").WhereNotNullOrWhiteSpace().ToListAsync()).ShouldBe(new[] { "a", "A", "b" });
    }

    [Fact]
    public async Task SkipAsyncTest()
    {
        (await this.CreateStubs(0, 1, 2, 3, 4).SkipAsync(2).ToListAsync()).ShouldBe(new[] { 2, 3, 4 });
    }

    [Fact]
    public async Task TakeAsyncTest()
    {
        (await this.CreateStubs(0, 1, 2, 3, 4).TakeAsync(3).ToListAsync()).ShouldBe(new[] { 0, 1, 2 });
    }

    private async IAsyncEnumerable<T> CreateStubs<T>(params T[] items)
    {
        await Task.Yield();
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}