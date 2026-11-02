FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /
COPY ["src/AlertOrchestrator.Api/AlertOrchestrator.Api.csproj", "src/AlertOrchestrator.Api/"]
COPY ["src/AlertOrchestrator.Application/AlertOrchestrator.Application.csproj", "src/AlertOrchestrator.Application/"]
COPY ["src/AlertOrchestrator.Domain/AlertOrchestrator.Domain.csproj", "src/AlertOrchestrator.Domain/"]
COPY ["src/AlertOrchestrator.Infrastructure/AlertOrchestrator.Infrastructure.csproj", "src/AlertOrchestrator.Infrastructure/"]
RUN dotnet restore "src/AlertOrchestrator.Api/AlertOrchestrator.Api.csproj"
COPY . .
WORKDIR "/src/AlertOrchestrator.Api"
RUN dotnet build "./AlertOrchestrator.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AlertOrchestrator.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlertOrchestrator.Api.dll"]
