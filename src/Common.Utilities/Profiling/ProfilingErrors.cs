// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Reports that profiling collection is disabled.</summary>
/// <example><code>result.Errors.ShouldContain(error => error is ProfilingDisabledError);</code></example>
public sealed class ProfilingDisabledError() : ResultErrorBase("Profiling collection is disabled.");

/// <summary>Reports that required profiling infrastructure is unavailable.</summary>
/// <param name="message">A safe description of the unavailable capability.</param>
/// <example><code>var error = new ProfilingUnavailableError("No profiling store is registered.");</code></example>
public sealed class ProfilingUnavailableError(string message) : ResultErrorBase(message);

/// <summary>Reports an invalid readable profiling key.</summary>
/// <param name="kind">The public identifier kind.</param>
/// <example><code>var error = new ProfilingInvalidKeyError("session");</code></example>
public sealed class ProfilingInvalidKeyError(string kind)
    : ResultErrorBase($"The {kind} key is invalid.");

/// <summary>Reports that an operation is invalid for the current session state.</summary>
/// <param name="message">A safe state-transition description.</param>
/// <example><code>var error = new ProfilingInvalidStateError("No session is active.");</code></example>
public sealed class ProfilingInvalidStateError(string message) : ResultErrorBase(message);

/// <summary>Reports that a shared store is required for the selected targets.</summary>
/// <example><code>var error = new ProfilingSharedStoreRequiredError();</code></example>
public sealed class ProfilingSharedStoreRequiredError()
    : ResultErrorBase("A shared profiling store is required when more than one node is targeted.");

/// <summary>Reports a safe profiling request validation failure.</summary>
/// <param name="message">The validation failure.</param>
/// <example><code>var error = new ProfilingValidationError("A duration is required.");</code></example>
public sealed class ProfilingValidationError(string message) : ResultErrorBase(message);

/// <summary>Reports an invalid, unsupported, or inconsistent portable Profiling archive.</summary>
/// <param name="message">A safe archive validation description.</param>
/// <example><code>var error = new ProfilingArchiveError("The archive version is unsupported.");</code></example>
public sealed class ProfilingArchiveError(string message) : ResultErrorBase(message);

/// <summary>Reports a failure while producing a Profiling visualization trace.</summary>
/// <param name="message">A safe trace-export failure description.</param>
/// <example><code>var error = new ProfilingTraceExportError("A writable destination is required.");</code></example>
public sealed class ProfilingTraceExportError(string message) : ResultErrorBase(message);

/// <summary>Reports bounded query capacity or concurrent maintenance contention.</summary>
/// <example><code>var error = new ProfilingBusyError("A clear is already applying.");</code></example>
public sealed class ProfilingBusyError(string message) : ResultErrorBase(message);

/// <summary>Reports a cursor invalidated by deletion, reset, expiry or changed filters.</summary>
/// <example><code>var error = new ProfilingQueryBoundaryError();</code></example>
public sealed class ProfilingQueryBoundaryError() : ResultErrorBase("The profiling query boundary expired or no longer matches this selection. Refresh the view.");

/// <summary>Requires a narrower selection instead of silently truncating exact analysis.</summary>
/// <example><code>var error = new ProfilingQueryLimitError();</code></example>
public sealed class ProfilingQueryLimitError() : ResultErrorBase("The profiling selection exceeds its exact analysis bound. Narrow the filters.");

/// <summary>Classifies a provider attempt without exposing backend payloads or guessing commit success.</summary>
/// <example><code>if (error is ProfilingPersistenceError { MayHaveCommitted: true }) ReportUnknown();</code></example>
public sealed class ProfilingPersistenceError(bool mayHaveCommitted, bool transient) : ResultErrorBase("Profiling persistence is unavailable; commit certainty and retry eligibility are reported separately.")
{
    /// <summary>Gets whether an acknowledgement may have been lost after commit began.</summary>
    /// <example><code>var uncertain = error.MayHaveCommitted;</code></example>
    public bool MayHaveCommitted { get; } = mayHaveCommitted;

    /// <summary>Gets whether a bounded background retry is eligible.</summary>
    /// <example><code>var retry = error.Transient;</code></example>
    public bool Transient { get; } = transient;
}

/// <summary>Reports an exhausted view-build deadline without presenting partial data as a successful view.</summary>
/// <example><code>if (result.Errors.Any(error => error is ProfilingQueryTimeoutError)) ShowStale();</code></example>
public sealed class ProfilingQueryTimeoutError() : ResultErrorBase("The profiling view exceeded its query deadline. Narrow the selection or refresh later.");
