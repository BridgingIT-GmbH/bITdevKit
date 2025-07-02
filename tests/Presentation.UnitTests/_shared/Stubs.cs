// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests;

using Domain.Model;
using Microsoft.AspNetCore.Http;

public class PersonStub : Entity<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }

    public static PersonStub Create(long ticks)
    {
        return new PersonStub { FirstName = $"John{ticks}", LastName = $"Doe{ticks}", Age = 42 };
    }
}

public class CustomHttpResult(string message) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => 418; // I'm a teapot, for fun

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = this.StatusCode ?? 500;
        return httpContext.Response.WriteAsync(message);
    }
}