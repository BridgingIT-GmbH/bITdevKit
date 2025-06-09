// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;
using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a request to query log entries with various filtering, searching, paging, and continuation options.
/// </summary>
public class LogEntryQueryRequest
{
    /// <summary>
    /// Gets or sets the start of the time range for filtering logs (inclusive).
    /// If null and <see cref="Age"/> is null, defaults to the start of the current day.
    /// Mutually exclusive with <see cref="Age"/>.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end of the time range for filtering logs (inclusive).
    /// If null, logs are retrieved up to the present.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum log level to filter by (e.g., Information).
    /// Includes the specified level and all higher severity levels (e.g., Warning, Error, Fatal).
    /// If null, all levels are included.
    /// </summary>
    public LogLevel? Level { get; set; }

    /// <summary>
    /// Gets or sets the TraceId to filter by.
    /// If null or empty, no TraceId filter is applied.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the CorrelationId to filter by.
    /// If null or empty, no CorrelationId filter is applied.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the LogKey to filter by.
    /// If null or empty, no LogKey filter is applied.
    /// </summary>
    public string LogKey { get; set; }

    /// <summary>
    /// Gets or sets the ModuleName to filter by.
    /// If null or empty, no ModuleName filter is applied.
    /// </summary>
    public string ModuleName { get; set; }

    ///// <summary>
    ///// Gets or sets the ThreadId to filter by.
    ///// If null or empty, no ThreadId filter is applied.
    ///// </summary>
    //public string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the ShortTypeName to filter by.
    /// If null or empty, no ShortTypeName filter is applied.
    /// </summary>
    public string ShortTypeName { get; set; }

    /// <summary>
    /// Gets or sets the text to search within Message, Exception, and LogEvent fields.
    /// If null or empty, no full-text search is applied.
    /// </summary>
    public string SearchText { get; set; }

    /// <summary>
    /// Gets or sets the number of records per page (default: 1000).
    /// Must be a positive integer.
    /// </summary>
    public int PageSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the continuation token for retrieving the next page of results.
    /// If null or empty, starts from the first page.
    /// </summary>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the age of logs to include, as a duration from the present.
    /// Converted to <see cref="StartTime"/> as (UtcNow - Age).
    /// Mutually exclusive with <see cref="StartTime"/>.
    /// </summary>
    public TimeSpan? Age { get; set; }

    /// <summary>
    /// Validates the request parameters.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (this.StartTime.HasValue && this.Age.HasValue)
        {
            throw new ArgumentException("StartTime and Age cannot both be specified.");
        }

        if (this.Age < TimeSpan.Zero)
        {
            throw new ArgumentException("Age cannot be negative.");
        }

        if (this.StartTime.HasValue && this.EndTime.HasValue && this.StartTime > this.EndTime)
        {
            throw new ArgumentException("StartTime cannot be greater than EndTime.");
        }

        if (this.PageSize <= 0)
        {
            throw new ArgumentException("PageSize must be positive.");
        }

        //if (!string.IsNullOrEmpty(this.LogKey) && this.LogKey.Contains(";"))
        //{
        //    throw new ArgumentException("LogKey contains invalid characters.");
        //}

        //if (!string.IsNullOrEmpty(this.ModuleName) && this.ModuleName.Contains(";"))
        //{
        //    throw new ArgumentException("ModuleName contains invalid characters.");
        //}

        //if (!string.IsNullOrEmpty(this.ThreadId) && this.ThreadId.Contains(";"))
        //{
        //    throw new ArgumentException("ThreadId contains invalid characters.");
        //}

        //if (!string.IsNullOrEmpty(this.ShortTypeName) && this.ShortTypeName.Contains(";"))
        //{
        //    throw new ArgumentException("ShortTypeName contains invalid characters.");
        //}

        if (!string.IsNullOrEmpty(this.SearchText) && Regex.IsMatch(this.SearchText, @"[\p{Cc}\p{Cf}]"))
        {
            throw new ArgumentException("SearchText contains invalid control characters.");
        }
    }
}
