// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics.CodeAnalysis;
using Shouldly;

public static class ShouldBeResultExtensions
{
    public static void ShouldBeSuccess([DoesNotReturnIf(false)] this IResult actual, string customMessage = null)
    {
        if (!actual.IsSuccess)
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(true, actual, customMessage).ToString());
        }
    }

    public static void ShouldBeSuccess<T>([DoesNotReturnIf(false)] this IResult<T> actual, string customMessage = null)
    {
        if (!actual.IsSuccess)
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(true, actual, customMessage).ToString());
        }
    }

    public static void ShouldBeFailure([DoesNotReturnIf(false)] this IResult actual, string customMessage = null)
    {
        if (actual.IsSuccess)
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(false, actual, customMessage).ToString());
        }
    }

    public static void ShouldBeFailure<T>([DoesNotReturnIf(false)] this IResult<T> actual, string customMessage = null)
    {
        if (actual.IsSuccess)
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(false, actual, customMessage).ToString());
        }
    }

    public static void ShouldContainMessage(
        [DoesNotReturnIf(false)] this IResult actual,
        string expected,
        string customMessage = null)
    {
        if (actual.Messages?.Contains(expected) == false)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    public static void ShouldContainMessage<T>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        string expected,
        string customMessage = null)
    {
        if (actual.Messages?.Contains(expected) == false)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    public static void ShouldNotContainMessage(
        [DoesNotReturnIf(false)] this IResult actual,
        string expected,
        string customMessage = null)
    {
        if (actual.Messages?.Contains(expected) == true)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    public static void ShouldNotContainMessage<T>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        string expected,
        string customMessage = null)
    {
        if (actual.Messages?.Contains(expected) == true)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    public static void ShouldContainMessages([DoesNotReturnIf(false)] this IResult actual, string customMessage = null)
    {
        if (actual.Messages.IsNullOrEmpty())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(true, actual, customMessage).ToString());
        }
    }

    public static void ShouldContainMessages<T>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        string customMessage = null)
    {
        if (actual.Messages.IsNullOrEmpty())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(true, actual, customMessage).ToString());
        }
    }

    public static void ShouldNotContainMessages(
        [DoesNotReturnIf(false)] this IResult actual,
        string customMessage = null)
    {
        if (!actual.Messages.IsNullOrEmpty())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(false, actual, customMessage).ToString());
        }
    }

    public static void ShouldNotContainMessages<T>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        string customMessage = null)
    {
        if (!actual.Messages.IsNullOrEmpty())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(false, actual, customMessage).ToString());
        }
    }

    public static void ShouldContainError<TError>(
        [DoesNotReturnIf(false)] this IResult actual,
        string customMessage = null)
        where TError : IResultError, new()
    {
        if (!actual.HasError<TError>())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(typeof(TError), actual, customMessage)
                .ToString());
        }
    }

    public static void ShouldContainError<T, TError>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        string customMessage = null)
        where TError : IResultError, new()
    {
        if (!actual.HasError<TError>())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(typeof(TError), actual, customMessage)
                .ToString());
        }
    }

    public static void ShouldNotContainError<TError>(
        [DoesNotReturnIf(false)] this IResult actual,
        string customMessage = null)
        where TError : IResultError, new()
    {
        if (actual.HasError<TError>())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(typeof(TError), actual, customMessage)
                .ToString());
        }
    }

    public static void ShouldNotContainError<T, TError>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        string customMessage = null)
        where TError : IResultError, new()
    {
        if (actual.HasError<TError>())
        {
            throw new ShouldAssertException(new ExpectedActualShouldlyMessage(typeof(TError), actual, customMessage)
                .ToString());
        }
    }

    public static void ShouldBeValue<T>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        T expected,
        string customMessage = null)
    {
        if (actual.Value?.Equals(expected) == false)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }

    public static void ShouldNotBeValue<T>(
        [DoesNotReturnIf(false)] this IResult<T> actual,
        T expected,
        string customMessage = null)
    {
        if (actual.Value?.Equals(expected) == true)
        {
            throw new ShouldAssertException(
                new ExpectedActualShouldlyMessage(expected, actual, customMessage).ToString());
        }
    }
}