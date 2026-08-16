// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides standard exclusion patterns used when scanning application dependencies.
/// </summary>
public readonly struct Blacklists
{
    /// <summary>
    /// Gets assembly-name patterns for framework and third-party dependencies that should be skipped during application assembly scans.
    /// </summary>
    public static readonly string[] ApplicationDependencies =
    [
        "AspNetcore*",
        "AutoMapper*",
        "Azure*",
        "BenchmarkDotNet*",
        "Bogus*",
        "Cosmos*",
        "coverlet*",
        "Dapper*",
        "DnsClient*",
        "Dumpify*",
        "Ensure*",
        "FluentAssertions*",
        "FluentValidation*",
        "Fractions*",
        "Grpc*",
        "HealthChecks*",
        "Hellang*",
        "Humanizer*",
        "IdentityModel*",
        "KubernetesClient*",
        "LiteDB*",
        "Mapster*",
        "MediatR*",
        "MessagePack*",
        "Microsoft*",
        "MinVer*",
        "MudBlazor*",
        "NBuilder*",
        "NewId*",
        "Newtonsoft*",
        "NJsonSchema*",
        "NSubstitute*",
        "NSwag*",
        "OpenTelemetry*",
        "Polly*",
        "Quartz*",
        "RabbitMQ*",
        "Scrutor*",
        "Serilog*",
        "Shouldly*",
        "StyleCop*",
        "Swashbuckle*",
        "System*",
        "Testcontainers*",
        "Xunit*",
        "YamlDotNet*",
    ];
}
