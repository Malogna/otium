# Otium server image. Build from the repository root (deploy/docker-compose.yml does this):
#   docker build -f src/Otium.Server/Dockerfile -t otium-server .
# The SQLite database lives on the /data volume; the server listens on plain HTTP port 8080 behind the HTTPS proxy.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY Directory.Build.props ./
COPY src/Otium.Core/Otium.Core.csproj src/Otium.Core/
COPY src/Otium.Server/Otium.Server.csproj src/Otium.Server/
RUN dotnet restore src/Otium.Server/Otium.Server.csproj

COPY src/Otium.Core/ src/Otium.Core/
COPY src/Otium.Server/ src/Otium.Server/
RUN dotnet publish src/Otium.Server/Otium.Server.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The image's non-root "app" user ($APP_UID, 1654) owns the data directory. A new named volume mounted here
# inherits that ownership.
RUN mkdir -p /data && chown "$APP_UID:$APP_UID" /data
VOLUME /data

COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Otium="Data Source=/data/otium.db"
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Otium.Server.dll"]
