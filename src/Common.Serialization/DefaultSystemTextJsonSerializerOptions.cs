// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class DefaultSystemTextJsonSerializerOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new EnumConverter<FilterOperator>(),
                new EnumConverter<FilterLogicOperator>(),
                new EnumConverter<FilterCustomType>(),
                new EnumConverter<OrderDirection>(),
                new FilterCriteriaJsonConverter(),
                new FilterSpecificationNodeConverter(),
                new JsonStringEnumConverter()
            },
            TypeInfoResolver =
                new PrivateConstructorContractResolver() // allow deserialization of types with only private constructors
            //IncludeFields = true,
            //PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate // TODO: .NET8 https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-8/#populate-read-only-members
        };

        return options;
    }
}