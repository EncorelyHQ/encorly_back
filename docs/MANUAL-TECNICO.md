# Manual técnico · Encorely API

| | |
|---|---|
| Sistema | Back de Encorely (`encorly_back`) |
| Público | Quien mantenga, pruebe o despliegue la API |
| Contrato | `v1.0.0-draft.17` ([CHANGELOG](../contracts/CHANGELOG.md)) |
| Base del manual | rama `dev`, commit `3730181` |
| Fecha | 2026-10-20 |

Este manual guía el trabajo sobre la API. No repite lo que ya está escrito en otro lado: el diseño completo está en
[`ARCHITECTURE.md`](../ARCHITECTURE.md), el arranque corto en [`README.md`](../README.md), el contrato en
[`contracts/openapi.v1.json`](../contracts/openapi.v1.json) y los datos personales en
[`privacidad-y-tiendas.md`](privacidad-y-tiendas.md). Si algo de aquí contradice al código, manda el código.

## Índice

1. [Introducción](#1-introducción)
2. [Arquitectura](#2-arquitectura)
3. [Requisitos](#3-requisitos)
4. [Instalación y arranque local](#4-instalación-y-arranque-local)
5. [Configuración](#5-configuración)
6. [Base de datos](#6-base-de-datos)
7. [API](#7-api)
8. [Tiempo real y push](#8-tiempo-real-y-push)
9. [Seguridad](#9-seguridad)
10. [Pruebas](#10-pruebas)
11. [CI/CD y despliegue](#11-cicd-y-despliegue)
12. [Mantenimiento](#12-mantenimiento)
13. [Solución de problemas](#13-solución-de-problemas)
14. [Pendientes detectados](#14-pendientes-detectados)

## 1. Introducción

Encorely conecta a personas que van al mismo concierto según su gusto musical. La API resuelve cuentas, catálogo musical,
ADN musical, eventos, radar de asistentes compatibles, match recíproco, chat cifrado de extremo a extremo, moderación y
panel de administración. El cliente es la app Expo del repo `encorely-front`.

Alcance de la v1 (decidido en `reportes/06-decisiones-y-alcance-v1.md`): sin salas de chat, sin login con Google ni Apple y
sin APIs externas salvo iTunes Search (catálogo), Expo Push (FCM/APNs) y Spotify (PKCE). La API no se publica en esta
entrega (D15), pero queda lista para hacerlo (ver [§11](#11-cicd-y-despliegue)).

Convenciones del manual:

- Los comandos se corren desde la raíz del repo, con el `dotnet` de mise (ver [§3](#3-requisitos)).
- Rutas de código relativas a `src/Encorely.Api/` cuando empiezan por `Features/`, `Infrastructure/` o `Data/`.
- En las tablas de rutas, la base `/api/v1` se omite.
- `<valor>` es algo que completas tú. Ningún comando de este manual lleva secretos reales.

## 2. Arquitectura

Monolito modular: un proceso ASP.NET Core 10 (Minimal APIs) con una carpeta por feature, PostgreSQL 16 como único
almacenamiento y SignalR para avisos en tiempo real. Kafka, Redis y el Worker del proyecto anterior se quitaron (D3).

```
src/Encorely.Api/
  Program.cs                 composición: opciones validadas, middlewares, rutas, migraciones y seed
  Features/<Feature>/        *Endpoints.cs (rutas + metadatos OpenAPI), *Contracts.cs (DTOs), *Service.cs (reglas), entidades
  Infrastructure/
    Auth/                    JWT, refresh tokens, BCrypt, LoginThrottle, SessionRevoker
    Configuration/           DotEnv (.env en Development), EnvironmentMapping (VAR_PLANA → sección), Options
    Database/                AppDbContext, AdvisoryLock, health check, MaintenanceService
    Http/                    ProblemDetails, validación, cabeceras de seguridad
    Push/                    cola, envío a Expo Push y lectura de receipts
    Realtime/                ChatHub y Notifier
  Data/Migrations, Data/Seed (JSON curado embebido), Data/Seeder.cs
tests/Encorely.Tests/        xUnit v3 + WebApplicationFactory + Testcontainers
contracts/                   openapi.v1.json + CHANGELOG.md
scripts/                     update-contract.sh, curate_songs.py
```

Features en código: `Auth`, `Users`, `Profiles`, `Legal`, `Music`, `Events`, `Matching`, `Chat`, `Moderation`, `Devices`,
`Sync`, `Sessions`, `Admin`. La tabla de responsabilidades, el pipeline de una request, el ADN musical, el radar, el match
recíproco y el modelo de amenazas del chat están en [`ARCHITECTURE.md`](../ARCHITECTURE.md) §1–§5.

Orden del pipeline, en corto: cabeceras reenviadas (solo con proxy de confianza) → log de acceso sin query → manejo de
errores → cabeceras de seguridad → CORS → JWT → comprobación de cuenta y sesión vivas → autorización → rate limit →
validación → handler → servicio → EF Core o Dapper.

## 3. Requisitos

| Herramienta | Versión | De dónde sale |
|---|---|---|
| .NET SDK | 10.0.402 | `mise.toml` y `global.json` (`rollForward: latestFeature`) |
| mise | cualquiera reciente | gestiona el SDK; no instalar .NET con apt |
| Docker | con `docker compose` v2 | Postgres local y Testcontainers |
| `dotnet-ef` | 10.0.13 | `dotnet-tools.json` (herramienta local) |
| curl, jq | opcional | verificación y lectura del contrato |

Puertos por defecto: API `5000` (`appsettings.Development.json` fija `Urls: http://0.0.0.0:5000`) y Postgres `5432`
(`DB_PORT`). Para usar otros, ver [§4.3](#43-puertos-alternos-y-una-segunda-instancia).

mise solo pone su `dotnet` en el `PATH` si está activado en la shell (`eval "$(mise activate zsh)"` en `~/.zshrc`). En una
shell sin activar, o en scripts, usa `mise exec -- dotnet …`. Si el sistema tiene otro SDK, `dotnet` a secas toma ese y falla
por `global.json` (ver [§13](#13-solución-de-problemas)).

## 4. Instalación y arranque local

### 4.1 Paso a paso

```bash
# 1. SDK
mise trust          # solo la primera vez en un clon nuevo
mise install

# 2. Configuración
cp .env.example .env
#    Completa en .env (los demás valores ya sirven para local):
#      DB_PASSWORD         la que quieras; docker compose crea la BD con ella
#      JWT_SIGNING_KEY     openssl rand -base64 48
#      BANNED_EMAIL_KEY    openssl rand -base64 48   (otra distinta)
#      DEMO_USER_PASSWORD  opcional: contraseña de las 16 cuentas demo

# 3. Postgres
docker compose up -d --wait

# 4. API (Development: aplica migraciones y siembra al arrancar)
mise exec -- dotnet run --project src/Encorely.Api
```

Al arrancar, el log muestra las 11 migraciones (`Applying migration '20261014050309_Initial'` … `'20261015155802_IndicesPurga'`)
y el seed:

```
Seed: 15 géneros
Seed: 120 canciones
Seed: 24 eventos
Seed: 16 usuarios demo
Seed: match demo del revisor
```

En una BD que ya tiene datos esas líneas no salen: el seed solo inserta lo que falta.

### 4.2 Verificación

```bash
curl http://localhost:5000/health/live     # Healthy (proceso vivo)
curl http://localhost:5000/health/ready    # Healthy (incluye la BD)
curl -s http://localhost:5000/openapi/v1.json | jq -r .info.title    # Encorely API (solo Development)

# Login con una cuenta demo (requiere DEMO_USER_PASSWORD)
curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H 'content-type: application/json' \
  -d '{"email":"valentina.rojas@encorely.demo","password":"<DEMO_USER_PASSWORD>"}' | jq '.user'
```

El login responde `accessToken`, `refreshToken`, `expiresIn: 900` y `user`. Con el access token:
`curl -H "authorization: Bearer <accessToken>" http://localhost:5000/api/v1/me`.

Cuentas demo: `admin@encorely.demo` (rol admin: `/api/v1/admin/*` y el panel en `/admin`), `revisor@encorely.demo` (para
revisores de tiendas, con un match sembrado) y 14 perfiles más en `Data/Seed/demo-users.json`. Todas usan
`DEMO_USER_PASSWORD`, que no está en el repo.

Desde el emulador de Android la API es `http://10.0.2.2:5000`; desde un teléfono en la misma red,
`http://<IP-del-PC>:5000`.

> Verificado el 2026-10-20 en un clon limpio de `dev`, con Postgres en el puerto 5433 y la API en 5100 (5000 y 5432 estaban
> ocupados por otra sesión; ver §4.3). `/health/live` y `/health/ready` respondieron `Healthy` unos 20 s después de
> `dotnet run`. La opción B (todo en contenedores) no se re-ejecutó porque publica el puerto 5000.

### 4.3 Puertos alternos y una segunda instancia

- **API en otro puerto:** `--urls` gana sobre `appsettings.Development.json`; `ASPNETCORE_URLS` no.

  ```bash
  mise exec -- dotnet run --project src/Encorely.Api --urls http://127.0.0.1:5100
  ```

- **Postgres en otro puerto:** una variable de la shell gana sobre el `.env`, tanto en compose como en la API
  (`DotEnv` no pisa variables ya definidas).

  ```bash
  DB_PORT=5433 docker compose up -d --wait
  DB_PORT=5433 mise exec -- dotnet run --project src/Encorely.Api --urls http://127.0.0.1:5100
  ```

- **Dos Postgres a la vez (otro proyecto de compose):** `docker-compose.yml` fija `container_name: encorely-postgres`, así que
  `docker compose -p <otro>` choca con el contenedor existente aunque esté detenido. Hay que renombrarlo con un archivo de
  override fuera del repo:

  ```yaml
  # /ruta/fuera/del/repo/compose.override.yml
  services:
    postgres:
      container_name: encorely-pg-2
  ```

  ```bash
  DB_PORT=5433 docker compose -p encorely-2 -f docker-compose.yml -f /ruta/fuera/del/repo/compose.override.yml up -d --wait
  # al terminar: docker compose -p encorely-2 -f docker-compose.yml -f /ruta/fuera/del/repo/compose.override.yml down -v
  ```

### 4.4 Opción B: todo en contenedores

Sin SDK: `docker compose --profile full up -d --build` construye la imagen y la levanta en `Development` con el mismo `.env`,
apuntando al Postgres del compose. API en `http://localhost:5000`.

### 4.5 Empezar de cero

`docker compose down -v` borra el contenedor y el volumen `encorely_pg` con todos los datos locales. El siguiente arranque
vuelve a migrar y sembrar.

## 5. Configuración

### 5.1 Cómo se carga

1. En `Development`, `DotEnv` lee el `.env` más cercano subiendo hasta la raíz del repo. No pisa variables ya definidas en
   la shell, docker o CI. Fuera de `Development` el `.env` se ignora.
2. `EnvironmentMapping` traduce las variables planas a secciones (`DB_HOST` → `Database:Host`, etc.).
3. Las opciones se validan al arrancar (`ValidateOnStart`). Si falta un obligatorio, la API no levanta. Ningún secreto tiene
   valor por defecto en el código.

En el `.env`, un `#` al inicio de un valor sin comillas o precedido de espacio abre un comentario, igual que en compose.

### 5.2 Variables

Fuente: [`.env.example`](../.env.example), `Infrastructure/Configuration/EnvironmentMapping.cs` y `Options.cs`.
"Defecto" es el valor que usa el código si la variable no está; "Obligatoria" indica si la API arranca sin ella.

| Variable | Qué controla | Defecto | Obligatoria |
|---|---|---|---|
| `DB_HOST` | Host de Postgres | — | Sí |
| `DB_PORT` | Puerto de Postgres (y puerto publicado por compose) | `5432` | No |
| `DB_NAME` | Nombre de la BD | — (compose: `encorely`) | Sí |
| `DB_USER` | Usuario de la BD | — (compose: `encorely`) | Sí |
| `DB_PASSWORD` | Contraseña de la BD; compose la usa para crearla | — | Sí (secreto) |
| `DB_SSL_MODE` | `Disable`, `Allow`, `Prefer`, `Require`, `VerifyCA`, `VerifyFull` | `Prefer` | Fuera de Development debe ser `Require` o superior |
| `DB_MIGRATE_ON_STARTUP` | Aplica migraciones al arrancar | `false` (`true` en Development) | No |
| `JWT_SIGNING_KEY` | Llave HS256 de los access tokens, mínimo 32 caracteres | — | Sí (secreto) |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Emisor y audiencia validados | `encorely-api` / `encorely-app` | No |
| `JWT_ACCESS_TOKEN_MINUTES` | Vida del access token (5–60) | `15` | No |
| `JWT_REFRESH_TOKEN_DAYS` | Vida del refresh token (1–90) | `30` | No |
| `BANNED_EMAIL_KEY` | Llave HMAC de los correos vetados, mínimo 32 caracteres, distinta de la del JWT. Cambiarla anula los vetos guardados | — | Sí (secreto) |
| `SEED_CATALOG` | Siembra géneros, canciones y eventos si faltan | `false` (`true` en Development) | No |
| `DEMO_USER_PASSWORD` | Si tiene valor, crea las cuentas demo con esa contraseña | vacío | No; fuera de Development solo con `SEED_REVIEWER_ONLY=true` |
| `SEED_REVIEWER_ONLY` | Siembra solo `revisor@` y su par, nunca el admin | `false` | No |
| `RADAR_MIN_SWIPES` | Canciones calificadas para desbloquear el radar (0–500) | `15` | No |
| `RADAR_MIN_AFFINITY` | Afinidad mínima (≥) para salir en el radar (0–100) | `0` | No |
| `AFFINITY_MIN_SWIPES` | Swipes para tener afinidad propia (o haber importado Spotify) (0–1000) | `25` | No |
| `RATE_LIMIT_AUTH_PER_MINUTE` | Registro, login y borrado web por IP y minuto | `10` | No |
| `AUTH_MAX_FAILED_LOGINS` | Fallos seguidos por (correo, IP) antes de bloquear | `5` | No |
| `AUTH_LOCKOUT_MINUTES` | Minutos de bloqueo del par (correo, IP) | `15` | No |
| `RATE_LIMIT_USER_WRITES_PER_MINUTE` | Escrituras por usuario y minuto | `120` | No |
| `RATE_LIMIT_REPORTS_PER_HOUR` | `POST /reports` por usuario y hora | `10` | No |
| `RATE_LIMIT_TOKENS_PER_MINUTE` | Refresh y logout por IP y minuto | `60` | No |
| `FORWARDED_TRUSTED_NETWORKS` | CIDR de los proxies de confianza, separados por coma | vacío (se ignora `X-Forwarded-For`) | Sí fuera de Development |
| `CHAT_MESSAGE_RETENTION_DAYS` | Días que se guarda un mensaje cifrado sin ack; `0` = sin límite | `30` | No |
| `PUSH_ENABLED` | Envía push por Expo Push | `false` | No |
| `EXPO_PUSH_ACCESS_TOKEN` | Token de Expo Push, si el proyecto lo exige | vacío | No (secreto) |
| `CORS_ORIGINS` | Orígenes CORS separados por coma (solo para Expo web) | vacío | No |
| `PUBLIC_BASE_URL` | URL pública de la API para enlaces legales y de borrado | vacío (se deriva del host) | Sí fuera de Development, y https |
| `PRIVACY_CONTACT_EMAIL` | Correo de consultas de datos personales | vacío | No |
| `SPOTIFY_CLIENT_ID` | Client ID público de la app de Spotify. Vacío: `POST /auth/spotify` da 502 `spotify_unavailable` | vacío | No |
| `SPOTIFY_ALLOWED_REDIRECTS` | Redirect URIs aceptadas, idénticas a las del Dashboard de Spotify | `encorely://spotify-callback` | No |

Hay valores que no salen de variables: la versión del consentimiento (`Legal:ConsentVersion`, `2026-10-14`) y su fecha
viven en `appsettings.json`. Cambiarlos obliga a todos los usuarios a aceptar de nuevo (`POST /me/consents`).

Spotify usa PKCE: no hay client secret en el back. Si un `.env` viejo trae uno, la API no lo lee.

## 6. Base de datos

### 6.1 Modelo

El diagrama entidad-relación y las reglas que garantiza la BD (unicidad, `CHECK`, índices parciales, cascadas, advisory locks)
están en [`ARCHITECTURE.md` §3](../ARCHITECTURE.md#3-modelo-de-datos). Nombres en `snake_case` (convención de EF
`UseSnakeCaseNamingConvention`) y enums guardados como texto.

Tablas actuales (25 más `__EFMigrationsHistory`), por área:

| Área | Tablas |
|---|---|
| Cuenta | `users`, `user_consents`, `refresh_tokens`, `device_push_tokens`, `banned_emails` |
| Música y perfil | `genres`, `songs`, `swipes`, `user_genres`, `user_genre_scores`, `user_artists`, `profile_reactions` |
| Eventos y match | `events`, `event_attendances`, `user_likes`, `matches` |
| Chat | `user_keys`, `user_box_keys`, `chat_messages`, `delivered_message_ids`, `message_franking_records` |
| Moderación | `blocks`, `reports` |
| Respaldo y vinculación | `encrypted_backup`, `device_link_request` |

Para inspeccionar la BD local:

```bash
docker exec -it encorely-postgres psql -U encorely -d encorely -c '\dt'
```

### 6.2 Migraciones

El esquema solo cambia con migraciones de EF Core (`Data/Migrations`). En Development se aplican al arrancar
(`DB_MIGRATE_ON_STARTUP=true`); los tests aplican todas sobre un Postgres real.

Crear una migración (después de cambiar entidades o `AppDbContext`):

```bash
mise exec -- dotnet tool restore
mise exec -- dotnet ef migrations add <Nombre> -p src/Encorely.Api -o Data/Migrations
```

Si `dotnet ef` no está disponible (no se restauraron las herramientas locales), el SDK de .NET 10 lo baja sin instalar nada:

```bash
mise exec -- dnx --yes dotnet-ef@10.0.13 migrations add <Nombre> -p src/Encorely.Api -o Data/Migrations
```

`DesignTimeDbContextFactory` usa una cadena ficticia (`Host=localhost;Database=encorely_design`): `migrations add` no necesita
BD ni secretos, pero `migrations list` no sabe cuáles están aplicadas y `migrations remove` intenta conectarse. Para
corregir una migración recién creada y aún no mergeada, se edita a mano (o se borran sus dos archivos y el cambio del
snapshot, revisando el diff).

Producción con una sola instancia migra al arrancar. Con varias instancias, las migraciones pasan a un paso previo del
despliegue (`efbundle`) y `DB_MIGRATE_ON_STARTUP=false`.

### 6.3 Seed

`Data/Seeder.cs` inserta lo que falta desde los JSON embebidos en `Data/Seed/`: `genres.json`, `songs.json`, `events.json` y
`demo-users.json`. Es idempotente. Las fechas de los eventos son relativas al día del seed, así que siempre quedan futuras.

- `SEED_CATALOG=true`: géneros, canciones y eventos.
- `DEMO_USER_PASSWORD` con valor: las 16 cuentas demo, sus gustos, asistencias cruzadas y el match del revisor.

Para regenerar el catálogo de canciones (portadas y previews de 30 s de iTunes Search):

```bash
python3 scripts/curate_songs.py    # reescribe src/Encorely.Api/Data/Seed/songs.json
```

Solo usa la biblioteca estándar, hace una pausa de ~1 s entre llamadas y omite candidatos que iTunes no encuentra. La lista
curada está dentro del script (`CATALOG`). Revisa el diff de `songs.json` antes de commitear. Este comando no se re-ejecutó
al escribir el manual porque llama a la API pública de iTunes.

## 7. API

### 7.1 Autenticación

- `POST /auth/register` o `POST /auth/login` devuelven un access token JWT (HS256, 15 min) y un refresh token opaco (30 días).
- La app manda `Authorization: Bearer <accessToken>`. Todo endpoint pide token salvo los marcados como públicos.
- `POST /auth/refresh` rota: devuelve un par nuevo e invalida el refresh usado. Reusar un refresh viejo revoca la familia
  completa; el siguiente refresh de esa sesión también da 401 `invalid_refresh_token` ("Tu sesión expiró. Inicia sesión de
  nuevo.").
- El access token lleva `sub` (usuario) y `sid` (sesión). Cada request comprueba que la cuenta exista, no esté suspendida y que
  la sesión siga abierta, así que logout, `DELETE /me/sessions/{id}`, reuso de refresh o ban cortan el token al instante.
- `/admin/*` exige rol admin, confirmado en la BD en cada request.
- `POST /auth/spotify` canjea un code PKCE de Spotify, crea la cuenta si el Spotify no está vinculado y descarta el token de
  Spotify. Nunca vincula por correo.

### 7.2 El contrato

[`contracts/openapi.v1.json`](../contracts/openapi.v1.json) se genera desde el código y `OpenApiContractTests` falla si no
coincide. La app genera sus tipos desde ese archivo. Para leerlo:

```bash
jq -r '.paths | keys[]' contracts/openapi.v1.json                    # rutas
jq '.paths["/api/v1/likes"].post' contracts/openapi.v1.json          # una operación
jq '.components.schemas.PublicProfileDto' contracts/openapi.v1.json  # un DTO
```

La API viva lo sirve en `/openapi/v1.json` solo en Development. JSON en camelCase, enums como string camelCase, fechas ISO-8601
UTC y binarios en base64 estándar.

Versionado: el contrato es `v1.0.0-draft.N`. Cada cambio de endpoints o DTOs se anota en
[`contracts/CHANGELOG.md`](../contracts/CHANGELOG.md), el más reciente arriba, marcando **Rompe** cuando el cliente debe
ajustarse. `info.version` del JSON se queda en `1.0.0`; el draft vigente lo da el CHANGELOG.

### 7.3 Errores

Todo error es `application/problem+json`:

```json
{"title":"Necesitas iniciar sesión.","status":401,"traceId":"0HNPF0DJF4DJ5:00000001","code":"unauthorized"}
```

- `title` y `detail` en español, para mostrar.
- `code` estable para ramificar en el cliente (`invalid_body`, `validation_failed`, `stale_recipient_key`,
  `chat_quota_exceeded`, `account_banned`…). Los posibles por operación están en el contrato.
- `errors` por campo (camelCase) en validación.
- `traceId` para cruzar con el log.
- Un recurso ajeno responde 404, no 403, para no confirmar que existe.
- Ninguna entrada inválida llega a 500: JSON mal formado, tipos equivocados, enums desconocidos, campos faltantes y el
  carácter U+0000 dan 400.
- Cuerpo máximo 256 KiB (413), salvo `PUT /me/backup` (15 MB) y `POST /reports` (1 MB). Los 429 llevan `Retry-After`.

Las excepciones de dominio se lanzan con `ApiException` (`Infrastructure/Http/ApiException.cs`):
`throw ApiException.Conflict("codigo_estable", "Título en español.")`.

### 7.4 Endpoints

Generada desde `contracts/openapi.v1.json` (68 operaciones). "Pública" = sin token. El detalle de cuerpos, respuestas y
códigos de error está en el contrato.

| Módulo | Operación | Auth | Qué hace |
|---|---|---|---|
| Auth | `POST /auth/register` | Pública | Crea una cuenta con email y contraseña e inicia sesión |
| Auth | `POST /auth/login` | Pública | Inicia sesión |
| Auth | `POST /auth/spotify` | Pública | Entra con Spotify (canjea el code PKCE). Crea la cuenta si el Spotify no está vinculado |
| Auth | `POST /auth/refresh` | Pública | Rota el refresh token y emite un access token nuevo |
| Auth | `POST /auth/logout` | Pública | Revoca el refresh token (y su familia) |
| Legal | `GET /legal/current` | Pública | Versión vigente de términos/política y URLs públicas |
| Legal | `POST /account-deletion` | Pública | Borra la cuenta con email y contraseña (página web de borrado, sin la app) |
| Perfil | `GET /me` | JWT | Mi perfil |
| Perfil | `PATCH /me` | JWT | Edita mi perfil (campos null no cambian) |
| Perfil | `DELETE /me` | JWT | Borra mi cuenta (pide la contraseña o reconfirmar con Spotify): datos personales, swipes, likes, matches y mensajes. Irreversible |
| Perfil | `POST /me/spotify/link` | JWT | Vincula mi cuenta de Spotify para poder entrar con ella |
| Perfil | `DELETE /me/spotify/link` | JWT | Desvincula Spotify (no si es mi única forma de entrar) |
| Perfil | `POST /me/consents` | JWT | Acepta una nueva versión de términos y política |
| Perfil | `GET /me/export` | JWT | Descarga mis datos personales (Ley 1581) |
| Perfil social | `GET /users/{id}/profile` | JWT | Perfil de otra persona; 404 si no existe, está suspendida o hay bloqueo en cualquier sentido |
| Perfil social | `PUT /users/{id}/reactions` | JWT | Reacciona a un artista o género de su perfil (reemplaza la reacción anterior a ese objetivo) |
| Perfil social | `DELETE /users/{id}/reactions` | JWT | Quita mi reacción a ese objetivo (204 aunque no hubiera) |
| Perfil social | `GET /me/reactions/received` | JWT | Quién reaccionó a mi perfil, de la más reciente a la más vieja. ?before=nextCursor pagina |
| Música | `GET /genres` | Pública | Catálogo de géneros |
| Música | `GET /songs/feed` | JWT | Canciones aún no calificadas por mí, mezclando mis géneros y exploración |
| Música | `POST /swipes` | JWT | Califica una canción (like/dislike). Repetir la misma canción no suma |
| Música | `GET /me/dna` | JWT | Mi ADN musical (vector de géneros) y estado del radar |
| Música | `POST /me/spotify/import` | JWT | Importa top artistas de Spotify (leídos en el dispositivo) para enriquecer el ADN; avisa los géneros que no mapean |
| Eventos | `GET /events` | JWT | Eventos futuros publicados, por fecha |
| Eventos | `GET /events/{id}` | JWT | Detalle de un evento |
| Eventos | `PUT /events/{id}/attendance` | JWT | Marco que voy al evento (idempotente) |
| Eventos | `DELETE /events/{id}/attendance` | JWT | Ya no voy al evento (idempotente) |
| Eventos | `GET /me/events` | JWT | Eventos futuros a los que voy |
| Eventos | `GET /events/{id}/radar` | JWT | Asistentes del evento ordenados por afinidad real. Requiere ir al evento y haber desbloqueado el radar |
| Match | `POST /likes` | JWT | Like a una persona con la que comparto un evento. Hay match solo si el like es recíproco (idempotente) |
| Match | `GET /matches` | JWT | Mis matches activos, el más reciente primero; paginable con limit, beforeAt y beforeId |
| Match | `GET /matches/{id}` | JWT | Un match mío |
| Match | `DELETE /matches/{id}` | JWT | Deshace el match y cierra el chat |
| Llaves E2EE | `PUT /keys/me` | JWT | Publica o rota mis llaves públicas. Si cambia la identidad, se avisa a mis matches (PeerKeyChanged) |
| Llaves E2EE | `GET /keys/me` | JWT | Mis llaves públicas vigentes (para saber el último boxKeyId) |
| Llaves E2EE | `GET /matches/{id}/peer-keys` | JWT | Llaves públicas vigentes de la otra persona del match |
| Llaves E2EE | `GET /matches/{id}/peer-keys/box/{boxKeyId}` | JWT | Una llave de caja histórica de la otra persona (para descifrar mensajes viejos) |
| Chat | `POST /matches/{id}/messages` | JWT | Envía un mensaje cifrado. 201 al crearlo; 200 si el mismo id ya existía con el mismo contenido |
| Chat | `GET /matches/{id}/messages` | JWT | Mensajes cifrados del match. ?before=seq pagina hacia atrás (desc); ?after=seq trae los nuevos (asc) |
| Chat | `POST /matches/{id}/messages/ack` | JWT | Confirma que guardé localmente los mensajes recibidos hasta upToSeq; el servidor los borra |
| Moderación | `POST /blocks` | JWT | Bloquea a un usuario: cierra el match, desaparece del radar y no puede escribirme (idempotente) |
| Moderación | `GET /blocks` | JWT | Usuarios que bloqueé |
| Moderación | `DELETE /blocks/{userId}` | JWT | Desbloquea (no reabre el match) |
| Moderación | `POST /reports` | JWT | Reporta a un usuario para revisión; si ya tengo uno abierto contra esa persona, le suma la evidencia (mismo id) |
| Push | `PUT /devices/push-token` | JWT | Registra el token push de este dispositivo |
| Push | `DELETE /devices/push-token` | JWT | Quita el token push (al cerrar sesión) |
| Respaldo cifrado | `PUT /me/backup` | JWT | Sube (reemplaza) mi respaldo cifrado en el dispositivo |
| Respaldo cifrado | `GET /me/backup` | JWT | Descarga mi respaldo cifrado |
| Respaldo cifrado | `DELETE /me/backup` | JWT | Borra mi respaldo |
| Respaldo cifrado | `GET /me/backup/info` | JWT | Tamaño y fecha de mi respaldo, sin descargarlo |
| Vincular dispositivo | `POST /keys/link-requests` | JWT | Dispositivo NUEVO: abre un buzón de vinculación (vence en 10 min) |
| Vincular dispositivo | `GET /keys/link-requests/{id}` | JWT | Consulta el buzón (el nuevo lo sondea hasta que esté ready) |
| Vincular dispositivo | `PUT /keys/link-requests/{id}` | JWT | Dispositivo VIEJO: deja el material de llaves cifrado hacia la llave efímera del nuevo (una sola vez) |
| Vincular dispositivo | `DELETE /keys/link-requests/{id}` | JWT | El nuevo ya recogió el material: se destruye el buzón |
| Sesiones | `GET /me/sessions` | JWT | Mis sesiones abiertas (dispositivos con sesión) |
| Sesiones | `DELETE /me/sessions` | JWT | Cierra todas mis sesiones, incluida esta |
| Sesiones | `DELETE /me/sessions/{id}` | JWT | Cierra una sesión (p. ej. un teléfono perdido) |
| Administración | `GET /admin/events` | JWT admin | Eventos (incluye no publicados y, opcionalmente, pasados) |
| Administración | `POST /admin/events` | JWT admin | Crea un evento |
| Administración | `PUT /admin/events/{id}` | JWT admin | Edita un evento |
| Administración | `DELETE /admin/events/{id}` | JWT admin | Borra un evento y sus asistencias |
| Administración | `GET /admin/songs` | JWT admin | Catálogo de canciones con conteo de likes/dislikes |
| Administración | `POST /admin/songs` | JWT admin | Agrega una canción al catálogo |
| Administración | `PUT /admin/songs/{id}` | JWT admin | Edita una canción (isActive=false la saca del feed) |
| Administración | `GET /admin/itunes/search` | JWT admin | Busca en iTunes Search para dar de alta canciones con preview |
| Administración | `GET /admin/reports` | JWT admin | Reportes, los más antiguos primero |
| Administración | `POST /admin/reports/{id}/resolve` | JWT admin | Resuelve un reporte; ban suspende al reportado, cierra sus matches y revoca sus sesiones |
| Administración | `PUT /admin/users/{id}/status` | JWT admin | Suspende o reactiva una cuenta |

Rutas fuera de `/api/v1` (no están en el contrato):

| Ruta | Qué es |
|---|---|
| `/health/live`, `/health/ready` | Health checks, públicos y sin detalle |
| `/hubs/chat` | SignalR, autenticado ([§8](#8-tiempo-real-y-push)) |
| `/admin` | Panel de administración en HTML (login con una cuenta admin) |
| `/cuenta/eliminar` | Borrado de cuenta desde la web (lo exige Google Play) |
| `/legal/terminos`, `/legal/privacidad` | Términos y política de privacidad |
| `/openapi/v1.json`, `/swagger` | Solo en Development (ver [§14](#14-pendientes-detectados) sobre `/swagger`) |

## 8. Tiempo real y push

### 8.1 ChatHub

`Infrastructure/Realtime/ChatHub.cs`, en `/hubs/chat`. Es `[Authorize]`, se cierra cuando vence el token
(`CloseOnAuthenticationExpiration`) y no tiene métodos invocables desde la app: solo avisa. La fuente de verdad es siempre el
REST; al recibir un aviso, la app consulta el endpoint que corresponde.

| Evento | Payload | La app hace |
|---|---|---|
| `MessageAvailable` | `{matchId, id, seq}` | `GET /matches/{id}/messages?after=<seq>` |
| `PeerKeyChanged` | `{matchId, userId}` | vuelve a pedir `peer-keys` y avisa del cambio de llave |
| `MatchClosed` | `{matchId}` | cierra el chat |
| `MatchCreated` | `{matchId}` | refresca la lista de matches |

El token viaja en `?access_token=<jwt>` solo en `/hubs/*` (los WebSocket no admiten cabeceras desde el navegador). El log de
acceso nunca registra query strings y el log de hosting de ASP.NET está en `Warning` por la misma razón.

### 8.2 Push

Envío por Expo Push API (`https://exp.host/--/api/v2/push/send`), que entrega por FCM y APNs. Se activa con
`PUSH_ENABLED=true`.

- La app registra su token con `PUT /devices/push-token`. Solo se aceptan `ExponentPushToken[…]` o `ExpoPushToken[…]`. El token
  queda ligado a la sesión (`sid`) y se borra al cerrarla. Si el token era de otra cuenta, pasa a la nueva y se cierra la sesión
  anterior en ese teléfono.
- Qué se envía (`Infrastructure/Push/PushDispatcher.cs`): `title: "Encorely"`, `body: "Tienes mensajes nuevos"` o
  `"Tienes un match nuevo"`, `channelId: "messages"` (canal que crea la app en Android) y `data: {t: "chat" | "match", matchId}`.
- Qué no se envía: contenido del mensaje, nombre o foto del remitente.
- El envío sale de una cola acotada en segundo plano (`PushQueue`): no frena la request. `PushReceiptChecker` revisa los
  receipts cada 15 min y borra los tokens que FCM/APNs marcan como `DeviceNotRegistered`.

## 9. Seguridad

La tabla de controles (OWASP ASVS 4.0 nivel 1) y los riesgos residuales aceptados están en
[`ARCHITECTURE.md` §6](../ARCHITECTURE.md#6-seguridad-base-owasp-asvs-40-nivel-1). Lo que hay que respetar al tocar código:

- **Identidad solo desde el JWT.** Usa `ClaimsPrincipal.UserId()` (claim `sub`). Ningún DTO recibe un `userId` para decidir
  quién llama. Así se cerró el IDOR sistémico del proyecto anterior.
- **Autorización por pertenencia.** Cada servicio filtra por el usuario del token. Recurso ajeno → 404. Cada endpoint con
  recursos tiene su test "A no accede a lo de B" ([§10.3](#103-agregar-un-test-de-autorización)).
- **Rate limit.** Políticas en `Program.cs`: por IP en auth (`RATE_LIMIT_AUTH_PER_MINUTE`) y tokens
  (`RATE_LIMIT_TOKENS_PER_MINUTE`), por usuario en escrituras (`.UserWrites()` en el endpoint) y reportes. Bloqueo por
  (correo, IP) tras `AUTH_MAX_FAILED_LOGINS` fallos, en memoria (`LoginThrottle`). Detrás de un proxy, sin
  `FORWARDED_TRUSTED_NETWORKS` todos los clientes comparten la IP del proxy.
- **Chat E2EE visto desde el servidor.** El servidor guarda y entrega solo ciphertext, nonce, ids de llaves y un
  `frankingTag` opcional; nunca texto plano ni llaves privadas. `ChatCanaryTests` busca un texto conocido en todas las tablas y
  en todo el log (en claro, base64 y hex). No agregues logs de cuerpos de request.
- **Límites declarados.** Sin forward secrecy en la v1 (quien robe una llave privada descifra lo capturado), un dispositivo
  por defecto, verificación manual del código de seguridad. El producto dice "cifrado de extremo a extremo", nunca "como
  Signal". Detalle en [`ARCHITECTURE.md` §5](../ARCHITECTURE.md#5-chat-cifrado-de-extremo-a-extremo).
- **Cabeceras.** Toda respuesta de `/api` lleva `Cache-Control: no-store`, `X-Frame-Options: DENY` y
  `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`. HSTS fuera de Development.
- **Secretos.** Solo en `.env` (ignorado por git) o en el proveedor de despliegue. `JWT_SIGNING_KEY` y `BANNED_EMAIL_KEY`
  se generan con `openssl rand -base64 48`, una distinta para cada una. No commitear `.env` ni `db/certs/` (ignorado). Rotar
  `JWT_SIGNING_KEY` invalida los access tokens vigentes (los refresh siguen sirviendo, porque son opacos y viven en la BD);
  para cerrar sesiones hay que revocarlas. Rotar `BANNED_EMAIL_KEY` anula los vetos guardados.

## 10. Pruebas

### 10.1 Correr la suite

```bash
mise exec -- dotnet test tests/Encorely.Tests
```

Requiere Docker: Testcontainers levanta un Postgres propio en un puerto aleatorio, así que no choca con el de compose ni
necesita `.env`. Un contenedor y una API en memoria (`WebApplicationFactory`) sirven a toda la suite; cada test crea sus
usuarios y por eso corren en paralelo.

> Verificado el 2026-10-20 en el clon limpio: 332 de 332 en unos 2 min. En una corrida anterior, con la máquina cargada (build
> del front en paralelo), `SyncSessionsTests.Cotas_de_tamano_y_de_parametros` cortó por timeout. Ver [§13](#13-solución-de-problemas).

Un subconjunto: `mise exec -- dotnet test tests/Encorely.Tests --filter "FullyQualifiedName~ChatTests"`.

### 10.2 Qué cubre

Un archivo por feature en `tests/Encorely.Tests/Features/`. Entre otros:

- aislamiento entre usuarios en cada endpoint con recursos;
- like unilateral sin match, y likes cruzados concurrentes que crean exactamente uno (20 rondas);
- `ChatCanaryTests`: ni la BD ni los logs contienen el texto plano del chat;
- rotación de refresh tokens con detección de reuso;
- ninguna entrada inválida da 500;
- arranque con configuración inválida (`StartupConfigurationTests`);
- `OpenApiContractTests`: el contrato versionado coincide con el código.

### 10.3 Agregar un test de autorización

Patrón de `SyncSessionsTests`: dos usuarios con `ApiFixture.CreateUserAsync()`, cada uno con su `HttpClient` ya autenticado,
y las aserciones de `HttpAssertions` (`Expect`, `ExpectProblem`).

```csharp
public sealed class MiFeatureTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task B_no_ve_ni_borra_el_recurso_de_A()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var creado = await a.Client.PostJson("/api/v1/mi-recurso", new { nombre = "x" }).Expect<MiRecursoDto>(HttpStatusCode.Created);

        await b.Client.GetAsync($"/api/v1/mi-recurso/{creado.Id}", Ct).ExpectProblem(HttpStatusCode.NotFound);
        await b.Client.DeleteAsync($"/api/v1/mi-recurso/{creado.Id}", Ct).ExpectProblem(HttpStatusCode.NotFound);
        await a.Client.GetAsync($"/api/v1/mi-recurso/{creado.Id}", Ct).Expect(HttpStatusCode.OK);
    }
}
```

`ExpectProblem` acepta además el `code` esperado: `.ExpectProblem(HttpStatusCode.Conflict, "codigo_estable")`. Para leer o
preparar datos directo en la BD: `api.WithDbAsync(db => …)`.

## 11. CI/CD y despliegue

### 11.1 CI

`.github/workflows/ci.yml` corre en push a `main`, `dev` y `rebuild/v1`, y en cada pull request.

| Job | Pasos |
|---|---|
| `build-test` | `dotnet restore` → `dotnet format --verify-no-changes` (excluye `Data/Migrations`) → falla si hay paquetes vulnerables (incluye transitivos) → build Release con warnings como errores → `dotnet test` con Postgres real |
| `docker` | `docker build` → arranca la imagen como en Render (`Production`, sin BD) y comprueba `/health/live` y `Cache-Control: no-store` en `/api/v1/events` |

Antes de abrir un PR, para no esperar a la CI:

```bash
mise exec -- dotnet format Encorely.slnx --verify-no-changes --exclude src/Encorely.Api/Data/Migrations
mise exec -- dotnet build Encorely.slnx -c Release
mise exec -- dotnet test tests/Encorely.Tests
```

Los tres se corrieron el 2026-10-20 sobre `dev`: format sin cambios, build Release con 0 advertencias y 0 errores, y la suite
como se cuenta en [§10.1](#101-correr-la-suite).

### 11.2 Despliegue

No se publica en esta entrega (D15). Lo preparado:

- `Dockerfile`: imagen sin secretos, usuario no root, escucha en 8080.
- `render.yaml`: Blueprint de Render con el servicio web (`plan: starter`, sin sleep porque el hub necesita la instancia
  despierta), Postgres 16, `healthCheckPath: /health/live`, `DB_SSL_MODE=Require`, `JWT_SIGNING_KEY` y `BANNED_EMAIL_KEY`
  generadas por Render, y los valores que hay que completar a mano marcados `sync: false`.

Para publicar falta:

1. Crear el Blueprint en Render y completar `FORWARDED_TRUSTED_NETWORKS`, `PUBLIC_BASE_URL` (https), `PRIVACY_CONTACT_EMAIL`
   y, si aplica, `EXPO_PUSH_ACCESS_TOKEN` y `SPOTIFY_CLIENT_ID`.
2. Crear el admin de producción a mano, con contraseña propia. El seed nunca lo crea fuera de Development.
3. Para la revisión de tiendas, `SEED_REVIEWER_ONLY=true` con un `DEMO_USER_PASSWORD` propio de esa revisión.
4. Pasar a `DB_SSL_MODE=VerifyFull` si el proveedor da un certificado verificable.
5. Con más de una instancia: migraciones como paso previo (`efbundle`), backplane de SignalR y un contador de intentos
   compartido (hoy `LoginThrottle` vive en memoria).

Fuera de Development la API no arranca sin `FORWARDED_TRUSTED_NETWORKS`, `PUBLIC_BASE_URL` https y `DB_SSL_MODE` `Require` o
superior. Los logs salen en JSON, una línea por evento, buscables por `traceId`.

## 12. Mantenimiento

### 12.1 Agregar un endpoint de punta a punta

1. **Contrato del feature.** DTOs en `Features/<Feature>/<Feature>Contracts.cs`, con DataAnnotations para validar.
2. **Regla.** Método en `<Feature>Service.cs` que recibe el `userId` del token y filtra por él. Errores con `ApiException`.
3. **Ruta.** En `<Feature>Endpoints.cs`, con nombre, resumen en español y errores declarados. Ejemplo real
   (`Features/Devices/DeviceEndpoints.cs`):

   ```csharp
   g.MapPut("/push-token", Register).WithName("RegisterPushToken")
       .WithSummary("Registra el token push de este dispositivo").Problems(400, 401).UserWrites();
   ```

   `.UserWrites()` aplica el rate limit por usuario a las escrituras. Un endpoint nuevo pide token por defecto (la política
   de fallback exige autenticación); `AllowAnonymous()` solo si de verdad es público.
4. **Esquema.** Si hay entidades nuevas, configúralas en `AppDbContext` y crea la migración ([§6.2](#62-migraciones)).
5. **Tests.** Caso feliz, validación (400 sin 500) y aislamiento entre usuarios ([§10.3](#103-agregar-un-test-de-autorización)).
6. **Contrato.** Regenera y revisa el diff:

   ```bash
   scripts/update-contract.sh     # requiere Docker; corre OpenApiContractTests con UPDATE_CONTRACT=1
   git diff contracts/openapi.v1.json
   ```

   El script llama a `dotnet` directamente: córrelo con mise activado o como `mise exec -- scripts/update-contract.sh`.
7. **CHANGELOG.** Entrada nueva arriba en `contracts/CHANGELOG.md` con fecha, `v1.0.0-draft.<N+1>` y qué cambia, marcando
   **Rompe** si el cliente debe ajustarse. Avisa al front: regenera sus tipos con `pnpm gen:api`.

### 12.2 Logs

- Development: consola legible. Fuera de Development: JSON por línea con timestamp UTC y scopes.
- El log de acceso propio registra método, ruta sin query, status y duración. Nunca cuerpos, tokens ni query strings.
- Los comandos SQL de EF están en `Critical` para que no salgan parámetros al log.

### 12.3 Health checks y tareas de fondo

- `/health/live`: el proceso responde (liveness, sin dependencias). Es el que usa Render.
- `/health/ready`: incluye la BD (readiness).
- `MaintenanceService` (cada hora): borra buzones de vinculación vencidos, refresh tokens vencidos hace más de 30 días,
  mensajes con más de `CHAT_MESSAGE_RETENTION_DAYS` días sin ack, registros de franking e ids entregados de más de 180 días,
  y purga la evidencia de los reportes resueltos hace más de 180 días.

## 13. Solución de problemas

Errores vistos en el proyecto, con el mensaje real.

| Síntoma | Causa | Solución |
|---|---|---|
| `A compatible .NET SDK was not found. Requested SDK version: 10.0.402` | `dotnet` es el del sistema (p. ej. 8.0 en `/usr/share/dotnet`) porque mise no está activado en esa shell | `mise exec -- dotnet …`, o activar mise en `~/.zshrc` |
| `mise ERROR Config files in …/mise.toml are not trusted` | Clon nuevo | `mise trust` en la raíz del repo |
| La API arranca en 5000 aunque definas `ASPNETCORE_URLS` | `appsettings.Development.json` fija `Urls` | `dotnet run --project src/Encorely.Api --urls http://127.0.0.1:<puerto>` |
| `Conflict. The container name "/encorely-postgres" is already in use` | `container_name` fijo en `docker-compose.yml`; pasa con `-p <otro proyecto>` o con un contenedor viejo detenido | Reusar el existente (`docker compose up -d`) o renombrarlo con un override ([§4.3](#43-puertos-alternos-y-una-segunda-instancia)) |
| `docker compose` falla con `Define DB_PASSWORD en .env` | No existe `.env` o `DB_PASSWORD` está vacía | `cp .env.example .env` y completar |
| `dotnet-ef not found` | No se restauraron las herramientas locales | `mise exec -- dotnet tool restore`, o `mise exec -- dnx --yes dotnet-ef@10.0.13 …` |
| `migrations list` muestra `Pending status not shown` y `migrations remove` falla | `DesignTimeDbContextFactory` usa una BD ficticia | Editar la migración a mano antes de mergear ([§6.2](#62-migraciones)) |
| `Npgsql.NpgsqlException: SSL connection requested. No SSL enabled connection from this host is configured.` | `DB_SSL_MODE=Require` contra el Postgres local (`postgres:16-alpine` no tiene TLS) | En local, `DB_SSL_MODE=Prefer`. `Require` o superior solo contra un Postgres con TLS |
| Fuera de Development: `FORWARDED_TRUSTED_NETWORKS es obligatorio fuera de desarrollo…` o `PUBLIC_BASE_URL es obligatorio fuera de desarrollo y debe ser una URL https absoluta.` | Validación de arranque de producción | Definir las dos. Para probar la imagen en local, ver el smoke de `ci.yml` |
| `DataAnnotation validation failed for 'JwtOptions' members: 'SigningKey'…` (o `'ModerationOptions'… 'BannedEmailKey'`) | Falta el secreto o tiene menos de 32 caracteres | `openssl rand -base64 48` para cada uno |
| Una contraseña del `.env` con ` #` no funciona | Un `#` precedido de espacio abre un comentario y recorta el valor (compose y la API lo cortan igual desde que `DotEnv` imita a compose) | Valor entre comillas, o generar la contraseña sin ` #` |
| `OpenApiContractTests` falla en CI | Cambiaron endpoints o DTOs sin regenerar el contrato | `scripts/update-contract.sh` y entrada en el CHANGELOG |
| Paso `Formato` de CI falla | `dotnet format` encontró diferencias | Correr `dotnet format Encorely.slnx --exclude src/Encorely.Api/Data/Migrations` y commitear |
| `SyncSessionsTests.Cotas_de_tamano_y_de_parametros` falla con `TaskCanceledException … The client aborted the request.` | Sube cuerpos de hasta 15 MB y corta por tiempo cuando la máquina está cargada | Repetir con `--filter "FullyQualifiedName~SyncSessionsTests"`; si falla también solo, es un error real |

## 14. Pendientes detectados

Diferencias entre la documentación y el código al 2026-10-20 (rama `dev`, `3730181`). No se corrigieron en esta tarea.

1. **`/swagger` responde 401 en Development.** `README.md` dice que Swagger UI abre en `/swagger`, pero `UseSwaggerUI` se
   registra después de la autorización y la política de fallback exige token para cualquier ruta sin endpoint. Verificado:
   `/swagger`, `/swagger/` y `/swagger/index.html` dan 401; `/openapi/v1.json` sí da 200. Es un bug de código, no de docs.
2. **`README.md` remite a la app en la rama `rebuild/v1`.** La rama de trabajo del front es `Dev`.
3. **`README.md` usa `open http://localhost:5000/swagger`**, que es de macOS; en Linux es `xdg-open` (y hoy daría 401, ver 1).
4. **`README.md` corre `dotnet …` sin mise.** En una máquina con otro SDK en el `PATH` falla por `global.json`; falta
   `mise exec --` o la nota de activar mise. Tampoco menciona `mise trust` en un clon nuevo.
5. **`ARCHITECTURE.md` §3 dice que `banned_emails` guarda un hash SHA-256 del correo.** Desde FIN-25 es HMAC-SHA256 con
   `BANNED_EMAIL_KEY` (`Features/Auth/BannedEmail.cs`); los vetos viejos en SHA-256 se siguen buscando.
6. **`ARCHITECTURE.md` §1 no lista el feature `Profiles`** (perfil social: `/users/{id}/profile`, reacciones), que existe en
   `Features/Profiles`. La estructura del `README.md` tampoco lo nombra.
7. **El diagrama ER de `ARCHITECTURE.md` §3 no incluye** `delivered_message_ids`, `user_artists`, `profile_reactions`,
   `message_franking_records` ni `banned_emails` (algunas aparecen en el texto), y nombra `encrypted_backups` y
   `device_link_requests` en plural, mientras las tablas reales son `encrypted_backup` y `device_link_request`.
8. **`ARCHITECTURE.md` §8 describe la limpieza horaria** solo con buzones de vinculación y refresh tokens. `MaintenanceService`
   también vence mensajes del chat (`CHAT_MESSAGE_RETENTION_DAYS`) y purga evidencia de reportes a los 180 días.
9. **`info.version` del contrato es `1.0.0`** mientras el CHANGELOG va en `v1.0.0-draft.17`. No es un error, pero quien lea solo
   el JSON no sabe qué draft tiene.
10. **`docker-compose.yml` fija `container_name: encorely-postgres`**, lo que impide levantar un segundo proyecto de compose
    (otra sesión, QA) sin un override.
