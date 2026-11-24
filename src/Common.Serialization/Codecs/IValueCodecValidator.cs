// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Validates an encoded contract in one bounded pass without materializing an arbitrary object graph.</summary>
/// <typeparam name="T">The contract explicitly validated by this implementation.</typeparam>
/// <remarks>Implementations must consume and validate the complete input, including trailing content.
/// This capability is registered explicitly; an ordinary decoder does not imply streaming validation.</remarks>
/// <example><code>var result = await validator.ValidateAsync(source, cancellationToken);</code></example>
public interface IValueCodecValidator<T>
{
    /// <summary>Gets the exact validated format.</summary>
    ValueCodecFormat Format { get; }

    /// <summary>Gets declared stream access and scratch requirements.</summary>
    ValueCodecCapabilities Capabilities { get; }

    /// <summary>Validates the whole input, leaving the caller-owned stream open.</summary>
    ValueTask<Result> ValidateAsync(Stream source, CancellationToken cancellationToken = default);
}