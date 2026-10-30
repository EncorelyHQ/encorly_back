# 00 · Reporte general del estado de Encorely

**Fecha:** 2026-10-13 · **Autor:** curaduría externa sobre 5 reportes independientes (01–05), con verificación puntual contra el código cuando hubo contradicción o duda · **Alcance:** `encorly_back` (@`bba861a` + 3 archivos sin commit + `db/` sin trackear), `encorely-front` (@`ff11541` + 5 archivos sin commit; rama remota `codex/actualizar-frontend` @`48f99c9`, Expo 57), `Encorly-landing` (`feat/reconstruction`), `EncorelyDjango`, `EncorelyPython` · **Modo:** solo lectura. El único archivo creado es este.

**Convención de orígenes:** `01-C1` = hallazgo C1 de `01-auditoria-externa.md`; `02-C5(S1)` = hallazgo C5 de `02-enjambre-qa.md` (grupo S1); `02-nat-7` = hallazgo nativo-7 del anexo Android de 02; `03-T10` = tell T10 de `03-auditoria-diseno.md`; `04-C3` = hallazgo C3 de `04-auditoria-movil.md`; `05-§x` = sección de `05-chat-e2ee-propuesta.md`. Las capturas citadas están en `reportes/capturas-qa/`.

---

## 1. Resumen ejecutivo

**Estado real:** Encorely es un prototipo que nunca cerró ninguna capa. No es seguro: cualquier JWT, que se obtiene gratis, lee y escribe los datos y chats de otro usuario. Tampoco funciona: el chat no envía, el ADN no cambia con los swipes, cada like crea un match con un desconocido y sin Spotify la app queda en spinners. No se puede publicar: nunca se compiló en nativo, depende de un login de Spotify limitado a 5 usuarios y no cumple los requisitos de las tiendas. Y no es mantenible: el esquema no tiene fuente de verdad, el contrato está escrito a mano en dos lados y la documentación describe otro sistema. El código es chico (back ≈ 3,7k LOC en 9 proyectos, front ≈ 8,7k LOC) y los datos son ficticios, así que reconstruir cuesta poco comparado con lo que cuesta refactorizar en el sitio.

**Los 5 problemas que más pesan:**
1. **La identidad sale del cliente y no del JWT** (GEN-01, GEN-02, GEN-03). Hay IDOR sistémico, el login con Google es falso y la sesión de 30 min no tiene refresh. Es la causa raíz de la mitad de los hallazgos de seguridad.
2. **El dominio está a medio hacer o simulado** (GEN-05, GEN-08, GEN-13). Perfil fijo en 0.5, matches sin reciprocidad ni estado, eventos hardcodeados y una UI que muestra cifras inventadas. El producto no hace lo que promete.
3. **El núcleo depende de APIs de Spotify que ya no sirven** (GEN-06). Login primario limitado a 5 usuarios, sin email y con `audio-features`, `recommendations` y `preview_url` deprecados. Hay que redefinir el modelo de "ADN musical".
4. **No hay fuente de verdad para el esquema ni para el contrato** (GEN-09, GEN-22). El drift produce 500 y el contrato roto deja el chat en 400.
5. **El móvil nativo no existe como producto** (GEN-10, GEN-11, GEN-12). No hay EAS ni dev build, la base es SDK 54 con `expo-av`, no hay push y faltan el borrado de cuenta, reportar/bloquear, la política de privacidad y el control de 18+.

**Veredicto:** **RECONSTRUIR** back, esquema, contrato, front, design system y chat, construyendo cortes verticales sobre una base nueva (.NET 10 y Expo 57). No hace falta big-bang de datos porque no hay datos reales que migrar (pendiente confirmarlo en producción). **CONSERVAR** piezas puntuales (§10). **REFACTORIZAR** solo la landing y la organización de repos.

---

## 2. Fuentes y método

| Reporte | Qué cubre | Hallazgos | Nivel de verificación |
|---|---|---|---|
| `01-auditoria-externa.md` | Sistema completo: seguridad, arquitectura back/front, contratos, BD, mensajería, UI, deps, DX, CI, docs, repos | 58 (17 C · 30 I · 11 M) + tabla de redundancias | **Estático** en su mayoría. Ejecutó `dotnet list package --vulnerable`, `npm audit`, `tsc` y tests, y levantó el sistema en local. C6, C11 y C13 están verificados en ejecución |
| `02-enjambre-qa.md` + `datos.json` + `ola2/nativo.json` | QA dinámico: 13 agentes (personas, seguridad, UI web) + anexo en **Android 15 emulado con Expo Go SDK 54** | 51 confirmados (8 C · 27 I · 16 M + 1 intermitente) deduplicados de 101 crudos; 0 descartados; anexo nativo: 12 hallazgos, 8 OK y 6 no probados | **Ejecución** contra API local (Development), BD local con seed ficticio, Worker activo, front web y emulador Android. iOS no probado. Spotify real no probado |
| `03-auditoria-diseno.md` | Identidad visual, tells "hechos con IA", auditoría por dimensión en 390×844, pantalla por pantalla (app + landing), dirección visual propuesta y design system | 19 tells + 11 dimensiones + 9 pantallas app + 9 secciones landing; propuesta "La boleta compartida" | **Estático + capturas** (`diseno-1…15`) y contraste calculado. Se ejecutó en un solo contexto (degradado, declarado) |
| `04-auditoria-movil.md` | Preparación móvil nativa: config Expo/EAS, SDK, auth nativa, push, navegación, a11y, red, tiendas, plan de testing, rama Expo 57 | 30 (8 C · 12 I · 10 M) + 16 repriorizaciones de 01/02 + checklist de tiendas | **Estático + herramientas** (`expo config`, `expo install --check`, `gh api` sobre la rama 57) + documentación oficial. **Sin dispositivo ni build nativo** |
| `05-chat-e2ee-propuesta.md` | Arquitectura del chat 1:1 E2EE: requisitos, amenazas, opciones (libsodium / Signal / MLS), contrato v2, SQL, hub, front, moderación con franking, fases | Propuesta (10 req. funcionales, 8 de seguridad, 8 fases); ~37 días-persona para v1 | **Diseño con fuentes**. No hay nada implementado ni probado. El riesgo técnico principal (`react-native-libsodium` en SDK 57) queda pendiente de un spike |

**Método de curaduría:**
- Leí los 5 reportes completos y el resumen estructurado de `datos.json` (52 ítems) y `nativo.json`.
- Unifiqué los hallazgos duplicados por **causa raíz** en IDs `GEN-xx`, indicando sus orígenes.
- **Repriorización por móvil nativo:** lo que solo pasa en web baja a menor (GEN-34). Lo que bloquea la publicación en tiendas sube.
- **Verifiqué contra el código** las contradicciones y las cifras dudosas: `git status`/`diff` de los 5 repos; `TokenService.cs`/`IdentityService.cs`; `Dockerfile`; uso de Redis y Kafka; `db/init/01_schema.sql`; conteos de a11y y colores en el front; `ChatScreen.tsx`/`LoginScreen.tsx` (teclado); y la vinculación de identidad en `IdentityService.cs:99`.

### 2.1 Contradicciones resueltas

| # | Contradicción | Resolución | Base |
|---|---|---|---|
| 1 | Logout roto: **crítico** (01-C14, 02-I21) vs funciona (02 nativo ok-1) | **Menor, solo web.** En nativo `Alert.alert` funciona y el logout vuelve al login | 02 `nativo-45.png`; 04 §4 coincide |
| 2 | Teclado del chat "mal compensado" (04-I3) vs "OK" (02 nativo ok-4) | **En Android funciona** (verificado en emulador, `nativo-31.png`). **En iOS sigue siendo sospecha fundada**: `ChatScreen.tsx:149` aplica `paddingBottom: insets.bottom` fuera del `KeyboardAvoidingView` (`:162-165`, offset fijo de 10/20). En cambio, el **teclado del login en Android sí está roto** (02-nat-2, `LoginScreen.tsx:112` `behavior: undefined`) | Código verificado |
| 3 | Botón BACK de Android "vuelve a la primera tab" (04-I1) vs "OK" (02 nativo ok-5) | **En Android funciona para chat→Conexiones y ajustes→perfil.** Lo que sigue vigente de 04-I1 es estructural: falta el swipe-back de iOS, no hay historial al abrir el chat desde una push en frío y la tab bar queda visible en ajustes | `nativo.json` ok-5 |
| 4 | `VenueRooms`: "faltan columnas" (01-C10/C9) vs "las columnas existen" (02-C3) | **Las dos son ciertas.** `db/init/01_schema.sql` (sin trackear) se **reconstruyó a partir del código** (sus comentarios citan `VenueRoomRepository.cs`), así que la tabla local "tiene" las columnas por construcción. El 500 viene de que Dapper no enlaza `@Description/@Capacity/@CreatedAt`, que no existen en el modelo. Consecuencia importante: **el esquema local no es la verdad; el de Supabase/producción no se conoce** | `01_schema.sql:60-90` |
| 5 | Expiración del JWT en `IdentityService.cs:85` (01) vs `TokenService.cs:39` (04) | **Las dos.** `TokenService` fija el `exp` real (30 min) e `IdentityService` reporta `Expiration` con otro `AddMinutes(30)`. Es el mismo número mágico en 4 sitios | grep verificado |
| 6 | Conteo de a11y: "~8 props / 84 táctiles" (01), "4+4 / ~47" (03), "8 / 75" (04) | **4 `accessibilityLabel` + 4 `accessibilityRole` en todo `src`** (grep). El total de táctiles varía según cómo se cuenten los wrappers de styled-components. La conclusión no cambia | grep verificado |
| 7 | `#F366FF` hardcodeado 81 (01) vs 83 (03) | **83** (grep sin distinguir mayúsculas) | grep verificado |
| 8 | "Expo Go no ejecuta SDK 54" (04-C1) vs "QA nativo hecho en Expo Go SDK 54" (02) | **No hay contradicción.** En Android se puede instalar el APK de Expo Go 54; en un iPhone físico no. Pero el QA nativo **no valida un build nativo**: el splash, el icono, los permisos, el push y el redirect `encorely://` siguen sin probar | `nativo.json` plataforma |
| 9 | Redis "ya está en el stack" y sirve de backplane (05-§5.6) | **Redis se usa en un solo lugar**: `IDistributedCache` en `UserService.cs:48,60`, que cachea un perfil. Quitarlo cuesta casi nada, y con una sola instancia el backplane no hace falta | grep verificado |
| 10 | 01-C4 "rotar la clave de Supabase ya" vs decisión del dueño | Queda **registrado como ítem** (GEN-04 y §12). No bloquea el plan | Decisión del dueño |
| 11 | 04 recomienda "partir de la rama 57" vs 01 "rama no auditada" | **Se usa la rama 57 como base de *toolchain*, no como base de `src/`** (ver D2). La rama solo validó la exportación del bundle y nunca se compiló en nativo | 04-I12, §7 |
| 12 | Estado del match: 3 estados (01-I24), `AcceptedAt` (02-I4), 4 estados con aceptación por usuario (05-§6.2) | Se adopta **el modelo de 05** (`Pending/Active/Closed/Blocked` + `User1AcceptedAt/User2AcceptedAt`), porque es el único que soporta consentimiento mutuo y bloqueo | — |

---

## 3. Scorecard general por capa

| Capa | Estado | Veredicto | Una línea |
|---|---|---|---|
| Backend · arquitectura | 🔴 | **Reconstruir** | 9 proyectos para 3,7k LOC, Clean Architecture declarada pero no aplicada, lógica duplicada entre controller y worker, Dockerfile que no arranca |
| Backend · dominio | 🔴 | **Reconstruir** | ADN fijo, afinidad sin poder discriminante, matches sin reciprocidad ni estado, eventos y feed hardcodeados |
| Backend · auth / identidad | 🔴 | **Reconstruir** | IDOR sistémico, Google falso, vinculación por email no verificado, sin refresh, sin rate limit |
| Backend · mensajería (Kafka, Worker, Redis, SignalR) | 🔴 | **Reconstruir simplificando** | Kafka sin garantías y con dual-write para un solo consumidor; Redis para un solo caché; hubs anónimos. Sobra para un equipo de 3 |
| Scripts de BD y esquema | 🔴 | **Reconstruir** (con el DDL local como inventario) | Migraciones borradas, DDL reconstruido sin trackear, drift que da 500, faltan constraints clave |
| Contrato y conexiones front↔back | 🔴 | **Reconstruir** (OpenAPI → cliente generado) | Tipos a mano en ambos lados, chat en 400, umbral 100 vs 25, `userId` viajando en cada request |
| Frontend · arquitectura, estado, navegación | 🔴 | **Reconstruir `src/`** sobre el toolchain de la rama 57 | 3 fuentes de sesión, guard imperativo, fetch manual, 31 `catch {}` vacíos, pantallas apiladas como tabs ocultas |
| UI/UX y design system | 🔴 | **Reconstruir** (dirección 03 sujeta a validación) | 3 marcas distintas, 83 hex sueltos, 2 sistemas de estilos, UI que muestra datos inventados, contraste por debajo de AA |
| Móvil nativo y distribución | 🔴 | **Construir** (no existe) | Sin EAS ni dev build, SDK 54 + `expo-av`, assets de plantilla, sin push y sin requisitos de tienda |
| Chat 1:1 (funcional + E2EE) | 🔴 | **Reconstruir** según 05 | Hoy no envía, guarda el texto en claro, cualquiera lo lee y no tiene tiempo real |
| Infra, CI, DX, docs | 🔴 | **Reconstruir** (mínimo y real) | El README no levanta el sistema, la IaC es decorativa, el CI usa el SDK equivocado y la documentación describe otro sistema |
| Organización de repos | 🟡 | **Refactorizar** | WIP sin commit en 3 repos, rama larga de otra persona, landing en cuenta personal, 2 repos muertos públicos |
| Landing (web) | 🟡 | **Refactorizar** | Funciona (waitlist idempotente y con key en el servidor), pero tiene estética de plantilla, promesas falsas y un input de 22 px en móvil |

---

## 4. Hallazgos consolidados priorizados

Verificación: **ejecución** = reproducido en vivo (API, navegador o emulador) · **estático** = leído en código, config o salida de herramienta · **sospecha** = implicado por el código o la documentación, sin confirmar en dispositivo o producción.

### 4.1 Críticos

**[GEN-01] IDOR/BOLA sistémico: la identidad viene del cliente y no se verifica la pertenencia** · Back · Orígenes: 01-C1, 01-C2, 02-C5(S1), 02-C8(S5), 02-I5(F15), 02-M10(S13), 05-§2.2 S2/S3 · **Ejecución**
- Evidencia:
  - Hay 0 lecturas de `ClaimTypes.NameIdentifier` en el API, aunque `TokenService.cs:30` lo emite.
  - Todos los controllers toman `userId` de la query o del body.
  - Reproducido con un JWT ajeno: leer `/User/me` de otro, cambiar su mood, sumarle swipes, leer y escribir el chat demo↔ana y suplantar al remitente (`ui3-9.png`).
  - Un `senderId` inexistente se persiste porque no hay FK.
  - El umbral de 25 se salta pasando el `userId` de otro, y `POST /Chat` no tiene umbral.
- Impacto: exposición total de datos personales y conversaciones; integridad nula de cualquier regla de negocio.
- Dirección: `CurrentUserId` desde el claim en una base `ApiController`. Contratos **sin** `userId`. `ChatAccessService`/autorización por recurso que devuelve 404 cuando no hay pertenencia. Test de arquitectura que falle si algún endpoint declara `userId`. Test de integración "B no ve recursos de A".

**[GEN-02] Autenticación falsificable y vinculación de cuentas insegura** · Back · Orígenes: 01-C3, 02-C7(S3), 04-I11, 04-C4 (parte) · **Ejecución** (Google) / **estático** (vinculación)
- Evidencia:
  - `POST /Auth/google {token:'x'}` devuelve 202 con un JWT y crea un usuario: `IdentityService.cs:52-58` ignora el `idToken`.
  - `IdentityService.cs:99` vincula por `GetBySpotifyIdAsync(id) ?? GetByEmailAsync(email)`, así que un email de Spotify no verificado entra a una cuenta de email ajena (verificado en código).
  - Google nativo no existe en el front.
- Impacto: toma de cuentas. Además anula el E2EE: un atacante que suplanta la cuenta publica su propia llave (05-S8).
- Dirección: validar el `idToken` (`GoogleJsonWebSignature.ValidateAsync`) o devolver 501 si no se implementa. Tabla `UserIdentities(Provider, ProviderSubject)` con vinculación **solo** por `(provider, sub)`. Sign in with Apple obligatorio si se ofrece Google (App Store 4.8).

**[GEN-03] Sesión sin refresh: JWT de 30 min, refresh en claro y sin consumidor, 401 sin manejo** · Back + front · Orígenes: 01-C12, 02-M9(S10), 04-C3 · **Estático + ejecución** (swagger sin `/Auth/refresh`)
- Evidencia:
  - `TokenService.cs:39` fija 30 min.
  - El refresh se guarda en claro y no hay endpoint que lo use.
  - El front descarta `refreshToken` y `http.ts` no intercepta el 401.
- Impacto: en móvil la app "muere" cada media hora y en silencio. Es **bloqueante en nativo** (04 §4). El hub de chat se reconectaría con un token vencido (05-§11).
- Dirección: access de 15 min + refresh rotativo con hash SHA-256, detección de reutilización y revocación al cerrar sesión o borrar la cuenta. Interceptor 401 *single-flight* en el cliente. Refresh en SecureStore.

**[GEN-04] Secretos: credencial de Supabase en historial público y secretos de fallback en código** · Back · Orígenes: 01-C4, 01-C5, 02-I14(S12), 01-I9 (parte) · **Estático**
- Evidencia:
  - `git show 955db69:DEPLOYMENT_INFO.md` contiene host, usuario y contraseña. El repo es público. Valor no transcrito.
  - Fallback del JWT secret en `Program.cs:132` y `TokenService.cs:20`.
  - Fallback de la password de BD en `Program.cs:35` y en las dos `DbConnectionFactory.cs:22`.
  - Contraseña del seed en un comentario de `db/init/02_seed.sql:2` (sin trackear).
  - `Trust Server Certificate=True` hardcodeado.
- Impacto: acceso a la BD de producción si la clave no se rotó. Tokens forjables si se despliega sin la variable.
- Dirección:
  - **Rotar la clave de Supabase cuando el dueño lo decida. Queda registrado, no bloquea el plan (§12).**
  - Secret scanning y push protection en la org.
  - `JwtOptions`/`DbOptions` con `ValidateOnStart` y sin fallback.
  - `SSL Mode=VerifyFull` en producción.
  - Quitar la contraseña del seed antes de commitear `db/`.

**[GEN-05] El núcleo de matchmaking no funciona: ADN fijo, match unilateral, afinidad sin poder discriminante y eventos falsos** · Back (dominio) · Orígenes: 02-C1(F12), 02-C4(F7), 02-I11(F8), 02-I12(F9), 02-M1(F10), 01-I2, 01-I4, 01-C15 (parte) · **Ejecución**
- Evidencia:
  - El perfil queda en 0.5/0.5/0.5 para siempre: el swipe no actualiza el ADN (`SwipeService.cs:34-47`, `KafkaConsumerWorker.cs:118-196`). Por eso la afinidad es 1.0 con el 78 % de la base.
  - Cada Like después de 25 swipes crea un **match con un desconocido sin reciprocidad** (`worker.log MATCH_CREATED`, `ui3-1.png`).
  - `IsHighPriority` compara `>= 85.0` contra una escala 0–1, así que nunca es true.
  - El radar ignora `eventId` (devuelve la misma respuesta para un evento real, uno inexistente u otro). El feed de eventos tiene fechas pasadas y está hardcodeado (`EventService.cs:9-17`). `next-track` es fijo.
  - El coseno sobre 3 rasgos no negativos da ≥0,95 en casi todos los pares.
- Impacto: el producto no hace lo que promete y genera contacto no consentido a escala.
- Dirección: **redefinir el dominio antes de programar** (PRODUCT.md):
  - Separar `Event` y `Attendance` ("voy").
  - Calcular `MusicProfile` en el servidor a partir de artistas y géneros (no de `audio-features`, ver GEN-06).
  - Separar `ConnectionRequest` (unilateral) de `Match` (mutuo).
  - Un único `RadarThresholdPolicy`, expuesto en `/me`.
  - Métrica de similitud discriminante y explicable (Jaccard ponderado de artistas/géneros o coseno centrado) con tests de propiedades.

**[GEN-06] Dependencia de Spotify inviable para una app pública; sin Spotify la app es un callejón sin salida** · Producto + back + front · Orígenes: 04-C4, 01-I20, 01-C13, 02-I19, 02-M14, 02-I25, 02-nat-3, 02-nat-4, 02-nat-8, 01-I10 (dna-mix) · **Ejecución** (callejón, en web y nativo) + **documentación** (política de Spotify, revisada por 04, no re-verificada aquí)
- Evidencia:
  - Spotify en modo desarrollo admite 5 usuarios por app y `/me` ya no devuelve email (desde feb/mar-2026).
  - `/recommendations` y `/audio-features` están deprecados para apps nuevas y `preview_url` llega en `null`.
  - Sin Spotify: "Preparando tracks…" infinito (`nativo-21.png`, `nativo-52.png`), perfil con spinners eternos (`nativo-34.png`), registro por email que siempre responde "Próximamente" (`nativo-5.png`, `nativo-11.png`) y "Conectado como Camilo Demo" sin Spotify (`nativo-36.png`).
  - `dna-mix` exige el token de Spotify del otro usuario.
- Impacto: imposible escalar más allá de 5 usuarios. El revisor de Apple o Google no puede entrar (Guideline 2.1). El "ADN musical" se apoya en datos que ya no existen.
- Dirección: **email (+ Apple/Google) como login primario y Spotify como conector opcional.** El feed de canciones se sirve desde el back con un catálogo propio de previews (Deezer o iTunes Search, revisando licencias, ver D11). El ADN se deriva de los swipes sobre ese catálogo y, si hay Spotify, de `/me/top`. Todo estado de carga termina en contenido, vacío o error con acción.

**[GEN-07] Chat 1:1 no funcional y en texto claro** · Back + front · Orígenes: 01-C11, 02-C2(F13), 02-M16, 02-M17, 02-M2(F17), 02-nat-7, 01-M6, 03 (chat), 05-§1 · **Ejecución** (web y Android)
- Evidencia:
  - El front manda `body: content` (un string) y el back espera `{content}`, así que todo envío da 400 (`ui3-2.png`).
  - El input se vacía y el mensaje desaparece sin error visible (`nativo-31.png`, `nativo-32.png`).
  - El mensaje de sistema `Guid.Empty` se pinta como burbuja del otro y se duplica en cada apertura (`nativo-g4.png`, `diseno-15.png`).
  - El GET y el POST devuelven formas distintas, no hay paginación ni tiempo real, y la tabla `Messages` guarda el texto en claro.
- Impacto: incumple el requisito 2 del dueño (chat funcional con E2EE).
- Dirección: reemplazar el chat completo según 05 (§8, Fase 3). No se parchea el chat v1.

**[GEN-08] Matches sin estado ni consentimiento mutuo: el chat se abre sin acuerdo de ambos** · Back + front · Orígenes: 01-I5, 02-I3(F14), 02-I4(F14), 02-I6(F16), 02-I24, 05-§2.1 F10 · **Ejecución**
- Evidencia:
  - `Matches` no tiene `Status`. `accept` no es idempotente: 5 llamadas dan 5 bienvenidas.
  - `/pending` incluye los matches ya aceptados.
  - Nombre "Match 2222" repetido (`MatchService.cs:40`, `ui3-1.png`).
  - La notificación se envía con `matchId` como si fuera un `userId`: el aceptante se notifica a sí mismo y la otra persona nunca se entera (verificado con un cliente SignalR propio).
  - La separación entre "Matches" y "Chats" vive en un `useState` local que se pierde al recargar.
- Impacto: rompe el consentimiento (requisito de una app de encuentros) y es prerrequisito duro del E2EE (`Status = Active`).
- Dirección: modelo de 05-§6.2 (`Pending/Active/Closed/Blocked` + aceptación por usuario, `UPDATE` condicional idempotente). `GET /matches` con `peer` real (JOIN). Notificar solo al otro usuario.

**[GEN-09] Esquema de BD sin fuente de verdad y con drift** · BD · Orígenes: 01-C9, 01-C10, 02-C3(F18), 01-I24, 02-I2(F11), 02-I9(F3), 02-I18(S9) · **Ejecución** (500 en salas, duplicados) + **estático**
- Evidencia:
  - Las migraciones EF se borraron en `7ee096c`. `db/init/*.sql` está reconstruido y **sin trackear**.
  - `POST /Venue/{id}/rooms` siempre da 500.
  - `ON CONFLICT ("UserId")` sin UNIQUE.
  - `Swipes` sin `UNIQUE(UserId,TrackId)`: 22 swipes sobre la misma canción cuentan para el umbral.
  - `UNIQUE(Email)` distingue mayúsculas: hay cuentas duplicadas por *casing* y el teclado móvil autocapitaliza.
  - No hay tabla `Events`. `Users.SpotifyId` guarda el email.
  - El esquema real de producción no se conoce.
- Impacto: imposible levantar un entorno reproducible. Bugs que ningún test detecta, porque los repositorios están mockeados.
- Dirección: migraciones versionadas desde el día 0 (D4), baseline nuevo diseñado (no copiado), constraints de dominio en BD, Testcontainers ejecutando todas las migraciones en CI y `snake_case` sin comillas.

**[GEN-10] El pipeline nativo no existe y la base SDK 54 no tiene salida** · Móvil · Orígenes: 04-C1, 04-C2, 04-I10, 04-I12, 02-nat-1, 01-I26 (parte) · **Estático + ejecución** (Expo Go)
- Evidencia:
  - No hay `eas.json` en `main` (verificado) ni `expo-dev-client`.
  - El API cae a `localhost` sin `EXPO_PUBLIC_API_URL`.
  - `expo-av` (eliminado en SDK 55) inyecta `RECORD_AUDIO` y `NSMicrophoneUsageDescription` sin usarlos.
  - Icono y splash de la plantilla de Expo (`nativo-start3.png`, `nativo-persist-montage.png`).
  - `com.encorely.app` sin confirmar.
  - La rama 57 nunca se compiló en nativo.
- Impacto: nadie puede probar en un dispositivo real. Rechazo por permisos y por assets *placeholder*. Quedarse en SDK 54 obliga a migrar dos veces.
- Dirección: Fase 0.6. Rama 57 + `expo-dev-client` + perfiles `development/preview/production` con `env`. Primer build Android en EAS instalado en un teléfono como criterio de salida.

**[GEN-11] Cumplimiento de tiendas y ley colombiana ausente** · Producto + back + front + landing · Orígenes: 04-C5, 04-C6, 04-C7, 01-M7, 05-§2.1 F7–F9, 04 §5 · **Estático**
- Evidencia:
  - No hay `DELETE /User/me`.
  - No hay reportar ni bloquear en ninguna parte.
  - Términos y Privacidad responden "Próximamente".
  - Se acepta una edad de 1 a 120 y se guarda solo en el dispositivo.
  - No hay consentimiento (Ley 1581/2012, Decreto 1377/2013) ni cuenta demo funcional.
- Impacto: **rechazo seguro** en App Store (1.2, 2.1, 5.1.1(v)) y en Google Play (UGC, borrado de cuenta dentro de la app y por web). Riesgo real de que entren menores a una app de encuentros.
- Dirección: Fase 3 (bloquear, reportar y borrar, integrados con el chat) y Fase 4: fecha de nacimiento con bloqueo a menores de 18, consentimiento versionado, política y términos publicados en la landing, formulario web de borrado, clasificación 18+ y cuenta demo.

**[GEN-12] Push inexistente de punta a punta, y el diseño previsto es inseguro** · Back + móvil · Orígenes: 04-C8, 02-I6(F16), 01-C15 (parte), 05-§5.6 · **Estático**
- Evidencia:
  - No hay `expo-notifications`. El envío FCM está comentado (`FirebasePushNotificationService.cs:35`) y usa `Topic = userId`, que cualquier cliente puede suscribir.
  - No existe una tabla de *device tokens*.
  - El fallo de Firebase se oculta (`Program.cs:41-48`).
- Impacto: en una app de matches y chat, sin push no hay conversación posible.
- Dirección: Expo Push API con tokens por dispositivo (nunca *topics*), payload sin contenido (05-§5.6), permiso pedido en contexto y deep link con arranque en frío.

**[GEN-13] La UI muestra datos inventados y controles placebo** · Front · Orígenes: 01-C15, 01-I18, 01-M5, 02-I22, 02-I23, 02-I26, 02-I27, 02-nat-5, 02-nat-6, 02-nat-8, 03-T10, 03-T16, 03-T18, 04-M4 · **Ejecución**
- Evidencia:
  - "247 personas con tu vibe cerca" hardcodeado, frente a "8 personas" en la lista (`nativo-22.png`, `nativo-24.png`).
  - "Alcanzaste el umbral de 100 swipes" con 31 swipes.
  - 100 % y "Match Certificado" para todos.
  - "Pasar" no hace nada y "Conectar" solo cambia el texto (`nativo-25.png`, `nativo-26.png`).
  - Los toggles de privacidad solo escriben en AsyncStorage.
  - Botón "Reset Swipes (Debug)" visible. Caras falsas de `pravatar.cc`.
- Impacto: destruye la confianza. En tienda cuenta como contenido engañoso.
- Dirección: regla de producto "**nada que no exista se muestra**". Lo que no está implementado se oculta. Los mocks solo van como implementaciones fake registradas en Development.

**[GEN-14] Hubs de SignalR anónimos y con identidad elegida por el cliente** · Back · Orígenes: 01-C16, 02-C6(S2), 04-I9, 05-§5.6 · **Ejecución** (`negotiate` responde 200 sin token)
- Evidencia: `NotificationHub.JoinUserGroup(userId)` y `VenueHub.SendVenueMessage(roomId, userId, …)` no tienen `[Authorize]`. Hacen broadcast sin persistir ni moderar. El front no los usa.
- Impacto: cualquiera puede escuchar las notificaciones de otro o publicar en salas.
- Dirección: quitar los `MapHub` ya. En la Fase 3 entra solo `ChatHub` con `RequireAuthorization()`, `OnMessageReceived` limitado a `/hubs` y sin métodos de cliente (05-§6.3).

**[GEN-15] El backend no se puede desplegar tal como está commiteado** · Back + infra · Orígenes: 01-C6, 01-C7, 01-C8, 01-I25 · **Estático** (C7 y C8) + **ejecución** (C6 en HEAD, según 01)
- Evidencia:
  - `Dockerfile:8,37` usa `sdk:9.0`/`aspnet:9.0` con proyectos `net8.0` (verificado).
  - El Worker no tiene imagen.
  - HEAD da 500 en todo POST/PUT con body por `[property: Required]` en records posicionales. El parche correcto existe **sin commit** (`git diff Requests.cs`, verificado).
  - El `ProducerConfig` registrado no se usa: `KafkaProducer.cs:19` cae a `localhost:9092`, así que fuera de local cada swipe espera el timeout con la BD ya modificada (dual-write).
  - El CI usa el SDK 9 y no hace `docker build`.
- Impacto: producción (Render) probablemente rota o desactualizada (no auditada).
- Dirección: en la reconstrucción, una sola imagen .NET 10 y `docker build` en CI desde el día 0. El legado solo se toca si hay una entrega académica antes (ver D16).

### 4.2 Importantes

**[GEN-16] Arquitectura del back sobredimensionada y mal estratificada** · Back · Orígenes: 01-I1, 01-I2, 01-I3, 01-I7, 01 §5 · **Estático**
- 9 proyectos para unas 3,7k LOC.
- `Application` depende de Dapper/Npgsql. `EncorelyDomain` está vacío y fuera del `.sln`, y `EncorelyModels` usa el namespace `EncorelyDomain.*`.
- Lógica en los controllers y duplicada en el Worker.
- N+1 de hasta 501 queries por like, y `SELECT *` que carga `PasswordHash` y `RefreshToken` de cientos de usuarios.
- Alta de usuario en 3 sentencias sin transacción y *lost update* en `UpdateAsync`.
- Dirección: monolito modular de 2–3 proyectos con carpetas por feature, transacciones explícitas, proyecciones con columnas explícitas y un test de arquitectura.

**[GEN-17] Mensajería sin garantías y piezas sin justificación** · Back · Orígenes: 01-C8, 01-I6, 05-§5.6; verificación propia (uso de Redis) · **Estático**
- `EnableAutoCommit=true` con errores que solo se loguean (at-most-once accidental). Sin DLQ, sin idempotencia y con key `Null`.
- 3 topics sin consumidor: `UserDnaSync`, `MatchConvertedToChat` y `VenueModerationFlagged`.
- El **access token de Spotify viaja en claro** por Kafka (`IdentityService.cs:143`).
- Dos interfaces de productor equivalentes.
- Redis se usa solo para cachear un perfil.
- Dirección: eliminar Kafka, Redis y el Worker como proceso aparte (D3). Tabla *outbox* + `BackgroundService` en el mismo proceso si hace falta asincronía.

**[GEN-18] Sin capa de validación ni mapeo de errores: 500 por input inválido** · Back · Orígenes: 01-C6 (pendiente), 01-I7, 02-I1(F1), 02-I8(F2), 02-I10(F6), 02-I16(S6), 02-M6(F4), 02-M7(F5) · **Ejecución**
- Enums fuera de rango dan 500 por la CHECK.
- Un FK inexistente da 500. Un registro concurrente da 1×202 y 7×500.
- Omitir `mood` lo resetea a 0.
- bcrypt trunca en 72 bytes sin `MaxLength`.
- Hay 2 formatos de error con mensajes en inglés, y register/login responden 202.
- La UI muestra crudo "API 401: Unauthorized" (`diseno-12.png`).
- Dirección: validación en el borde (FluentValidation o DataAnnotations con enums nullables), `ProblemDetails` + `IExceptionHandler`, excepciones de dominio tipadas, mapeo de `23505/23503/23514` y copy de error humano en español.

**[GEN-19] Autenticación por debajo de ASVS L1** · Back · Orígenes: 01-I8, 02-I17(S8), 02-I9(F3), 02-I18(S9), 02-M6(F4) · **Ejecución**
- Sin rate limit ni lockout.
- Oráculo de tiempo: 1,7 ms si el email no existe y 136 ms si existe (`ui3-5.png`). El 409 del registro enumera emails.
- `MinLength(6)` y email sin `trim` ni `lower`.
- Dirección: `AddRateLimiter` en `/auth/*`, verificación contra un hash ficticio, respuesta uniforme en el registro (enviar el aviso por correo), mínimo de 10–12 caracteres con un máximo de 72 bytes, email normalizado + `citext` o índice sobre `lower()`.

**[GEN-20] Superficie operativa expuesta** · Back · Orígenes: 01-I9, 02-M11(S7) · **Ejecución**
- Swagger en producción.
- `/health` anónimo que devuelve `ex.Message` de Postgres y es la liveness de k8s.
- Sin cabeceras de seguridad, `Server: Kestrel`, sin `UseForwardedHeaders`.
- Firebase falla en silencio.
- Dirección: `/health/live` y `/health/ready` sin detalle, Swagger solo en Development, cabeceras de seguridad, HSTS y logs que enmascaren `access_token`/`authorization`.

**[GEN-21] Salas de evento: moderación ingenua y sin permisos** · Back · Orígenes: 01-I10, 02-I7(F19), 02-I15(S4), 02-M3, 02-M4, 02-M5(F20), 02-M8, 02-M10(S13) · **Ejecución**
- El filtro por *substring* censura "episodio", "podio" y "melodiosa", y deja pasar "s p a m", "sp4m" y homoglifos.
- `DELETE` "admin" sin rol.
- Lectura sin ser miembro.
- Un mensaje moderado responde 200, igual que uno publicado.
- La entidad se devuelve cruda con `senderId`.
- `durationHours` no tiene límites.
- Dirección: posponer las salas a la Fase 6 (D13). Cuando entren: `[Authorize]`, membresía, rol de moderador, moderación por palabra completa normalizada, 422 al bloquear, DTO de vista y etiqueta "Sala pública · no cifrada" (05-§8.5).

**[GEN-22] Contrato front↔back implícito y divergente** · Contrato · Orígenes: 01 (scorecard de contratos), 01-I13, 02 (causas comunes), 02-I22, 03 (coherencia), 01 §5 · **Ejecución** (umbral) + **estático**
- Tipos escritos a mano en ambos lados. `FrontendDtos.cs` es un segundo contrato muerto.
- Umbral de swipes: 100 en el front, 25 en el back (repetido ×4) y 25 en la landing.
- Escalas 0–1 vs 0–100. Enums como int y como string mezclados.
- Endpoints sin consumidor y consumidores sin endpoint.
- Dirección: **OpenAPI como fuente** (generado por .NET 10) → cliente TS generado en CI (`openapi-typescript` + `openapi-fetch` u `orval` con hooks de TanStack Query), con un job que falle si el cliente commiteado difiere. Valores de negocio (umbral) servidos por la API, nunca duplicados.

**[GEN-23] Arquitectura del front: estado duplicado, errores tragados y sin capa de red** · Front · Orígenes: 01-I11, 01-I12, 04-I5, 04-I7 · **Estático + ejecución** (spinners)
- 3 fuentes de sesión (`AuthContext.user`, `SpotifyAuthContext.user`, `backendUserId`).
- `NavigationGuard` imperativo que compara strings.
- `useState+useEffect` en cada pantalla, 31 `catch {}` vacíos y `fetch` sin timeout: el *cold start* de Render deja spinners colgados.
- El splash se oculta antes de conocer la sesión: destello del onboarding.
- En iOS el Keychain sobrevive a la reinstalación y AsyncStorage no, lo que deja estados incoherentes.
- Dirección: `SessionContext` con unión discriminada + `Stack.Protected`, TanStack Query (con `onlineManager` y `focusManager`), `AbortSignal.timeout(15000)`, splash retenido hasta resolver la sesión y detección de primera instalación.

**[GEN-24] Navegación y layout nativos incorrectos** · Front · Orígenes: 04-I1, 04-I2, 04-I3, 02-I20, 02-nat-2, 02-nat-9, 01-M8 · **Ejecución** (Android) / **sospecha** (iOS)
- Chat, ajustes y radar-matches son tabs ocultas: no hay swipe-back en iOS, la tab bar sigue visible y no hay historial desde una push.
- La tab bar flotante tapa el último ítem (`nativo-22.png`, `nativo-28.png`, `nativo-36.png`).
- **En Android el teclado del login tapa los campos y concatena la contraseña al email** (`nativo-18.png`, `nativo-51.png`).
- Lo que está OK en Android: BACK y teclado del chat (§2.1).
- Dirección: `Stack` raíz con `(tabs)` anidado, tab bar nativa no flotante y `react-native-keyboard-controller`.

**[GEN-25] El estado del dispositivo se filtra entre usuarios** · Front · Orígenes: 01-I14, 01-I13 (parte) · **Estático**
- `logout` no borra el estado de swipes, el perfil local (edad, género, bio), los toggles ni el onboarding.
- El contador de swipes vive en el dispositivo y tiene un *closure* obsoleto.
- Dirección: claves con namespace por `userId`, `clearUserScopedStorage()` y cero estado de negocio en el dispositivo (todo sale de la API).

**[GEN-26] Accesibilidad nativa insuficiente** · Front + UI · Orígenes: 01-I16, 03 (a11y y contraste), 04-I6, 02-M13, 02-nat-10, 02-nat-11 · **Ejecución** (uiautomator, font scale 1.3) + contraste calculado
- El swipe solo funciona con gesto (WCAG 2.5.7).
- 4 labels y 4 roles en todo `src`.
- Botones de icono sin `content-desc`, de 24–40 dp.
- Tabs y hora del chat a 10 px.
- Blanco sobre `#F366FF` = 2,57 y hora sobre la burbuja = 1,43.
- 0 usos de `useReducedMotion`.
- Con fuente al 130 % se cortan textos (`nativo-font13-montage.png`).
- Dirección: primitivas accesibles por defecto (03-§6.2), botones ✕/♥ con `accessibilityActions`, objetivos de 44 pt / 48 dp, paleta verificada de 03-§5.2 y pruebas con VoiceOver/TalkBack y fontScale 2.0.

**[GEN-27] Identidad visual genérica "hecha con IA" y sin design system** · UI/UX · Orígenes: 01-I17, 03-T1…T19, 03 §3–4, 04-I8, 04-M3, 01-M7 (copy), 02-M7 (idioma) · **Estático + capturas** (`diseno-1…15`)
- 3 marcas distintas: login rojo/violeta, app magenta, landing violeta→magenta.
- 83 `#F366FF` sueltos, gradiente morado copiado en 10 pantallas, glassmorphism, glow y unos 30 emojis haciendo de iconos.
- 17 tamaños de fuente y 20 radios distintos.
- styled-components y StyleSheet conviven (origen de 83 de los 85 errores de `tsc`).
- Copy con voseo rioplatense, inglés de feature y jerga ("Similitud coseno >70 %").
- Nielsen 12/40 y audit 6/20 (app).
- Impacto: incumple el requisito 3 del dueño.
- Dirección: Fase 1 con la dirección de 03 si se valida (D5), `StyleSheet` + tokens tipados, Phosphor y guía de voz en tuteo colombiano.

**[GEN-28] Audio de las previews: API muerta y mal comportamiento en nativo** · Front · Orígenes: 04-I4, 04-C1 (parte), 01-M8 · **Estático**
- `expo-av`.
- El audio sigue sonando al cambiar de tab: no hay `useIsFocused` en todo `src`.
- `playsInSilentModeIOS: true`, así que suena en silencio en un concierto o en clase.
- Sonido huérfano si la tarjeta se desmonta.
- `setState` cada 250 ms.
- Dirección: `usePreviewAudio` de la rama 57 + foco, respeto del modo silencio, sin autoplay hasta el primer toque y progreso en un `SharedValue`.

**[GEN-29] Calidad estática y tests que no protegen** · Front + back · Orígenes: 01-I19, 01-I26, 01-I29, 04-M1 · **Ejecución de herramientas**
- 85 errores de `tsc`. Sin ESLint ni Prettier.
- Hooks dentro de `.map` (`SwipeCard.tsx:141`).
- Los 52 tests pasan, pero ninguno cubre SQL, autorización ni integración: no detectan ninguno de los críticos.
- 2 lockfiles. `.npmrc` con `prefix=/home/edav404/...`.
- `lottie-react-native` y `victory-native` instalados sin uso.
- Dirección: estrategia de tests por riesgo (integración con Testcontainers, autorización, contrato y E2E con Maestro), lint y typecheck bloqueantes en CI y un solo gestor de paquetes (pnpm, ya adoptado en la rama 57).

**[GEN-30] Plataforma y dependencias al borde** · Back + front · Orígenes: 01-I30, 01-C17, 04-M2 · **Ejecución de herramientas**
- .NET 8 llega a EOL el **10-nov-2026** (en 4 semanas).
- Npgsql 8.0.0 con una CVE alta. `SignalR.Core` 1.2.0 redundante y con advisory.
- Confluent, Dapper, Swashbuckle y JWT desactualizados.
- APIs deprecadas de Reanimated 4 (`runOnJS`).
- Landing con `react-router` vulnerable.
- Dirección: reconstruir sobre .NET 10 LTS (D1). Dependabot/Renovate en los repos.

**[GEN-31] DX, configuración y documentación que no corresponden a la realidad** · Infra/DX · Orígenes: 01-I21, 01-I22, 01-I23, 01-I28, 01-M11 · **Ejecución** (vivido en la sesión de 01)
- El README no levanta el back: 8 pasos fallan.
- PROJECT_STATUS habla de .NET 9, EF Core y "distancia euclidiana".
- La connection string está ×3 y la config de Kafka ×3.
- Variables usadas sin documentar.
- Sin pin de runtimes. 2 composes.
- k8s y terraform son de un ejercicio académico ("Tarea 82/83") y no despliegan nada.
- Dirección: runbook de 5 comandos, un compose con profiles, `.env.example` completo por app, `mise.toml` + `global.json`, IaC real mínima (`render.yaml` o equivalente) y la IaC académica movida a `docs/academic/`.

**[GEN-32] Repos con trabajo divergente y propiedad difusa** · Organización · Orígenes: 01-I27, 04-I12, 01-M10; `git status` verificado · **Ejecución** (git)
- Back: `Requests.cs`, `DnaMixRequest.cs` y `.gitignore` sin commit; `db/` y `docker-compose.local.yml` sin trackear.
- Front: 5 archivos sin commit (login por email, `setApiToken`).
- La rama `codex/actualizar-frontend` (Expo 57) es de otra persona (`edav404`) y está sin merge.
- Landing: `feat/reconstruction` con un commit sin push, `vite.config.ts` modificado, repo en cuenta personal y Vercel en otra cuenta personal (`jdicans`).
- `.gitignore` modificado también en Django y Python.
- 2 repos muertos y públicos.
- Dirección: D9 y D14.

**[GEN-33] Landing: estética de plantilla, promesas falsas y un fallo de conversión en móvil** · Landing · Orígenes: 03 §4.2, 03-T5…T8, T15, 01-M9, 01-C17 (react-router) · **Ejecución** (capturas `diseno-1…11`, 109 animaciones medidas)
- **El input del waitlist mide 22 px de alto en móvil** (`WaitlistForm.tsx:85`).
- Texto con gradiente animado, mockups 3D que no corresponden a la app, 109 animaciones activas y `GlobalBackground` con auroras.
- Promete una integración con Ticketmaster que no existe.
- Cifras "4D/100 %/$0". Redes con `href="#"`.
- El token de Cloudflare es un placeholder. El honeypot solo existe en el cliente.
- Dirección: Fase 5. Además, la landing debe alojar la política, los términos y el formulario de borrado que exige Play.

### 4.3 Menores

**[GEN-34] Problemas que solo ocurren en web (la web de Expo no es objetivo)** · Front · Orígenes: 01-C14, 01-I15, 02-I13(S11), 02-I21, 02-M12, 03-§3.1, 04 §4 · **Ejecución**
- `Alert.alert` no hace nada en web: logout y confirmaciones invisibles.
- JWT en `localStorage`. Sin `maxWidth` en desktop.
- Dirección: **no invertir.** Desactivar el target web de Expo o dejarlo fuera de soporte. `Alert` se reemplaza igual por el `Toast`/`Dialog` propio del design system.

**[GEN-35] Código muerto y fósiles** · Back + front · Orígenes: 01-M1, 01-M2, 03-§6.3 · **Estático**
- 111 archivos en `obj_old/net9.0`.
- `AuthService`, `INotificationClient` y `FrontendDtos.cs`.
- `HomeScreen` (462 líneas) y `OnboardingScreen` (443) sin uso.
- `useSwipes`, `useMatches`, endpoints del cliente sin llamadores.
- GUIDs inválidos en el `.sln`.
- Dirección: desaparecen con la reconstrucción. No se migran.

**[GEN-36] Números mágicos y nombres inconsistentes** · Back + front · Orígenes: 01-M3, 01-M4 · **Estático**
- 25, 0.70, 85.0, 30 min, 7 días, `LIMIT 500`.
- Typos en los nombres de repos (`encorly_back`, `Encorly-landing`, `encorly-back.onrender.com`).
- `Usuario` vs `Users`.
- Dirección: políticas nombradas en el dominio. Renombrar repos al consolidar (D14).

**[GEN-37] Detalles nativos de rendimiento y arranque** · Móvil · Orígenes: 04-I8, 04-M4, 04-M5, 04-M6, 04-M8, 04-M9, 04-M10, 04-M7, 02-nat-12 · Mixto: **ejecución** (flash blanco en Expo Go) / **sospecha** (resto)
- `BlurView` sin efecto en Android.
- Imágenes sin caché (`expo-image`). Fuentes cargadas en runtime (7 pesos).
- PKCE no se reanuda en arranque en frío.
- iPad en modo compatibilidad.
- Permisos de la plantilla de Android.
- Haptics sin uso semántico en el match.
- Flash blanco al abrir y unos 11–13 s de arranque en dev (`nativo-persist-montage.png`).
- Dirección: checklist de la Fase 4 (verificar en un *dev build*, no en Expo Go).

### 4.4 Sospechas abiertas (no verificadas en ejecución)

| Sospecha | Origen | Cómo cerrarla |
|---|---|---|
| Producción (Render/Supabase) rota: Kafka ausente, timeouts de 300 s, imagen .NET 9 | 01-C7/C8 | Revisar el dashboard de Render y sus logs (fuera de alcance) |
| Vercel sin deploy desde 05-29 por el lockfile | 01-I25 | Panel de Vercel |
| El redirect `encorely://spotify-callback` lo rechaza el dashboard de Spotify | 04-I11 | Primer *dev build* |
| Hueco de unos 34 pt entre el input del chat y el teclado en iOS | 04-I3 | *Dev build* iOS (TestFlight) |
| `react-native-libsodium` compila en SDK 57 / New Architecture | 05-§7.1 | Spike del primer día de la Fase 3 (plan B: noble) |
| Supabase con RLS y columnas que coinciden con Dapper | 01 §8 | Leer el esquema de producción (solo lectura) |

---

## 5. Causas raíz

1. **Se confía en el cliente para la identidad y las reglas.** `userId` en cada request, umbral calculado en el front, contador de swipes local y hubs que reciben el `userId`. Explica GEN-01, 02, 08, 14, 21 y 25, y obliga a reconstruir el contrato.
2. **No hay fuentes de verdad versionadas** (esquema, contrato, config, tokens de diseño, documentación). Migraciones borradas, tipos a mano, config ×3, 3 temas y docs aspiracionales. Explica GEN-09, 22, 27, 31 y 36.
3. **El dominio nunca se especificó; se programó a partir de demos.** Stubs (eventos, next-track, perfil 0.5) que llegaron a "producción", match unilateral y métrica sin calibrar. Explica GEN-05, 08 y 13.
4. **El producto se construyó sobre una dependencia externa que cambió** (Spotify como login, perfil y feed). Explica GEN-06 y parte de GEN-05 y GEN-11.
5. **Se eligió la arquitectura por currículo, no por problema.** Kafka, Redis, Worker, 9 proyectos, k8s y terraform para un CRUD con un consumidor. Agrega superficie de fallo sin valor (GEN-15, 16, 17 y 31).
6. **No hay pruebas de lo que importa ni gates en CI.** Tests con mocks, sin integración, sin autorización y sin dispositivo. Nada impidió que llegaran a main un 500 en todo POST, un INSERT inválido o un chat en 400 (GEN-15, 18 y 29).
7. **Se diseñó y probó para web aunque el objetivo era nativo.** Nunca se hizo un build nativo, se validó en el navegador ("fix chat keyboard" se probó en web), y no se tuvieron en cuenta push, tiendas ni ciclo de vida de la app (GEN-10, 11, 12, 24 y 28).
8. **UI generada sin dirección ni dueño de producto.** Pantallas hechas por separado, con relleno plausible y sin voz (GEN-13, 26 y 27).

---

## 6. Evaluación por capa

### 6.1 Backend · arquitectura: **RECONSTRUIR**
- **Por qué no refactorizar:** 3,7k LOC repartidas en 9 proyectos con dependencias invertidas. Arreglar el IDOR toca todos los controllers y DTOs, y arreglar el dominio toca todos los servicios. Lo que se salva cabe en un día de copiar y pegar.
- **Nueva forma:** **monolito modular en .NET 10 LTS**, con un único deployable.
  - `Encorely.Api`, con features en carpetas: `Auth`, `Users`, `Music`, `Events`, `Matching`, `Chat`, `Moderation`, `Devices`.
  - `Encorely.Tests` (unit + integración con Testcontainers + arquitectura).
  - Opcional: `Encorely.Domain` puro si la rúbrica lo exige.
  - Controllers o Minimal APIs con `ProblemDetails`, `IOptions` + `ValidateOnStart` y `OpenApi` nativo.
- **Se conserva:** SQL parametrizado (o EF), BCrypt, la emisión de claims de `TokenService` (`NameIdentifier`), la idea de `ExceptionMiddleware` (rehecho como `IExceptionHandler`), el Dockerfile multi-stage con usuario no root y CORS con orígenes explícitos.

### 6.2 Backend · dominio: **RECONSTRUIR** (empieza en papel)
- Escribir `PRODUCT.md` con el glosario y las reglas antes de tocar código:
  - Usuario (18+). MusicProfile (derivado en el servidor). Track (catálogo propio).
  - Swipe (idempotente por usuario y track). Event. Attendance.
  - ConnectionRequest. Match (mutuo, con estado). Block. Report.
  - Un solo umbral (25 o el que se decida) servido por la API.
- La métrica de compatibilidad debe ser explicable al usuario ("los dos escuchan Bomba Estéreo"). Eso también sirve al copy de 03-§5.7.
- **Se conserva:** el concepto de producto (swipe → ADN → radar por evento → match → chat) y las constraints del modelo Django (`unique_together`) como referencia.

### 6.3 Backend · auth e identidad: **RECONSTRUIR**
- Email + contraseña como camino primario. Sign in with Apple + Google con validación del token. Spotify como **conector** (OAuth PKCE en el cliente e intercambio de tokens guardados cifrados en el servidor solo si se usa `/me/top`).
- `UserIdentities(provider, subject)`. Refresh rotativo con hash. Revocación. Rate limiting. Normalización.
- **Se conserva:** el flujo PKCE S256 del front (`SpotifyAuthContext`), que está bien hecho, y SecureStore en nativo.

### 6.4 Backend · mensajería: **RECONSTRUIR SIMPLIFICANDO**

| Pieza | Uso real hoy | Costo para un equipo de 3 | Veredicto |
|---|---|---|---|
| **Kafka** | 1 consumidor (swipe → match) + 3 topics huérfanos | Broker administrado de pago o 3 contenedores en local; dual-write; semántica de offsets; nadie lo opera | **Eliminar.** El volumen (miles de swipes al día) lo resuelve Postgres. Si se necesita asincronía: tabla `outbox` + `BackgroundService` en el mismo proceso. Si la **rúbrica** exige un broker, confinarlo a analítica fuera del camino crítico |
| **Redis** | `IDistributedCache` de un perfil | Otro servicio más que desplegar y asegurar (hoy sin auth) | **Eliminar.** `IMemoryCache` si hace falta. Reconsiderarlo solo con más de 1 instancia del API (backplane de SignalR) |
| **Worker** (proceso aparte) | Consumir swipes y crear matches | Otra imagen, otro deploy, DI duplicado | **Eliminar como proceso.** La lógica va a `Matching` (síncrona o `BackgroundService`) |
| **SignalR** | Hubs anónimos que nadie consume | Bajo (viene en ASP.NET Core) | **Conservar solo `ChatHub` autenticado en la Fase 3**, como optimización: la fuente de verdad es el inbox REST + push (05-§5.6) |
| **Firebase Admin** | Push comentado | SDK de iOS si se usa FCM directo | **Reemplazar por Expo Push API** (HTTP, cubre APNs y FCM) |

### 6.5 Scripts de BD y esquema: **RECONSTRUIR**
- Baseline nuevo diseñado a partir del dominio de 6.2, no copiado: `snake_case`, `citext` para el email y constraints de negocio (UNIQUE de swipes, estado del match, CHECK de la pareja ordenada).
- Migraciones versionadas (D4), seeds de desarrollo separados y sin secretos, y Testcontainers en CI.
- **Se conserva:** `db/init/01_schema.sql` como **inventario** de lo que el código usaba (útil para no olvidar columnas) y el seed ficticio como fixture inicial, sin la contraseña en el comentario.
- La tabla `Messages` no se migra (D7).

### 6.6 Contrato y conexiones front↔back: **RECONSTRUIR**
- OpenAPI generado por el back → cliente TS generado y commiteado en el monorepo, con un check en CI.
- Versionado `/api/v1` nuevo (el `v2` de 05 se vuelve `v1` del sistema nuevo).
- Errores `ProblemDetails` con un `code` estable que el front traduce. Paginación por cursor. Sin `userId`.
- URL del API por perfil de EAS (`EXPO_PUBLIC_API_URL`) que **falla de forma ruidosa** si falta. Staging por HTTPS (no `http://IP-LAN`).
- **Se conserva:** el patrón `apiRequest`/`ApiError` testeado, como envoltorio del cliente generado.

### 6.7 Frontend · arquitectura, estado y navegación: **RECONSTRUIR `src/`** sobre el toolchain de la rama 57
- **Estructura:**
  - `app/` con `Stack` raíz; `(auth)` y `(app)` protegidos con `Stack.Protected`; `(app)/(tabs)` con 4 tabs nativas (*Descubrir · Conciertos · Chats · Tú*); chat, ajustes y verificación como pantallas del Stack.
  - `features/*` (screens, hooks y api por feature).
  - `ui/` (design system).
  - `lib/` (sesión, almacenamiento seguro, red).
- **Estado:**
  - Sesión en un `SessionContext` con unión discriminada.
  - Estado de servidor en TanStack Query.
  - En el dispositivo, solo tokens (SecureStore) y preferencias de UI con namespace por usuario.
- **Se conserva:**
  - `secureStorage.ts` nativo y la detección de reinstalación (añadir).
  - `haptics.ts`.
  - El gesto de swipe con Reanimated 4 + Gesture Handler (rehecho con `scheduleOnRN`, `useWindowDimensions` y `activeOffsetX`).
  - `usePreviewAudio` de la rama 57.
  - PKCE.
  - Los 6 tests del audio de la rama.

### 6.8 UI/UX y design system: **RECONSTRUIR**
- La dirección "La boleta compartida" de 03 es la mejor propuesta encima de la mesa:
  - Nace del dominio (boleta, manilla, setlist).
  - Su paleta tiene ratios WCAG verificados.
  - Trae una escala tipográfica nativa con Dynamic Type.
  - La verificación E2EE encaja como "número de serie".
  - El sistema es mínimo: 3 radios y 7 pasos de espaciado.
- **Mi crítica:**
  - (a) Hay que validarla con usuarios reales antes de invertir (03-F0, 2 días).
  - (b) La muesca debe limitarse a 2–3 componentes o se vuelve disfraz, cosa que 03 ya advierte.
  - (c) Big Shoulders es condensada y solo funciona desde 22 pt; hay que vigilar la legibilidad de nombres de artistas largos con fontScale alto.
  - (d) El paquete de tokens compartido app↔landing justifica el monorepo.
- **Se conserva:** la propuesta de 03 completa como especificación (§5–6), el inventario de tells como checklist de "lo que no se hace" y los quick wins de copy.

### 6.9 Móvil nativo y distribución: **CONSTRUIR**
- Expo SDK 57 / RN 0.86, `expo-dev-client`, EAS Build con perfiles `development/preview/production` y `appVersionSource: remote`, EAS Update con `runtimeVersion: appVersion`, y Maestro en EAS Workflows.
- `expo-audio` sin micrófono, `expo-notifications`, `expo-image`, `expo-system-ui` y fuentes incrustadas.
- Distribución: prueba cerrada en Play (12 testers × 14 días si la cuenta es personal) y TestFlight. Sin Mac: iOS por EAS + TestFlight.
- **Se conserva:** de la rama 57, `package.json`/lockfile, CI JS, `.npmrc` saneado y la base de `eas.json`.

### 6.10 Chat 1:1 (funcional + E2EE): **RECONSTRUIR según 05**
- Adopto la propuesta de 05 casi sin cambios. Opción A con libsodium `crypto_box`, llaves por dispositivo y verificación por QR. Store-and-forward con borrado tras el ACK e historial en SQLCipher. REST como fuente de verdad, `ChatHub` autenticado como optimización y push sin contenido. Franking para reportes verificables. Un dispositivo activo en v1.
- **Ajustes de curaduría:**
  - (1) La "fase 0" de 05 (saneamiento: `CurrentUserId`, `JwtOptions`, Google y `Matches.Status`) **se absorbe en las Fases 0 y 2 de este plan**. El chat solo agrega el retiro del chat v1.
  - (2) La entrega de `/api/v2` de 05 pasa a ser el `v1` del sistema nuevo.
  - (3) Sin Redis: el backplane solo hace falta con más de 1 instancia.
  - (4) La tarjeta local "Hicieron match en <evento>" depende del modelo de evento de la Fase 2.
  - (5) Spike de libsodium **el primer día** de la Fase 3, con un plan B (`@noble/*`) cruzado contra los mismos vectores.
  - (6) El texto de producto dice "cifrado de extremo a extremo" y **nunca** "como Signal".

### 6.11 Infra, CI, DX y docs: **RECONSTRUIR (mínimo y real)**
- `mise.toml` (node, pnpm, dotnet) + `global.json`. Un `docker-compose.yml` con solo Postgres (y Mailpit para correos de dev).
- CI por PR: build, tests (Testcontainers), `dotnet format`, `docker build`, `tsc`, lint, jest, contrato y `expo install --check`. Workflow de EAS (preview Android + Maestro) en PR a `main`.
- Secret scanning + push protection. Dependabot/Renovate.
- README como runbook de 5 comandos. `docs/adr/` para las decisiones de §9.
- IaC mínima real (`render.yaml` o el equivalente del proveedor elegido, D15). La IaC académica va a `docs/academic/`.

### 6.12 Organización de repos: **REFACTORIZAR**
- Monorepo en la org `EncorelyHQ` (D14):
  - `apps/mobile` (Expo)
  - `apps/landing` (Vite)
  - `services/api` (.NET)
  - `packages/tokens` (design tokens → RN + CSS vars)
  - `packages/api-client` (generado)
  - `docs/`
- Archivar Django y Python (D8). Transferir la landing y el proyecto de Vercel/Netlify a la org.
- Preservar todo el WIP antes de mover nada (D9).

### 6.13 Landing: **REFACTORIZAR**
- Funcionalmente sirve: la Netlify Function guarda la key en el servidor, es idempotente y usa `aria-live`.
- Se rehace la capa visual con la identidad nueva. Se quitan las promesas falsas. Se arregla el input de 22 px. Se añaden las páginas legales, el formulario de borrado de cuenta y la ruta para Universal/App Links si se usan. Se actualiza `react-router`.

---

## 7. Requisitos obligatorios del nuevo sistema

### 7.1 Móvil nativo
- iOS 16.4+ y Android 7+ con Expo SDK 57 y New Architecture. Portrait. iPad en modo compatibilidad revisado. **Sin target web en la app.**
- Builds de EAS firmados por perfil. Ninguna URL `localhost` ni `http://` en `preview/production`.
- Permisos mínimos y justificados en español. Sin micrófono. Cámara solo para el QR de verificación.
- Tema `venue/papel` siguiendo el sistema. Splash del mismo color que el fondo. Sin flash blanco.
- Sesión que sobrevive semanas (refresh). Arranque sin destello del onboarding.
- Red con timeout, reintento en GET, banner "Sin conexión" y refetch al volver a primer plano.

### 7.2 Chat 1:1 con E2EE (05-§2)
- F1–F10 y S1–S8 de 05, en particular:
  - El servidor, la BD, los logs y el push nunca ven texto plano (test del canario).
  - Identidad desde el JWT. 404 si no hay pertenencia.
  - Llaves solo en SecureStore `WHEN_UNLOCKED_THIS_DEVICE_ONLY`.
  - Verificación por QR y aviso de cambio de llave.
  - Chat solo con match `Active`.
  - Bloquear y reportar con franking.
- Limitaciones declaradas en la UI y en `docs/`: v1 sin forward secrecy, un dispositivo y sin backup del historial.

### 7.3 Tiendas (04 §5)
- Borrado de cuenta dentro de la app y por URL web.
- Reportar, bloquear, contacto publicado y SLA de 24 h.
- Política de privacidad y términos publicados.
- Fecha de nacimiento con bloqueo a menores de 18 y clasificación 18+.
- Etiquetas App Privacy / Data Safety completas. `PrivacyInfo.xcprivacy`.
- Cuenta demo por email con acceso completo.
- Sign in with Apple si hay Google.
- Atribución y cumplimiento de la Developer Policy de Spotify si se integra.
- Icono y splash propios. targetSdk 36.
- Prueba cerrada en Play (12 testers × 14 días si la cuenta es personal).

### 7.4 Identidad visual propia
- Un solo origen de tokens compartido entre app y landing.
- Cero emojis en la UI. Una sola librería de iconos. Una guía de voz (tuteo colombiano, sin anglicismos de feature).
- Contraste AA verificado en ambos temas.
- "Nada que no exista se muestra": sin cifras inventadas ni controles placebo.
- Aprobación del dueño tras la validación con 5 usuarios (D5).

### 7.5 Seguridad mínima (OWASP ASVS 4.0 L1 + MASVS-L1 en móvil)

| Área ASVS | Requisito verificable |
|---|---|
| V2 Autenticación | Contraseña ≥10 (≤72 bytes). Rate limit en `/auth/*` (p. ej. 10/min por IP+email). Respuestas y tiempos uniformes (sin enumeración). Tokens de Google/Apple validados (firma, `aud`, `exp`) |
| V3 Sesión | Access ≤15 min. Refresh rotativo con hash y detección de reutilización. Revocación al cerrar sesión, borrar la cuenta o cambiar la contraseña |
| V4 Control de acceso | Denegar por defecto (`[Authorize]` global). Identidad solo del claim. Pertenencia en cada recurso. Test de arquitectura sin `userId` en los contratos. Rol de moderador por claim |
| V5 Validación | DTOs con rangos y longitudes. Enums validados. Ningún 500 por input del cliente (test de *fuzzing* básico) |
| V7 Errores y logs | `ProblemDetails` sin stack. Enmascarar `password/token/authorization/cookie/access_token`. Correlación por `traceId` |
| V8 Protección de datos | Secretos y llaves solo en SecureStore (nativo). Nada de PII en AsyncStorage. Exclusión de backups del SO para llaves y SQLCipher |
| V9 Comunicaciones | TLS en todo. `SSL Mode=VerifyFull` hacia Postgres en producción |
| V10/V14 Configuración | Sin secretos por defecto (`ValidateOnStart`). Swagger solo en dev. Cabeceras de seguridad. Secret scanning + push protection. Escaneo de dependencias en CI |

---

## 8. Plan de reconstrucción por fases

**Estrategia:** *greenfield* por cortes verticales, no big-bang de corte.
- El código nuevo vive en el monorepo y el legado queda congelado (con una etiqueta `legacy-final`) como referencia.
- Cada fase termina desplegada en *staging* y con un build `preview` instalable.
- **Justificación de no refactorizar en el sitio:**
  - Cada capa tiene críticos.
  - El código es chico.
  - No hay usuarios ni datos reales que migrar (a confirmar en producción, §12).
  - Arreglar el IDOR y el dominio en el sitio toca más del 80 % de los archivos.
- El riesgo del big-bang (perder lo que funciona) es bajo porque hoy no hay nada que funcione de punta a punta.

**Supuestos de esfuerzo:**
- Días-persona (dp) a **dedicación completa**. Con dedicación universitaria (~20 h/semana) hay que **multiplicar el calendario ×2**.
- Equipo: B = back, F = front/móvil, D = diseño + QA + landing (rotativo).

| Fase | Contenido | Depende de | Esfuerzo | Calendario (3 pers.) |
|---|---|---|---|---|
| **0 · Decisiones y cimientos** | 0.1 decisiones + PRODUCT.md · 0.2 monorepo/CI/WIP · 0.3 esquema versionado · 0.4 identidad y auth · 0.5 contrato · 0.6 base Expo + EAS | — | ~34 dp | 3 semanas |
| **1 · Design system** | Validación con 5 usuarios, tokens, fuentes, primitivas, iconos, tab bar nativa, icono y splash | 0.1, 0.6 (en paralelo a 0.3–0.5) | ~12 dp | 2 semanas (D) |
| **2 · Núcleo de producto** | Onboarding 3 pasos, catálogo + swipes + ADN, eventos + asistencia + radar, conexión → match mutuo, perfil y ajustes reales | 0, 1 | ~28 dp | 2–3 semanas |
| **3 · Chat 1:1 E2EE + push** | 05-F1…F4 (llaves, sobres, inbox, hub, SQLCipher, push sin contenido, verificación QR, bloquear/reportar/franking, borrar cuenta) | 2 (`Match.Status`), 0.6 (dev build) | ~33 dp | 3–4 semanas |
| **4 · Cumplimiento, tiendas y endurecimiento** | Consentimiento + 18+, assets y fichas, Data Safety/App Privacy, cuenta demo, Maestro, a11y, rendimiento, prueba cerrada y TestFlight | 3, 5 (páginas legales) | ~18 dp + 14 días de prueba cerrada | 2 semanas + 2 de espera |
| **5 · Landing** (paralela) | Rehacer con la identidad nueva, páginas legales, formulario de borrado, quitar promesas falsas | 1 (tokens) | ~7 dp | 1–2 semanas (D) |
| **6 · Salas grupales por evento** (opcional, post-v1) | Hub autenticado, membresía, moderación normalizada, roles, etiqueta "no cifrada" | 3, 4 | ~10 dp | 1–2 semanas |
| **7 · Chat v2** (post-v1) | 05-F5 backup cifrado (6 dp), 05-F6 FS gruesa (8 dp), 05-F7 multi-dispositivo (L) | 3 | 14 dp + L | — |

**Total v1 (fases 0–5):** ~132 dp ≈ **9–10 semanas a tiempo completo con 3 personas, más un 25 % de colchón ≈ 12 semanas**. Con dedicación parcial, 5–6 meses. **Corte mínimo académico** (si hay entrega antes): Fases 0 + 1-lite + 2 + chat de la Fase 3 sin franking ni push (~85 dp ≈ 7–8 semanas a tiempo completo).

### Fase 0 · Decisiones y cimientos (bloquea todo)

**0.1 Decisiones y especificación (3 dp, todo el equipo)**
- Cerrar D1–D17 (§9) como ADRs en `docs/adr/`.
- `PRODUCT.md`: glosario, reglas, umbral único, qué es real en v1 y qué no, fuente de música y de eventos.
- Reclutar desde ya a los 12 testers de Play y crear las cuentas de desarrollador (Apple 99 USD/año, Play 25 USD).
- *Aceptación:* ADRs mergeados y PRODUCT.md aprobado por el dueño.

**0.2 Repo, CI y preservación del WIP (5 dp, B+F)**
- Preservar el WIP antes de mover nada (D9).
- Monorepo con `mise.toml`, `global.json`, pnpm workspaces, compose solo con Postgres, secret scanning y push protection, Dependabot y CI de §6.11.
- *Aceptación:*
  - `git clone && mise install && pnpm i && docker compose up -d && dotnet test && pnpm test` en verde en una máquina limpia en **<15 min**.
  - El CI bloquea un PR con un error de `tsc` o un test rojo.
  - `git status` limpio en los 5 repos viejos.

**0.3 Esquema versionado (4 dp, B)**
- Migración baseline (users, identities, refresh_tokens, devices) y Testcontainers.
- *Aceptación:* el CI levanta Postgres, aplica todas las migraciones desde cero y corre los tests de repositorio. Una migración que rompe hace fallar el PR.

**0.4 Identidad y auth (9 dp, B)**
- Registro y login por email, refresh rotativo, `CurrentUserId`, `[Authorize]` global, rate limit, normalización, `ProblemDetails`, `JwtOptions` validadas, cabeceras de seguridad, `/health/live|ready`.
- Sign in with Apple/Google si entran en v1 (D10).
- *Aceptación (tests automáticos):*
  - B recibe 404 en recursos de A.
  - Ningún endpoint acepta `userId` (test de arquitectura).
  - El 11.º login en 1 min da 429.
  - Refresh reutilizado → revoca la familia.
  - Email con mayúsculas o espacios entra a la misma cuenta.
  - Arrancar sin `JWT_SECRET` falla.
  - Un `idToken` de Google falso da 401.

**0.5 Contrato (3 dp, B+F)**
- OpenAPI generado → `packages/api-client` y un check de CI.
- *Aceptación:* cambiar un DTO sin regenerar el cliente hace fallar el CI. El front no tiene tipos de API escritos a mano.

**0.6 Base Expo + EAS (10 dp, F)**
- Rama 57 como toolchain (D2). `src/` nuevo con `Stack.Protected`, `SessionContext`, TanStack Query y red con timeout.
- `eas.json` con 3 perfiles y `env`, `expo-dev-client`, detección de reinstalación, splash retenido.
- *Aceptación:*
  - **APK de desarrollo instalado en un Android físico** que hace login por email contra staging HTTPS.
  - Tras forzar la expiración, el refresh es transparente.
  - Cerrar y reabrir la app conserva la sesión.
  - Reinstalar limpia las llaves.
  - Un build `preview` sin `EXPO_PUBLIC_API_URL` falla al compilar.

### Fase 1 · Design system (en paralelo, D)
- Validar "La boleta compartida" con 5 usuarios de 18–30 años en Bogotá y Medellín (03-F0).
- Luego, 03-F1: `packages/tokens`, `ThemeProvider` venue/papel, Big Shoulders + Archivo + Plex Mono incrustadas, primitivas 1–10 de 03-§6.2, Phosphor, `Toast/Dialog` en lugar de `Alert`, tab bar nativa, icono 1024 y *adaptive icon* y splash.
- *Aceptación:*
  - Catálogo de componentes (pantalla `/dev/ui` solo en dev) con los 5 estados.
  - Contraste AA verificado por script en ambos temas.
  - Ningún hex fuera de `tokens.ts` (regla de lint).
  - Ningún emoji en `ui/` ni `features/` (lint).
  - fontScale 2.0 sin cortes en las primitivas.
  - El dueño aprueba la dirección.

### Fase 2 · Núcleo de producto (B+F)
- **Back:**
  - Catálogo de tracks con preview (D11).
  - `POST /swipes` idempotente (UNIQUE) y `MusicProfile` recalculado en el servidor.
  - `RadarThresholdPolicy` en `/me`.
  - `Events` + `Attendance` (D12).
  - `GET /events/{id}/radar` filtrado por asistencia, umbral y bloqueos.
  - `ConnectionRequest` → `Match` mutuo con `Status`.
  - `GET /matches` con `peer` real.
  - Matching en el mismo proceso (sin Kafka ni Worker).
- **Front:**
  - Onboarding de 3 pasos.
  - Descubrir: portada a sangre, botones ✕/▶/♥, `accessibilityActions`, audio con foco y modo silencio.
  - Conciertos/radar por evento. `MatchTicket`. Conexiones. Perfil "mi pase". Ajustes como lista nativa con persistencia real.
  - Estados vacío y error en todas las pantallas.
- *Aceptación:*
  - Un usuario **sin Spotify** completa el flujo registro → 25 swipes → marca "voy" a un evento → ve personas compatibles → envía una conexión. El otro la acepta y ambos ven el match (Maestro `core_flow.yaml`).
  - 22 swipes sobre el mismo track cuentan 1.
  - Dos perfiles opuestos dan una afinidad <0,5 y dos idénticos >0,9 (test de propiedades).
  - El radar de un evento inexistente da 404.
  - No hay cifras ni textos fijos en la UI (búsqueda de literales numéricos en `features/`).

### Fase 3 · Chat 1:1 E2EE + push (todo el equipo)
- Los criterios de aceptación de **05-§10, fases 1 a 4**, sin cambios: vectores en Jest y en el dispositivo, canario ausente en el `pg_dump` y en los logs, p95 <1 s, rechazo de sobres manipulados, `count(*) = 0` tras el ACK, push sin contenido verificada con `FakePushSender`, franking `Verified` true/false, bloqueo → 404 + `ConversationClosed`, borrado de cuenta con 0 filas residuales.
- Más: retirar el chat v1 (`ChatController`, `Messages` → D7) y quitar los hubs anónimos.
- *Día 1:* spike de `react-native-libsodium` en un dev build de SDK 57 (Android físico y simulador iOS vía EAS). Si falla, se aplica el plan B con noble antes del día 3.

### Fase 4 · Cumplimiento, tiendas y endurecimiento
- Consentimiento versionado (Ley 1581), fecha de nacimiento con bloqueo a menores de 18, reportar/bloquear también desde el perfil y el radar.
- Fichas, capturas, Data Safety/App Privacy y `PrivacyInfo.xcprivacy`. Revisión del `AndroidManifest` (permisos). Cuenta demo y notas para el revisor.
- Maestro en EAS Workflows: `login_email`, `core_flow`, `chat_send`, `block_report` y `delete_account`.
- VoiceOver/TalkBack. Android de gama media (rendimiento, sin blur). Brillo bajo.
- Prueba cerrada en Play (14 días) y TestFlight.
- *Aceptación:*
  - El checklist de 04 §5 completo, sin ❌.
  - Los 5 flujos Maestro en verde en Android e iOS (simulador EAS).
  - Arranque en frío <3 s en un *release build* de gama media.
  - Checklist ASVS L1 de §7.5 firmado.
  - Build aceptado en la prueba cerrada de Play y en TestFlight.

### Fase 5 · Landing (paralela desde la Fase 1)
- 03-F5 con los tokens compartidos: hero tipográfico, 3 pasos tipo setlist, capturas reales de la app nueva y waitlist con input de 48 px.
- `/privacidad`, `/terminos` y `/eliminar-cuenta` (formulario que llama a un endpoint con verificación por correo).
- Sin Ticketmaster ni cifras. `react-router` actualizado.
- *Aceptación:*
  - Lighthouse móvil ≥90 en rendimiento y accesibilidad.
  - 0 animaciones infinitas fuera de `prefers-reduced-motion: no-preference`.
  - Las URLs legales responden 200 y están enlazadas desde la app.

---

## 9. Decisiones pendientes del dueño

| # | Decisión | Opciones | Recomendación |
|---|---|---|---|
| **D1** | Versión de .NET | .NET 8 (EOL **10-nov-2026**) · .NET 10 LTS (soporte hasta nov-2028) | **.NET 10.** Construir de cero sobre una plataforma que caduca en 4 semanas no tiene sentido. La API de SignalR y la de minimal APIs no cambian de forma relevante |
| **D2** | Base del front | (a) Mergear `codex/actualizar-frontend` y refactorizar · (b) **Rama 57 como toolchain y `src/` nuevo** · (c) `create-expo-app` vacío · (d) Quedarse en SDK 54 | **(b).** Se hereda lo que vale de la rama (SDK 57, `expo-audio`, pnpm, CI, `.npmrc` y la base de EAS) sin arrastrar las 3 sesiones, styled-components ni el guard. Merge a `main` solo después del primer dev build en un dispositivo. Coordinar con `edav404`. (d) implica migrar dos veces |
| **D3** | Kafka, Redis, Worker y SignalR | Mantener todo · **Simplificar** · Mantener Kafka por rúbrica | **Eliminar Kafka, Redis y el Worker como proceso. Conservar solo `ChatHub` autenticado.** Si la materia exige broker o eventos, outbox + Kafka solo para analítica fuera del camino crítico (§6.4) |
| **D4** | Acceso a datos | Dapper + DbUp · EF Core 10 + migraciones · Mixto | **EF Core 10 con migraciones** como fuente de verdad: el modelo y el esquema salen de una sola definición y `has-pending-model-changes` en CI elimina la clase de bug de GEN-09. Dapper solo para 1–2 consultas calientes (radar). Si la rúbrica exige Dapper/CQRS: Dapper + DbUp + Testcontainers obligatorio desde el día 0 |
| **D5** | Dirección visual | "La boleta compartida" (03-A) · "Afiche de poste" (03-B) · Otra | **Validar A con 5 usuarios (2 días) antes de construir la Fase 1.** Usar B solo como lenguaje de campaña. Si la validación la rechaza, pedir una segunda ronda con la misma disciplina (nacer del dominio) |
| **D6** | Forward secrecy en el chat v1 | Sin FS (v1 de 05) · FS gruesa (05-F6, +8 dp) · Signal/MLS | **Sin FS en v1, declarado en la UI y en la documentación.** Añadir la Fase 6 de 05 solo si la nota o el docente la exige. Signal y MLS no tienen binding RN mantenido: hoy serían menos seguros en la práctica |
| **D7** | `DROP TABLE "Messages"` | Drop · Archivar y luego drop · Mantener | **Drop en la Fase 3**, en el esquema nuevo (que no la tendrá). En el legado: no se toca. Si producción (Supabase) tiene mensajes reales, exportarlos y luego borrarlos con confirmación explícita y el comando a la vista |
| **D8** | Django y Python | Archivar · Borrar · Mantener | **Archivar en GitHub** (solo lectura, con un README que apunte al monorepo). No borrar: sus constraints sirven de referencia y el historial académico se conserva. Revisar antes que no queden secretos (`SECRET_KEY` con fallback) |
| **D9** | Cambios locales sin commit | Descartar · Commitear en `main` · **Commitear en ramas WIP** | **Ramas WIP con push**, sin mezclarlas en `main`. Back: `wip/legacy-fixes` (DTO `[Required]` correcto, `.gitignore`, `docker-compose.local.yml`, y `db/` **sin la contraseña del comentario del seed**). Front: `wip/email-login-local` (5 archivos). Landing: hacer push del commit pendiente de `feat/reconstruction` y decidir su merge. Django y Python: revisar el diff del `.gitignore`. Luego, etiqueta `legacy-final` en cada repo |
| **D10** | Login primario | Spotify · **Email** · Solo social | **Email + contraseña** como base. **Sign in with Apple** obligatorio si se ofrece Google. Spotify solo como conector opcional |
| **D11** | Fuente de canciones y "ADN" | Spotify (inviable) · Deezer API (preview de 30 s, sin auth para búsqueda) · iTunes Search · Catálogo curado | **Catálogo propio curado** (artistas de los eventos colombianos + géneros) **con previews de Deezer o iTunes**. Revisar sus términos de uso antes de comprometerse. ADN = vector de géneros y artistas, no `audio-features` |
| **D12** | Fuente de eventos | Hardcode · **Tabla curada con script de alta** · Integración externa (Ticketmaster Discovery, etc.) | **Tabla curada** para el MVP (Bogotá y Medellín, 20–50 eventos). Integración externa en v2. No prometer Ticketmaster en la landing |
| **D13** | Salas grupales por evento en v1 | Incluir · **Posponer a post-v1** · Eliminar | **Posponer (Fase 6).** Exigen moderación de UGC, roles y SLA. El valor diferencial de la v1 es el radar y el chat 1:1 |
| **D14** | Organización de repos | Polirepo actual · **Monorepo** | **Monorepo en `EncorelyHQ`** (tokens y cliente API compartidos en el mismo PR, un solo CI, EAS soporta monorepos). Transferir la landing y los proyectos de Vercel/Netlify a la org. Corregir los typos de nombre |
| **D15** | Hosting y costos | Render free (*cold start* de 30–60 s) · Render de pago o similar · Otro | **Instancia de pago mínima sin sleep** para el API (el *cold start* rompe la UX móvil y la revisión de las tiendas) + Postgres administrado (Supabase o Neon). Más Apple Developer (99 USD/año) y Play (25 USD) |
| **D16** | ¿Arreglar el legado para una entrega cercana? | No · Solo quick wins de 01 §6 | **No, salvo que haya una entrega académica antes de la Fase 2.** Si la hay: solo los quick wins 2, 3, 4, 6 y 7 de 01 §6 (≤3 dp), en `wip/legacy-fixes` |
| **D17** | Propiedad y coordinación | Informal · **Responsables por área** | **Un responsable por área** (B, F, D) y acuerdo explícito con `edav404` sobre la rama 57 antes de la Fase 0.6 |

---

## 10. Lo que está bien y conviene conservar

- **Back:**
  - SQL 100 % parametrizado. `IncrementSwipeCountAsync` atómico (`UPDATE … RETURNING`).
  - BCrypt para contraseñas. Refresh generado con un RNG criptográfico.
  - El JWT valida issuer, audience, firma y lifetime.
  - `[Authorize]` en los controllers que no son de auth. CORS con orígenes explícitos. Stack trace solo en Development.
  - Dockerfile multi-stage con usuario no root. `.env` nunca commiteado.
- **Front:**
  - PKCE S256 sin client secret.
  - `apiRequest`/`ApiError` centralizados y testeados.
  - SecureStore para los tokens en nativo (bien resuelto, 04 §4).
  - Swipe con Reanimated 4 + Gesture Handler en worklets.
  - `strict: true`. Rutas delgadas que reexportan módulos.
  - `react-native-safe-area-context`, `GestureHandlerRootView` y el wrapper de haptics.
  - En Android: el BACK, el teclado del chat, la persistencia de sesión y los mensajes de error del login funcionan (02 nativo ok-1…ok-8).
- **Rama Expo 57:**
  - `expo-audio` con un ciclo de vida correcto y 6 tests.
  - Un solo lockfile, CI JS, Expo Doctor 21/21 y typecheck limpio.
- **Landing:**
  - Netlify Function con la key en el servidor, idempotente.
  - `aria-live` en el formulario.
  - La mayoría de los contrastes pasan AA.
- **Activos de proceso:**
  - Los 5 reportes como especificación.
  - La propuesta de diseño (03-§5–6) y la arquitectura E2EE (05) como diseño de detalle.
  - Los guiones de personas y seguridad del enjambre QA (`qa-enjambre/…/ola1, ola2`) como base de regresión.
  - `db/init/01_schema.sql` como inventario y el seed ficticio como fixture.
  - Las constraints del modelo Django como referencia.

---

## 11. Riesgos del plan y supuestos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| Dedicación real del equipo menor a la estimada (semestre universitario) | Alta | Calendario ×2 | Corte mínimo académico (§8). Fases con criterios binarios para poder cortar alcance sin dejar nada a medias |
| `react-native-libsodium` no compila en SDK 57 / New Architecture | Media | +3–5 dp | Spike el día 1 de la Fase 3. Plan B con `@noble/*` y los mismos vectores |
| La validación de diseño rechaza "La boleta compartida" | Media | +1 semana (D) | Validar en la Fase 0, antes de construir las primitivas |
| Términos de Deezer o iTunes no permiten el uso de previews en este producto | Media | Rediseño del feed | Revisarlos en 0.1. Fallback: catálogo sin audio (portada + artista) con un CTA de "escuchar en…" |
| Rechazo de tienda por UGC o encuentros (18+) | Media | +1–2 semanas | Checklist de 04 §5 como criterio de salida de la Fase 4. Cuenta demo completa. Notas al revisor |
| Prueba cerrada de Play (12 testers × 14 días) | Alta si la cuenta es personal | +2 semanas | Reclutar en la Fase 0 y empezar con el primer `preview` de la Fase 2 |
| Sin Mac para iOS | Alta | Menos QA en iOS | EAS Build + TestFlight. Maestro en simulador iOS de EAS Workflows |
| Conflicto con el trabajo de `edav404` | Media | Pérdida de trabajo o duplicación | D9 + D17 antes de 0.6 |
| Producción con usuarios o datos reales no inventariados | Baja (sospecha) | Migración no planificada | Auditar producción en solo lectura antes de apagar el legado (§12) |
| Aprender .NET 10 / EF Core | Baja | Fricción inicial | Las diferencias con .NET 8 son menores. Los ADRs fijan los patrones |
| Que la rúbrica exija Kafka, Dapper o Clean Architecture | Desconocida | Contradice D3/D4 | Revisar el enunciado en 0.1. La regla del proyecto es que el enunciado manda sobre el estilo. Aplicar las alternativas de D3/D4 |

**Supuestos:**
- (1) No hay usuarios reales en producción.
- (2) El equipo es de 3 personas con perfil fullstack y una puede llevar diseño.
- (3) El objetivo incluye publicar en tiendas. Si fuera solo una demo académica, la Fase 4 se reduce a una distribución interna por EAS.
- (4) La política de Spotify descrita por 04 es correcta (documentación de feb–jul 2026, no re-verificada aquí).
- (5) Los esfuerzos son estimaciones de ±30 %.

---

## 12. Fuera de alcance, no verificado y pendiente

- **Rotación de la contraseña de Supabase** expuesta en `955db69` (repo público). Ítem registrado: **la hará el dueño después**. Hasta entonces, asumir que la BD de producción es accesible para terceros y no guardar en ella datos reales. Complementar con secret scanning (GEN-04).
- **Limpieza de datos QA en la BD local:**
  - usuarios `qa*@example.com` y `google_*@google.com`;
  - mensajes con prefijo `QA*` en los chats y salas del seed;
  - bienvenidas duplicadas en los matches `…0001` y `…0003`;
  - matches del Worker con usuarios QA.
  - Lo más simple es recrear el volumen local (requiere confirmación). También hay archivos en `qa-enjambre/2026-10-13-2104/tmp/` con JWT locales ya vencidos, pendientes de borrar con confirmación.
- **QA nativo en iOS:** no se hizo. Todo lo de iOS (teclado, swipe-back, Keychain tras reinstalar, notch, Dynamic Type) es estático o sospecha.
- **Build nativo real:** el QA de Android fue en **Expo Go** (no en un dev build). No se validaron el splash, el icono, los permisos, el push ni el redirect `encorely://`. La rama 57 nunca se compiló en nativo.
- **Producción no auditada:** Render (si usa Docker, si hay Kafka, Redis o Worker, variables), Supabase (esquema real, RLS, si la clave ya se rotó, si hay datos reales), Vercel (deploy detenido desde 05-29) y Netlify.
- **Spotify real:** OAuth nativo, `/Auth/spotify`, `/Playlist/dna-mix` y el comportamiento de los endpoints deprecados con este client ID no se probaron.
- **WebSockets de SignalR:** solo se probaron `negotiate` y un cliente de escucha. No se abrieron conexiones de envío.
- **No hechos:** pentest, carga y rendimiento real, y revisión legal (Ley 1581, términos de Deezer o iTunes y Developer Policy de Spotify).
- **Auditoría de diseño degradada** (un solo contexto, sin snapshot de impeccable) y **viewport móvil web aproximado con iframes** en el QA: los puntajes Nielsen/audit son orientativos.
- **Verificación del QA en `Production`:** el enjambre corrió en `Development`. Lo que ve un usuario real en los 500 no se midió.
