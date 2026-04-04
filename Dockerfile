# ============================================================
# Tarea 81: Multi-stage Docker build for production optimization
# Build stage: compile + publish
# Runtime stage: minimal ASP.NET runtime (no SDK bloat)
# ============================================================

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY *.sln ./
COPY EncorelyDomain/ EncorelyDomain/
COPY EncorelyApplication/ EncorelyApplication/
COPY EncorelyInfrastructure/ EncorelyInfrastructure/
COPY EncorelyApi/ EncorelyApi/
COPY EncorelyWorker/ EncorelyWorker/

RUN dotnet restore

WORKDIR /src/EncorelyApi
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system encorely && adduser --system --ingroup encorely encorely
USER encorely

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "EncorelyApi.dll"]
