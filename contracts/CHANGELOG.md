# Cambios del contrato `openapi.v1.json`

El archivo se genera desde el código (`scripts/update-contract.sh`) y un test falla si queda desactualizado.
Cada cambio de endpoints o DTOs se anota aquí, el más reciente arriba. **Rompe** = el cliente debe ajustarse.

## 2026-10-15 · v1.0.0-draft.14 (FIN-17 a 29: moderación, radar, push y cabeceras)
- **Push (FIN-23) · rompe si el cliente manda otra cosa:** `PUT /devices/push-token` exige `ExponentPushToken[…]` o
  `ExpoPushToken[…]` → si no, 400 `validation_failed`. Si el token era de **otra cuenta** (mismo teléfono, otra sesión), pasa a la
  nueva y la sesión anterior en ese teléfono se **cierra** (su access token da 401).
- **Radar (FIN-22):** `affinity` es `null` también cuando alguno no tiene ningún género con peso positivo (antes salía `0`). El
  umbral `RADAR_MIN_AFFINITY` se aplica con ≥. Si hay más de 200 asistentes, responde con la cabecera `X-Radar-Truncated: true`
  (el cuerpo sigue siendo la lista). `attendeeCount` ya no cuenta cuentas suspendidas ni bloqueos en ninguna dirección.
- **Admin (FIN-17, FIN-21):** `POST /admin/reports/{id}/resolve` concurrente sobre el mismo reporte aplica una sola acción; la otra
  da 409 `report_already_resolved` (ahora declarado en el contrato). Suspender a otro admin (directo o por reporte) da 403
  `cannot_change_admin_status` y deja el reporte abierto.
- **Vinculación (FIN-18):** el tope de 3 vinculaciones abiertas se respeta también con peticiones simultáneas (el resto, 429).
- **Cabeceras (FIN-24):** toda respuesta bajo `/api` lleva `Cache-Control: no-store`.

## 2026-10-15 · v1.0.0-draft.13 (re-QA: errores, respaldo concurrente y reuso de ids del chat)
Sin cambios en `openapi.v1.json`; todo es comportamiento.
- **Errores (FIN-20):** los fallos de binding (tipo equivocado en el cuerpo, query o ruta) responden 400 `invalid_body` con
  `detail` = "Revisa el campo `<ruta>`: falta o no tiene el formato esperado." (p. ej. `kdf.opsLimit`, `limit`), sin repetir el valor.
  405, 409, 413 y 415 del framework llevan `code` (`method_not_allowed`, `conflict`, `payload_too_large`,
  `unsupported_media_type`) y título en español. El 413 ya no sale como "Content Too Large".
- **Query con valores fijos (FIN-01):** `GET /admin/reports?status=` y `DELETE /users/{id}/reactions?targetKind=` aceptan el
  valor sin distinguir mayúsculas (`Open` = `open`). Un valor fuera de la lista da 400 `validation_failed` con
  `errors.<campo>` = "Usa uno de estos valores: …".
- **Respaldo (FIN-16):** `PUT /me/backup` concurrente desde dos dispositivos ya no da 500 en el primer respaldo; gana el último.
- **Chat (FIN-04):** reusar el `id` de un mensaje **sin** `frankingTag` que el receptor ya confirmó responde 409
  `message_already_delivered` (mismo remitente y match) o `message_id_conflict` (otro), igual que con tag. Antes creaba un duplicado.

## 2026-10-14 · v1.0.0-draft.12 (cierre de FIN pendientes: moderación, cotas, chat, privacidad y listados)
- **Reportes (FIN-10):** un solo reporte abierto por (quien reporta, reportado). Repetir `POST /reports` contra la misma persona
  devuelve el **mismo `id`**, suma la evidencia nueva (sin repetir `messageId`, tope acumulado 60 → 400 `too_much_evidence`) y
  actualiza `reason`, `detail` (si viene) y `matchId` (si viene). Ya resuelto, el siguiente abre otro. `POST /reports` pasa a
  **10 por hora** por usuario (`RATE_LIMIT_REPORTS_PER_HOUR`) → 429. En el panel, `priorReportsAgainstUser` cuenta **personas**.
- **Retención de evidencia (FIN-11):** `AdminReportDto` gana `evidencePurgedAt` (aditivo). 180 días después de resolverse un
  reporte, o en cuanto se borra la cuenta reportada, se borran `detail` y el `plaintext` de la evidencia (queda `""`); se
  conservan `messageId` y `verified`. Un reporte abierto cuya cuenta reportada ya no existe se descarta solo. La política
  (`/legal/privacidad` §6 y §8, `/cuenta/eliminar`) documenta el plazo; **no** cambia `consentVersion` (reduce retención).
- **Cotas (FIN-08):** cuerpo máximo **256 KiB** en todos los endpoints → 413; `PUT /me/backup` admite 15 MB y `POST /reports` 1 MB.
  `POST /auth/refresh` y `/auth/logout` pasan a **60 por minuto por IP** (`RATE_LIMIT_TOKENS_PER_MINUTE`) → 429. Se suman al límite
  por usuario (429): `PUT /me/backup`, `PUT /keys/link-requests/{id}`, `PUT /events/{id}/attendance`, `PUT /devices/push-token`,
  `POST /me/spotify/import` y `GET /me/export`. Cada género de `topArtists[].genres` admite hasta 100 caracteres. KDF del respaldo:
  `memLimit` hasta 1 GiB, `opsLimit` hasta 2^32−1, y por algoritmo argon2id ≤ 20 y pbkdf2-sha256 ≤ 10 000 000 (si no, 400 `unsupported_kdf`).
- **Chat (FIN-12):** `POST /matches/{id}/messages` responde **429 `chat_quota_exceeded`** si el remitente ya tiene 50 MiB cifrados
  sin recoger en ese match (el ack del otro libera espacio; la cuota es por remitente). `GET …/messages` corta la página al llegar a
  1 MiB de ciphertext aunque no llegue a `limit` (siempre trae al menos uno y `nextCursor` sigue funcionando igual).
  `CHAT_MESSAGE_RETENTION_DAYS` pasa a **30** por defecto: un cliente sin base local (sin ack) pierde lo que tenga más de 30 días.
- **Spotify (FIN-15):** `POST /me/spotify/import` responde `SpotifyImportResponse` = los campos de `DnaResponse` **+ `unmappedGenres`**
  (aditivo): géneros recibidos que no aportaron al ADN, sin repetidos y en orden de llegada (máx. 50). Si todos caen ahí, el ADN no
  cambió y conviene avisarlo. El mapeo ahora reconoce `rock`, `colombian rock`, `alternative rock`, `corridos` y `corridos bélicos`, y
  `dance pop`/`pop rock` van a `pop-latino` (antes caían en electrónica o en nada).
- **Listados (FIN-13), aditivo:** `GET /matches?limit=&beforeAt=&beforeId=` pagina por keyset del más reciente al más viejo por
  (`lastMessageAt ?? createdAt`, `id`): para la página siguiente se pasan esos dos valores del último match recibido. `limit` 1–100;
  `beforeAt` y `beforeId` van juntos (si no, 400). **Sin `limit` responde como antes** (todos, con tope de 500). `GET /admin/songs`
  gana `offset` (0–100000) y el panel muestra "Cargar más canciones". Siguen sin paginar `/admin/events`, `/me/events`, la cola de
  `/admin/reports` (200) y `/me/export` (en memoria).

## 2026-10-14 · v1.0.0-draft.11 (decisiones del perfil social y consentimiento del perfil público)
Decisiones de Camilo sobre el draft.10, más la §6 del pedido (privacidad).
- **Consentimiento nuevo:** `GET /legal/current` pasa a `consentVersion: "2026-10-14"`. `/legal/privacidad` gana la finalidad
  "perfil público": qué se muestra (nombre, edad, ciudad, bio, géneros, artistas, estadísticas y el nombre junto a las reacciones),
  a quién (cualquier usuario) y cómo apagarlo. También suma a los datos los artistas de Spotify y las reacciones. **Todos** quedan
  con `consentUpToDate: false` hasta aceptar con `POST /me/consents`. Registro y login con Spotify ya exigen la versión vigente.
- **Interruptor:** `PATCH /me { showProfileToOthers: boolean }`. `/me` (y `profile` de `/me/export`) ganan `showProfileToOthers`
  (el interruptor, encendido por defecto) y `profileVisible` (si otros me ven ahora: interruptor encendido **y** la versión
  `2026-10-14` o posterior aceptada).
  - Sin `profileVisible`: `GET /users/{id}/profile` y `PUT /users/{id}/reactions` hacia esa persona dan **404 `user_not_found`**,
    igual que con bloqueo. El dueño siempre puede pedir su propio perfil.
  - Las reacciones se conservan, pero solo se muestran si el dueño **y** quien reaccionó tienen `profileVisible` (la reacción
    muestra el nombre de quien reaccionó).
  - Radar y matches no cambian.
- **Rompe (comportamiento):** `GET /users/{id}/profile` lo ve **cualquier usuario autenticado**, ya sin match ni evento en común.
  Siguen dando 404 el bloqueo en cualquier sentido, una cuenta suspendida o inexistente, y ahora también un perfil no visible.
  La contención es bloquear (`POST /blocks`) y reportar (`POST /reports`, `reason: "harassment"`): los dos funcionan sin relación previa.
- **Artistas:** primero el top de Spotify y después los de las canciones con "Me gusta" (sin imagen), con tope de 12.
- **Nuevo: quién reaccionó.** `GET /me/reactions/received?before=&limit=` (`limit` 1–50, 20 por defecto) →
  `{ items: [{ from: { userId, displayName }, targetKind, targetKey, targetName, reaction, reactedAt }], nextCursor }`, de la
  más reciente a la más vieja. `nextCursor` es opaco: se pasa como `?before`. Un cursor inválido da 400. `targetName` es el nombre
  al reaccionar, así que la reacción se sigue viendo aunque ese objetivo ya no esté en el perfil.
- **Cambio:** `reactionsReceived` de `/me` sigue siendo el resumen agregado, pero ya no descarta los objetivos que salieron del
  perfil (usa el nombre guardado).
- **Umbral de afinidad:** `AFFINITY_MIN_SWIPES` por defecto **25** (o Spotify importado).

## 2026-10-14 · v1.0.0-draft.10 (perfil social)
Pedido del front en `reportes/10-pedidos-back-perfil-social.md`. Enums en camelCase como el resto del contrato: `meToo`,
`letsGo`, `sendOne` (no `me_too`).
- **Rompe (tipo):** `RadarEntryDto.affinity` pasa a `int | null`. Es `null` si quien mira o la otra persona no tiene señal propia:
  menos de `AFFINITY_MIN_SWIPES` swipes (por defecto 40) y sin Spotify importado. El radar ordena primero las que tienen
  afinidad, de mayor a menor, y luego las `null` por nombre. `RADAR_MIN_AFFINITY` no descarta las `null`.
- Nuevo (no rompe): `SpotifyArtist` gana `imageUrl?` (`images[0].url` de Spotify). Solo se guarda si es `https://i.scdn.co/…`;
  cualquier otra URL se ignora sin error. `POST /me/spotify/import` ahora **guarda** el top de artistas (antes solo los convertía en
  géneros); cada importación reemplaza la lista. Cada artista se identifica por `key`, el nombre normalizado (`"Bad Bunny"` →
  `"bad-bunny"`); los repetidos conservan el mejor puesto.
- Nuevo: `GET /users/{id}/profile` → `PublicProfileDto` `{ id, displayName, age, city, bio, genres: [{ slug, name }] (máx. 8),
  artists: [{ key, name, imageUrl, shared }] (máx. 12), stats: { swipeCount, genresExplored, eventsMarked },
  common: { affinity: int|null, sharedGenres, sharedArtists }, myReactions: [{ targetKind, targetKey, reaction }] }`.
  Se ve con un match activo o un evento próximo en común (el corte del radar). En cualquier otro caso, o con bloqueo en
  cualquier sentido o cuenta suspendida, **404 `user_not_found`** (nunca 403). El propio perfil también se puede pedir.
- Nuevo: `PUT /users/{id}/reactions` `{ targetKind: "artist"|"genre", targetKey, reaction: "meToo"|"letsGo"|"sendOne" }` → **204**.
  Una reacción por objetivo y por par; repetir reemplaza el tipo. `targetKey` es el `slug` del género o la `key` del artista y
  debe estar en ese perfil ahora: si no, **400 `reaction_target_not_found`**. A uno mismo → **400 `cannot_react_self`**. Sin
  relación o con bloqueo → **404 `user_not_found`**. Rate limit por usuario (429).
- Nuevo: `DELETE /users/{id}/reactions?targetKind=&targetKey=` → **204** siempre (también si no había nada); `targetKind` fuera de
  `artist|genre` → 400.
- Nuevo (no rompe): `GET /me` (y `PATCH /me`, y `profile` de `/me/export`) ganan `artists` (con `shared: false`), `stats` y
  `reactionsReceived: [{ targetKind, targetKey, targetName, reaction, count }]`. Esta última viene agregada y sin identidades, y
  solo cuenta las reacciones de cuentas activas y sin bloqueo, sobre objetivos que siguen en el perfil.
- Nuevo (no rompe): `/me/export` gana `reactions` (las que dejé: `peerDisplayName`, `targetKind`, `targetKey`, `reaction`, `updatedAt`).
- Comportamiento: las reacciones no cuentan para el match ni mandan push. Bloquear borra las del par y suspender una cuenta
  borra las suyas, dadas y recibidas.
- Sin decidir, y aislado en `ProfileService` para cambiarlo con una línea: quién puede ver un perfil (`CanViewAsync`) y si los
  artistas incluyen los de canciones con "Me gusta" (`IncludeLikedSongArtists`, hoy `false`: solo Spotify).

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
