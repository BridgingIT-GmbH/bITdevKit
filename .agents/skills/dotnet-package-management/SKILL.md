---
name: dotnet-package-management
description: Manage NuGet packages using Central Package Management (CPM), dotnet CLI, and dotnet-outdated (command `dotnet outdated`) to inspect/update dependencies and diagnose restore issues. Never edit XML directly—prefer dotnet commands and dotnet-outdated.
---

# NuGet Package Management

## When to Use This Skill

Use this skill when:
- Adding, removing, or updating NuGet packages
- Setting up Central Package Management (CPM) for a solution
- Managing package versions across multiple projects
- Troubleshooting package conflicts, restore issues, or dependency drift
- Auditing outdated dependencies and applying safe upgrades with `dotnet outdated`

---

## Golden Rule: Never Edit XML Directly

**Always use `dotnet` CLI commands (and `dotnet outdated` where appropriate) to manage packages.** Never manually edit `.csproj` or `Directory.Packages.props`.

```bash
# DO: Use CLI commands
dotnet add package Newtonsoft.Json
dotnet remove package Newtonsoft.Json
dotnet list package --outdated

# DON'T: Edit XML directly
# <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

Why:
- CLI validates package exists and resolves versions
- Handles transitive dependencies correctly
- Updates lock files if present
- Avoids typos/malformed XML
- Works correctly with CPM

---

## Central Package Management (CPM)

CPM centralizes package versions in one file, eliminating version conflicts across projects.

### Enable CPM

Create `Directory.Packages.props` in solution root:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageVersion Include="Serilog" Version="4.0.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
  </ItemGroup>
</Project>
```

### Project Files with CPM

Projects reference packages **without versions**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
    <PackageReference Include="Serilog" />
  </ItemGroup>
</Project>
```

### Adding Packages with CPM

```bash
# Adds to Directory.Packages.props AND project file
dotnet add package Serilog.Sinks.Console
```

---

## dotnet-outdated (command: `dotnet outdated`)

`dotnet-outdated` is a .NET tool that:
- Finds outdated NuGet dependencies across a solution/project
- Can upgrade packages (optionally automatically)
- Helps surface dependency constraints that block upgrades
- Works well alongside CPM to keep versions consistent

### Install

Prefer a local tool manifest (reproducible), otherwise install globally.

```bash
# Local tool (recommended)
dotnet new tool-manifest
dotnet tool install dotnet-outdated-tool

# OR global tool
dotnet tool install --global dotnet-outdated-tool
```

Verify:

```bash
dotnet outdated --version
```

### Common Usage

Scan solution/project for outdated dependencies:

```bash
dotnet outdated
```

Scan a specific project:

```bash
dotnet outdated src/MyApp/MyApp.csproj
```

Include transitive dependencies (useful for “why can’t I upgrade X?” investigations):

```bash
dotnet outdated --include-transitive
```

Fail the build if outdated packages are found (CI gate):

```bash
dotnet outdated --fail-on-updates
```

Upgrade packages (interactive / apply upgrades):

```bash
# Upgrade (you can add additional flags as desired)
dotnet outdated --upgrade
```

> Notes for CPM:
> - With CPM enabled, upgrades should primarily flow through `Directory.Packages.props` (central versions).
> - Use `dotnet outdated` to *identify* and *apply* upgrades; then run `dotnet restore` and build/tests.
> - If you see upgrades being proposed at per-project level while using CPM, treat that as a signal to ensure the package is centrally versioned (and avoid manual XML edits).

### Troubleshooting with dotnet-outdated

When upgrades are blocked:
- Run with transitive included to see dependency chains:

```bash
dotnet outdated --include-transitive
```

Then:
- Check which package(s) constrain the version (often a direct dependency pins a lower range).
- Consider upgrading the constraining package first (or aligning versions via CPM).

---

## When NOT to Use CPM

Central Package Management isn't always the right choice:

### Legacy Projects
Migration can surface many conflicts at once. Consider incremental migration.

### Version Ranges
CPM expects exact versions (ranges aren’t supported in central management).

### Older Tooling
CPM requires newer SDK/NuGet/VS. If your team tooling is older, CPM can break builds.

### Multi-Repo Solutions
Each repo needs its own `Directory.Packages.props`.

---

## CLI Command Reference

### Adding Packages

```bash
dotnet add package Serilog
dotnet add package Serilog --version 4.0.0
dotnet add src/MyApp/MyApp.csproj package Serilog
```

### Removing Packages

```bash
dotnet remove package Serilog
dotnet remove src/MyApp/MyApp.csproj package Serilog
```

### Listing Packages

```bash
dotnet list package
dotnet list package --outdated
dotnet list package --include-transitive
dotnet list package --vulnerable
dotnet list package --deprecated
```

### Updating Packages

```bash
# Recommended: use dotnet-outdated
dotnet outdated
dotnet outdated --upgrade

# After upgrade
dotnet restore
```

### Restore and Clean

```bash
dotnet restore
dotnet nuget locals all --clear
dotnet restore --force
```

---

## Package Sources

```bash
dotnet nuget list source
```

```bash
dotnet nuget add source https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json \
  --name MyFeed \
  --username az \
  --password $PAT \
  --store-password-in-clear-text
```

---

## Troubleshooting

### Version Conflicts / Why is package X present?

```bash
dotnet list package --include-transitive
dotnet outdated --include-transitive
```

### Restore Failures

```bash
dotnet nuget locals all --clear
dotnet restore --verbosity detailed
```

### Lock Files (Reproducible Builds)

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
```

Commit `packages.lock.json` files.

---

## Anti-Patterns

- Editing XML directly
- Inline versions when using CPM
- Mixing version management (some centralized, some inline)
- Letting related packages drift to different versions (use shared version variables)

---

## Quick Reference

| Task | Command |
|------|---------|
| Add package | `dotnet add package <name>` |
| Remove package | `dotnet remove package <name>` |
| List packages | `dotnet list package` |
| Outdated (built-in view) | `dotnet list package --outdated` |
| Outdated (richer tooling) | `dotnet outdated` |
| Upgrade deps | `dotnet outdated --upgrade` |
| CI gate on updates | `dotnet outdated --fail-on-updates` |
| Restore | `dotnet restore` |
| Clear cache | `dotnet nuget locals all --clear` |

---

## Resources

- Central Package Management: https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
- dotnet CLI reference: https://learn.microsoft.com/en-us/dotnet/core/tools/
- NuGet.config reference: https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file
- dotnet-outdated README: https://github.com/dotnet-outdated/dotnet-outdated/blob/master/README.md