// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Usage examples:
// program echo Hello
// program echo Hello --upper
// program echo "Hello World" --repeat3 --color yellow
// program echo greet --repeat2 --upper --color green
// program help
// program help echo

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConsoleCommands(cfg =>
{
    cfg.AddCommand<EchoConsoleCommand>(); // register commands
});

using var host = builder.Build();

return await ConsoleCommands.RunAsync(host.Services, args);