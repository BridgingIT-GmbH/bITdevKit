// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#pragma warning disable SA1200 // Using directives should be placed correctly
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
#pragma warning restore SA1200 // Using directives should be placed correctly

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();

await builder.Build().RunAsync();