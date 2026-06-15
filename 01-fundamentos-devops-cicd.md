# 1. El Ciclo de Vida del Software bajo la Filosofía DevOps

## 📌 Introducción

DevOps no es una herramienta ni un cargo; es una **filosofía cultural y técnica** que busca integrar los equipos de desarrollo (Dev) y operaciones (Ops) para acelerar la entrega de software con calidad. En el contexto de sistemas distribuidos, DevOps se convierte en un habilitador crítico, ya que la complejidad de coordinar múltiples servicios, nodos y contenedores exige automatización total.

---

## 1.1 Definiciones Clave del Pipeline CI/CD

### Integración Continua (CI — Continuous Integration)

> **Definición:** Práctica donde cada desarrollador integra sus cambios al menos una vez al día en un repositorio compartido, y cada integración es verificada mediante una compilación automatizada y ejecución de pruebas.

**Características esenciales:**
- Repositorio único de código fuente (Git).
- Build automatizado ante cada `git push`.
- Ejecución de pruebas unitarias y de integración rápidas.
- Retroalimentación inmediata (menos de 10 minutos idealmente).

**Beneficio clave:** Detecta problemas de integración temprano, evitando el "infierno de la integración" donde confluyen cambios incompatibles de varios desarrolladores.

### Entrega Continua (CD — Continuous Delivery)

> **Definición:** Extensión de CI donde el software está siempre en un estado `release-ready`. El código pasa por pipelines automatizados de prueba y validación, pero el despliegue a producción requiere una **aprobación manual**.

**Características esenciales:**
- El artefacto (binario/imagen) se construye una sola vez y se promueve entre entornos.
- Pruebas de aceptación automatizadas en entornos staging.
- Un solo clic o confirmación manual separa el "listo para desplegar" del "desplegado".

### Despliegue Continuo (CD — Continuous Deployment)

> **Definición:** Automatización total: cada commit que supera todas las etapas del pipeline se despliega automáticamente a producción, sin intervención humana.

**Diferencia clave con Entrega Continua:**

| Aspecto | Continuous Delivery | Continuous Deployment |
|---------|-------------------|----------------------|
| Aprobación humana | Sí (último paso manual) | No (completamente automático) |
| Riesgo | Menor (control humano) | Mayor (requiere confianza en tests) |
| Velocidad | Horas | Minutos |
| Caso de uso | Aplicaciones reguladas (banca, salud) | SaaS, startups, web apps |

---

## 1.2 Ventajas de la Automatización

### Reproducibilidad de Entornos

> _"Funciona en mi máquina" deja de ser una excusa._

Con pipelines automatizados, cada entorno (desarrollo, staging, producción) se levanta desde la misma definición declarativa:
- `Dockerfile` + `docker-compose.yml`
- Archivos YAML de infraestructura
- Scripts idempotentes

Esto garantiza que el código se ejecute exactamente igual en cualquier lugar.

### Eliminación del Error Humano

Las tareas repetitivas y propensas a errores (compilar, empaquetar, desplegar, configurar bases de datos) se delegan a la máquina:
- Pasos olvidados → no ocurren (el pipeline los fuerza todos).
- Configuraciones inconsistentes → se definen una vez y se versionan.
- Despliegues a medianoche → el pipeline lo hace sin supervisión.

### Reducción drástica del Time-to-Market

| Métrica | Sin automatización | Con CI/CD automatizado |
|---------|-------------------|----------------------|
| Tiempo de release | Semanas | Minutos u horas |
| Frecuencia de deploy | Mensual/Trimestral | Varias veces al día |
| Tasa de fallos en deploy | ~30% | < 5% |
| Tiempo de recuperación | Horas/Días | Minutos |

**Datos relevantes:** Empresas como Netflix, Amazon y Google realizan miles de despliegues diarios gracias a pipelines automatizados.

---

## 1.3 El Concepto de Inmutabilidad de la Infraestructura

### ¿Qué es Infraestructura Inmutable?

En el enfoque tradicional (mutable), los servidores se actualizan _in-place_: SSH, aplicar parches, modificar configuraciones. Con el tiempo, estos servidores se vuelven "snowflake" (copos de nieve) — únicos e irreproducibles.

En la **infraestructura inmutable**, una vez que un contenedor o instancia se despliega, **nunca se modifica**. Si se necesita un cambio, se construye una nueva imagen desde cero y se reemplaza.

### Por qué los artefactos distribuidos deben empaquetarse en imágenes de contenedor selladas

1. **Consistencia garantizada:** La misma imagen que pasa las pruebas en CI es exactamente la misma que se despliega en producción. No hay "deriva de configuración".

2. **Aislamiento por contenedores:** Cada microservicio ejecuta en su propio contenedor con sus dependencias específicas, evitando conflictos de versiones entre servicios.

3. **Rollback instantáneo:** Si una versión falla, se despliega la imagen anterior. El cambio es atómico: se reemplaza el contenedor completo.

4. **Escalabilidad horizontal:** Las imágenes inmutables permiten replicar instancias idénticas sin configuración adicional (escalar = más contenedores de la misma imagen).

5. **Seguridad:** Las imágenes selladas reducen la superficie de ataque. No hay puertas traseras dejadas por configuraciones manuales.

### Multi-stage Builds (Construcción Multi-etapa)

```dockerfile
# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Etapa 2: Runtime (imagen final mínima)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "App.dll"]
```

**Ventajas del multi-stage:**
- La imagen final solo contiene el runtime y el binario compilado (≈ 200 MB vs >1 GB con el SDK).
- Menor superficie de vulnerabilidades.
- Menor tiempo de descarga y despliegue.

---

## 1.4 DevOps aplicado a Sistemas Distribuidos

En sistemas concurrentes y distribuidos, la filosofía DevOps adquiere nuevos desafíos:

| Desafío | Solución DevOps |
|---------|----------------|
| Coordinación entre microservicios | Pipelines independientes por servicio + tests de contrato |
| Gestión de configuraciones distribuidas | Variables de entorno + secretos externos (Vault) |
| Observabilidad | Logs centralizados (ELK), métricas (Prometheus), tracing (Jaeger) |
| Pruebas de integración distribuidas | Entornos efímeros (ephemeral environments) con Docker Compose |
| Versionamiento de APIs | Semantic versioning + pruebas de compatibilidad |

---

## 🧠 Resumen Conceptual

```
                    ┌─────────────────────────────────────────┐
                    │         FILOSOFÍA DEVOPS                │
                    │  Integración Desarrollo + Operaciones   │
                    └──────────────┬──────────────────────────┘
                                   │
                    ┌──────────────▼──────────────────────────┐
                    │         PIPELINE CI/CD                  │
                    │                                         │
                    │  ┌──────┐   ┌────────┐   ┌──────────┐  │
                    │  │  CI  │──►│  CD    │──►│    CD    │  │
                    │  │Build │   │Delivery│   │Deployment│  │
                    │  │+Test │   │Staging │   │Production│  │
                    │  └──────┘   └────────┘   └──────────┘  │
                    └──────────────┬──────────────────────────┘
                                   │
                    ┌──────────────▼──────────────────────────┐
                    │   INFRAESTRUCTURA INMUTABLE             │
                    │   (Imágenes de contenedor selladas)     │
                    └─────────────────────────────────────────┘
```

---

## 📚 Referencias

- Kim, G., Humble, J., Debois, P., & Willis, J. (2021). *The DevOps Handbook*.
- Humble, J., & Farley, D. (2010). *Continuous Delivery*.
- Fowler, M. (2006). *Continuous Integration*. martinfowler.com.
- Forgejo Documentation. *Forgejo Actions*. codeberg.org/forgejo
