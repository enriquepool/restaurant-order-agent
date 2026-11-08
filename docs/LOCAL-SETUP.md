# Guía de pruebas locales

Aquí está todo lo necesario para probar el agente en tu máquina, primero sin WhatsApp y después
con WhatsApp. Los comandos funcionan en **Windows (PowerShell)** y **macOS (Terminal)**, salvo
donde se indica.

```
Fase 1: herramientas  →  Fase 2: base de datos + modelo  →  Fase 3: probar por HTTP  →  Fase 4: WhatsApp
```

---

## Fase 1: Instalar herramientas (una sola vez)

| Herramienta | Windows | macOS |
|---|---|---|
| .NET 10 SDK | `winget install Microsoft.DotNet.SDK.10` | `brew install --cask dotnet-sdk` |
| Docker Desktop | `winget install Docker.DockerDesktop` | `brew install --cask docker` |
| Ollama | `winget install Ollama.Ollama` | `brew install ollama` |
| Dev Tunnels (para WhatsApp) | `winget install Microsoft.devtunnel` | `brew install --cask devtunnel` |

Cierra y vuelve a abrir la terminal después de instalar.

### Solo Windows: habilitar virtualización para Docker

Docker Desktop necesita WSL2, y WSL2 necesita la virtualización del procesador.

1. **Activa la virtualización en el BIOS/UEFI.** Reinicia, entra al BIOS (normalmente con F2, F10,
   Supr o Esc) y activa **Intel VT-x** (o *Intel Virtualization Technology*), o **SVM Mode** en procesadores AMD.
2. En Windows, abre **PowerShell como administrador** y ejecuta:
   ```powershell
   wsl --install --no-distribution
   ```
3. Reinicia la computadora.
4. Abre Docker Desktop y espera a que diga *Engine running*.

Para comprobarlo: `docker version` debe mostrar una sección **Server**.

---

## Fase 2: Levantar la base de datos y el modelo

Todos los comandos se ejecutan desde la carpeta del repositorio.

```bash
# 1. Descargar el modelo local (unos 5 GB, solo la primera vez)
ollama pull qwen3:8b

# 2. Comprobar que el modelo responde
ollama run qwen3:8b "Di hola en una palabra"

# 3. Levantar PostgreSQL en Docker
docker compose up -d postgres

# 4. Restaurar herramientas y paquetes de .NET
dotnet tool restore
dotnet restore
```

> **¿Tu computadora es lenta?** Usa un modelo más chico: `ollama pull qwen3:4b` y luego
> `dotnet user-secrets set "Llm:Model" "qwen3:4b" --project src/OrderAgent.Api`.

---

## Fase 3: Probar el agente sin WhatsApp

```bash
# Corre las pruebas automáticas (no requieren Ollama ni Postgres)
dotnet test

# Arranca la API (crea las tablas y el menú de ejemplo automáticamente)
dotnet run --project src/OrderAgent.Api
```

La API queda en **http://localhost:5080**. Para probarla:

- **Desde el editor:** abre `src/OrderAgent.Api/OrderAgent.Api.http` y ejecuta las peticiones en orden.
  Funciona en Visual Studio y Rider; en VS Code instala la extensión *REST Client*.
- **Desde la terminal:**
  ```bash
  curl -X POST http://localhost:5080/api/v1/chat -H "Content-Type: application/json" -d "{\"customerId\":\"5215500000001\",\"message\":\"Hola, quiero 2 tacos al pastor\"}"
  ```

Qué revisar:

1. `GET /health/ready` responde `ready` (hay conexión con Postgres).
2. `GET /api/v1/menu` devuelve los 12 platillos de ejemplo.
3. Al pedir algo, el agente responde con el total correcto.
4. Al confirmar, la orden aparece en `GET /api/v1/orders?status=Confirmed`.
5. En la consola de la API verás las llamadas del modelo a las herramientas (`add_item_to_order`, etc.).

> La primera respuesta puede tardar de 10 a 30 segundos mientras Ollama carga el modelo en memoria.

---

## Fase 4: Conectar WhatsApp

Para esta fase usaremos **WhatsApp Cloud API** de Meta. Es gratis para pruebas: Meta te da un número
de prueba y puedes enviar mensajes hasta a 5 números que tú registres.

### 4.1 Crear la app en Meta

1. Entra a <https://developers.facebook.com> con tu cuenta de Facebook y regístrate como desarrollador.
2. **My Apps → Create App**. Elige el caso de uso **"Connect with customers through WhatsApp"**
   (o tipo *Business*) y sigue el asistente. Se crea una cuenta de WhatsApp Business de prueba.
3. En el menú de la app ve a **WhatsApp → API Setup**. Ahí verás:
   - **Temporary access token**: dura **24 horas**.
   - **Phone number ID**: un número largo; no es el número de teléfono.
   - Un número de prueba ("From").
4. En **"To"**, agrega **tu número de celular** y verifícalo con el código que te llega por WhatsApp.
5. Ve a **App settings → Basic** y copia el **App secret** (botón *Show*).

### 4.2 Guardar los secretos (fuera del repositorio)

Inventa un **VerifyToken**: cualquier texto, por ejemplo `mi-token-restaurante-2026`.

```bash
cd src/OrderAgent.Api
dotnet user-secrets set "WhatsApp:VerifyToken"   "mi-token-restaurante-2026"
dotnet user-secrets set "WhatsApp:AppSecret"     "<App secret>"
dotnet user-secrets set "WhatsApp:AccessToken"   "<Temporary access token>"
dotnet user-secrets set "WhatsApp:PhoneNumberId" "<Phone number ID>"
cd ../..
```

Los secretos se guardan en tu perfil de usuario, nunca en el repositorio:
`%APPDATA%\Microsoft\UserSecrets` en Windows y `~/.microsoft/usersecrets` en macOS.

### 4.3 Exponer tu API a internet con un túnel

Meta necesita una URL pública con HTTPS para enviarte los mensajes.

```bash
# Inicia sesión una vez (puedes usar tu cuenta de GitHub)
devtunnel user login -g

# Con la API corriendo en otra terminal:
devtunnel host -p 5080 --allow-anonymous
```

Copia la URL que aparece, por ejemplo `https://abc123-5080.usw3.devtunnels.ms`.

> Alternativa: [ngrok](https://ngrok.com) con `ngrok http 5080`.

### 4.4 Registrar el webhook en Meta

1. En tu app ve a **WhatsApp → Configuration → Webhook → Edit**.
2. **Callback URL:** `https://<tu-tunel>/webhooks/whatsapp`
3. **Verify token:** el mismo texto que guardaste en `WhatsApp:VerifyToken`.
4. Haz clic en **Verify and save**. En la consola de la API debe aparecer
   `Webhook de WhatsApp verificado.`
5. En **Webhook fields**, haz clic en **Manage** y suscríbete a **messages**.

### 4.5 Probar

Desde tu celular, envía un WhatsApp al **número de prueba** de Meta:

> Hola, quiero 2 órdenes de tacos al pastor y una horchata

El agente responde por WhatsApp. Escribe **`reiniciar`** para empezar una conversación nueva.

---

## Solución de problemas

| Síntoma | Causa probable / solución |
|---|---|
| `docker: failed to connect to the docker API` | Docker Desktop no está abierto. En Windows, revisa la sección de virtualización. |
| `/health/ready` responde 503 | Postgres no está corriendo: `docker compose up -d postgres`. |
| El agente responde "tuve un problema técnico" | Ollama no está corriendo o no descargaste el modelo. Prueba `ollama list`. |
| Respuestas muy lentas | Usa `qwen3:4b`. La primera respuesta siempre tarda más. |
| Meta no verifica el webhook | El túnel no está activo, la URL no termina en `/webhooks/whatsapp`, o el VerifyToken no coincide. |
| Llega el mensaje pero no hay respuesta, con error **131030** en la consola | Tu número no está en la lista "To" de API Setup. |
| Error **401/190** al enviar | El token temporal de 24 h expiró. Genera otro y actualiza `WhatsApp:AccessToken`. |
| `Firma inválida en el webhook` | El `WhatsApp:AppSecret` no coincide con el de *App settings → Basic*. |

## Todo en Docker (opcional)

Para correr también la API en un contenedor:

```bash
cp .env.example .env      # en Windows PowerShell: Copy-Item .env.example .env
# edita .env con tus valores de WhatsApp
docker compose up -d --build
```

La API queda en el mismo puerto (5080) y se conecta a Ollama en tu máquina mediante `host.docker.internal`.
