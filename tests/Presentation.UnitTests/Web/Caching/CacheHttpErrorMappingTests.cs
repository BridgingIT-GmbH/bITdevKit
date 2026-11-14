// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks that Application failure categories retain their HTTP meaning through the shared Result mapper.</summary>
public sealed class CacheHttpErrorMappingTests
{
    /// <summary>Typed cache failures produce the documented Problem Details status before delivery starts.</summary>
    [Theory]
    [InlineData(CacheErrorCode.Validation, 400)]
    [InlineData(CacheErrorCode.Contract, 400)]
    [InlineData(CacheErrorCode.Unsupported, 400)]
    [InlineData(CacheErrorCode.NotFound, 404)]
    [InlineData(CacheErrorCode.Conflict, 412)]
    [InlineData(CacheErrorCode.Capacity, 429)]
    [InlineData(CacheErrorCode.SourceFailure, 502)]
    [InlineData(CacheErrorCode.ProviderUnavailable, 503)]
    [InlineData(CacheErrorCode.SourceRejected, 503)]
    [InlineData(CacheErrorCode.Indeterminate, 503)]
    [InlineData(CacheErrorCode.CoordinationTimeout, 504)]
    [InlineData(CacheErrorCode.SourceTimeout, 504)]
    [InlineData(CacheErrorCode.Corruption, 500)]
    public async Task SharedMapping_PreservesFailureCategoryAndProblemDetails(CacheErrorCode code, int expected)
    {
        CacheHttpErrorMapping.Register();
        var services = new ServiceCollection().AddLogging().AddOptions();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        var sut = CacheHttpResultMapper.Failure(Result.Failure(new CacheError(code, "Controlled failure")));

        await sut.ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(expected);
        context.Response.ContentType.ShouldStartWith("application/problem+json");
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(expected);
        json.RootElement.GetProperty("code").GetString().ShouldBe(code.ToString());
    }
}