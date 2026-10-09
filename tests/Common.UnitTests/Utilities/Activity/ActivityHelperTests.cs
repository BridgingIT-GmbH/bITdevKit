// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics;
using Shouldly;
using Xunit;

/// <summary>Verifies exact-once business execution when background work has no tracing activity.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~ActivityHelperTests</code></example>
public sealed class ActivityHelperTests
{
    /// <summary>A null ambient activity executes the original delegate once without falling through to a null source.</summary>
    [Fact]
    public async Task StartActvity_NullActivity_ExecutesOnceWithOriginalToken()
    {
        Activity sut = null;
        var calls = 0;
        using var cancellation = new CancellationTokenSource();

        await sut.StartActvity(
            "background:publish",
            (activity, token) =>
            {
                calls++;
                activity.ShouldBeNull();
                token.ShouldBe(cancellation.Token);
                return Task.CompletedTask;
            },
            cancellationToken: cancellation.Token
        );

        calls.ShouldBe(1);
    }

    /// <summary>A null activity preserves the original asynchronous business exception.</summary>
    [Fact]
    public async Task StartActvity_NullActivity_PreservesBusinessException()
    {
        Activity sut = null;
        var expected = new InvalidOperationException("Business failure");
        var calls = 0;

        var actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.StartActvity(
                "background:publish",
                (_, _) =>
                {
                    calls++;
                    return Task.FromException(expected);
                }
            )
        );

        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
    }

    /// <summary>An existing activity without an observer keeps the single execution path.</summary>
    [Fact]
    public async Task StartActvity_ExistingActivity_ExecutesOnce()
    {
        using var sut = new Activity("parent").Start();
        var calls = 0;

        await sut.StartActvity(
            "background:publish",
            (_, _) =>
            {
                calls++;
                return Task.CompletedTask;
            }
        );

        calls.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(sut);
    }
}
