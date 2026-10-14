# Encorely · API

Back de Encorely: conecta a personas que van al mismo concierto según su gusto musical real.
.NET 10 · ASP.NET Core Minimal APIs · EF Core 10 + PostgreSQL 16 · SignalR · chat cifrado de extremo a extremo.

- Contrato: [`contracts/openapi.v1.json`](contracts/openapi.v1.json) (generado desde el código, con [CHANGELOG](contracts/CHANGELOG.md)).
- Arquitectura, modelo de datos y modelo de amenazas del chat: [`ARCHITECTURE.md`](ARCHITECTURE.md).
- App móvil: repo `encorely-front`, rama `rebuild/v1`.

## Levantarlo en local (menos de 15 min)

Requisitos: Docker con `docker compose` v2 y el SDK de .NET 10. Con [mise](https://mise.jdx.dev) basta `mise install`
en la raíz (lee `mise.toml`). Sin SDK también funciona: ver la opción B.

```bash
# 1. Configuración
cp .env.example .env
#    Completa en .env:
#      DB_PASSWORD         la que quieras (crea la BD local con ella)
#      JWT_SIGNING_KEY     openssl rand -base64 48
#      DEMO_USER_PASSWORD  (opcional) contraseña de las 16 cuentas demo @encorely.demo

# 2. Postgres
docker compose up -d

# 3. API en http://localhost:5000 (migraciones + seed automáticos en Development)
dotnet run --project src/Encorely.Api
```

Opción B, todo en contenedores (sin SDK): `docker compose --profile full up -d --build` → API en `http://localhost:5000`.

Comprobar:

```bash
curl http://localhost:5000/health/ready      # Healthy
open http://localhost:5000/swagger           # Swagger UI (solo Development)
```

Desde el emulador de Android la API es `http://10.0.2.2:5000`; desde un teléfono en la misma red, `http://<IP-del-PC>:5000`
(la API escucha en `0.0.0.0:5000`).

### Datos de demostración

Al arrancar en Development se siembra, de forma idempotente:

| Qué | Cuánto |
|---|---|
| Géneros | 15 (reguetón, vallenato, salsa, champeta, cumbia, rock en español, indie…) |
| Canciones | ≥ 70 de la escena colombiana y latina, con portada y preview de 30 s de iTunes Search |
| Eventos | ≥ 20 en Bogotá y Medellín, con fechas relativas al día del seed (siempre futuras) |
| Usuarios demo | 16 cuentas `@encorely.demo` con gustos distintos y asistencias cruzadas, si `DEMO_USER_PASSWORD` tiene valor |

Cuentas especiales: `admin@encorely.demo` (rol admin, para `/api/v1/admin/*`) y `revisor@encorely.demo` (cuenta para
revisores de tiendas). La contraseña de todas es la de `DEMO_USER_PASSWORD`: no está en el repo.

Para volver a empezar desde cero: `docker compose down -v` (borra el volumen de la BD local) y arrancar de nuevo.

## Tests

```bash
dotnet test tests/Encorely.Tests
```

Usan un Postgres real en Docker (Testcontainers) y la API en memoria. Cubren, entre otros: que un usuario no lee ni escribe
nada de otro en cada endpoint con recursos, que un like unilateral no crea match (y que likes cruzados concurrentes crean
exactamente uno), que la BD y los logs no contienen el texto plano del chat (test canario), la rotación de refresh tokens con
detección de reuso, y que ningún input inválido da 500.

`OpenApiContractTests` falla si el contrato versionado no coincide con el código. Tras cambiar endpoints o DTOs:

```bash
scripts/update-contract.sh     # regenera contracts/openapi.v1.json
```

y anota el cambio en `contracts/CHANGELOG.md`.

## Migraciones

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> -p src/Encorely.Api -o Data/Migrations
```

En Development se aplican al arrancar (`DB_MIGRATE_ON_STARTUP=true`). En producción también, mientras haya una sola instancia.

## Configuración

Todas las variables están documentadas en [`.env.example`](.env.example). Ningún secreto tiene valor por defecto en el
código: si falta `JWT_SIGNING_KEY` o un dato de la BD, la API no arranca. `DB_SSL_MODE` acepta
`Disable | Allow | Prefer | Require | VerifyCA | VerifyFull` (local: `Prefer`; producción: `Require` o superior).

## Despliegue

No se publica en esta entrega (D15), pero está preparado: `Dockerfile` (imagen sin secretos, usuario no root) y
`render.yaml` (Blueprint de Render con Postgres y los secretos marcados para completar). CI en GitHub Actions: build con
warnings como errores, tests con Postgres real y build de la imagen.

## Estructura

```
src/Encorely.Api
  Features/<Feature>/        Endpoints (rutas), Contracts (DTOs), Service (lógica), entidades
    Auth Users Legal Music Events Matching Chat Moderation Devices Sync Sessions Admin
  Infrastructure/            Auth (JWT), Http (ProblemDetails, validación), Database, Realtime (ChatHub), Push
  Data/Migrations, Data/Seed (JSON curado embebido)
tests/Encorely.Tests         xUnit v3 + WebApplicationFactory + Testcontainers
contracts/                   openapi.v1.json + CHANGELOG.md
scripts/                     update-contract.sh, curate_songs.py
```
