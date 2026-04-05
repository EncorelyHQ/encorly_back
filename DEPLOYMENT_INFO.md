# 🚀 Guía de Despliegue - Encorely Backend (Supabase)

Para desplegar el backend de Encorely utilizando la base de datos de Supabase, se deben configurar las siguientes variables de entorno en el servidor de producción (App Service, Heroku, Docker, etc.).

---

## 🏗️ Configuración de Base de Datos (Supabase)

Estas variables permiten que el API se conecte de forma segura a Supabase:

| Variable | Valor Sugerido | Descripción |
| :--- | :--- | :--- |
| `DB_HOST` | `db.clsjlwjqytgracjethon.supabase.co` | Host de la base de datos. |
| `DB_PORT` | `5432` | Puerto estándar de PostgreSQL. |
| `DB_NAME` | `postgres` | Nombre de la base de datos. |
| `DB_USER` | `postgres` | Usuario administrador. |
| `DB_PASSWORD` | `fihtos-fetsuf-Zotqy1` | Contraseña del proyecto. |

> [!IMPORTANT]
> El sistema está configurado para requerir **SSL** (`SSL Mode=Require`) y confiar en el certificado del servidor para facilitar la conexión con Supabase.

---

## 📡 Configuración de Mensajería (Kafka)

Si se despliega en un entorno con Kafka externo, actualizar:

| Variable | Valor |
| :--- | :--- |
| `KAFKA_HOST` | `localhost` (o IP del broker) |
| `KAFKA_PORT` | `9092` |

---

## 🎧 Integración con Spotify

| Variable | Descripción |
| :--- | :--- |
| `JWT_SECRET_KEY` | Una cadena secreta larga para firmar los tokens. |
| `GET_SPOTIFY_CLIENT_ID` | Client ID de la app en Spotify Dashboard. |
| `GET_SPOTIFY_CLIENT_SECRET` | Client Secret de la app en Spotify Dashboard. |

---

## 🚀 Pasos para el Despliegue

1. **Configurar Variables**: Cargar los valores anteriores en el entorno de ejecución.
2. **Aplicar Migraciones**: Ejecutar el siguiente comando desde la raíz (si el entorno tiene el SDK de .NET):
   ```bash
   dotnet ef database update --project EncorelyApi
   ```
3. **Iniciar**: Ejecutar `dotnet run --project EncorelyApi` o iniciar el contenedor Docker.

---
*Preparado por Antigravity AI para el equipo de Infraestructura.*
