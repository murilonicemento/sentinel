FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Ingestion.Api/Ingestion.Api.csproj", "Ingestion.Api/"]
COPY ["src/Ingestion.Application/Ingestion.Application.csproj", "Ingestion.Application/"]
COPY ["src/Ingestion.Domain/Ingestion.Domain.csproj", "Ingestion.Domain/"]
COPY ["src/Ingestion.Infrastructure/Ingestion.Infrastructure.csproj", "Ingestion.Infrastructure/"]
COPY ["tests/Ingestion.IntegrationTests/Ingestion.IntegrationTests.csproj", "Ingestion.IntegrationTests/"]
COPY ["tests/Ingestion.UnitTests/Ingestion.UnitTests.csproj", "Ingestion.UnitTests/"]
RUN dotnet restore "src/Ingestion.Api/Ingestion.Api.csproj"
COPY . .
WORKDIR "/src/Ingestion.Api"
RUN dotnet build "./Ingestion.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Ingestion.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ingestion.Api.dll"]
