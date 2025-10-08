// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class ResultTaskExtensions
{
    public static async Task<Result<TNew>> MapResult<T, TNew>(this Task<Result<T>> resultTask, IMapper mapper)
        where TNew : class
    {
        if (mapper is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.MapResult<T, TNew>(mapper);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Asynchronously transforms the value of the specified <see cref="Result{TValue}" /> object to a new result with the
    ///     specified result type.
    /// </summary>
    /// <typeparam name="T">The type of the value in the source result.</typeparam>
    /// <typeparam name="TNew">The type of the value in the result after transformation.</typeparam>
    /// <param name="resultTask">A task that represents the asynchronous operation, containing the source result.</param>
    /// <param name="mapper">
    ///     The mapper instance used to convert the value from <typeparamref name="T" /> to
    ///     <typeparamref name="TNew" />.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a new <see cref="Result{TValue}" /> instance with
    ///     the transformed value, original messages, and errors.
    /// </returns>
    public static async Task<Result<TNew>> MapResultAsync<T, TNew>(this Task<Result<T>> resultTask, IMapper mapper)
        where TNew : class
    {
        if (mapper is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.MapResult<T, TNew>(mapper);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }
}