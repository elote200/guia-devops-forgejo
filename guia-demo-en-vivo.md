# 🎤 Guía de Presentación — Demo en Vivo del Pipeline CI/CD

> **Duración estimada:** 15-20 minutos
> **Formato:** Slides → Demo en vivo → Conclusión

---

## 📺 Configuración de Pantalla Recomendada

```
┌─────────────────────────┬─────────────────────────┐
│      PANTALLA 1         │      PANTALLA 2         │
│    (Proyector/Demo)     │    (Tu monitor)         │
│                         │                         │
│  Split en 2 mitades:    │  Terminales de respaldo:│
│                         │                         │
│  ┌───────────────────┐  │  - docker logs (tail)   │
│  │  Forgejo UI       │  │  - curl endpoints       │
│  │  (Actions tab)    │  │  - docker ps            │
│  ├───────────────────┤  │                         │
│  │  Terminal:        │  │                         │
│  │  curl/health      │  │                         │
│  └───────────────────┘  │                         │
│                         │                         │
└─────────────────────────┴─────────────────────────┘
```

**Atajos de teclado útiles:**
| Acción | Comando |
|--------|---------|
| Limpiar terminal | `Ctrl+L` |
| Nueva terminal | `Ctrl+Shift+T` |
| Split terminal | `Ctrl+Shift+5` |

---

## 📋 Índice de la Presentación

| # | Segmento | Tiempo | Slides |
|---|----------|--------|--------|
| 1 | Introducción DevOps & CI/CD | 3 min | 3-4 slides |
| 2 | Arquitectura del proyecto | 2 min | 1-2 slides |
| 3 | Demo en vivo 🎬 | 8 min | — |
| 4 | Lecciones aprendidas | 2 min | 1 slide |
| 5 | Q&A | 3-5 min | — |

---

## 🎬 Segmento 3: Demo en Vivo — Paso a Paso

### [0:00] Preparación — Verificar que todo funciona

Antes de comenzar la demo, verifica en tu monitor:

```bash
# 1. Forgejo está vivo
curl -s -o /dev/null -w "%{http_code}" http://localhost:3000
# → 200

# 2. Runner está idle
docker ps | grep forgejo-runner
# → Up X minutes

# 3. Producción corriendo
curl -s http://localhost:8080/health
# → {"status":"healthy",...}

# 4. Pipeline anterior fue exitoso
# Ver en Forgejo UI → Actions → último run = success ✅
```

### [0:00] Paso 1 — "Esta es nuestra aplicación .NET 8"

> **🎙️ "Comenzamos con una API REST en .NET 8 con 10 endpoints y 15 pruebas unitarias usando xUnit + Moq."**

```bash
# Mostrar endpoints rápidamente
curl -s http://localhost:8080/health | python3 -m json.tool
curl -s http://localhost:8080/api/tasks | python3 -m json.tool
```

**En pantalla:** Terminal mostrando los curls + sus respuestas JSON formateadas.

### [0:30] Paso 2 — "Este es nuestro servidor CI/CD"

> **🎙️ "Tenemos Forgejo, un servidor Git auto-gestionado con CI/CD integrado compatible con GitHub Actions."**

Navega a **http://localhost:3000** → Login → **super/devops-lab** → **Actions**

**En pantalla:** Forgejo UI mostrando el historial de runs.

> **🎙️ "Aquí vemos los 18 runs del pipeline. Cada push dispara una ejecución. El último (#18) fue exitoso con las 4 fases en verde."**

🖱️ *Apunta al run #18, expande los jobs para mostrar los checkmarks verdes.*

### [1:00] Paso 3 — "Disparamos un nuevo pipeline"

> **🎙️ "Vamos a hacer un push para que vean el pipeline desde cero."**

```bash
# En la terminal (desde dotnet-app/)
echo "// Demo commit - "$(date) >> src/trigger.txt
git add src/trigger.txt
git commit -m "chore: trigger demo pipeline"
git push
```

**En pantalla:** Terminal mostrando el push exitoso.

> **🎙️ "Mientras el pipeline arranca, expliquemos las 4 fases."**

🖱️ *Cambia a la UI de Forgejo Actions — el nuevo pipeline debería aparecer en segundos.*

### [1:30] Paso 4 — Fase 1: TDD (se ejecuta ~2 min)

> **🎙️ "Fase 1 — TDD. 15 pruebas unitarias que validan: registro de usuarios, login con credenciales, CRUD de tareas con concurrencia usando SemaphoreSlim."**

🖱️ *Haz clic en "Fase 1: TDD" para expandir los logs en vivo.*

**Mientras corre (tiene ~2 min), aprovecha para explicar:**
- **Principio TDD:** Primero escribimos pruebas, luego implementamos
- **Cobertura:** Auth (6 tests) + Tasks (8 tests) + Concurrencia (1 test)
- **Qué pasa si falla:** El pipeline se detiene (mostrar slide)

> **🎙️ "15 de 15 pasan. El pipeline continúa..."**

### [2:00] Paso 5 — Fase 2: Docker Build (se ejecuta ~1 min)

> **🎙️ "Fase 2 — Build multi-etapa. El Dockerfile tiene 3 etapas: SDK para compilar, Test para ejecutar pruebas, Runtime para producción."**

🖱️ *Expande la Fase 2.*

> **🎙️ "La magia del multi-stage: la imagen final con SDK pesa 1.2 GB, la imagen runtime solo 210 MB. Ahorramos 80%."*

**Dato para la exposición:** Mencionar qué es multi-stage y por qué importa (seguridad — menos superficie de ataque, velocidad — despliegues más rápidos).

### [2:30] Paso 6 — Fase 3: Pruebas HTTP (se ejecuta ~1.5 min)

> **🎙️ "Fase 3 — Pruebas de integración HTTP con curl. Levantamos un contenedor temporal en la red compartida y probamos 7 endpoints: health check, registro de usuario, CRUD de tareas."*

🖱️ *Expande la Fase 3 para mostrar los curls ejecutándose.*

> **🎙️ "Usamos curl en vez de Playwright porque: 1) Playwright .NET requiere descargar Chromium cada vez (~200 MB), 2) curl es instantáneo, 3) para una API REST es suficiente."*

🖱️ *Apunta a las líneas con los curls en el log.*

### [3:00] Paso 7 — Fase 4: Deploy (se ejecuta ~1 min)

> **🎙️ "Fase 4 — Deploy a producción. Solo en main/master. Detenemos el contenedor anterior, levantamos el nuevo en la red compartida, puerto 8080, y verificamos el health check."*

🖱️ *Expande la Fase 4 para mostrar el log final.*

**Cuando termine:**
> **🎙️ "Pipeline completado. 4 fases, ~6 minutos, de un git push a producción."*

### [3:30] Paso 8 — Verificación final

```bash
# Verificar que corre
curl -s http://localhost:8080/health | python3 -m json.tool

# Mostrar que los datos se preservaron (volumen persistente)
curl -s http://localhost:8080/api/tasks | python3 -m json.tool
```

**En pantalla:** Terminal mostrando la app funcionando.

---

## ⚠️ Paso Opcional — Simular Error (si hay tiempo)

> **🎙️ "Veamos qué pasa si alguien introduce un error."**

```bash
# Introducir error deliberado en un test
cd /home/kelvin/concu/Exposicion/dotnet-app
```

```csharp
// En tests/UnitTests/UserServiceTests.cs — cambiar una aserción
Assert.True(result.Success);  →  Assert.False(result.Success);
```

```bash
git add -A
git commit -m "test: romper pipeline intencionalmente"
git push
```

🖱️ *Mostrar en Forgejo Actions cómo Fase 1 detecta el error y el pipeline se detiene automáticamente. Señalar que Fase 2-4 nunca se ejecutan.*

> **🎙️ "El CI/CD nos protege: el error se detecta en segundos, antes de llegar a producción."*

**⚠️ Advertencia:** revertir el cambio inmediatamente después para no dejar el repo roto:

```bash
git revert HEAD --no-edit
git push
```

---

## 🗣️ Guión Sugerido (Qué Decir en Cada Parte)

### Apertura (3 min)
> *"Hoy vamos a ver cómo implementamos un pipeline CI/CD completo para una aplicación .NET usando Forgejo, una alternativa open-source a GitHub Actions. El pipeline tiene 4 fases: pruebas unitarias, build de contenedor, pruebas de integración, y deploy automático. Todo corre 100% en local con Docker Compose."*

### Arquitectura (2 min)
> *"Forgejo es nuestro servidor Git con CI/CD integrado. Cuando hacemos push, Forgejo crea una tarea. El Runner recoge la tarea y ejecuta jobs en contenedores efímeros. Los jobs acceden a Docker mediante el socket del host para construir imágenes y levantar contenedores de prueba. Todo comparte la red forgejo-server_default para que puedan comunicarse por nombre."*

### Durante la demo (8 min)
> *"Acabamos de hacer push. El pipeline ya arrancó..."*
> *"Fase 1: TDD — 15 pruebas que garantizan que la lógica de negocio funciona."*
> *"Fase 2: Docker Build — imagen multi-etapa optimizada."*
> *"Fase 3: Pruebas HTTP — curl contra contenedor temporal en red compartida."*
> *"Fase 4: Deploy — producción en http://localhost:8080."*

### Cierre (2 min)
> *"Lo más valioso que aprendimos: usar socket Docker directo en vez de DIND (más simple, menos problemas de red), reemplazar Playwright por curl (más rápido para APIs REST), y la importancia de depurar sistemáticamente — 18 intentos hasta el éxito."*

---

## 🆘 Plan de Contingencia (Si Algo Sale Mal)

| Problema | Síntoma | Qué hacer |
|----------|---------|-----------|
| Pipeline no arranca | Forgejo no muestra nuevo run | Mostrar el run #18 que ya está en verde. Decir: *"El pipeline funciona, podemos ver el resultado del último"* |
| Fase 1 falla | Tests rojos | Mostrar el error y explicar *"Esto es EXACTAMENTE lo que queremos — el CI detecta errores antes de producción"* |
| Fase 3 falla | HTTP test falla | Tener curls de respaldo en terminal. Ejecutar manualmente: `curl http://localhost:8080/health` |
| Fase 4 falla | Deploy no responde | Tener `docker ps` para mostrar que el contenedor existe. Mostrar logs con `docker logs devops-demo-prod` |
| La app no responde | curl falla | Usar el contenedor de staging que quedó de la última demo. Verificar con `docker ps -a` |
| Forgejo UI lenta | Página no carga | Refresh y esperar. Tener terminal como respaldo: `docker logs forgejo-runner --tail 20` |
| Pérdida total | Todo roto | Tener capturas de pantalla del run #18 exitoso en slides como respaldo |

**Máxima prioridad:** No perder tiempo debuggeando en vivo. Si algo falla, decir *"Esto es parte del aprendizaje — justamente el CI/CD sirve para detectar estos problemas"* y seguir con las slides.

---

## 📸 Capturas de Pantalla Recomendadas (Backup para Slides)

| Slide | Qué mostrar |
|-------|-------------|
| 1 | Forgejo UI → Actions → Run #18 con 4 fases en verde |
| 2 | Log de Fase 1: 15/15 tests passing |
| 3 | Log de Fase 3: 7 curls exitosos |
| 4 | Log de Fase 4: mensaje "PIPELINE COMPLETADO CON EXITO" |
| 5 | Terminal: `curl http://localhost:8080/health` respondiendo |
| 6 | Timeline de runs 1→18 (del resumen-exposicion.md) |

---

## 📁 Archivos para la Presentación

| Archivo | Para qué |
|---------|----------|
| `resumen-exposicion.md` | Proyectarlo con los diagramas Mermaid |
| `03-guia-instalacion-forgejo.md` | Referencia de la infraestructura |
| `04-guia-pipeline-forgejo.md` | Referencia del pipeline |
| **Esta guía** | Llevar impresa o en monitor secundario |

---

> **¡Buena suerte en la exposición!** 🚀
> Pipeline probado y funcionando — confía en el proceso.
