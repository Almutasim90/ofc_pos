# syntax=docker/dockerfile:1

# ---- Build the SPA ----
FROM node:22-alpine AS webbuild
WORKDIR /src/apps/web
COPY apps/web/package.json apps/web/package-lock.json* ./
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi
COPY apps/web/ ./
RUN npm run build

# ---- Build and publish the API, embedding the SPA ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/ ./
RUN dotnet restore src/OFC.Api/OFC.Api.csproj
RUN dotnet publish src/OFC.Api/OFC.Api.csproj -c Release -o /app /p:UseAppHost=false
# Copy the built SPA into the API's web root so the same container serves it.
COPY --from=webbuild /src/apps/web/dist /app/wwwroot

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OFC.Api.dll"]
