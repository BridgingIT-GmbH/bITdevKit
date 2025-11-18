// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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
/// // Create saga and value
/// var saga = new TestSagaScope();
/// var booking = new TripBooking();
///
/// // Chain operations with compensations
/// var result = await Result&lt;TripBooking&gt;.Success(booking)
///     .StartOperation&lt;TripBooking, TestSagaScope&gt;(saga)
///          .BindAsync(async (b, ct) =>
///          {
///              var flight = await flightService.BookAsync(b.FlightDetails, ct);
///              // Register compensation to undo this booking
///              saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
///              b.FlightConfirmation = flight.ConfirmationNumber;
///              return Result&lt;TripBooking&gt;.Success(b);
///          }, CancellationToken.None)
///          .BindAsync(async (b, ct) =>
///          {
///              var hotel = await hotelService.BookAsync(b.HotelDetails, ct);
///              // Register compensation to undo this booking
///              saga.RegisterCompensation(async ct => await hotelService.CancelAsync(hotel.Id, ct));
///              b.HotelConfirmation = hotel.ConfirmationNumber;
///              return Result&lt;TripBooking&gt;.Success(b);
///          }, CancellationToken.None)
///          // If any step fails, compensations execute in reverse order
///     .EndOperationAsync(CancellationToken.None);
///
/// // Check results
/// if (result.IsSuccess)
/// {
///     Console.WriteLine("All steps succeeded, no compensations executed");
/// }
/// else
/// {
///     Console.WriteLine($"Saga failed and rolled back. Errors: {saga.CompensationErrors.Count}");
/// }
/// </code>
/// </example>
public class SagaOperationScope : IOperationScope
{
    /// <summary>
    /// Internal list of registered compensation functions, each representing an undo operation for a completed step.
    /// </summary>
    private readonly List<Func<CancellationToken, Task>> compensations = [];

    /// <summary>
    /// Internal list of exceptions that occurred during compensation execution.
    /// Used to track errors that occur when undoing previous steps.
    /// </summary>
    private readonly List<Exception> compensationErrors = [];

    /// <summary>
    /// Tracks the maximum number of compensations registered at any point.
    /// Used to distinguish between compensations cleared on success vs. those that were never registered.
    /// </summary>
    private int maxCompensationCount;

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
    /// Use <see cref="CompensationErrors"/> to determine which failed compensations.
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
    ///
    /// Compensations are executed in reverse order (LIFO - Last In First Out) during rollback,
    /// ensuring proper cleanup order. For example, if you book Flight then Hotel,
    /// the compensation will cancel Hotel then Flight.
    /// </summary>
    /// <param name="compensation">
    /// An async function that undoes a previous step.
    /// This function receives a <see cref="CancellationToken"/> and should be properly cancellable.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="compensation"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// **Thread Safety:** This method is not thread-safe. Ensure compensations are registered
    /// sequentially from a single thread during saga execution.
    /// </para>
    /// <para>
    /// **Timing:** Compensations should be registered immediately after a successful step completes.
    /// Registering too late (e.g., after multiple steps) could leave the system inconsistent if an
    /// earlier step's compensation fails.
    /// </para>
    /// <para>
    /// **Order Matters:** Registration order determines execution order. Register compensations
    /// for "outer" operations first, inner operations last, so rollback proceeds from innermost to outermost.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Correct order: register compensation immediately after each step
    /// var flight = await flightService.BookAsync(details, ct);
    /// saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
    ///
    /// var hotel = await hotelService.BookAsync(details, ct);
    /// saga.RegisterCompensation(async ct => await hotelService.CancelAsync(hotel.Id, ct));
    ///
    /// // If hotel booking fails, compensations will execute in reverse:
    /// // 1. Cancel hotel (fails - already failed)
    /// // 2. Cancel flight (succeeds - this is the compensation)
    /// </code>
    /// </example>
    public void RegisterCompensation(Func<CancellationToken, Task> compensation)
    {
        ArgumentNullException.ThrowIfNull(compensation);

        this.compensations.Add(compensation);
        this.maxCompensationCount = Math.Max(this.maxCompensationCount, this.compensations.Count);
    }

    /// <summary>
    /// Commits the saga, marking all changes as successful and clearing compensations.
    ///
    /// Called automatically by <see cref="ResultOperationScope{T, TOperation}"/> when all saga steps
    /// complete successfully. This signals that no rollback is needed.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the commit operation.
    /// In typical saga implementations, commit is non-cancellable, but this parameter is provided
    /// for interface compliance and potential extension scenarios.
    /// </param>
    /// <returns>A completed task representing the commit operation.</returns>
    /// <remarks>
    /// <para>
    /// **Side Effects:** This method clears all registered compensations from memory, as they are no longer needed.
    /// However, <see cref="CompensationCount"/> preserves the count for audit purposes.
    /// </para>
    /// <para>
    /// **Idempotency:** This method can be called multiple times safely. Subsequent calls are no-ops.
    /// </para>
    /// </remarks>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        this.IsCommitted = true;
        // Keep the max count for assertions, but clear the list
        this.compensations.Clear();

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
    /// Cancellations during rollback are honored, but best-effort continues.
    /// </param>
    /// <returns>A task representing the complete rollback operation.</returns>
    /// <remarks>
    /// <para>
    /// **Execution Strategy (Best-Effort):**
    /// 1. Executes compensations in reverse registration order (LIFO)
    /// 2. If a compensation throws an exception, it is captured but does not stop other compensations
    /// 3. All compensation attempts are counted in <see cref="CompensationExecutedCount"/>
    /// 4. Failed compensations are recorded in <see cref="CompensationErrors"/>
    /// </para>
    ///
    /// <para>
    /// **Why Best-Effort?** In distributed systems, you cannot guarantee every cleanup will succeed.
    /// For example, a hotel booking cancellation might fail if the hotel service is temporarily down.
    /// Rather than abandon the entire rollback, the saga continues canceling other bookings,
    /// leaving the system in the best possible state. Manual intervention may be needed for persistent failures.
    /// </para>
    ///
    /// <para>
    /// **Cancellation Handling:** If <paramref name="cancellationToken"/> is cancelled during rollback,
    /// the operation is interrupted, but already-completed compensations remain undone.
    /// Pending compensations might not execute, leaving the system inconsistent.
    /// </para>
    ///
    /// <para>
    /// **Order Guarantee:** Compensations execute strictly in LIFO order:
    /// If compensations are registered as [A, B, C], they execute as [C, B, A].
    /// This ensures proper cleanup order for nested operations.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Scenario: Book Flight (F), Hotel (H), Car (C)
    /// // Compensations registered: [F, H, C]
    /// // If Car booking fails, rollback executes: [C, H, F]
    ///
    /// saga.RegisterCompensation(async ct => await FlightService.CancelAsync(flightId, ct)); // #1
    /// saga.RegisterCompensation(async ct => await HotelService.CancelAsync(hotelId, ct));   // #2
    /// saga.RegisterCompensation(async ct => await CarService.CancelAsync(carId, ct));       // #3
    ///
    /// // On failure, rollback executes in order: #3, #2, #1
    /// await saga.RollbackAsync(cancellationToken);
    /// </code>
    /// </example>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        this.IsRolledBack = true;

        // Execute compensations in reverse order (LIFO)
        for (var i = this.compensations.Count - 1; i >= 0; i--)
        {
            try
            {
                await this.compensations[i](cancellationToken);
                this.CompensationExecutedCount++;
            }
            catch (Exception ex)
            {
                // Log compensation errors but continue with other compensations
                this.compensationErrors.Add(ex);
                this.CompensationExecutedCount++;
            }
        }
    }
}
