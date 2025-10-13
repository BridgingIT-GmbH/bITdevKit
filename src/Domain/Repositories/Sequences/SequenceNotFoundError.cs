// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Error indicating that a sequence was not found in the database.
/// </summary>
public class SequenceNotFoundError(string sequenceName, string schema) : ResultErrorBase($"Sequence '{sequenceName}' not found in schema '{schema}'")
{
    /// <summary>
    /// Gets the name of the sequence that was not found.
    /// </summary>
    public string SequenceName { get; } = sequenceName;

    /// <summary>
    /// Gets the schema where the sequence was expected.
    /// </summary>
    public string Schema { get; } = schema;
}
