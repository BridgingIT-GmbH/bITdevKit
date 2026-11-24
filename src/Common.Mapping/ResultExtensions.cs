// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Provides extension methods for manipulating and transforming <see cref="Result{TValue}" /> values.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    ///     Transforms the value of the specified <see cref="Result{TValue}" /> object to a new result with the specified
    ///     result type.
    /// </summary>
    /// <typeparam name="T">The type of the value in the source result.</typeparam>
    /// <typeparam name="TNew">The type of the value in the result after transformation.</typeparam>
    /// <param name="result">The source result containing the value to be transformed.</param>
    /// <param name="mapper">
    ///     The mapper instance used to convert the value from <typeparamref name="T" /> to
    ///     <typeparamref name="TNew" />.
    /// </param>
    /// <returns>
    ///     A new <see cref="Result{TValue}" /> instance containing the transformed value, with the original messages and
    ///     errors.
    /// </returns>
    public static Result<TNew> MapResult<T, TNew>(this Result<T> result, IMapper mapper)
        where TNew : class
    {
        return result.Bind<T, TNew>(value =>
        {
            if (mapper is null)
            {
                return Result<TNew>.Failure(new Error("Mapper cannot be null"));
            }

            try
            {
                return mapper.MapResult<T, TNew>(value);
            }
            catch (OperationCanceledException)
            {
                return Result<TNew>.Failure(new OperationCancelledError());
            }
        });
    }
}
