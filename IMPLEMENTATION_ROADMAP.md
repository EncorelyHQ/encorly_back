# Roadmap de Implementación: Backend Encorely

Este documento detalla los pasos técnicos necesarios para transformar la arquitectura actual en una plataforma de producción completa, incluyendo las funcionalidades pendientes y las recomendaciones estratégicas.

---

## 🏁 Fase 1: Estabilización y Producción (Prioridad Alta)

El objetivo es pasar de la persistencia en memoria a una base de datos real y asegurar la identidad de los usuarios.

### 1.1 Persistencia de Datos Real
- **EF Core Migrations**: Generar la migración inicial (`Add-Migration InitialCreate`) y aplicarla a PostgreSQL.
- **Repositorios**: Refactorizar `IdentityService` y `MatchService` para eliminar las listas estáticas (`static List<User>`) y usar `EncorelyDbContext`.
- **Caché (Redis)**: Implementar Redis para almacenar los perfiles musicales temporales y reducir la carga en Postgres durante el Sound-Swipe.

### 1.2 Seguridad y JWT
- **Identity Server / Custom JWT**: Implementar la generación de tokens JWT reales con `Expiration`, `Issuer` y `Audience` validados.
- **Refresh Tokens**: Añadir lógica de renovación de sesión para evitar cierres inesperados en la app móvil.
- **Middleware de Autorización**: Asegurar que todos los endpoints (excepto Auth) requieran el header `Authorization: Bearer <token>`.

### 1.3 Integración Spotify Pro
- **Token Management**: Implementar un servicio que gestione el refresco automático de los `access_tokens` de los usuarios vinculados.
- **Spotify Client**: Reemplazar los mocks de canciones por llamadas reales a `/v1/me/top/tracks` y `/v1/recommendations`.
- **Evolución del Algoritmo**: Migrar de Distancia Euclidiana simple a un modelo de **Coseno de Similitud** para mayor precisión en vectores de alta dimensionalidad (incluyendo géneros y artistas).

---

## 🚀 Fase 2: Experiencia de Usuario y Escalabilidad (Prioridad Media)

Enfoque en la interactividad y la retención del usuario.

### 2.1 Notificaciones Push
- **Firebase Cloud Messaging (FCM)**: Integrar un servicio que envíe notificaciones push cuando el worker detecte un `MatchRegisteredEvent`.
- **SignalR Groups**: Refinar el `NotificationHub` para que solo envíe mensajes a las conexiones activas del usuario, manejando múltiples dispositivos.

### 2.2 Filtros Avanzados y Moods
- **Query Refinement**: Actualizar el `EventsController` para que el Radar permita filtrar por `ConcertMood` (ej: buscar solo gente que quiera ir a Moshpit).
- **Match Priority**: Priorizar en el radar a usuarios con mayor afinidad musical acumulada (> 85%).

### 2.3 Ticketing & Analytics
- **Partner Integration**: Añadir campos de `PurchaseUrl` en los eventos de la cartelera (Mock o API externa).
- **Match Analytics**: Tracking de cuántos matches terminan en apertura de chat para optimizar el algoritmo de afinidad.

---

## 🌐 Fase 3: Comunidad y Social Avanzado (Prioridad Futura)

Funcionalidades disruptivas para diferenciar a Encorely.

### 3.1 Social Hubs (Chat de Recinto)
- **Venue Rooms**: Crear salas de chat grupales temporales que se activen 2 horas antes de un concierto para todos los usuarios que hayan hecho "Swipe Right" al evento.
- **Modo Moderador**: Lógica para reportar mensajes y usuarios en chats grupales.

### 3.2 Generación de Playlist de Match
- **Shared DNA Playlist**: Al concretarse un match, llamar a la API de Spotify para crear una playlist privada en las cuentas de ambos usuarios con 20 canciones que ambos disfrutan.

### 3.3 CI/CD y Cloud Hosting
- **Docker Production**: Optimizar las imágenes de Docker para producción (multi-stage builds).
- **Kubernetes / ECS**: Desplegar el API y el Worker en un orquestador de contenedores.
- **Terraform/IAC**: Definir la infraestructura de Postgres and Kafka en AWS/Azure como código.

---
## 📊 Resumen de Esfuerzo Estimado

| Fase | Duración Estimada | Complejidad |
| :--- | :--- | :--- |
| **Fase 1** | 2-3 semanas | Media |
| **Fase 2** | 3-4 semanas | Alta |
| **Fase 3** | 4-6 semanas | Muy Alta |

---
*Roadmap diseñado por Antigravity AI - Solutions Architect Team*
