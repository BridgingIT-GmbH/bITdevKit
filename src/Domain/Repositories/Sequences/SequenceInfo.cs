// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Contains metadata information about a database sequence.
/// </summary>
public class SequenceInfo
{
    /// <summary>
    /// Gets or sets the name of the sequence.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the schema containing the sequence.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets the current value of the sequence.
    /// </summary>
    public long CurrentValue { get; set; }

    /// <summary>
    /// Gets or sets the minimum value the sequence can generate.
    /// </summary>
    public long MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value the sequence can generate.
    /// </summary>
    public long MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the increment step for the sequence.
    /// </summary>
    public int Increment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sequence cycles back to minimum after reaching maximum.
    /// </summary>
    public bool IsCyclic { get; set; }
}
