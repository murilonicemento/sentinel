FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY --from=repo-root Directory.Packages.props /

COPY src/AuditCompliance.Api/AuditCompliance.Api.csproj src/AuditCompliance.Api/
COPY src/AuditCompliance.Application/AuditCompliance.Application.csproj src/AuditCompliance.Application/
COPY src/AuditCompliance.Domain/AuditCompliance.Domain.csproj src/AuditCompliance.Domain/
COPY src/AuditCompliance.Infrastructure/AuditCompliance.Infrastructure.csproj src/AuditCompliance.Infrastructure/

RUN dotnet restore src/AuditCompliance.Api/AuditCompliance.Api.csproj

COPY src ./src
RUN dotnet publish src/AuditCompliance.Api/AuditCompliance.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "AuditCompliance.Api.dll"]
