# 4. Guía de Configuración del Pipeline CI/CD en Forgejo Actions

## 📌 Visión General

Este documento detalla cómo configurar un **pipeline CI/CD completo de 4 fases** en Forgejo Actions para la aplicación .NET del caso práctico. El pipeline se ejecuta automáticamente con cada `git push` al repositorio.

**Stack:** Forgejo Actions (compatible GitHub Actions) → Shell commands + Docker socket → node:20-bookworm.

---

## 4.1 Arquitectura del Pipeline

```
                    git push
                       │
                       ▼
        ┌──────────────────────────────┐
        │   FASE 1: TDD (xUnit)        │
        │   Compilar + Pruebas         │
        │   Unitarias (15 tests)       │
        └──────────┬───────────────────┘
                   │ ¿Tests OK?
              ┌────┴────┐
              │  ✔ Sí   │  ✘ No → ❌ Pipeline falla
              └────┬────┘
                   ▼
        ┌──────────────────────────────┐
        │   FASE 2: Docker Build       │
        │   Multi-stage build (shell)  │
        │   docker build -t ... .      │
        └──────────┬───────────────────┘
                   │ ¿Build OK?
              ┌────┴────┐
              │  ✔ Sí   │  ✘ No → ❌ Pipeline falla
              └────┬────┘
                   ▼
        ┌──────────────────────────────┐
        │   FASE 3: Pruebas HTTP       │
        │   curl contra contenedor     │
        │   temporal en red compartida │
        └──────────┬───────────────────┘
                   │ ¿Tests OK?
              ┌────┴────┐
              │  ✔ Sí   │  ✘ No → ❌ Pipeline falla
              └────┬────┘
                   ▼
        ┌──────────────────────────────┐
        │   FASE 4: Deploy Automático  │
        │   docker run --network ...   │
        │   -p 8080:5000 en Producción │
        └──────────────────────────────┘
                   │
                   ▼
            ✅ PIPELINE EXITOSO
            http://localhost:8080
```

---

## 4.2 El Archivo `.forgejo/workflows/ci-cd.yml`

Forgejo Actions utiliza archivos YAML en el directorio `.forgejo/workflows/` (o `.gitea/workflows/`). Cada archivo define uno o más **jobs** que se ejecutan secuencialmente mediante la directiva `needs:`.

### Ubicación en el repositorio

```
dotnet-app/
├── .forgejo/
│   └── workflows/
│       └── ci-cd.yml        ← Pipeline principal (4 fases)
├── src/
│   └── MinimalWebApi.csproj
├── tests/
│   └── MinimalWebApi.Tests.csproj
├── Dockerfile
└── ...
```

### Pipeline completo (`ci-cd.yml`)

```yaml
# .forgejo/workflows/ci-cd.yml
# Pipeline CI/CD de 4 fases: TDD → Docker → HTTP Tests → Deploy
name: CI/CD Pipeline - DevOps Demo
run-name: >
  Pipeline #{{ github.sha | truncate(7, '') }} |
  {{ github.actor }} |
  {{ github.ref }}

on:
  push:
    branches:
      - main
      - master
      - develop
  pull_request:
    branches:
      - main

env:
  DOTNET_VERSION: "8.0"
  IMAGE_NAME: "devops-demo-api"
  IMAGE_TAG: "latest"
  APP_PORT: 5000

jobs:

  # ============================================
  # FASE 1: TDD - PRUEBAS UNITARIAS (xUnit)
  # ============================================
  fase-1-tdd:
    name: "Fase 1: TDD - Pruebas Unitarias xUnit"
    runs-on: ubuntu-latest
    steps:
      - name: Checkout codigo fuente
        uses: actions/checkout@v4

      - name: Configurar .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restaurar dependencias
        run: |
          echo "Restaurando paquetes NuGet..."
          dotnet restore MinimalWebApi.sln

      - name: Compilar solucion
        run: |
          echo "Compilando solucion..."
          dotnet build MinimalWebApi.sln \
            --configuration Release \
            --no-restore

      - name: Ejecutar pruebas unitarias (xUnit)
        run: |
          echo "Ejecutando pruebas unitarias TDD..."
          dotnet test tests/MinimalWebApi.Tests.csproj \
            --configuration Release \
            --no-build \
            --logger "console;verbosity=detailed" \
            --logger "trx;LogFileName=test-results.trx" \
            --results-directory ./TestResults

      - name: Publicar resultados de pruebas
        if: always()
        run: |
          echo "Resultados de pruebas guardados en ./TestResults"
          ls -la ./TestResults/

  # ============================================
  # FASE 2: CONSTRUCCION DE CONTENEDOR
  # ============================================
  fase-2-docker:
    name: "Fase 2: Build de Contenedor Multi-Etapa"
    needs: fase-1-tdd           # Solo si Fase 1 pasa
    runs-on: ubuntu-latest
    steps:
      - name: Checkout codigo fuente
        uses: actions/checkout@v4

      - name: Instalar Docker CLI
        run: |
          apt-get update -qq && apt-get install -y -qq docker.io 2>&1 | tail -3

      - name: Build y exportar imagen Docker
        run: |
          echo "Construyendo imagen Docker multi-etapa..."
          docker build \
            -f ./Dockerfile \
            -t ${{ env.IMAGE_NAME }}:${{ env.IMAGE_TAG }} \
            -t ${{ env.IMAGE_NAME }}:${{ github.sha }} \
            .

      - name: Verificar imagen creada
        run: |
          echo "Imagenes generadas:"
          docker images --filter "reference=${{ env.IMAGE_NAME }}" \
            --format "table {{.Repository}}:{{.Tag}}"

  # ============================================
  # FASE 3: PRUEBAS DE INTEGRACION HTTP (curl)
  # ============================================
  fase-3-uat:
    name: "Fase 3: Pruebas de Integracion HTTP"
    needs: fase-2-docker         # Solo si Fase 2 pasa
    runs-on: ubuntu-latest
    steps:
      - name: Checkout codigo fuente
        uses: actions/checkout@v4

      - name: Instalar Docker CLI
        run: |
          apt-get update -qq && apt-get install -y -qq docker.io 2>&1 | tail -3

      - name: Levantar contenedor temporal de la aplicacion
        run: |
          echo "Iniciando contenedor de prueba..."
          docker run -d \
            --name devops-demo-test \
            --network="forgejo-server_default" \
            -e ASPNETCORE_URLS=http://+:${{ env.APP_PORT }} \
            -e ASPNETCORE_ENVIRONMENT=Staging \
            ${{ env.IMAGE_NAME }}:${{ env.IMAGE_TAG }}

          echo "Esperando que la aplicacion responda..."
          for i in $(seq 1 15); do
            if curl -s http://devops-demo-test:${{ env.APP_PORT }}/health > /dev/null 2>&1; then
              echo "Aplicacion lista en el intento $i"
              break
            fi
            echo "   Intento $i - esperando..."
            sleep 2
          done

      - name: Pruebas de integracion HTTP (endpoints REST)
        run: |
          set -e
          BASE="http://devops-demo-test:${{ env.APP_PORT }}"
          echo "=== 1. Health Check ==="
          curl -s "$BASE/health" | python3 -m json.tool

          echo ""
          echo "=== 2. Registrar usuario ==="
          RES=$(curl -s -X POST "$BASE/api/auth/register" \
            -H "Content-Type: application/json" \
            -d '{"username":"demo","email":"demo@test.com"}')
          echo "$RES" | python3 -m json.tool

          echo ""
          echo "=== 3. Obtener perfil de usuario ==="
          curl -s "$BASE/api/auth/me?username=demo" | python3 -m json.tool

          echo ""
          echo "=== 4. Crear tarea ==="
          RES=$(curl -s -X POST "$BASE/api/tasks" \
            -H "Content-Type: application/json" \
            -d '{"title":"Tarea demo","description":"Demo para exposicion","assignedTo":"demo"}')
          echo "$RES" | python3 -m json.tool

          echo ""
          echo "=== 5. Listar tareas ==="
          curl -s "$BASE/api/tasks" | python3 -m json.tool

          echo ""
          echo "=== 6. Obtener tarea por ID ==="
          curl -s "$BASE/api/tasks/2" | python3 -m json.tool

          echo ""
          echo "=== 7. Health final ==="
          curl -s "$BASE/health"
          echo ""
          echo "Pruebas de integracion HTTP completadas exitosamente"

      - name: Limpiar contenedor de prueba
        if: always()
        run: |
          echo "Limpiando contenedor temporal..."
          docker stop devops-demo-test || true
          docker rm devops-demo-test || true

  # ============================================
  # FASE 4: DEPLOY AUTOMATICO A PRODUCCION
  # ============================================
  fase-4-deploy:
    name: "Fase 4: Deploy a Produccion"
    needs: fase-3-uat             # Solo si Fase 3 pasa
    runs-on: ubuntu-latest
    if: github.ref_name == 'main' || github.ref_name == 'master'
    steps:
      - name: Instalar Docker CLI
        run: |
          apt-get update -qq && apt-get install -y -qq docker.io 2>&1 | tail -3

      - name: Detener contenedor actual (si existe)
        continue-on-error: true
        run: |
          echo "Deteniendo contenedor anterior..."
          docker stop devops-demo-prod || true
          docker rm devops-demo-prod || true

      - name: Desplegar nueva version
        run: |
          echo "Desplegando nueva version..."
          docker run -d \
            --name devops-demo-prod \
            --restart unless-stopped \
            --network="forgejo-server_default" \
            -p 8080:${{ env.APP_PORT }} \
            -e ASPNETCORE_URLS=http://+:${{ env.APP_PORT }} \
            -e ASPNETCORE_ENVIRONMENT=Production \
            -v app-data:/app/data \
            ${{ env.IMAGE_NAME }}:${{ env.IMAGE_TAG }}

      - name: Verificar despliegue
        run: |
          echo "Verificando despliegue..."
          sleep 5
          for i in $(seq 1 12); do
            if curl -s http://devops-demo-prod:${{ env.APP_PORT }}/health > /dev/null 2>&1; then
              echo "Despliegue verificado exitosamente"
              curl -s http://devops-demo-prod:${{ env.APP_PORT }}/health
              break
            fi
            if [ $i -eq 12 ]; then
              echo "El despliegue no respondio despues de 60 segundos"
              docker logs devops-demo-prod --tail 30
              exit 1
            fi
            echo "   Intento $i - esperando..."
            sleep 5
          done

      - name: Limpiar imagenes antiguas
        continue-on-error: true
        run: |
          echo "Limpiando imagenes antiguas..."
          docker image prune -f --filter "until=24h"

      - name: Notificar exito
        run: |
          echo "============================================"
          echo "  PIPELINE COMPLETADO CON EXITO"
          echo "  Commit: ${{ github.sha }}"
          echo "  Imagen: ${{ env.IMAGE_NAME }}:${{ env.IMAGE_TAG }}"
          echo "  URL: http://localhost:8080"
          echo "============================================"
```

---

## 4.3 Desglose Detallado de Cada Fase

### 🔬 Fase 1: TDD (xUnit)

| Aspecto | Detalle |
|---------|---------|
| **Propósito** | Validar que la lógica de negocio cumple los contratos definidos por las pruebas |
| **Herramienta** | xUnit + Moq (mocks) + .NET SDK 8.0 |
| **Trigger** | `git push` a branches `main`, `master` o `develop` |
| **Pasos** | Checkout → Setup .NET → Restore → Build → Test |
| **Criterio de éxito** | 15/15 pruebas unitarias pasan (código de salida 0 de `dotnet test`) |
| **Si falla** | El pipeline se detiene. No continúa a Fase 2. |

**Principio TDD aplicado:** Las pruebas en `tests/UnitTests/` validan:
- Registro de usuarios (username duplicado, email inválido)
- Login con credenciales válidas/inválidas
- CRUD de tareas (operaciones concurrentes con `SemaphoreSlim`)
- Hash de contraseñas con BCrypt

### 🐳 Fase 2: Construcción de Contenedor

| Aspecto | Detalle |
|---------|---------|
| **Propósito** | Empaquetar la aplicación en una imagen Docker inmutable y optimizada |
| **Estrategia** | Multi-stage build (SDK → Test → Runtime) |
| **Pasos** | Checkout → Instalar Docker CLI → `docker build` → Verificar |
| **Tags generados** | `devops-demo-api:latest` y `devops-demo-api:<sha>` |
| **Duración típica** | ~1 minuto (con caché de capas Docker) |

**Ventajas del multi-stage:**
```
Imagen con SDK:  >1.2 GB
Imagen Runtime:  ~210 MB (Ahorro ≈ 80%)
```

### 🌐 Fase 3: Pruebas de Integración HTTP

| Aspecto | Detalle |
|---------|---------|
| **Propósito** | Validar que los endpoints REST funcionan correctamente en entorno real |
| **Herramienta** | curl + python3 (preinstalados en node:20-bookworm) |
| **Infraestructura** | Contenedor Docker temporal en red `forgejo-server_default` |
| **Pruebas** | Health check, registrar usuario, obtener perfil, CRUD tareas |
| **Acceso** | Por nombre de contenedor: `http://devops-demo-test:5000` |
| **Limpieza** | `docker stop/rm` en `if: always()` |

**¿Por qué curl en vez de Playwright?**
| Enfoque | Problema |
|---------|----------|
| Playwright .NET NuGet | `Microsoft.Playwright.CLI` solo llega a v1.2.3, sin `playwright install` |
| npx playwright | Descarga Chromium cada run (~2 min extra, ~200 MB) |
| **curl + python3** | Instantáneo, sin dependencias extra, suficiente para backend REST |

**Flujo de pruebas HTTP:**
```
1. docker run -d --network=forgejo-server_default ... devops-demo-api
2. Esperar health check (polling cada 2s, máximo 30s)
3. curl a cada endpoint REST
4. Validar respuesta con python3 -m json.tool
5. Detener y eliminar el contenedor
```

### 🚀 Fase 4: Deploy Automático

| Aspecto | Detalle |
|---------|---------|
| **Propósito** | Desplegar la imagen validada en el entorno de producción local |
| **Condición** | Solo en `main`/`master` (no en `develop`), usando `github.ref_name` |
| **Estrategia** | Blue/Green simple: detener contenedor anterior → iniciar nuevo |
| **Red** | `forgejo-server_default` para consistencia con Fase 3 |
| **Puerto** | `8080:5000` (evita conflictos con servicios existentes en puerto 80) |
| **Verificación** | Health check por nombre de contenedor cada 5s durante 60s |
| **Persistencia** | Volumen `app-data` montado en `/app/data` para datos de producción |

---

## 4.4 Variables de Entorno

| Variable | Valor por defecto | Descripción |
|----------|-------------------|-------------|
| `DOTNET_VERSION` | `8.0` | Versión del SDK .NET |
| `IMAGE_NAME` | `devops-demo-api` | Nombre de la imagen Docker |
| `IMAGE_TAG` | `latest` | Tag de la imagen |
| `APP_PORT` | `5000` | Puerto interno de la aplicación |

---

## 4.5 Ejecución del Pipeline Paso a Paso

### Prerrequisitos en el Runner

El runner de Forgejo debe tener:
- Socket Docker montado (`/var/run/docker.sock`)
- Red compartida `forgejo-server_default`
- `container.docker_host: automount` en config.yaml

### Paso 1: Clonar el repositorio localmente

```bash
git clone http://localhost:3000/<usuario>/devops-lab.git
cd devops-lab
```

### Paso 2: Crear el directorio de workflows

```bash
mkdir -p .forgejo/workflows
# Copiar el archivo ci-cd.yml
cp /ruta/a/ci-cd.yml .forgejo/workflows/ci-cd.yml
```

### Paso 3: Commit y push

```bash
git add -A
git commit -m "feat: pipeline CI/CD completo con 4 fases"
git push origin main
```

### Paso 4: Monitorear en vivo

1. Ir a **Forgejo UI → Repositorio → Actions**
2. Ver el pipeline ejecutándose en tiempo real
3. Cada job expandible para ver logs detallados
4. El pipeline completo tarda ~5-7 minutos (Fase 1: ~2 min, Fase 2: ~1 min, Fase 3: ~2 min, Fase 4: ~1 min)

---

## 4.6 Escenarios: Simular Error y Corrección

### Escenario 1: Error en pruebas unitarias

```csharp
// Introducir un error intencional en un test
public class UserServiceTests
{
    [Fact]
    public void Login_ValidCredentials_ReturnsSuccess()
    {
        var service = new UserService();
        var request = new LoginRequest { Username = "admin", Password = "WRONG_PASSWORD" };
        var result = service.Login(request);
        Assert.True(result.Success); // ❌ Falla: password incorrecto
    }
}
```

**Resultado en el pipeline:**
```
FASE 1 - TDD: ❌ FALLÓ
  ✘ Login_ValidCredentials_ReturnsSuccess [737ms]
  Error: Assert.True() Failure: Expected True, actual False

Pipeline detenido. No continúa a Fase 2.
```

**Corrección:** Arreglar el test y hacer push → el pipeline se re-ejecuta automáticamente.

### Escenario 2: Error en pruebas HTTP

```bash
# Simular que un endpoint devuelve estructura incorrecta
curl -s http://devops-demo-test:5000/api/tasks/99  # ID inexistente
# → 404 Not Found → python3 -m json.tool falla por respuesta vacía
```

**Resultado:**
```
FASE 3 - HTTP: ❌ FALLÓ
  Expecting value: line 1 column 1 (char 0)
  → 404 devuelve Content-Length: 0

Imagen no desplegada. Pipeline detenido.
```

### Escenario 3: Error de red entre contenedores

```bash
# Si el contenedor de prueba no comparte la red:
docker run -d --name devops-demo-test ...  # Sin --network
# → curl http://devops-demo-test:5000/health
# → Could not resolve host: devops-demo-test
```

**Solución:** Agregar `--network="forgejo-server_default"` al `docker run`.

---

## 4.7 Extensión del Pipeline

### Agregar notificaciones

```yaml
notify:
  name: "Notificar estado del pipeline"
  needs: [fase-1-tdd, fase-2-docker, fase-3-uat, fase-4-deploy]
  if: always()
  runs-on: ubuntu-latest
  steps:
    - name: Enviar notificacion
      run: |
        if [ "${{ needs.fase-4-deploy.result }}" == "success" ]; then
          echo "Pipeline completado exitosamente"
          # curl -X POST https://hooks.slack.com/... -d '{"text":" Pipeline exitoso"}'
        else
          echo "Pipeline fallo en alguna fase"
          # curl -X POST https://hooks.slack.com/... -d '{"text":" Pipeline fallo"}'
        fi
```

### Agregar pruebas de estrés concurrente

```yaml
- name: Prueba de estres concurrente (Opcional)
  run: |
    echo "Ejecutando prueba de estres con 50 usuarios concurrentes..."
    docker run --rm -i grafana/k6 run - <<EOF
      import http from 'k6/http';
      import { check } from 'k6';

      export const options = {
        vus: 50,
        duration: '30s',
      };

      export default function () {
        const res = http.get('http://devops-demo-prod:5000/health');
        check(res, { 'status was 200': (r) => r.status === 200 });
      }
    EOF
```

### Usar imagen específica por SHA en deploy

```yaml
- name: Desplegar version especifica
  run: |
    docker run -d \
      --name devops-demo-prod \
      --network="forgejo-server_default" \
      -p 8080:5000 \
      ${{ env.IMAGE_NAME }}:${{ github.sha }}  # Tag = SHA del commit
```

---

## 4.8 Lecciones Aprendidas (Troubleshooting Real)

Estos son problemas reales encontrados durante la implementación y cómo se resolvieron:

| Problema | Causa | Solución |
|----------|-------|----------|
| `runs-on key not defined` | Jobs dependientes se ejecutan antes de que el job padre termine de parsear | Warning no crítico, ignorar |
| `Playwright CLI not found` | `Microsoft.Playwright.CLI` NuGet solo v1.2.3, sin `playwright install` | Reemplazar con curl + python3 |
| `unable to find user appuser` | Dockerfile usaba `USER appuser` sin `adduser` previo | Agregar `RUN adduser ... && chown ...` antes del `USER` |
| `address already in use` en puerto 80 | Puerto 80 ocupado por servicio del host | Cambiar a `-p 8080:5000` |
| `Could not resolve host` | Contenedor app en red distinta al job container | Usar `--network="forgejo-server_default"` |
| `/api/tasks/1` → 404 | Datos seed empiezan en ID 2, no 1 | Cambiar prueba a `/api/tasks/2` |
| `gitea.sha` no funciona | Forgejo 15 usa `github.*` en vez de `gitea.*` | Usar `github.sha`, `github.actor`, `github.ref`, `github.ref_name` |
| `docker: command not found` | Imagen node:20-bookworm no incluye Docker CLI | `apt-get install -y docker.io` |
| Cache runner: `permission denied` | `/data/.cache/actcache/` sin permisos | No crítico, ignorar |

---

## 📚 Referencias

- Forgejo Actions Docs: *https://forgejo.org/docs/next/user/actions/*
- Forgejo Workflow Syntax: *https://forgejo.org/docs/next/user/actions-syntax/*
- Forgejo Runner Config: *https://codeberg.org/forgejo/runner*
- GitHub Actions (compatible): *https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions*
