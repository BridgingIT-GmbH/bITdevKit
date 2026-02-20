// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Exception thrown when a conflict occurs, such as a resource already existing
///     or a state conflict that prevents an operation from proceeding.
/// </summary>
/// <remarks>
///     This exception typically results in a 409 Conflict HTTP response.
/// </remarks>
public class ConflictException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictException" /> class.
    /// </summary>
    public ConflictException()
        : base("A conflict occurred.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictException" /> class with
    ///     a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictException" /> class with
    ///     a specified error message and a reference to the inner exception that is the
    ///     cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    ///     The exception that is the cause of the current exception, or a null reference
    ///     if no inner exception is specified.
    /// </param>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainException" /> class with
    ///     a specified error message and error code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="code">A custom error code associated with the domain error.</param>
    public ConflictException(string message, string code)
        : base(message)
    {
        this.Code = code;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainException" /> class with
    ///     a specified error message, error code, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="code">A custom error code associated with the domain error.</param>
    /// <param name="innerException">
    ///     The exception that is the cause of the current exception, or a null reference
    ///     if no inner exception is specified.
    /// </param>
    public ConflictException(string message, string code, Exception innerException)
        : base(message, innerException)
    {
        this.Code = code;
    }

    /// <summary>
    ///     Gets or sets a custom error code associated with the domain exception.
    /// </summary>
    public string Code { get; set; }
}