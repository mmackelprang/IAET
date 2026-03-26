# Iaet.Cli

`Iaet.Cli` is the `dotnet` global tool entry point for IAET. It wires together the DI container and exposes all toolkit functionality through a [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) command tree.

## Commands

| Command | Description |
|---|---|
| `iaet capture start` | Launch a Playwright browser, record HTTP traffic, persist to catalog |
| `iaet catalog sessions` | List all capture sessions with request counts |
| `iaet catalog endpoints` | List deduplicated endpoints observed in a session |

Each command handler receives its dependencies through the DI `IServiceProvider` and creates a scoped service lifetime per invocation.

## DI Host Builder

`Program.cs` builds a `Microsoft.Extensions.Hosting` host with:

- **Serilog** — console + rolling-file sink (writes to `logs/iaet-<date>.log`)
- **`AddIaetCapture()`** — registers `ICaptureSessionFactory` and Playwright services
- **`AddIaetCatalog(connectionString)`** — registers `CatalogDbContext` and `IEndpointCatalog`
- **Configuration** — `Iaet:DatabasePath` setting (default: `catalog.db` in the working directory)

## Tool Installation

Pack and install locally during development:

```bash
pwsh scripts/build.ps1 -Target pack
dotnet tool install -g iaet --add-source artifacts/
```

Install from NuGet (once published):

```bash
dotnet tool install -g iaet
```

The package name and tool command are both `iaet`. The tool targets `net10.0` and ships a self-contained executable on supported platforms.
