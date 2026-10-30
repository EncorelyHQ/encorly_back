# Plan — Manual técnico y manual de usuario (Markdown)

**Estado:** plan, sin ejecutar. **Decidido:** un manual por repo y estructura estándar (el docente no exige plantilla).

## 1. Entregables

| Archivo | Repo | Rama | Público |
|---|---|---|---|
| `docs/MANUAL-TECNICO.md` | `encorly_back` | `docs/manuales` desde `dev` → PR a `dev` | Quien mantenga o despliegue el back |
| `docs/MANUAL-TECNICO.md` | `encorely-front` | `docs/manuales` desde `Dev` → PR a `Dev` | Quien mantenga o compile la app |
| `docs/MANUAL-USUARIO.md` | `encorely-front` | misma rama | Usuario final (18–30 años, no técnico) |

Regla común: los manuales **enlazan** a `README.md`, `ARCHITECTURE.md` y `contracts/` en vez de copiarlos, para no tener dos fuentes de verdad. El manual explica y guía; el detalle vive donde ya existe.

## 2. Manual técnico — back (`encorly_back/docs/MANUAL-TECNICO.md`)

1. **Introducción:** propósito del sistema, alcance, a quién va dirigido, convenciones.
2. **Arquitectura:** resumen del monolito modular .NET 10 por features; enlace a `ARCHITECTURE.md`.
3. **Requisitos:** .NET 10 con mise (`mise.toml`), Docker con compose v2, puertos (API 5000, Postgres 5432).
4. **Instalación y arranque local:** paso a paso, verificado ejecutándolo (compose → migraciones → seed → API → `/health/ready`).
5. **Configuración:** tabla de variables de `.env.example` (qué hace cada una, valor por defecto, obligatoria o no), sin valores secretos.
6. **Base de datos:** modelo de datos (tablas y relaciones), cómo crear y aplicar migraciones (`dnx --yes dotnet-ef@10.0.13 …`, ver lecciones), seed curado y cómo regenerarlo.
7. **API:** autenticación (JWT + refresh con rotación), cómo leer el contrato `contracts/openapi.v1.json`, versionado en `contracts/CHANGELOG.md`, convenciones de error (ProblemDetails en español), tabla de endpoints por módulo.
8. **Tiempo real y push:** `ChatHub` autenticado y push FCM/APNs (qué se envía y qué no).
9. **Seguridad:** identidad solo desde el JWT, autorización por pertenencia, rate limit, chat E2EE desde el punto de vista del servidor (solo ciphertext), límites declarados (sin forward secrecy), manejo de secretos.
10. **Pruebas:** cómo correr la suite (xUnit + Testcontainers), qué cubre, cómo agregar un test de autorización.
11. **CI/CD y despliegue:** workflows existentes, qué falta para producción (D15: no se publica, pero queda preparado).
12. **Mantenimiento:** agregar un endpoint de punta a punta (código → test → contrato → CHANGELOG), logs, health checks.
13. **Solución de problemas:** errores reales vistos en el proyecto (lecciones de `Universidad/.claude/lecciones.md`: puertos, `--urls`, `dotnet-ef`, SSL de Postgres).

## 3. Manual técnico — front (`encorely-front/docs/MANUAL-TECNICO.md`)

1. **Introducción y alcance:** app móvil nativa (Android/iOS con Expo); la web no es objetivo.
2. **Arquitectura:** estructura de `src/`, sesión única, navegación (expo-router), cliente API generado desde el contrato; enlace a `ARCHITECTURE.md`.
3. **Requisitos:** Node con mise, gestor de paquetes del repo (no mezclar), Android SDK + JDK 17, emulador `encorely_api35`.
4. **Instalación y ejecución:** Expo Go vs dev build (`expo run:android`), URL del API (`EXPO_PUBLIC_API_URL`, `10.0.2.2` en emulador), verificado ejecutándolo.
5. **Configuración:** `.env.example`, `app.json`/`app.config`, scheme y deep links, permisos declarados.
6. **Contrato con el back:** cómo regenerar los tipos desde `../encorly_back/contracts/openapi.v1.json` y qué hacer cuando cambia la versión del contrato.
7. **Design system "La boleta compartida":** tokens, tipografías, iconos, componentes base y reglas (sin emojis como iconos, sin datos inventados, contraste AA).
8. **Chat cifrado en el cliente:** generación y guardado de llaves (SecureStore), cifrado con `@noble`, código de seguridad, límites declarados.
9. **Push y Spotify:** registro del token de dispositivo, conector Spotify (PKCE) y sus límites (5 usuarios en modo desarrollo).
10. **Pruebas:** typecheck, lint, Jest y E2E (enlace a `docs/e2e.md`).
11. **Build y distribución:** perfiles de EAS, requisitos de tiendas (enlace a `docs/tiendas.md`).
12. **Mantenimiento y actualización de SDK:** enlace a `docs/upgrade/`.
13. **Solución de problemas:** lecciones reales (gestor de paquetes, imports de `expo-router/react-navigation`, Metro, emulador).

## 4. Manual de usuario (`encorely-front/docs/MANUAL-USUARIO.md`)

Lenguaje simple, tuteo, sin jerga técnica; cada pantalla con **captura real** del emulador y pasos numerados.

1. **Qué es Encorely** y para quién es (mayores de 18).
2. **Requisitos:** teléfono Android/iOS y conexión a internet.
3. **Primeros pasos:** crear cuenta, iniciar sesión, onboarding de géneros, conectar Spotify (opcional).
4. **Descubrir música:** swipe con botones ✕/♥, previews de audio, cómo se forma tu ADN musical.
5. **Eventos y radar:** marcar que vas a un evento, ver personas compatibles, "Me interesa" y cuándo se puede dar like.
6. **Matches:** cuándo se crea un match (los dos se dan like), ver tus matches.
7. **Chat privado:** enviar mensajes, qué significa el cifrado de extremo a extremo en palabras simples, cómo verificar el código de seguridad con la otra persona, qué no protege (sin forward secrecy, un solo dispositivo).
8. **Perfil y ajustes:** editar perfil, notificaciones, cerrar sesión.
9. **Seguridad y privacidad:** bloquear y reportar, borrar tu cuenta y qué se borra, política de privacidad.
10. **Preguntas frecuentes y problemas comunes:** no veo personas en el radar, no me llegan notificaciones, perdí el teléfono (se pierde el historial del chat).
11. **Glosario simple.**

## 5. Cómo se ejecuta (cuando se decida)

- **Quién:** las dos terminales actuales (una por repo), cada una con su manual técnico; el manual de usuario lo hace la terminal del front.
- **Fuentes:** el código actual de la rama base, `README.md`, `ARCHITECTURE.md`, `contracts/`, `docs/` existentes, `reportes/06` y `reportes/ENCORELY-REPORTE-COMPLETO.md`.
- **Verificación:** cada comando de instalación del manual técnico se ejecuta tal cual antes de escribirlo. El manual de usuario se arma recorriendo la app en el emulador y tomando capturas con `adb exec-out screencap -p` en `docs/img/manual-usuario/`.
- **Calidad:** sin secretos ni contraseñas reales (solo cuentas demo del seed); sin capturas con datos personales; redacción sin "tono IA" (frases cortas, concretas).
- **Cierre:** commit en `docs/manuales` (en español, imperativo) y PR a la rama base con el token de `lmilo`.
- **Esfuerzo estimado:** back técnico ~2–3 h; front técnico ~2–3 h; usuario ~3–4 h (por las capturas). Los tres pueden ir en paralelo.
