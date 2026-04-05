# MyCookbook
Web app for cooking recipe management and meal planning.

## Environment variables

| Variable | Description |
|---|---|
| `SA_PASSWORD` | PostgreSQL SA password (used in docker-compose) |
| `COOKBOOK_URL` | Public URL of the app (e.g. `https://cookbook.example.com`) |
| `WEB_PORT` | Host port exposed by docker-compose (default: `8080`) |
| `Authentik__Authority` | OIDC authority URL — the Authentik application URL (e.g. `https://authentik.example.com/application/o/cookbook/`) |
| `Authentik__ClientId` | OAuth2 client ID from the Authentik provider |
| `Authentik__ClientSecret` | OAuth2 client secret from the Authentik provider |
| `YouTrack__BaseUrl` | YouTrack base URL for feedback integration |
| `YouTrack__Token` | YouTrack API token |
| `YouTrack__ProjectKey` | YouTrack project key |
| `Grafana__Key` | Grafana API key (Loki sink) |
| `Grafana__Login` | Grafana login |
| `Grafana__Url` | Grafana Loki URL |
| `Mailgun__ApiKey` | Mailgun API key |
| `Mailgun__FromEmail` | Mailgun sender address |
| `Mailgun__MailDomain` | Mailgun domain |

## Authentik OIDC setup

Create an **OAuth2/OpenID Connect provider** in Authentik (not a proxy provider) and configure:

| Setting | Value |
|---|---|
| Redirect URI | `https://cookbook.example.com/signin-oidc` |
| Post-logout redirect URI | `https://cookbook.example.com` |
| Scopes | `openid`, `profile`, `email` |
| Client type | Confidential |

Then create an application pointing at the provider and set `Authentik__Authority` to the provider's issuer URL, which ends with `/application/o/<application-slug>/`.

## Sample docker-compose

```yml
services:
  mycookbook:
    image: vingii/mycookbook:latest
    container_name: mycookbook
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=db;Database=MyCookbookDb;Username=postgres;Password=${SA_PASSWORD}
      - COOKBOOK_URL=https://cookbook.example.com
      - Authentik__Authority=https://authentik.example.com/application/o/cookbook/
      - Authentik__ClientId=<client-id>
      - Authentik__ClientSecret=<client-secret>
    depends_on:
      db:
        condition: service_healthy
    ports:
      - "${WEB_PORT:-8080}:8080"
    networks:
      - mycookbook-network
    env_file:
      - .env
    volumes:
      - dataprotection-keys:/root/.aspnet/DataProtection-Keys

  db:
    image: postgres:16
    container_name: mycookbook-db
    environment:
      - POSTGRES_DB=MyCookbookDb
      - POSTGRES_PASSWORD=${SA_PASSWORD}
    healthcheck:
      test: ["CMD", "pg_isready", "-U", "postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 5s
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - mycookbook-network

networks:
  mycookbook-network:
    driver: bridge

volumes:
  postgres-data:
    driver: local
  dataprotection-keys:
    driver: local
```
