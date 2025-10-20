# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotNet-Soddi is a .NET global tool for downloading and importing Stack Overflow data dumps into SQL Server databases. It's packaged as `dotnet-soddi` and installed via `dotnet tool install --global dotnet-soddi`.

Target framework: .NET 7.0

## Building and Testing

### Build the project
```bash
dotnet build
```

### Run tests
```bash
dotnet test
```

### Package the tool locally
The project is configured with `GeneratePackageOnBuild=true`, so building automatically creates a NuGet package in `src/Soddi/nupkg/`.

### Install the tool locally for testing
```bash
dotnet pack src/Soddi/Soddi.csproj
dotnet tool install --global --add-source src/Soddi/nupkg dotnet-soddi
```

### Uninstall and reinstall during development
```bash
dotnet tool uninstall --global dotnet-soddi
dotnet tool install --global --add-source src/Soddi/nupkg dotnet-soddi
```

Or use the provided script:
```bash
.\removeAndAddFromDotNetTools.cmd
```

## Architecture

### Command Structure
The application uses Spectre.Console.Cli for command-line parsing. Each command follows a pattern:
- **Options class**: Defines command arguments and options (e.g., `ImportOptions`, `DownloadOptions`)
- **Handler class**: Implements `AsyncCommand<TOptions>` and contains the command logic (e.g., `ImportHandler`, `DownloadHandler`)

Commands are registered in `Program.cs` using `config.AddCommandWithExample<THandler>()`.

### Key Components

#### Services Layer (`src/Soddi/Services/`)
- **ProcessorFactory**: Creates the appropriate `IArchivedDataProcessor` based on input (single .7z file, folder of .7z files, or folder of .xml files)
- **IArchivedDataProcessor**: Interface for reading data from various archive formats. Implementations:
  - `ParallelArchiveProcessor`: Processes .7z archives using concurrent streams
  - `SequentialArchiveProcessor`: Processes .7z archives sequentially (fallback for compatibility)
  - `FolderProcessor`: Processes extracted XML files directly
- **XmlToDataReader**: Streams XML data and converts it to `IDataReader` for SQL bulk insert
- **SqlServerBulkInserter**: Handles bulk insert operations using `SqlBulkCopy`
- **DatabaseHelper**: SQL Server database operations (create, drop, connection management)
- **ArchiveDownloader**: Downloads archives via HTTP from archive.org
- **AvailableArchiveParser**: Parses the list of available Stack Exchange sites

#### Tasks Layer (`src/Soddi/Tasks/SqlServer/`)
SQL Server import tasks executed in sequence:
1. **CreateDatabase**: Creates database if `--dropAndCreate` is specified
2. **CreateSchema**: Creates tables and indexes
3. **InsertData**: Bulk inserts data from XML files
4. **InsertTypeValues**: Populates lookup tables
5. **AddConstraints**: Adds primary keys and unique constraints (unless `--skipConstraints`)
6. **AddForeignKeys**: Adds foreign key relationships
7. **CheckCounts**: Validates row counts match XML input

Each task implements `ITask` interface.

#### TableTypes (`src/Soddi/TableTypes/`)
Classes representing Stack Overflow data tables (Badges, Comments, Posts, Users, Votes, etc.). Each class is decorated with `[StackOverflowDataTable("filename")]` attribute to map to XML files.

#### Progress Bars (`src/Soddi/ProgressBar/`)
Custom Spectre.Console progress bar components for download and import visualization.

### Data Flow

1. User runs `soddi import <path>` command
2. `ProcessorFactory` examines the path and creates appropriate processor
3. Processor streams data from .7z archives or XML files
4. `XmlToDataReader` converts XML streams to `IDataReader` format
5. `SqlServerBulkInserter` bulk inserts data into SQL Server
6. Post-processing tasks add constraints and validate counts

### Dependency Injection

The application uses Microsoft.Extensions.DependencyInjection with Scrutor for assembly scanning:
```csharp
var container = new ServiceCollection()
    .AddSingleton<IFileSystem>(new FileSystem())
    .Scan(scan => scan.FromCallingAssembly().AddClasses());
```

All services are registered as singletons and automatically discovered via Scrutor's assembly scanning.

### Testing

Tests use:
- **xUnit** for test framework
- **Shouldly** for assertions
- **System.IO.Abstractions.TestingHelpers** for file system mocking

Test files include sample .7z and XML files in `tests/Soddi.Tests/test-files/`.

## Key Libraries

- **Spectre.Console/Spectre.Console.Cli**: CLI framework and rich terminal output
- **SharpCompress**: 7z archive reading
- **Microsoft.Data.SqlClient**: SQL Server connectivity
- **MonoTorrent**: BitTorrent protocol implementation
- **System.IO.Abstractions**: File system abstraction for testability
- **Polly**: Retry policies for resilient SQL operations
- **Humanizer.Core**: Human-friendly string formatting

## Versioning and Publishing

- Version is managed by **MinVer** based on Git tags
- CI/CD uses **dotnet-releaser** to build, pack, and publish to NuGet on tagged commits
- GitHub Actions workflow: `.github/workflows/dotnet.yml`
  - Runs `dotnet test -c Debug` on all pushes/PRs
  - Publishes to NuGet when a tag is pushed

## Connection String Defaults

Default connection string: `Server=.;Integrated Security=true;TrustServerCertificate=True`

The `TrustServerCertificate=True` is required for local SQL Server instances with self-signed certificates.
