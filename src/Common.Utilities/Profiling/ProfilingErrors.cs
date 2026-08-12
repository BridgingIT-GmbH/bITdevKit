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