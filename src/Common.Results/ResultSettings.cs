// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the settings used for logging results and handling exceptions in the application.
/// </summary>
public class ResultSettings
{
    /// <summary>
    /// Gets or sets the Logger instance that will be used for logging operations.
    /// Implements the IResultLogger interface.
    /// </summary>
    public IResultLogger Logger { get; set; }

    /// <summary>
    /// Gets or sets the factory function used to create <see cref="ExceptionError"/> instances.
    /// </summary>
    /// <remarks>
    /// The factory function accepts two parameters: a message of type <see cref="string"/> and an exception of type <see cref="Exception"/>.
    /// It returns an <see cref="ExceptionError"/> object that encapsulates the provided exception with the given message.
    /// This property allows customization of how exceptions are transformed into <see cref="IResultError"/> instances within the application.
    /// </remarks>
    public Func<Exception, ExceptionError> ExceptionErrorFactory { get; set; }
}