// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines the stable error codes reported by development instance isolation.
/// </summary>
/// <example>
/// <code>
/// if (exception.Code == DevAppInstanceErrorCodes.PortInUse)
/// {
///     Console.Error.WriteLine(exception.RecoveryAction);
/// }
/// </code>
/// </example>
public static class DevAppInstanceErrorCodes
{
    /// <summary>Linked worktree metadata cannot be resolved reliably.</summary>
    public const string WorktreeDetectionFailed = "worktree_detection_failed";

    /// <summary>All managed slots are claimed.</summary>
    public const string SlotsExhausted = "slots_exhausted";

    /// <summary>An assignment or registry document is corrupt, copied, or has an unsupported version.</summary>
    public const string AssignmentInvalid = "assignment_invalid";

    /// <summary>The requested slot belongs to another worktree.</summary>
    public const string SlotInUse = "slot_in_use";

    /// <summary>The registry lock could not be acquired in time.</summary>
    public const string RegistryLocked = "registry_locked";

    /// <summary>The application lease is held by another process.</summary>
    public const string InstanceRunning = "instance_running";

    /// <summary>An assigned port is unavailable.</summary>
    public const string PortInUse = "port_in_use";

    /// <summary>Managed settings disagree with caller-supplied settings or recorded declarations.</summary>
    public const string ConfigurationConflict = "configuration_conflict";

    /// <summary>A linked worktree is hosted behind IIS or IIS Express.</summary>
    public const string UnsupportedHost = "unsupported_host";

    /// <summary>A filesystem ownership marker or inventory record does not match the requested target.</summary>
    public const string ResourceNotOwned = "resource_not_owned";

    /// <summary>A reset or prune operation could not remove every target.</summary>
    public const string ResetIncomplete = "reset_incomplete";

    /// <summary>An unexpected error, for example a file system failure, stopped the development instance startup.</summary>
    public const string UnexpectedFailure = "unexpected_failure";
}

/// <summary>
/// Represents a development instance failure with a stable error code and recovery action.
/// </summary>
/// <example>
/// <code>
/// throw new DevAppInstanceException(
///     DevAppInstanceErrorCodes.SlotsExhausted,
///     "All 49 development slots are assigned.",
///     "Run 'bdk dev prune' to free slots of deleted worktrees.");
/// </code>
/// </example>
public sealed class DevAppInstanceException : Exception
{
    private const string LogKey = "BDK";
    private const string OptOutHint = "To start without worktree isolation, set DevAppInstance__Mode=Disabled.";

    /// <summary>
    /// Initializes a new instance of the <see cref="DevAppInstanceException"/> class.
    /// </summary>
    /// <param name="code">The stable error code from <see cref="DevAppInstanceErrorCodes"/>.</param>
    /// <param name="message">The sanitized error message.</param>
    /// <param name="recoveryAction">The recommended recovery action.</param>
    /// <param name="innerException">The optional inner exception.</param>
    public DevAppInstanceException(string code, string message, string recoveryAction = null, Exception innerException = null)
        : base(message, innerException)
    {
        this.Code = code;
        this.RecoveryAction = recoveryAction;
    }

    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the recommended recovery action.
    /// </summary>
    public string RecoveryAction { get; }

    /// <summary>
    /// Gets or sets the application ID when known.
    /// </summary>
    public string AppId { get; init; }

    /// <summary>
    /// Gets or sets the slot when known.
    /// </summary>
    public int? Slot { get; init; }

    /// <summary>
    /// Formats the exception as one startup error line.
    /// </summary>
    /// <returns>The formatted line using the <c>[BDK]</c> log key.</returns>
    public string ToLogLine()
    {
        var app = this.AppId is null ? string.Empty : $", app={this.AppId}";
        var slot = this.Slot is null ? string.Empty : $", slot={this.Slot}";
        var recovery = string.IsNullOrWhiteSpace(this.RecoveryAction) ? string.Empty : $" Recovery: {this.RecoveryAction}";

        return $"[{LogKey}] worktree failure: {this.Message} (code={this.Code}{app}{slot}).{recovery}";
    }

    /// <summary>
    /// Formats the exception as one host startup error line that also tells how to start without worktree isolation.
    /// </summary>
    /// <returns>The formatted line using the <c>[BDK]</c> log key.</returns>
    /// <example>
    /// <code>
    /// Console.Error.WriteLine(exception.ToStartupLogLine());
    /// </code>
    /// </example>
    public string ToStartupLogLine()
    {
        var line = this.ToLogLine();

        return this.HasOptOutHint() ? line : $"{line} {OptOutHint}";
    }

    /// <summary>
    /// Gets the recovery action for a host startup error, followed by how to start without worktree isolation.
    /// Use it with a structured log template that passes the log key as an argument.
    /// </summary>
    /// <returns>The recovery action including the opt-out hint, or only the hint when no recovery action is known.</returns>
    /// <example>
    /// <code>
    /// logger.LogError(
    ///     "[{LogKey}] worktree failure: {ErrorMessage} (code={ErrorCode}, app={AppId}, slot={Slot}) {RecoveryAction}",
    ///     "BDK", exception.Message, exception.Code, exception.AppId, exception.Slot, exception.GetStartupRecoveryAction());
    /// </code>
    /// </example>
    public string GetStartupRecoveryAction()
    {
        if (string.IsNullOrWhiteSpace(this.RecoveryAction))
        {
            return OptOutHint;
        }

        return this.HasOptOutHint() ? this.RecoveryAction : $"{this.RecoveryAction} {OptOutHint}";
    }

    /// <summary>
    /// Formats the exception as one startup line for a host that falls back to its configured ports and databases.
    /// </summary>
    /// <returns>The formatted line using the <c>[BDK]</c> log key.</returns>
    /// <example>
    /// <code>
    /// Console.Error.WriteLine(exception.ToFallbackLogLine());
    /// </code>
    /// </example>
    public string ToFallbackLogLine()
    {
        var app = this.AppId is null ? string.Empty : $", app={this.AppId}";
        var slot = this.Slot is null ? string.Empty : $", slot={this.Slot}";
        var recovery = string.IsNullOrWhiteSpace(this.RecoveryAction) ? string.Empty : $" Recovery: {this.RecoveryAction}";

        return $"[{LogKey}] worktree isolation failed, the host uses its configured ports and databases: {this.Message} (code={this.Code}{app}{slot}).{recovery}";
    }

    private bool HasOptOutHint() =>
        this.RecoveryAction?.Contains("Mode=Disabled", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Wraps an unexpected startup error, so the host reports it with the <c>[BDK]</c> log key and a recovery action.
    /// </summary>
    /// <param name="exception">The unexpected exception.</param>
    /// <param name="appId">The application ID when known.</param>
    /// <param name="slot">The slot when known.</param>
    /// <returns>The exception itself when it is a <see cref="DevAppInstanceException"/>, otherwise a wrapper with <see cref="DevAppInstanceErrorCodes.UnexpectedFailure"/>.</returns>
    /// <example>
    /// <code>
    /// catch (IOException ex)
    /// {
    ///     throw DevAppInstanceException.FromUnexpected(ex, "taskharbor");
    /// }
    /// </code>
    /// </example>
    public static DevAppInstanceException FromUnexpected(Exception exception, string appId = null, int? slot = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception as DevAppInstanceException ?? new DevAppInstanceException(
            DevAppInstanceErrorCodes.UnexpectedFailure,
            $"Worktree isolation failed unexpectedly: {exception.GetType().Name}: {exception.Message}",
            "Check access to the worktree '.dev' directory and the 'bdk-dev' registry in the Git common directory, then start again. 'bdk dev status' shows the recorded state.",
            exception)
        { AppId = appId, Slot = slot };
    }
}
