FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

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
RUN dotnet publish "./Backup.Server.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Backup.Server.Api.dll"]
