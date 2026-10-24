FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ChannelsService.Api/ChannelsService.Api.csproj", "src/ChannelsService.Api/"]
COPY ["src/ChannelsService.Application/ChannelsService.Application.csproj", "src/ChannelsService.Application/"]
COPY ["src/ChannelsService.Domain/ChannelsService.Domain.csproj", "src/ChannelsService.Domain/"]
COPY ["src/ChannelsService.Infrastructure/ChannelsService.Infrastructure.csproj", "src/ChannelsService.Infrastructure/"]
RUN dotnet restore "src/ChannelsService.Api/ChannelsService.Api.csproj"
COPY . .
WORKDIR "/src/src/ChannelsService.Api"
RUN dotnet build "ChannelsService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ChannelsService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChannelsService.Api.dll"]
