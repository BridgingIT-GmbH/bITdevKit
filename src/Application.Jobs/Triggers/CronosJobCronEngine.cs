// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Cronos;

/// <summary>
/// Provides the default Cronos-backed implementation of <see cref="IJobCronEngine"/>.
/// </summary>
public class CronosJobCronEngine : IJobCronEngine
{
    /// <inheritdoc />
    public Result Validate(string expression)
    {
        var parseResult = this.TryParse(expression);
        return parseResult.IsSuccess
            ? Result.Success()
            : Result.Failure(parseResult.Errors.Select(x => x.Message));
    }

    /// <inheritdoc />
    public Result<DateTimeOffset?> GetNextOccurrenceUtc(
        string expression,
        DateTimeOffset fromUtc,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var parseResult = this.TryParse(expression);
        if (!parseResult.IsSuccess)
        {
            return Result<DateTimeOffset?>.Failure(default(DateTimeOffset?)).WithErrors(parseResult.Errors);
        }

        var next = parseResult.Value.GetNextOccurrence(fromUtc, timeZone);
        return Result<DateTimeOffset?>.Success(next?.ToUniversalTime());
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<DateTimeOffset>> GetOccurrencesUtc(
        string expression,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeZoneInfo timeZone,
        bool fromInclusive = false,
        bool toInclusive = true)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (toUtc < fromUtc)
        {
            return Result<IReadOnlyList<DateTimeOffset>>.Success([]);
        }

        var parseResult = this.TryParse(expression);
        if (!parseResult.IsSuccess)
        {
            return Result<IReadOnlyList<DateTimeOffset>>.Failure([]).WithErrors(parseResult.Errors);
        }

        var occurrences = parseResult.Value
            .GetOccurrences(fromUtc, toUtc, timeZone, fromInclusive, toInclusive)
            .Select(x => x.ToUniversalTime())
            .ToArray();

        return Result<IReadOnlyList<DateTimeOffset>>.Success(occurrences);
    }

    private Result<CronExpression> TryParse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result<CronExpression>.Failure().WithError(new ValidationError("A cron expression is required."));
        }

        try
        {
            return Result<CronExpression>.Success(CronExpression.Parse(expression.Trim(), ResolveFormat(expression)));
        }
        catch (Exception exception) when (exception is CronFormatException or ArgumentException)
        {
            return Result<CronExpression>.Failure().WithError(new ValidationError($"Invalid cron expression '{expression}': {exception.Message}"));
        }
    }

    private static CronFormat ResolveFormat(string expression)
    {
        if (expression.StartsWith('@'))
        {
            return CronFormat.Standard;
        }

        var parts = expression.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            5 => CronFormat.Standard,
            6 => CronFormat.IncludeSeconds,
            _ => throw new ArgumentException("Cron expressions must use either 5 parts or 6 parts with seconds."),
        };
    }
}