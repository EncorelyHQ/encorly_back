# Guía de Verificación - Encorely Auth Flow

Sigue estos pasos para validar que la persistencia y la mensajería están funcionando correctamente.

## 1. Migraciones de Base de Datos
Para crear la tabla `users` en PostgreSQL, ejecuta los siguientes comandos desde la raíz del proyecto:

```bash
# Crear la migración inicial
dotnet ef migrations add InitialCreate --project EncorelyInfrastructure --startup-project EncorelyApi

# Aplicar la migración a la base de datos (Docker debe estar corriendo)
dotnet ef database update --project EncorelyApi
```

## 2. Verificación de PostgreSQL
Una vez aplicada la migración y realizada una petición de prueba:
- Conéctate al contenedor: `docker exec -it encorely-db psql -U admin -d encorely_db`
- Verifica la tabla: `\dt`
- Consulta los usuarios: `SELECT * FROM users;`
- Verifica el índice único: `\d users`

## 3. Verificación de Kafka (Control Center)
- Abre `http://localhost:9021` en tu navegador.
- Ve a **Clusters** > **encorely-kafka** > **Topics**.
- Busca el tópico `user-dna-sync`.
- En la pestaña **Messages**, deberías ver los eventos de sincronización con el `UserId` y el `SpotifyToken`.

## 4. Interpretación del 202 Accepted
El código **202 Accepted** indica que la petición fue recibida y validada, y que el proceso pesado (análisis de ADN musical) ha sido delegado a un sistema en segundo plano (vía Kafka). Es el estándar para arquitecturas Event-Driven altamente escalables.
