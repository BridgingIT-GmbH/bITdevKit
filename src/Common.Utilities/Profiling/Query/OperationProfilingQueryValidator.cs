// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Validates bounded selectors without changing the provider's captured paging boundary.</summary>
/// <example><code>var valid = validator.Validate(query);</code></example>
public sealed class OperationProfilingQueryValidator
{
    private readonly ProfilingOperationQueryCodec codec;

    /// <summary>Creates a validator sharing the recording and retained-query limits.</summary>
    /// <example><code>var validator = new OperationProfilingQueryValidator(options, TimeProvider.System);</code></example>
    public OperationProfilingQueryValidator(ProfilingOptions options, TimeProvider clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.codec = new(options.Operations, options.Queries, clock ?? TimeProvider.System, []);
    }

    /// <summary>Checks supported selectors, typed predicates and UTC ranges; the provider authenticates cursors.</summary>
    /// <example><code>if (validator.Validate(query).IsFailure) return BadRequest();</code></example>
    public IResult Validate(OperationProfilingQuery query)
    {
        try
        {
            if (query?.Cursor?.Length > 8192) { return Result.Failure(new ProfilingQueryBoundaryError()); }

            this.codec.Normalize(query, query?.Boundary);
            return Result.Success();
        }
        catch (ArgumentException)
        {
            return Result.Failure(new ProfilingValidationError("Profiling queries require bounded selectors, typed values and UTC intervals."));
        }
    }

    /// <summary>Applies all selected segment predicates to one summary, never to different siblings.</summary>
    /// <example><code>var selected = record.Segments.Where(segment => OperationProfilingQueryValidator.MatchesSegment(segment, query));</code></example>
    public static bool MatchesSegment(ProfilingSegmentSummary summary, OperationProfilingQuery query) =>
        (query.SegmentKey is null || ProfilingKeyComparer.Instance.Equals(summary.Key, query.SegmentKey))
        && (query.SegmentPath is null || summary.Path.Equals(query.SegmentPath))
        && (query.SegmentOutcomes.Count == 0 || summary.Outcomes.Any(outcome => query.SegmentOutcomes.Contains(outcome.Outcome) && outcome.Statistics.Count > 0))
        && query.SegmentDimensions.All(predicate =>
        {
            var dimension = summary.Dimensions.FirstOrDefault(value => ProfilingKeyComparer.Instance.Equals(value.Key, predicate.Key));
            return predicate.Operator switch
            {
                ProfilingDimensionOperator.Equal => dimension is { Mixed: false, Partial: false } && dimension.Value == predicate.Value,
                ProfilingDimensionOperator.Present => dimension?.Value is not null || dimension?.Mixed == true,
                ProfilingDimensionOperator.Missing => dimension?.Value is null && dimension?.Mixed != true,
                ProfilingDimensionOperator.Mixed => dimension?.Mixed == true,
                _ => false,
            };
        });
}
