// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Provides extension methods for manipulating and transforming <see cref="Result{TValue}" /> resultb values.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    ///     Transforms the value of the specified <see cref="Result{TValue}" /> object to a new result with the specified
    ///     result type.
    /// </summary>
    /// <typeparam name="TValue">The type of the value in the source result.</typeparam>
    /// <typeparam name="TResult">The type of the value in the result after transformation.</typeparam>
    /// <param name="source">The source result containing the value to be transformed.</param>
    /// <param name="mapper">
    ///     The mapper instance used to convert the value from <typeparamref name="TValue" /> to
    ///     <typeparamref name="TResult" />.
    /// </param>
    /// <returns>
    ///     A new <see cref="Result{TValue}" /> instance containing the transformed value, with the original messages and
    ///     errors.
    /// </returns>
    public static Result<TResult> For<TValue, TResult>(this Result<TValue> source, IMapper mapper)
        where TResult : class
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (source?.IsFailure == true)
        {
            return Result<TResult>.Failure().WithMessages(source?.Messages).WithErrors(source?.Errors);
        }

        return Result<TResult>.Success(source != null ? mapper.Map<TValue, TResult>(source.Value) : null)
            .WithMessages(source?.Messages)
            .WithErrors(source?.Errors);
    }
}