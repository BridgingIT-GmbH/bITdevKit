// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class DefaultJsonSerializerOptions
{
    /// <summary>
    /// Creates the default <see cref="JsonSerializerOptions"/> preconfigured for the DevKit.
    /// </summary>
    /// <returns>A new instance of <see cref="JsonSerializerOptions"/> with the DevKit defaults applied.</returns>
    /// <remarks>
    /// Defaults applied:
    /// <list type="bullet">
    /// <item><description><see cref="JsonSerializerOptions.WriteIndented"/> = true</description></item>
    /// <item><description><see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> = true</description></item>
    /// <item><description><see cref="JsonSerializerOptions.PropertyNamingPolicy"/> = <see cref="JsonNamingPolicy.CamelCase"/></description></item>
    /// <item><description><see cref="JsonSerializerOptions.DefaultIgnoreCondition"/> = <see cref="JsonIgnoreCondition.WhenWritingNull"/></description></item>
    /// <item><description><see cref="JsonSerializerOptions.TypeInfoResolver"/> = <see cref="UniversalContractResolver"/> (supports private constructors)</description></item>
    /// <item><description>Enum converters using <see cref="EnumMemberConverter{TEnum}"/> for several internal enums</description></item>
    /// <item><description>Result converters (<see cref="ResultJsonConverter"/>, value &amp; paged result factories)</description></item>
    /// <item><description><see cref="JsonStringEnumConverter"/> for string enum serialization</description></item>
    /// </list>
    /// Use one of the overloads with parameters to append converters or override settings after creation.
    /// </remarks>
    public static JsonSerializerOptions Create()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new UniversalContractResolver(), // allow deserialization of types with only private constructors
            Converters =
            {
                new EnumMemberConverter<FilterOperator>(),
                new EnumMemberConverter<FilterLogicOperator>(),
                new EnumMemberConverter<FilterCustomType>(),
                new EnumMemberConverter<OrderDirection>(),
                new EnumMemberConverter<PageSize>(),
                //new DictionaryConverter(), // causes issues with deserializing dictionaries in the problem details (data), loosing the data property
                new FilterCriteriaJsonConverter(),
                new FilterSpecificationNodeConverter(),
                new ResultJsonConverter(),
                new ResultValueJsonConverterFactory(),
                new ResultPagedJsonConverterFactory(),
                new JsonStringEnumConverter(), // read/write enums as strings
            },
            //IncludeFields = true,
            //PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate // TODO: .NET8 https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-8/#populate-read-only-members
        };
    }

    /// <summary>
    /// Creates the default <see cref="JsonSerializerOptions"/> and appends additional converters.
    /// </summary>
    /// <param name="converters">Additional converters to add. Null instances are skipped.</param>
    /// <returns>The configured <see cref="JsonSerializerOptions"/> instance.</returns>
    /// <remarks>
    /// This overload is additive only. To override existing properties (e.g. naming policy) use the <see cref="Create(Action{JsonSerializerOptions})"/> overload.
    /// </remarks>
    public static JsonSerializerOptions Create(params JsonConverter[] converters)
    {
        var options = Create();
        if (converters is { Length: > 0 })
        {
            foreach (var c in converters)
            {
                if (c is not null)
                {
                    options.Converters.Add(c);
                }
            }
        }

        return options;
    }

    /// <summary>
    /// Creates the default <see cref="JsonSerializerOptions"/> then invokes a custom configuration action allowing overrides.
    /// </summary>
    /// <param name="configure">Action that can further configure or override the default options. If null, defaults remain unchanged.</param>
    /// <returns>The configured <see cref="JsonSerializerOptions"/> instance.</returns>
    /// <remarks>
    /// Use this overload when you need to override defaults (e.g. change <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>, adjust <see cref="JsonSerializerOptions.DefaultIgnoreCondition"/> or modify converter ordering).
    /// </remarks>
    public static JsonSerializerOptions Create(Action<JsonSerializerOptions> configure)
    {
        var options = Create();
        configure?.Invoke(options);

        return options;
    }
}