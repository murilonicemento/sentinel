FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /
COPY ["src/Reporting.Api/Reporting.Api.csproj", "src/Reporting.Api/"]
COPY ["src/Reporting.Application/Reporting.Application.csproj", "src/Reporting.Application/"]
COPY ["src/Reporting.Domain/Reporting.Domain.csproj", "src/Reporting.Domain/"]
COPY ["src/Reporting.Infrastructure/Reporting.Infrastructure.csproj", "src/Reporting.Infrastructure/"]
RUN dotnet restore "src/Reporting.Api/Reporting.Api.csproj"
COPY . .
WORKDIR "/src/Reporting.Api"
RUN dotnet build "./Reporting.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Reporting.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Reporting.Api.dll"]
