# Symi API

[![CI](https://github.com/aknkrds/symi-2/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/aknkrds/symi-2/actions/workflows/ci.yml)

Symi API is an ASP.NET Core Web API.

## Quick Start

- Requirements: .NET SDK 9, Docker (optional)
- Run locally:
  - `dotnet restore`
  - `dotnet run --project Symi.Api`
  - Check health: `GET http://localhost:5229/health`
  - Swagger UI (Development): `http://localhost:5229/swagger`

## CI

- GitHub Actions runs on `windows-latest` for branches `main` and `dev`.
- Steps: checkout → setup .NET → restore → build (Release) → test (TRX) → publish test report.
- Test artifacts: TRX files are uploaded as workflow artifacts.

## Docker (dev)

- Build API image: `docker build -t symi-api:dev ./Symi.Api`
- Compose: `docker compose -f docker-compose.dev.yml up -d`
- API: `http://localhost:8080/health`

## Secrets (GitHub Actions)

- Recommended repository secrets:
  - `JWT__AccessTokenSecret`
  - `JWT__RefreshTokenSecret`
  - `ConnectionStrings__Default`

## Logging

- Serilog Console sink enabled.
- Correlation ID (`X-Correlation-Id`) is echoed back in responses.
- Unhandled exceptions return JSON with stack in Development.

## Database & EF Core

- Provider selection:
  - SQLite by default: `ConnectionStrings:Default = "Data Source=symi.db"`
  - PostgreSQL when conn string contains `Host=` or `Username=` (e.g. `Host=localhost;Database=symi;Username=postgres;Password=...`).
- Initialization:
  - Development/Production: applies migrations (`Database.Migrate()`).
  - Testing: uses `EnsureCreated()` to avoid migration dependency.
- Migrations (run from repo root):
  - Add initial: `dotnet ef migrations add InitialCreate --project Symi.Api --startup-project Symi.Api`
  - Update DB: `dotnet ef database update --project Symi.Api --startup-project Symi.Api`

## Rate Limiting

- Config-driven limits in `appsettings.json`:
  - `RateLimit:DefaultPerMinute`: global default (e.g. `60`).
  - `RateLimit:Routes:/auth/login`: stricter per-route (e.g. `15`).
  - `RateLimit:Routes:/auth/refresh`: route override (e.g. `30`).
- Responses include `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset` and `Retry-After` on 429.

## Error Contract

- 403 Forbidden: `{ "code": "forbidden", "message": "Insufficient role or policy" }`
- 500 Global handler: `{ "code": "error", "message": "<exception message>", "route": "<path>", "stack": "<stacktrace>" }` (stack present in Development).