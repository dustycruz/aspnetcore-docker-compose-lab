# ASP.NET Core 8 + Docker Compose Lab

A small ASP.NET Core 8 minimal Web API (`MyApi`) that runs in Docker Compose behind Nginx, stores
products in SQL Server 2022 through Entity Framework Core 8, and caches product lookups in Redis 7.

```text
client ──► nginx (localhost:8080) ──► api (MyApi :8080) ──┬──► sqlserver (SQL Server 2022)
                                                          └──► redis (Redis 7.4)
```

**Start here:** [docs/INSTALLATION.md](docs/INSTALLATION.md) lists every tool, NuGet package,
.NET CLI tool and Docker image the lab needs, with exact versions and installation commands.

> .NET 8 reaches end of support on **10 November 2026**. The lab targets .NET 8 on purpose; see the
> installation guide for details.

## Quick start

Prerequisites: .NET 8 SDK, Docker with Compose v2+, Git ([details](docs/INSTALLATION.md#1-required-and-optional-software)).

```bash
git clone https://github.com/dustycruz/aspnetcore-docker-compose-lab.git
cd aspnetcore-docker-compose-lab

dotnet tool restore
dotnet build
dotnet test

cp .env.example .env              # PowerShell: Copy-Item .env.example .env  - then change the password
docker compose up --build -d --wait
curl http://localhost:8080/health/ready
```

## Endpoints (through Nginx on http://localhost:8080)

| Method | Path | Description |
|---|---|---|
| GET | `/` | Service info, including the container host name (handy with `--scale api=2`) |
| GET | `/api/products` | All products (from SQL Server) |
| GET | `/api/products/{id}` | One product, cache-aside through Redis. The `X-Cache` response header is `MISS` or `HIT` |
| POST | `/api/products` | Create: `{"name":"Webcam","description":"1080p","price":49.99}` |
| PUT | `/api/products/{id}` | Update (and evict from Redis) |
| DELETE | `/api/products/{id}` | Delete (and evict from Redis) |
| GET | `/health/live` | Liveness: the process is running |
| GET | `/health/ready` | Readiness: SQL Server and Redis checks (503 if either fails) |

Validation errors return `400` with RFC 9110 `ValidationProblem` details.

## Repository layout

```text
.config/dotnet-tools.json   local dotnet-ef 8.0.32 tool
.env.example                template for .env (SQL Server sa password, Nginx host port)
docker-compose.yml          sqlserver, redis, api, nginx services
global.json                 pins the .NET 8 SDK
nginx/nginx.conf            reverse proxy to api:8080
MyApi/                      ASP.NET Core 8 Web API + Dockerfile + EF Core migrations
MyApi.Tests/                xUnit tests (EF Core InMemory + fake cache)
docs/INSTALLATION.md        complete dependency and installation guide
```

## How it behaves

* **Startup:** Compose starts SQL Server and Redis, then waits until both are healthy before it starts the API.
  The API applies EF Core migrations (creating `MyApiDb` with three seeded products), and Nginx
  starts once the API is healthy.
* **Redis outage:** product reads fall back to SQL Server and `/health/ready` reports `Unhealthy`
  (503). The API reconnects automatically when Redis returns.
* **Scaling:** `docker compose up -d --scale api=2` runs two API containers. Nginx re-resolves the
  `api` name through Docker DNS and balances requests across both.
* **Data:** SQL Server and Redis data live in the named volumes `sqlserver-data` and `redis-data`.
  `docker compose down -v` removes them.
