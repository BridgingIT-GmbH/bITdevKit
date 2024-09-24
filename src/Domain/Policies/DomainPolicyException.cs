// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using Common;

/// <summary>
///     The <c>DomainPolicyException</c> class represents exceptions that occur
///     when a domain policy check fails within the application domain.
/// </summary>
/// <remarks>
///     This exception is typically thrown when domain-specific rules or policies are violated.
///     It provides facilities to include detailed messages and a result object that can
///     contain additional errors and messages.
/// </remarks>
public class DomainPolicyException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainPolicyException" /> class.
    ///     Represents exceptions related to domain policy violations.
    /// </summary>
    public DomainPolicyException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainPolicyException" /> class.
    ///     Represents an exception that is thrown when a domain policy is violated in the application.
    /// </summary>
    public DomainPolicyException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainPolicyException" /> class.
    ///     Represents an exception that is thrown when a domain policy is violated.
    /// </summary>
    public DomainPolicyException(string message, Result result)
        : base(message)
    {
        this.Result = result;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainPolicyException" /> class.
    ///     Represents an exception that is thrown when a domain policy violation occurs.
    /// </summary>
    public DomainPolicyException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    ///     Gets the result associated with the DomainPolicyException, which represents the outcome of an operation, including
    ///     any errors or messages.
    /// </summary>
    public Result Result { get; }

    /// <summary>
    ///     Returns a string that represents the current object, including its message,
    ///     errors, and messages if available.
    /// </summary>
    /// <returns>A string representation of the current object.</returns>
    public override string ToString()
    {
        var result = this.Message;

        if (this.Result.Errors.SafeAny())
        {
            if (!result.IsNullOrEmpty())
            {
                result += Environment.NewLine;
            }

            result += "Errors: ";
            result += this.Result.Errors.ToString(", ");
        }

        if (this.Result.Messages.SafeAny())
        {
            if (!result.IsNullOrEmpty())
            {
                result += Environment.NewLine;
            }

            result += "Messages: ";
            result += this.Result.Messages.ToString(", ");
        }

        return result;
    }
}