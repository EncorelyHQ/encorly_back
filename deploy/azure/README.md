# Despliegue en Azure for Students

Producción: `https://encorely-api.northcentralus.cloudapp.azure.com`. Topología, red y desvíos frente a la
propuesta con Application Gateway: manual técnico, 3.2.3, 3.4.1 y 5.5.4.

| Archivo | Dónde va | Qué hace |
|---|---|---|
| `compose.yml` | `/opt/encorely/` en `vm-encorely-api` | API + Caddy; lee `API_TAG` y `API_DOMAIN` de `/opt/encorely/.env` y los secretos de `/etc/encorely/api.env` |
| `Caddyfile` | `/opt/encorely/` en `vm-encorely-api` | TLS con Let's Encrypt y proxy a `api:8080` (incluye WebSocket de `/hubs/chat`) |
| `setup-db.sh` | Se ejecuta una vez en `vm-encorely-db` | Disco de datos en `/var/lib/postgresql`, swap, PostgreSQL 16 solo en la red privada |

Acceso por SSH con alias en `~/.ssh/config`: `encorely-api` (IP pública) y `encorely-db` (`ProxyJump encorely-api`).

## Publicar una versión nueva

```bash
TAG=$(git rev-parse --short HEAD)
docker build -t encorely-api:$TAG .
docker save encorely-api:$TAG | gzip -1 | ssh encorely-api 'gunzip | docker load'
ssh encorely-api "sudo sed -i 's/^API_TAG=.*/API_TAG=$TAG/' /opt/encorely/.env && cd /opt/encorely && sudo docker compose up -d"
curl -fsS https://encorely-api.northcentralus.cloudapp.azure.com/health/ready
```

Las migraciones corren al arrancar (`DB_MIGRATE_ON_STARTUP=true`, una sola réplica). Para volver atrás se pone la
etiqueta anterior en `API_TAG` y se repite `docker compose up -d`.
