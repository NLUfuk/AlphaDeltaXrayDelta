# Multi-stage build for the CRM+Kanban API (.NET 10). Build context is the REPO ROOT so the
# central package management files and sibling projects are available to restore.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching): copy the manifests the restore graph needs.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet restore src/CrmKanban.Api/CrmKanban.Api.csproj

RUN dotnet publish src/CrmKanban.Api/CrmKanban.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# TLS terminates at the reverse proxy; the API serves plain HTTP inside the network.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1
EXPOSE 8080

# Non-root for defense in depth.
USER $APP_UID

ENTRYPOINT ["dotnet", "CrmKanban.Api.dll"]
