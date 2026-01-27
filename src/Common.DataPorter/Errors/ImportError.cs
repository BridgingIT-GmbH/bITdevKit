// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Error that occurred during an import operation.
/// </summary>
public sealed class ImportError : DataPorterError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportError"/> class.
    /// </summary>
    public ImportError()
        : base("An error occurred during import.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ImportError(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ImportError(string message, Exception innerException)
        : base(message)
    {
        this.InnerException = innerException;
    }

    /// <summary>
    /// Gets the inner exception, if any.
    /// </summary>
    public Exception InnerException { get; }
}
