// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class Technical
    {
        /// <summary>Creates a <see cref="TechnicalError"/> for general technical issues.</summary>
        public static TechnicalError Error(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="ExceptionError"/> for encapsulating exceptions.</summary>
        public static ExceptionError Exception(Exception exception, string message = null)
            => new(exception, message);

        /// <summary>Creates a <see cref="MappingError"/> for data mapping failures.</summary>
        public static MappingError Mapping(Exception exception, string message = null)
            => new(exception, message);
    }
}

/// <summary>
/// Represents an error indicating a technical conflict or failure, typically used to signal issues that are not caused by invalid user input.
/// </summary>
/// <param name="message">The error message that describes the technical conflict. If null, a default message of "Conflict" is used.</param>
public class TechnicalError(string message = null) : ResultErrorBase(message ?? "Conflict")
{
    public TechnicalError() : this(null)
    {
    }
}