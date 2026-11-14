// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Observes a bounded provider-attempt timeout without releasing or replaying the still-occupied native work.</summary>
/// <example><code>await observer.ReportInfrastructureTimeoutAsync(epoch);</code></example>
public interface ICacheProviderInfrastructureObserver
{
    /// <summary>Gets the process-local circuit epoch used to ignore late results from a superseded open/probe cycle.</summary>
    long CircuitEpoch { get; }

    /// <summary>Records one infrastructure timeout while actual native cleanup remains with its existing owner.</summary>
    /// <param name="epoch">The epoch captured before the original provider attempt began.</param>
    /// <returns>Completion of the bounded circuit-state update.</returns>
    ValueTask ReportInfrastructureTimeoutAsync(long epoch);

    /// <summary>Observes an actual native session/stream outcome which occurs after the complete provider SPI call returned.</summary>
    /// <param name="epoch">The circuit epoch captured immediately before the actual native operation.</param>
    /// <param name="infrastructureFailure">True for a classified infrastructure failure; false for a completed non-infrastructure outcome.</param>
    /// <returns>The bounded circuit-state update, without replaying the operation or releasing its resource ownership.</returns>
    ValueTask ReportInfrastructureOutcomeAsync(long epoch, bool infrastructureFailure);
}