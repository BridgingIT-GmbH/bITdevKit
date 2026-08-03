// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Provides provider-neutral discovery of containers available to a blob-store client.</summary>
/// <remarks>This optional capability is used by operational tooling and does not change the core blob client contract.</remarks>
/// <example><code>var containers = await catalog.ListContainersAsync(cancellationToken);</code></example>
public interface IBlobStoreContainerCatalog
{
    /// <summary>Lists all currently available container names in ordinal order.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing distinct container names.</returns>
    /// <example><code>var result = await catalog.ListContainersAsync(cancellationToken);</code></example>
    Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default);
}
