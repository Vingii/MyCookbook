## Build & Run

```bash
# Run all tests
dotnet test

# Run a single test class
dotnet test MyCookbook.Test --filter "FullyQualifiedName~RecipeTableTests"

# Run a single test method
dotnet test MyCookbook.Test --filter "FullyQualifiedName~RecipeTableTests.SomeTestMethod"

# Build and run with Podman Compose (includes SQL Server)
podman compose up --build
```

## Architecture

**MyCookbook** is an ASP.NET Core 8 app with a REST API backend and a Vue 3 + TypeScript frontend.

### Project Layout

```
MyCookbook/               # ASP.NET Core 8 — REST API + serves Vue static files
  Api/                    # MVC controllers + DTOs
  Services/               # ApiTokenService, ApiKeyAuthenticationHandler, etc.
MyCookbook.Test/          # xUnit + bunit tests
frontend/                 # Vue 3 + Vite + Vuetify 3 + TypeScript SPA
  src/api/                # Axios client + typed API wrappers
  src/composables/        # useIngredientHighlighter (Czech inflection highlighting)
  src/i18n/               # EN/CS translations (translations.ts)
  src/stores/             # Pinia stores (auth, recipes, planner, ui)
  src/views/              # Page components
  src/components/         # Shared components (RecipeTable, IngredientList, StepList)
  public/dictionaries/    # Czech inflection dictionaries (cs-nouns.jsonl, cs-adj.jsonl)
LanguageDictionaryCleaner/ # Standalone CLI: processes kaikki.org JSONL → inflection dicts
```

### Key Layers (Backend)

- **Api/** — REST controllers: `RecipesController`, `IngredientsController`, `StepsController`, `PlannerController`, `TagsController`, `FavoritesController`, `ExportController`, `AuthController`
- **Api/Dto/** — DTO classes and request models; `DtoMapper.cs` maps entities → DTOs
- **Data/** — EF Core DbContext (`CookbookDatabaseContext`) and `CookbookDatabaseService` (the main data access class with 70+ async methods)
- **Services/** — `ApiTokenService` (bearer tokens), `ApiKeyAuthenticationHandler`, `YouTrackFeedbackProvider`, `HeaderAuthenticationMiddleware`, email
- **Logging/TimeLogger.cs** — Performance logger; warns >200ms, errors >500ms

### Key Layers (Frontend)

- `frontend/src/api/client.ts` — Axios instance (`baseURL: /api`, 401 → redirect to login)
- `frontend/src/api/` — `recipes.ts`, `planner.ts`, `auth.ts`, `tags.ts`
- `frontend/src/stores/ui.ts` — `useUiStore`: theme (light/dark), locale (en/cs), persisted to localStorage
- `frontend/src/stores/` — `useRecipesStore`, `usePlannerStore`, `useAuthStore` (Pinia)
- `frontend/src/i18n/translations.ts` — EN/CS translation strings, used via `ui.t.*`
- `frontend/src/composables/useIngredientHighlighter.ts` — loads Czech inflection dicts, expands ingredient names to all their forms, highlights matches in step descriptions
- `frontend/src/router/index.ts` — Vue Router with auth guard
- `frontend/src/views/` — `RecipeBrowserView`, `RecipeViewerView`, `PlannerView`, `ExportView`, `SettingsView`, `UnauthorizedView`

### Data Model

One DbContext: `CookbookDatabaseContext` — all recipe data. (ASP.NET Core Identity and `ApplicationDbContext` have been removed.)

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

There is no local login/register. All auth is external:

- **Authentik SSO** — `HeaderAuthenticationMiddleware` reads `X-Authentik-*` forwarded headers; sets `context.User` with `"HeaderAuth"` auth type
- **API key auth** — `Authorization: Bearer <token>` via `ApiKeyAuthenticationHandler`; raw token is SHA-256 hashed and stored in `UserPreferences["ApiToken"]`

Policy `"CookieOrApiKey"` accepts either HeaderAuth or API key. Login/logout redirect to `/api/auth/logout` and trigger a page reload.

### Logging

Serilog with Console and Grafana Loki sinks.

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
| `Grafana__Key` / `Grafana__Login` / `Grafana__Url` | Grafana integration |
| `YouTrack__BaseUrl` / `YouTrack__Token` / `YouTrack__ProjectKey` | YouTrack feedback integration |
| `Mailgun__ApiKey` / `Mailgun__FromEmail` / `Mailgun__MailDomain` | Email sending (dev only) |

## Versioning

Version is set in `MyCookbook/MyCookbook.csproj` as `<Version>`. The project follows [Semantic Versioning](https://semver.org/). All changes are documented in `MyCookbook/CHANGELOG.md` using the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format. When adding a feature, update both the `<Version>` in the `.csproj` and add an entry to `CHANGELOG.md`.

## Testing and Coverage

**After implementing a feature or fixing a bug, run coverage to verify the affected code is tested.**

### Backend

```bash
# Run tests with coverage (outputs Cobertura XML to MyCookbook.Test/TestResults/)
dotnet test --collect:"XPlat Code Coverage"

# Optional: generate an HTML report (one-time global install)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"MyCookbook.Test/TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
# Then open coverage-report/index.html
```

### Frontend

```bash
cd frontend

# Run tests
npm test

# Run tests in watch mode
npm run test:watch

# Run tests with coverage (outputs to frontend/coverage/)
npm run test:coverage
```

### Known test limitations

- `DEV_AUTO_LOGIN=devuser` is always active in the Development environment used by `TestingWebAppFactory`, so testing 401 responses requires a separate factory subclass that overrides this config key.
- EF Core InMemory does not enforce relational constraints, so referential integrity bugs will not be caught in integration tests.
- `CookbookDatabaseService.AddTag()` has a known bug (missing `SaveChangesAsync`); `TagsController` works around this directly.

## E2E Tests

E2E tests use [Playwright](https://playwright.dev/) with [`playwright-bdd`](https://vitalets.github.io/playwright-bdd/) (Gherkin `.feature` files). The full stack must be running first.

```bash
# Start the app (if not already running)
podman compose up --build -d

# Install dependencies (first time only)
cd e2e
npm install
npx playwright install --with-deps chromium

# Run all E2E tests
npm test

# Open Playwright UI debugger
npm run test:ui

# Open codegen to inspect live selectors
npm run codegen
```

Feature files live in `e2e/features/`, step definitions in `e2e/steps/`.
`playwright-bdd` generates Playwright test files into `e2e/.features-gen/` (gitignored) — rebuilt automatically on each `npm test`.

Auth is handled automatically via `DEV_AUTO_LOGIN=devuser` (set in `.env`). Each scenario creates its own recipe data via the REST API and cleans it up after.

**If a selector breaks**, run `npm run codegen` against the running app to find the correct locator — Vuetify's generated DOM can be verbose.
