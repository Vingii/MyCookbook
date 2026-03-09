FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs
WORKDIR /src

COPY ["MyCookbook/MyCookbook.csproj", "MyCookbook/"]
RUN dotnet restore "MyCookbook/MyCookbook.csproj"

COPY . .

WORKDIR "/src/frontend"
RUN npm ci && npm run build

WORKDIR "/src/MyCookbook"
RUN dotnet build "MyCookbook.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyCookbook.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyCookbook.dll"]