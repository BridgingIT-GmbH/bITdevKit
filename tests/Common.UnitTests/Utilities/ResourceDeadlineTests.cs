// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using Microsoft.Extensions.Time.Testing;

/// <summary>Checks transfer and fixed cancellation deadlines using a deterministic monotonic clock.</summary>
public sealed class ResourceDeadlineTests
{
    /// <summary>Progress renews inactivity while the absolute lifetime remains fixed at construction.</summary>
    [Fact]
    public void Progress_RepeatedBeforeIdleExpiry_StillExpiresAtAbsoluteLifetime()
    {
        var time = new FakeTimeProvider();
        var notified = 0;
        using var sut = new TransferDeadline(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), () => notified++, time);
        sut.Start();

        time.Advance(TimeSpan.FromSeconds(2));
        sut.Progress();
        time.Advance(TimeSpan.FromSeconds(2));
        sut.Progress();
        sut.HasExpired.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(1));

        sut.Token.IsCancellationRequested.ShouldBeTrue();
        sut.HasExpired.ShouldBeTrue();
        notified.ShouldBe(1);
    }

    /// <summary>Deferring initial timer scheduling does not move the absolute deadline.</summary>
    [Fact]
    public void Start_AfterAbsoluteLifetime_ReportsExpiryImmediately()
    {
        var time = new FakeTimeProvider();
        using var sut = new TransferDeadline(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), timeProvider: time);
        time.Advance(TimeSpan.FromSeconds(6));

        sut.Start();

        sut.HasExpired.ShouldBeTrue();
    }

    /// <summary>Long absolute lifetimes remain valid when the inactivity check fits the timer's scheduling interval.</summary>
    [Fact]
    public void Start_LongLifetime_UsesShorterIdleDeadline()
    {
        var time = new FakeTimeProvider();
        using var sut = new TransferDeadline(TimeSpan.FromSeconds(1), TimeSpan.FromDays(365), timeProvider: time);

        sut.Start();
        time.Advance(TimeSpan.FromSeconds(1));

        sut.HasExpired.ShouldBeTrue();
    }

    /// <summary>Notification faults and reentrant disposal do not prevent the terminal transfer callback.</summary>
    [Fact]
    public void TransferExpiry_ThrowingCallbacksAndReentrantDisposal_NotifiesOnce()
    {
        var time = new FakeTimeProvider();
        var notified = 0;
        using var sut = new TransferDeadline(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5),
            () => { notified++; throw new InvalidOperationException("Observer failure."); }, time);
        var token = sut.Token;
        using var disposal = token.Register(sut.Dispose);
        using var failure = token.Register(() => throw new InvalidOperationException("Notification failure."));
        sut.Start();

        time.Advance(TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(5));

        token.IsCancellationRequested.ShouldBeTrue();
        notified.ShouldBe(1);
    }

    /// <summary>Fixed deadlines observe callback faults while allowing every callback and reentrant disposal to finish.</summary>
    [Fact]
    public void FixedDeadline_ThrowingCallbacksAndObserver_ContainsFaults()
    {
        var time = new FakeTimeProvider();
        var notified = 0;
        var observed = 0;
        using var sut = new CancellationDeadline(TimeSpan.FromSeconds(5), time,
            exception => { exception.ShouldBeOfType<AggregateException>(); observed++; throw new InvalidOperationException("Observer failure."); });
        var token = sut.Token;
        using var disposal = token.Register(() => { notified++; sut.Dispose(); });
        using var failure = token.Register(() => throw new InvalidOperationException("Notification failure."));

        time.Advance(TimeSpan.FromSeconds(5));
        sut.Cancel();

        token.IsCancellationRequested.ShouldBeTrue();
        notified.ShouldBe(1);
        observed.ShouldBe(1);
    }

    /// <summary>Disposal before expiry releases timer ownership without requesting cancellation.</summary>
    [Fact]
    public void FixedDeadline_DisposedBeforeExpiry_DoesNotCancel()
    {
        var time = new FakeTimeProvider();
        var sut = new CancellationDeadline(TimeSpan.FromSeconds(5), time);
        var token = sut.Token;

        sut.Dispose();
        time.Advance(TimeSpan.FromSeconds(10));
        sut.Cancel();

        token.IsCancellationRequested.ShouldBeFalse();
    }
}