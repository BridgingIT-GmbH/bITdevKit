// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Generic saga scope implementation for coordinating distributed transactions using the compensating transaction pattern.
///
/// This class orchestrates multi-step workflows where each successful step registers a compensation function.
/// If any step fails, all registered compensations are executed in reverse order (LIFO - Last In First Out)
/// to undo previous changes. This implements the saga pattern for maintaining consistency across
/// distributed operations without traditional two-phase commit.
/// </summary>
/// <remarks>
/// <para>
/// **Typical Usage Scenario:**
/// When booking a trip, you might need to coordinate:
/// 1. Book a flight (register compensation to cancel it)
/// 2. Book a hotel (register compensation to cancel it)
/// 3. Rent a car (register compensation to cancel it)
///
/// If step 3 fails, compensations are executed in reverse: cancel car → cancel hotel → cancel flight.
/// </para>
///
/// <para>
/// **Key Features:**
/// - Lazy initialization: compensations are registered but not executed until needed
/// - Resilient execution: if one compensation fails, others still execute
/// - Order preservation: compensations execute in strict LIFO order
/// - Error tracking: all compensation failures are recorded for diagnostics
/// - Named steps: each compensation can be labeled for better logging and tracing
/// - Conditional execution: compensations can have conditions that determine if they execute
/// - Event hooks: subscribe to compensation lifecycle events for monitoring and alerting
/// - Saga context: track correlation IDs and custom metadata across distributed operations
/// - Integrated logging: optional ILogger for structured logging throughout saga execution
/// </para>
///
/// <para>
/// **Implementation Pattern:**
/// This class implements <see cref="IOperationScope"/>, making it compatible with the Result pattern's
/// <see cref="ResultOperationScope{T, TOperation}"/> for clean, fluent operation chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create saga with optional logger
/// var saga = new SagaOperationScope(logger);
/// saga.Context.CorrelationId = correlationId;
/// saga.Context.SetMetadata("UserId", userId);
///
/// // Subscribe to compensation events
/// saga.OnCompensationEvent += async (evt) =>
/// {
///     if (evt.EventType == CompensationEventType.Failed)
///     {
///         await alertService.NotifyAsync($"Compensation {evt.StepName} failed");
///     }
/// };
///
/// // Chain operations with named compensations
/// var result = await Result&lt;TripBooking&gt;.Success(booking)
///     .StartOperation(saga)
///          .BindAsync(async (b, ct) =>
///          {
///              var flight = await flightService.BookAsync(b.FlightDetails, ct);
///              // Register named compensation
///              saga.RegisterCompensation("FlightCancellation",
///                  async ct => await flightService.CancelAsync(flight.Id, ct));
///              b.FlightConfirmation = flight.ConfirmationNumber;
///              return Result&lt;TripBooking&gt;.Success(b);
///          }, CancellationToken.None)
///          .BindAsync(async (b, ct) =>
///          {
///              var hotel = await hotelService.BookAsync(b.HotelDetails, ct);
///              // Register conditional compensation (only if payment was captured)
///              saga.RegisterCompensation("HotelCancellation",
///                  async ct => await hotelService.CancelAsync(hotel.Id, ct),
///                  async ct => await paymentService.IsCapturedAsync(hotel.PaymentId, ct));
///              b.HotelConfirmation = hotel.ConfirmationNumber;
///              return Result&lt;TripBooking&gt;.Success(b);
///          }, CancellationToken.None)
///     .EndOperationAsync(CancellationToken.None);
/// </code>
/// </example>
public class SagaOperationScope : IOperationScope
{
    /// <summary>
    /// Internal list of registered compensation descriptors with their metadata.
    /// </summary>
    private readonly List<SagaCompensationDescriptor> compensations = [];

    /// <summary>
    /// Internal list of exceptions that occurred during compensation execution.
    /// </summary>
    private readonly List<Exception> compensationErrors = [];

    /// <summary>
    /// Logger for structured logging throughout saga execution.
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    /// Tracks the maximum number of compensations registered at any point.
    /// </summary>
    private int maxCompensationCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SagaOperationScope"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for structured logging. If null, a null logger is used.</param>
    public SagaOperationScope(ILogger logger = null)
    {
        this.logger = logger ?? NullLogger.Instance;
        this.Context = new SagaContext();

        this.logger.LogDebug("Saga {SagaId} initialized", this.Context.SagaId);
    }

    /// <summary>
    /// Occurs when a compensation event happens during saga execution.
    /// Subscribe to this event for monitoring, alerting, or custom handling.
    /// </summary>
    public event CompensationEventHandler OnCompensationEvent;

    /// <summary>
    /// Gets a value indicating whether the saga completed successfully and committed all changes.
    /// When true, all compensations have been cleared as they were not needed.
    /// </summary>
    /// <remarks>
    /// This is mutually exclusive with <see cref="IsRolledBack"/>.
    /// Both can be false if the saga has not yet completed.
    /// </remarks>
    public bool IsCommitted { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the saga encountered a failure and rolled back all changes.
    /// When true, compensations have been executed to undo previous steps.
    /// </summary>
    /// <remarks>
    /// This is mutually exclusive with <see cref="IsCommitted"/>.
    /// Both can be false if the saga has not yet completed.
    /// </remarks>
    public bool IsRolledBack { get; private set; }

    /// <summary>
    /// Gets the saga context containing correlation IDs, metadata, and tracing information.
    /// </summary>
    public SagaContext Context { get; }

    /// <summary>
    /// Gets the total number of compensations that were registered during the saga execution.
    /// This value is preserved even after commit (when compensations are cleared) for audit trails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This count represents the peak number of registered compensations and is useful for:
    /// - Verifying all expected steps completed successfully
    /// - Auditing workflow depth
    /// - Testing compensation count expectations
    /// </para>
    /// </remarks>
    public int CompensationCount => this.maxCompensationCount;

    /// <summary>
    /// Gets the number of compensations that were executed during rollback.
    /// Each execution attempt (success or failure) increments this counter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This counter increments for each compensation function invoked, regardless of outcome.
    /// Use <see cref="CompensationErrors"/> to determine which compensations failed.
    /// </para>
    /// <para>
    /// Example: If 3 compensations are registered and rollback occurs:
    /// - CompensationExecutedCount will be 3
    /// - CompensationErrors.Count may be 0-3 depending on which failed
    /// </para>
    /// </remarks>
    public int CompensationExecutedCount { get; private set; }

    /// <summary>
    /// Gets a read-only list of exceptions that occurred during compensation execution.
    /// Each failed compensation adds an exception to this collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Failed compensations do not prevent other compensations from executing.
    /// This "best-effort" rollback strategy ensures maximum cleanup even if some undo operations fail.
    /// </para>
    /// <para>
    /// Inspect this collection to determine which cleanup steps failed and may need manual intervention.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Exception> CompensationErrors => this.compensationErrors.AsReadOnly();

    /// <summary>
    /// Registers a compensation function to be executed if the saga fails.
    /// This is a backward-compatible overload that uses anonymous compensation.
    ///
    /// Compensations are executed in reverse order (LIFO - Last In First Out) during rollback.
    /// </summary>
    /// <param name="compensation">
    /// An async function that undoes a previous step.
    /// This function receives a <see cref="CancellationToken"/> and should be properly cancellable.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="compensation"/> is null.
    /// </exception>
    /// <remarks>
    /// This method is maintained for backward compatibility. Consider using the overload with stepName for better diagnostics.
    /// </remarks>
    public void RegisterCompensation(Func<CancellationToken, Task> compensation)
    {
        this.RegisterCompensation($"Step{this.compensations.Count + 1}", compensation, condition: null);
    }

    /// <summary>
    /// Registers a named compensation function to be executed if the saga fails.
    ///
    /// Compensations are executed in reverse order (LIFO - Last In First Out) during rollback,
    /// ensuring proper cleanup order. For example, if you book Flight then Hotel,
    /// the compensation will cancel Hotel then Flight.
    /// </summary>
    /// <param name="stepName">
    /// A meaningful name for this compensation step.
    /// Used in logging, tracing, and error messages for better diagnostics.
    /// </param>
    /// <param name="compensation">
    /// An async function that undoes a previous step.
    /// This function receives a <see cref="CancellationToken"/> and should be properly cancellable.
    /// </param>
    /// <param name="condition">
    /// Optional condition that determines if this compensation should execute.
    /// If null, the compensation always executes during rollback.
    /// If provided and returns false, the compensation is skipped.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="compensation"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="stepName"/> is null or whitespace.
    /// </exception>
    /// <remarks>
    /// <para>
    /// **Thread Safety:** This method is not thread-safe. Ensure compensations are registered
    /// sequentially from a single thread during saga execution.
    /// </para>
    /// <para>
    /// **Timing:** Compensations should be registered immediately after a successful step completes.
    /// </para>
    /// <para>
    /// **Order Matters:** Registration order determines execution order (LIFO during rollback).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Register named compensation
    /// saga.RegisterCompensation("FlightCancellation",
    ///     async ct => await flightService.CancelAsync(flightId, ct));
    ///
    /// // Register conditional compensation (only if payment was captured)
    /// saga.RegisterCompensation("PaymentReversal",
    ///     async ct => await paymentService.ReverseAsync(paymentId, ct),
    ///     async ct => await paymentService.IsCapturedAsync(paymentId, ct));
    /// </code>
    /// </example>
    public void RegisterCompensation(
        string stepName,
        Func<CancellationToken, Task> compensation,
        Func<CancellationToken, Task<bool>> condition = null)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        var descriptor = new SagaCompensationDescriptor
        {
            Order = this.compensations.Count,
            StepName = stepName,
            Action = compensation,
            Condition = condition,
            RegisteredAt = DateTime.UtcNow
        };

        this.compensations.Add(descriptor);
        this.maxCompensationCount = Math.Max(this.maxCompensationCount, this.compensations.Count);

        this.logger.LogDebug(
            "Saga {SagaId}: Registered compensation '{StepName}' (Order: {Order}, Conditional: {HasCondition})",
            this.Context.SagaId,
            stepName,
            descriptor.Order,
            condition is not null);

        // Raise event
        this.RaiseCompensationEvent(new SagaCompensationEvent
        {
            StepName = stepName,
            EventType = CompensationEventType.Registered,
            Context = this.Context
        }).GetAwaiter().GetResult(); // Fire and forget for registration
    }

    /// <summary>
    /// Commits the saga, marking all changes as successful and clearing compensations.
    ///
    /// Called automatically by <see cref="ResultOperationScope{T, TOperation}"/> when all saga steps
    /// complete successfully. This signals that no rollback is needed.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the commit operation.
    /// </param>
    /// <returns>A completed task representing the commit operation.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        this.IsCommitted = true;
        this.compensations.Clear();

        this.logger.LogInformation(
            "Saga {SagaId} committed successfully with {CompensationCount} registered compensations",
            this.Context.SagaId,
            this.maxCompensationCount);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rolls back the saga by executing all registered compensations in reverse order (LIFO).
    ///
    /// Called automatically by <see cref="ResultOperationScope{T, TOperation}"/> when any saga step fails
    /// or an exception occurs. This method ensures best-effort cleanup of all previous steps,
    /// even if individual compensation operations fail.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the rollback operation.
    /// </param>
    /// <returns>A task representing the complete rollback operation.</returns>
    /// <remarks>
    /// <para>
    /// **Execution Strategy (Best-Effort):**
    /// 1. Executes compensations in reverse registration order (LIFO)
    /// 2. Checks conditions before executing each compensation
    /// 3. If a compensation throws an exception, it is captured but does not stop other compensations
    /// 4. All compensation attempts are counted in <see cref="CompensationExecutedCount"/>
    /// 5. Failed compensations are recorded in <see cref="CompensationErrors"/>
    /// </para>
    /// </remarks>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        this.IsRolledBack = true;

        this.logger.LogWarning(
            "Saga {SagaId} rolling back {CompensationCount} compensation(s)",
            this.Context.SagaId,
            this.compensations.Count);

        // Execute compensations in reverse order (LIFO)
        for (var i = this.compensations.Count - 1; i >= 0; i--)
        {
            var descriptor = this.compensations[i];

            await this.ExecuteCompensationAsync(descriptor, cancellationToken);
        }

        this.logger.LogWarning(
            "Saga {SagaId} rollback completed: {ExecutedCount} executed, {FailedCount} failed, {SkippedCount} skipped",
            this.Context.SagaId,
            this.CompensationExecutedCount,
            this.compensationErrors.Count,
            this.compensations.Count(c => c.Status == SagaCompensationStatus.Skipped));
    }

    /// <summary>
    /// Executes a single compensation with condition checking, timing, and event raising.
    /// </summary>
    private async Task ExecuteCompensationAsync(
        SagaCompensationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check condition if present
            if (descriptor.Condition is not null)
            {
                this.logger.LogDebug(
                    "Saga {SagaId}: Evaluating condition for '{StepName}'",
                    this.Context.SagaId,
                    descriptor.StepName);

                var shouldExecute = await descriptor.Condition(cancellationToken);

                if (!shouldExecute)
                {
                    descriptor.Status = SagaCompensationStatus.Skipped;

                    this.logger.LogDebug(
                        "Saga {SagaId}: Compensation '{StepName}' skipped (condition not met)",
                        this.Context.SagaId,
                        descriptor.StepName);

                    await this.RaiseCompensationEvent(new SagaCompensationEvent
                    {
                        StepName = descriptor.StepName,
                        EventType = CompensationEventType.Skipped,
                        Context = this.Context
                    });

                    return; // Skip this compensation
                }
            }

            // Execute compensation
            descriptor.Status = SagaCompensationStatus.Executing;
            descriptor.ExecutedAt = DateTime.UtcNow;

            this.logger.LogInformation(
                "Saga {SagaId}: Executing compensation '{StepName}'",
                this.Context.SagaId,
                descriptor.StepName);

            await this.RaiseCompensationEvent(new SagaCompensationEvent
            {
                StepName = descriptor.StepName,
                EventType = CompensationEventType.Started,
                Context = this.Context
            });

            var startTime = DateTime.UtcNow;

            await descriptor.Action(cancellationToken);

            descriptor.ExecutionDuration = DateTime.UtcNow - startTime;
            descriptor.Status = SagaCompensationStatus.Succeeded;
            this.CompensationExecutedCount++;

            this.logger.LogInformation(
                "Saga {SagaId}: Compensation '{StepName}' succeeded ({Duration}ms)",
                this.Context.SagaId,
                descriptor.StepName,
                descriptor.ExecutionDuration.Value.TotalMilliseconds);

            await this.RaiseCompensationEvent(new SagaCompensationEvent
            {
                StepName = descriptor.StepName,
                EventType = CompensationEventType.Succeeded,
                Duration = descriptor.ExecutionDuration,
                Context = this.Context
            });
        }
        catch (Exception ex)
        {
            descriptor.ExecutionDuration = descriptor.ExecutedAt.HasValue
                ? DateTime.UtcNow - descriptor.ExecutedAt.Value
                : null;
            descriptor.Status = SagaCompensationStatus.Failed;
            descriptor.ExecutionError = ex;
            this.compensationErrors.Add(ex);
            this.CompensationExecutedCount++;

            this.logger.LogError(
                ex,
                "Saga {SagaId}: Compensation '{StepName}' failed ({Duration}ms)",
                this.Context.SagaId,
                descriptor.StepName,
                descriptor.ExecutionDuration?.TotalMilliseconds ?? 0);

            await this.RaiseCompensationEvent(new SagaCompensationEvent
            {
                StepName = descriptor.StepName,
                EventType = CompensationEventType.Failed,
                Duration = descriptor.ExecutionDuration,
                Error = ex,
                Context = this.Context
            });
        }
    }

    /// <summary>
    /// Raises a compensation event to all subscribers.
    /// </summary>
    private async Task RaiseCompensationEvent(SagaCompensationEvent evt)
    {
        if (this.OnCompensationEvent is not null)
        {
            try
            {
                await this.OnCompensationEvent(evt);
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Saga {SagaId}: Error in compensation event handler for '{StepName}' ({EventType})",
                    this.Context.SagaId,
                    evt.StepName,
                    evt.EventType);
            }
        }
    }
}
