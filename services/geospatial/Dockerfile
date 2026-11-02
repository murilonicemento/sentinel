FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /
COPY ["src/Geospatial.Api/Geospatial.Api.csproj", "src/Geospatial.Api/"]
COPY ["src/Geospatial.Application/Geospatial.Application.csproj", "src/Geospatial.Application/"]
COPY ["src/Geospatial.Domain/Geospatial.Domain.csproj", "src/Geospatial.Domain/"]
COPY ["src/Geospatial.Infrastructure/Geospatial.Infrastructure.csproj", "src/Geospatial.Infrastructure/"]
COPY ["tests/Geospatial.IntegrationTests/Geospatial.IntegrationTests.csproj", "tests/Geospatial.IntegrationTests/"]
COPY ["tests/Geospatial.UnitTests/Geospatial.UnitTests.csproj", "tests/Geospatial.UnitTests/"]
RUN dotnet restore "src/Geospatial.Api/Geospatial.Api.csproj"
COPY . .
WORKDIR "/src/Geospatial.Api"
RUN dotnet build "./Geospatial.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Geospatial.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Geospatial.Api.dll"]
