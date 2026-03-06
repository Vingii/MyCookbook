# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the backend
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

# Frontend dev mode (proxies /api to localhost:5000)
cd frontend && npm run dev   # http://localhost:5173

# Build frontend for production (outputs to MyCookbook/wwwroot/)
cd frontend && npm run build
```

## Architecture

**MyCookbook** is an ASP.NET Core 8 app with a REST API backend and a Vue 3 + TypeScript frontend.

### Project Layout

```
MyCookbook/               # ASP.NET Core 8 — REST API + serves Vue static files
  Api/                    # MVC controllers + DTOs
  Services/               # ApiTokenService, ApiKeyAuthenticationHandler, etc.
MyCookbook.Test/          # xUnit + bunit tests
frontend/                 # Vue 3 + Vite + TypeScript SPA
  src/api/                # Axios client + typed API wrappers
  src/stores/             # Pinia stores (auth, recipes, planner)
  src/views/              # Page components
  src/components/         # Shared components
LanguageDictionaryCleaner/ # Standalone utility for language dictionary processing
```

### Key Layers (Backend)

- **Api/** — REST controllers: `RecipesController`, `IngredientsController`, `StepsController`, `PlannerController`, `TagsController`, `FavoritesController`, `ExportController`, `AuthController`
- **Api/Dto/** — DTO classes and request models; `DtoMapper.cs` maps entities → DTOs
- **Components/Account/** — Blazor SSR identity pages (`/Account/Login`, `/Account/Register`, etc.) — the only remaining Blazor code
- **Data/** — EF Core DbContexts and `CookbookDatabaseService` (the main data access class with 70+ async methods)
- **Services/** — `ApiTokenService` (bearer tokens), `ApiKeyAuthenticationHandler`, `YouTrackFeedbackProvider`, `HeaderAuthenticationMiddleware`, email
- **Logging/TimeLogger.cs** — Performance logger; warns >200ms, errors >500ms

### Key Layers (Frontend)

- `frontend/src/api/client.ts` — Axios instance (`baseURL: /api`, 401 → redirect to login)
- `frontend/src/api/` — `recipes.ts`, `planner.ts`, `auth.ts`, `tags.ts`
- `frontend/src/stores/` — `useRecipesStore`, `usePlannerStore`, `useAuthStore` (Pinia)
- `frontend/src/router/index.ts` — Vue Router with auth guard
- `frontend/src/views/` — `DashboardView`, `RecipeBrowserView`, `RecipeViewerView`, `PlannerView`, `ExportView`, `RandomRecipeView`

### Data Model

Two DbContexts:
- `ApplicationDbContext` — ASP.NET Core Identity (users/auth)
- `CookbookDatabaseContext` — All recipe data

Core entities: `Recipe`, `Ingredient`, `Step`, `PlannedRecipe`, `FavoriteRecipe`, `Tag`, `UserPreference`. All tables are isolated per-user via a `UserName` field — every query filters by user.

`Step` has a `StepType` enum (Active/SemiPassive/Passive) for timeline visualization. Durations are stored as minutes at the recipe level and as `TimeSpan` on steps.

**Known quirk:** `CookbookDatabaseService.AddTag()` has a bug (no `SaveChangesAsync`). `TagsController` works around this by calling `db.GetContext()` directly.

### REST API

All endpoints use `[Authorize(Policy = "CookieOrApiKey")]` (except `GET /api/recipes/shared/{guid}` which is public, and `GET /api/auth/me` which is anonymous).

```
GET/POST   /api/recipes                        list + create
GET        /api/recipes/random                 random recipe
GET        /api/recipes/shared/{guid}          public sharing (no auth)
GET/PUT/DELETE /api/recipes/{guid}             get detail, update, delete
POST       /api/recipes/{guid}/clone           clone
POST       /api/recipes/{guid}/lastcooked      mark cooked
POST/PUT/DELETE /api/recipes/{guid}/ingredients/{id}  + /up /down
POST/PUT/DELETE /api/recipes/{guid}/steps/{id}        + /up /down
POST/DELETE /api/recipes/{guid}/tags/{name}
POST/DELETE /api/recipes/{guid}/favorite
GET/POST/PUT/DELETE /api/planner              ?from=YYYY-MM-DD&to=YYYY-MM-DD
GET        /api/tags
GET        /api/export
POST       /api/import
GET        /api/auth/me
GET/DELETE /api/auth/token                    generate/revoke bearer token
```

### Authentication

- **Cookie auth** — ASP.NET Core Identity (`IdentityConstants.ApplicationScheme`)
- **API key auth** — `Authorization: Bearer <token>` via `ApiKeyAuthenticationHandler`; raw token is SHA-256 hashed and stored in `UserPreferences["ApiToken"]`
- **Authentik SSO** — `HeaderAuthenticationMiddleware` reads `X-Authentik-*` forwarded headers
- **Google OAuth** — via `Google__ClientId`/`Google__ClientSecret`

Policy `"CookieOrApiKey"` accepts either cookie or bearer token.

### Testing

Tests use:
- `TestingWebAppFactory` replaces SQL Server with EF Core InMemory (unique DB per test via Guid)
- Moq + RichardSzalay.MockHttp for mocking

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
