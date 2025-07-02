// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides simple (non cancellable) debouncing functionality to delay execution of an action until a specified interval has passed since the last call.
/// </summary>
public class SimpleDebouncer : IDisposable
{
    private readonly TimeSpan delay;
    private readonly Func<Task> action;
    private System.Timers.Timer timer;
    private bool isPending;

    /// <summary>
    /// Initializes a new instance of the Debouncer class.
    /// </summary>
    /// <param name="delay">The delay interval before executing the action.</param>
    /// <param name="action">The async action to execute after the delay.</param>
    public SimpleDebouncer(TimeSpan delay, Func<Task> action)
    {
        this.delay = delay;
        this.action = action ?? throw new ArgumentNullException(nameof(action));
        this.timer = new System.Timers.Timer(delay.TotalMilliseconds)
        {
            AutoReset = false
        };
        this.timer.Elapsed += async (s, e) =>
        {
            this.isPending = false;
            await action();
        };
    }

    /// <summary>
    /// Triggers the debounced action, delaying execution until the specified interval has passed since the last call.
    /// </summary>
    public void Debounce()
    {
        if (this.isPending)
        {
            this.timer.Stop();
        }
        this.isPending = true;
        this.timer.Start();
    }

    /// <summary>
    /// Disposes of the debouncer, stopping the timer.
    /// </summary>
    public void Dispose()
    {
        this.timer?.Dispose();
        this.timer = null;
    }
}