# Restaurant Order Agent

Agente de IA que toma órdenes para un restaurante. Diseñado para:

- Correr en **Windows y macOS** (Intel y Apple Silicon).
- Ejecutarse en **contenedores** (Docker).
- Ser consumido en el futuro por apps **Android / iOS** (API-first).
- Comunicarse con otros agentes (cocina, inventario, pagos) vía **A2A** / **MCP**.

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 (LTS) – ASP.NET Core Minimal APIs |
| Agente | Microsoft Agent Framework |
| LLM (desarrollo) | Ollama (modelo local y gratuito) |
| Base de datos | PostgreSQL + EF Core (Npgsql) |
| Contenedores | Docker + docker compose |

## Arquitectura

```
 App móvil / Web / Otros agentes (A2A)
                │ HTTP
                ▼
 OrderAgent.Api            → endpoints /api/v1/chat, /.well-known/agent.json
 OrderAgent.Agent          → LLM + herramientas (GetMenu, AddItem, RemoveItem, GetOrderSummary, ConfirmOrder)
 OrderAgent.Domain         → reglas de negocio (Order, OrderItem, MenuItem)
 OrderAgent.Infrastructure → EF Core + PostgreSQL
```

**Principio clave:** el LLM solo conversa y decide qué herramienta usar. Precios, disponibilidad
y totales siempre vienen del dominio y la base de datos, nunca del modelo.

## Requisitos

| | Windows | macOS |
|---|---|---|
| .NET 10 SDK | `winget install Microsoft.DotNet.SDK.10` | `brew install --cask dotnet-sdk` |
| Docker Desktop | `winget install Docker.DockerDesktop` | `brew install --cask docker` |
| Ollama | `winget install Ollama.Ollama` | `brew install ollama` |

Después de instalar Ollama, descarga un modelo con soporte de herramientas:

```bash
ollama pull qwen3:8b
```

> Ollama corre **nativo** en el host (no en Docker) para aprovechar la GPU, incluida Metal en Mac.
> Los contenedores lo alcanzan en `http://host.docker.internal:11434`.

## Flujo de trabajo

Todo cambio entra por Pull Request: `feature/* → develop → stage → master`.
Detalles en [docs/BRANCHING.md](docs/BRANCHING.md).

## Estado

🚧 En construcción.
