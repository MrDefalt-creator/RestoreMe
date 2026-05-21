FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Compose builds with context=repo-root so installers/ is reachable alongside Backup/.
COPY ["Backup/Backup.Server.Api/Backup.Server.Api.csproj", "Backup/Backup.Server.Api/"]
COPY ["Backup/Backup.Server.Application/Backup.Server.Application.csproj", "Backup/Backup.Server.Application/"]
COPY ["Backup/Backup.Server.Domain/Backup.Server.Domain.csproj", "Backup/Backup.Server.Domain/"]
COPY ["Backup/Backup.Shared.Contracts/Backup.Shared.Contracts.csproj", "Backup/Backup.Shared.Contracts/"]
COPY ["Backup/Backup.Server.Infrastructure/Backup.Server.Infrastructure.csproj", "Backup/Backup.Server.Infrastructure/"]
RUN dotnet restore "Backup/Backup.Server.Api/Backup.Server.Api.csproj"
COPY Backup/ Backup/
WORKDIR "/src/Backup/Backup.Server.Api"
RUN dotnet publish "./Backup.Server.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Bake the installer scripts (~10 KB) so the install-agent wizard can hand
# operators a URL that resolves on the backend itself — no external GitHub
# dependency for the script half of the flow. The agent BINARIES are NOT
# baked into this image; compose mounts them in at /app/wwwroot/installers/
# binaries from the `agent_binaries` volume (populated by the agent-builder
# one-shot service). See docker-compose/README.md → Agent Setup.
COPY installers/install-agent.ps1     /app/wwwroot/installers/install-agent.ps1
COPY installers/install-agent.sh      /app/wwwroot/installers/install-agent.sh
COPY installers/restoreme-agent.service /app/wwwroot/installers/restoreme-agent.service
ENTRYPOINT ["dotnet", "Backup.Server.Api.dll"]
