# 07 · Conciliación del back de Encorely · 2026-10-14

**Alcance:** `encorly_back`, rama `rebuild/v1` @ `c0f4dbb` (.NET 10). **Modo:** solo lectura. El único archivo creado es este. **Insumos:** auditoría externa de hoy, enjambre QA de hoy (solo API) y los reportes del legado (00, 01, 02, 05, 06 y el QA `2026-10-13-2104`). **Verificación propia:** releí en el código actual cada hallazgo crítico e importante, y también los 32 menores. Además hice un GET a `http://127.0.0.1:5100/health/ready`, que respondió `200 Healthy`. No envié ninguna petición que escriba.

**Convención de rutas:** salvo que diga otra cosa, las rutas son relativas a `src/Encorely.Api/`.

---

## 1. Resumen ejecutivo

**Estado real:** el back reconstruido es otro sistema. De los 13 críticos del legado que tocan al back, 8 están resueltos con test que lo prueba, 1 dejó de aplicar por alcance (D10), 2 quedan parciales y 2 reaparecen de otra forma pero ya no son críticos. Hoy queda **1 crítico, y es funcional:** la cola de moderación del panel no carga (FIN-01). No es de seguridad y se corrige con una línea. El resto son 14 importantes, casi todos de esfuerzo S, y 32 menores.

**Veredicto.** **Listo para demo**, siempre que antes se corrija FIN-01 si la demo muestra la pestaña de reportes del panel. Los flujos de la app (registro, swipes, ADN, eventos, radar, match y chat E2EE) pasaron en el QA sin IDOR y con 120 OK. **No está listo para desplegar:** antes hay que cerrar FIN-02, FIN-03, FIN-06, FIN-07, FIN-08 y FIN-09. Son unos 4 o 5 PRs pequeños (§8).

**Los 5 que más pesan:**
1. **FIN-01:** `GET /admin/reports?status=open` responde 400, así que no se pueden ver ni resolver reportes desde el panel.
2. **FIN-02:** cerrar o revocar una sesión no invalida el access token ni corta el hub durante 15 min. El caso "teléfono perdido" queda abierto.
3. **FIN-03:** el seed demo no tiene guarda de entorno y siembra `admin@` con la misma contraseña que `revisor@`, cuenta que se entrega a terceros.
4. **FIN-04:** reutilizar el id de un mensaje ya confirmado deja la evidencia de franking sin verificar. Es un bypass de la moderación.
5. **FIN-05:** el ban no borra los likes del par. Al reactivar la cuenta, el match se rehace con un solo like nuevo.

**Antes y después:**
- Críticos: legado 17 (01) / 8 (02) / 15 consolidados (00); hoy **1**.
- Hallazgos del back del legado (26): 12 resueltos, 6 parciales, 6 reaparecen distinto (ninguno crítico), 2 no aplican, **0 abiertos**.
- §7.5 de 00 (ASVS L1): 1 de 7 áreas aplicables cumple completa, 6 parciales, 0 sin cumplir.

---

## 2. Fuentes y método

| Reporte | Qué cubre | Hallazgos | Nivel de verificación |
|---|---|---|---|
| `encorly_back/AUDITORIA-2026-10-14.md` (AUD) | Repo completo: arquitectura, seguridad, privacidad, deps, DX, CI, ops, contrato | 0 C · 12 I · 22 M | **Estático** + `dotnet build` (0 warnings), `dotnet test` (238/238) y `dotnet list package --outdated/--deprecated/--vulnerable` |
| `encorly_back/qa-enjambre/2026-10-14-0939/` (QA: `informe.md` + `datos.json`) | Solo API: 4 personas, 2 de seguridad y 3 verificadores sobre una instancia local aislada (Release, Development, BD `encorely_qa`) | 1 C · 11 I · 19 M confirmados (31 grupos de 67 crudos) · 4 descartados · 120 OK | **Dinámico**. No probó UI, el rate limit real (estaba en 400/min), producción, push real, expiraciones ni más de un admin |
| `reportes/00-reporte-general.md` (+ 01, 02, 05, 06 y `qa-enjambre/2026-10-13-2104`) | Legado: GEN-01…37, §7.5 seguridad mínima, decisiones D1–D16 | 01: 17 C · 30 I · 11 M; 02: 8 C · 27 I · 16 M; 00: 15 GEN críticos | Lo que dicen esos reportes. Contrasté cada GEN contra el código **nuevo** |
| Este informe | Conciliación y verificación | 47 FIN · 4 descartes | **Estático propio** sobre `c0f4dbb`: código, tests, config y git. Más un GET a `/health/ready` |

**Convención de IDs:**
- `FIN-xx` es un hallazgo conciliado.
- Los orígenes llevan prefijo: `AUD-I1` es el importante 1 de la auditoría. `QA-S1`, `QA-F1` y `QA-M1` son los grupos del enjambre, por su `grupo` en `datos.json`. En `informe.md` aparecen renumerados (C1, I1…, M1…).
- Los M* del QA (chat y respaldo) no tienen relación con los M* de la auditoría (menores).
- **Método:** `estático` (solo AUD), `dinámico` (solo QA) o `ambos`. La columna "Verificado hoy" indica mi relectura del código actual.

---

## 3. Contradicciones resueltas

| # | Contradicción | Resolución | Base |
|---|---|---|---|
| 1 | AUD: "ningún crítico verificado". QA: 1 crítico (QA-F1) | **FIN-01 es crítico.** La auditoría no cruzó el JS del panel con el binder de Minimal APIs, y la suite tampoco lo cubre: el único test llama a `/admin/reports` **sin** `?status`. Calibra el método: el estático no ve fallos de binding en runtime | `Features/Admin/AdminEndpoints.cs:72-74`, `AdminPanelPage.cs:193,638`, `tests/…/AdminTests.cs:171` |
| 2 | AUD-I1 importante vs QA-S2 menor (lockout) | **Importante, por el DoS y no por el oráculo.** El oráculo aporta poco porque el 409 del registro ya enumera (riesgo aceptado en ARCHITECTURE §6). Pero en una app de citas el acosador **sí** conoce el correo de la víctima: con 5 peticiones cada 15 min, desde cualquier IP, le bloquea el login **y** el borrado web por tiempo indefinido. Además contradice la línea de §6 "mismo tiempo y mismo 401" | `Features/Auth/AuthService.cs:87-93,149-158`, `Features/Legal/LegalService.cs:22`, `ARCHITECTURE.md` §6 |
| 3 | AUD-I4 importante vs QA-S5 menor (sin tope de body) | **Importante para desplegar, no bloquea la demo.** El QA midió CPU (0,06 s por 25 MB), pero no memoria con concurrencia. `refresh` y `logout` son anónimos y no tienen rate limit, así que unas 20 peticiones de 28 MB en paralelo agotan una instancia `starter` | `Program.cs:35` (sin `ConfigureKestrel`; grep de `MaxRequestBodySize/RequestSizeLimit` vacío), `Features/Auth/AuthEndpoints.cs:18-21` |
| 4 | AUD-I5 importante vs QA-F14 menor (resolver un reporte no es atómico) | **Menor.** Hacen falta dos moderadores en el mismo milisegundo y el seed tiene un solo admin. Un doble clic repite la misma acción, que es inocua. Se arregla en el mismo PR de moderación (§8) | `Features/Admin/AdminService.cs:99-115` |
| 5 | AUD-M1 menor vs QA-M1 importante (500 en el primer `PUT /me/backup` concurrente) | **Menor.** No pierde datos (una escritura gana), solo pasa en la primera subida y el reintento del cliente lo resuelve. Lo grave es que ese 500 no se ve en el log de acceso, y eso queda en FIN-09, que es importante | `Features/Sync/SyncService.cs:19-33` |
| 6 | AUD-M3 menor ("ventana de gracia del refresh") vs QA-F10 descartado | **Descartado.** Es la detección de reutilización documentada (OWASP). Lo que corresponde es hacer el refresh *single-flight* en el cliente | `AuthService.cs:107-112,122-128`, `tests/…/AuthTests.cs:196`, ARCHITECTURE §6 |
| 7 | AUD-I7(a) importante vs QA-S6 menor; AUD-M7 y QA-S7 menores | **Se agrupan en FIN-07, importante.** La causa raíz es una sola: la configuración de producción no se valida al arrancar. Una parte (la trampa del comentario en `.env.example`) ya afecta el runbook de hoy, no solo un despliegue futuro | §5, FIN-07 |
| 8 | QA-S9: el agente dijo "0 respuestas 500"; el verificador encontró 7 en el log | **Gana la verificación, y la confirmo en código.** `UseSafeRequestLogging` está dentro de `UseExceptionHandler`, así que su `finally` lee el 200 por defecto antes de que se escriba el 500. AUD-I10 vio que faltaba el `traceId` pero no este 200. Ambos se unen en FIN-09 | `Program.cs:146` vs `:150`, `Infrastructure/Http/SecurityHeaders.cs:30-43` |
| 9 | AUD-I11: "la CI no corre en `dev`" | **Es correcto a medias, así que baja a menor.** `pull_request` no tiene filtro de rama, por lo que los PR hacia `dev` **sí** se prueban. Solo quedan fuera los push directos. Lo que sigue pendiente es format, el chequeo de vulnerables, dependabot y el smoke test | `.github/workflows/ci.yml:3-6`; `origin/dev` existe |
| 10 | AUD-I12 importante (versión del contrato fija) | **Menor.** El contrato está versionado en git, tiene un test que exige que esté al día y un CHANGELOG por draft. La versión `1.0.0` del documento es cosmética mientras el front no fije el cliente por ese campo | `Infrastructure/OpenApiSetup.cs:26`, `contracts/CHANGELOG.md:6`, `tests/…/PlatformTests.cs:98` |
| 11 | QA-M6: el agente lo clasificó menor y el verificador lo subió a importante | **Importante, lo confirmo.** El reporte busca primero el mensaje vivo y, si el ack ya lo borró, cae a `message_franking_records`, que guarda el tag **del primer** mensaje (`ON CONFLICT DO NOTHING`). En el flujo normal (el receptor confirma al recibir), el segundo mensaje sale `verified=false` | `Features/Chat/ChatService.cs:24,63-70,107-109,117-125`, `Features/Moderation/ModerationService.cs:103-124` |
| 12 | AUD-I9 (cierre de sesión en 4 sitios) como importante independiente | **Se integra en FIN-02 como vehículo del fix.** Su impacto solo aparece cuando cambia la regla, y el fix de FIN-02 es justo ese cambio | `Features/Sessions/SessionEndpoints.cs:42-58`, `AuthService.cs:162-166`, `AdminService.cs:147,156` |

**Calibración del método:**
- **Solo el QA** encontró 1 crítico y 6 importantes (FIN-01, 04, 05, 10, 12, 14, 15). Son fallos de comportamiento (binding, idempotencia tras el ack, ciclo ban→reactivar, mapeo de datos) que la lectura estática y la suite no vieron.
- **Solo la auditoría** encontró 1 importante (FIN-11, retención) y 17 menores de ops, CI, imagen, índices y deuda, que el QA no puede ver desde la API.
- **Los dos** coinciden en 12 FIN, y esos son los de mayor confianza.

---

## 4. Scorecard por dimensión (hoy vs legado)

| Dimensión | Legado (00 §3) | Hoy | Una línea |
|---|---|---|---|
| Arquitectura del back | 🔴 | 🟢 | Monolito modular de un proyecto, `Features/*` + `Infrastructure/*`, transacciones explícitas. Quedan redundancias menores (AUD §5) |
| Dominio (ADN, radar, match) | 🔴 | 🟡 | El núcleo funciona y tiene tests. FIN-05 (rematch tras reactivar) rompe el invariante de consentimiento; FIN-15 y FIN-22 son de calidad |
| Auth / identidad / sesión | 🔴 | 🟡 | Identidad solo desde `sub`, refresh rotativo, lockout. FIN-02 (revocación) y FIN-06 (oráculo y DoS) |
| Chat E2EE y mensajería | 🔴 | 🟡 | Ciphertext only con test canario y hub autenticado. FIN-04 (bypass de franking) y FIN-12 (sin cuota ni expiración) |
| Esquema / BD | 🔴 | 🟢 | Migraciones EF desde el día 0, constraints de dominio y Postgres real en los tests. Faltan índices (FIN-29) |
| Contrato front↔back | 🔴 | 🟡 | OpenAPI generado con test. Pero FIN-01 es un contrato que promete valores que el binder rechaza |
| Moderación / admin | 🔴 (salas) | 🔴 | FIN-01 (crítico), FIN-05, FIN-10, FIN-14, FIN-17 y FIN-21. Es la zona más débil hoy |
| Privacidad (Ley 1581) | 🔴 | 🟡 | Export, borrado (app y web), consentimiento y 18+. FIN-11 (retención de evidencia) contradice la política publicada |
| Seguridad transversal | 🔴 | 🟡 | ASVS L1 casi completo (§7.2). Faltan cotas (FIN-08) y validación de arranque (FIN-07) |
| Tests | 🔴 | 🟢 | 238 tests contra Postgres real, con IDOR, concurrencia, canario y contrato. Hueco: binding de query (FIN-01) |
| Plataforma / dependencias | 🔴 | 🟢 | .NET 10 y EF Core 10 al día, sin CVE. Sin dependabot (FIN-38) |
| DX / onboarding | 🔴 | 🟡 | README como runbook con mise y compose por perfiles. FIN-07(b, c) rompe la opción B y puede romper la A |
| CI/CD | 🔴 | 🟡 | Build con warnings como errores, tests y `docker build`. Sin format, vulnerables, dependabot ni smoke test (FIN-38) |
| Operación / observabilidad | 🔴 | 🟡 | Health checks correctos. FIN-09: 500 registrado como 200, sin JSON ni `traceId` |

---

## 5. Hallazgos conciliados priorizados

### 5.1 Crítico

**[FIN-01] La cola de moderación del panel nunca carga: `GET /admin/reports?status=open|resolved|dismissed` responde 400**
- **Orígenes:** QA-F1 · **Método:** dinámico (QA) · **Verificado hoy:** sí.
  - `Features/Admin/AdminEndpoints.cs:72-74`: el parámetro es el enum `ReportStatus`. El binder usa `Enum.TryParse`, que distingue mayúsculas, así que acepta `Open` pero no `open`.
  - `Program.cs:203`: el `JsonStringEnumConverter` camelCase solo aplica al body. El contrato anuncia `[open, resolved, dismissed]`.
  - `Features/Admin/AdminPanelPage.cs:193,638`: el panel siempre manda el valor en minúsculas.
  - `tests/…/AdminTests.cs:171`: el test llama sin `?status`, por eso no lo detectó.
- **Impacto:** el admin no puede ver ni resolver reportes de acoso desde la única UI de moderación. En una app de citas, la moderación es un flujo de seguridad del usuario y no un extra. No expone datos.
- **Fix** (de QA-F1, más el test que faltaba):
  ```csharp
  private static async Task<Ok<IReadOnlyList<AdminReportDto>>> ListReports(AdminService admin, CancellationToken ct,
      [Description("open | resolved | dismissed; por defecto open")] string? status = null)
  {
      var parsed = ReportStatus.Open;
      if (status is not null && (int.TryParse(status, out _) || !Enum.TryParse(status, ignoreCase: true, out parsed)))
          throw ApiException.BadRequest(ErrorCodes.InvalidBody, "La solicitud no es válida.", "status: usa open, resolved o dismissed.");
      return TypedResults.Ok(await admin.ListReportsAsync(parsed, ct));
  }
  // + test: GET /admin/reports?status=open|resolved|dismissed => 200, y ?status=1 => 400
  ```
  Hay que revisar si otro endpoint recibe un enum por query (hoy solo este). El `ThrowOnBadRequest=false` de `Program.cs:139` explica por qué el 400 sale genérico (FIN-20).
- **Esfuerzo:** S.

### 5.2 Importantes

**[FIN-02] Cerrar o revocar una sesión no invalida el access token vigente ni corta el hub (hasta 15 min)**
- **Orígenes:** AUD-I2, QA-S1, AUD-I9 · **Método:** ambos · **Verificado hoy:** sí.
  - `Infrastructure/Auth/AuthSetup.cs:62-79`: `UseActiveUserCheck` solo mira si la cuenta existe y si está baneada; nunca valida el `sid`, aunque `TokenService.cs:31` lo emite.
  - `Features/Sessions/SessionEndpoints.cs:42-58` y `AuthService.cs:138-143,162-166`: revocan la familia y los push tokens, pero no el JWT.
  - `Infrastructure/Realtime/HubConnections.cs:18`: `Abort` existe solo por `userId` y nadie lo llama al revocar.
  - El cierre de sesión está implementado en 4 sitios: `SessionEndpoints.cs:45-48,55-56`, `AuthService.cs:162-166` y `AdminService.cs:147,156`.
- **Impacto:** el caso de uso declarado del endpoint ("p. ej. un teléfono perdido") deja 15 min para leer `/me/export` y `/me/backup`, recibir mensajes por el hub y borrar la cuenta (FIN-19). La V3 de §7.5 queda incompleta.
- **Fix:** un `SessionRevoker` único, usado en los 4 sitios, más la validación del `sid` en el middleware, que ya consulta la BD en cada request.
  ```csharp
  // AuthSetup.UseActiveUserCheck, junto a la consulta de status
  var sid = ctx.User.SessionId();
  var sessionOpen = sid is null || await db.RefreshTokens.AnyAsync(t =>
      t.FamilyId == sid && t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now, ctx.RequestAborted);
  if (status is null || !sessionOpen) { /* 401 session_revoked */ }
  // HubConnections.Abort(Guid userId, Guid? sessionId): filtrar por el claim sid del HubCallerContext.User
  ```
  Hay índice por `FamilyId` (`Infrastructure/Database/AppDbContext.cs:73`). Las dos consultas se pueden unir en una.
- **Esfuerzo:** S/M.

**[FIN-03] El seed demo corre sin guarda de entorno, y `admin@encorely.demo` comparte la contraseña con `revisor@`**
- **Orígenes:** AUD-I6, QA-S4 · **Método:** ambos · **Verificado hoy:** sí.
  - `Data/Seeder.cs:42-46`: siembra si `DemoUserPassword` tiene valor, sin mirar el entorno.
  - `Data/Seeder.cs:141,149,154`: aplica un solo hash a todas las cuentas y asigna el rol desde el JSON.
  - `Data/Seed/demo-users.json:3,21`: el admin está en el JSON y su correo es público en el repo.
  - `Program.cs:195`: `PrepareDatabaseAsync` corre siempre.
  - `docs/privacidad-y-tiendas.md:63`: obliga a sembrar al revisor en el back que revisan las tiendas.
- **Impacto:** el día que se publique, quien reciba la clave del revisor (un tercero, en la consola de la tienda) entra al panel admin, ve correos y evidencia de reportes y suspende cuentas. Hoy no hay despliegue (D15), así que no es crítico.
- **Fix:**
  ```csharp
  // Seeder.SeedDemoUsersAsync: fuera de Development/Testing, nunca un admin desde el JSON
  if (!env.IsDevelopment() && !env.IsEnvironment("Testing")) pending = pending.Where(d => !d.Admin).ToList();
  // y en un IValidateOptions<SeedOptions>: Fail si DemoUserPassword tiene valor fuera de Development/Testing
  // salvo SEED_REVIEWER_ONLY=true (siembra solo revisor + su par)
  ```
  El admin de producción se crea a mano (por SQL o CLI) con una clave propia.
- **Esfuerzo:** S.

**[FIN-04] Reutilizar el id de un mensaje ya confirmado crea un duplicado cuya evidencia de franking no verifica**
- **Orígenes:** QA-M6 · **Método:** dinámico (QA) · **Verificado hoy:** sí.
  - `Features/Chat/ChatService.cs:24,117-125`: `ResolveExistingAsync` solo mira `chat_messages`, que el ack vacía (`:107-109`).
  - `ChatService.cs:66-70`: el franking se inserta con `ON CONFLICT (message_id) DO NOTHING`.
  - `Features/Moderation/ModerationService.cs:103-112`: tras el ack, la evidencia se verifica contra el tag guardado, que es el del primer mensaje.
  - El test `SecurityHardeningTests.cs:305` cubre el ack normal, no la reutilización del id.
- **Impacto:** un acosador con un cliente modificado puede enviar contenido nuevo que nadie podrá verificar al reportarlo. Es un bypass de la moderación verificable (ARCHITECTURE §5.5) y también rompe el `seq` y la idempotencia.
- **Fix** (de QA-M6):
  ```csharp
  if (await ResolveExistingAsync(userId, matchId, request, ct) is { } same) return (same, false);
  if (await db.MessageFrankingRecords.AnyAsync(f => f.MessageId == request.Id, ct)) throw IdConflict();
  // y dentro de la tx: var inserted = await db.Database.ExecuteSqlInterpolatedAsync(…ON CONFLICT DO NOTHING…);
  // if (request.FrankingTag is not null && inserted == 0) throw IdConflict();  // carrera
  ```
  Para cubrir también los mensajes sin tag, la alternativa es una tabla ligera `chat_message_ids`, o convertir el ack en tombstone.
- **Esfuerzo:** S.

**[FIN-05] El ban no borra los likes del par: al reactivar, el match se rehace con un solo like**
- **Orígenes:** QA-F13 · **Método:** dinámico (QA) · **Verificado hoy:** sí.
  - `Features/Admin/AdminService.cs:141-158`: cierra los matches con `Banned`, pero no toca `user_likes`.
  - Unmatch (`Features/Matching/MatchingService.cs:118`) y bloqueo (`ModerationService.cs:45`) sí los borran.
  - Al reactivar (`AdminService.cs:135-140`) solo cambian el estado y el veto.
  - `MatchingService.cs:66` encuentra el like recíproco viejo.
- **Impacto:** contradice ARCHITECTURE §4.3 ("volver a hacer match exige dos likes nuevos"). El reactivado puede rehacer el match con quien lo reportó si esa persona no lo había bloqueado. Es contacto sin consentimiento nuevo y es el GEN-08 de vuelta (§7).
- **Fix:**
  ```csharp
  // AdminService.SetUserStatusAsync, rama Banned, dentro de la misma tx
  await db.UserLikes.Where(l => l.FromUserId == userId || l.ToUserId == userId).ExecuteDeleteAsync(ct);
  ```
  Hay que documentar en §6 que reactivar no restaura matches, y agregar un test de ban → reactivar → like que no crea match.
- **Esfuerzo:** S.

**[FIN-06] El bloqueo por intentos funciona como oráculo de cuentas y como palanca para un DoS dirigido**
- **Orígenes:** AUD-I1, QA-S2 · **Método:** ambos · **Verificado hoy:** sí.
  - `Features/Auth/AuthService.cs:87-88`: el 429 se decide con `user?.LockoutEndsAt` **antes** del hash, así que solo existe para cuentas reales.
  - `AuthService.cs:92,149-158`: el contador es por cuenta, sin tener en cuenta la IP.
  - `Features/Legal/LegalService.cs:22`: el borrado web comparte la misma puerta.
- **Impacto:** cualquiera que conozca el correo (en una app de citas, típicamente el acosador) deja a la víctima sin login y sin borrado web de forma indefinida. Contradice ARCHITECTURE §6.
- **Fix** (de AUD-I1):
  ```csharp
  var ok = hasher.Verify(password, user?.PasswordHash);            // siempre hashea
  if (!ok) { if (user is not null) await RegisterFailureAsync(user.Id, now, ct); throw Unauthorized(…); }
  if (user!.LockoutEndsAt > now) throw new ApiException(429, ErrorCodes.AccountLocked, "…"); // solo a quien acierta
  ```
  Contra el DoS: bloquear por la clave (cuenta, IP) o pasar a un backoff progresivo. Lo que se acepte se documenta en §6.
- **Esfuerzo:** S (oráculo) / M (DoS).

**[FIN-07] La configuración de producción no se valida al arrancar, y el runbook tiene dos trampas**
- **Orígenes:** AUD-I7, AUD-M7, QA-S6, QA-S7 · **Método:** ambos · **Verificado hoy:** sí, por lectura. No ejecuté compose.
  - **(a) `FORWARDED_TRUSTED_NETWORKS` vacío:**
    - Detrás de Render, el rate limit de auth particiona por la IP del balanceador (`Program.cs:107-113`), así que bloquea el login de todos los usuarios.
    - HSTS no se emite, porque `Program.cs:144-145` ni siquiera monta `UseForwardedHeaders`.
    - `render.yaml:27-30` solo lo advierte en un comentario, y `Program.cs:41` no valida nada.
  - **(b) La opción B del README no arranca:**
    - `docker-compose.yml:36` fuerza `Production` con `env_file: .env`.
    - `.env.example:12` trae `DB_SSL_MODE=Prefer`.
    - `Infrastructure/Configuration/Options.cs:36-39` rechaza `Prefer` fuera de Development, y `postgres:16-alpine` no tiene TLS.
  - **(c) La contraseña de la BD puede quedar distinta en la API y en Postgres:**
    - `.env.example:8` tiene un comentario inline en `DB_PASSWORD`.
    - `Infrastructure/Configuration/DotEnv.cs:36` no lo corta y Compose sí. Si el dev completa la línea sin borrar el comentario, Postgres y la API quedan con contraseñas distintas. Esto **afecta también la opción A** (BD en compose y `dotnet run`).
  - **(d) `PUBLIC_BASE_URL` vacío:**
    - `Features/Legal/LegalService.cs:14` arma las URLs legales con la cabecera `Host`.
    - `appsettings.json:12` tiene `AllowedHosts: "*"`.
    - `Options.cs:146` (`LegalOptionsValidator`) no exige la variable fuera de Development.
- **Impacto:** cada trampa falla en silencio: (a) y (d) en el primer despliegue; (b) y (c) hoy, al seguir el runbook.
- **Fix:**
  ```csharp
  public sealed class ProxyOptionsValidator(IHostEnvironment env) : IValidateOptions<ProxyOptions>
  {
      public ValidateOptionsResult Validate(string? n, ProxyOptions o) =>
          env.IsDevelopment() || env.IsEnvironment("Testing") || o.ParseNetworks().Count > 0
              ? ValidateOptionsResult.Success
              : ValidateOptionsResult.Fail("FORWARDED_TRUSTED_NETWORKS es obligatorio fuera de desarrollo.");
  }
  // + .ValidateOnStart() en Program.cs:41; mismo patrón en LegalOptionsValidator para PUBLIC_BASE_URL (https)
  ```
  En el perfil `full` de compose, usar `ASPNETCORE_ENVIRONMENT: Development`. En `.env.example`, poner los comentarios en su propia línea, o hacer que `DotEnv` corte en `" #"` los valores sin comillas.
- **Esfuerzo:** S.

**[FIN-08] No hay cotas de tamaño, y `refresh` y `logout` no tienen rate limit**
- **Orígenes:** AUD-I4, AUD-M20, QA-S5 · **Método:** ambos · **Verificado hoy:** sí.
  - No existe `ConfigureKestrel`, `MaxRequestBodySize` ni `RequestSizeLimit` en `src/` (grep vacío), así que rige el default de 30 MB en todos los endpoints, incluidos los anónimos.
  - `Features/Sync/SyncEndpoints.cs:13` declara un 413 que nunca se emite.
  - `Features/Auth/AuthEndpoints.cs:18-21`: `refresh` y `logout` no tienen `RequireRateLimiting`.
  - `UserWrites()` solo está en 7 endpoints. Faltan backup, attendance, push-token, Spotify import y export.
  - `Features/Music/MusicContracts.cs:29` limita la cantidad de géneros, pero no el largo de cada uno.
  - `Features/Sync/SyncContracts.cs:9-10`: `Range(1, long.MaxValue)`.
- **Impacto:** se puede agotar la memoria de una instancia `starter` con pocas peticiones concurrentes y anónimas. No bloquea la demo.
- **Fix:**
  ```csharp
  builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 256 * 1024);
  backup.MapPut("", PutBackup).WithMetadata(new RequestSizeLimitAttribute(15 * 1024 * 1024));
  g.MapPost("/refresh", Refresh).RequireRateLimiting(RateLimitPolicy); // y /logout
  ```
  Además: aplicar `.UserWrites()` a las escrituras caras, `[MaxLength]` por elemento con un atributo de colección, y cotas por algoritmo de KDF (`SyncService.cs:15-17`).
- **Esfuerzo:** S.

**[FIN-09] Observabilidad: el log de acceso registra los 500 como 200, EF ensucia el log y no hay JSON ni `traceId`**
- **Orígenes:** AUD-I10, QA-S9 · **Método:** ambos (S9 reprodujo 4 de 18 PUT concurrentes con 500) · **Verificado hoy:** sí.
  - `Program.cs:146` monta `UseExceptionHandler` por fuera y `:150` monta `UseSafeRequestLogging` por dentro.
  - `Infrastructure/Http/SecurityHeaders.cs:37-42`: el `finally` lee `StatusCode` antes de que se escriba el 500, y no registra `TraceIdentifier`, aunque `Infrastructure/Http/Problems.cs:60` sí lo devuelve al cliente.
  - `appsettings.json:9` no baja `Microsoft.EntityFrameworkCore.Update`, así que cada colisión ya controlada (409) deja un `fail` con stack.
- **Impacto:** cualquier alerta por 5xx basada en el log de acceso queda ciega, y el `traceId` que reporte un usuario no se puede buscar. No hay fuga de datos: QA-S9 comprobó que el log va sin correos, tokens ni ciphertext.
- **Fix:**
  ```csharp
  app.UseSafeRequestLogging();   // antes de UseExceptionHandler: registra el status final
  app.UseExceptionHandler();
  // log de acceso: + ctx.TraceIdentifier; builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
  // appsettings: "Microsoft.EntityFrameworkCore.Update": "Critical" (ApiExceptionHandler ya registra los no controlados)
  ```
- **Esfuerzo:** S/M.

**[FIN-10] Los reportes no se deduplican: una sola persona infla el contador y entierra la cola**
- **Orígenes:** QA-M4 · **Método:** dinámico (QA) · **Verificado hoy:** sí.
  - `Features/Moderation/ModerationService.cs:76-87`: siempre hace `Reports.Add`.
  - `AdminService.cs:174-175`: cuenta reportes, no personas que reportan.
  - `AdminService.cs:93`: la cola va de más viejo a más nuevo, con `Take(200)` fijo.
- **Impacto:** 25 reportes de la misma persona aparecen como "24 previos" en el panel y pueden empujar a una suspensión. Un flujo de 120 por minuto entierra los reportes legítimos.
- **Fix:** índice único parcial `(reporter_id, reported_user_id) WHERE status = 'Open'`. Si choca, la evidencia nueva se agrega al reporte abierto. El contador se calcula con `COUNT(DISTINCT reporter_id)`, y `/reports` lleva una política de rate limit propia (p. ej. 10 por hora).
- **Esfuerzo:** S.

**[FIN-11] La evidencia de los reportes guarda texto plano sin plazo y sobrevive al borrado de la cuenta**
- **Orígenes:** AUD-I3 · **Método:** estático · **Verificado hoy:** sí.
  - `Features/Moderation/Moderation.cs:31-37`: `ReportEvidence.Plaintext`.
  - `AppDbContext.cs:184-185`: al borrar la cuenta, las FK quedan en `SetNull`, pero el texto y el `Detail` se conservan.
  - `Infrastructure/Database/MaintenanceService.cs:34-48`: purga mensajes y franking, pero no reportes.
  - `docs/privacidad-y-tiendas.md:26` dice "anonimizados si se borra la cuenta".
- **Impacto:** es un dato personal (de quien reporta y de quien es reportado) sin plazo de retención, lo que choca con la Ley 1581 y con la política publicada.
- **Fix:** columna `EvidencePurgedAt`. `MaintenanceService` vacía `Plaintext` y `Detail` N días después de `ResolvedAt` (p. ej. 180, igual que el franking) y conserva `MessageId` y `Verified`. El plazo se documenta en la política. **Requiere decisión del dueño** sobre el plazo.
- **Esfuerzo:** M.

**[FIN-12] El chat no tiene cuota de almacenamiento ni expiración por defecto, y una página de historial puede pesar ~9 MB**
- **Orígenes:** QA-M5 · **Método:** dinámico (QA) · **Verificado hoy:** sí.
  - `Features/Chat/ChatService.cs:13-83`: no hay cuota acumulada, solo el límite de 64 KiB por mensaje (`ChatContracts.cs:32`) y el de escrituras por minuto.
  - `ChatService.cs:90-100`: pagina hasta 100 mensajes sin cota de bytes.
  - `MaintenanceService.cs:41-43`: solo purga si `MessageRetentionDays > 0`, y el default es 0 (`.env.example:46`).
- **Impacto:** con dos cuentas propias se pueden escribir unos 10 GB por día por par. Además incumple 05-S6 ("los sobres no entregados expiran a los 30 días").
- **Fix:** antes de abrir la transacción, sumar `Ciphertext.Length` por (match, remitente) pendiente y responder 429 `chat_quota_exceeded` si se pasa de ~50 MB. En `ListAsync`, cortar la página al llegar a ~1 MB. Poner `CHAT_MESSAGE_RETENTION_DAYS=30` por defecto.
- **Esfuerzo:** M.

**[FIN-13] Hay listados sin paginar: el panel ya oculta hoy 46 canciones**
- **Orígenes:** AUD-I8, QA-F3 · **Método:** ambos · **Verificado hoy:** sí.
  - `AdminService.cs:61` hace `Take(limit)` sin offset; `AdminEndpoints.cs:56` lo deja en 100 por defecto, y `AdminPanelPage.cs:500-506` no manda `limit`. Con 146 canciones en el catálogo, 46 no se ven en el panel.
  - Sin tope: `AdminService.cs:17-25` (`/admin/events`), `Features/Events/EventService.cs:54-59` (`/me/events`) y `MatchingService.cs:82-87` (`/matches`).
  - `AdminService.cs:93`: `Take(200)` fijo.
  - `UserService.cs:101-119`: el export se arma completo en memoria.
- **Impacto:** el admin no puede gestionar el catálogo completo. Lo demás escala de forma lineal y para el volumen de una demo no se nota.
- **Fix:** `offset` (o keyset) en `/admin/songs` y que el panel pagine. Keyset `?before=<lastMessageAt,id>&limit=` en `/matches`. El export en streaming (`AsAsyncEnumerable` + `Utf8JsonWriter`). **Cambia el contrato**, así que hay que coordinarlo con el front.
- **Esfuerzo:** M.

**[FIN-14] En el panel, "Suspender cuenta" banea con un clic, sin confirmación y sin forma de reactivar**
- **Orígenes:** QA-F12 · **Método:** dinámico (QA, por lectura del panel) · **Verificado hoy:** sí.
  - `AdminPanelPage.cs:664-669`: tres botones sin confirmación. "Borrar evento" sí la pide.
  - El panel no llama a `/admin/users/{id}/status`, así que no hay forma de reactivar desde la UI.
  - `AdminService.cs:37-48`: editar o borrar un evento no avisa a los asistentes.
- **Impacto:** un clic errado cierra todos los matches del usuario y revoca sus sesiones, y eso no se recupera al reactivar (FIN-05).
- **Fix:** paso de confirmación con el texto "Cierra todos sus matches y no se recuperan" (diff de QA-F12) y botón "Reactivar" en el detalle del usuario. El aviso a asistentes queda como mejora (push `EventChanged`).
- **Esfuerzo:** S.

**[FIN-15] El mapeo de géneros de Spotify deja fuera rock, colombian rock, alternative rock y corridos, y el 200 no avisa**
- **Orígenes:** QA-F5 · **Método:** dinámico (QA) · **Verificado hoy:** sí.
  - `Features/Music/SpotifyGenreMap.cs:22-23`: no existe la keyword `rock`, `colombian rock` ni `corridos`; solo `corrido`, y la coincidencia es por palabra completa (`:41-43`).
  - `:25` vs `:27`: `dance` (electrónica) se evalúa antes que `pop`, y gana la primera regla que coincide (`:44-45`).
- **Impacto:** un fan de rock colombiano, que es público central de la app, importa Spotify, recibe 200 y su ADN queda vacío sin ningún aviso.
- **Fix:** agregar las keywords, poner `("pop-latino", ["dance pop", "pop rock"])` antes de electrónica, un test de tabla en `SpotifyGenreMapTests` y `unmappedGenres` en la respuesta (cambio aditivo del contrato).
- **Esfuerzo:** S.

### 5.3 Menores

Todos los menores están **verificados hoy** en el código, en las líneas citadas.

| ID | Hallazgo | Orígenes · método | Evidencia (hoy) | Fix | Esf. |
|---|---|---|---|---|---|
| FIN-16 | Primer `PUT /me/backup` concurrente responde 500 (`23505` sin capturar) | AUD-M1, QA-M1, QA-S9(c) · ambos | `Features/Sync/SyncService.cs:19-33` | `INSERT … ON CONFLICT (user_id) DO UPDATE` o capturar `UniqueViolation` y reintentar como update | S |
| FIN-17 | Resolver un reporte es check-then-act y el ban se confirma fuera de la resolución | AUD-I5, QA-F14 · ambos | `AdminService.cs:99-115` (`:108` confirma su propia tx en `:125-160`) | Una sola tx: `ExecuteUpdate(r => r.Id==id && r.Status==Open)` → 0 filas = 409; el ban va dentro, sin commit propio | S |
| FIN-18 | El tope de 3 vinculaciones abiertas se supera con peticiones concurrentes | AUD-M2, QA-M3 · ambos | `SyncService.cs:54-60` | tx + `AdvisoryLock.AcquireAsync(db, "device-link", userId)` | S |
| FIN-19 | `DELETE /me` borra la cuenta solo con el Bearer; también sirve el de una sesión cerrada, por FIN-02 | QA-S3 · dinámico | `Features/Users/UserEndpoints.cs:16-17,28-32` | Tras FIN-02, pedir contraseña o un `auth_time` reciente (ASVS V3, operaciones sensibles). Cambia el contrato | S |
| FIN-20 | Los 400 de binding son genéricos y sin campo; 405/409/413/415 salen sin `code`; el 413 sale en inglés; `Range` aparece en notación científica | QA-F2, QA-M9 · dinámico | `Program.cs:139` (`ThrowOnBadRequest=false`, la misma causa que el 400 de FIN-01), `Infrastructure/Http/Problems.cs:24-30,67`, `SyncContracts.cs:9-10`, `ChatContracts.cs:53` | `ThrowOnBadRequest=true` + campo desde `JsonException.Path` + códigos para 405/409/413/415 | S |
| FIN-21 | Un admin puede suspender a otro admin | AUD-M4 · estático (el QA no pudo probarlo: hay un solo admin) | `AdminService.cs:120-131` (solo bloquea `userId == adminId`) | 403 si el objetivo tiene `Role == Admin` | S |
| FIN-22 | El radar muestra candidatos sin ADN (afinidad 0), usa `>=` cuando el doc dice "superar" y `MaxEntries=200` trunca sin avisar; `attendeeCount` cuenta suspendidos y bloqueados | AUD-M11, QA-F4 · ambos | `Features/Events/RadarService.cs:16,57-61,74`; `appsettings.json:26`; `EventService.cs:70` | Exigir `norm` en el candidato; alinear doc y código; `truncated` en la respuesta; filtrar `status`/bloqueos en el conteo | S |
| FIN-23 | El push token no valida el formato `Expo(nent)PushToken[…]` y se reasigna entre usuarios | AUD-M5, QA-S11 · ambos | `Features/Devices/DeviceContracts.cs:8`; `DeviceService.cs:36-50` | `RegularExpression` y no reasignar sin revocar al dueño anterior | S |
| FIN-24 | `/api/*` responde sin `Cache-Control: no-store` | AUD-M6 · estático | `SecurityHeaders.cs:5-21` (solo `AdminPanelPage.cs:23` pone `no-store`) | Agregarlo en `UseSecurityHeaders` para `/api` | S |
| FIN-25 | `banned_emails` usa SHA-256 sin clave (verificable por diccionario) | AUD-M8 · estático | `Features/Auth/BannedEmail.cs:16` | HMAC con una clave del servidor (con migración del veto) | S |
| FIN-26 | `DotEnv` sube por los directorios padre y se carga también en producción | AUD-M9 · estático | `DotEnv.cs:9-21`; `Program.cs:33` | Cargar solo en Development | S |
| FIN-27 | `RateLimit:AuthPermitPerMinute`, `MaxFailedLogins` y `LockoutMinutes` no tienen variable de entorno | AUD-M21 · estático | `Infrastructure/Configuration/EnvironmentMapping.cs:26` (solo `USER_WRITES`) | Mapearlas y documentarlas en `.env.example` | S |
| FIN-28 | El HttpClient de Expo no tiene timeout (queda en el default de 100 s) | AUD-M10 · estático | `Infrastructure/Push/PushQueue.cs:27` | `client.Timeout = 10 s` | S |
| FIN-29 | Faltan índices: `swipes(song_id)`, `refresh_tokens(expires_at)` y `chat_messages(created_at)` | AUD-M12 · estático | `AppDbContext.cs:70-74,95-99,160-165` (la PK de swipes es `(user_id, song_id)`) | Una migración con los 3 índices | S |
| FIN-30 | El feed usa `ORDER BY random()` (hasta 3 consultas) | AUD-M13 · estático | `Features/Music/MusicService.cs:34-43` | `TABLESAMPLE` u offset aleatorio cuando crezca el catálogo | M |
| FIN-31 | Código muerto | AUD-M14 · estático | `Features/Music/Dna.cs:40` (solo lo usa `EventsTests.cs:363`); `Features/Matching/Matching.cs:14` (`AccountDeleted`), `:32` (`Involves`), sin usos | Borrar, o usarlo en el test de radar | S |
| FIN-32 | Cambiar el género de una canción puede cruzarse con un swipe concurrente | AUD-M15 · estático | `AdminService.cs:82-86` | `SELECT … FOR UPDATE` sobre la canción | S |
| FIN-33 | Las migraciones corren al arrancar sin lock | AUD-M16 · estático | `Data/Seeder.cs:208-209`; `render.yaml:31-33` | `pg_advisory_lock` alrededor de `MigrateAsync` o un `efbundle` previo | S |
| FIN-34 | Imagen con tags flotantes `10.0`, base sin chiseled y sin lockfile de NuGet | AUD-M17 · estático | `Dockerfile:2,10`; `Directory.Build.props` sin `RestorePackagesWithLockFile` | `aspnet:10.0.x-noble-chiseled` por ARG + lockfile | S |
| FIN-35 | 678 líneas de HTML, JS y CSS en un string de C#, sin lint | AUD-M18 · estático | `Features/Admin/AdminPanelPage.cs` | Pasarlo a `wwwroot/admin/` como recurso embebido | M |
| FIN-36 | El contrato tiene `\u00XX` escapados, lo que hace ilegibles los diffs | AUD-M19 · estático | `contracts/openapi.v1.json` (247 secuencias) | `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` al escribirlo | S |
| FIN-37 | La versión del contrato está fija en `1.0.0` y el CHANGELOG va en `draft.6` | AUD-I12 · estático · **baja de I a M** | `Infrastructure/OpenApiSetup.cs:26`; `contracts/CHANGELOG.md:6` | `<Version>` en `Directory.Build.props`, leído en `OpenApiSetup`, y un test contra el CHANGELOG | S |
| FIN-38 | CI: no corre en push a `dev` y no tiene format, vulnerables, dependabot ni smoke de la imagen | AUD-I11 · estático · **baja de I a M** | `.github/workflows/ci.yml:3-6,30-39`; no hay `.github/dependabot.yml` | `branches: [main, dev, rebuild/v1]`; `dotnet format --verify-no-changes`; `list package --vulnerable`; `docker run` + `curl /health/live`; dependabot nuget y actions | S |
| FIN-39 | El catálogo con previews de iTunes "de uso académico" se siembra también en producción | AUD-M22 · estático · **sospecha legal** | `render.yaml:34-35`; `scripts/curate_songs.py` | Revisar los términos de iTunes Search antes de publicar | S |
| FIN-40 | Swagger UI no sirve en Development: responde 401 y, aun con token, la CSP de la API bloquea sus scripts | QA-S8 · dinámico | `Program.cs:152-154,163-170`; `SecurityHeaders.cs:15-17` (el comentario dice lo contrario) | Montarlo antes de la autenticación y con CSP propia, o quitarlo y dejar solo `/openapi/v1.json` | S |
| FIN-41 | Canciones en el admin: `iTunesTrackId` acepta valores ≤ 0, se permiten duplicados sin id, `previewUrl ""` se rechaza y `ILIKE` no escapa `%` ni `_` | QA-F11 · dinámico | `Features/Admin/AdminContracts.cs:38-39`; `AdminService.cs:56,212` | `[Range(1, …)]`, escapar el patrón, normalizar `""` a null antes de validar, índice parcial por título y artista | S |
| FIN-42 | Las URLs del admin aceptan `http://` y `ftp://` | QA-S12 · dinámico | `AdminContracts.cs:15,37,38` (`[Url]`) | Atributo `HttpsUrl` | S |
| FIN-43 | Un swipe sobre una canción desactivada responde 404 `song_not_found` | QA-F6 · dinámico | `MusicService.cs:52-54` | 410 `song_unavailable` | S |
| FIN-44 | Texto débil: `displayName` acepta invisibles y RTL override; los textos de evento se recortan después de validar; el lineup no tiene largo máximo | QA-F7 · dinámico | `Features/Auth/AuthService.cs:27-28`; `UserService.cs:43`; `AdminService.cs:194-197` | `Text.CleanName` (rechaza Cc/Cf) y revalidar después del `Trim` | S |
| FIN-45 | Fechas sin tope razonable: `birthDate` 1900 y eventos en el pasado o en 9999 | QA-F8 · dinámico | `UserService.cs:60`; `AdminService.cs:198` | Edad ≤ 110 y `startsAt` entre hoy y +3 años | S |
| FIN-46 | Re-aceptar la misma versión del consentimiento inserta otra fila, y no hay tope de sesiones activas | QA-F9 · dinámico | `UserService.cs:93`; `AuthService.cs:64-73` | Idempotencia por `(user, version)` y tope de 10 familias | S |
| FIN-47 | Correos equivalentes en NFC/NFD crean cuentas distintas y `[EmailAddress]` es laxo | QA-S10 · dinámico | `Infrastructure/Http/Validation.cs:74`; `Features/Auth/AuthContracts.cs:12`; `Directory.Build.props:10` (`InvariantGlobalization`, así que `Normalize` no hace nada) | Parte local ASCII estricta e IDN en el dominio (diff de QA-S10) | S |

---

## 6. Descartados

| Origen | Hallazgo | Motivo |
|---|---|---|
| QA-F10 y AUD-M3 | Un refresh en paralelo o un reintento revoca la familia completa | Es diseño documentado: rotación con detección de reutilización (ARCHITECTURE §6, test `AuthTests.cs:196`). Una ventana de gracia debilita justo esa detección. **Lo que queda es un requisito del front:** refresh *single-flight*, que no verifiqué (§10) |
| QA-M2 | `PUT /me/backup` es last-write-wins y permite bajar `version` | Es diseño: blob opaco y único por usuario (`Features/Sync/Sync.cs:3-6`, contrato "reemplaza"). `version` es la versión del formato (`SyncContracts.cs:16`, `Range(1,1000)`), no una revisión. Solo queda la deuda de documentarlo |
| QA-M7 | El like no exige haber desbloqueado el radar | Es regla documentada (ARCHITECTURE §4.3: el like exige un evento compartido; los 15 swipes son requisito del radar). El sub-punto `eventId` inferido es opcional y el front siempre lo envía |
| QA-M8 | Escribir en un match cerrado da 403 y no 404 | Es diseño (ARCHITECTURE §5.2): el 404 es para terceros. Lo cubre el test `ChatTests.cs:102` |
| Sub-puntos del QA | `<script>` guardado en `displayName` y `bio`; contraseña de 6 emojis; cambiar like↔dislike; mayúsculas en `ReportReason` | La API es JSON y el panel usa `textContent`. La política es de 12 a 72 bytes y los emojis la cumplen. "Repetir no suma" es contrato. El enum es camelCase por contrato |

No descarté por incorrecto ningún hallazgo de la auditoría. AUD-I11 y AUD-I12 bajaron a menor (§3, filas 9 y 10). AUD-I9 se integró en FIN-02.

---

## 7. Comparativa con el legado

### 7.1 Hallazgos GEN que aplican al back

**Fuera de alcance del back** (solo front, móvil, diseño o landing): GEN-10, 13, 23, 24, 25, 26, 27, 28, 33, 34 y 37.

| GEN (sev. legado) | Tema | Estado hoy | Evidencia actual | Vínculo |
|---|---|---|---|---|
| GEN-01 (C) | IDOR sistémico, identidad desde el cliente | **Resuelto** | `Infrastructure/Auth/CurrentUser.cs:9-13` (solo `sub`); `AuthSetup.cs:52` (política por defecto autenticada); tests `ChatTests.cs:129`, `MatchingModerationTests` (`Un_tercero_no_puede_ver_ni_deshacer_el_match_ajeno`); QA: 0 IDOR en 120 OK. Falta el test de arquitectura "sin `userId` en contratos" (§7.5 V4) | — |
| GEN-02 (C) | Google falso, vinculación por email | **No aplica / decisión (D10)** | No hay login social ni tabla de identidades; Spotify solo importa géneros (`MusicService`, test `Spotify_de_A_no_cambia_el_adn_de_B`). Si Google o Apple vuelven en v1.1, aplica la dirección de 00 | — |
| GEN-03 (C) | Sesión sin refresh | **Resuelto** | Access de 15 min (`appsettings.json:21`); refresh rotativo con hash y detección de reutilización (`AuthService.cs:100-136`; `AuthTests.cs:196`). La revocación del access queda en FIN-02 | FIN-02 |
| GEN-04 (C) | Secretos en historial y fallbacks | **Parcial** | Sin fallbacks: `ValidateOnStart` (`Program.cs:38-59`); TLS a la BD fuera de Development (`Options.cs:32-39`; `SecurityHardeningTests.cs:569`). **Sigue:** el commit `955db69` con la credencial de Supabase está en el historial de este mismo repo (no transcrita; su rotación la decide el dueño). Secret scanning y push protection no se pueden verificar desde el repo | FIN-03 (otra credencial compartida) |
| GEN-05 (C) | Matchmaking falso | **Resuelto** | El ADN cambia con los swipes (`MusicTests.cs:109,130`); el match es recíproco (`MatchingModerationTests.cs:59`); afinidad en SQL igual a la de C# (`EventsTests.cs:345`); eventos curados en BD | FIN-22 (calidad del radar) |
| GEN-06 (C) | Dependencia de Spotify | **Resuelto** | Login por email (D10), catálogo curado con previews (`Seeder` + `scripts/curate_songs.py`) y Spotify como conector opcional | FIN-15, FIN-39 |
| GEN-07 (C) | Chat no funcional y en claro | **Resuelto** | Solo ciphertext (`ChatCanaryTests.cs:40`); pertenencia (`ChatTests.cs:129`); hub con `[Authorize]` y sin métodos invocables (`Infrastructure/Realtime/ChatHub.cs:21-35`; `ChatTests.cs:222`). 05-S6 (expiración a 30 días) **no se cumple**: retención 0 | FIN-04, FIN-12 |
| GEN-08 (C) | Match sin estado ni consentimiento | **Reaparece distinto** | El estado y la reciprocidad existen (`MatchingModerationTests`: `Like_unilateral_no_crea_match`, `Likes_reciprocos_concurrentes_crean_exactamente_un_match_activo`). Pero el ciclo ban → reactivar rehace un match **sin consentimiento nuevo** | **FIN-05** |
| GEN-09 (C) | Esquema sin fuente de verdad | **Resuelto** | `Data/Migrations` (EF Core) desde el día 0; los tests migran sobre Postgres real; email único normalizado (`AppDbContext.cs:48`); swipe único por PK (`:97`); match activo con índice único parcial (`:143`) | FIN-29, FIN-47 |
| GEN-11 (C) | Cumplimiento de tiendas y Ley 1581 | **Parcial** (parte del back) | `DELETE /me`, `/cuenta/eliminar` (`SecurityHardeningTests.cs:117`), reportes y bloqueos, consentimiento versionado, 18+ (`UserService.cs:61`; `ProfileTests.cs:92`), export, cuenta del revisor. **Falta:** la retención de la evidencia contradice la política publicada, y `PRIVACY_CONTACT_EMAIL` está vacío | FIN-11 |
| GEN-12 (C) | Push inexistente o inseguro | **Resuelto** | Tokens por sesión y dispositivo (`DevicePushToken`), Expo Push sin contenido; `SecurityHardeningTests.cs:458-536`; `DevicesPushTests` | FIN-23, FIN-28 |
| GEN-14 (C) | Hubs anónimos | **Resuelto** | `ChatHub.cs:21` con `[Authorize]`; token por query solo en `/hubs` (`AuthSetup.cs:41-46`); se corta al suspender o borrar (`HubLifecycleTests`) | FIN-02 (no se corta al revocar) |
| GEN-15 (C) | No desplegable | **Reaparece distinto** | El original está resuelto: imagen .NET 10 multi-stage sin root (`Dockerfile`) y `docker build` en CI (`ci.yml:33-39`). Pero la ruta de despliegue y el runbook no se ejecutan en CI (sin smoke test) y fallan en silencio | **FIN-07**, FIN-38 |
| GEN-16 (I) | Arquitectura sobredimensionada | **Resuelto** | Un proyecto, `Features/*`, transacciones explícitas, proyecciones a DTO | FIN-02 (cierre de sesión en 4 sitios) |
| GEN-17 (I) | Kafka, Redis y Worker | **Resuelto (D3)** | 0 referencias a Kafka o Redis en `Directory.Packages.props`; trabajo de fondo en el mismo proceso (`MaintenanceService`, `PushQueue`) | — |
| GEN-18 (I) | 500 por input, sin mapeo de errores | **Reaparece distinto** | ProblemDetails, enums como texto y `23505` mapeado (`AuthService.cs:52-56`); `PlatformTests.cs:30,38,46,64`. La misma causa (colisión de unicidad sin mapear, que en el legado daba "1×202 y 7×500") vuelve en `PUT /me/backup`. Los errores de binding salen sin campo | **FIN-16**, FIN-20 |
| GEN-19 (I) | Auth por debajo de ASVS L1 | **Reaparece distinto** | Rate limit (`AuthTests.cs:172`), lockout (`AuthTests.cs:136`), hash ficticio, contraseña de 12 a 72 bytes (`AuthTests.cs:92`), email normalizado. La enumeración vuelve como oráculo por 429 | **FIN-06** |
| GEN-20 (I) | Superficie operativa expuesta | **Parcial** | Health sin detalle (`PlatformTests.cs:12`; GET de hoy: `Healthy`); OpenAPI fuera de producción (`SecurityHardeningTests.cs:557`); cabeceras (`PlatformTests.cs:83`). **Falta:** HSTS e IP real dependen de una variable sin validar | FIN-07, FIN-40 |
| GEN-21 (I) | Salas de evento | **No aplica (D13)** | No hay salas en `Features/` | — |
| GEN-22 (I) | Contrato implícito y divergente | **Reaparece distinto** | OpenAPI generado y con test (`PlatformTests.cs:98`), CHANGELOG por draft, sin `userId`. Pero el test compara el documento, no el comportamiento: el contrato promete `open` y el binder solo acepta `Open` | **FIN-01**, FIN-37 |
| GEN-29 (I) | Tests que no protegen | **Parcial** | 238 tests con Postgres real, IDOR, concurrencia y canario. **Falta:** binding de query y enums; flujo ban → reactivar; reutilización de id tras el ack; test de arquitectura de contratos. El QA encontró 1 C y 11 I que la suite no cubre | FIN-01, 04, 05 |
| GEN-30 (I) | Plataforma y dependencias | **Parcial** | .NET 10 (`global.json`), dependencias al día y sin CVE (AUD §2). **Falta:** dependabot o Renovate | FIN-38 |
| GEN-31 (I) | DX y docs que no corresponden | **Reaparece distinto** | Hay README como runbook, `mise.toml`, `global.json`, compose con perfiles, `.env.example` completo y `render.yaml`. Pero la opción B no arranca y `.env.example` tiene una trampa | **FIN-07** |
| GEN-32 (I) | Repos con trabajo divergente | **Resuelto (D9/D14)** | El WIP del legado se preservó en `wip/legacy-fixes`; el árbol está limpio (salvo `AUDITORIA-2026-10-14.md` sin trackear). Higiene pendiente: 8 worktrees `worktree-agent-*` locales | — |
| GEN-35 (M) | Código muerto | **Resuelto** | Los fósiles del legado ya no están; queda código muerto menor | FIN-31 |
| GEN-36 (M) | Números mágicos y nombres | **Parcial** | Políticas con nombre (`MatchingOptions`, `RateLimitOptions`); umbral servido por la API (`DnaResponse.RadarThreshold`). **Falta:** la zona horaria de Colombia está en 4 copias (AUD §5) y hay variables sin mapear; los typos en nombres de repos se mantienen por D14 | FIN-27 |

### 7.2 Checklist de seguridad mínima (00 §7.5, ASVS L1)

| Área | Requisito | Estado | Evidencia / qué falta |
|---|---|---|---|
| V2 Autenticación | Contraseña de 10 o más caracteres (≤ 72 bytes) | ✅ | 12 a 72 bytes (`AuthContracts.cs:13`; `AuthTests.cs:92`) |
| | Rate limit en `/auth/*` | 🟡 | Por IP en registro y login (`AuthTests.cs:172`). `refresh` y `logout` no tienen (FIN-08). La IP real depende del proxy (FIN-07) |
| | Sin enumeración (respuestas y tiempos uniformes) | 🟡 | El hash ficticio cubre el tiempo; el 409 del registro es riesgo aceptado (§6); el 429 delata la cuenta (FIN-06) |
| | Tokens de Google y Apple validados | — | No aplica (D10) |
| V3 Sesión | Access ≤ 15 min | ✅ | `appsettings.json:21` |
| | Refresh rotativo, con hash y detección de reutilización | ✅ | `AuthService.cs:100-136`; `AuthTests.cs:196` |
| | Revocación al cerrar sesión, borrar la cuenta o cambiar la contraseña | 🟡 | El refresh sí se revoca; el access y el hub no (FIN-02). Borrar la cuenta corta (`PlatformTests.cs:71`). **No existe cambio ni recuperación de contraseña** (§10) |
| V4 Control de acceso | Denegar por defecto; identidad del claim; pertenencia; rol | ✅ | `AuthSetup.cs:52`; `CurrentUser.cs:9-13`; 404 a ajenos; rol admin confirmado en la BD. El test de arquitectura "sin `userId`" no existe; lo mitiga el QA (0 IDOR, mass assignment bloqueado) |
| V5 Validación | DTO con rangos, enums validados, ningún 500 por input | 🟡 | `PlatformTests.cs:30-64`, `SecurityHardeningTests.cs:193-259` (NUL, null en arrays). El QA no encontró 500 por input, pero sí por concurrencia (FIN-16); los 400 de binding no dicen el campo (FIN-20); las fechas no tienen tope (FIN-45) |
| V7 Errores y logs | ProblemDetails sin stack; enmascarado; correlación por `traceId` | 🟡 | Sin stack (`Problems.cs`); logs sin query string ni cuerpos (`SecurityHeaders.cs:24-43`); el `traceId` no está en el log y los 500 salen como 200 (FIN-09) |
| V8 Protección de datos | Llaves en SecureStore, nada en AsyncStorage | — | Es del móvil, fuera del back |
| V9 Comunicaciones | TLS; `VerifyFull` hacia Postgres en producción | 🟡 | Se exige Require o superior fuera de Development (`Options.cs:36-39`), pero `render.yaml:25-26` usa `Require` y no `VerifyFull` |
| V10/V14 Configuración | Sin secretos por defecto | ✅ | `ValidateOnStart`; JWT de 32 o más caracteres |
| | Swagger solo en dev | ✅ | `Program.cs:158-170`; `SecurityHardeningTests.cs:557` (en dev está roto, FIN-40) |
| | Cabeceras de seguridad | ✅ | `PlatformTests.cs:83`; HSTS depende del proxy (FIN-07) |
| | Secret scanning y push protection | ? | No se puede verificar desde el repo |
| | Escaneo de dependencias en CI | ❌ | `ci.yml` no tiene `--vulnerable` ni dependabot (FIN-38) |

**Por área** (7 aplicables al back: V2, V3, V4, V5, V7, V9 y V10/14): **V4 cumple completa**; V2, V3, V5, V7, V9 y V10/14 son **parciales**; ninguna está sin cumplir. Por ítem, de 15 verificables: 7 ✅, 6 🟡, 1 ❌ y 1 sin verificar.

### 7.3 Cifras

| Métrica | Legado | Hoy |
|---|---|---|
| Críticos | 01: **17** · 02: **8** · 00: **15** GEN (13 del back) | AUD: 0 · QA: 1 · conciliado: **1** (FIN-01, funcional) |
| Importantes | 01: 30 · 02: 27 · 00: 18 GEN | AUD: 12 · QA: 11 · conciliado: **14** |
| Menores | 01: 11 · 02: 16 · 00: 4 GEN | AUD: 22 · QA: 19 · conciliado: **32** |
| GEN del back (26) | 26 abiertos | **12 resueltos · 6 parciales · 6 reaparecen distinto · 2 no aplican · 0 abiertos** |
| GEN críticos del back (13) | 13 abiertos | 8 resueltos · 2 parciales (04, 11) · 2 reaparecen como importante o menor (08, 15) · 1 no aplica (02) |
| IDOR reproducidos | Sistémico (02-S1) | 0 (QA, 120 OK) |
| Tests | 52, ninguno cubre los críticos | 238 con Postgres real; fallaron en ver 1 C y 6 I de comportamiento |

---

## 8. Plan de ataque (PRs pequeños, primero los quick wins)

| # | Lote / PR | FIN | Esfuerzo | Por qué en este orden |
|---|---|---|---|---|
| 1 | **Moderación usable** | 01, 05, 14, 17, 21, 10 | S (1 PR, ~1 día) | Lo único crítico y lo que más se ve en una demo del panel. Todo está en `Admin*` y `ModerationService`. Agregar tests: `?status=open`, ban → reactivar → like, resolución concurrente |
| 2 | **Arranque seguro** | 03, 07, 26, 27 | S | Cierra la trampa del runbook de hoy (07c) y lo que bloquea desplegar. Son validadores de opciones, compose y `.env.example` |
| 3 | **Sesión de verdad** | 02 (+ `SessionRevoker`), 19 | S/M | Depende de 2 solo por el orden de revisión. Coordinar con el front si se exige contraseña en `DELETE /me` |
| 4 | **Integridad del chat** | 04, 12 | S/M | El bypass de franking es barato de cerrar. La cuota necesita decidir el umbral y la retención por defecto |
| 5 | **Cotas** | 08, 18, 23 | S | Una línea de Kestrel, un atributo por endpoint, rate limit en `refresh`/`logout` y el advisory lock de vinculaciones |
| 6 | **Errores y logs** | 09, 16, 20 | S/M | Reordenar el middleware, bajar EF, upsert del respaldo y `ThrowOnBadRequest`. Reverificar FIN-01 después: cambia el camino del 400 |
| 7 | **Lockout** | 06 | S (oráculo) / M (DoS) | Decisión de diseño: (cuenta, IP) o backoff. Documentar en §6 |
| 8 | **CI y operación** | 38, 34, 33, 29, 28, 36, 37 | S | Habilita el gate de vulnerables y el smoke de imagen antes de cualquier despliegue |
| 9 | **Privacidad** | 11 | M | Requiere decidir el plazo de retención. Migración, job y política |
| 10 | **Listados** | 13 | M | Cambia el contrato de `/matches`; coordinar con el front. El panel de canciones puede ir antes, solo con `offset` |
| 11 | **Calidad de datos** | 15, 22, 41–47, 24, 25, 32, 43 | S cada uno | De a uno cuando se toque cada zona. El 15 primero, porque afecta a usuarios reales |
| 12 | **Deuda** | 30, 31, 35, 39, 40 + redundancias de AUD §5 | S/M | Oportunista. FIN-39 es una decisión legal antes de publicar |

**Reverificación:** repetir el QA solo sobre FIN-01, 02, 04, 05 y 16 después de los lotes 1, 3, 4 y 6, y sumar la cadena de navegador sobre el panel.

---

## 9. Lo que está bien

- **Identidad y autorización:** solo `sub`, denegar por defecto, 404 a ajenos, rol admin confirmado en la BD en cada request. Hay test y el QA no encontró IDOR.
- **Concurrencia resuelta en Postgres:** advisory locks, `UPDATE` condicionados (refresh, lockout, `seq` del chat) y `ON CONFLICT`. Probado con carreras reales (`SecurityHardeningTests.cs:104,269,378-413`).
- **Chat E2EE:** el servidor nunca ve texto plano (test canario), el franking se verifica en tiempo constante y el hub no tiene métodos invocables.
- **Configuración:** `ValidateOnStart` sin fallbacks, TLS obligatorio a la BD fuera de Development y JWT de 32 o más caracteres.
- **Errores, cabeceras y logs:** ProblemDetails en español con `code`, CSP por hash en las páginas HTML, logs sin query string ni cuerpos.
- **Base de calidad:** migraciones desde el día 0, contrato generado con test, 238 tests con Postgres real, warnings como errores y dependencias al día.
- **Documentación honesta:** ARCHITECTURE §6 declara los riesgos residuales y el README sirve de runbook. Que dos revisiones independientes coincidan en 12 hallazgos y ninguna encuentre IDOR ni fuga es la mejor evidencia de que la reconstrucción funcionó.

---

## 10. No verificado, fuera de alcance y pendientes del dueño

**No verificado o fuera de alcance:**
- **UI:** ni la app móvil ni el panel en un navegador (el QA fue solo API). FIN-01 y FIN-14 salen de leer `AdminPanelPage.cs` y de las respuestas de la API.
- **Ejecución que no hice:** `docker compose --profile full`, `docker build` ni un despliegue en Render. FIN-07(b, c), FIN-33 y FIN-34 salen de leer el código. **Tampoco reproduje** los hallazgos dinámicos: confirmé su causa en el código y su reproducción es del QA.
- **No probado por el QA:** el rate limit real de 10/min (estaba en 400/min), la expiración de tokens y de vinculaciones, los jobs horarios, el push real, el radar con más de 200 asistentes, la suspensión entre admins y el comportamiento de Render (IPs del proxy, solapamiento de instancias).
- **Front:** que el refresh sea *single-flight*. Es el requisito que se desprende del descarte de QA-F10 y AUD-M3, y no lo verifiqué.
- **Hueco funcional observado que no reportó ninguno de los dos:** no existe cambio ni recuperación de contraseña. Afecta la V3 de §7.5 (revocar al cambiar la contraseña) y deja sin salida a quien olvide su clave, porque tampoco hay verificación por correo. Queda como **sospecha de alcance**, no como FIN: puede ser una decisión de la v1 que no encontré documentada.

**Pendientes del dueño:**
- `PRIVACY_CONTACT_EMAIL` y `PUBLIC_BASE_URL` (los dos con `sync:false` y vacíos).
- Plazo de retención de la evidencia de reportes (FIN-11) y de los mensajes no entregados (FIN-12).
- Si la CI debe correr en push a `dev` (FIN-38).
- Rotar la credencial de Supabase del commit `955db69`, que sigue en el historial del repo (GEN-04).
- Términos de iTunes Search antes de publicar (FIN-39).
- **Limpieza de la corrida QA**, que pide confirmación porque implica `DROP` y `rm`:
  - BD `encorely_qa`.
  - Instancia en `:5100`, que sigue viva: `/health/ready` respondió 200 hoy.
  - `qa-enjambre/2026-10-14-0939/tmp/` (175 MB, con tokens de prueba ya vencidos).
  - Procesos `node hub.mjs`.
- **Higiene del repo:** `AUDITORIA-2026-10-14.md` sin trackear y 8 worktrees `worktree-agent-*` locales en `.claude/worktrees/`.
