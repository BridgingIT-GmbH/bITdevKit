// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class Data
    {
        /// <summary>Creates an <see cref="EntityNotFoundError"/> when an entity cannot be found.</summary>
        public static EntityNotFoundError EntityNotFound(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="EntityDuplicateError"/> for duplicate entity violations.</summary>
        public static EntityDuplicateError EntityDuplicate(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="NotFoundError"/> for generic not found scenarios.</summary>
        public static NotFoundError NotFound(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DataIntegrityError"/> for data consistency violations.</summary>
        public static DataIntegrityError DataIntegrity(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="ConcurrencyError"/> for optimistic concurrency conflicts.</summary>
        public static ConcurrencyError Concurrency(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="StaleDataError"/> when data has changed since loaded.</summary>
        public static StaleDataError StaleData(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="ChangeError"/> for entity change operation failures.</summary>
        public static ChangeError Change(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DataError"/> for general data persistence and consistency errors.</summary>
        public static DataError Error(string message = null)
            => new(message);
    }
}

/// <summary>
/// Represents a general data persistence or consistency error.
/// </summary>
/// <param name="message">The error message that describes the data error. If null, a default message is used.</param>
public class DataError(string message = null) : ResultErrorBase(message ?? "Data error")
{
    public DataError() : this(null)
    {
    }
}