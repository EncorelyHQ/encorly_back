# Cambios del contrato `openapi.v1.json`

El archivo se genera desde el código (`scripts/update-contract.sh`) y un test falla si queda desactualizado.
Cada cambio de endpoints o DTOs se anota aquí, el más reciente arriba. **Rompe** = el cliente debe ajustarse.

## 2026-10-14 · v1.0.0-draft.9 (bloqueo por intentos)
- Cambio de comportamiento (no cambia el esquema): el **429 `account_locked`** de `POST /auth/login`, `POST /account-deletion`
  y `DELETE /me` con contraseña ahora es por **correo + IP**: tras 5 contraseñas fallidas desde una IP contra un correo, ese
  par queda bloqueado 15 min; desde otra IP se sigue entrando. Sale también para correos sin cuenta, y durante el bloqueo no se
  evalúa la contraseña (ni la correcta). El cliente lo trata igual que antes: "Demasiados intentos, espera unos minutos".

## 2026-10-14 · v1.0.0-draft.8 (login con Spotify)
- Nuevo: `POST /auth/spotify` `{ code, codeVerifier, redirectUri, acceptTerms, consentVersion }`. La app hace el flujo PKCE
  (scope `user-read-email`) y manda el code **sin canjearlo**; el back lo canjea y lee `/v1/me`. Responde lo mismo que el login
  más `isNewUser`. Si el Spotify ya está vinculado, entra (el consentimiento se ignora). Si no, crea una cuenta **sin contraseña**
  con el correo de Spotify (`onboardingCompleted=false`, `isNewUser=true`). Nunca vincula una cuenta existente por correo.
  `redirectUri` debe estar en `SPOTIFY_ALLOWED_REDIRECTS` (hoy `encorely://spotify-callback`). Errores:
  - **400 `consent_required`**: cuenta nueva sin `acceptTerms` o con otra versión. El code ya se gastó: hay que repetir el flujo.
  - **400 `spotify_invalid_code`**: code vencido, ya usado, o verifier/redirect que no casan.
  - **400 `spotify_email_missing`**: Spotify no entregó un correo válido (solo al crear cuenta).
  - **400 `validation_failed`**: `redirectUri` no permitida o campos fuera de rango (el verifier mide 43 a 128).
  - **403 `account_banned`**: cuenta suspendida, o el correo es de una cuenta suspendida y borrada.
  - **403 `spotify_user_not_allowed`**: la app de Spotify está en modo desarrollo y ese usuario no está en el Dashboard.
  - **409 `spotify_email_in_use`**: ya hay una cuenta con ese correo; entrar con contraseña y vincular desde el perfil.
  - **502 `spotify_unavailable`**: Spotify no respondió (o el servidor no tiene `SPOTIFY_CLIENT_ID`).
- Nuevo: `POST /me/spotify/link` (body `SpotifyGrant`: `{ code, codeVerifier, redirectUri }`) → **204**; idempotente con el mismo
  Spotify. **409 `spotify_already_linked`** si ese Spotify es de otro perfil o el perfil ya tiene otro (desvincular primero).
  Mismos 400/403/502 de Spotify que el login.
- Nuevo: `DELETE /me/spotify/link` → **204** (también si no había nada). **409 `spotify_last_login_method`** si la cuenta no
  tiene contraseña.
- Nuevo (no rompe): `GET /me` (y `PATCH /me`, y `profile` de `/me/export`) ganan `spotifyLinked` y `hasPassword`.
- Cambio (no rompe a quien ya manda la contraseña): `DELETE /me` acepta `{ "password": string }` **o**
  `{ "spotify": SpotifyGrant }` (un grant nuevo de la cuenta vinculada), exactamente uno: ninguno o ambos → **400**. Spotify de
  otra cuenta → **403 `spotify_account_mismatch`**. En el esquema `password` pasa a opcional.
- Cambio de comportamiento: `POST /auth/login` con el correo de una cuenta sin contraseña da el 401 `invalid_credentials`
  genérico y no suma intentos fallidos. El borrado web (`/account-deletion`) tampoco sirve para esas cuentas: se borran desde la app.

## 2026-10-14 · v1.0.0-draft.7 (conciliación del back)
- Arreglo (no rompe): `GET /admin/reports?status=open|resolved|dismissed` respondía 400 con los valores en minúsculas que
  anunciaba el contrato. Ahora el parámetro es un `string` con `enum` explícito (`open`, `resolved`, `dismissed`; por defecto `open`).
  Cualquier otro valor (`Open`, `1`, `closed`) da **400**.
- Cambio de comportamiento (no cambia el esquema): cerrar una sesión (`DELETE /me/sessions/{id}`, `DELETE /me/sessions`,
  `POST /auth/logout` o el reuso de un refresh token) invalida **en el acto** el access token de esa sesión (claim `sid`):
  cualquier llamada con él responde **401 `unauthorized`**, y su conexión al hub se corta. Antes seguía sirviendo hasta 15 min.
  Las demás sesiones del mismo usuario no se ven afectadas. Un 401 tras refrescar con éxito ya no debería darse: si el refresh
  también da 401, la sesión se cerró desde otro dispositivo y hay que volver al login.
- **Rompe:** `DELETE /me` exige body `{ "password": string }` (la contraseña actual). Sin body o vacía → **400**;
  contraseña incorrecta → **403 `invalid_password`** (403 y no 401: la sesión sigue válida, el cliente no debe refrescar ni
  cerrar sesión, solo pedir de nuevo la contraseña). Comparte el bloqueo por intentos del login: tras 5 fallos responde
  **429 `account_locked`**, y tiene el mismo rate limit por IP que `/auth/*`. Con la contraseña correcta, 204 como antes.
- Nuevo (no rompe): `POST /matches/{id}/messages` con el `id` de un mensaje que el receptor ya confirmó (ack) ya no crea un
  duplicado. Si es el mismo mensaje (mismo remitente y `frankingTag`) → **409 `message_already_delivered`**: el cliente lo da
  por entregado. Si el contenido o el remitente cambian → **409 `message_id_conflict`**. Antes se aceptaba con un `seq` nuevo y
  la evidencia de ese contenido no verificaba al reportarlo.
- Cambio de comportamiento (no cambia el esquema): suspender una cuenta borra todos sus likes, dados y recibidos. Si se
  reactiva, no recupera matches: volver a hacer match exige dos likes nuevos, igual que tras un unmatch o un bloqueo.

## 2026-10-14 · v1.0.0-draft.6 (pedidos del front)
Todo son campos nuevos al final de cada DTO: no rompe a clientes que ignoran campos desconocidos.
- `GET /legal/current`: si el servidor no tiene `PUBLIC_BASE_URL`, `termsUrl`, `privacyUrl` y `accountDeletionUrl` se arman con el
  scheme y host de la request (desde el emulador: `http://10.0.2.2:5000/...`). Con `PUBLIC_BASE_URL` se usa tal cual. Ya no hay default `localhost`.
- `GET /legal/current` gana `privacyContactEmail` (string|null): correo de consultas y reclamos (`PRIVACY_CONTACT_EMAIL`).
  La página `/legal/privacidad` lo muestra como canal si está configurado.
- `GET /me` (y `PATCH /me`, y `profile` de `/me/export`) ganan `acceptedConsentVersion` (string|null, la versión más reciente aceptada)
  y `consentUpToDate` (bool, true si es la vigente de `/legal/current`). Si es false: pedir aceptar y `POST /me/consents`.
- `GET /me/sessions`: cada sesión gana `current` (bool): true en la sesión del access token con que se llama (claim `sid`).
- Push (payload hacia Expo): `channelId: "messages"` (la app debe crear ese canal Android), `data.t` = `"chat"` | `"match"` en lugar de
  `data.type` (`newMessage`/`newMatch`, **eliminado**), `data.matchId` igual. El servidor ahora consulta los receipts de Expo y borra
  los tokens `DeviceNotRegistered`: si la app deja de recibir avisos, debe volver a registrar el token al abrirse.
- Seed demo: `revisor@encorely.demo` ya tiene un match activo con `valentina.rojas@encorely.demo` (evento compartido), con chat listo.

## 2026-10-14 · v1.0.0-draft.5 (endurecimiento de seguridad)
- Nuevo (no rompe): `POST /auth/register` responde **403 `account_banned`** ("No puedes crear una cuenta con este correo.")
  si el correo perteneció a una cuenta suspendida y luego borrada. Un correo con cuenta viva sigue dando 409 `email_taken`.
  El borrado de cuenta sigue permitido aunque esté suspendida.
- Nuevo (no rompe): `POST /account-deletion` aplica el mismo bloqueo por intentos que el login: tras 5 fallos
  (configurable) responde **429 `account_locked`** también con la contraseña correcta, y no borra nada.
- Nuevo (no rompe): `GET /events/{id}/radar` responde **400 `event_past`** si el evento empezó hace más de 1 día (mismo corte que `POST /likes`).
- Nuevo (no rompe): el carácter U+0000 en cualquier texto del JSON o en query strings da **400** (`invalid_body`), nunca 500.
- Nuevo (no rompe): `null` dentro de listas (`genres`, `topArtists`, `topArtists[].genres`, `evidence`, `artistLineup`)
  da 400 `validation_failed` con "No puede contener elementos vacíos.".
- Nuevo (no rompe): `POST /reports` con evidencia verifica el franking aunque el receptor ya hiciera ack del mensaje
  (el compromiso se conserva 180 días). Un mensaje sin franking que ya no existe sigue siendo 400 `invalid_evidence`.
- Nuevo (no rompe): el access token trae el claim `sid` (id de sesión = `SessionDto.id`). `PUT /devices/push-token` con un
  token de una sesión ya cerrada responde 401. Los push tokens se borran al hacer logout, al cerrar la sesión
  (`DELETE /me/sessions/{id}`; `DELETE /me/sessions` borra todos) y al suspender la cuenta: tras cerrar sesión hay que volver
  a registrar el token en el siguiente login.
- Cambio de comportamiento: `/openapi/v1.json` solo se sirve en Development/Testing (404 en producción).
- Sin cambios de forma: rotaciones concurrentes de llaves, `PATCH /me` e importaciones de Spotify en paralelo ahora se
  serializan por usuario (ya no hay 500 ni totales por encima del tope); un ban concurrente con un like no deja match activo.

## 2026-10-14 · v1.0.0-draft.4
- Nuevo (no rompe): límite por usuario (120/min por defecto) en swipes, likes, envío de mensajes, publicación de
  llaves, bloqueos, reportes y vinculación. Al excederlo: 429 `rate_limited` con cabecera `Retry-After`.

## 2026-10-14 · v1.0.0-draft.3
- Nuevo (v2, no rompe): respaldo cifrado `PUT/GET/DELETE /me/backup` y `GET /me/backup/info` (blob opaco cifrado en el
  dispositivo con passphrase; KDF argon2id|scrypt|pbkdf2-sha256; máx. 10 MiB).
- Nuevo (v2, no rompe): vinculación de dispositivo `POST/GET/PUT/DELETE /keys/link-requests[/{id}]` — buzón de un solo uso
  (10 min, máx. 3 abiertos) para pasar el material de llaves cifrado del dispositivo viejo al nuevo vía QR.
- Nuevo (no rompe): sesiones `GET /me/sessions`, `DELETE /me/sessions/{id}`, `DELETE /me/sessions` (cerrar en todos lados).
- Cambio de comportamiento: los `title` de errores de negocio llegan siempre tal cual (antes los títulos sin tildes se
  reemplazaban por uno genérico). Ramifica por `code`, no por `title`.

## 2026-10-14 · v1.0.0-draft.2
- Nuevo (v2, no rompe): panel de administración bajo `/api/v1/admin/*` (rol admin confirmado en BD en cada request):
  eventos (listar/crear/editar/borrar), canciones (listar/crear/editar; `isActive=false` la saca del feed; no se borra),
  búsqueda en iTunes Search (`/admin/itunes/search?term=`), reportes (listar por estado, resolver con dismiss|resolve|ban)
  y estado de cuentas (`PUT /admin/users/{id}/status`). Ban: cierra matches activos (MatchClosed) y revoca sesiones.

## 2026-10-14 · v1.0.0-draft.1
- Primer borrador completo del contrato v1 (+ piezas de v1.1/v2 ya previstas: llaves rotables, franking, push, Spotify, ack de entrega, exportación de datos, borrado web).
- Base `/api/v1`, JSON camelCase, enums como string camelCase, enteros estrictos (no se aceptan como string), binarios en base64.
- Errores `application/problem+json` con `code` estable, `traceId` y `errors` (claves camelCase) en validación.
- Handlers aún en implementación: pueden responder 500 hasta que su corte esté integrado.
