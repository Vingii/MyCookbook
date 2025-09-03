# MyCookbook
 Web app for cooking recipe management and meal planning.

## Supported environment variables
- SA_PASSWORD
- Jira__Domain
- Jira__ProjectKey
- Jira__Email
- Jira__Key
- Grafana__Key
- Grafana__Login
- Google__ClientId
- Google__ClientSecret
- Mailgun__ApiKey
- Mailgun__FromEmail
- Mailgun__MailDomain
- WEB_PORT
- COOKBOOK_URL
- COOKBOOK_AUTHENTIK_URL

## Sample docker-compose
```yml
services:
  mycookbook:
    image: vingii/mycookbook:latest
    container_name: mycookbook
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=db;Database=MyCookbookDb;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;
    depends_on:
      db:
        condition: service_started
    ports:
      - "${WEB_PORT:-8080}:8080"
    networks:
      - mycookbook-network
    env_file:
      - .env
    volumes:
      - dataprotection-keys:/root/.aspnet/DataProtection-Keys

  db:
    image: "mcr.microsoft.com/mssql/server:2022-latest"
    container_name: mycookbook-db
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SA_PASSWORD}
    healthcheck:
      test: [ "CMD", "/opt/mssql-tools18/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", "${SA_PASSWORD}", "-Q", "SELECT 1", "-C"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 5s
    volumes:
      - mssql-data:/var/opt/mssql
    networks:
      - mycookbook-network
    env_file:
      - .env

networks:
  mycookbook-network:
    driver: bridge

volumes:
  mssql-data:
    driver: local
  dataprotection-keys:
    driver: local
```