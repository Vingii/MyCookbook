# Stage 1: .NET SDK Base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

COPY ["MyCookbook/MyCookbook.csproj", "MyCookbook/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "MyCookbook/MyCookbook.csproj"

# Stage 2: Frontend Build
FROM node:24-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN --mount=type=cache,target=/root/.npm \
    npm ci
COPY frontend/ ./
RUN npm run build

# Stage 3: Publish
FROM build-env AS publish
COPY MyCookbook/ MyCookbook/
COPY --from=frontend-build /src/MyCookbook/wwwroot /src/MyCookbook/wwwroot

WORKDIR "/src/MyCookbook"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "MyCookbook.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# --- STAGE 4: Final Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 8080

RUN apk add --no-cache icu-libs tzdata

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER $APP_UID
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyCookbook.dll"]