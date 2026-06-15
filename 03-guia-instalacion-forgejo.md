# 3. Guía Rápida de Instalación y Configuración de Forgejo + Runners

## 📌 ¿Qué es Forgejo?

**Forgejo** es un servidor Git auto-alojado, ligero y de código abierto (fork de Gitea). Incluye **Forgejo Actions** — un sistema nativo de CI/CD compatible con GitHub Actions — permitiendo ejecutar pipelines automatizados sin depender de servicios externos.

> **Requisitos mínimos:** Docker Engine 20.10+, 1 CPU, 512 MB RAM, 1 GB disco.

---

## 3.1 Instalación de Forgejo con Docker Compose

### Paso 1: Crear archivo `docker-compose.yml`

Usamos **socket Docker directo** en lugar de Docker-in-Docker (DIND). Esto evita problemas de resolución DNS entre contendedores, reduce la sobrecarga y simplifica la red.

```yaml
services:
  forgejo:
    image: codeberg.org/forgejo/forgejo:15
    container_name: forgejo
    environment:
      - USER_UID=1000
      - USER_GID=1000
      - FORGEJO__server__ROOT_URL=http://localhost:3000
      - FORGEJO__server__HTTP_PORT=3000
      - FORGEJO__actions__ENABLED=true
    volumes:
      - ./forgejo-data:/data
      - /etc/timezone:/etc/timezone:ro
      - /etc/localtime:/etc/localtime:ro
    ports:
      - "3000:3000"
      - "2222:22"
    restart: unless-stopped

  forgejo-runner:
    image: data.forgejo.org/forgejo/runner:12
    container_name: forgejo-runner
    environment:
      - DOCKER_HOST=unix:///var/run/docker.sock
    depends_on:
      - forgejo
    volumes:
      - ./runner-config:/data
      - /var/run/docker.sock:/var/run/docker.sock
    restart: unless-stopped
    command: "forgejo-runner daemon --config /data/config.yaml"
```

**Diferencias clave respecto al enfoque DIND tradicional:**
| Aspecto | DIND (descartado) | Socket Docker (usado) |
|---------|-------------------|----------------------|
| Conectividad Docker | TCP a contenedor `docker:dind` | Socket Unix del host |
| Red de jobs | Aislada, problemas DNS | Red `forgejo-server_default` compartida |
| Seguridad | Mayor aislamiento | Más simple, suficiente para demo |
| Resolución DNS entre contenedores | Problemática | Resolución por nombre funcionando |

### Paso 2: Crear directorios e iniciar servicios

```bash
# Crear directorios de datos
mkdir -p forgejo-data runner-config

# Iniciar contenedores
docker compose up -d

# Verificar que estén corriendo
docker compose ps
```

### Paso 3: Configuración inicial de Forgejo

1. Abrir en el navegador: **http://localhost:3000**
2. Pantalla de configuración inicial:
   - **Título del sitio:** `DevOps Lab`
   - **URL raíz:** `http://localhost:3000`
   - **Credenciales del admin:** Crear usuario (ej: `admin` / `Admin123!`)
3. Hacer clic en **"Instalar Forgejo"**

![Configuración inicial](https://forgejo.org/assets/images/screenshots/installation.png)

---

## 3.2 Configuración de Forgejo Actions

### Paso 1: Generar token de registro del Runner

```bash
# Opción A: Desde la UI de Forgejo
# Settings → Actions → Runners → Create registration token

# Opción B: Desde terminal
docker exec forgejo forgejo actions generate-runner-token
```

Salida esperada:
```
El token de registro del runner es: xYZabc123...
```

### Paso 2: Crear archivo de configuración del Runner

Crea `runner-config/config.yaml`:

```yaml
log:
  level: info
  job_level: info

runner:
  file: .runner
  capacity: 2
  timeout: 3h
  shutdown_timeout: 30m
  insecure: false
  fetch_timeout: 30s
  fetch_interval: 2s
  report_interval: 1s

container:
  network: "forgejo-server_default"
  privileged: false
  docker_host: "automount"
  force_pull: false
  force_rebuild: false
  valid_volumes:
    - '**'
```

**Parámetros críticos:**
| Parámetro | Valor | Por qué |
|-----------|-------|---------|
| `container.network` | `forgejo-server_default` | Jobs comparten red con Forgejo → acceso por nombre de contenedor |
| `container.docker_host` | `automount` | Monta automáticamente el socket Docker del host |
| `container.valid_volumes` | `['**']` | Permite montar cualquier volumen necesario |
| `runner.capacity` | `2` | Hasta 2 jobs simultáneos |

### Paso 3: Registrar el Runner (desde la web — más fácil)

Primero levantar los servicios:

```bash
docker compose up -d
```

Luego, desde la interfaz web de Forgejo:

1. Ir a **Site Administration** (⚙️ → "Site Administration")
2. **Actions** → **Runners**
3. Click en **"Set up runner"**
4. Se genera un UUID + Token para el runner. Copia esos valores.

![Set up runner en Forgejo](https://forgejo.org/assets/images/screenshots/runner-setup.png)

Agrega el bloque `server.connections` en `runner-config/config.yaml` con los valores copiados:

```yaml
server:
  connections:
    devops-runner:
      url: http://forgejo:3000
      uuid: "e69df7ba-ee0e-404b-99e1-d5853ac3218f"
      token: "cc42bf4b17c6eb7a290b0e1ba442d308e320d928"
```

> **Importante:** La URL debe ser `http://forgejo:3000` (nombre del servicio Docker, **no** `localhost`). Si usas `localhost`, el runner intentará conectarse a sí mismo y fallará con `connection refused`.

Luego agrega también los labels que usará el runner para ejecutar jobs:

```yaml
server:
  connections:
    devops-runner:
      url: http://forgejo:3000
      uuid: "e69df7ba-ee0e-404b-99e1-d5853ac3218f"
      token: "cc42bf4b17c6eb7a290b0e1ba442d308e320d928"
      labels:
        - ubuntu-latest:docker://node:20-bookworm
        - dotnet:docker://mcr.microsoft.com/dotnet/sdk:8.0
```

Guarda el archivo. El runner se conectará automáticamente al iniciar — no necesitas ejecutar `forgejo-runner register`.

### Paso 4: Verificar el Runner

```bash
docker compose start forgejo-runner
```

En **Site Administration → Actions → Runners** de Forgejo, debe aparecer el runner como `idle`:

```
Runner: devops-runner   Status: 🟢 Idle   Labels: ubuntu-latest, dotnet
```

Para verificar el estado desde terminal:

```bash
docker logs forgejo-runner --tail 10
```
Deberías ver:
```
runner: devops-runner, with version: v12.11.1, with labels: [ubuntu-latest dotnet], ephemeral: false, declared successfully
[poller] launched
```

---

## 3.3 Configuración de Labels (Entornos de Ejecución)

Los **labels** definen en qué contenedor se ejecutará cada job del pipeline:

| Label | Imagen | Uso |
|-------|--------|-----|
| `ubuntu-latest` | `node:20-bookworm` | Compilación, pruebas HTTP, deploy |
| `dotnet` | `mcr.microsoft.com/dotnet/sdk:8.0` | Build y test .NET |

Los labels se declaran dentro del bloque `server.connections` en `config.yaml`, bajo la conexión del runner:

> **Nota:** El runner ejecuta jobs en contenedores efímeros que comparten la red `forgejo-server_default`. Esto permite que los jobs accedan a contenedores de aplicación por nombre (ej: `http://devops-demo-test:5000`), resolviendo los problemas de resolución DNS típicos de DIND.

---

## 3.4 Troubleshooting Común

### Error: "Cannot connect to the Docker daemon"

```bash
# Verificar que el socket Docker está montado
docker compose exec forgejo-runner ls -la /var/run/docker.sock

# Si no aparece, revisar docker-compose.yml
# Debe tener: volumes: [ "/var/run/docker.sock:/var/run/docker.sock" ]

# Verificar permisos del socket
ls -la /var/run/docker.sock
```

### Error: "Runner cannot connect to Forgejo"

**Causa más común:** La URL en `config.yaml` usa `localhost` en vez del nombre del servicio Docker.

```yaml
# ❌ MAL — localhost apunta al propio contenedor
url: http://localhost:3000

# ✅ BIEN — forgejo es el nombre del servicio en docker-compose
url: http://forgejo:3000
```

```bash
# Verificar redes
docker network ls
docker network inspect forgejo-server_default

# Hacer ping desde runner a forgejo
docker exec forgejo-runner ping forgejo -c 3
```

### Error: "Job stuck / no runner available"

```bash
# Verificar que el runner está registrado
cat runner-config/config.yaml | grep -A5 "connections"

# Verificar logs del runner
docker logs forgejo-runner --tail 50

# El label del job YAML debe coincidir con el label del runner
# En el YAML: runs-on: ubuntu-latest
# En config: labels: [ "ubuntu-latest:docker://node:20-bookworm" ]
```

### Error: "Port already in use" en deploy

```bash
# Verificar qué ocupa el puerto
ss -tlnp | grep ':80 '

# En la demo usamos puerto 8080 para evitar conflictos
# Ver docker logs del deploy fallido
docker logs devops-demo-prod --tail 20
```

### Error: "unable to find user appuser"

```bash
# El Dockerfile debe crear el usuario antes de usarlo
# Verificar que tiene estas líneas:
#   RUN adduser --disabled-password --gecos "" --no-create-home appuser && \
#       chown -R appuser:appuser /app
#   USER appuser
```

### Error de caché del runner: "permission denied" en `/data/.cache/actcache/`

No crítico — solo un warning. El runner intenta usar un cache server interno pero falla por permisos. No afecta la ejecución del pipeline.

### Reiniciar desde cero

```bash
docker compose down -v
rm -rf forgejo-data runner-config
# Volver al Paso 2
```

---

## 3.5 Arquitectura Final

```
                    ┌──────────────────────┐
                    │      Navegador       │
                    │   http://localhost   │
                    └──────────┬───────────┘
                               │ :3000 / :8080
                    ┌──────────▼───────────┐
                    │      FORGEJO         │
                    │  Servidor Git + CI   │
                    │  + Forgejo Actions   │
                    └──────────┬───────────┘
                               │ red: forgejo-server_default
                    ┌──────────▼───────────┐
                    │   FORGEJO RUNNER     │
                    │  (Agente Dockerizado)│
                    │  ┌─────────────────┐ │
                    │  │ Contenedor Job  │ │
                    │  │  (efímero)      │ │
                    │  │   node:20       │ │
                    │  └────────┬────────┘ │
                    │           │          │
                    │  socket: /var/run/docker.sock
                    └──────────────────────┘
                               │
                    ┌──────────▼───────────┐
                    │  Contenedores App    │
                    │  (devops-demo-test,  │
                    │   devops-demo-prod)  │
                    └──────────────────────┘
```

**Flujo de CI/CD:**
1. `git push` → Forgejo detecta y crea tareas en Actions
2. Runner recoge la tarea, crea un job container efímero (node:20-bookworm)
3. Job accede a Docker via socket para construir imágenes y levantar contenedores
4. Los contenedores de aplicación comparten red con Forgejo → accesibles por nombre desde los jobs
5. Pipeline 4 fases: `TDD → Docker Build → Pruebas HTTP → Deploy`

---

## 📚 Referencias

- Forgejo Documentation: *https://forgejo.org/docs/latest/admin/installation/*
- Forgejo Actions: *https://forgejo.org/docs/next/user/actions/*
- Forgejo Runner: *https://codeberg.org/forgejo/runner*
