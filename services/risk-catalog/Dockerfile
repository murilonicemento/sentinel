FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /
COPY ["src/RiskCatalog.Api/RiskCatalog.Api.csproj", "src/RiskCatalog.Api/"]
COPY ["src/RiskCatalog.Application/RiskCatalog.Application.csproj", "src/RiskCatalog.Application/"]
COPY ["src/RiskCatalog.Domain/RiskCatalog.Domain.csproj", "src/RiskCatalog.Domain/"]
COPY ["src/RiskCatalog.Infrastructure/RiskCatalog.Infrastructure.csproj", "src/RiskCatalog.Infrastructure/"]
COPY ["tests/RiskCatalog.IntegrationTests/RiskCatalog.IntegrationTests.csproj", "tests/RiskCatalog.IntegrationTests/"]
COPY ["tests/RiskCatalog.UnitTests/RiskCatalog.UnitTests.csproj", "tests/RiskCatalog.UnitTests/"]
RUN dotnet restore "src/RiskCatalog.Api/RiskCatalog.Api.csproj"
COPY . .
WORKDIR "/src/RiskCatalog.Api"
RUN dotnet build "./RiskCatalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RiskCatalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RiskCatalog.Api.dll"]
