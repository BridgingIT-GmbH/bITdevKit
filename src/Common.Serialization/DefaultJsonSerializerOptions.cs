// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using BridgingIT.DevKit.Common.Converters;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class DefaultJsonSerializerOptions
{
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
                new DictionaryConverter(),
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
}