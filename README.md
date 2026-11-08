# Restaurant Order Agent

Agente de IA que toma órdenes para un restaurante por **WhatsApp**. Diseñado para:

- Correr en **Windows y macOS** (Intel y Apple Silicon).
- Ejecutarse en **contenedores** (Docker).
- Ser consumido en el futuro por apps **Android / iOS** (API-first).
- Comunicarse con otros agentes (cocina, inventario, pagos) vía **A2A** / **MCP**.

👉 **Para probarlo en tu máquina, sigue [docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md).**

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 (LTS) – ASP.NET Core Minimal APIs |
| Agente | Microsoft Agent Framework + Microsoft.Extensions.AI |
| LLM (desarrollo) | Ollama con `qwen3:8b` (local y gratuito) |
| Canal | WhatsApp Cloud API (Meta) |
| Base de datos | PostgreSQL 17 + EF Core (Npgsql) |
| Contenedores | Docker + docker compose |
| Pruebas | xUnit v3 + Microsoft.Testing.Platform |

## Arquitectura

```
 Cliente en WhatsApp                     App móvil / Web / Otros agentes
        │                                            │
        ▼  Meta Cloud API (webhook)                  ▼  HTTP
 ┌────────────────────────────────────────────────────────────────┐
 │ OrderAgent.Api                                                 │
 │   POST /webhooks/whatsapp  → valida firma → cola en memoria    │
 │   POST /api/v1/chat        → pruebas locales sin WhatsApp      │
 │   GET  /api/v1/menu, /api/v1/orders, /.well-known/agent-card.json │
 ├────────────────────────────────────────────────────────────────┤
 │ OrderAgent.Agent           (independiente del canal)           │
 │   OrderAgentService → ChatClientAgent (LLM) → OrderTools       │
 │   get_menu · add_item_to_order · remove_item_from_order ·      │
 │   get_order_summary · confirm_order · cancel_order             │
 ├────────────────────────────────────────────────────────────────┤
 │ OrderAgent.Domain          reglas de negocio puras             │
 │   Order · OrderItem · MenuItem                                 │
 ├────────────────────────────────────────────────────────────────┤
 │ OrderAgent.Infrastructure  EF Core + Postgres · cliente WhatsApp│
 └────────────────────────────────────────────────────────────────┘
```

**Principio clave:** el LLM solo conversa y decide qué herramienta usar. Precios, disponibilidad
y totales siempre vienen del dominio y la base de datos, nunca del modelo.

### Flujo de un mensaje de WhatsApp

1. Meta llama a `POST /webhooks/whatsapp`. La API valida la firma `X-Hub-Signature-256`,
   encola el mensaje y responde `200` de inmediato.
2. Un `BackgroundService` toma el mensaje, descarta duplicados y llama al agente.
3. El agente carga el historial de la conversación (jsonb en Postgres), agrega el menú actual a las
   instrucciones y ejecuta el modelo, que invoca las herramientas necesarias.
4. La respuesta se envía al cliente con la Graph API de Meta.

## Estructura

```
src/
  OrderAgent.Domain/          Entidades y reglas de negocio
  OrderAgent.Agent/           Agente, herramientas, prompt y contratos (puertos)
  OrderAgent.Infrastructure/  Postgres (EF Core, migraciones) y WhatsApp
  OrderAgent.Api/             Host ASP.NET Core, endpoints y Dockerfile
tests/
  OrderAgent.Tests/           Pruebas unitarias (dominio, herramientas, agente, webhook)
docs/
  LOCAL-SETUP.md              Guía paso a paso de pruebas locales
  BRANCHING.md                Estrategia de branches
```

## Comandos frecuentes

```bash
docker compose up -d postgres          # base de datos
dotnet run --project src/OrderAgent.Api   # API en http://localhost:5080
dotnet test                            # pruebas
dotnet ef migrations add <Nombre> --project src/OrderAgent.Infrastructure --output-dir Persistence/Migrations
```

## Flujo de trabajo

Todo cambio entra por Pull Request: `feature/* → develop → stage → master`.
Detalles en [docs/BRANCHING.md](docs/BRANCHING.md).

## Próximos pasos

- Autenticación (JWT) para `/api/v1/chat` y `/api/v1/orders`, necesaria antes de exponerlos fuera de Development.
- Endpoint JSON-RPC de A2A y un `KitchenAgent` que reciba las órdenes confirmadas.
- Proveedor de LLM en la nube (Claude) para producción, configurable sin cambiar código.
- Pipeline de CI en GitHub Actions (build + pruebas en Windows y macOS).
