FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Backup.Server.Api/Backup.Server.Api.csproj", "Backup.Server.Api/"]
COPY ["Backup.Server.Application/Backup.Server.Application.csproj", "Backup.Server.Application/"]
COPY ["Backup.Server.Domain/Backup.Server.Domain.csproj", "Backup.Server.Domain/"]
COPY ["Backup.Shared.Contracts/Backup.Shared.Contracts.csproj", "Backup.Shared.Contracts/"]
COPY ["Backup.Server.Infrastructure/Backup.Server.Infrastructure.csproj", "Backup.Server.Infrastructure/"]
RUN dotnet restore "Backup.Server.Api/Backup.Server.Api.csproj"
COPY . .
WORKDIR "/src/Backup.Server.Api"
RUN dotnet build "./Backup.Server.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet ef database update --project Backup.Server.Infrastructure --startup-project Backup.Server.Api
RUN dotnet publish "./Backup.Server.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Backup.Server.Api.dll"]
