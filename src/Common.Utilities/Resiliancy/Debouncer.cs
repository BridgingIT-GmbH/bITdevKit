namespace BridgingIT.DevKit.Common.Utilities;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides debouncing functionality to delay execution of an action until a specified interval has passed since the last call.
/// </summary>
/// <remarks>
/// This class ensures that the provided action is executed only once after a series of rapid calls,
/// effectively debouncing the action to prevent multiple executions within the specified delay period.
/// </remarks>
/// <example>
/// <code>
/// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => await Task.Delay(100, ct),
///     progress: new Progress&lt;DebouncerProgress&gt;(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingDelay.TotalSeconds}s")));
/// await debouncer.DebounceAsync(CancellationToken.None); // Delays execution by 1 second
/// </code>
/// </example>
public class Debouncer : IDisposable
{
    private readonly TimeSpan delay;
    private readonly Func<CancellationToken, Task> action;
    private readonly IProgress<DebouncerProgress> progress;
    private CancellationTokenSource cts;
    private Task currentTask;
    private readonly Lock lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Debouncer"/> class.
    /// </summary>
    /// <param name="delay">The delay interval before executing the action.</param>
    /// <param name="action">The asynchronous action to execute after the delay.</param>
    /// <param name="progress">An optional progress reporter for debouncing operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null.</exception>
    /// <example>
    /// <code>
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct =>
    /// {
    ///     Console.WriteLine("Action executed");
    ///     await Task.Delay(100, ct);
    /// }, progress: new Progress&lt;DebouncerProgress&gt;(p => Console.WriteLine(p.Status)));
    /// </code>
    /// </example>
    public Debouncer(
        TimeSpan delay,
        Func<CancellationToken, Task> action,
        IProgress<DebouncerProgress> progress = null)
    {
        this.delay = delay;
        this.action = action ?? throw new ArgumentNullException(nameof(action));
        this.progress = progress;
        this.cts = new CancellationTokenSource();
        this.currentTask = Task.CompletedTask;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Debouncer"/> class with a simpler action that does not require a CancellationToken.
    /// </summary>
    /// <param name="delay">The delay interval before executing the action in debounce mode.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="progress">An optional progress reporter for debouncing operations. Defaults to null.</param>
    /// <example>
    /// <code>
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async () => await Task.Delay(100),
    ///     progress: new Progress&lt;DebouncerProgress&gt;(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingDelay.TotalSeconds}s")));
    /// await debouncer.DebounceAsync(CancellationToken.None); // Delays execution by 1 second
    /// </code>
    /// </example>
    public Debouncer(
        TimeSpan delay,
        Func<Task> action,
        IProgress<DebouncerProgress> progress = null)
        : this(delay, ct => action(), progress)
    {
    }

    /// <summary>
    /// Triggers the debounced action, delaying execution until the specified interval has passed since the last call.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Each call to this method cancels any previous pending operation and starts a new delay period.
    /// The action will only be executed if no new calls are made within the delay period.
    /// </remarks>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// await debouncer.DebounceAsync(cts.Token); // Action executes after 1 second
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public Task DebounceAsync(CancellationToken cancellationToken = default)
    {
        lock (this.lockObject)
        {
            // Cancel any previous pending operation
            this.cts.Cancel();
            this.cts.Dispose();
            this.cts = new CancellationTokenSource();
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, cancellationToken);

        this.currentTask = Task.Run(async () =>
        {
            try
            {
                // Report that the debounce operation has been queued
                this.progress?.Report(new DebouncerProgress(this.delay, false, "Debounce queued"));
                await Task.Delay(this.delay, linkedCts.Token);
                if (!linkedCts.IsCancellationRequested)
                {
                    // Report that the action is being executed
                    this.progress?.Report(new DebouncerProgress(TimeSpan.Zero, false, "Executing debounced action"));
                    await this.action(linkedCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Report that the debounce operation was canceled
                this.progress?.Report(new DebouncerProgress(TimeSpan.Zero, false, "Debounce canceled"));
            }
            finally
            {
                linkedCts.Dispose();
            }
        }, cancellationToken);

        return this.currentTask;
    }

    /// <summary>
    /// Disposes of the debouncer, canceling any pending actions and releasing resources.
    /// </summary>
    /// <example>
    /// <code>
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// debouncer.Dispose(); // Cancels any pending actions and cleans up
    /// </code>
    /// </example>
    public void Dispose()
    {
        lock (this.lockObject)
        {
            this.cts.Cancel();
            this.cts.Dispose();
        }
    }
}

/// <summary>
/// A fluent builder for configuring and creating a <see cref="Debouncer"/> instance.
/// </summary>
/// <example>
/// <code>
/// var debouncer = new DebouncerBuilder(TimeSpan.FromSeconds(1), async ct => await Task.Delay(100, ct))
///     .WithProgress(new Progress&lt;DebouncerProgress&gt;(p => Console.WriteLine($"Progress: {p.Status}")))
///     .Build();
/// await debouncer.DebounceAsync(CancellationToken.None); // Delays execution by 1 second
/// </code>
/// </example>
public class DebouncerBuilder
{
    private readonly TimeSpan delay;
    private readonly Func<CancellationToken, Task> action;
    private IProgress<DebouncerProgress> progress;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncerBuilder"/> with the specified delay and action.
    /// </summary>
    /// <param name="delay">The delay interval for debouncing.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    public DebouncerBuilder(TimeSpan delay, Func<Task> action)
    {
        this.delay = delay;
        this.action = ct => action();
        this.progress = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncerBuilder"/> with the specified delay and action that accepts a CancellationToken.
    /// </summary>
    /// <param name="delay">The delay interval for debouncing.</param>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    public DebouncerBuilder(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        this.delay = delay;
        this.action = action;
        this.progress = null;
    }

    /// <summary>
    /// Configures the debouncer to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for debouncing operations.</param>
    /// <returns>The <see cref="DebouncerBuilder"/> instance for chaining.</returns>
    public DebouncerBuilder WithProgress(IProgress<DebouncerProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured <see cref="Debouncer"/> instance.
    /// </summary>
    /// <returns>A configured <see cref="Debouncer"/> instance.</returns>
    public Debouncer Build()
    {
        return new Debouncer(
            this.delay,
            this.action,
            this.progress);
    }
}