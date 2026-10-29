# Pedidos del front al back: perfil social (propuesta para draft.10)

2026-10-14 · Origen: plan de rediseño del perfil (prototipo en `09-prototipo-perfil-stickers.html`).
Nada de esto está implementado en el front todavía. Los nombres de campos son propuesta: el back decide la forma final y la anota en `contracts/CHANGELOG.md`.

## Decidido por Camilo

### 1. Afinidad solo con señal suficiente
- `RadarEntryDto.affinity` pasa a `int | null`. Es `null` cuando alguna de las dos personas no tiene señal suficiente.
- Propuesta de umbral: 40 swipes **o** Spotify importado (configurable, como `RadarMinSwipes`). Camilo no ha confirmado el 40.
- Motivo: con el bono de 2.0 por género declarado y el radar abierto a los 15 swipes, el coseno lo deciden los géneros del onboarding. Dos personas con los mismos 3 géneros declarados dan 100 % sin haber calificado nada en común.
- El front deja de decir "afinidad": muestra "80 % de gustos en común", redondeado a múltiplos de 5, y siempre con los géneros o artistas en común al lado. El redondeo lo hace el front.

### 2. Reacciones a artistas y géneros de otro perfil (tres tipos)
- Tipos: `me_too` ("Yo también"), `lets_go` ("Vamos a verlo"), `send_one` ("Pásame una").
- Una reacción por objetivo y por par de personas. Reaccionar de nuevo reemplaza el tipo.
- `PUT /users/{id}/reactions` con body `{ targetKind: "artist" | "genre", targetKey, reaction }` → 204, idempotente.
- `DELETE /users/{id}/reactions?targetKind=&targetKey=` → 204, también si no había nada.
- Errores propuestos: 400 si el objetivo no está en el perfil de esa persona o si es uno mismo; 404 si no hay relación que permita ver el perfil o si hay bloqueo; 429 con el rate limit por usuario existente.

## Decisiones de Camilo (2026-10-14, después del draft.10)
El draft.10 salió con los valores por defecto. Estas decisiones lo cambian:
- **Visibilidad:** cualquier usuario autenticado ve el perfil de otro. 404 si hay bloqueo, si la cuenta está suspendida o si no existe. Se eligió sabiendo que expone más; la restricción se hace bloqueando y reportando.
- **Artistas:** de Spotify **y** de las canciones con "Me gusta". Tope de 12, primero los de Spotify.
- **Reacciones:** quien las recibe ve quién reaccionó (nombre). No cuentan para el match y no mandan push. Bloquear oculta las reacciones de esa persona y le impide dejar nuevas. Bloquear y reportar tienen que funcionar sin match ni evento en común.
- **Umbral de afinidad:** 25 canciones o Spotify importado. El front aclara que con más canciones el porcentaje se afina.

## Lo que el back necesita para soportarlo

### 3. Guardar los artistas (hoy se descartan)
- `ImportSpotifyAsync` solo convierte los artistas en pesos de géneros (`MusicService.cs:100`). Para mostrarlos y reaccionarles hay que guardarlos: nombre, rank, géneros, URL de la imagen e `importedAt`.
- `SpotifyArtist` gana `imageUrl?: string` (opcional, no rompe). El front lo manda desde `/v1/me/top/artists` (`images[0].url`).
- Hay que validar que la URL sea https de `i.scdn.co` para no guardar URLs arbitrarias.

### 4. `GET /users/{id}/profile` (perfil público)
DTO propuesto:
```
PublicProfileDto {
  id, displayName, age|null, city|null, bio|null,
  genres: [{ slug, name }]            // top N por peso
  artists: [{ key, name, imageUrl|null, shared: bool }]
  stats: { swipeCount, genresExplored, eventsMarked }
  common: { affinity: int|null, sharedGenres: [slug], sharedArtists: [key] }
  myReactions: [{ targetKind, targetKey, reaction }]
}
```
- 404 si no hay relación permitida (ver la decisión pendiente) o si hay bloqueo en cualquier sentido. Nunca 403: no se revela que la cuenta existe.
- `eventsMarked` = eventos con asistencia marcada. El front lo presenta como "conciertos marcados", no "asistidos".

### 5. `/me` gana
- `artists` (misma forma que en el perfil público).
- `stats` (misma forma).
- `reactionsReceived: [{ targetKind, targetKey, targetName, reaction, count }]`, agregado y sin identidades.

### 6. Privacidad (Ley 1581)
Mostrar tus artistas, géneros y estadísticas a otras personas es una finalidad nueva del tratamiento. Probablemente exige:
- Una nueva versión del consentimiento (`/legal/current`).
- Un interruptor por usuario, por ejemplo `PATCH /me { showProfileToOthers }`.

Revisar con el texto de privacidad vigente antes de cerrar el contrato.

**Aprobado por Camilo el 2026-10-14 y encargado al back.** Alcance:
- **Versión nueva del consentimiento** que describe qué se muestra, a quién y cómo apagarlo. Todos quedan con `consentUpToDate=false` y el front pide aceptarla de nuevo.
- **Interruptor `showProfileToOthers`.** Apagado: el perfil y las reacciones hacia esa persona responden 404 a los demás, y las reacciones que ya recibió se ocultan. El radar y los matches no cambian.
- **Por defecto:** el perfil no es visible mientras la persona no acepte la versión nueva. Al aceptarla, el interruptor arranca encendido.
