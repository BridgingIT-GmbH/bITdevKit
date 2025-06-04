// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a builder class for configuring <see cref="ResultSettings"/>.
/// </summary>
public class ResultSettingsBuilder
{
    /// <summary>
    /// Gets or sets the logger to be used for logging result-related information and errors.
    /// </summary>
    /// <remarks>
    /// The logger must implement the <see cref="IResultLogger"/> interface. By default,
    /// it is set to an instance of <see cref="ResultNullLogger"/> if not specified.
    /// </remarks>
    public IResultLogger Logger { get; set; }

    /// <summary>
    /// Gets or sets the delegate function responsible for creating <see cref="ExceptionError"/> instances.
    /// </summary>
    /// <value>
    /// A <see cref="Func{T1, T2, TResult}"/> that takes a string message and an <see cref="Exception"/>,
    /// and returns an <see cref="ExceptionError"/>. By default, it is set to a function that creates a new
    /// <see cref="ExceptionError"/> instance with the provided exception and message.
    /// </value>
    public Func<Exception, ExceptionError> ExceptionErrorFactory { get; set; }

    /// <summary>
    /// The ResultSettingsBuilder class is responsible for creating instances of ResultSettings.
    /// It allows configuring the logger and the exception error factory used by the Result class.
    /// </summary>
    public ResultSettingsBuilder()
    {
        this.Logger = new ResultNullLogger();
        this.ExceptionErrorFactory = (exception) => new ExceptionError(exception);
    }

    /// <summary>
    /// Builds and returns a ResultSettings object.
    /// </summary>
    /// <return>
    /// A ResultSettings object configured with the current properties of the ResultSettingsBuilder instance.
    /// </return>
    public ResultSettings Build()
    {
        return new ResultSettings
        {
            Logger = this.Logger ?? new ResultNullLogger(),
            ExceptionErrorFactory = this.ExceptionErrorFactory
        };
    }
}