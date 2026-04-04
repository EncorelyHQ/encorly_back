# 🎸 Encorely - Event-Driven Backend

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512bd4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2B%20EDA-blue)](#-arquitectura)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

**Encorely** es el motor backend de alto rendimiento diseñado para conectar a fans de música en vivo. Utiliza un enfoque basado en eventos (**Event-Driven Architecture**) y **Clean Architecture** para procesar el ADN musical de los usuarios y generar conexiones (matches) basadas en afinidad real.

---

## 🏗️ Arquitectura y Diseño

El sistema está construido bajo los principios de **Clean Architecture**, asegurando que el núcleo de negocio sea independiente de las tecnologías externas:

### Capas del Proyecto
- 📂 **EncorelyDomain**: El corazón del sistema. Contiene entidades (`User`, `Swipe`, `MusicalProfile`), eventos de dominio y enums.
- 📂 **EncorelyApplication**: Orquestación de la lógica de negocio. Define interfaces, servicios (`CompatibilityService`, `MatchService`) y DTOs.
- 📂 **EncorelyInfrastructure**: Implementaciones técnicas. Manejo de **PostgreSQL** vía **EF Core**, integración con **Apache Kafka** y clientes externos.
- 📂 **EncorelyApi**: Capa de presentación. Controladores RESTful, Hubs de **SignalR** para notificaciones en tiempo real y configuración de Inyección de Dependencias.
- 📂 **EncorelyWorker**: Servicio en segundo plano que consume eventos de **Kafka**, procesando el ADN musical de forma asíncrona.

---

## 🚀 Stack Tecnológico

| Componente | Tecnología |
| :--- | :--- |
| **Framework** | .NET 9.0 (ASP.NET Core) |
| **Ingesta de Eventos** | Apache Kafka (KRaft) |
| **Persistencia** | PostgreSQL 16 |
| **Comunicación Dual** | REST API + SignalR (Real-time) |
| **Contenedores** | Docker & Docker Compose |
| **ORM** | Entity Framework Core |

---

## 🎸 Core Engine: Musical DNA & Affinity

### 1. Sound-Swipe & Processing
Cada interacción del usuario emite un evento asíncrono a **Kafka**. El `EncorelyWorker` analiza estos eventos para refinar el `MusicalProfile` (Energy, Danceability, Valence).

### 2. El Umbral de los 25 Swipes
Para garantizar conexiones de calidad, el sistema impone un **Threshold** de **25 swipes** antes de habilitar funciones sociales como el Radar o el Chat. Esto asegura que el ADN musical sea estadísticamente relevante.

### 3. Motor de Afinidad (Euclidean Algorithm)
Utilizamos un algoritmo de **Distancia Euclidiana** en un espacio tridimensional de atributos musicales. Solo los usuarios con una compatibilidad **superior al 70%** son visibles en el Radar.

---

## 📡 Referencia de API Principal

| Método | Endpoint | Descripción |
| :--- | :--- | :--- |
| `POST` | `/api/v1/auth/register` | Registro de usuario (Email/Pass/Google/Spotify). |
| `GET` | `/api/v1/user/me` | Información del perfil y contador de swipes. |
| `POST` | `/api/v1/swipes/interactions/swipe` | Registro de swipe (Desencadena evento Kafka). |
| `GET` | `/api/v1/events/{id}/matches` | Radar de compatibilidad (Requiere 25 swipes). |
| `POST` | `/api/v1/matches/{id}/accept` | Confirmar match y abrir sala de chat. |

---

## ⚙️ Configuración del Entorno Local

### Requisitos
- Docker Desktop
- .NET 9 SDK

### Paso a Paso
1. **Infraestructura**: Levanta la DB y Kafka:
   ```bash
   docker-compose up -d
   ```
2. **Variables**: Crea un archivo `.env` basado en `.env.example`.
3. **Persistencia**: Aplica las migraciones:
   ```bash
   dotnet ef database update --project EncorelyApi
   ```
4. **Ejecución**: Lanza el API y el Worker:
   ```bash
   dotnet run --project EncorelyApi
   ```

---

## 📑 Estándares y Mejores Prácticas
- **Identidad**: Autenticación Multi-Provider integrada.
- **Seguridad**: Protección de secretos vía variables de entorno.
- **Escalabilidad**: Desacoplamiento total entre la ingesta de swipes y el cálculo de afinidad.
- **Logs**: Trazabilidad de hitos críticos (`[UMBRAL]`, `[DNA_COMPLETED]`).

---
*Desarrollado con ❤️ por el equipo de Senior Solutions Architects.*
