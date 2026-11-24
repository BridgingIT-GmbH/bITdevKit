// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies bounded cache execution facts without key, tenant or operation-identifier labels.</summary>
/// <example><code>var category = CacheObservationKind.Request;</code></example>
public enum CacheObservationKind
{
    /// <summary>A public cache method was invoked.</summary>
    Request,

    /// <summary>A valid positive entry was acquired.</summary>
    PositiveHit,

    /// <summary>A valid explicit not-found marker was acquired.</summary>
    NegativeHit,

    /// <summary>No compatible valid entry was acquired.</summary>
    Miss,

    /// <summary>A public logical entry read succeeded.</summary>
    ReadSuccess,

    /// <summary>One entry publication was confirmed.</summary>
    WriteSuccess,

    /// <summary>The count of entries logically invalidated.</summary>
    InvalidatedEntries,

    /// <summary>One actual source callback attempt began.</summary>
    SourceAttempt,

    /// <summary>One actual source callback attempt succeeded.</summary>
    SourceSuccess,

    /// <summary>One actual source callback attempt failed.</summary>
    SourceFailure,

    /// <summary>A source dependency policy rejected work before invocation.</summary>
    SourceShortCircuit,

    /// <summary>A caller joined existing owned work.</summary>
    Join,

    /// <summary>A caller finished observing shared work.</summary>
    WaitCompleted,

    /// <summary>A caller observation deadline elapsed.</summary>
    WaitTimeout,

    /// <summary>An owned source deadline elapsed.</summary>
    SourceTimeout,

    /// <summary>A caller or owned work unit observed cancellation.</summary>
    Cancelled,

    /// <summary>One bounded provider call was admitted.</summary>
    ProviderCall,

    /// <summary>One authoritative version validation was requested.</summary>
    ProviderValidation,

    /// <summary>One provider payload snapshot was acquired.</summary>
    ProviderPayloadRead,

    /// <summary>One infrastructure-classified provider failure occurred.</summary>
    ProviderFailure,

    /// <summary>A finite work or memory admission was rejected.</summary>
    AdmissionRejected,

    /// <summary>A retained hot candidate was found before validation.</summary>
    HotCandidate,

    /// <summary>A provider-validated local binary representation was reused.</summary>
    HotBinaryReuse,

    /// <summary>A provider-validated local decoded representation was reused.</summary>
    HotDecodedReuse,

    /// <summary>A hot candidate failed authoritative provider validation.</summary>
    HotValidationFailure,

    /// <summary>A hot candidate was superseded by another publication.</summary>
    HotVersionChanged,

    /// <summary>A complete verified representation entered hot retention.</summary>
    HotPromotion,

    /// <summary>A hot retention admission was rejected.</summary>
    HotAdmissionRejected,

    /// <summary>A local hot representation was retired by eviction.</summary>
    HotEviction,

    /// <summary>One admitted automatic refresh unit began.</summary>
    RefreshAttempt,

    /// <summary>One automatic refresh unit succeeded.</summary>
    RefreshSuccess,

    /// <summary>One automatic refresh unit failed.</summary>
    RefreshFailure,

    /// <summary>One admitted warm-up unit began.</summary>
    WarmupAttempt,

    /// <summary>One warm-up unit succeeded.</summary>
    WarmupSuccess,

    /// <summary>One warm-up unit failed.</summary>
    WarmupFailure,

    /// <summary>A protected payload transfer handle was acquired.</summary>
    TransferAcquired,

    /// <summary>A protected transfer reached verified terminal completion.</summary>
    TransferCompleted,

    /// <summary>A protected transfer ended in an explicit failure.</summary>
    TransferFailed,

    /// <summary>A protected transfer was disposed before verified completion.</summary>
    TransferAbandoned,

    /// <summary>An HTTP destination accepted the complete response and final flush.</summary>
    HttpDestinationCompleted,

    /// <summary>An HTTP destination failed independently of the source handle integrity outcome.</summary>
    HttpDestinationFailed,

    /// <summary>Optional compression used its declared uncompressed fallback.</summary>
    CompressionFallback,

    /// <summary>An acquired payload could not be decoded.</summary>
    DecodeFailure,

    /// <summary>A payload or metadata corruption outcome was reported.</summary>
    Corruption,

    /// <summary>A persisted representation or catalog was incompatible.</summary>
    CompatibilityFailure,

    /// <summary>A named provider circuit opened.</summary>
    BreakerOpened,

    /// <summary>A nonmutating provider probe closed an open circuit.</summary>
    BreakerRecovered,

    /// <summary>An open or occupied half-open provider circuit rejected work.</summary>
    BreakerRejected,

    /// <summary>One nonmutating provider readiness probe began.</summary>
    BreakerProbe,

    /// <summary>One bounded logical invalidation request began.</summary>
    InvalidationRequested,

    /// <summary>One bounded invalidation unit committed.</summary>
    InvalidationCompleted,

    /// <summary>One bounded invalidation unit failed.</summary>
    InvalidationFailed
}