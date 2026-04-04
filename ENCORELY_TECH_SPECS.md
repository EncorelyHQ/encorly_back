# Encorely Backend - Especificaciones Técnicas

Backend de alto rendimiento para **Encorely**, la plataforma de conexión social para fans de música en vivo, basado en **.NET 9** y **Clean Architecture**.

## 🏗️ Arquitectura del Sistema (Clean Architecture)

El proyecto sigue los principios de desacoplamiento total entre capas:

1.  **EncorelyDomain**: Entidades núcleo (`User`, `Swipe`, `MusicalProfile`), Enums y Eventos de dominio.
2.  **EncorelyApplication**: Lógica de negocio, DTOs, interfaces de servicios y servicios de aplicación (`IdentityService`, `SwipeService`, `MatchService`, etc.).
3.  **EncorelyInfrastructure**: Implementaciones de persistencia (`DbContext`), mensajería (Kafka Producers) y clientes externos (Spotify API).
4.  **EncorelyApi**: Capa de presentación (Controllers), configuración de inyección de dependencias, documentación Swagger y orquestación de middleware.

### Comunicación Orientada a Eventos (EDA)
Utiliza **Apache Kafka** para desacoplar la ingesta de datos del procesamiento pesado (Análisis de ADN Musical).
- **Topics**: `user-dna-sync`, `swipe-raw-events`, `match-notifications`.

---

## 🔐 Identidad y Autenticación (Multi-Provider)

El sistema soporta tres métodos de ingreso, gestionados de forma unificada en `IIdentityService`:
- **Spotify**: Sincronización transparente de ADN musical.
- **Google**: Registro rápido con validación de umbral para chat.
- **Custom (Email/Pass)**: Registro tradicional para usuarios sin redes sociales.

---

## 🎸 Módulos Principales y Reglas de Negocio

### 1. Sound-Swipe (Descubrimiento)
- **Puente Musical**: Si el usuario no tiene Spotify, el sistema usa *Client Credentials Flow* para proveer `preview_url` de canciones populares.
- **Eventos**: Cada swipe emite un `SwipeRegisteredEvent` asíncrono.

### 2. The Venue (Eventos y Radar)
- **Umbral de acceso (Threshold)**: Un usuario requiere **mínimo 25 swipes** para acceder a la lista de personas compatibles (matches) en un concierto.
- **Log de Umbral**: El sistema emite un log `[UMBRAL]` al alcanzar el acceso concedido.

### 4. Motor de Afinidad (Affinity Engine)
- **Algoritmo**: Utiliza similitud musical basada en Energy, Danceability y Valence.
- **Cálculo**: Los matches solo son visibles si la afinidad es **> 70%**.
- **Refinamiento**: Los resultados del Radar filtran automáticamente por evento compartido y puntuación de compatibilidad.

### 5. Procesamiento Asíncrono (EncorelyWorker)
- **Kafka Worker**: Consume `swipe-raw-events` para actualizar perfiles musicales en tiempo real.
- **DNA Ready**: Al llegar al swipe 25, emite un log/evento `[DNA_COMPLETED]`, habilitando el acceso total al sistema.

### 6. Notificaciones Real-Time (SignalR)
- **Hub**: `NotificationHub` en `/notificationHub`.
- **Match Alerts**: Notifica bidireccionalmente (`NotifyMatchFound`) cuando un usuario acepta un match mutuo.

---

## 📡 Referencia de API (Endpoints)

| Módulo | Endpoint | Método | Descripción |
| :--- | :--- | :--- | :--- |
| **Auth** | `/api/v1/auth/register` | POST | Registro tradicional Email/Password. |
|  | `/api/v1/auth/login` | POST | Login tradicional. |
|  | `/api/v1/auth/spotify` | POST | Auth via Spotify API (202 Accepted). |
|  | `/api/v1/auth/google` | POST | Auth via Google ID Token. |
| **User** | `/api/v1/user/me` | GET | Perfil actual y contador de swipes. |
|  | `/api/v1/user/settings` | PUT | Cambiar 'Concert Mood' (Moshpit, Chill, VIP). |
| **Swipes** | `/api/v1/swipes/next-track` | GET | Obtener siguiente canción (Simulado/Spotify). |
|  | `/api/v1/swipes/interactions/swipe` | POST | Registrar interacción (Asíncrono). |
| **Events** | `/api/v1/events/feed` | GET | Cartelera de conciertos cercanos. |
|  | `/api/v1/events/{id}/matches` | GET | Lista de matches (Requiere 25 swipes). |
| **Matches**| `/api/v1/matches/pending` | GET | Likes recibidos pendientes de aceptar. |
|  | `/api/v1/matches/{id}/accept` | POST | Aceptar match y generar `roomId`. |
| **Chat** | `/api/v1/chat/{roomId}/messages`| GET | Historial de mensajes (Validación Google DNA).|

---

## ⚙️ Configuración y Entorno

### Persistencia
- **Local Dev**: In-Memory persistence (`static List<User>`) para pruebas rápidas sin DB física.
- **Producción/Docker**: PostgreSQL 16 vía Npgsql.

### Docker Infra
- Archivo: `docker-compose.yml`
- Servicios: `PostgreSQL`, `Kafka (KRaft)`, `Confluent Control Center` (Puerto 9021).
- Variables: Gestionadas en `.env` raíz (DB\_PASS, KAFKA\_PORT, etc.).

---

## 🧪 Herramientas de Verificación

1.  **FullFlowTest.http**: Suite completa de pruebas secuenciales para el flujo Register -> Swipe -> Radar -> Chat.
2.  **verification_guide.md**: Guía paso a paso para validación de datos en Kafka y Postgres.
3.  **Swagger**: Documentación interactiva en `/swagger/index.html`.

---
*Senior Backend Developer - Encorely Social Team*
