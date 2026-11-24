# DevKit CLI

`BridgingIT.DevKit.Cli` installs the `bdk` .NET tool for local host discovery, host Console Commands, and a STDIO MCP server for coding agents.

## Use a repository-local tool

Restore an existing repository tool manifest with `dotnet tool restore`. For a new consuming repository:

```sh
dotnet new tool-manifest
dotnet tool install BridgingIT.DevKit.Cli
dotnet tool run bdk version
```

Configure the MCP client to start `dotnet tool run bdk mcp` from that repository. Runtime tools require a running local Development host. Documentation and guidance work before the application starts.

## Embedded documentation

The CLI assembly contains public DevKit Markdown and generated API metadata. `bdk_docs_search`, `bdk_docs_get`, `bdk_api_search`, and `bdk_api_get` read embedded resources without contacting GitHub or GitHub Pages. There is no documentation cache, separate content directory, or online fallback.

The installed tool determines the available content. Version differences between MCP and the application's DevKit packages are ignored. Official GitHub document URLs are local path aliases regardless of their branch, tag, or version.

`bdk_docs_get` accepts `source`, bounded `maxChars`, and optional `offset`. Follow the returned `next` call while `nextOffset` is present. Offsets and `totalChars` use UTF-16 indices; chunks preserve complete Unicode characters. Missing content returns `documentation_unavailable`. `bdk_mcp_status` reports documentation source `embedded`.

After installing a rebuilt tool, restart its MCP connection to use the new content.

## Build documentation from source

The website and MCP share the public root-level patterns in `docs/public-documentation.props`. Matching document additions, edits, renames, and removals are embedded on the next build. No per-file registration or manual sync is required. ADRs, specifications, PRDs, presentations, and generated site content stay excluded. Curated guidance topics are authored separately.

Debug builds skip API generation and can reuse previously generated API files. Missing API metadata affects API lookup only. Release builds and packaging generate metadata using PowerShell and the pinned DocFX tool, without website HTML or Docker. Standard MSBuild incremental inputs avoid repeating extraction when API inputs are unchanged.

```sh
dotnet tool restore
dotnet build src/Presentation.Cli/Presentation.Cli.csproj --configuration Release
```

For a Debug package prepared with `--no-build`, embed the API data first:

```sh
dotnet build src/Presentation.Cli/Presentation.Cli.csproj --configuration Debug -p:BdkGenerateMcpApiReference=true
dotnet pack src/Presentation.Cli/Presentation.Cli.csproj --configuration Debug --no-build
```

Release builds and packaging check only for the `api/agent-index.json` completeness marker. They do not validate individual links or details. `--no-build` checks the already-built assembly and does not include later documentation edits.

## Further reading

- [DevKit CLI guide](https://github.com/bridgingit/bitdevkit/blob/main/docs/features-cli.md)
- [DevKit MCP guide](https://github.com/bridgingit/bitdevkit/blob/main/docs/features-cli-mcp.md)
- [MCP client configuration](https://github.com/bridgingit/bitdevkit/blob/main/docs/features-cli-mcp-clients.md)
