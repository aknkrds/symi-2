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

## Migration/DB (local)

- SQLite default connection: `Data Source=symi.db`
- DB initializes on first run.