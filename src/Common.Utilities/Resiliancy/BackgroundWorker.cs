namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages long-running background tasks with cancellation and progress reporting.
/// </summary>
/// <remarks>
/// Initializes a new instance of the BackgroundWorker class with the specified work.
/// </remarks>
/// <param name="work">The asynchronous work to execute in the background, accepting a CancellationToken and IProgress<int> for progress reporting.</param>
/// <param name="handleErrors">If true, catches and logs exceptions from the work; otherwise, throws them. Defaults to false.</param>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="progress">An optional progress reporter for background operations. Defaults to null.</param>
/// <example>
/// <code>
/// var worker = new BackgroundWorker(async (ct, progress) =>
/// {
///     for (int i = 0; i <= 100; i += 10)
///     {
///         await Task.Delay(100, ct);
///         progress.Report(i);
///     }
/// }, progress: new Progress<BackgroundWorkerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Percentage: {p.ProgressPercentage}%")));
/// worker.ProgressChanged += (s, e) => Console.WriteLine($"Legacy Progress: {e.ProgressPercentage}%");
/// await worker.StartAsync(CancellationToken.None);
/// </code>
/// </example>
public class BackgroundWorker(
    Func<CancellationToken, IProgress<int>, Task> work,
    bool handleErrors = false,
    ILogger logger = null,
    IProgress<BackgroundWorkerProgress> progress = null)
{
    private readonly Func<CancellationToken, IProgress<int>, Task> work = work ?? throw new ArgumentNullException(nameof(work));
    private CancellationTokenSource cts = new();
    private Task task;
    private readonly IProgress<BackgroundWorkerProgress> progress = progress;

    /// <summary>
    /// Delegate type for the ProgressChanged event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event arguments containing the progress percentage.</param>
    public delegate void ProgressChangedHandler(object sender, ProgressChangedEventArgs e);

    /// <summary>
    /// Event raised when the background work reports progress.
    /// </summary>
    public event ProgressChangedHandler ProgressChanged;

    /// <summary>
    /// Gets the current status of the background task.
    /// </summary>
    /// <example>
    /// <code>
    /// var worker = new BackgroundWorker(async (ct, progress) => await Task.Delay(100, ct));
    /// Console.WriteLine($"Task status: {worker.Status}");
    /// </code>
    /// </example>
    public TaskStatus Status => this.task?.Status ?? TaskStatus.Created;

    /// <summary>
    /// Starts the background work.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for background operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the background work is already running.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<BackgroundWorkerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Percentage: {p.ProgressPercentage}%"));
    /// var worker = new BackgroundWorker(async (ct, p) =>
    /// {
    ///     for (int i = 0; i <= 100; i += 10)
    ///     {
    ///         await Task.Delay(100, ct);
    ///         p.Report(i);
    ///     }
    /// });
    /// worker.ProgressChanged += (s, e) => Console.WriteLine($"Legacy Progress: {e.ProgressPercentage}%");
    /// await worker.StartAsync(cts.Token, progress);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task StartAsync(CancellationToken cancellationToken = default, IProgress<BackgroundWorkerProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        if (this.task?.IsCompleted == false)
        {
            throw new InvalidOperationException("Background work is already running.");
        }

        this.cts = new CancellationTokenSource();
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, cancellationToken))
        {
            var legacyProgress = new Progress<int>(value =>
            {
                ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(value));
                progress?.Report(new BackgroundWorkerProgress(value, $"Progress updated to {value}%"));
            });
            this.task = Task.Run(async () =>
            {
                try
                {
                    await this.work(linkedCts.Token, legacyProgress);
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation
                }
                catch (Exception ex) when (handleErrors)
                {
                    logger?.LogError(ex, "Background work failed.");
                }
            }, linkedCts.Token);
            await this.task;
        }
    }

    /// <summary>
    /// Cancels the background work.
    /// </summary>
    /// <example>
    /// <code>
    /// var worker = new BackgroundWorker(async (ct, progress) => await Task.Delay(1000, ct));
    /// await worker.StartAsync(CancellationToken.None);
    /// worker.Cancel(); // Cancels the background work
    /// </code>
    /// </example>
    public void Cancel()
    {
        this.cts.Cancel();
    }
}

/// <summary>
/// Event arguments for the ProgressChanged event, containing the progress percentage.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ProgressChangedEventArgs class with the specified progress percentage.
/// </remarks>
/// <param name="progressPercentage">The progress percentage (0 to 100).</param>
public class ProgressChangedEventArgs(int progressPercentage) : EventArgs
{
    /// <summary>
    /// Gets the progress percentage (0 to 100).
    /// </summary>
    public int ProgressPercentage { get; } = progressPercentage;
}

/// <summary>
/// A fluent builder for configuring and creating a BackgroundWorker instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the BackgroundWorkerBuilder with the specified work.
/// </remarks>
/// <param name="work">The asynchronous work to execute in the background, accepting a CancellationToken and IProgress<int> for progress reporting.</param>
public class BackgroundWorkerBuilder(Func<CancellationToken, IProgress<int>, Task> work)
{
    private bool handleErrors = false;
    private ILogger logger = null;
    private IProgress<BackgroundWorkerProgress> progress = null;

    /// <summary>
    /// Configures the background worker to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The BackgroundWorkerBuilder instance for chaining.</returns>
    public BackgroundWorkerBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Configures the background worker to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for background operations.</param>
    /// <returns>The BackgroundWorkerBuilder instance for chaining.</returns>
    public BackgroundWorkerBuilder WithProgress(IProgress<BackgroundWorkerProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured BackgroundWorker instance.
    /// </summary>
    /// <returns>A configured BackgroundWorker instance.</returns>
    public BackgroundWorker Build()
    {
        return new BackgroundWorker(
            work,
            this.handleErrors,
            this.logger,
            this.progress);
    }
}