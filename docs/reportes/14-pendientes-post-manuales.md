# Pendientes tras los manuales técnicos (2026-10-20)

Salen de la §14 de `encorly_back/docs/MANUAL-TECNICO.md` (PR #13, `aaa3dfc`) y de `encorely-front/docs/MANUAL-TECNICO.md`
(PR #20, `57eb404`). Todos están verificados contra el código de `dev` / `Dev` en esa fecha. Ninguno está corregido.

## 1. Importante

| # | Repo | Problema | Evidencia | Fix propuesto |
|---|---|---|---|---|
| I1 | front | La CI no corre en `Dev`: los PR a `Dev` se mergean sin typecheck, lint, tests ni bundles | `.github/workflows/frontend.yml`: `branches: [main, rebuild/v1]` en `push` y `pull_request` | Cambiar a `branches: [main, Dev]`. Hacerlo en el mismo PR que I2, porque si no el primer PR sale en rojo |
| I2 | front | `pnpm check:expo` falla: hay 8 parches pendientes de SDK 57 (`expo` 57.0.28 → ~57.0.30, `expo-router`, `expo-notifications`, `expo-auth-session`, `expo-constants`, `expo-linking`, `expo-asset`, `@expo/metro-runtime`) | Corrida del 2026-10-20, "Found outdated dependencies" | Con Metro parado: `pnpm exec expo install --fix`, luego `pnpm verify` y `pnpm build:bundles`. Probar en el emulador el push y el login con Spotify (`expo-auth-session`) |
| I3 | back | `/swagger` responde 401 en Development, aunque el README lo da como Swagger UI | `UseSwaggerUI` (`Program.cs:202`) queda después de `UseAuthorization` (`:191`), y la política de fallback (`AuthSetup.cs:55`) exige token también en rutas sin endpoint | Mover el bloque `if (app.Environment.IsDevelopment()) app.UseSwaggerUI(...)` antes de `app.UseAuthentication()`. Agregar un test: `GET /swagger/index.html` → 200 en Development y 404 en Production |

## 2. Menor

| # | Repo | Problema | Fix |
|---|---|---|---|
| M1 | front | `vercel.json` llama a `pnpm run build:web`, que no existe, y la web no es objetivo (D-alcance) | Borrar `vercel.json` (o desconectar el proyecto de Vercel si existe) |
| M2 | back | `docker-compose.yml` fija `container_name: encorely-postgres`, así que no se pueden levantar dos proyectos de compose (QA, otra sesión) | Quitar `container_name` (compose nombra `<proyecto>-postgres-1`) y ajustar la línea `docker exec encorely-postgres` del manual técnico §6.1 |
| M3 | front | `.env.example` no trae `EXPO_PUBLIC_EAS_PROJECT_ID`, que sí lee `src/features/push/config.ts` | Agregarla, vacía y comentada |
| M4 | front | No hay `mise.toml`: `mise use node@24` crea uno sin versionar y mise no lee `.node-version` en esta máquina | Versionar `mise.toml` con `node = "24"` y `java = "temurin-17"`, como el back |

## 3. Documentación desactualizada

**Back** (`README.md`, `ARCHITECTURE.md`):
- El README remite al front en la rama `rebuild/v1`, pero la base es `Dev`.
- El README corre `dotnet` sin `mise exec --` y no menciona `mise trust`. En esta máquina, `dotnet` a secas toma el SDK 8 y falla por `global.json`.
- El README usa `open` (es de macOS) para Swagger.
- ARCHITECTURE §3 dice que `banned_emails` guarda SHA-256, pero desde FIN-25 es HMAC con `BANNED_EMAIL_KEY`.
- ARCHITECTURE §1 y la estructura del README no incluyen el feature `Profiles`.
- Al ER de ARCHITECTURE §3 le faltan `delivered_message_ids`, `user_artists`, `profile_reactions`, `message_franking_records` y `banned_emails`. Además, las tablas reales son `encrypted_backup` y `device_link_request` en singular.
- ARCHITECTURE §8 no menciona la retención del chat ni la purga de evidencia de `MaintenanceService`.
- `info.version` del contrato es `1.0.0` mientras el CHANGELOG va en draft.17. No es un error; basta una nota en el README.

**Front** (`README.md`, `ARCHITECTURE.md`, `docs/`):
- README, ARCHITECTURE y `tiendas.md` nombran la rama `rebuild/v1`. El README pide además el back en `rebuild/v1`, que está en el draft.6.
- La tabla de scripts del README no incluye `typecheck`, `lint` ni `build:bundles`.
- ARCHITECTURE no incluye los dominios `account`, `admin` y `shared` ni el panel de administración.
- `docs/upgrade/README.md` describe la base anterior: perfiles `validation-*`, "30 pruebas", Kafka y la rama `codex/…`.
- `docs/upgrade/INVENTORY.md` lista paquetes retirados y le faltan `@noble/*`, signalr, `expo-sqlite`, `expo-camera` y otros.

## 4. Decisiones de Camilo

- `encorly_back/AUDITORIA-2026-10-14.md` sigue sin trackear: falta decidir si se versiona, se mueve a `reportes/` o se descarta.
- Arrastrados del handoff del back: `PRIVACY_CONTACT_EMAIL`, rotar el Supabase de `955db69` y revisar los textos de privacidad
  de FIN-11 y FIN-12.

## 5. Orden sugerido

1. **Sesión del front:** I1 + I2 en una sola rama `fix/ci-dev-sdk57`, con M1, M3, M4 y la documentación del front.
2. **Sesión del back:** I3 con su test, M2 y la documentación del back, en `fix/swagger-docs`. Contrato sin cambios.
3. Si las rutas, scripts o comandos cambian, actualizar las §13 y §14 de cada manual técnico en el mismo PR.
