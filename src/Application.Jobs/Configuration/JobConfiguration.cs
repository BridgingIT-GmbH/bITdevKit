// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Represents appsettings overrides for code-registered jobs.
/// </summary>
public sealed record JobConfiguration(
    IReadOnlyDictionary<string, JobConfigurationOverride> Jobs)
{
    /// <summary>
    /// Parses a scheduler configuration section.
    /// </summary>
    public static JobConfiguration Parse(IConfiguration configuration)
    {
        var jobs = new Dictionary<string, JobConfigurationOverride>(StringComparer.OrdinalIgnoreCase);
        var jobsSection = configuration?.GetSection("Jobs");
        if (jobsSection is null)
        {
            return new JobConfiguration(jobs);
        }

        foreach (var jobSection in jobsSection.GetChildren())
        {
            var triggers = new Dictionary<string, JobTriggerConfigurationOverride>(StringComparer.OrdinalIgnoreCase);
            foreach (var triggerSection in jobSection.GetSection("Triggers").GetChildren())
            {
                triggers[triggerSection.Key] = new JobTriggerConfigurationOverride(
                    GetNullableBoolean(triggerSection["Enabled"]),
                    triggerSection["Schedule"] ?? triggerSection["Cron"],
                    GetStringValues(triggerSection, "TargetInstances"));
            }

            jobs[jobSection.Key] = new JobConfigurationOverride(
                GetNullableBoolean(jobSection["Enabled"]),
                GetStringValues(jobSection, "TargetInstances"),
                triggers);
        }

        return new JobConfiguration(jobs);
    }

    private static bool? GetNullableBoolean(string value)
    {
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static IReadOnlyList<string> GetStringValues(IConfigurationSection section, params string[] keys)
    {
        foreach (var key in keys ?? [])
        {
            var inlineValue = section[key];
            if (!string.IsNullOrWhiteSpace(inlineValue))
            {
                return inlineValue.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            var childValues = section.GetSection(key).GetChildren()
                .Select(x => x.Value?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            if (childValues.Length > 0)
            {
                return childValues;
            }
        }

        return [];
    }
}

/// <summary>
/// Represents appsettings overrides for a single code-registered job.
/// </summary>
/// <param name="Enabled">The optional enabled override.</param>
/// <param name="TargetInstances">The optional scheduler instance targets override.</param>
/// <param name="Triggers">The trigger overrides.</param>
public sealed record JobConfigurationOverride(
    bool? Enabled,
    IReadOnlyList<string> TargetInstances,
    IReadOnlyDictionary<string, JobTriggerConfigurationOverride> Triggers);

/// <summary>
/// Represents appsettings overrides for a single code-registered trigger.
/// </summary>
/// <param name="Enabled">The optional enabled override.</param>
/// <param name="Schedule">The optional schedule override.</param>
/// <param name="TargetInstances">The optional scheduler instance targets override.</param>
public sealed record JobTriggerConfigurationOverride(
    bool? Enabled,
    string Schedule,
    IReadOnlyList<string> TargetInstances);
