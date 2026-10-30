# Imagen de producción de la API. No contiene secretos: todo llega por variables de entorno.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Encorely.Api/Encorely.Api.csproj src/Encorely.Api/
RUN dotnet restore src/Encorely.Api/Encorely.Api.csproj
COPY src/ src/
RUN dotnet publish src/Encorely.Api/Encorely.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Encorely.Api.dll"]
