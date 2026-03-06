# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the solution
dotnet build

# Run the web app (requires SQL Server - see docker-compose.yml)
dotnet run --project MyCookbook

# Run all tests
dotnet test

# Run a single test class
dotnet test MyCookbook.Test --filter "FullyQualifiedName~RecipeTableTests"

# Run a single test method
dotnet test MyCookbook.Test --filter "FullyQualifiedName~RecipeTableTests.SomeTestMethod"

# Build and run with Docker Compose (includes SQL Server)
docker compose up --build
```

## Architecture

**MyCookbook** is an ASP.NET Core 8 Blazor Server app for recipe management and meal planning.

### Project Layout

```
MyCookbook/               # Main Blazor Server web application
MyCookbook.Test/          # xUnit + bunit tests
LanguageDictionaryCleaner/ # Standalone utility for language dictionary processing
```

### Key Layers

- **Pages/** — Blazor page components (`Index`, `RecipeViewer`, `RecipeBrowser`, `Planner`, `Export`, `RandomRecipe`, `RecipeShared`)
- **Components/** — Reusable Blazor components (`RecipeTable`, `App`, dialogs)
- **Data/** — EF Core DbContexts and `CookbookDatabaseService` (the main data access class with 70+ async methods)
- **Services/** — Supporting services (YouTrack feedback, localization, email, logging, auth middleware)
- **Logging/TimeLogger.cs** — Performance logger; warns >200ms, errors >500ms

### Data Model

Two DbContexts:
- `ApplicationDbContext` — ASP.NET Core Identity (users/auth)
- `CookbookDatabaseContext` — All recipe data

Core entities: `Recipe`, `Ingredient`, `Step`, `PlannedRecipe`, `FavoriteRecipe`, `Tag`, `UserPreference`. All tables are isolated per-user via a `UserName` field — every query filters by user.

`Step` has a `StepType` enum (Active/SemiPassive/Passive) for timeline visualization. Durations are stored as minutes at the recipe level and as `TimeSpan` on steps.

### Testing

Tests use:
- **bunit** for Blazor component testing
- `TestingWebAppFactory` replaces SQL Server with EF Core InMemory (unique DB per test via Guid)
- `BlazorTestBase` as base class for component tests
- Moq + RichardSzalay.MockHttp for mocking

### Authentication

Three supported modes: ASP.NET Core Identity (local), Google OAuth (via `Google__ClientId`/`Google__ClientSecret`), and Authentik SSO via `HeaderAuthenticationMiddleware` (reads forwarded headers, configured with `COOKBOOK_AUTHENTIK_URL`).

Password requirements are relaxed in Development (minimum length 1, no complexity rules).

### Localization

Custom localization stack: `StringLocalizer` + `MemoryLanguageDictionary` (in-memory word inflection for Czech/English). Language changes are broadcast via `LanguageNotifier` (pub/sub) to subscribed components.

### Logging

Serilog with Console and Grafana Loki sinks. Loki endpoint configured via `LOKI_URI`.

## Environment Variables

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `SA_PASSWORD` | SQL Server SA password (used in docker-compose) |
| `ASPNETCORE_ENVIRONMENT` | `Development`, `Production`, or `Docker` |
| `ASPNETCORE_URLS` | App listen URLs (e.g. `http://+:8080`) |
| `WEB_PORT` | Host port exposed by docker-compose (default: `8080`) |
| `COOKBOOK_URL` | Public URL of the app |
| `COOKBOOK_AUTHENTIK_URL` | Authentik SSO URL for header-based auth |
| `LOKI_URI` | Grafana Loki endpoint for log shipping |
| `Google__ClientId` / `Google__ClientSecret` | Google OAuth credentials |
| `Grafana__Key` / `Grafana__Login` / `Grafana__Url` | Grafana integration |
| `YouTrack__BaseUrl` / `YouTrack__Token` / `YouTrack__ProjectKey` | YouTrack feedback integration |
| `Mailgun__ApiKey` / `Mailgun__FromEmail` / `Mailgun__MailDomain` | Email sending (dev only) |

## Versioning

Version is set in `MyCookbook/MyCookbook.csproj` as `<Version>`. The project follows [Semantic Versioning](https://semver.org/). All changes are documented in `MyCookbook/CHANGELOG.md` using the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format. When releasing, update both the `<Version>` in the `.csproj` and add an entry to `CHANGELOG.md`.
