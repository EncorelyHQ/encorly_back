# Cambios del contrato `openapi.v1.json`

El archivo se genera desde el código (`scripts/update-contract.sh`) y un test falla si queda desactualizado.
Cada cambio de endpoints o DTOs se anota aquí, el más reciente arriba. **Rompe** = el cliente debe ajustarse.

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
