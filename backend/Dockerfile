# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
# dotnet restore's default massively-parallel HTTP requests can overwhelm a container's bridge
# network and hit NuGet's 100s timeout even though the network itself is fine (curl succeeds,
# HttpClient doesn't — see dotnet/dotnet-docker#6844). --disable-parallel avoids that, and the
# retry loop rides out any remaining transient blip instead of failing the whole build on it.
RUN for i in 1 2 3; do dotnet restore src/OFC.Api/OFC.Api.csproj --disable-parallel && exit 0; echo "dotnet restore attempt $i failed, retrying in 5s..."; sleep 5; done; exit 1
RUN dotnet publish src/OFC.Api/OFC.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Install curl so the Docker healthcheck can probe /health (the default
# aspnet runtime image does not ship it).
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "OFC.Api.dll"]
