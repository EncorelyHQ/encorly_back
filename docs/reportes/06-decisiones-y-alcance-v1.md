# Encorely — Decisiones y alcance de la v1 (48 h)

**Fecha:** 2026-10-13 · **Base:** `00-reporte-general.md` (plan completo ≈ 132 días-persona) recortado a una entrega de **48 horas**.
**Regla del recorte:** todo lo que entra funciona de punta a punta y tiene prueba. Lo que no cabe se documenta, no se deja a medias.

---

## 1. Decisiones tomadas

| # | Decisión | Elegido |
|---|---|---|
| D1 | Plataforma back | **.NET 10 LTS** |
| D2 | Base del front | **Toolchain de la rama `codex/actualizar-frontend` (Expo 57) + `src/` nuevo** |
| D3 | Kafka / Redis / Worker | **Eliminados.** API + `ChatHub` SignalR autenticado |
| D4 | Acceso a datos | **EF Core 10 + migraciones**; Dapper solo para la query del radar |
| D5 | Dirección visual | **"La boleta compartida"** (03), adoptada sin validación previa |
| D6 | Chat E2EE | **Sin forward secrecy en v1, declarado** en la app y en la documentación |
| D7 | `Messages` legado | No se toca el legado; el esquema nuevo nace sin esa tabla |
| D10 | Login | **Email + contraseña** en v1; Google/Apple en v1.1; Spotify opcional a futuro |
| D11 | Canciones / ADN | **Catálogo curado** con previews de 30 s (iTunes Search); ADN = vector de géneros |
| D12 | Eventos | **Tabla curada** (seed). Integración externa la hace el equipo de la v2 |
| D13 | Salas grupales | **Fuera de la v1** |
| D14/D9 | Repos | **Ramas nuevas** en `encorly_back` y `encorely-front`. El resto de repos no se toca |
| D15 | Hosting / tiendas | **No se publica.** Entregable = código + demo en emulador/teléfono |
| D16 | Legado | **No se arregla**; todo el esfuerzo al sistema nuevo |
| Cripto | Librería E2EE | **`@noble/curves` (X25519) + `@noble/ciphers` (XChaCha20-Poly1305)**, JS puro, corre en Expo Go |
| Rúbrica | Restricciones del docente | Ninguna tecnología/arquitectura exigida |

**Entregables:** demo en emulador/teléfono · tests pasando · código y repos limpios (solo back y front) · documento de arquitectura.

---

## 2. Qué ENTRA en la v1 (48 h)

### Backend — `encorly_back`, rama `rebuild/v1`, .NET 10
| # | Funcionalidad | Criterio de aceptación |
|---|---|---|
| B1 | **Auth email**: registro, login, refresh con rotación, logout (revoca refresh) | Email normalizado (trim + minúsculas), BCrypt, JWT 15–30 min + refresh; rate limit en login |
| B2 | **Identidad solo desde el JWT** en todos los endpoints | Test: usuario A no lee ni escribe nada de B (403/404) |
| B3 | **Perfil**: ver, editar (nombre, edad 18+, ciudad, géneros), **borrar cuenta** | Borrar elimina datos personales y mensajes del usuario |
| B4 | **Catálogo curado** de canciones (seed) con género, artista, portada y preview | ≥ 60 canciones de la escena colombiana/latina |
| B5 | **Swipes** like/dislike, únicos por canción; **ADN = vector de géneros** que cambia con cada swipe | Test: el ADN cambia tras swipes; repetir canción no suma |
| B6 | **Eventos curados** (Bogotá/Medellín, fechas futuras) + "voy a este evento" | ≥ 15 eventos en seed |
| B7 | **Radar por evento**: asistentes del evento, ordenados por afinidad real (no todos al 99 %) | Umbral configurable; solo usuarios que van al evento |
| B8 | **Match recíproco**: like a persona → match solo si ambos se dan like; estado en `Matches` | Test: like unilateral no crea match |
| B9 | **Chat**: enviar/listar paginado por REST, aviso en tiempo real por `ChatHub` autenticado; **el servidor solo guarda ciphertext** | Test: la BD no contiene texto plano; solo miembros del match acceden |
| B10 | **Directorio de llaves públicas** (una llave por usuario en v1) | El servidor nunca recibe llaves privadas |
| B11 | **Bloquear y reportar** usuario | Bloqueado no aparece en radar ni puede escribir |
| B12 | Errores **ProblemDetails** en español, validación, `/health/live` y `/health/ready` sin detalles, Swagger solo en Development, **OpenAPI exportado** | Ningún 500 por input inválido |
| B13 | **Tests**: xUnit + WebApplicationFactory + Postgres real (Testcontainers) | Suite verde en local y en GitHub Actions |

### Front — `encorely-front`, rama `rebuild/v1` (desde Expo 57), `src/` nuevo
| # | Funcionalidad | Criterio de aceptación |
|---|---|---|
| F1 | **Registro y login** por email; una sola sesión (`SessionContext`) en SecureStore; refresh automático ante 401 | Reabrir la app mantiene sesión; token vencido no deja la app zombi |
| F2 | **Onboarding**: datos básicos + elegir géneros (sin Spotify) | Se completa sin salidas muertas |
| F3 | **Swipe** de canciones con preview (expo-audio) **y botones ✕/♥** accesibles | Funciona solo con botones (accesibilidad) |
| F4 | **Eventos** + marcar asistencia + **Radar** del evento | Muestra asistentes reales con afinidad real |
| F5 | **Like a persona → match**; lista de matches con nombre real | Sin "Match XXXX", sin cifras inventadas |
| F6 | **Chat E2EE**: llaves X25519 generadas en el dispositivo (SecureStore), mensajes cifrados con XChaCha20-Poly1305, **código de seguridad** visible para verificar | El mensaje viaja y se guarda cifrado; la app declara "sin forward secrecy" en la info de seguridad |
| F7 | **Perfil**: editar, cerrar sesión, **borrar cuenta**; bloquear/reportar desde el chat | Todo funciona en Android |
| F8 | **Design system "boleta"**: tokens, Big Shoulders + Archivo + IBM Plex Mono, iconos Phosphor, componentes base; **sin emojis como iconos ni datos falsos** | Un solo tema; contraste AA |
| F9 | **Tests**: Jest para cripto (cifrar/descifrar, llave errónea falla) y cliente API | Suite verde |

### Documentación y repos
| # | Entregable |
|---|---|
| D-1 | README de cada repo como **runbook**: levantar en < 15 min (docker compose + migraciones + seed + app en emulador) |
| D-2 | `ARCHITECTURE.md`: diagrama, capas, modelo de datos, flujo de match, **E2EE con modelo de amenazas y límites declarados** |
| D-3 | Contrato **OpenAPI** versionado (para el equipo de la v2) |
| D-4 | `.env.example` completos, sin secretos de fallback; CI mínimo (build + tests) en ambos repos |

---

## 3. Qué queda FUERA de la v1 (documentado como v1.1 / v2)

| Fuera | Por qué | Cuándo |
|---|---|---|
| Login con **Google y Apple** | Requiere configurar consolas de Google/Apple y dev build | v1.1 |
| Conector **Spotify** (importar gustos) | Modo desarrollo limitado a 5 usuarios | v2 |
| **Notificaciones push** | Requiere dev build + FCM/APNs | v1.1 |
| **Salas grupales** por evento | Exigen moderación, roles y SLA | v2 |
| **Forward secrecy**, multi-dispositivo, backup cifrado, verificación por **QR con cámara** | Fuera de 48 h; v1 muestra el código de seguridad en texto | v1.1 / v2 |
| **Integración externa de eventos** (Ticketmaster u otra) | La hace el equipo de la v2 | v2 |
| **Publicación en tiendas**, EAS builds, hosting de producción | Entregable = código + demo | v2 |
| **QA en iOS** | No hay Mac en el entorno | v2 |
| Landing, Django, Python, monorepo | El dueño decidió no tocarlos | — |
| Kafka, Redis, Worker | Eliminados por decisión D3 | — |
| Panel de administración | Eventos y canciones entran por seed | v2 |

---

## 4. Orden de trabajo y qué se corta si no da el tiempo

**Orden:** (1) base de repos y ramas → (2) back B1–B2–B12 + esquema y seed → (3) front F1 + design system F8 → (4) swipe/ADN/eventos/radar (B4–B7, F2–F4) → (5) match + chat E2EE (B8–B10, F5–F6) → (6) perfil, borrar, bloquear/reportar (B3, B11, F7) → (7) tests, docs, CI.

**Si el tiempo no alcanza, se recorta en este orden** (de lo primero a sacar a lo último): verificación con código de seguridad (F6 parcial) → bloquear/reportar (B11) → previews de audio (se deja portada + artista) → CI en GitHub Actions. **No se recorta:** identidad desde JWT, match recíproco, chat cifrado funcional, tests de autorización.

---

## 5. Riesgos

- **48 h es muy justo** para este alcance: el riesgo principal es el chat E2EE. Por eso usamos `@noble` (sin dev build) y verificación simple.
- **Expo 57** sale de la rama de `edav404`: hay que avisarle que `rebuild/v1` parte de su trabajo.
- **Previews de iTunes**: uso académico con atribución; revisar términos antes de un uso comercial.
- **.NET 10 SDK** no está instalado: se instala al inicio (Microsoft apt o mise).

---

## 6. Mandato del dueño (vigente en todo momento)

**La v1 es la meta mínima, no el límite.** En las 48 h de trabajo continuo se prioriza la v1 y, sin pausa, se ejecuta **todo el resto del plan curado** (v1.1 y v2 de `00-reporte-general.md` y `ENCORELY-REPORTE-COMPLETO.md`). El "plan de 132 días-persona" no es una razón para recortar.

**Únicas exclusiones:** salas grupales por evento, Ticketmaster API, Uber API y **cualquier otra API externa**.

Se trabaja por cortes verticales: en todo momento existe una versión que funciona de punta a punta, y cada corte suma sobre la anterior.

**Excepciones permitidas (confirmadas por el dueño):**
- **Previews de iTunes Search** para el audio del swipe.
- **Push con FCM/APNs** (expo-notifications).
- **Conector Spotify:** ya lo construyó `edav404` (PKCE en el front, `SpotifyService` en el back). Se **conserva y adapta** en la reconstrucción como conector opcional para importar gustos, sin `audio-features` (deprecado) y sabiendo que el modo desarrollo de Spotify limita a 5 usuarios.

**Quedan fuera:** login con Google y Apple (solo email + contraseña; sin Google tampoco se exige Sign in with Apple), Ticketmaster, Uber y cualquier otra API externa no listada.
