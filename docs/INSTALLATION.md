# Installation and Dependency Guide

This guide lists **everything** you need to install, restore or pull to build and run this lab, and it
matches the source code in this repository exactly. It assumes you are starting from a machine with
no development tools installed.

The lab is an ASP.NET Core 8 minimal Web API (`MyApi`) that stores products in **SQL Server 2022**
through **Entity Framework Core 8**, caches single-product reads in **Redis 7**, and is served to you
through an **Nginx** reverse proxy. Docker Compose runs all four pieces. An xUnit test project
(`MyApi.Tests`) tests the business logic without needing Docker.

```text
browser / curl ──► nginx:1.30-alpine (host port 8080)
                        │  proxy_pass http://api:8080
                        ▼
                  api  (MyApi, aspnet:8.0 runtime image)
                    │                        │
       EF Core 8 ───┘                        └─── StackExchange.Redis
                    ▼                        ▼
   mssql/server:2022-CU27-ubuntu-22.04    redis:7.4-alpine
```

> **Important – .NET 8 support ends on 10 November 2026.** .NET 8 is an LTS release and the lab
> deliberately targets it, but Microsoft stops shipping security fixes for it on that date (less than a
> month after this guide was written, on 17 October 2026). It is fine for learning. For anything that will run after that date, plan to
> move to .NET 10 (LTS, supported to November 2028) and the matching EF Core 10 packages.

---

## Contents

1. [Required and optional software](#1-required-and-optional-software)
2. [NuGet packages](#2-nuget-packages)
3. [Package installation commands](#3-package-installation-commands)
4. [Docker images](#4-docker-images)
5. [Project creation, restore, build and test](#5-project-creation-restore-build-and-test)
6. [Package configuration files](#6-package-configuration-files)
7. [Verify the installation and troubleshooting](#7-verify-the-installation-and-troubleshooting)
8. [Final dependency checklist](#8-final-dependency-checklist)
9. [What was verified, and how](#9-what-was-verified-and-how)

---

## 1. Required and optional software

### 1.1 Required

| Software | Version | Why you need it | Official download |
|---|---|---|---|
| **.NET 8 SDK** | Any 8.0.x SDK ≥ 8.0.100. Latest at the time of writing: **SDK 8.0.426** (runtime 8.0.32, released 13 Oct 2026) | Compiles the API, restores NuGet packages, runs `dotnet test`, runs the `dotnet-ef` tool. `global.json` in this repo selects an 8.0 SDK even if you also have .NET 9/10 installed. | <https://dotnet.microsoft.com/download/dotnet/8.0> |
| **Docker** with **Docker Compose v2 or later** | Docker Desktop (Windows/macOS/Linux) **or** Docker Engine + Compose plugin (Linux). Tested with Docker Engine 29.8.2 and Compose v5.6.0. | Builds the API image and runs SQL Server, Redis, Nginx and the API together. The `docker compose` (space, not hyphen) command is required; the old Python `docker-compose` v1 is not supported. | Docker Desktop: <https://www.docker.com/products/docker-desktop/><br>Docker Engine (Linux): <https://docs.docker.com/engine/install/><br>Compose plugin (Linux): <https://docs.docker.com/compose/install/linux/> |
| **Git** | Any current release | Version control, and cloning this repository. | <https://git-scm.com/downloads> |
| **A terminal** | – | Runs every command in this guide. **Windows:** PowerShell (built in) inside Windows Terminal (built into Windows 11). **macOS:** Terminal (built in). **Linux:** any shell (bash/zsh). | Windows Terminal: <https://aka.ms/terminal> (only if it is missing) |
| **curl** | – | Sends test requests to the API. Built into Windows 10/11 (call it as `curl.exe` in PowerShell), macOS and most Linux distributions. | Normally already installed |

**Docker platform and licensing notes**

* **Windows:** Docker Desktop needs the **WSL 2** backend and must be in **Linux containers** mode (the
  default). Hardware virtualisation must be enabled in the BIOS/UEFI.
* **SQL Server image is `linux/amd64` only.** On Apple Silicon Macs, Docker Desktop runs it under
  emulation. Turn on *Settings → General → "Use Rosetta for x86_64/amd64 emulation on Apple Silicon"*.
  The compose file sets `platform: linux/amd64` for this reason. ARM64 Linux hosts (for example a
  Raspberry Pi) cannot run the SQL Server container.
* **Memory:** SQL Server needs at least 2 GB of RAM inside the container. Give Docker Desktop at least
  4 GB (*Settings → Resources*).
* **Docker Desktop licence:** free for personal use, education, non-commercial open source and small
  businesses (fewer than 250 employees **and** less than US$10 million annual revenue). Larger
  organisations need a paid subscription: <https://www.docker.com/pricing/>. Docker Engine on Linux is
  free.

### 1.2 Optional (helpful for development, not needed to run the lab)

| Software | Why it is useful | Official download |
|---|---|---|
| **Visual Studio Code** + **C# Dev Kit** extension | Cross-platform editor with C# IntelliSense, debugging and a Test Explorer for `MyApi.Tests`. Recommended editor for this lab on macOS and Linux. | VS Code: <https://code.visualstudio.com/download><br>C# Dev Kit: <https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit> |
| **Visual Studio 2022** (17.8 or later; 17.14 is the current servicing baseline) – Windows only | Full IDE. Supports .NET 8 projects. Microsoft's download page now offers **Visual Studio 2026** first, which also opens this solution (the `global.json` file keeps it on the .NET 8 SDK). Community edition is free for individuals, students and open-source work. | <https://visualstudio.microsoft.com/downloads/> |
| **MSSQL extension for VS Code** | Cross-platform way to browse the `MyApiDb` database and run queries. Microsoft's recommended replacement for Azure Data Studio. | <https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql> |
| **SQL Server Management Studio (SSMS) 22** – Windows only | Full SQL Server management tool. SSMS 22 installs through a small `vs_SSMS.exe` stub that opens the Visual Studio Installer. | <https://learn.microsoft.com/ssms/install/install> |

Do **not** install **Azure Data Studio**: Microsoft retired it, and support ended on 28 February 2026.
Use the VS Code MSSQL extension or SSMS instead.

To connect a database tool to the lab's SQL Server, while `docker compose up` is running, use:
server `localhost,1433`, SQL authentication, user `sa`, the password from your `.env` file, and
**Trust server certificate** enabled. The port is bound to `127.0.0.1` only.

### 1.3 Not required

* **No local SQL Server or Redis install.** Both run in containers.
* **No IIS, IIS Express or Nginx install.** Nginx runs in a container; the API runs on Kestrel.
* **No `Microsoft.EntityFrameworkCore.Tools` package.** See [§2.3](#23-packages-evaluated-and-deliberately-not-used).

---

## 2. NuGet packages

Versions are pinned directly in each `.csproj` file (no central package management). EF Core packages
all use the same version, **8.0.32**, the latest 8.0 patch at the time of writing.

### 2.1 `MyApi` (the Web API project)

| Package ID | Version | Purpose | Required? |
|---|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | **8.0.32** | EF Core provider for SQL Server: `UseSqlServer`, `EnableRetryOnFailure`, migrations at startup. Brings in EF Core 8.0.32 and `Microsoft.Data.SqlClient` 5.1.9 as transitive dependencies. | Required |
| `Microsoft.EntityFrameworkCore.Design` | **8.0.32** | Design-time services used by the `dotnet-ef` tool to create migrations. Marked `PrivateAssets="all"`, so it is **not** published with the app. | Required to create or change migrations (the repo already contains `InitialCreate`) |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | **8.0.32** | Provides `AddDbContextCheck<AppDbContext>()`, the SQL Server readiness check behind `/health/ready`. | Required |
| `StackExchange.Redis` | **2.13.17** | Redis client: `IConnectionMultiplexer` for the product cache and the Redis health check. Its `net8.0` build depends only on .NET 8-era packages. | Required |

### 2.2 `MyApi.Tests` (the xUnit test project)

| Package ID | Version | Purpose | Required? |
|---|---|---|---|
| `Microsoft.NET.Test.Sdk` | **18.11.0** | MSBuild targets and test host that let `dotnet test` run tests. Ships a `net8.0` build. | Required for tests |
| `xunit` | **2.9.3** | Test framework (`[Fact]`, `[Theory]`, `Assert`). | Required for tests |
| `xunit.runner.visualstudio` | **3.1.5** | Adapter that lets `dotnet test`, Visual Studio and VS Code discover and run xUnit tests. Ships a `net8.0` build. | Required for tests |
| `Microsoft.EntityFrameworkCore.InMemory` | **8.0.32** | In-memory EF Core database so `ProductService` can be tested without SQL Server. Same version as the other EF packages. | Required for tests |

The test project also references `MyApi` with a project reference, so it sees the SQL Server and
Redis packages transitively. It does not reference them directly.

### 2.3 Packages evaluated and deliberately **not** used

| Package | Decision |
|---|---|
| `Microsoft.EntityFrameworkCore.Tools` | **Not needed.** It only adds PowerShell commands (`Add-Migration`, `Update-Database`) to Visual Studio's Package Manager Console. This lab uses the cross-platform `dotnet-ef` CLI tool instead ([§3.3](#33-ef-core-command-line-tool-dotnet-ef)). If you prefer the Package Manager Console, add it to `MyApi` at version 8.0.32. |
| `Microsoft.AspNetCore.Diagnostics.HealthChecks` | **Do not install.** Health check middleware (`MapHealthChecks`, `HealthCheckOptions`) has been part of the ASP.NET Core shared framework since 3.0. The NuGet package is an obsolete 2.2-era package. |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | **Do not install.** `AddHealthChecks()`, `IHealthCheck` and `HealthCheckResult` are in the shared framework (used by `RedisHealthCheck.cs`). |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | **Not used.** It wraps StackExchange.Redis behind `IDistributedCache`. The lab uses `IConnectionMultiplexer` directly, for the cache and for the Redis ping health check. Adding both would duplicate the Redis client. |
| `AspNetCore.HealthChecks.Redis` / `AspNetCore.HealthChecks.SqlServer` | **Not used.** These are third-party packages. The Redis check is a 20-line built-in `IHealthCheck`, and SQL Server is covered by the official EF Core check. |
| `StackExchange.Redis` 3.x | **Not used.** 3.x is a new major version whose `net8.0` dependency group pulls in `Microsoft.Extensions.Logging.Abstractions` 10.0.x and `System.IO.Pipelines` 10.0.x. The latest 2.x release (2.13.17) keeps the dependency graph on .NET 8 packages. |
| `Swashbuckle.AspNetCore` / `Microsoft.AspNetCore.OpenApi` | **Not used.** The project is created with `--no-openapi`, so the template does not add them. |
| `Microsoft.AspNetCore.Mvc.Testing` | **Not used.** The tests target the service and validation layers directly. |
| `coverlet.collector` | **Optional, not included.** Code coverage collector. If you want coverage, run `dotnet add MyApi.Tests/MyApi.Tests.csproj package coverlet.collector --version 6.0.4` from the repository root, then `dotnet test --collect:"XPlat Code Coverage"`. This was verified to work with this solution. |
| `xunit.v3` | **Not used.** xUnit v2 (`xunit` 2.9.3) is still maintained and is what the .NET 8 `xunit` template uses. |

### 2.4 Built into .NET 8 – no package needed

Kestrel; minimal APIs (`MapGet`, `MapGroup`, `Results.*`); configuration and environment-variable
binding (`ConnectionStrings__DefaultConnection`); logging; dependency injection; health checks and
their middleware; ProblemDetails and `Results.ValidationProblem`; forwarded-headers support
(`ASPNETCORE_FORWARDEDHEADERS_ENABLED`); `System.Text.Json`. All of these come from the
`Microsoft.AspNetCore.App` shared framework, which the `Microsoft.NET.Sdk.Web` project SDK references
automatically.

---

## 3. Package installation commands

Every command block names its working directory. "Repository root" means the folder that contains
`MyApi.sln`, `global.json` and `docker-compose.yml`.

### 3.1 If you cloned this repository: verify, don't reinstall

The packages are already listed in the `.csproj` files, so **do not run `dotnet add package`
again**. Restore them and confirm the versions:

**Working directory: repository root**

```bash
dotnet restore
dotnet list package
```

Expected output (abridged):

```text
Project 'MyApi' has the following package references
   [net8.0]:
   Top-level Package                                                        Requested   Resolved
   > Microsoft.EntityFrameworkCore.Design                                   8.0.32      8.0.32
   > Microsoft.EntityFrameworkCore.SqlServer                                8.0.32      8.0.32
   > Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore      8.0.32      8.0.32
   > StackExchange.Redis                                                    2.13.17     2.13.17

Project 'MyApi.Tests' has the following package references
   [net8.0]:
   Top-level Package                             Requested   Resolved
   > Microsoft.EntityFrameworkCore.InMemory      8.0.32      8.0.32
   > Microsoft.NET.Test.Sdk                      18.11.0     18.11.0
   > xunit                                       2.9.3       2.9.3
   > xunit.runner.visualstudio                   3.1.5       3.1.5
```

### 3.2 If you are creating the projects yourself (from scratch)

Run these **after** creating the projects in [§5.2](#52-path-b--create-everything-from-scratch).

**Working directory: repository root**

```bash
# MyApi – Web API project
dotnet add MyApi/MyApi.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.32
dotnet add MyApi/MyApi.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.32
dotnet add MyApi/MyApi.csproj package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 8.0.32
dotnet add MyApi/MyApi.csproj package StackExchange.Redis --version 2.13.17

# MyApi.Tests – test project. The xunit template already adds the first three packages with
# older versions; these commands update them to the pinned versions.
dotnet add MyApi.Tests/MyApi.Tests.csproj package Microsoft.NET.Test.Sdk --version 18.11.0
dotnet add MyApi.Tests/MyApi.Tests.csproj package xunit --version 2.9.3
dotnet add MyApi.Tests/MyApi.Tests.csproj package xunit.runner.visualstudio --version 3.1.5
dotnet add MyApi.Tests/MyApi.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 8.0.32

# The xunit template also adds coverlet.collector, which this lab does not use (see §2.3).
# If your template version did not add it, this command prints "does not contain any
# PackageReference 'coverlet.collector'" and changes nothing - that error is safe to ignore.
dotnet remove MyApi.Tests/MyApi.Tests.csproj package coverlet.collector
```

The commands work the same in PowerShell, bash and zsh. `dotnet add package` writes the
`<PackageReference>` into the `.csproj` and restores immediately. `Microsoft.EntityFrameworkCore.Design`
is added with `PrivateAssets=all` automatically.

### 3.3 EF Core command-line tool (`dotnet-ef`)

The tool is installed as a **local tool**, pinned in `.config/dotnet-tools.json`. A local tool is
tied to this repository and is version-locked to the EF Core packages, so it does not conflict with
any global `dotnet-ef` you have for other projects.

**If you cloned this repository** (the manifest already exists):

**Working directory: repository root**

```bash
dotnet tool restore
dotnet ef --version        # Entity Framework Core .NET Command-line Tools 8.0.32
```

**If you are creating everything from scratch:**

**Working directory: repository root**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.32
```

You only need `dotnet-ef` to **create or change migrations** (and optionally to apply them by hand).
When you run the stack, the API applies the migrations itself on startup
(`Database__ApplyMigrationsOnStartup=true` in `docker-compose.yml`).

Useful commands (**working directory: repository root**):

```bash
dotnet ef migrations list --project MyApi
dotnet ef migrations has-pending-model-changes --project MyApi
dotnet ef migrations add <Name> --project MyApi --output-dir Data/Migrations
```

When these commands run you will see the message *"An error occurred while accessing the
Microsoft.Extensions.Hosting services … Connection string 'DefaultConnection' is not configured"*.
This is expected and harmless: the tool then falls back to `MyApi/Data/DesignTimeDbContextFactory.cs`,
which needs no running database to create migrations.

> A global install (`dotnet tool install --global dotnet-ef --version 8.0.32`) also works, but it is
> not needed and is not used by this guide.

---

## 4. Docker images

Docker images are **not** NuGet packages. NuGet packages are .NET libraries that `dotnet restore`
downloads into your NuGet cache. Docker images are container file systems that Docker downloads from
a registry (Microsoft Container Registry `mcr.microsoft.com`, or Docker Hub).

| Image | Role | Configured in | Notes |
|---|---|---|---|
| `mcr.microsoft.com/dotnet/sdk:8.0` | **Build stage.** Restores NuGet packages and runs `dotnet publish` inside Docker. Not present in the final image. | `MyApi/Dockerfile` (`FROM … AS build`) | `8.0` is a floating tag for the newest 8.0 SDK patch (8.0.426 when this was written). MIT licence. |
| `mcr.microsoft.com/dotnet/aspnet:8.0` | **Runtime stage.** ASP.NET Core 8 runtime only, which keeps the final `myapi:lab` image small. Runs as the non-root `app` user on port 8080. | `MyApi/Dockerfile` (`FROM … AS final`) | Floating tag for the newest 8.0 runtime patch (8.0.32). MIT licence. |
| `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04` | SQL Server 2022 database (`sqlserver` service). | `docker-compose.yml` | Pinned to cumulative update 27. **`linux/amd64` only.** Runs as the free **Developer** edition (`MSSQL_PID=Developer`), which is licensed for development and testing only, not production. `ACCEPT_EULA=Y` means you accept the SQL Server licence terms. |
| `redis:7.4-alpine` | Redis 7 cache (`redis` service). | `docker-compose.yml` | Floating patch tag on the 7.4 line (7.4.11 when this was written). Redis 7.4 and later are dual-licensed RSALv2/SSPLv1, which is fine for learning and internal use. If you need an OSI open-source licence, Redis 7.2 (BSD) and Valkey are alternatives. |
| `nginx:1.30-alpine` | Reverse proxy (`nginx` service) in front of the API on host port 8080. | `docker-compose.yml`, configured by `nginx/nginx.conf` | Nginx 1.30 is the current stable branch (1.30.6 when this was written). The config uses `server … resolve`, which needs Nginx ≥ 1.27.3. BSD-2 licence. |
| `myapi:lab` | **Built locally** from `MyApi/Dockerfile`. Never pulled. | `docker-compose.yml` (`api` service, `build:` + `image:`) | – |

### 4.1 Pulling images

**You do not have to pull anything by hand.** `docker compose up --build` pulls the three service
images, and `docker build` pulls the two .NET base images, automatically when they are not already
on your machine. Pulling ahead of time is useful on slow connections or to check registry access.

**Working directory: any**

```bash
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0
docker pull mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04
docker pull redis:7.4-alpine
docker pull nginx:1.30-alpine
```

Or let Compose pull the service images. This needs a `.env` file first ([§5.3](#53-run-the-full-stack-with-docker-compose)).

**Working directory: repository root**

```bash
docker compose pull --ignore-buildable   # pulls sqlserver, redis, nginx; skips the locally built api image
docker compose build --pull              # builds myapi:lab, refreshing the two .NET base images
```

---

## 5. Project creation, restore, build and test

Choose **one** path.

### 5.1 Path A – clone this repository (recommended)

**Working directory: the folder where you keep your projects**

```bash
git clone https://github.com/dustycruz/aspnetcore-docker-compose-lab.git
cd aspnetcore-docker-compose-lab
```

**Working directory: repository root**

```bash
dotnet --version        # must print 8.0.x (global.json enforces this)
dotnet tool restore     # installs the pinned local dotnet-ef 8.0.32
dotnet restore          # restores NuGet packages for MyApi and MyApi.Tests
dotnet list package     # verify versions (see §3.1)
dotnet build --configuration Release
dotnet test
```

Expected results: `Build succeeded. 0 Warning(s) 0 Error(s)` and
`Passed! - Failed: 0, Passed: 16, Skipped: 0, Total: 16`.

Then continue with [§5.3](#53-run-the-full-stack-with-docker-compose).

### 5.2 Path B – create everything from scratch

These are the exact commands used to create this repository.

**Working directory: the folder where you keep your projects**

```bash
mkdir aspnetcore-docker-compose-lab
cd aspnetcore-docker-compose-lab
git init
```

**Working directory: repository root**

```bash
# 1. Pin the SDK to .NET 8 (any installed 8.0.x SDK >= 8.0.100 is accepted).
dotnet new globaljson --sdk-version 8.0.100 --roll-forward latestFeature

# 2. Standard .NET .gitignore (already ignores .env, bin/ and obj/).
dotnet new gitignore

# 3. Solution file.
dotnet new sln --name MyApi

# 4. ASP.NET Core Web API project (minimal APIs, no Swagger packages, HTTP only - Nginx is the front door).
dotnet new webapi --name MyApi --output MyApi --framework net8.0 --no-openapi --no-https

# 5. xUnit test project.
dotnet new xunit --name MyApi.Tests --output MyApi.Tests --framework net8.0

# 6. Add both projects to the solution.
dotnet sln MyApi.sln add MyApi/MyApi.csproj MyApi.Tests/MyApi.Tests.csproj

# 7. Let the tests reference the API project.
dotnet add MyApi.Tests/MyApi.Tests.csproj reference MyApi/MyApi.csproj
```

8. Install the NuGet packages: run every command in [§3.2](#32-if-you-are-creating-the-projects-yourself-from-scratch).
9. Install the EF Core tool: `dotnet new tool-manifest` and `dotnet tool install dotnet-ef --version 8.0.32` ([§3.3](#33-ef-core-command-line-tool-dotnet-ef)).
10. Replace both `.csproj` files with the final contents in [§6](#6-package-configuration-files). Depending on your SDK patch,
    the templates generate slightly different files; the §6 versions are authoritative.
11. Delete the template sample files: `MyApi/MyApi.http`, `MyApi.Tests/UnitTest1.cs` and, if it exists,
    `MyApi.Tests/GlobalUsings.cs` (the `Xunit` global using is in the `.csproj` instead).
12. Add the application, test and infrastructure files with the same contents as in this repository:

    | Path | What it is |
    |---|---|
    | `MyApi/Program.cs` | Service registration, migrations on startup, endpoints, health checks (replaces the template file) |
    | `MyApi/appsettings.json`, `MyApi/appsettings.Development.json` | Configuration (replace the template files) |
    | `MyApi/Properties/launchSettings.json` | Local `dotnet run` profile on <http://localhost:5003> (replaces the template file) |
    | `MyApi/Models/Product.cs` | Entity |
    | `MyApi/Contracts/ProductContracts.cs` | Request/response records |
    | `MyApi/Data/AppDbContext.cs` | EF Core model and seed data |
    | `MyApi/Data/DesignTimeDbContextFactory.cs` | Lets `dotnet-ef` create migrations without a database |
    | `MyApi/Services/IProductCache.cs`, `RedisProductCache.cs`, `ProductService.cs`, `ProductValidator.cs` | Business logic and Redis cache-aside |
    | `MyApi/HealthChecks/RedisHealthCheck.cs` | Redis ping health check |
    | `MyApi/Endpoints/ProductEndpoints.cs` | `/api/products` CRUD endpoints |
    | `MyApi/Dockerfile`, `MyApi/.dockerignore` | Multi-stage image build |
    | `MyApi.Tests/FakeProductCache.cs`, `ProductServiceTests.cs`, `ProductValidatorTests.cs` | 16 xUnit tests |
    | `docker-compose.yml`, `.env.example`, `nginx/nginx.conf` | Container orchestration and reverse proxy |

13. Create the initial migration, unless you copied `MyApi/Data/Migrations/` from this repository:

    **Working directory: repository root**

    ```bash
    dotnet ef migrations add InitialCreate --project MyApi --output-dir Data/Migrations
    ```

14. Restore, verify, build and test:

    **Working directory: repository root**

    ```bash
    dotnet restore
    dotnet list package
    dotnet build --configuration Release
    dotnet test
    ```

### 5.3 Run the full stack with Docker Compose

**Working directory: repository root**

Create your `.env` file from the template and **change the password**:

```bash
# bash / zsh (macOS, Linux, WSL)
cp .env.example .env
```

```powershell
# PowerShell (Windows)
Copy-Item .env.example .env
```

Then start everything:

```bash
docker compose up --build -d --wait
docker compose ps
```

All four services should show `(healthy)`. The first start takes longer, because Docker downloads
about 1 GB of images and SQL Server initialises its system databases.

Try the API through Nginx (in PowerShell type `curl.exe` instead of `curl`):

```bash
curl http://localhost:8080/
curl http://localhost:8080/health/ready
curl http://localhost:8080/api/products
curl -i http://localhost:8080/api/products/1      # X-Cache: MISS (read from SQL Server)
curl -i http://localhost:8080/api/products/1      # X-Cache: HIT  (read from Redis)
curl -X POST http://localhost:8080/api/products -H "Content-Type: application/json" -d "{\"name\":\"Webcam\",\"description\":\"1080p\",\"price\":49.99}"
```

Stop the stack with `docker compose down`. Add `-v` to also delete the SQL Server and Redis data
volumes.

### 5.4 Optional – run the API outside Docker (Visual Studio F5 / `dotnet run`)

Run the databases in Docker and the API on your machine. This is useful for debugging in an IDE.
Without this setup, pressing F5 stops with *"Connection string 'DefaultConnection' is not
configured"*: only `docker-compose.yml` supplies the connection string, and it does so only to the
container.

**Working directory: repository root**

1. Start SQL Server and Redis (if the full stack is already running, they are too):

   ```bash
   docker compose up -d --wait sqlserver redis
   ```

2. Store the connection string once, using .NET's built-in **user secrets**. Secrets are kept in your
   user profile, outside the repository, so the password is never committed. No package is needed:
   `MyApi.csproj` already contains a `UserSecretsId`. Replace the password with the one in your `.env`;
   the command is the same in Command Prompt, PowerShell and bash:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=MyApiDb;User Id=sa;Password=<your .env password>;Encrypt=True;TrustServerCertificate=True" --project MyApi
   dotnet user-secrets list --project MyApi
   ```

3. Run the API: press **F5** in Visual Studio (profile **http**), or run:

   ```bash
   dotnet run --project MyApi --launch-profile http
   ```

The API listens on <http://localhost:5003>. In Development it reads the user secret, reads Redis from
`appsettings.Development.json` (`localhost:6379`), and applies migrations on startup. User secrets
are only loaded in the Development environment, so the Docker container (Production) is unaffected.

---

## 6. Package configuration files

**Approach:** versions are pinned in each project's `.csproj` file. This repository has **no**
`Directory.Packages.props` (central package management would only add a file for two projects) and
**no** `nuget.config` (the default nuget.org source is used). The local tool manifest
`.config/dotnet-tools.json` **is** used to pin `dotnet-ef`.

### 6.1 `MyApi/MyApi.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>52113af6-5ecc-48a7-8943-cfac5d76a81c</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.32">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.32" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="8.0.32" />
    <PackageReference Include="StackExchange.Redis" Version="2.13.17" />
  </ItemGroup>

</Project>
```

### 6.2 `MyApi.Tests/MyApi.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.32" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApi\MyApi.csproj" />
  </ItemGroup>

</Project>
```

### 6.3 `.config/dotnet-tools.json`

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "8.0.32",
      "commands": [
        "dotnet-ef"
      ]
    }
  }
}
```

### 6.4 `global.json` (SDK selection, not a package file)

```json
{
  "sdk": {
    "rollForward": "latestFeature",
    "version": "8.0.100"
  }
}
```

This makes every `dotnet` command in the repository use your newest installed **8.0.x** SDK, even if
.NET 9 or 10 SDKs are also installed. If no 8.0 SDK is installed, `dotnet` stops with a clear error
instead of silently building with another version.

### 6.5 `Directory.Packages.props`

Not used. Do not create it: a `Directory.Packages.props` that turns on central package management
(`ManagePackageVersionsCentrally`) would make the `Version` attributes in the `.csproj` files above
fail with error NU1008.

---

## 7. Verify the installation and troubleshooting

### 7.1 Verification commands

**Working directory: any** (tools)

```bash
dotnet --list-sdks          # at least one 8.0.x line
dotnet --list-runtimes      # Microsoft.AspNetCore.App 8.0.x and Microsoft.NETCore.App 8.0.x
git --version
docker version              # shows both Client and Server; a missing Server section means the daemon is not running
docker compose version      # Docker Compose version v2.x or later
docker info                 # fails if Docker is not running or you lack permission
docker run --rm hello-world # end-to-end check that Docker can pull and run images
```

**Working directory: repository root** (project)

```bash
dotnet --version                                   # 8.0.x, selected by global.json
dotnet tool restore && dotnet ef --version         # 8.0.32
dotnet restore                                     # "Restore complete" / no errors
dotnet list package                                # versions in §3.1
dotnet list package --include-transitive           # full graph, e.g. Microsoft.EntityFrameworkCore 8.0.32
dotnet list package --vulnerable --include-transitive
dotnet build --configuration Release               # Build succeeded, 0 Error(s)
dotnet test                                        # Passed: 16, Failed: 0
```

**Working directory: repository root** (Docker images; needs `.env`)

```bash
docker compose config --quiet && echo "compose file OK"
docker compose pull --ignore-buildable
docker compose build
docker compose config --images                     # lists the five service/base images in use
docker images                                      # shows the pulled images and myapi:lab
```

### 7.2 Troubleshooting

**Missing or wrong SDK**

| Symptom | Fix |
|---|---|
| `dotnet: command not found` / `'dotnet' is not recognized` | Install the .NET 8 SDK (not just a runtime), then open a **new** terminal so `PATH` is refreshed. |
| `A compatible .NET SDK was not found. Requested SDK version: 8.0.100` | `global.json` requires an 8.0 SDK. Install it from <https://dotnet.microsoft.com/download/dotnet/8.0>; having only .NET 9/10 is not enough. |
| `NETSDK1045: The current .NET SDK does not support targeting .NET 8.0` | You are running an SDK older than 8.0 outside this folder, or `global.json` is missing. Run commands from the repository root. |
| `Could not execute because the specified command or file was not found` for `dotnet ef` | Run `dotnet tool restore` from the repository root. |

**NuGet restore errors and incompatible versions**

| Symptom | Fix |
|---|---|
| `NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json` | No network access to nuget.org: check your internet connection, proxy or VPN. Then `dotnet nuget locals http-cache --clear` and restore again. Behind a corporate proxy, set `HTTPS_PROXY` or ask IT for the NuGet feed. |
| `NU1102: Unable to find package X with version (= 8.0.32)` | Typo in the version, or a private feed that mirrors only some versions. `dotnet nuget list source` shows your active sources. nuget.org must be enabled. |
| `NU1202: Package X is not compatible with net8.0` | A version that only targets .NET 9/10 was added (for example an EF Core 9.x/10.x package). Use the exact versions in §2. |
| `NU1605` / `NU1608` downgrade or version-conflict warnings | EF Core packages with different versions, for example `SqlServer 8.0.32` with `InMemory 8.0.10`. Keep every `Microsoft.EntityFrameworkCore.*` package and `dotnet-ef` on **8.0.32**. |
| `NU1008: Projects that use central package version management should not define the version` | A `Directory.Packages.props` file exists somewhere above the repository. Remove it, or move the repository out of that folder. |
| Restore works but builds act strangely after a version change | `dotnet nuget locals all --clear`, delete the `bin/` and `obj/` folders, then `dotnet restore`. |

**Docker and image download failures**

| Symptom | Fix |
|---|---|
| `Cannot connect to the Docker daemon` / `error during connect` | Start Docker Desktop (wait for "Engine running"), or on Linux run `sudo systemctl start docker`. On Linux, add yourself to the `docker` group (`sudo usermod -aG docker $USER`, then sign out and in again). |
| `docker: 'compose' is not a docker command` | Install the Compose v2 plugin (Linux) or update Docker Desktop. Do not use the old `docker-compose` v1. |
| `manifest unknown` / `not found` when pulling | Tag typo, or a tag removed from the registry. Compare against §4 exactly. |
| `no matching manifest for linux/arm64` (SQL Server) | Apple Silicon: turn on Rosetta emulation in Docker Desktop. ARM64 Linux: SQL Server cannot run there. |
| `TLS handshake timeout`, `i/o timeout`, `toomanyrequests` | Network, proxy or Docker Hub rate limit. Retry, `docker login` (raises the Docker Hub limit), or configure the proxy in Docker Desktop (*Settings → Resources → Proxies*). |
| `dotnet restore` fails **inside** `docker build` but works on your machine | The build container has no access to nuget.org (corporate proxy/TLS inspection). Pass the proxy with `--build-arg HTTPS_PROXY=…` and trust the corporate root certificate inside the build stage. |
| `required variable MSSQL_SA_PASSWORD is missing a value` | Create `.env` from `.env.example` in the repository root. |
| `sqlserver` never becomes healthy | `docker compose logs sqlserver`. The usual causes are a password that does not meet SQL Server's complexity rules, or less than 2 GB of memory for Docker. Changing the password after the first start has no effect on the existing volume: `docker compose down -v` resets it. |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Set `NGINX_HOST_PORT=8081` (or another free port) in `.env`. |
| `502 Bad Gateway` from Nginx | The API is not ready or has crashed: `docker compose logs api`. |

---

## 8. Final dependency checklist

### 8.1 Software you must install manually

- [ ] **.NET 8 SDK** (8.0.x; latest 8.0.426) – <https://dotnet.microsoft.com/download/dotnet/8.0>
- [ ] **Docker Desktop** (Windows/macOS/Linux) **or** **Docker Engine + Docker Compose v2 plugin** (Linux) – <https://www.docker.com/products/docker-desktop/> · <https://docs.docker.com/engine/install/>
- [ ] **Git** – <https://git-scm.com/downloads>
- [ ] Windows only: **WSL 2** (Docker Desktop offers to install it)
- [ ] Apple Silicon only: **Rosetta emulation** turned on in Docker Desktop

### 8.2 NuGet packages restored by the .NET projects (`dotnet restore`)

`MyApi`:
- [ ] `Microsoft.EntityFrameworkCore.SqlServer` 8.0.32
- [ ] `Microsoft.EntityFrameworkCore.Design` 8.0.32 (design-time only, not published)
- [ ] `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 8.0.32
- [ ] `StackExchange.Redis` 2.13.17

`MyApi.Tests`:
- [ ] `Microsoft.NET.Test.Sdk` 18.11.0
- [ ] `xunit` 2.9.3
- [ ] `xunit.runner.visualstudio` 3.1.5
- [ ] `Microsoft.EntityFrameworkCore.InMemory` 8.0.32

### 8.3 .NET CLI tools (`dotnet tool restore`)

- [ ] `dotnet-ef` 8.0.32 – **local** tool, pinned in `.config/dotnet-tools.json`

### 8.4 Docker images (pulled automatically by `docker compose up --build`, or manually with `docker pull`)

- [ ] `mcr.microsoft.com/dotnet/sdk:8.0` – build stage (`MyApi/Dockerfile`)
- [ ] `mcr.microsoft.com/dotnet/aspnet:8.0` – runtime stage (`MyApi/Dockerfile`)
- [ ] `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04` – SQL Server 2022 Developer (`docker-compose.yml`)
- [ ] `redis:7.4-alpine` – Redis 7 (`docker-compose.yml`)
- [ ] `nginx:1.30-alpine` – reverse proxy (`docker-compose.yml`)
- [ ] `myapi:lab` – **built locally**, never pulled

### 8.5 Optional development tools

- [ ] Visual Studio Code + C# Dev Kit
- [ ] Visual Studio 2022 (17.8+) or Visual Studio 2026 – Windows
- [ ] MSSQL extension for VS Code, or SSMS 22 (Windows) – to browse the database
- [ ] Windows Terminal – if your Windows installation lacks it
- [ ] `coverlet.collector` 6.0.4 – only if you want code coverage reports

### 8.6 Included in .NET 8 – no separate installation

- [ ] ASP.NET Core runtime, Kestrel web server and minimal APIs (`Microsoft.AspNetCore.App` shared framework)
- [ ] Health checks: `AddHealthChecks`, `IHealthCheck`, `MapHealthChecks` (do **not** install `Microsoft.AspNetCore.Diagnostics.HealthChecks`)
- [ ] Dependency injection, configuration, environment variables, logging
- [ ] ProblemDetails / validation problem responses
- [ ] Forwarded headers (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`)
- [ ] `System.Text.Json`
- [ ] `dotnet new` templates (`webapi`, `xunit`, `sln`, `gitignore`, `globaljson`, `tool-manifest`)
- [ ] NuGet client (`dotnet add package`, `dotnet restore`, `dotnet list package`)

---

## 9. What was verified, and how

On 17 October 2026, on Ubuntu 24.04 (x64), with .NET SDK 8.0.131 (runtime 8.0.31, installed from the Ubuntu archive
package `dotnet-sdk-8.0`), Docker Engine 29.8.2 and Docker Compose v5.6.0:

* Every NuGet version above was looked up on nuget.org, restored, and confirmed with `dotnet list package`.
  All of them ship `net8.0` (or .NET Standard) assets. `dotnet list package --vulnerable --include-transitive`
  reported no vulnerable packages.
* `dotnet build` produced 0 warnings and 0 errors. `dotnet test` passed 16 of 16 tests.
* `dotnet ef migrations has-pending-model-changes` reported no changes, and `dotnet ef database update`
  ran against the containerised SQL Server.
* All five images were pulled. Confirmed versions: SDK 8.0.426, ASP.NET Core runtime 8.0.32,
  Redis 7.4.11, Nginx 1.30.6, SQL Server 2022 CU27 (`linux/amd64`).
* `docker compose up --wait` brought all four services to `healthy`. The CRUD endpoints, validation errors,
  the Redis `X-Cache` MISS/HIT behaviour, cache invalidation on update and delete, the `/health/ready` 503 response while
  Redis was stopped (with product reads still served from SQL Server), restarts with existing data,
  recreating the API container, and `--scale api=2` load balancing were all exercised through Nginx.
* The verification sandbox reaches nuget.org only through an HTTPS-inspecting proxy, so for the in-Docker
  restore the image was built with that proxy's certificate added in a throwaway copy of the
  Dockerfile. The committed `MyApi/Dockerfile` needs no such change on a normal internet connection.
* **Not verified here:** the Windows and macOS installers, Docker Desktop itself, Apple Silicon
  emulation of SQL Server, Visual Studio/VS Code/SSMS, and the download pages on
  docs.docker.com, git-scm.com, code.visualstudio.com, visualstudio.microsoft.com, marketplace.visualstudio.com and
  learn.microsoft.com, which the sandbox could not reach. Those links are the vendors' official
  download locations, but check them in your browser.
