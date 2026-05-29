# ============================================================
# Multi-stage Docker build for production optimization
# Build stage: compile + publish
# Runtime stage: minimal ASP.NET runtime (no SDK bloat)
# ============================================================

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy build configuration shared by all projects
COPY Directory.Build.props ./

# Copy only the .csproj files first to leverage Docker layer caching
# (restore is re-run only when a project file changes, not on every code edit)
COPY EncorelyModels/EncorelyModels.csproj EncorelyModels/
COPY EncorelyQuery/EncorelyQuery.csproj EncorelyQuery/
COPY EncorelyRepository/EncorelyRepository.csproj EncorelyRepository/
COPY EncorelyApplication/EncorelyApplication.csproj EncorelyApplication/
COPY EncorelyInfrastructure/EncorelyInfrastructure.csproj EncorelyInfrastructure/
COPY EncorelyApi/EncorelyApi.csproj EncorelyApi/

# Restore only the API and its transitive project dependencies
RUN dotnet restore EncorelyApi/EncorelyApi.csproj

# Copy the source of the projects the API depends on
COPY EncorelyModels/ EncorelyModels/
COPY EncorelyQuery/ EncorelyQuery/
COPY EncorelyRepository/ EncorelyRepository/
COPY EncorelyApplication/ EncorelyApplication/
COPY EncorelyInfrastructure/ EncorelyInfrastructure/
COPY EncorelyApi/ EncorelyApi/

RUN dotnet publish EncorelyApi/EncorelyApi.csproj -c Release -o /app/publish --no-restore

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
