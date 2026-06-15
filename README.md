# 🚀 Guía DevOps con Forgejo

Pipeline CI/CD completo de 4 fases para una aplicación .NET 8, usando **Forgejo** (servidor Git auto-gestionado) y **Forgejo Actions** (CI/CD nativo). Todo corre 100% local con Docker Compose.

## 📁 Contenido

| Archivo | Descripción |
|---------|-------------|
| `01-fundamentos-devops-cicd.md` | Investigación: fundamentos DevOps y CI/CD |
| `02-piramide-testing-concurrente.md` | Pirámide de testing y concurrencia |
| `03-guia-instalacion-forgejo.md` | Instalación y configuración de Forgejo + Runner |
| `04-guia-pipeline-forgejo.md` | Pipeline CI/CD documentado paso a paso |
| `guia-demo-en-vivo.md` | Guía para presentar la demo en vivo |
| `resumen-exposicion.md` | Resumen visual con diagramas Mermaid |
| `03.DevOps_CICD_Sistemas_Distribuidos.pdf` | PDF original del caso práctico |
| `dotnet-app/` | Aplicación .NET 8 (API REST + tests + Dockerfile) |
| `forgejo-server/` | Docker Compose y configuración del runner |

## 🏗️ Pipeline (4 fases)

1. **🔬 TDD** — 15 pruebas unitarias (xUnit + Moq)
2. **🐳 Docker Build** — Multi-stage build (210 MB final)
3. **🌐 Pruebas HTTP** — curl contra contenedor temporal
4. **🚀 Deploy** — Producción en `localhost:8080`

## ⚡ Requisitos

- Docker Engine 20.10+
- 1 CPU, 512 MB RAM, 1 GB disco

## 🚦 Demo rápida

```bash
# 1. Clonar
git clone https://github.com/elote200/guia-devops-forgejo.git

# 2. Ir a la guía de instalación
cd guia-devops-forgejo
cat 03-guia-instalacion-forgejo.md

# 3. O la guía del pipeline
cat 04-guia-pipeline-forgejo.md
```

---

> Proyecto académico — DevOps & Sistemas Distribuidos
