// // MIT-License
// // Copyright BridgingIT GmbH - All Rights Reserved
// // Use of this source code is governed by an MIT-style license that can be
// // found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
//
// namespace BridgingIT.DevKit.Application.Commands;
//
// public static class ResultExtensions
// {
//     // Extensions for Result<TValue>
//     public static CommandResponse<Result<TValue>> ToCommandResponse<TValue>(this Result<TValue> result)
//     {
//         return new CommandResponse<Result<TValue>> { Result = result };
//     }
//
//     public static CommandResponse<Result> ToCommandResponse<TValue>(this Result<TValue> result)
//     {
//         if (result?.IsFailure == true)
//         {
//             return new CommandResponse<Result>
//             {
//                 Result = Result.Failure()
//                     .WithMessages(result?.Messages)
//                     .WithErrors(result?.Errors)
//             };
//         }
//
//         return new CommandResponse<Result>
//         {
//             Result = Result.Success()
//                 .WithMessages(result?.Messages)
//                 .WithErrors(result?.Errors)
//         };
//     }
//
//     public static CommandResponse<Result<TResult>> ForDifferentType<TValue, TResult>(this Result<TValue> result)
//     {
//         if (result?.IsFailure == true)
//         {
//             return new CommandResponse<Result<TResult>>
//             {
//                 Result = Result<TResult>.Failure()
//                     .WithMessages(result?.Messages)
//                     .WithErrors(result?.Errors)
//             };
//         }
//
//         return new CommandResponse<Result<TResult>>
//         {
//             Result = Result<TResult>.Success()
//                 .WithMessages(result?.Messages)
//                 .WithErrors(result?.Errors)
//         };
//     }
//
//     // Extensions for base Result
//     public static CommandResponse<Result<TValue>> ForValue<TValue>(this Result result)
//     {
//         if (result?.IsFailure == true)
//         {
//             return new CommandResponse<Result<TValue>>
//             {
//                 Result = Result<TValue>.Failure()
//                     .WithMessages(result?.Messages)
//                     .WithErrors(result?.Errors)
//             };
//         }
//
//         return new CommandResponse<Result<TValue>>
//         {
//             Result = Result<TValue>.Success()
//                 .WithMessages(result?.Messages)
//                 .WithErrors(result?.Errors)
//         };
//     }
//
//     public static CommandResponse<Result<TValue>> ForValueWithData<TValue>(
//         this Result result,
//         TValue value)
//     {
//         if (result?.IsFailure == true)
//         {
//             return new CommandResponse<Result<TValue>>
//             {
//                 Result = Result<TValue>.Failure(value)
//                     .WithMessages(result?.Messages)
//                     .WithErrors(result?.Errors)
//             };
//         }
//
//         return new CommandResponse<Result<TValue>>
//         {
//             Result = Result<TValue>.Success(value)
//                 .WithMessages(result?.Messages)
//                 .WithErrors(result?.Errors)
//         };
//     }
//
//     public static CommandResponse<Result> ForBase(this Result result)
//     {
//         if (result?.IsFailure == true)
//         {
//             return new CommandResponse<Result>
//             {
//                 Result = Result.Failure()
//                     .WithMessages(result?.Messages)
//                     .WithErrors(result?.Errors)
//             };
//         }
//
//         return new CommandResponse<Result>
//         {
//             Result = Result.Success()
//                 .WithMessages(result?.Messages)
//                 .WithErrors(result?.Errors)
//         };
//     }
//
//     public static CommandResponse<Result<TResult>> ForResult<TResult>(this Result result)
//     {
//         if (result?.IsFailure == true)
//         {
//             return new CommandResponse<Result<TResult>>
//             {
//                 Result = Result<TResult>.Failure()
//                     .WithMessages(result?.Messages)
//                     .WithErrors(result?.Errors)
//             };
//         }
//
//         return new CommandResponse<Result<TResult>>
//         {
//             Result = Result<TResult>.Success()
//                 .WithMessages(result?.Messages)
//                 .WithErrors(result?.Errors)
//         };
//     }
// }