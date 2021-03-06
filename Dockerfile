# syntax=docker/dockerfile:1

# ---- Angular client ---------------------------------------------------------
FROM node:12-alpine AS client
WORKDIR /client
COPY client/package.json ./
RUN npm install --no-audit --no-fund
COPY client/ ./
RUN npm run build:prod

# ---- .NET build -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY nuget.config Directory.Build.props FleetTracker.sln ./
COPY src/FleetTracker.Core/FleetTracker.Core.csproj src/FleetTracker.Core/
COPY src/FleetTracker.Infrastructure/FleetTracker.Infrastructure.csproj src/FleetTracker.Infrastructure/
COPY src/FleetTracker.Api/FleetTracker.Api.csproj src/FleetTracker.Api/
RUN dotnet restore src/FleetTracker.Api/FleetTracker.Api.csproj
COPY src/ src/
RUN dotnet publish src/FleetTracker.Api/FleetTracker.Api.csproj -c Release -o /app --no-restore

# ---- Runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY --from=client /client/dist/fleet-client ./wwwroot
RUN mkdir -p /data
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Fleet="Data Source=/data/fleet.db"
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "FleetTracker.Api.dll"]
