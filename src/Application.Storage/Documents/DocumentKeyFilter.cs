// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines how a <see cref="DocumentKey" /> should be interpreted when filtering document queries.
/// </summary>
public enum DocumentKeyFilter
{
    /// <summary>
    /// Matches the exact <see cref="DocumentKey.PartitionKey" /> and <see cref="DocumentKey.RowKey" />.
    /// </summary>
    FullMatch,

    /// <summary>
    /// Matches the exact <see cref="DocumentKey.PartitionKey" /> and documents whose row key ends with the supplied row key.
    /// </summary>
    RowKeySuffixMatch,

    /// <summary>
    /// Matches the exact <see cref="DocumentKey.PartitionKey" /> and documents whose row key starts with the supplied row key.
    /// </summary>
    RowKeyPrefixMatch
}
