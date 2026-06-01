// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

global using System.Net;
global using System.Net.Http.Json;
global using System.Security.Claims;
global using System.Text.Encodings.Web;
global using BridgingIT.DevKit.Common;
global using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
global using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;
global using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;
global using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;
global using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
global using BridgingIT.DevKit.Infrastructure.EntityFramework;
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using NSubstitute;
global using Shouldly;
global using Xunit;
global using Xunit.Abstractions;
