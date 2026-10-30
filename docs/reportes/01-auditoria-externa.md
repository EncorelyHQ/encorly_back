# Auditoría externa — Encorely

**Fecha:** 2026-10-13 · **Alcance:** sistema completo (`encorly_back`, `encorely-front`, `Encorly-landing`, `EncorelyDjango`, `EncorelyPython`, infra, DB, DX) · **Dimensiones:** todas · **Método:** 4 auditores en paralelo (backend, frontend+UI, contratos+seguridad, infra/DB/DX) + verificación propia levantando el sistema en local. Solo lectura salvo los parches locales previos (sin commit) que se evalúan aquí.

---

## 1. Resumen ejecutivo

El sistema **no es desplegable ni seguro** en su estado commiteado y **solo funciona en el camino feliz nativo con Spotify**.

1. **Autorización inexistente (IDOR sistémico).** Ningún endpoint lee la identidad del JWT; todos reciben `userId` del cliente. Con un JWT gratuito (`/Auth/register` o `/Auth/google`, que no valida nada) se leen/escriben datos y chats de cualquiera.
2. **Secretos expuestos.** Password real de Supabase en historial de un repo **público** (`955db69`); secreto JWT y password de BD como *fallback* en código.
3. **Roto de punta a punta.** HEAD devuelve 500 en todo POST/PUT con body; Dockerfile .NET 9 vs código net8 (no arranca); enviar mensaje de chat da 400; sin Spotify la app queda en spinners infinitos; logout no funciona en web; JWT muere a los 30 min sin refresh.
4. **Sin fuente de verdad.** Esquema de BD borrado del repo (migraciones EF eliminadas en `7ee096c`) y con *drift* frente al SQL; documentación que describe otro sistema (.NET 9, EF Core, distancia euclidiana); 3 fuentes de sesión en el front; 2 temas, 2 sistemas de estilos, 2 lockfiles.
5. **UI que miente y luce genérica.** Controles de privacidad placebo, cifras inventadas ("247 personas"), acciones simuladas, emojis como iconos, contraste por debajo de AA.

**Veredicto:** prototipo académico con buena intención arquitectónica pero sin cerrar ninguna capa; requiere reconstrucción ordenada (no big-bang) empezando por seguridad, contrato API y esquema versionado.

---

## 2. Mapa del sistema

| Repo | Stack | Último commit | Remoto | Estado |
|---|---|---|---|---|
| `encorly_back` | .NET 8, Dapper, Npgsql, Kafka (Confluent), Redis, SignalR, Firebase, BCrypt | 2026-06-06 | EncorelyHQ · **público** | Vivo (Render, sospecha) |
| `encorely-front` | Expo 54, RN 0.81, React 19, expo-router 6, styled-components 6, Reanimated 4 | local 2026-06-06 · remoto `codex/actualizar-frontend` (Expo 57) **2026-10-14** | EncorelyHQ · público | Vivo (Vercel, cuenta personal) · **trabajo paralelo de otra persona** |
| `Encorly-landing` | Vite 7, React 19, Tailwind 4, Netlify Functions, Brevo | `feat/reconstruction` 06-11 (1 commit sin push); `main` 03-12 | lmilo (personal) · público | Vivo |
| `EncorelyDjango` | Django 5.1, DRF, SQLite | 2026-05-09 | EncorelyHQ | Implementación **paralela** (no anterior), muerta |
| `EncorelyPython` | Python + Tkinter en memoria | 2026-04-11 | EncorelyHQ | Muerto (MVP académico) |

**Back (capas reales):** `EncorelyApi` (8 controllers + 2 hubs) → `EncorelyApplication` (servicios) → `EncorelyQuery` / `EncorelyRepository` (Dapper, CQRS ligero) → Postgres. `EncorelyWorker` consume `swipe-raw-events` y crea matches. `EncorelyDomain` vacío y fuera del `.sln`; el dominio vive en `EncorelyModels`.
**Front:** `src/app` (rutas que reexportan) → `src/modules/*/screens` → `src/clients/{api,spotify}` + 3 contextos (`AuthContext`, `SpotifyAuthContext`, `OnboardingContext`) + `NavigationGuard`.
**Tests:** back 28/28 verde (unitarios con NSubstitute); front 24/24 verde (3 suites). Ninguno cubre SQL, autorización ni integración.
**Tooling:** back sin `dotnet format`; front con **85 errores de `tsc`**, sin ESLint/Prettier/CI; runtimes sin pin (`global.json`, `.nvmrc`, `.mise.toml` ausentes).
**Infra:** `docker-compose.yml` (postgres + kafka + control-center), `docker-compose.local.yml` nuevo sin trackear, k8s/terraform decorativos ("Tarea 82/83"). Deploy real: Render + Supabase + Vercel + Netlify, sin IaC.

---

## 3. Scorecard

| Dimensión | Estado | Benchmark | Comentario |
|---|---|---|---|
| Seguridad | 🔴 | OWASP Top 10 / ASVS L1 | IDOR total, login falso con Google, secretos en historial público y en código, hubs anónimos |
| Arquitectura back | 🔴 | Clean Architecture, SOLID, 12-factor | Application depende de infraestructura; Domain vacío; lógica en controllers; config ×3 |
| Contratos front↔back | 🔴 | Contrato único/tipado | Chat roto (400), escalas 0–1 vs 0–100, enums int/string mezclados, DTOs muertos |
| Persistencia / DB | 🔴 | Migraciones versionadas | Esquema no existe en repo; drift con SQL; Matches sin estado; INSERT de VenueRoom inválido |
| Mensajería / Worker | 🔴 | At-least-once, outbox | Producer ignora config, dual-write, sin DLQ, N+1, worker sin imagen |
| Arquitectura front | 🟡 | Idiomático Expo Router | 3 fuentes de sesión, guard imperativo frágil, fetch manual por pantalla, God Components |
| UI/UX | 🔴 | Nielsen, WCAG 2.2 AA | Callejones sin salida, controles placebo, datos fake, contraste < AA, swipe solo por gesto, estética "hecha con IA" |
| Design system | 🔴 | Tokens únicos | 2 temas con marcas distintas, 81 hex hardcodeados, sin escala de spacing |
| Tests | 🟡 | Cobertura de riesgo | Verdes pero "test theater": no detectan ninguno de los críticos |
| Dependencias | 🟡 | Sin CVE alta, lock único | Npgsql 8.0.0 (CVE alta), SignalR.Core 1.2.0, 2 lockfiles, `.npmrc` de otro usuario; .NET 8 EOL 10-nov-2026 |
| DX / Onboarding | 🔴 | Levantar en <15 min | El README no levanta el back (8 pasos fallan) |
| CI/CD | 🟡 | Build+test+docker+lint | Back: CI con SDK 9 y sin `docker build`; front y landing sin CI; Vercel sin deploy desde 05-29 |
| Documentación | 🔴 | Docs = realidad | README/PROJECT_STATUS describen otro sistema |
| Organización de repos | 🟡 | Ownership claro | 2 repos muertos públicos, landing fuera de la org, ramas largas divergentes |

---

## 4. Hallazgos priorizados

### Críticos

**[C1] IDOR/BOLA sistémico: la identidad viene del cliente** · Seguridad (OWASP A01, ASVS V4) · *Broken Object Level Authorization* · Verificado
- Evidencia: 0 usos de `ClaimTypes.NameIdentifier` en `EncorelyApi` aunque `TokenService.cs:30` lo emite. Afectados: `UserController.cs:24,41-43` (GET me, PUT settings), `SwipesController.cs:21,28-30`, `MatchesController.cs:20,27`, `ChatController.cs:24,39`, `EventsController.cs:43`, `VenueController.cs:41`, `PlaylistController.cs:22-25`, hubs `NotificationHub.cs:7`, `VenueHub.cs:20`.
- Impacto: cualquier usuario autenticado lee email de otro, cambia su mood, infla su SwipeCount (salta el umbral), acepta sus matches, lee y escribe sus chats.
- Fix:
  ```csharp
  [ApiController, Authorize]
  public abstract class ApiControllerBase : ControllerBase {
      protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  }
  // Quitar UserId de DTOs y [FromQuery]; en MatchService validar pertenencia:
  if (m.UserId1 != uid && m.UserId2 != uid) throw new ForbiddenException();
  ```
- Esfuerzo: M (back + 7 funciones del front).

**[C2] Chat sin control de pertenencia** · Seguridad · Verificado
- Evidencia: `ChatController.cs:24-34`, `MatchService.cs:43-61` nunca compara con `UserId1/UserId2`.
- Impacto: leer/escribir conversaciones ajenas conociendo el `matchId` (expuesto por `/Matches/pending?userId=<víctima>`).
- Fix: el de C1 + test "A no accede a recurso de B". Esfuerzo: S.

**[C3] Login falso y toma de cuentas** · Seguridad (ASVS V2) · *Broken Authentication* · Verificado
- Evidencia: `IdentityService.cs:51-57` (`LoginWithGoogleAsync`) ignora el `idToken` y crea un usuario aleatorio con JWT válido; `IdentityService.cs:99` vincula por `GetBySpotifyIdAsync(id) ?? GetByEmailAsync(email)` (email de Spotify no verificado → entra a cuenta Custom ajena; pre-hijacking inverso).
- Fix: `GoogleJsonWebSignature.ValidateAsync(idToken, new() { Audience = new[]{ clientId } })`; vincular solo por `(Provider, ProviderId)`; si no se implementa Google, devolver 501. Esfuerzo: M.

**[C4] Password real de Supabase en historial de repo público** · Secretos (A05/A07) · Verificado
- Evidencia: `git show 955db69:DEPLOYMENT_INFO.md` (host, user y password de 20 caracteres); reemplazado en `e78361b`, sigue en `main`, `origin/dev`, `origin/feature/api`; `gh repo view` → PUBLIC.
- Fix: **rotar en Supabase ya**; activar secret scanning + push protection; `git filter-repo` opcional (forks/caches ya la tienen). Esfuerzo: S.

**[C5] Secretos de fallback hardcodeados** · Secretos / 12-factor III · *Insecure default* · Verificado
- Evidencia: JWT secret en `Program.cs:132` y `TokenService.cs:20`; DB password en `Program.cs:35` y ambas `DbConnectionFactory.cs:22` (mismo valor que `.env.example`). `.env.example` no declara `JWT_SECRET_KEY`.
- Fix:
  ```csharp
  builder.Services.AddOptions<JwtOptions>().BindConfiguration("Jwt")
      .Validate(o => o.Secret?.Length >= 32, "Jwt:Secret >= 32 bytes").ValidateOnStart();
  ```
  Esfuerzo: S.

**[C6] HEAD responde 500 en todo POST/PUT con body** · API · *Misplaced validation attributes* · Verificado (ejecutado)
- Evidencia: `git show HEAD:EncorelyApplication/DTOs/Requests.cs` tiene 9 `[property: Required]`; ASP.NET Core 8 lanza `InvalidOperationException` en records posicionales. Parche local correcto, **sin commit**. Pendiente: `[Required]` sobre `Guid`/enum no valida (`Guid.Empty` pasa, `mood: 7` viola CHECK → 500).
- Fix: commitear parche + `[EnumDataType]` / validador de Guid no vacío. Esfuerzo: S.

**[C7] Imagen Docker no arranca; Worker sin imagen** · Despliegue · *Runtime/TFM mismatch* · Verificado por análisis
- Evidencia: `Dockerfile:8,36-37` usa `sdk:9.0`/`aspnet:9.0`; todos los csproj `net8.0`, sin `RollForward`. k8s referencia `encorely-worker:latest` inexistente. CI usa SDK 9 y no hace `docker build`.
- Fix: imágenes `:8.0` (o migrar todo a .NET 10, ver I-dep), `ARG PROJECT` para Api/Worker, job `docker build` en CI. Esfuerzo: S.

**[C8] Producer Kafka ignora su configuración + dual-write** · Integración · *Dead configuration / Dual write* · Verificado
- Evidencia: `Program.cs:79-88` arma `ProducerConfig` que nadie usa; `KafkaProducer.cs:19` lee `Kafka:BootstrapServers` con fallback `localhost:9092`; Worker usa un tercer criterio (`KafkaConsumerWorker.cs:43`). `SwipeService.cs:40-46` escribe en BD y espera `Acks.All`.
- Impacto: fuera de local cada swipe espera ~300 s y da 500 con la BD ya modificada.
- Fix: inyectar el `ProducerConfig` registrado, `MessageTimeoutMs=5000`, patrón outbox. Esfuerzo: M.

**[C9] Esquema de BD sin fuente de verdad, con drift** · Persistencia · *Schema drift / ClickOps DDL* · Verificado
- Evidencia: migraciones EF borradas en `7ee096c`; snapshot vs Dapper: `Messages."Timestamp"` vs `"CreatedAt"` (`MessageRepository.cs:20`); `VenueRooms` sin `Description/Capacity/IsActive/CreatedAt` en snapshot pero usados; `ON CONFLICT ("UserId")` (`MusicalProfileRepository.cs:22`) exige UNIQUE inexistente. `db/init/*.sql` reconstruido y **sin trackear**.
- Fix: DbUp (encaja con Dapper) con `V001__baseline.sql` = `01_schema.sql`; test de integración con Testcontainers que ejecute todo el SQL. Esfuerzo: M.

**[C10] `VenueRoomRepository` inserta columnas que el modelo no tiene** · Persistencia · *Model/SQL drift* · Verificado por lectura
- Evidencia: `VenueRoomRepository.cs:20,32` usa `@Description`, `@Capacity`, `@CreatedAt` (no existen en `VenueRoom.cs`), no persiste `EventId`/`ExpiresAt`; `durationHours` ignorado; `GetActiveRoomsAsync` filtra columna `IsActive` vs propiedad calculada.
- Fix: `INSERT ("Id","EventId","Name","ExpiresAt","CreatedAt")`. Esfuerzo: S.

**[C11] Envío de chat roto (contrato) y error oculto** · Contratos/UX · Verificado
- Evidencia: front `src/clients/api/matches.ts:27-35` manda `body: content` (string); back `ChatController.cs:39` espera `SendMessageRequest { Content }` (`Requests.cs:26`) → 400. `ChatScreen.tsx` no revierte el mensaje optimista y pinta el error solo en `ListEmptyComponent`.
- Fix: `body: { content }`; estado `failed` + reintento; banner de error. Esfuerzo: S.

**[C12] Sesión zombi: JWT de 30 min sin refresh ni manejo de 401** · Auth · Verificado
- Evidencia: `IdentityService.cs:85` (`AddMinutes(30)`), no hay `/Auth/refresh`, `GetPrincipalFromExpiredToken` muerto, refresh token guardado en claro; front descarta `refreshToken`/`expiration`, `http.ts` no intercepta 401, `useSwipeEngine.ts:26-29` traga errores.
- Fix: `/Auth/refresh` con rotación y hash SHA-256; interceptor 401 single-flight en `apiRequest`; validar `expiration` al hidratar. Esfuerzo: M.

**[C13] Sin Spotify la app es un callejón sin salida** · UX/Estados · Verificado (código + navegador)
- Evidencia: `SwipeScreen.tsx:105,111,188` — `if (!token) return` antes del `try/finally` deja `loading=true` para siempre ("Preparando tracks…"); igual en `ProfileScreen.tsx:553,714`. El endpoint `getNextTrack` (`clients/api/swipes.ts:6`) existe y no se usa.
- Fix: estado explícito "Conecta Spotify" o feed del backend; nunca `return` antes de resolver el estado de carga. Esfuerzo: S.

**[C14] Logout y todas las confirmaciones no funcionan en web** · Plataforma · *Native API sin polyfill* · Verificado
- Evidencia: `react-native-web` implementa `Alert.alert` como `static alert() {}`; `SettingsScreen.tsx:401-417` pone `logout` en el `onPress` del Alert. Afecta también `OnboardingStep5Screen.tsx:125,145`, `OnboardingStep3Screen.tsx:105,113`, `LoginScreen.tsx:25`, `SpotifyConnectCard.tsx:96`, `SettingsScreen.tsx:396,579,589`. Target de deploy: Vercel (web).
- Fix: adaptador `confirm()` por plataforma o modal propio. Esfuerzo: S.

**[C15] La UI miente: controles placebo y datos inventados** · Ética/UX (Nielsen #1) · *Placebo control / Fake data* · Verificado
- Evidencia: toggles de privacidad solo en AsyncStorage (`SettingsScreen.tsx:522,535`); "Conectar" muestra "Solicitud enviada ✓" sin llamar a nada y "Pasar" sin `onPress` (`RadarMatchesScreen.tsx:69-73`); "247 personas con tu vibe cerca" hardcodeado (`RadarScreen.tsx:152`); "¡Felicidades! Alcanzaste el umbral" con 0 swipes (`:147`); preferencias de onboarding guardadas y nunca usadas; feed de eventos fijo con fechas pasadas (`EventService.cs:9-16`); `next-track` fijo (`SwipeService.cs:56,59`); push Firebase comentado (`FirebasePushNotificationService.cs:35`).
- Fix: implementar u ocultar/etiquetar "Próximamente"; mover mocks a implementaciones fake registradas solo en Development. Esfuerzo: S (ocultar) / M (implementar).

**[C16] Hubs SignalR anónimos con identidad elegida por el cliente** · Seguridad · Verificado
- Evidencia: `NotificationHub.cs:5-9`, `VenueHub.cs:6,20` sin `[Authorize]`; `SendVenueMessage` difunde sin persistir ni moderar; falta `JwtBearerEvents.OnMessageReceived`. El front no usa SignalR.
- Fix: quitar `MapHub` hasta que haya consumidor, o `[Authorize]` + grupos por `Context.UserIdentifier`. Esfuerzo: S.

**[C17] Dependencias con CVE alta** · Supply chain · Verificado (`dotnet list package --vulnerable`, `npm audit`)
- Evidencia: Npgsql 8.0.0 (GHSA-x9vc-6hfv-hg8c), `Microsoft.AspNetCore.SignalR.Core` 1.2.0 (GHSA-334f-vvp4-7g6p, redundante con FrameworkReference), transitivas `System.Text.Json` 8.0.0 y `Caching.Memory` 8.0.0; landing `react-router` (runtime: open redirect/XSS). Front: 2 critical + 52 high mayormente de toolchain.
- Fix: Npgsql ≥ 8.0.3, quitar SignalR.Core, parches 8.0.x, subir react-router. Esfuerzo: S.

### Importantes

**[I1] Clean Architecture declarada, no aplicada** · Arquitectura · *Dependency Inversion violada / Leaky layers* · Verificado
- `EncorelyApplication.csproj` referencia `EncorelyQuery` y `EncorelyRepository` (Dapper/Npgsql); controllers inyectan queries directamente (`UserController`, `EventsController`); `EncorelyDomain` vacío y fuera del sln; `EncorelyModels/Events/*.cs` con namespace `EncorelyDomain.Events`; Application hace `new SpotifyClient` (`SpotifyService.cs:11-58`, `PlaylistService.cs:53`).
- Fix: interfaces de persistencia en Application/Domain; infra referencia hacia adentro; `ISpotifyClientFactory`. Esfuerzo: L.

**[I2] Lógica de negocio en controllers y en el worker (duplicada)** · Arquitectura · *Fat Controller / Copy-paste / Anemic Domain*
- `EventsController.GetMatches` (`:43-86`) con umbral, scoring, `dynamic` e ignora `eventId`; mismo bucle en `KafkaConsumerWorker.cs:159-173`; umbral 25 en 4 sitios; `UserService.GetMeAsync/UpdateSettingsAsync` muertos porque el controller se los salta.
- Fix: `MatchmakingService` + `SwipeThresholdPolicy` únicos en Application. Esfuerzo: M.

**[I3] N+1 en matchmaking** · Rendimiento · *N+1* · Verificado
- Worker: `GetAllAsync()` (LIMIT 500) + 1 consulta de perfil por candidato = hasta 501 queries por like; `EventsController` igual sin LIMIT y con 1 ida a Redis por candidato; `SELECT *` carga `PasswordHash` y `RefreshToken` de cientos de usuarios.
- Fix: una consulta `JOIN "MusicalProfiles"` con columnas explícitas. Esfuerzo: S.

**[I4] Métrica de afinidad degenerada y escala incoherente** · Dominio · *Wrong metric / Magic number* · Verificado
- Coseno sobre 3 rasgos no negativos 0–1 (`CompatibilityService.cs:13-24`) da ≥0.95 para perfiles realistas (el umbral 0.70 deja pasar a casi todos; `Tempo` ignorado). `IsHighPriority` compara `>= 85.0` contra 0–1 (`EventsController.cs:75`, `KafkaConsumerWorker.cs:173`) → siempre false; push dice "Afinidad: 0.93%".
- Fix: centrar rasgos o 1 − distancia euclídea normalizada; usar `CompatibilityService.IsHighPriority`; contrato único 0–1. Esfuerzo: S.

**[I5] Flujo de matches incompleto** · Funcional · *Stub* · Verificado
- `GetPendingMatchesAsync` devuelve `"Match " + Id[..4]` y no el otro usuario (`MatchService.cs:40`); no hay estado (aceptado = mensaje de sistema con `SenderId=Guid.Empty`); `AcceptMatchAsync` no idempotente y notifica antes de persistir; `SignalRNotificationService.cs:24,32` usa `matchId` como userId.
- Fix: columna `Status`, `otherUserId` + JOIN a `Users`, idempotencia. Esfuerzo: M.

**[I6] Kafka sin garantías** · Mensajería · *At-most-once accidental* · Verificado
- `EnableAutoCommit=true` + errores solo logueados (`KafkaConsumerWorker.cs:51,94-98`) → mensajes perdidos; sin DLQ ni idempotencia; `Consume()` bloqueante antes del primer `await` (`:67`); topics producidos sin consumidor (`UserDnaSync`, `MatchConvertedToChat`, `VenueModerationFlagged`); key `Null`; access token de Spotify viaja en claro (`IdentityService.cs:143`); dos interfaces de productor equivalentes.
- Fix: commit manual, topic `.dlq`, `await Task.Yield()`, key = UserId, no publicar tokens. Esfuerzo: M.

**[I7] Sin transacciones; excepciones de dominio → 500** · Integridad · *Generic exceptions*
- Alta de usuario en 3 sentencias sin transacción (`IdentityService.cs:114-150`); `KeyNotFoundException`/`InvalidOperationException` no mapeadas (`ExceptionMiddleware.cs:28-34`); 23505 en registro concurrente → 500; *lost update* en `UsuarioRepository.UpdateAsync` (`:37`) que reescribe `SwipeCount`.
- Fix: unit of work con `IDbTransaction`; `ProblemDetails` + `IExceptionHandler`; UPDATEs específicos. Esfuerzo: M.

**[I8] AuthN por debajo de ASVS L1** · Seguridad · Verificado
- Sin rate limiting/lockout (`AddRateLimiter` = 0); `MinLength(6)`; 409 en registro enumera emails; login/register responden 202; refresh token en claro.
- Fix: `AddRateLimiter` + `[EnableRateLimiting("auth")]`; mínimo 12 caracteres. Esfuerzo: S–M.

**[I9] Superficie operativa expuesta** · Seguridad/Ops · Verificado
- `/health` anónimo devuelve `ex.Message` de Postgres (`Program.cs:181,201`) y la liveness de k8s lo usa (restart loop si cae la BD); Swagger en producción (`Program.cs:163-171`); `UseHttpsRedirection` sin `UseForwardedHeaders`; `SSL Mode=Require;Trust Server Certificate=True` (MITM) hardcodeado; Firebase falla en silencio (`Program.cs:48`).
- Fix: `/health/live` y `/health/ready` sin detalle; Swagger solo en Development; `SSL Mode` configurable (`VerifyFull` en prod, `Prefer` en local). Esfuerzo: S.

**[I10] Moderación y roles de Venue** · Seguridad · Verificado
- Filtro `Contains` de 6 palabras (`VenueService.cs:17,62`); DELETE "admin" sin rol (`VenueController.cs:55`); `durationHours` sin límites (overflow → 500); `dna-mix` exige el token de Spotify del otro usuario.
- Fix: `[Authorize(Roles="Admin")]` + claim de rol; validar rangos; tokens de Spotify en servidor cifrados. Esfuerzo: M.

**[I11] Front: tres fuentes de verdad de sesión + guard frágil** · Arquitectura front · *Duplicated state / Imperative navigation*
- `AuthContext.user` (tipado `SpotifyUser` también para email), `SpotifyAuthContext.user`, `backendUserId`; `NavigationGuard.tsx:24-93` compara strings de segmento y hace `replace` en cascada; `SpotifyConnectCard.tsx:105` navega en paralelo; fallo de vinculación con backend tragado (`SpotifyAuthContext.tsx:96-105`).
- Fix: `SessionContext` con unión discriminada + `Stack.Protected` de expo-router.
  ```ts
  type Session = { status: 'anon' } | { status: 'authed'; userId: string; jwt: string; provider: 'spotify' | 'email'; spotify?: SpotifyTokens }
  ```
  Esfuerzo: M–L.

**[I12] Front: estado de servidor manual y errores tragados** · Arquitectura front
- `useState+useEffect` por pantalla (Matches, RadarMatches, Chat, Profile, Swipe); `ProfileScreen.tsx:554-607` hace fetch crudo a Spotify con polling 30 s aunque la tab no esté enfocada; `apiRequest` sin timeout (cold start Render); 31 `catch {}` vacíos; `formatApiError` muestra `API 500: <body>`.
- Fix: TanStack Query, `AbortSignal.timeout(15000)`, errores con copy humano. Esfuerzo: M.

**[I13] Contador y umbral de swipes inconsistentes** · Dominio · *Duplicated source of truth*
- Front `RADAR_SWIPES_THRESHOLD=100` (`config/onboarding.ts:5`, `useSwipes.ts:5`) vs back 25 (×4) vs copy "Completá 25 swipes"; contador local en AsyncStorage por dispositivo (`useSwipeEngine.ts:10`), con closure obsoleto; `getMe().swipeCount` ignorado.
- Fix: el back expone `swipeCount` y `radarThreshold` en `/User/me`; el front no cuenta. Esfuerzo: S–M.

**[I14] Estado del dispositivo se filtra entre usuarios** · Privacidad · Verificado
- `logout` no borra `@encorely_swipe_state`, `encorely_user_profile` (edad, género, bio), toggles ni onboarding; el parche local marca onboarding completo a nivel dispositivo.
- Fix: claves namespaced por `userId` + `clearUserScopedStorage()`. Esfuerzo: S.

**[I15] Tokens en localStorage en web** · Seguridad · Verificado
- `secureStorage.web.ts:5-16` → AsyncStorage → localStorage para JWT y refresh de Spotify; `vercel.json` sin CSP.
- Fix: JWT en memoria + refresh en cookie `HttpOnly; Secure; SameSite=Strict` vía backend; CSP. Esfuerzo: M.

**[I16] Accesibilidad por debajo de WCAG 2.2 AA** · UI/UX · Verificado (contrastes calculados)
- Swipe solo por gesto, sin botones alternativos (2.5.7); ~8 props de a11y frente a 84 `TouchableOpacity`; botones de icono sin `accessibilityLabel`; tab bar a 10 px; 0 usos de `useReducedMotion`; autoplay de audio.

| Texto / fondo | Ratio | Dónde |
|---|---|---|
| blanco / `#1DB954` | 2.59 | botón Spotify (`LoginScreen`, `SpotifyConnectCard`) |
| blanco / `#F366FF` | 2.57 | burbuja propia del chat, "Conectar" |
| `#4B4B6A` / `#0A0A0F` | 2.36 | disclaimer del login |
| `rgba(255,255,255,.22)` | 2.04 | placeholders Settings |
| `rgba(255,255,255,.35)` | 3.23 | inputs onboarding |

- Fix: botones ✕/♥ + `accessibilityActions`; texto negro sobre verde Spotify; `useReducedMotion()`. Esfuerzo: M.

**[I17] Design system inexistente y estética genérica "hecha con IA"** · UI/Marca · *No design tokens / Template aesthetic*
- `designTokens.ts` (fondo `#181818`, primario magenta `#F366FF`) vs `loginTheme.ts` (fondo `#0A0A0F`, primario rojo `#EF4444`, acento violeta): el login es otra marca. `#F366FF` hardcodeado 81 veces; sin escala de spacing; styled-components (20 archivos) + StyleSheet (7) + inline; flash por fondos distintos. Emojis como iconos (🎵⚡🎯📡🌍⚙️), gradiente morado/magenta con glow, copy con jerga ("Similitud coseno >70% = match garantizado"), mezcla de voseo y tuteo para público colombiano.
- Fix: dirección visual propia (ver reporte de diseño) y un solo tema en tokens. Esfuerzo: L, gradual.

**[I18] Acción destructiva sin confirmación y debug en producción** · UX (Nielsen #5) · Verificado
- El icono "refresh" de Discover ejecuta `resetSwipes` (`SwipeScreen.tsx:304`); botón "⚙️ Reset Swipes (Debug)" visible (`:278-282`).
- Fix: `if (__DEV__)` + confirmación. Esfuerzo: S.

**[I19] Calidad estática del front ausente** · Tooling · Verificado
- `tsc` 85 errores (83 TS7031 por `@types/styled-components-react-native` v5 junto a styled-components v6); sin ESLint (hooks dentro de `.map` en `SwipeCard.tsx:141`, deps faltantes en `LoginScreen.tsx:92-105`); sin scripts `test`/`lint`/`typecheck`; SonarCloud Quality Gate fallido en `ff11541`.
- Fix: quitar `@types/styled-components-react-native`, `eslint-config-expo` + Prettier + scripts + CI. Esfuerzo: S–M.

**[I20] Integración con APIs deprecadas de Spotify** · Integración · Sospecha fundada
- `/recommendations` (`swipeFeed.ts:17-41`) y `/audio-features` (`spotifyApi.ts:197`, `SpotifyService.cs:26`) devuelven 403 para apps nuevas desde nov-2024; `preview_url` suele ser null. El "Vector de Vibe" de respaldo es inventado (`danceability = explicit ? 0.75 : 0.45`, `spotifyApi.ts:250-262`) y nunca se envía al backend.
- Fix: el feed lo sirve el backend; derivar el perfil de géneros/artistas (`/me/top`), no de audio-features. Esfuerzo: M.

**[I21] DX: no se levanta siguiendo el README** · DX · *Docs drift* · Verificado (vivido en esta sesión)
- El compose arranca antes de crear `.env`; Postgres sin SSL vs `SSL Mode=Require`; `dotnet ef database update` no existe; sin Redis ni Worker en compose; sin `launchSettings.json` (arranca en Production y CORS bloquea `:8081`); pide .NET 9; BD vacía. `pnpm` borra `node_modules` instalados con npm.
- Fix: README como runbook de 5 comandos; compose único con profiles; `launchSettings.json`; script de certificados o `DB_SSL_MODE`. Esfuerzo: M.

**[I22] Documentación describe otro sistema** · Docs · *Aspirational docs*
- README/PROJECT_STATUS: .NET 9, EF Core, "Distancia Euclidiana" (es coseno), "sin tests" (hay 52 en total), "no hay pendientes críticos"; badge MIT sin `LICENSE`; `endpoints.md` omite 5 endpoints.
- Fix: README runbook, endpoints generados desde Swagger, PROJECT_STATUS → issues. Esfuerzo: M.

**[I23] Configuración con varias fuentes de verdad** · 12-factor III · *Config sprawl*
- Connection string ×3 (una muerta en `Program.cs:37`); DI duplicado Api/Worker; Redis 6379 fijo sin auth; `DotNetEnv.TraversePath()` también en prod; front: URL del API desde env, `app.json.extra` y `PROD_DEFAULT`. Variables usadas y no documentadas: `DB_PORT`, `REDIS_HOST`, `ALLOWED_ORIGINS`, `JWT_*`, `GOOGLE_APPLICATION_CREDENTIALS`, `EXPO_PUBLIC_API_URL`; documentadas y no usadas: `GET_SPOTIFY_CLIENT_ID/SECRET`.
- Fix: `IOptions<T>` + `ValidateOnStart`, extensión `AddEncorelyPersistence()`, `.env.example` completo en cada repo. Esfuerzo: M.

**[I24] Diseño del esquema** · BD · *Implicit state / Magic value*
- Matches sin `Status`; `Swipes` sin `UNIQUE(UserId,TrackId)` (Django sí lo tenía); `Users.SpotifyId` guarda el email (debería ser `ProviderId`); `UNIQUE(Email)` sensible a mayúsculas; `VenueRooms.EventId` sin tabla `Events`; identificadores PascalCase entre comillas heredados de EF.
- Fix:
  ```sql
  ALTER TABLE "Matches" ADD "Status" smallint NOT NULL DEFAULT 0 CHECK ("Status" IN (0,1,2));
  CREATE UNIQUE INDEX ON "Swipes"("UserId","TrackId");
  CREATE UNIQUE INDEX ON "Users"(lower("Email"));
  ```
  Esfuerzo: M.

**[I25] CI mínimo o ausente** · CI/CD
- Back: `dotnet.yml` con SDK 9.0.x, sin `docker build`, sin format, no corre en `dev`. Front y landing sin CI. Vercel sin deploy desde `14c49bc` (05-29), probablemente por `--frozen-lockfile` con pnpm-lock desincronizado.
- Fix: SDK alineado + `docker build` + `dotnet format --verify-no-changes`; workflow front con `tsc`, `lint`, `jest`. Esfuerzo: S.

**[I26] Lockfiles y gestor de paquetes del front** · Build · Verificado
- `package-lock.json` + `pnpm-lock.yaml` (del 04-04, sin jest/testing-library); `.npmrc` con `prefix=/home/edav404/.npm-global` e `ignore-scripts=true`; `victory-native` y `lottie-react-native` sin uso; `expo-av` deprecado (se elimina en SDK 55).
- Fix: `"packageManager": "pnpm@9.x"`, un solo lock, sin `.npmrc` personal, desinstalar lo que no se usa, migrar a `expo-audio`. Esfuerzo: S.

**[I27] Ramas y ownership divergentes** · Organización
- Front: `codex/actualizar-frontend` (Expo 57, **2026-10-14 00:18 UTC**) en paralelo a cambios locales sin commit en `LoginScreen.tsx`, `AuthContext.tsx`, `http.ts`. Landing: 10 commits en `feat/reconstruction` sin merge y 1 sin push; repo en cuenta personal. Vercel en cuenta personal `jdicans`.
- Fix: acordar la rama base del front **antes** de reconstruir; transferir la landing y Vercel a la org. Esfuerzo: S.

**[I28] Infra decorativa** · IaC · *Cargo-cult IaC*
- k8s/terraform de ejercicio académico ("Tarea 82/83"): imagen con mayúsculas (inválida), secret no definido, ECS mencionado sin recursos, sin Kafka/Redis; el deploy real no tiene IaC.
- Fix: mover a `docs/academic/` y añadir `render.yaml`. Esfuerzo: S.

**[I29] Tests que no protegen** · Calidad · *Test theater*
- 28 tests back con mocks: ninguno de repositorios/SQL, autorización, hubs ni integración; `AuthServiceTests` prueba código muerto. 24 tests front sin NavigationGuard, AuthContext ni pantallas. Sin medición de cobertura.
- Fix: Testcontainers + `01_schema.sql`; tests de IDOR; tests de contrato front↔back. Esfuerzo: M.

**[I30] Plataforma .NET 8 a punto de EOL** · Dependencias
- .NET 8 termina soporte el **10-nov-2026** (menos de un mes); desactualizados Confluent.Kafka 2.3→2.16, Dapper 2.1.28→2.1.89, Swashbuckle 6.5→10.x, JWT 8.3→8.23, FirebaseAdmin 3.0.1→3.7.
- Fix: si se reconstruye, hacerlo directamente sobre .NET 10 LTS. Esfuerzo: M.

### Menores

- **[M1] Fósiles en git:** 111 archivos en `*/obj_old/Debug/net9.0`; `Directory.Build.props` los excluye en vez de eliminarlos; `__pycache__` en EncorelyPython; `EntityFrameworkCore.InMemory` en tests sin EF; `.sln` con GUIDs inválidos (letras G–K).
- **[M2] Código muerto:** back `IAuthService/AuthService`, `INotificationClient`, `GetPrincipalFromExpiredToken`, `ISwipeQueries`, `FrontendDtos.cs` (segundo contrato), `if IsDevelopment {}` vacío; front `HomeScreen`, `OnboardingScreen`, `useSwipes`, `useMatches` (stub), `useSpotifyAuth`, `getNextTrack`/`updateSettings`/`registerWithEmail`/`authWithGoogle` sin uso, `app/(auth)/onboarding.tsx`.
- **[M3] Magic numbers:** 25, 0.70, 0.85/85.0, 30 min, 7 días, perfil por defecto 0.5/120, `LIMIT 500`, 6379.
- **[M4] Naming:** typos `encorly_back`, `Encorly-landing`, `encorly-back.onrender.com`; mezcla español/inglés (`Usuario` / `Users`); `AuthProvider.Custom` documentado como "Email".
- **[M5] Avatares de `i.pravatar.cc`:** envían IDs de usuario/match a un tercero y muestran caras falsas en una app de citas.
- **[M6] Chats inalcanzables tras recargar:** `openedRooms` solo en memoria (`MatchesScreen.tsx:22`); el chat no tiene polling ni tiempo real.
- **[M7] Copy:** jerga técnica, voseo mezclado, "Vibe Explorer"/"Encorely User" hardcodeados; no hay términos ni política de privacidad aunque se piden edad y género (Ley 1581 de 2012, Colombia).
- **[M8] Tab bar flotante** tapa el último ítem en Matches/Radar (sospecha); providers sin memoizar; sonido huérfano si la tarjeta se desmonta (`SwipeCard.tsx:197-238`).
- **[M9] Landing:** token de Cloudflare Analytics es placeholder (`index.html:57`); honeypot solo en cliente; `subscribe.ts` sin rate limit y fuera del `include` de tsconfig; README no menciona `netlify dev`.
- **[M10] Django:** `SECRET_KEY` con fallback, `DEBUG=True` por defecto, `pytest.ini` sin pytest en requirements → archivar.
- **[M11] Sin pin de runtimes** (`global.json`, `.mise.toml`, `engines`, `packageManager`); `.gitignore` del front no ignora `.env`; no hay `.env.example` en el front.

---

## 5. Redundancias

| Qué se duplica | Dónde | Propuesta |
|---|---|---|
| Fábrica de conexiones / connection string | `EncorelyQuery/DbConnectionFactory.cs`, `EncorelyRepository/DbConnectionFactory.cs`, `Program.cs:37` (muerta) | Una `IDbConnectionFactory` + `IOptions<DbOptions>` |
| Defaults de JWT | `Program.cs:132-134`, `TokenService.cs:20-22` | `JwtOptions` validadas al arranque |
| Bootstrap de Kafka | `Program.cs:79-86`, `KafkaProducer.cs:19`, `KafkaConsumerWorker.cs:43` | `KafkaOptions` única |
| Interfaces de productor | `IKafkaProducer<T>`, `IEventProducer<T>` | Una sola |
| Matchmaking | `EventsController.cs:66-83`, `KafkaConsumerWorker.cs:159-173` | `MatchmakingService` |
| Umbral de swipes | back ×4 (25), front ×2 (100), copy (25), Python (100) | `/User/me` expone `radarThreshold` |
| Emisión de tokens | `IdentityService.cs:74-87` y `:146-159` | Método único |
| Registro de DI | `EncorelyApi/Program.cs`, `EncorelyWorker/Program.cs` | `AddEncorelyPersistence()` |
| Domain vs Models | `EncorelyDomain` vacío, `EncorelyModels` con namespace `EncorelyDomain.Events` | Un proyecto `Encorely.Domain` real |
| Contrato API | `FrontendDtos.cs` (sin uso), `Requests.cs`, `types.ts` manual | OpenAPI → cliente generado |
| Sesión en el front | `AuthContext.user`, `SpotifyAuthContext.user`, `backendUserId`, `SESSION_USER_KEY` | `SessionContext` único |
| Contador de swipes | `useSwipeEngine`, `useSwipes` (muerto), backend `SwipeCount` | Solo backend |
| Tema / estilos | `designTokens.ts`, `loginTheme.ts`, más de 80 hex; styled-components + StyleSheet + inline | Un tema de tokens + StyleSheet |
| Fetch a Spotify | `spotifyApi.ts`, `swipeFeed.ts`, `ProfileScreen.tsx` | Un solo cliente (idealmente en el back) |
| Cálculo de vibe + `setSession` | `LoginScreen`, `SpotifyConnectCard`, `SettingsScreen`, `OnboardingScreen` (muerto) | Un hook |
| Lockfiles | `package-lock.json`, `pnpm-lock.yaml` | Uno |
| Compose | `docker-compose.yml`, `docker-compose.local.yml` | Uno con profiles |
| Dominio completo | .NET, Django, Python | Archivar Django y Python |

---

## 6. Quick wins vs mejoras estratégicas

**Quick wins** (orden sugerido, cada uno ≤ 1 día):
1. Rotar el password de Supabase y activar secret scanning (C4).
2. Quitar los fallbacks de secretos y validar config al arranque (C5).
3. Commitear el parche de DTOs (C6) y corregir `body: { content }` en el chat (C11).
4. Dockerfile a `:8.0` + `docker build` en CI (C7).
5. Npgsql ≥ 8.0.3, quitar SignalR.Core (C17).
6. Quitar `MapHub` de los hubs sin consumidor (C16).
7. Ocultar los controles placebo, las cifras fake y el botón de debug (C15, I18).
8. Adaptador de `confirm()` para web (C14) y salida del spinner sin Spotify (C13).
9. Un solo lockfile, borrar `.npmrc`, quitar `@types/styled-components-react-native` (I26, I19).

**Estratégicas** (en orden):
1. **Contrato y autorización:** `ApiControllerBase.CurrentUserId`, DTOs sin `userId`, OpenAPI → cliente TS generado (C1, C2, I-contratos).
2. **Esquema versionado:** DbUp + baseline + Testcontainers (C9, C10, I24, I29).
3. **Auth completa:** refresh con rotación, Google validado, vinculación por provider, rate limiting (C3, C12, I8).
4. **Dominio y capas:** `MatchmakingService`, `Status` en Matches, afinidad corregida, interfaces hacia adentro (I1–I5).
5. **Mensajería confiable:** outbox + DLQ + commit manual (C8, I6).
6. **Front:** `SessionContext` + `Stack.Protected`, TanStack Query, estados completos (I11–I14).
7. **Identidad visual y design system** desde cero (I16, I17; ver reporte de diseño).
8. **Plataforma:** .NET 10 LTS, compose único, runbook, CI en los 3 repos, archivar Django/Python (I21–I30).

---

## 7. Lo que está bien

- SQL 100 % parametrizado con Dapper; `IncrementSwipeCountAsync` atómico (`UPDATE … RETURNING`).
- BCrypt para passwords; refresh token generado con RNG criptográfico; JWT valida issuer, audience, firma y lifetime.
- `[Authorize]` en todos los controllers que no son de auth; CORS con orígenes explícitos; el stackTrace solo se expone en Development.
- Dockerfile multi-stage con usuario no root; `.dockerignore` razonable; `.env` nunca commiteado.
- Front: PKCE S256 sin client secret; `apiRequest`/`ApiError` centralizados y testeados; swipe con Reanimated 4 + Gesture Handler en worklets; `strict: true`; rutas delgadas que reexportan módulos.
- Hay CI en el back y los 52 tests (28 + 24) pasan.
- Landing: la función de Netlify guarda la key en el servidor y es idempotente.
- Django tenía migraciones y constraints correctas (`unique_together`): referencia útil para el nuevo esquema.

---

## 8. Fuera de alcance / no verificado

- Configuración real de Render, Vercel y Netlify (variables, si Render usa Docker, si hay Kafka/Redis/Worker en producción).
- Si el password de Supabase de `955db69` ya fue rotado; si Supabase tiene RLS y las columnas que usa Dapper.
- Comportamiento real de los endpoints deprecados de Spotify con este client ID.
- Build real de la imagen Docker y del deploy de Vercel (inferido por análisis y fechas).
- Contenido de la rama `codex/actualizar-frontend` (Expo 57): no auditada.
- La UI se juzgó por código + capturas de la sesión local; la validación dinámica la cubren `/enjambre-qa` y la auditoría de diseño.
- Pentest, carga y rendimiento real.
