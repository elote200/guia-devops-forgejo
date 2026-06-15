# ============================================
# ETAPA 1: BUILD
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de proyecto y restaurar dependencias (capa de caché)
COPY src/MinimalWebApi.csproj .
RUN dotnet restore

# Copiar el resto del código fuente
COPY src/ .

# Compilar y publicar en modo Release
RUN dotnet publish -c Release -o /app/publish

# ============================================
# ETAPA 2: TEST UNITARIO (imagen intermedia)
# ============================================
FROM build AS test
WORKDIR /src

# Copiar proyectos de test
COPY tests/MinimalWebApi.Tests.csproj tests/
RUN dotnet restore tests/MinimalWebApi.Tests.csproj

# Copiar fuentes de test
COPY tests/ tests/

# Ejecutar pruebas unitarias
RUN dotnet test tests/MinimalWebApi.Tests.csproj \
    --configuration Release \
    --logger "trx;LogFileName=test_results.trx" \
    --results-directory /testresults

# ============================================
# ETAPA 3: RUNTIME (producción)
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copiar binarios compilados desde build
COPY --from=build /app/publish .

# Variables de entorno para producción
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

EXPOSE 5000

# Crear usuario no-root para seguridad y darle permisos
RUN adduser --disabled-password --gecos "" --no-create-home appuser && \
    chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "MinimalWebApi.dll"]
