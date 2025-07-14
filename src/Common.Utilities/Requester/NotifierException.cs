// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that occurs during notification processing.
/// </summary>
/// <remarks>
/// This exception is thrown when an error occurs in the Notifier system, such as when no handlers are found for a notification.
/// It supports both a message-only constructor and a constructor with an inner exception for detailed error reporting.
/// </remarks>
/// <example>
/// <code>
/// throw new NotifierException("No handlers found for notification type EmailSentNotification");
/// throw new NotifierException("Notification processing failed", innerException);
/// </code>
/// </example>
public class NotifierException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotifierException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NotifierException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifierException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public NotifierException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
