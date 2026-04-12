FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /
COPY ["src/RiskEvaluation.Api/RiskEvaluation.Api.csproj", "src/RiskEvaluation.Api/"]
COPY ["src/RiskEvaluation.Application/RiskEvaluation.Application.csproj", "src/RiskEvaluation.Application/"]
COPY ["src/RiskEvaluation.Domain/RiskEvaluation.Domain.csproj", "src/RiskEvaluation.Domain/"]
COPY ["src/RiskEvaluation.Infrastructure/RiskEvaluation.Infrastructure.csproj", "src/RiskEvaluation.Infrastructure/"]
COPY ["tests/RiskEvaluation.IntegrationTests/RiskEvaluation.IntegrationTests.csproj", "tests/RiskEvaluation.IntegrationTests/"]
COPY ["tests/RiskEvaluation.UnitTests/RiskEvaluation.UnitTests.csproj", "tests/RiskEvaluation.UnitTests/"]
RUN dotnet restore "src/RiskEvaluation.Api/RiskEvaluation.Api.csproj"
COPY . .
WORKDIR "/src/RiskEvaluation.Api"
RUN dotnet build "./RiskEvaluation.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RiskEvaluation.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RiskEvaluation.Api.dll"]