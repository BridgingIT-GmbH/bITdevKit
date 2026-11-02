// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Text;

/// <summary>Validates the Entities dashboard setup at startup so typos and unsupported rules fail fast.</summary>
internal static class EntitiesDashboardConfigurationValidator
{
    /// <summary>Throws an <see cref="InvalidOperationException"/> listing every configuration problem.</summary>
    /// <param name="options">The options to validate.</param>
    public static void Validate(EntitiesDashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var problems = new List<string>();

        CheckLimit(problems, nameof(EntitiesDashboardOptions.BatchSize), options.BatchSize);
        CheckLimit(problems, nameof(EntitiesDashboardOptions.MaxBatchSize), options.MaxBatchSize);
        CheckLimit(problems, nameof(EntitiesDashboardOptions.MaxExportRows), options.MaxExportRows);
        CheckLimit(problems, nameof(EntitiesDashboardOptions.MaxBulkSelection), options.MaxBulkSelection);

        foreach (var configuration in options.EntityConfigurations.Values)
        {
            var typeName = configuration.EntityType.Name;

            foreach (var rule in configuration.Properties.Values)
            {
                ValidateRulePath(problems, configuration.EntityType, typeName, rule.Path);
            }

            foreach (var path in configuration.References.Keys)
            {
                if (path.Contains('.', StringComparison.Ordinal)
                    || EntitiesDashboardTypeInfo.FindProperty(configuration.EntityType, path) == null)
                {
                    problems.Add(
                        $"{typeName}: reference '{path}' must be a public top-level property.");
                }
            }
        }

        if (problems.Count == 0)
        {
            return;
        }

        var message = new StringBuilder("Invalid EntitiesDashboard configuration:");
        foreach (var problem in problems)
        {
            message.AppendLine().Append("- ").Append(problem);
        }

        throw new InvalidOperationException(message.ToString());
    }

    private static void CheckLimit(List<string> problems, string name, int value)
    {
        if (value < 1)
        {
            problems.Add($"{name} must be at least 1 (was {value}).");
        }
    }

    private static void ValidateRulePath(List<string> problems, Type entityType, string typeName, string path)
    {
        var segments = path.Split('.');
        if (segments.Length > 2 || segments.Any(string.IsNullOrWhiteSpace))
        {
            problems.Add($"{typeName}: property path '{path}' must be a property or one level of a value-object property.");

            return;
        }

        if (string.Equals(segments[0], "Id", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"{typeName}: the Id property cannot be masked or hidden ('{path}').");

            return;
        }

        var property = EntitiesDashboardTypeInfo.FindProperty(entityType, segments[0]);
        if (property == null)
        {
            problems.Add($"{typeName}: property '{segments[0]}' is not a public instance property.");

            return;
        }

        if (segments.Length == 1)
        {
            return;
        }

        var propertyType = EntitiesDashboardTypeInfo.Unwrap(property.PropertyType);
        if (EntitiesDashboardTypeInfo.IsScalar(propertyType)
            || EntitiesDashboardTypeInfo.IsCollection(propertyType)
            || propertyType == typeof(byte[]))
        {
            problems.Add($"{typeName}: '{segments[0]}' is not a value-object property, so '{path}' cannot be resolved.");

            return;
        }

        if (EntitiesDashboardTypeInfo.FindProperty(propertyType, segments[1]) == null)
        {
            problems.Add($"{typeName}: '{segments[1]}' is not a public instance property of {propertyType.Name} ('{path}').");
        }
    }
}
