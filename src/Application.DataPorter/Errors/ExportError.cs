// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Error that occurred during an export operation.
/// </summary>
public sealed class ExportError : DataPorterError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportError"/> class.
    /// </summary>
    public ExportError()
        : base("An error occurred during export.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ExportError(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ExportError(string message, Exception innerException)
        : base(message)
    {
        this.InnerException = innerException;
    }

    /// <summary>
    /// Gets the inner exception, if any.
    /// </summary>
    public Exception InnerException { get; }
}
