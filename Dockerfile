# Etapa 1: build
# Usamos el SDK de .NET porque necesitamos restaurar dependencias y compilar la API.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copiamos primero los archivos principales del proyecto.
# Esto permite restaurar dependencias antes de copiar todo el código.
COPY DevOpsTp.sln ./
COPY src/DevOpsTp.Api/DevOpsTp.Api.csproj src/DevOpsTp.Api/
COPY tests/DevOpsTp.Tests/DevOpsTp.Tests.csproj tests/DevOpsTp.Tests/

RUN dotnet restore

# Copiamos el resto del código fuente.
COPY . .

# Publicamos la API en modo Release.
RUN dotnet publish src/DevOpsTp.Api/DevOpsTp.Api.csproj -c Release -o /app/publish --no-restore

# Etapa 2: runtime
# Usamos una imagen más liviana, solo con lo necesario para ejecutar la API.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG APP_VERSION=local
ARG GIT_COMMIT=local
ARG BUILD_DATE=local

ENV APP_VERSION=$APP_VERSION
ENV GIT_COMMIT=$GIT_COMMIT
ENV BUILD_DATE=$BUILD_DATE

WORKDIR /app

# Instalamos curl para que Docker pueda validar el healthcheck y creamos un usuario sin privilegios.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && groupadd --system appgroup \
    && useradd --system --gid appgroup --home-dir /app --shell /usr/sbin/nologin appuser \
    && rm -rf /var/lib/apt/lists/*

# La API escucha en el puerto 8080 dentro del contenedor.
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Copiamos la aplicación ya publicada desde la etapa de build.
COPY --from=build --chown=appuser:appgroup /app/publish .

# Ejecutamos la API con un usuario no-root.
USER appuser

# Docker verifica periódicamente que la API responda correctamente.
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DevOpsTp.Api.dll"]
