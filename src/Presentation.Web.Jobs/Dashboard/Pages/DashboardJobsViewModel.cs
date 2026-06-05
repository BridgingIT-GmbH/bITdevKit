// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard.Pages;

using BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// View model for the server-rendered Jobs dashboard page.
/// </summary>
public sealed class DashboardJobsViewModel
{
    /// <summary>
    /// Gets or sets the UTC timestamp when this model was captured.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the URL of the refreshable content fragment.
    /// </summary>
    public string ContentPath { get; set; } = "/_bdk/dashboard/jobs/content";

    /// <summary>
    /// Gets or sets the dashboard summary (counts for the header cards).
    /// </summary>
    public JobSchedulerDashboardSummaryModel Summary { get; set; } = new();

    /// <summary>
    /// Gets or sets the paged list of registered jobs.
    /// </summary>
    public IReadOnlyList<JobSchedulerJobModel> Jobs { get; set; } = [];

    /// <summary>
    /// Gets or sets the most recent occurrences across all jobs.
    /// </summary>
    public IReadOnlyList<JobSchedulerOccurrenceModel> Occurrences { get; set; } = [];

    /// <summary>
    /// Gets the errors that occurred while building this model.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the Jobs scheduler service is registered.
    /// </summary>
    public bool IsAvailable { get; set; } = true;
}
