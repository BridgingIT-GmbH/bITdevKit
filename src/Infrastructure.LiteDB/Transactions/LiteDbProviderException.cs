// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

/// <summary>Carries recoverable persistence failures to the ActiveEntity result boundary.</summary>
/// <param name="errors">The errors preserved in the failed result.</param>
public sealed class LiteDbProviderException(IEnumerable<IResultError> errors) : Exception("The LiteDB operation failed.")
{
    /// <summary>Gets the errors to preserve at the result boundary.</summary>
    public IReadOnlyList<IResultError> Errors { get; } = errors.ToArray();

    /// <summary>Gets messages supplied by a failed callback.</summary>
    public IReadOnlyList<string> Messages { get; } = [];

    /// <summary>Creates a failure containing one recoverable error.</summary>
    /// <param name="error">The persistence or validation error.</param>
    public LiteDbProviderException(IResultError error) : this([error]) { }

    /// <summary>Preserves both messages and errors from a failed operation.</summary>
    /// <param name="result">The failed callback result.</param>
    public LiteDbProviderException(IResult result) : this(result.Errors)
    {
        this.Messages = result.Messages.ToArray();
    }
}
