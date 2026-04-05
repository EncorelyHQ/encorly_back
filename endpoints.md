# 📡 Documentación de Endpoints - Encorely Backend

Este archivo detalla los endpoints principales. Para una experiencia interactiva, pruebas en vivo y esquemas detallados de cada modelo, visita la documentación oficial de Swagger:

> [!TIP]
> **Documentación Interactiva (Swagger UI)**:  
> URL: `https://[TU-DOMINIO]/swagger` o `http://localhost:5000/swagger`
> 
> Aquí puedes probar los endpoints directamente desde el navegador y ver los tipos de datos exactos de cada DTO.

---

## 🔐 Autenticación (`/api/v1/Auth`)

### 1. Login con Spotify
- **Método**: `POST`
- **URL**: `/api/v1/Auth/spotify`
- **Body**:
  ```json
  {
    "token": "string (Access Token de Spotify)"
  }
  ```
- **Respuesta**: `202 Accepted` + Perfil de usuario/Token JWT.

### 2. Login con Google
- **Método**: `POST`
- **URL**: `/api/v1/Auth/google`
- **Body**:
  ```json
  {
    "token": "string (Google Identity Token)"
  }
  ```
- **Respuesta**: `202 Accepted`.

### 3. Registro con Email
- **Método**: `POST`
- **URL**: `/api/v1/Auth/register`
- **Body**:
  ```json
  {
    "email": "string",
    "password": "string"
  }
  ```
- **Respuesta**: `202 Accepted`.

---

## 👤 Usuario (`/api/v1/User`)

### 1. Obtener mi perfil
- **Método**: `GET`
- **URL**: `/api/v1/User/me?userId={guid}`
- **Respuesta**: `200 OK`
  ```json
  {
    "id": "guid",
    "displayName": "string",
    "email": "string",
    "provider": "string (Spotify|Google|Custom)",
    "swipeCount": "int",
    "mood": "string (Moshpit|Chill|VIP)"
  }
  ```

### 2. Actualizar configuración
- **Método**: `PUT`
- **URL**: `/api/v1/User/settings`
- **Body**:
  ```json
  {
    "userId": "guid",
    "mood": "int (0: Moshpit, 1: Chill, 2: VIP)"
  }
  ```
- **Respuesta**: `204 No Content`.

---

## 🎵 Swipes e Interacción (`/api/v1/Swipes`)

### 1. Obtener siguiente track
- **Método**: `GET`
- **URL**: `/api/v1/Swipes/next-track?userId={guid}`
- **Respuesta**: `200 OK` (Objeto Track con ID, nombre, artista).

### 2. Registrar interacción (Swipe)
- **Método**: `POST`
- **URL**: `/api/v1/Swipes/interactions/swipe`
- **Body**:
  ```json
  {
    "userId": "guid",
    "trackId": "string (Spotify ID)",
    "direction": "int (0: Dislike, 1: Like, 2: Superlike)"
  }
  ```
- **Respuesta**: `202 Accepted`.

---

## 🎸 Eventos y Radar (`/api/v1/Events`)

### 1. Feed de eventos cercanos
- **Método**: `GET`
- **URL**: `/api/v1/Events/feed`
- **Respuesta**: `200 OK` (Lista de eventos con ID, nombre, fecha y ubicación).

### 2. Radar de compatibilidad (Matches por evento)
- **Método**: `GET`
- **URL**: `/api/v1/Events/{eventId}/matches?userId={guid}&targetMood={int?}`
- **Restricción**: Requiere un `swipeCount >= 25`.
- **Respuesta**: `200 OK`
  ```json
  [
    {
      "id": "guid",
      "displayName": "string",
      "affinity": "double (0-100)",
      "isHighPriority": "bool",
      "mood": "string"
    }
  ]
  ```

---

## 💬 Chat y Matches (`/api/v1/Chat` / `api/v1/Matches`)

### 1. Obtener mensajes de un match
- **Método**: `GET`
- **URL**: `/api/v1/Chat/{roomId}/messages?userId={guid}`
- **Respuesta**: `200 OK` (Lista de mensajes con senderId, content y timestamp).

### 2. Enviar mensaje a un match
- **Método**: `POST`
- **URL**: `/api/v1/Chat/{matchId}/messages?userId={guid}`
- **Body**: (Solo el string del contenido en el cuerpo de la petición).
- **Respuesta**: `{ "id": "guid", "content": "string", "timestamp": "datetime" }`.

### 3. Aceptar un match
- **Método**: `POST`
- **URL**: `/api/v1/Matches/{matchId}/accept?userId={guid}`
- **Respuesta**: `{ "roomId": "guid" }`.

---

## 🏟️ Venue Rooms (Salas Grupales) (`/api/v1/Venue`)

### 1. Crear sala de venue
- **Método**: `POST`
- **URL**: `/api/v1/Venue/{eventId}/rooms?name={string}&durationHours={int}`
- **Respuesta**: `{ "id": "guid", "name": "string", "expiresAt": "datetime", "eventId": "string" }`.

### 2. Publicar mensaje en sala
- **Método**: `POST`
- **URL**: `/api/v1/Venue/{roomId}/messages?userId={guid}`
- **Body**: `string (contenido)`
- **Respuesta**: `{ "id": "guid", "content": "string", "isModerated": "bool", "timestamp": "datetime" }`.

---

## 🧬 DNA Playlist (`/api/v1/Playlist`)

### 1. Generar DNA Mix (Blended Playlist)
- **Método**: `POST`
- **URL**: `/api/v1/Playlist/dna-mix?userId1={guid}&userId2={guid}&accessToken1={string}&accessToken2={string}`
- **Respuesta**: `200 OK`
  ```json
  {
    "spotifyPlaylistId": "string",
    "name": "string",
    "description": "string",
    "totalTracks": "int",
    "externalUrl": "string (URL de Spotify)",
    "syncSuccess": "bool"
  }
  ```

---

## 📡 SignalR Hubs (WebSockets)

### 1. Hub de Notificaciones (`/notificationHub`)
- **Método**: `JoinUserGroup(userId: string)`
- **Eventos recibidos**:
  - `MatchFound`: `{ "matchId": "guid", "affinity": "double" }`
  - `DnaCompleted`: `{ "userId": "guid", "message": "string" }`

### 2. Hub de Venue (`/venueHub`)
- **Métodos**: `JoinVenueRoom(roomId)`, `LeaveVenueRoom(roomId)`, `SendVenueMessage(roomId, userId, message)`
- **Eventos recibidos**:
  - `ReceiveVenueMessage`: `(userId, message, timestamp)`
  - `UserJoined/UserLeft`: `(connectionId)`

---
*Documentación generada automáticamente por Antigravity AI.*
