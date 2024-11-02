// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error result that encapsulates an exception.
/// </summary>
public class ExceptionError : IResultError
{
    private readonly Exception exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionError"/> class.
    /// </summary>
    /// <param name="exception">The exception to encapsulate.</param>
    public ExceptionError(Exception exception)
    {
        this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
        this.Message = exception.Message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionError"/> class.
    /// </summary>
    /// <param name="exception">The exception to encapsulate.</param>
    /// <param name="message"></param>
    public ExceptionError(Exception exception, string message)
    {
        this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
        this.Message = message ?? exception.Message;
    }

    /// <summary>
    /// Gets the error message associated with the encapsulated exception.
    /// </summary>
    public string Message { get; init; }

    public void Throw()
    {
        throw this.exception;
    }

    /// <summary>
    /// Gets the type of the encapsulated exception.
    /// </summary>
    public string ExceptionType => this.exception.GetType().FullName;

    /// <summary>
    /// Gets the stack trace of the encapsulated exception.
    /// </summary>
    public string StackTrace => this.exception.StackTrace;

    /// <summary>
    /// Gets the original exception that was encapsulated.
    /// </summary>
    public Exception OriginalException => this.exception;
}