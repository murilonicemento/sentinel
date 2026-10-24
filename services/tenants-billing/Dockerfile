FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/TenantsBilling.Api/TenantsBilling.Api.csproj src/TenantsBilling.Api/
COPY src/TenantsBilling.Application/TenantsBilling.Application.csproj src/TenantsBilling.Application/
COPY src/TenantsBilling.Domain/TenantsBilling.Domain.csproj src/TenantsBilling.Domain/
COPY src/TenantsBilling.Infrastructure/TenantsBilling.Infrastructure.csproj src/TenantsBilling.Infrastructure/

RUN dotnet restore src/TenantsBilling.Api/TenantsBilling.Api.csproj

COPY src ./src
RUN dotnet publish src/TenantsBilling.Api/TenantsBilling.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TenantsBilling.Api.dll"]
