FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["server/PoolTracker.Api/PoolTracker.Api.csproj", "server/PoolTracker.Api/"]
RUN dotnet restore "server/PoolTracker.Api/PoolTracker.Api.csproj"

COPY server/PoolTracker.Api/. server/PoolTracker.Api/
WORKDIR /src/server/PoolTracker.Api
RUN dotnet publish "PoolTracker.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PoolTracker.Api.dll"]
