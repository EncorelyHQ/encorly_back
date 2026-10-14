# Cambios del contrato `openapi.v1.json`

El archivo se genera desde el código (`scripts/update-contract.sh`) y un test falla si queda desactualizado.
Cada cambio de endpoints o DTOs se anota aquí, el más reciente arriba. **Rompe** = el cliente debe ajustarse.

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
