# AGENTS.md

Repository guidance for agents working on bITdevKit, a modular .NET toolkit built around Domain-Driven Design, Clean Architecture, and modular vertical slices.

## Start here

- Read [docs/INDEX.md](docs/INDEX.md) for the feature map and [README.md](README.md) for repository and documentation workflows.
- Treat [.github/copilot-instructions.md](.github/copilot-instructions.md) as authoritative for architectural boundaries, module practices, and package preferences. Use [.editorconfig](.editorconfig) for formatting and naming.
- Resolve SDK, language, framework, and package versions from [global.json](global.json), [Directory.Build.props](Directory.Build.props), project files, and [Directory.Packages.props](Directory.Packages.props). These files take precedence over older version references in prose.
- Consult the relevant [ADRs](docs/adr/), especially [Clean Architecture](docs/adr/0001-clean-onion-architecture.md) and [modular monoliths](docs/adr/0003-modular-monolith-architecture.md).
- Inspect existing implementations before adding code. Reuse DevKit abstractions, requester/notifier, pipeline behaviors, results, rules, and specifications.

## Repository layout

Projects are grouped by package name directly under `src/`, rather than nested layer folders:

| Path or pattern | Purpose |
| --- | --- |
| `src/Common.*` | Shared abstractions, results, rules, extensions, mapping, and utilities |
| `src/Domain`, `src/Domain.*` | Domain building blocks, events, mediation, and outbox support |
| `src/Application.*` | Commands, queries, and feature service contracts and orchestration |
| `src/Infrastructure.*` | Persistence providers, messaging transports, and external integrations |
| `src/Presentation`, `src/Presentation.*` | Host integration, minimal APIs, dashboards, and client support |
| `tests/*.UnitTests`, `tests/*.IntegrationTests` | Toolkit unit and integration test projects |
| `tests/Shared` | Shared test fixtures and models |
| `examples/` | Sample applications and their tests |
| `benchmarks/` | Performance benchmarks |
| `.agents/skills/` | Repository skills |
| `.vscode/tasks.json` | Build, test, run, and documentation tasks |
| `bITdevKit.slnx` | Main solution |

The main runtime projects target .NET 10. Source generators target .NET Standard 2.0. `Directory.Build.props` sets `LangVersion` to `latest` and enables implicit usings.

The toolkit includes ASP.NET Core minimal APIs, RazorSlices dashboards, EF Core with multiple providers, Mapster, Serilog, Quartz, and FluentValidation. Do not assume SQL Server is the only persistence option.

## Skills

- Check `.agents/skills/` before starting a task. Read and follow the `SKILL.md` for each matching skill.
- Use matching skills before a manual workflow. Use a manual approach only when no available skill covers the task.
- Use [find-skills](.agents/skills/find-skills/SKILL.md) when unsure which skill applies.
- Use [dashboard-pages](.agents/skills/dashboard-pages/SKILL.md) for dashboard changes.
- Use [git-commit](.agents/skills/git-commit/SKILL.md) when asked to commit or run `/commit`.
- Use [technical-writing](.agents/skills/technical-writing/SKILL.md) for technical documentation and [unslop](.agents/skills/unslop/SKILL.md) for writing.

## Architecture

- Keep dependencies pointing inward and preserve module isolation. The layer rules describe application design; toolkit projects also reference shared `Common.*` packages.
- Domain contains aggregates, entities, value objects, enumerations, events, and business rules. Do not introduce dependencies on Application, Infrastructure, or Presentation.
- Application contains commands, queries, handlers, DTOs, specifications, and service abstractions. Depend on Domain and shared abstractions, not Infrastructure or Presentation.
- Infrastructure may reference Domain and Application and implements their abstractions. It contains repositories, EF Core contexts and configurations, external integrations, and technical jobs and startup tasks.
- Presentation contains endpoints, dashboards, module registration, and boundary mapping. Reference Application and Domain types as needed for mapping. Route use cases through Application services or requester/notifier.
- Hosts compose modules, providers, authentication, Serilog logging, request correlation, problem details, and OpenAPI middleware.
- Prefer repositories and specifications over direct `DbContext` access in application use cases. Follow the existing feature's design rather than introducing a parallel abstraction.

## Coding standards

- Follow `.editorconfig` and the target project's settings. Use file-scoped namespaces, four-space C# indentation, and `var` as configured.
- Use PascalCase for types and public members, camelCase for locals and private instance fields, and `I` prefixes for interfaces. Do not prefix private fields with underscores.
- Use PascalCase for private constants and private static readonly fields, as configured in `.editorconfig`.
- Use `this.` qualification for instance fields, properties, methods, and events as configured.
- Separate public members, including implicit public interface members, with one blank line. Place the blank line before the following member's XML documentation and attributes.
- Use descriptive names and modern C# features where they improve clarity. Prefer records for suitable data models, pattern matching for branching, and null-conditional and null-coalescing operators where appropriate.
- Preserve the target project's nullability settings. Most projects leave nullable annotations disabled, but some enable them.
- Prefer `Result` or `Result<T>` for recoverable failures. Use FluentValidation for inputs and exceptions for exceptional failures.
- Use asynchronous I/O and propagate cancellation where supported. Avoid N+1 queries and unnecessary materialization.
- Document public types, methods, and properties with XML comments. Include usage examples for public-facing symbols.
- Prefer BCL functionality and existing dependencies. Manage NuGet versions centrally in `Directory.Packages.props`.

## Build and test

Prefer the tasks in [.vscode/tasks.json](.vscode/tasks.json):

| Task | Scope |
| --- | --- |
| `Solution - restore` | Restore `bITdevKit.slnx` |
| `Solution - build` | Build `bITdevKit.slnx` |
| `Solution - tests (unit)` | Run matching unit test projects under `tests/` sequentially |
| `Solution - tests (integration)` | Run matching integration test projects under `tests/` sequentially |
| `DoFiesta - watch` | Run DoFiesta with hot reload |
| `WeatherFiesta - watch` | Run WeatherFiesta with hot reload |

- From the repository root, `dotnet build` resolves the solution automatically.
- Run top-level builds and test commands sequentially in the same worktree. Parallel runs can cause misleading `obj/ref` failures or missing metadata errors.
- The solution test tasks exclude project names matching `EventSourcing|WeatherForecast`. They do not discover tests under `examples/` or projects named `*.Tests`. Run relevant excluded projects explicitly.
- Use xUnit, Shouldly, and NSubstitute. Follow Arrange-Act-Assert and name the instance under test `sut`.
- Test observable behavior in handlers, domain logic, rules, and mapping. Use integration tests for endpoints and persistence, with `WebApplicationFactory` or Testcontainers where appropriate.
- Check integration test fixtures for Docker and external service requirements before running them.
- Run checks appropriate to the change and report the commands and results. Keep formatting changes limited to touched files.

## Local browser testing

For DoFiesta browser testing, use `DoFiesta - watch` or run this command from the repository root. Quote the URLs so PowerShell passes the semicolon as part of the argument:

```powershell
dotnet run --project examples/DoFiesta/DoFiesta.Presentation.Web.Server/DoFiesta.Presentation.Web.Server.csproj --nologo --urls "https://localhost:5001;http://localhost:5000"
```

- Start DoFiesta with `dotnet run` or `dotnet watch`, rather than its built server DLL. Direct DLL launches can miss development static web assets and cause blank pages with HTTP 500 responses for `/_framework/blazor.web.js` or `/_content/MudBlazor/MudBlazor.min.js`.
- In a linked Git worktree, DoFiesta and WeatherFiesta run as a development instance: the worktree's slot replaces `--urls`, the catalog names and the cookie names. Read the actual URL from the `[BDK] worktree` startup line or from `bdk dev status --json` (`dotnet run --project src/Presentation.Cli/Presentation.Cli.csproj -- dev status`). Stop an instance with `bdk dev stop --app dofiesta`, drop its catalogs with `bdk dev reset --app dofiesta --confirm-instance dofiesta:wNN`, and run `bdk dev prune` from any checkout after deleting worktrees.
- Both DoFiesta and WeatherFiesta configure database deletion and recreation when `IsLocalDevelopment()` is true. Create the sample data needed for each browser test.
- For the repo-local Playwright helper, run `npm install` and `npx playwright install chromium` inside `.agents/skills/playwright-skill/` when dependencies are missing.
- Pass an absolute script path to `node .agents/skills/playwright-skill/run.js <script>`. The helper changes its working directory before resolving inputs.
- For local HTTPS, use Playwright's `ignoreHTTPSErrors: true` and PowerShell's `Invoke-WebRequest -SkipCertificateCheck` for readiness probes.
- If `CS2012` reports locked files, identify and stop the relevant sample app process before rebuilding. Stop processes started for verification when finished and check that their ports are released.

## Logging

- Log through `ILogger<T>` with Serilog configured by the host.
- Use structured message templates. Follow the feature's existing log key convention, for example:

```csharp
logger.LogInformation("[{LogKey}] customer created (customerId={CustomerId})", logKey, customer.Id);
```

- Use Information for normal operations, Warning for recoverable issues, and Error for failures.
- Do not log secrets or sensitive personal data.

## Documentation

- Write Markdown under `docs/` and consult the relevant feature guide and ADR before changing behavior.
- Use fictional, self-contained domain samples in guides, specifications, package READMEs, and documentation captures. Do not name, link to, or show repository example applications in those documents.
- Use real DevKit APIs with fictional models and routes. Keep application-specific execution evidence outside the documentation tree.

## Guidance for agent prompts

When writing an implementation prompt, include:

- The target feature, such as `StartupTasks` or `JobScheduling`.
- The layers in scope: Domain, Application, Infrastructure, or Presentation.
- The feature requirements and expected behavior.
- Existing DevKit features to reuse, including abstractions, extensions, utilities, pipeline behaviors, results, and rules.

## Git and pull requests

- Inspect `git status --porcelain` and preserve unrelated working-tree changes.
- Use focused branches such as `feature/<area>-<description>`, `fix/<issue>`, or `chore/<task>`. Follow existing folder structure and naming conventions. Keep each PR and commit limited to one logical change.
- When asked to commit, follow the git-commit skill. Inspect `git diff --staged` first, or `git diff` if nothing is staged.
- Use `<type>[optional scope]: <description>` with an imperative description and a subject under 50 characters. Include a scope when relevant and reference relevant issues with `#`.
- Use Conventional Commit types such as `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, and `ci`. For example, `fix(api): correct repository filtering`.
- Include meaningful tests and documentation updates where applicable.
- Never commit secrets. Do not change Git configuration, skip hooks, force push, or run destructive Git commands without explicit authorization.
