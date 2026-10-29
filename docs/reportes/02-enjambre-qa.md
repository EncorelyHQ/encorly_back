# QA enjambre — Encorely — 2026-10-13 21:04

**Entorno:** local · front http://localhost:8081 (Expo web) · API http://localhost:5000/api/v1 (.NET 8, Development) · BD Postgres local (docker, seed ficticio) · Worker Kafka activo
**Alcance:** todo el sistema · **Agentes:** 13 (4 personas, 2 seguridad, 3 verificadores, UI-1, UI-2, UI-3 + orquestadora) · **Modo:** completo
**Base estática:** `reportes/01-auditoria-externa.md` (los hallazgos ya conocidos se marcan `[conocido: Cx/Ix]`)

## Resumen
- **Confirmados:** 8 críticos · 27 importantes · 16 menores (51 en total, deduplicados desde 101 hallazgos crudos de la ola 1)
- **Descartados:** 0 completos (2 descartes parciales dentro de hallazgos confirmados) · **Intermitentes:** 1 (accesibilidad por teclado en login, limitación del arnés)
- **En el front:** 26 visibles para el usuario · 8 tapados (solo por API) · 18 sin pantalla que los use
- **Lo más grave, en una línea cada uno:**
  1. **IDOR sistémico (S1):** con cualquier JWT se leen y escriben perfil, swipes, matches y chats privados de otro usuario; reproducido en vivo leyendo el chat demo↔ana y escribiendo como otra persona.
  2. **El núcleo del producto no funciona (F7 + F12):** el ADN musical no cambia con los swipes (perfil fijo 0.5/0.5/0.5 → afinidad 1.0 con el 78 % de la base) y cada Like tras 25 swipes crea un match automático con un desconocido, sin reciprocidad.
  3. **El chat no envía nada desde la app (F13):** todo envío da 400 y la UI lo traga en silencio.
  4. **Auth falsificable (S3, S5):** `/Auth/google` da sesión con cualquier string y crea usuarios; el umbral de 25 swipes se salta con el userId de otro o repitiendo la misma canción.
  5. **Crear sala de evento siempre da 500 (F18)** y la moderación borra por substring mensajes inocentes ("episodio", "podio", "melodiosa").

## Causas comunes
- **Identidad del cliente en vez del token:** S1, S5, S4, S13 y parte de F15 se resuelven con un `CurrentUserId` desde el JWT y chequeo de pertenencia en servicios.
- **Sin capa de validación ni mapeo de errores:** F1, F2, F5, F6, F11 y S6 vienen de no validar enums/Guid/longitudes y dejar que Postgres o excepciones genéricas respondan 500.
- **Lógica de dominio incompleta o simulada:** F7, F8, F9, F10, F12 y F14 son stubs o reglas a medias (perfil por defecto, afinidad no discriminante, eventos fijos, match sin estado).
- **Contrato front↔back implícito:** F13, F17 y los umbrales distintos (100 vs 25) nacen de tipos escritos a mano en ambos lados.
- **APIs nativas sin equivalente web:** `Alert.alert` es un no-op en web; explica logout roto, confirmaciones invisibles y onboarding sin salida.

## Críticos (8)

### C1. Cada Like tras 25 swipes crea un Match automático con cualquier usuario, sin reciprocidad, sin mood y sin evento
- **Grupo:** F12 · **Área:** descubrimiento · **Origen:** api · **Estado:** confirmado
- **En el front:** tapado: solo por API — El like que crea el match solo sale del swipe, inalcanzable sin Spotify; el resultado (matches sin reciprocidad) si aparece en listas.
- **Cómo se reproduce:**
  1. qavf1-dna (mood Chill) con 25 swipes
  2. 2x POST swipe direction 1 (Like) con trackIds QAVF1-like0/1, 4 s entre ambos
  3. grep MATCH_CREATED en worker.log; SELECT Matches del usuario con mood del otro
- **Evidencia:** worker.log: '[MATCH_CREATED] 04d4c8bc… ↔ 8288cd6e… affinity=1.0 high=False' y '… ↔ 58312c6a…'; BD Matches del usuario: 2 filas. (`capturas/ui3-1.png`)
- **Por qué esta severidad:** genera contacto no consentido a escala (cada Like = un match con un desconocido) y desacopla matches del radar/evento, que es el flujo principal del producto.
- **Causa:** EncorelyWorker/KafkaConsumerWorker.cs:159-196 (GetAllAsync sin filtro de mood/evento; MaxMatchesPerSwipe=1 por Like; CreateAsync sin reciprocidad); el Like es sobre una canción, no sobre una persona (SwipeRegisteredEvent sin targetUserId).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyWorker/KafkaConsumerWorker.cs
  -        var allUsers = await usuarioQueries.GetAllAsync();
  +        // Solo candidatos del mismo mood que ya cruzaron el umbral
  +        var allUsers = await usuarioQueries.GetByMoodExceptAsync(me.Mood, me.Id);
  @@
  -            await matchRepository.CreateAsync(match);
  +            // Sugerencia unilateral, no match: se confirma cuando el otro también la acepta
  +            await suggestionRepository.UpsertAsync(new MatchSuggestion(me.Id, candidate.Id, affinity));
  +            if (await suggestionQueries.ExistsAsync(candidate.Id, me.Id))
  +                await matchRepository.CreateAsync(match);
  +    // Límite: no más de N sugerencias por usuario y día para cortar el spam
  
  Recomendada: separar sugerencia (unilateral) de match (mutuo), filtrar por mood/evento y limitar por día. Alternativa: mantener el match automático pero solo por mood + tope diario y exigir accept de ambos lados antes del chat.
  ```

### C2. [conocido: C11] El chat del front manda el contenido como string JSON pelado y el back exige {content}: todo envío desde ChatScreen da 400
- **Grupo:** F13 · **Área:** social · **Origen:** api · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre — Enviar mensaje: POST 400, el mensaje no queda.
- **Cómo se reproduce:**
  1. Login demo@encorely.app
  2. POST /api/v1/Chat/22222222-2222-4222-8222-000000000001/messages?userId=<demo> con body JSON "QAVF2- string pelado" (lo que serializa sendChatMessage: JSON.stringify(content))
  3. Repetir con body {"content":"QAVF2- objeto"}
- **Evidencia:** Reproducido 2026-10-14T02:13Z: 400 traceId 00-397f6d5b…; 200 id 8cf7acd8-7872-4c8f-9da4-c5f1fb297259 ('QAVF2- objeto'). (`capturas/ui3-2.png`)
- **Por qué esta severidad:** la funcionalidad central de chat no funciona desde la UI (0 mensajes enviables).
- **Causa:** Front: encorely-front/src/clients/api/matches.ts:34 (body: content → http.ts:62 JSON.stringify('texto') = "texto"). Back espera SendMessageRequest(Content) en encorly_back/EncorelyApi/Controllers/ChatController.cs:39 + EncorelyApplication/DTOs/Requests.cs:26-28. Mensaje crudo en UI: http.ts:72-76 + errors.ts:4.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorely-front/src/clients/api/matches.ts
  +++ b/encorely-front/src/clients/api/matches.ts
  @@ -25,11 +25,11 @@
  -/** POST /Chat/{matchId}/messages?userId={guid} — body es el string del contenido. */
  +/** POST /Chat/{matchId}/messages?userId={guid} — body { content }. */
   export function sendChatMessage(
     matchId: string,
     userId: string,
     content: string
   ): Promise<ChatMessage> {
     return apiRequest<ChatMessage>(`/Chat/${encodeURIComponent(matchId)}/messages`, {
       method: 'POST',
       query: { userId },
  -    body: content,
  +    body: { content },
     })
   }
  Recomendada: corregir el front (el back ya es coherente con Venue y con el contrato documentado en Swagger). Alternativa: aceptar ambos formatos en el back con un JsonConverter de SendMessageRequest que lea string u objeto (más deuda, no recomendado).
  ```

### C3. [conocido: C10] POST /Venue/{eventId}/rooms siempre 500: el INSERT referencia parámetros (@Description, @Capacity, @CreatedAt) que VenueRoom no tiene y omite EventId/ExpiresAt; el 500 devuelve stackTrace
- **Grupo:** F18 · **Área:** venue · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. Login demo@encorely.app
  2. POST /api/v1/Venue/EV_BOG_001/rooms?name=QAVF2-Sala&durationHours=3 con Bearer <token de qavf2-demo>
  3. Repetir con eventId NOEXISTE y con durationHours=-1
- **Evidencia:** Reproducido 2026-10-14T02:16Z. \d "VenueRooms": Id, EventId (default ''), Name, Description, Capacity (default 0), IsActive (default true), ExpiresAt (default now()+4h), CreatedAt. Matiz respecto al hallazgo original: las columnas SÍ existen en la tabla; lo que no existe son las propiedades en el modelo, así que Dapper no enlaza @Description y Postgres recibe el texto '@Description' (operador @ + identificador sin comillas → 'description').
- **Por qué esta severidad:** funcionalidad completa rota (0 salas creables). El stackTrace del 500 solo sale en Development (ExceptionMiddleware.cs:44-46), no suma severidad.
- **Causa:** encorly_back/EncorelyRepository/Implements/VenueRoomRepository.cs:19-21 (INSERT con @Description, @Capacity, @CreatedAt inexistentes en EncorelyModels/VenueRoom.cs:4-12; IsActive es propiedad calculada; no inserta EventId ni ExpiresAt). Mismo defecto en UpdateAsync :30-33. Sin validación de durationHours en EncorelyApi/Controllers/VenueController.cs:25-29. Los tests (Encorely.Tests/Services/VenueServiceTests.cs) mockean el repositorio, por eso no lo detectan.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyRepository/Implements/VenueRoomRepository.cs
  +++ b/encorly_back/EncorelyRepository/Implements/VenueRoomRepository.cs
  @@ CreateAsync
           const string sql = @"
  -            INSERT INTO ""VenueRooms"" (""Id"", ""Name"", ""Description"", ""Capacity"", ""IsActive"", ""CreatedAt"")
  -            VALUES (@Id, @Name, @Description, @Capacity, @IsActive, @CreatedAt)
  +            INSERT INTO ""VenueRooms"" (""Id"", ""EventId"", ""Name"", ""ExpiresAt"")
  +            VALUES (@Id, @EventId, @Name, @ExpiresAt)
               RETURNING ""Id""";
  @@ UpdateAsync
  -            SET ""Name"" = @Name, ""Description"" = @Description, ""Capacity"" = @Capacity, ""IsActive"" = @IsActive
  +            SET ""Name"" = @Name, ""ExpiresAt"" = @ExpiresAt
  --- a/encorly_back/EncorelyApi/Controllers/VenueController.cs
       public async Task<IActionResult> CreateRoom(string eventId, [FromQuery] string name, [FromQuery] int durationHours = 4, CancellationToken ct = default)
       {
  +        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || durationHours is < 1 or > 72)
  +            return BadRequest(new { message = "Nombre (1-120) y duración (1-72 h) obligatorios." });
  Recomendada: alinear el INSERT con el modelo + test de integración con Postgres (Testcontainers) para los repositorios Dapper. Alternativa: añadir Description/Capacity/CreatedAt al modelo VenueRoom (mantiene columnas muertas y sigue sin guardar EventId/ExpiresAt si no se tocan).
  ```

### C4. El ADN musical no cambia con los swipes: perfil fijo 0.5/0.5/0.5 para todo usuario sin Spotify
- **Grupo:** F7 · **Área:** descubrimiento · **Origen:** api · **Estado:** confirmado
- **En el front:** tapado: solo por API — Discover necesita tracks de Spotify; con login email queda en spinner "Preparando tracks..." (C13), no se pueden hacer swipes sin Spotify (no probado).
- **Cómo se reproduce:**
  1. POST /Auth/register qavf1-dna@example.com
  2. 25+ swipes (Left, Down, Right) con trackIds QAVF1-*
  3. SELECT Energy,Danceability,Valence,Tempo FROM MusicalProfiles WHERE UserId=<qavf1-dna>
- **Evidencia:** tmp/qavf1/out_desc.txt + consulta BD (userId 04d4c8bc-97e2-4bfa-af6a-1e1c22ead3eb). (`capturas/ui3-8.png`)
- **Por qué esta severidad:** rompe la propuesta central del producto: sin ADN real la afinidad es 1.0 entre todos los perfiles por defecto (78% de la BD) y radar/matches quedan aleatorios.
- **Causa:** EncorelyApplication/Services/SwipeService.cs:34-47 (sin lectura de features del track ni update del perfil); EncorelyApplication/Services/IdentityService.cs:120-130 (único lugar que crea el perfil, con 0.5 fijo); EncorelyWorker/KafkaConsumerWorker.cs:118-196 (consume swipes pero no actualiza ADN).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyWorker/KafkaConsumerWorker.cs
       private async Task ProcessSwipeAsync(SwipeRegisteredEvent evt, CancellationToken ct)
       {
  +        await UpdateDnaAsync(evt, ct); // antes del filtro de Like: Left/Down también informan el ADN
  @@
  +    // Media móvil exponencial: cada swipe acerca (Like) o aleja (Dislike) el perfil de las features del track
  +    private async Task UpdateDnaAsync(SwipeRegisteredEvent evt, CancellationToken ct)
  +    {
  +        var features = await trackCatalog.GetFeaturesAsync(evt.TrackId, ct); // catálogo local de tracks (Energy/Danceability/Valence)
  +        if (features is null) return;
  +        var sign = evt.Direction == nameof(SwipeDirection.Right) ? 1 : evt.Direction == nameof(SwipeDirection.Left) ? -1 : 0.5;
  +        const double alpha = 0.1;
  +        profile.Energy = Math.Clamp(profile.Energy + alpha * sign * (features.Energy - profile.Energy), 0, 1);
  +        // idem Danceability, Valence
  +        await profileRepository.CreateOrUpdateAsync(profile);
  +    }
  
  Recomendada: actualizar el ADN en el worker (asíncrono, ya consume el topic) con features de un catálogo local de tracks. Alternativa: calcular el ADN on-demand al cruzar el umbral de 25 con la media de features de los Likes.
  ```

### C5. IDOR sistémico: el userId sale del cliente (query/body), no del JWT; sin verificación de pertenencia
- **Grupo:** S1 · **Área:** acceso · **Origen:** seguridad-a · **Estado:** confirmado · también lo reportaron: seguridad-a-4, seguridad-a-5, seguridad-a-6, seguridad-a-7, seguridad-a-8, seguridad-a-9, seguridad-a-11, seguridad-a-14
- **En el front:** visible: el usuario lo sufre — userId viaja en cada query y se lee de localStorage editable (encorely_backend_user_id); no explotado. Ademas /chat/<roomId> en URL abre chats ajenos.
- **Cómo se reproduce:**
  1. Login qavs-atacante (token A) y qavs-victima (token V)
  2. GET /User/me?userId=<demo> con token A -> 200 (perfil+email de demo)
  3. GET /Matches/pending?userId=<demo> con token A -> 200 (matchIds reales de demo)
  4. GET /Chat/22222222-2222-4222-8222-000000000001/messages?userId=<A> con token A -> 200 (chat privado demo<->ana)
  5. PUT /User/settings {userId:<V>,mood:2} con token A -> 204 (cambia mood de V)
  6. POST /Swipes/interactions/swipe {userId:<V>,...} con token A -> 202 (incrementa SwipeCount de V)
  7. POST /Chat/{seedRoom}/messages?userId=<demo> body {content:'QAVS-...'} con token A -> 200 (suplanta sender)
  8. POST /Chat/{seedRoom}/messages?userId=99999999-...-999999999999 -> 200 (senderId inexistente persiste, sin FK)
  9. POST /Venue/{seedRoom}/messages?userId=<demo|bogus> con token A -> 200
- **Evidencia:** GET /User/me?userId=<demo> tokenA -> 200 {email:'demo@encorely.app',...}; PUT settings de V con tokenA -> 204 y mood V pasó a VIP (restaurado); swipe V 0->1; POST chat userId=demo y userId=9999... -> 200; POST venue userId=demo/bogus -> 200 (`capturas/ui3-9.png`)
- **Causa:** UserController.cs:25 (GetMe toma userId de [FromQuery]) y :41 (UpdateSettings de request.UserId); SwipesController.cs:28 (RegisterSwipe de request.UserId); EventsController.cs:46 (GetMatches de [FromQuery]); MatchesController.cs:21 y :28; ChatController.cs:23,39 (userId de [FromQuery], sin comprobar pertenencia al match); VenueController.cs:43 (PostMessage de [FromQuery]). Los servicios tampoco validan propiedad: MatchService.GetChatMessagesAsync/SendMessageAsync/AcceptMatchAsync no comparan senderId/userId con los participantes del match. Raíz: la identidad se confía del cliente, no del token.
- **Fix propuesto** (no aplicado):
  ```diff
  Derivar el userId del JWT en cada controller y validar pertenencia en el servicio. Ejemplo (UserController), replicar en todos:
  --- a/EncorelyApi/Controllers/UserController.cs
  +++ b/EncorelyApi/Controllers/UserController.cs
  @@
  -    [HttpGet("me")]
  -    public async Task<IActionResult> GetMe([FromQuery] Guid userId, CancellationToken ct)
  -    {
  -        var user = await _usuarioQueries.GetByIdAsync(userId);
  +    [HttpGet("me")]
  +    public async Task<IActionResult> GetMe(CancellationToken ct)
  +    {
  +        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  +        var user = await _usuarioQueries.GetByIdAsync(userId);
  @@
  -    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
  -    {
  -        var user = await _usuarioQueries.GetByIdAsync(request.UserId);
  +    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
  +    {
  +        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  +        var user = await _usuarioQueries.GetByIdAsync(userId);
  En Chat/Venue, además comprobar que userId es participante del match (UserId1/UserId2) o miembro de la sala antes de leer/escribir, devolviendo 403 si no. Alternativa (una línea): un ActionFilter/claim-binding que inyecte el userId del token e ignore el del cliente, centralizando el control.
  ```

### C6. Hubs SignalR /notificationHub y /venueHub anónimos y sin derivar identidad del token
- **Grupo:** S2 · **Área:** acceso · **Origen:** seguridad-a · **Estado:** confirmado · también lo reportaron: seguridad-b-1
- **En el front:** no aplica (sin pantalla) — Sin pantalla: grep en encorely-front/src no muestra llamadas desde la UI (sin signalr/notificationHub/venueHub).
- **Cómo se reproduce:**
  1. POST http://localhost:5000/notificationHub/negotiate?negotiateVersion=1 sin Authorization -> 200
  2. POST http://localhost:5000/venueHub/negotiate?negotiateVersion=1 sin Authorization -> 200
  3. Leer Hubs/NotificationHub.cs y VenueHub.cs (no se abrió WebSocket, por regla del entorno)
- **Evidencia:** notificationHub/negotiate sin token -> 200; venueHub/negotiate sin token -> 200 {connectionId,connectionToken:<tok>,availableTransports:[WebSockets,...]}. NotificationHub.cs y VenueHub.cs sin [Authorize]; Program.cs:222-223 MapHub sin RequireAuthorization.
- **Causa:** EncorelyInfrastructure/Hubs/NotificationHub.cs:5 (JoinUserGroup(string userId) usa el argumento, no Context.User); VenueHub.cs:15 (SendVenueMessage(roomId,userId,message) sin auth ni persistencia); EncorelyApi/Program.cs:222-223 (MapHub sin RequireAuthorization). AddSignalR en Program.cs:51 sin política.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Program.cs
  +++ b/EncorelyApi/Program.cs
  @@
  -app.MapHub<NotificationHub>("/notificationHub");
  -app.MapHub<VenueHub>("/venueHub");
  +app.MapHub<NotificationHub>("/notificationHub").RequireAuthorization();
  +app.MapHub<VenueHub>("/venueHub").RequireAuthorization();
  +// + configurar JwtBearer OnMessageReceived para leer access_token de la query (SignalR WebSocket)
  Añadir [Authorize] a ambos hubs, derivar userId de Context.UserIdentifier/Context.User (eliminar el parámetro userId de JoinUserGroup y SendVenueMessage), y validar membresía de la sala. SendVenueMessage debe persistir y pasar por moderación (o eliminarse y emitir solo desde el servidor tras VenueController.PostMessage).
  ```

### C7. POST /Auth/google emite JWT y crea usuario sin validar el token ante Google
- **Grupo:** S3 · **Área:** acceso · **Origen:** seguridad-a · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — authWithGoogle sin llamadores en src/.
- **Cómo se reproduce:**
  1. POST /Auth/google {token:'QAVS-cualquier-cosa'} -> 202 con accessToken+userId
  2. POST /Auth/google {token:'x'} -> 202 con otro userId distinto
- **Evidencia:** call1 -> 202 userId=d2998785-...; call2 (token 'x') -> 202 userId=e6f8272a-... (distinto). Usuario creado: provider 'Google', email 'google_<rnd>@google.com', swipeCount 0.
- **Causa:** EncorelyApplication/Services/IdentityService.cs:52-58 — LoginWithGoogleAsync genera googleId='google_'+Guid aleatorio y email derivado, ignora por completo el idToken recibido y llama a ProcessIdentityAsync que crea el usuario y emite JWT. No hay verificación con Google.Apis.Auth.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/Services/IdentityService.cs
  +++ b/EncorelyApplication/Services/IdentityService.cs
  @@
  -    public async Task<TokenResponse> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
  -    {
  -        var googleId = "google_" + Guid.NewGuid().ToString().Substring(0, 8);
  -        var email = $"{googleId}@google.com";
  -        return await ProcessIdentityAsync(googleId, email, AuthProvider.Google, null, ct);
  +    public async Task<TokenResponse> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
  +    {
  +        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
  +            new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _googleClientId } });
  +        return await ProcessIdentityAsync(payload.Subject, payload.Email, AuthProvider.Google, null, ct, displayName: payload.Name);
  Validar firma/aud/exp del idToken con Google.Apis.Auth.GoogleJsonWebSignature y usar sub/email reales; si falla, lanzar InvalidCredentialsException (401).
  ```

### C8. Umbral de 25 swipes saltable (userId ajeno en el query) y POST de chat sin umbral
- **Grupo:** S5 · **Área:** acceso · **Origen:** seguridad-a · **Estado:** confirmado · también lo reportaron: seguridad-a-20
- **En el front:** visible: el usuario lo sufre — Radar muestra 403 al usuario de 24 swipes (umbral aplicado en UI); userId editable en localStorage lo saltaria (no explotado). Chat abre por URL sin umbral.
- **Cómo se reproduce:**
  1. Token V (qavs-victima, swipeCount=1)
  2. GET /Events/TM_003/matches?userId=<V> con token V -> 403 'Debes completar el Umbral de los 25 swipes'
  3. GET /Events/TM_003/matches?userId=<demo, 32 swipes> con token V -> 200 (lista de compatibles)
  4. Usuario Google nuevo swipeCount=0: GET /Chat/{seed}/messages?userId=<él> -> 400 bloqueo; POST /Chat/{seed}/messages?userId=<él> -> 200 (sin umbral)
- **Evidencia:** radar userId=V -> 403; userId=demo -> 200 [{displayName:'Daniela Pardo',...}]. Google swipeCount=0: GET chat -> 400 'Su ADN musical aún no es suficiente para chatear'; POST chat -> 200 {content:'QAVS-google sin umbral'}. (`capturas/ui3-6.png`)
- **Causa:** EventsController.cs:44-52 — el umbral se comprueba sobre el Usuario obtenido de [FromQuery] userId, no del token. ChatController.cs:27 — el bloqueo por SwipeCount solo se aplica si user.Provider == AuthProvider.Google y solo en GetMessages; ChatController.cs:39 SendMessage no llama a GetMeAsync ni compara SwipeCount (ningún provider). Inconsistencia: usuarios Custom no tienen umbral en el chat.
- **Fix propuesto** (no aplicado):
  ```diff
  Derivar userId del token (fix de S1) y extraer la comprobación de umbral a un helper aplicado tanto en GET como en POST de chat, sin condicionar por provider:
  --- a/EncorelyApi/Controllers/ChatController.cs
  +++ b/EncorelyApi/Controllers/ChatController.cs
  @@ SendMessage
  -        var result = await _matchService.SendMessageAsync(matchId, userId, request.Content, ct);
  +        var user = await _userService.GetMeAsync(userId, ct);
  +        if (user.SwipeCount < 25)
  +            return BadRequest(new { message = "Su ADN musical aún no es suficiente para chatear" });
  +        var result = await _matchService.SendMessageAsync(matchId, userId, request.Content, ct);
  y quitar el condicional `Provider == Google` del GET. Recomendado: centralizar el umbral en el servicio.
  ```

## Importantes (27)

### I1. Registro concurrente del mismo email: 1x202 y 7x500 (violación UQ_Users_Email sin mapear)
- **Grupo:** F1 · **Área:** cuenta · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — registerWithEmail sin llamadores; no hay pantalla de registro.
- **Cómo se reproduce:**
  1. Lanzar 8 POST /Auth/register en paralelo con {email:'qavf1-par@example.com',password:<pwd>}
  2. Repetir con qavf1-par2@example.com
- **Evidencia:** tmp/qavf1/out_cuenta.txt (### F1). Reproducible 2/2 rondas.
- **Por qué esta severidad:** no hay pérdida ni duplicación de datos (la UNIQUE lo impide) y el stackTrace solo sale en Development; el impacto real es un doble tap en 'Registrarse' que muestra error 500 aunque la cuenta sí se creó.
- **Causa:** EncorelyApplication/Services/IdentityService.cs:61-63 (GetByEmailAsync + throw DuplicateEmail) y :114 (CreateAsync sin capturar 23505); EncorelyApi/Middleware/ExceptionMiddleware.cs:28-34 (solo mapea AppException) y :44-45 (stack en Development).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/Services/IdentityService.cs
  +++ b/EncorelyApplication/Services/IdentityService.cs
  @@ ProcessIdentityAsync
  -            await _usuarioRepository.CreateAsync(user);
  +            try
  +            {
  +                await _usuarioRepository.CreateAsync(user);
  +            }
  +            catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
  +            {
  +                throw new DuplicateEmailException();
  +            }
  
  Recomendada: capturar 23505 en el punto de INSERT (la UNIQUE ya es la fuente de verdad). Alternativa: mapear PostgresException 23505 -> 409 de forma global en ExceptionMiddleware (más corto, pero pierde semántica de qué recurso choca).
  ```

### I2. Swipes repetidos sobre la misma canción suman al umbral; direction ausente cuenta como Left; trackId sin validar
- **Grupo:** F11 · **Área:** descubrimiento · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-descubrimiento-7, persona-descubrimiento-11
- **En el front:** tapado: solo por API — Swipes solo desde el mazo Spotify (tracks distintos); sin Spotify no hay swipes en UI.
- **Cómo se reproduce:**
  1. Registrar qavf1-dna; GET radar -> 403 umbral
  2. 22x POST swipe {trackId:'QAVF1-misma',direction:0}
  3. POST swipe sin direction {trackId:'QAVF1-dm'} (descubrimiento-7)
  4. POST swipe trackId 'QAVF1-'+'x'*5000 y "QAVF1-<script>alert(1)</script>'--" (descubrimiento-11)
  5. GET /User/me -> swipeCount
  6. GET /Events/EV_BOG_001/matches
- **Evidencia:** tmp/qavf1/out_desc.txt; BD Swipes de 04d4c8bc…: 27 filas, 6 trackIds distintos, max length 5006, QAVF1-dm Direction=0. (`capturas/ui3-8.png`)
- **Por qué esta severidad:** el umbral de 25 (regla de negocio que protege radar/chat) se salta con un bucle trivial sobre una sola canción.
- **Causa:** EncorelyApplication/Services/SwipeService.cs:39-44 (inserta e incrementa sin comprobar duplicado); EncorelyApplication/DTOs/Requests.cs:16-20 (Direction no nullable, TrackId sin formato/longitud); db/init/01_schema.sql:56 (índice no único).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/db/init/01_schema.sql (migración, previa deduplicación)
  -CREATE INDEX IF NOT EXISTS "IX_Swipes_UserId_TrackId" ON "Swipes" ("UserId", "TrackId");
  +CREATE UNIQUE INDEX IF NOT EXISTS "UX_Swipes_UserId_TrackId" ON "Swipes" ("UserId", "TrackId");
  --- a/EncorelyRepository/Implements/SwipeRepository.cs
  -            INSERT INTO ""Swipes"" (...) VALUES (...)
  +            INSERT INTO ""Swipes"" (...) VALUES (...) ON CONFLICT (""UserId"", ""TrackId"") DO NOTHING
  --- a/EncorelyApplication/Services/SwipeService.cs
  -        await _swipeRepository.CreateAsync(swipe);
  -        await _usuarioRepository.IncrementSwipeCountAsync(userId);
  +        var inserted = await _swipeRepository.CreateAsync(swipe); // filas afectadas
  +        if (inserted == 0) return; // canción ya swipeada: idempotente, no cuenta
  +        await _usuarioRepository.IncrementSwipeCountAsync(userId);
  --- a/EncorelyApplication/DTOs/Requests.cs
   public record SwipeRequest(
       [Required] Guid UserId,
  -    [Required] string TrackId,
  -    [Required] SwipeDirection Direction
  +    [Required, RegularExpression("^[A-Za-z0-9]{22}$")] string TrackId,
  +    [Required, EnumDataType(typeof(SwipeDirection))] SwipeDirection? Direction
   );
  
  Recomendada: UNIQUE + ON CONFLICT DO NOTHING (idempotente y seguro ante concurrencia) + DTO estricto. Alternativa: contar el umbral como COUNT(DISTINCT TrackId) en lugar del contador (no requiere migrar, pero sigue guardando basura). Nota: la regex de 22 chars rompería los trackIds de prueba QA*; usar un perfil de validación configurable en local.
  ```

### I3. [conocido: I5] /Matches/pending devuelve displayName 'Match XXXX' (4 primeros chars del GUID) sin datos del otro usuario; los 5 matches seed se llaman todos 'Match 2222'
- **Grupo:** F14 · **Área:** social · **Origen:** api · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre — Lista muestra "Match 3d83", "Match 2222" repetido: no se sabe con quien.
- **Cómo se reproduce:**
  1. Login demo@encorely.app
  2. GET /api/v1/Matches/pending?userId=11111111-1111-4111-8111-000000000001
- **Evidencia:** Reproducido 2026-10-14T02:14Z: [...{matchId:'22222222-…0004',displayName:'Match 2222',compatibility:1},{…0003,'Match 2222',0.99},{…0002,'Match 2222',0.99},{…0001,'Match 2222',1}]. Users sí tiene DisplayName y ProfileImageUrl. (`capturas/ui3-1.png`)
- **Por qué esta severidad:** el usuario no sabe con quién hizo match; no expone datos ni rompe flujo.
- **Causa:** encorly_back/EncorelyApplication/Services/MatchService.cs:40 (DisplayName = "Match " + m.Id.ToString().Substring(0, 4)); EncorelyQuery/Implementations/MatchQueries.cs:26 no hace JOIN con Users. Front: encorely-front/src/modules/matches/screens/MatchesScreen.tsx:12,70,87 (avatarFor(matchId)) y src/modules/chat/screens/ChatScreen.tsx:81-84.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyQuery/Implementations/MatchQueries.cs
  +++ b/encorly_back/EncorelyQuery/Implementations/MatchQueries.cs
  +    public async Task<IEnumerable<MatchSummary>> GetSummariesByUserIdAsync(Guid userId)
  +    {
  +        using var connection = _connectionFactory.CreateConnection();
  +        const string sql = @"
  +            SELECT m.""Id"" AS MatchId, u.""Id"" AS OtherUserId, u.""DisplayName"", u.""ProfileImageUrl"",
  +                   m.""AffinityScore"" AS Compatibility, m.""CreatedAt""
  +            FROM ""Matches"" m
  +            JOIN ""Users"" u ON u.""Id"" = CASE WHEN m.""UserId1"" = @UserId THEN m.""UserId2"" ELSE m.""UserId1"" END
  +            WHERE m.""UserId1"" = @UserId OR m.""UserId2"" = @UserId
  +            ORDER BY m.""CreatedAt"" DESC";
  +        return await connection.QueryAsync<MatchSummary>(sql, new { UserId = userId });
  +    }
  --- a/encorly_back/EncorelyApplication/Services/MatchService.cs
  +++ b/encorly_back/EncorelyApplication/Services/MatchService.cs
  @@ -36,8 +36,6 @@
  -        var matches = await _matchQueries.GetByUserIdAsync(userId);
  -        return matches
  -            .OrderByDescending(m => m.CreatedAt)
  -            .Select(m => new { MatchId = m.Id, DisplayName = "Match " + m.Id.ToString().Substring(0, 4), Compatibility = m.AffinityScore });
  +        return await _matchQueries.GetSummariesByUserIdAsync(userId);
  (front: usar item.displayName/profileImageUrl en MatchesScreen y pasarlos a ChatScreen por params)
  Recomendada: JOIN en la query (una sola ida a BD). Alternativa: endpoint GET /Users/{id}/public y que el front resuelva el perfil por match (N+1, no recomendado).
  ```

### I4. Matches sin estado de aceptación: accept no es idempotente (duplica el mensaje de bienvenida y la notificación en cada llamada) y /pending sigue devolviendo matches ya aceptados
- **Grupo:** F14 · **Área:** social · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-social-4
- **En el front:** visible: el usuario lo sufre — Al tocar un match se llama accept: chat con 10 bienvenidas duplicadas; el match sigue en Matches.
- **Cómo se reproduce:**
  1. Login demo@encorely.app
  2. Contar mensajes '¡Match creado! Ya pueden chatear.' en GET /Chat/22222222-2222-4222-8222-000000000003/messages?userId=<demo>
  3. POST /Matches/22222222-2222-4222-8222-000000000003/accept?userId=<demo> dos veces
  4. Volver a contar y GET /Matches/pending?userId=<demo>
  5. Login valentina.ospina y GET /Matches/pending?userId=…000000000004
- **Evidencia:** Reproducido 2026-10-14T02:14Z: 'welcome before 3 after 5'; 'pending after has 0003: True 9'; \d "Matches" sin AcceptedAt/Status. Front: MatchesScreen.tsx:22,50-51 separa 'Matches'/'Chats' con un useState<Set> local (openedRooms), que se pierde al recargar. (`capturas/ui3-2.png`)
- **Por qué esta severidad:** basura visible en el chat (N bienvenidas), notificaciones repetidas y tabs 'Matches/Chats' que no reflejan la realidad; no pierde datos.
- **Causa:** encorly_back/EncorelyApplication/Services/MatchService.cs:43-64 (AcceptMatchAsync no lee/escribe estado, no valida que userId sea UserId1/UserId2, notifica e inserta el mensaje siempre); MatchService.cs:35-41 + EncorelyQuery/Implementations/MatchQueries.cs:26 (pending = todos los matches del usuario); modelo EncorelyModels/Match.cs:3-13 sin estado. Front: encorely-front/src/modules/matches/screens/MatchesScreen.tsx:22,50-51.
- **Fix propuesto** (no aplicado):
  ```diff
  -- migración
  +ALTER TABLE "Matches" ADD COLUMN "AcceptedAt" timestamptz NULL;
  --- a/encorly_back/EncorelyApplication/Services/MatchService.cs
  +++ b/encorly_back/EncorelyApplication/Services/MatchService.cs
  @@ AcceptMatchAsync
           var match = await _matchQueries.GetByIdAsync(matchId);
           if (match == null) throw new NotFoundException("Match");
  -        
  -        var roomId = match.Id;
  -        
  -        await _notificationService.NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, ct);
  +        if (match.UserId1 != userId && match.UserId2 != userId) throw new ForbiddenException("Match"); // nueva AppException → 403
  +        var roomId = match.Id;
  +        // Idempotente y a prueba de carreras: solo la primera llamada cambia la fila.
  +        var firstAccept = await _matchRepository.TryMarkAcceptedAsync(matchId, DateTime.UtcNow);
  +        if (!firstAccept) return roomId;
  +        await _notificationService.NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, ct);
  --- a/encorly_back/EncorelyRepository/Implements/MatchRepository.cs
  +    public async Task<bool> TryMarkAcceptedAsync(Guid id, DateTime at)
  +    {
  +        using var c = _connectionFactory.CreateConnection();
  +        return await c.ExecuteAsync("UPDATE \"Matches\" SET \"AcceptedAt\" = @At WHERE \"Id\" = @Id AND \"AcceptedAt\" IS NULL", new { Id = id, At = at }) == 1;
  +    }
  --- a/encorly_back/EncorelyQuery/Implementations/MatchQueries.cs (pending)
  -WHERE "UserId1" = @UserId OR "UserId2" = @UserId
  +WHERE ("UserId1" = @UserId OR "UserId2" = @UserId) AND "AcceptedAt" IS NULL
  (+ endpoint GET /Matches/active o campo status en la respuesta; el front deriva tabs del back, no de openedRooms)
  Recomendada: columna AcceptedAt + UPDATE condicional (idempotente y atómico ante llamadas paralelas). Alternativa: índice único parcial sobre Messages(MatchId) WHERE SenderId = '00000000-…' para impedir el duplicado (tapa el síntoma, no da estado).
  ```

### I5. [conocido parcial: I13] Umbral de 25 swipes no se aplica al chat: usuario Custom con 0 swipes lee y escribe; la regla solo existe para Google, solo en GET y responde 400 (no 403)
- **Grupo:** F15 · **Área:** social · **Origen:** api · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre — Usuario de 24 swipes abre por URL el chat demo-ana y lo lee (GET 200).
- **Cómo se reproduce:**
  1. POST /api/v1/Auth/register {email:'qavf2-nuevo@example.com',password:'Encorely123!'}
  2. Login con ese usuario; GET /User/me → swipeCount 0, provider Custom
  3. GET /Chat/22222222-2222-4222-8222-000000000001/messages?userId=<nuevo>
  4. POST /Chat/22222222-2222-4222-8222-000000000001/messages?userId=<nuevo> {content:'QAVF2- intruso 0 swipes'}
- **Evidencia:** Reproducido 2026-10-14T02:15Z con userId bc31de29-bdaa-4731-afe8-0c8ca94e7ce0. Nota: el usuario además NO es miembro del match …0001 (demo↔ana): eso es el IDOR conocido C1/C2, que se solapa; el bug del umbral se sostiene por código aunque el usuario fuera miembro (SendMessage no tiene ningún check). (`capturas/ui3-9.png`)
- **Por qué esta severidad:** regla de negocio central (ADN mínimo antes de socializar) inefectiva; no expone datos por sí sola (la exposición viene del IDOR ya conocido).
- **Causa:** encorly_back/EncorelyApi/Controllers/ChatController.cs:28-31 (condición limitada a AuthProvider.Google y devuelve BadRequest); ChatController.cs:38-43 SendMessage sin comprobación de umbral ni de pertenencia. El umbral está duplicado como literal en EventsController.cs:48 y SwipeService.cs:12.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyApi/Controllers/ChatController.cs
  +++ b/encorly_back/EncorelyApi/Controllers/ChatController.cs
  @@
  +    private const int MinSwipesThreshold = 25;
  +    private static readonly object ThresholdError = new { message = "Debes completar el Umbral de los 25 swipes para chatear" };
  +
       [HttpGet("{roomId}/messages")]
       public async Task<IActionResult> GetMessages(Guid roomId, [FromQuery] Guid userId, CancellationToken ct)
       {
           var user = await _userService.GetMeAsync(userId, ct);
  -
  -        if (user.Provider == AuthProvider.Google && user.SwipeCount < 25)
  -        {
  -            return BadRequest(new { message = "Su ADN musical aún no es suficiente para chatear" });
  -        }
  +        if (user.SwipeCount < MinSwipesThreshold) return StatusCode(403, ThresholdError);
  @@
       public async Task<IActionResult> SendMessage(Guid matchId, [FromQuery] Guid userId, [FromBody] SendMessageRequest request, CancellationToken ct)
       {
  +        var user = await _userService.GetMeAsync(userId, ct);
  +        if (user.SwipeCount < MinSwipesThreshold) return StatusCode(403, ThresholdError);
           var result = await _matchService.SendMessageAsync(matchId, userId, request.Content, ct);
  Recomendada: mover la regla a una policy [Authorize(Policy = "MinSwipes")] con el userId sacado del JWT (resuelve a la vez C1/C2 y quita los literales 25 duplicados); el diff de arriba es el mínimo. Alternativa: chequear en MatchService.SendMessageAsync/GetChatMessagesAsync para que cualquier canal (SignalR) quede cubierto.
  ```

### I6. Notificación al aceptar match usa matchId como userId: la otra persona nunca se entera; el aceptante se notifica a sí mismo (SignalR y push)
- **Grupo:** F16 · **Área:** social · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Front sin SignalR ni push.
- **Cómo se reproduce:**
  1. Abrir 2 conexiones SignalR a /notificationHub (negotiate + WebSocket, protocolo json)
  2. Conexión A invoca JoinUserGroup('11111111-…0001' demo); conexión B invoca JoinUserGroup('11111111-…0002' ana)
  3. Como demo: POST /api/v1/Matches/22222222-2222-4222-8222-000000000001/accept?userId=<demo> (match demo↔ana)
  4. Ver qué conexión recibe NotifyMatchFound y revisar 'FCM Mock' en api.log
- **Evidencia:** Reproducido 2026-10-14T02:17Z con cliente SignalR propio (tmp/qavf2/sr.mjs): 'demo recibió [NotifyMatchFound…]', 'ana recibió []'. api.log líneas 12696-12716: 'FCM Mock: Simulated push notification sent to user 22222222-2222-4222-8222-000000000003'.
- **Por qué esta severidad:** la notificación de match nunca llega a quien debe; hoy latente (front sin hub, FCM mock) pero rota en cuanto se active.
- **Causa:** encorly_back/EncorelyApplication/Services/MatchService.cs:50 pasa (userId del aceptante, matchId); encorly_back/EncorelyApi/Services/SignalRNotificationService.cs:23-24 envía a Group(userId) y Group(matchId), y :29-30 SendPushNotificationAsync(userId) y SendPushNotificationAsync(matchId). El contrato IMatchNotificationService.cs:5 no recibe el id del destinatario. Texto de afinidad :27 sin escalar ×100.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyApplication/Services/MatchService.cs
  +++ b/encorly_back/EncorelyApplication/Services/MatchService.cs
  @@ AcceptMatchAsync
  -        await _notificationService.NotifyMatchFoundAsync(userId, matchId, match.AffinityScore, ct);
  +        var otherUserId = match.UserId1 == userId ? match.UserId2 : match.UserId1;
  +        await _notificationService.NotifyMatchFoundAsync(otherUserId, matchId, match.AffinityScore, ct);
  --- a/encorly_back/EncorelyApi/Services/SignalRNotificationService.cs
  +++ b/encorly_back/EncorelyApi/Services/SignalRNotificationService.cs
  -    public async Task NotifyMatchFoundAsync(Guid userId, Guid matchId, double affinityScore, CancellationToken ct = default)
  +    public async Task NotifyMatchFoundAsync(Guid recipientUserId, Guid matchId, double affinityScore, CancellationToken ct = default)
       {
  -        await _hubContext.Clients.Group(userId.ToString()).SendAsync("NotifyMatchFound", userId, matchId, affinityScore, ct);
  -        await _hubContext.Clients.Group(matchId.ToString()).SendAsync("NotifyMatchFound", matchId, userId, affinityScore, ct);
  +        await _hubContext.Clients.Group(recipientUserId.ToString()).SendAsync("NotifyMatchFound", recipientUserId, matchId, affinityScore, ct);
  @@
  -        var body = $"¡Has logrado un Match! Afinidad: {Math.Round(affinityScore, 2)}%";
  -        await _pushService.SendPushNotificationAsync(userId, title, body, payloadData, ct);
  -        await _pushService.SendPushNotificationAsync(matchId, title, body, payloadData, ct);
  +        var body = $"¡Alguien aceptó tu Match! Afinidad: {Math.Round(affinityScore * 100)}%";
  +        await _pushService.SendPushNotificationAsync(recipientUserId, title, body, payloadData, ct);
       }
  Recomendada: notificar solo al otro usuario y llamarlo solo en la primera aceptación (ver F14/persona-social-2). Alternativa: notificar a ambos (UserId1 y UserId2) con textos distintos.
  ```

### I7. Moderación de salas por substring sin normalizar: bloquea mensajes inocentes (episodio, podio, whatever, chateau, Scampi, melodiosa) y deja pasar variantes triviales (s p a m, sp4m, cirílico, ódio, h4te, violéncia, estafa)
- **Grupo:** F19 · **Área:** venue · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-venue-7
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. Login demo
  2. POST /api/v1/Venue/44444444-4444-4444-8444-000000000002/messages?userId=<demo> con cada uno de: 'QAVF2-Qué episodio tan bueno', 'QAVF2-subimos al podio', 'QAVF2-whatever, nos vemos', 'QAVF2-el chateau de la esquina', 'QAVF2-Scampi y vino', 'QAVF2-melodiosa canción'
  3. Repetir con 'QAVF2-s p a m', 'QAVF2-s.p.a.m', 'QAVF2-sp4m', 'QAVF2-sраm' (а cirílica), 'QAVF2-ódio', 'QAVF2-h4te', 'QAVF2-violéncia', 'QAVF2-estafa'
  4. GET /Venue/…0002/messages
- **Evidencia:** Reproducido 2026-10-14T02:19Z (14 POST). Visibles tras GET: ['QAVF2-s p a m','QAVF2-s.p.a.m','QAVF2-sp4m','QAVF2-sраm','QAVF2-ódio','QAVF2-h4te','QAVF2-violéncia','QAVF2-estafa']. Luego moderados con DELETE (mensajes propios) para limpiar la sala.
- **Por qué esta severidad:** los falsos positivos censuran en silencio conversaciones normales en español ('episodio', 'podio', 'melodiosa'); la evasión es menor por sí sola.
- **Causa:** encorly_back/EncorelyApplication/Services/VenueService.cs:61 (BannedKeywords.Any(kw => content.Contains(kw, OrdinalIgnoreCase))) con léxico de 6 términos en :17-20; sin normalización de acentos, separadores, leet ni homoglifos.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyApplication/Services/VenueService.cs
  +++ b/encorly_back/EncorelyApplication/Services/VenueService.cs
  @@
  -        var isFlagged = BannedKeywords.Any(kw => content.Contains(kw, StringComparison.OrdinalIgnoreCase));
  +        var isFlagged = ContentModerator.IsFlagged(content);
  +++ b/encorly_back/EncorelyApplication/Services/ContentModerator.cs
  +public static class ContentModerator
  +{
  +    private static readonly HashSet<string> Banned = new() { "spam", "odio", "hate", "violencia", "violence", "scam", "estafa" };
  +    private static readonly Dictionary<char, char> Leet = new() { ['4'] = 'a', ['@'] = 'a', ['3'] = 'e', ['1'] = 'i', ['0'] = 'o', ['5'] = 's', ['а'] = 'a', ['о'] = 'o', ['е'] = 'e', ['р'] = 'p', ['с'] = 'c' };
  +
  +    public static bool IsFlagged(string content)
  +    {
  +        // Quita diacríticos y mapea leet/homoglifos antes de tokenizar por palabra completa.
  +        var folded = new string(content.Normalize(NormalizationForm.FormD)
  +            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
  +            .Select(c => Leet.TryGetValue(char.ToLowerInvariant(c), out var m) ? m : char.ToLowerInvariant(c)).ToArray());
  +        var words = Regex.Split(folded, "[^a-z]+").Where(w => w.Length > 0).ToArray();
  +        if (words.Any(Banned.Contains)) return true;
  +        // 's p a m' / 's.p.a.m': letras sueltas consecutivas unidas
  +        var joinedSingles = string.Concat(words.Select(w => w.Length == 1 ? w : " ")).Split(' ');
  +        return joinedSingles.Any(Banned.Contains);
  +    }
  +}
  Recomendada: palabra completa tras normalizar (elimina los falsos positivos, que son lo que más daña al usuario) + test parametrizado con los 14 casos. Alternativa: delegar en un servicio de moderación (p. ej. API de moderación externa) y dejar la lista como fallback.
  ```

### I8. Excepciones de validación/dominio/FK salen como 500 con message del driver y stackTrace (rutas absolutas del servidor)
- **Grupo:** F2 · **Área:** transversal (cuenta/descubrimiento/social/venue) · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-descubrimiento-6, persona-descubrimiento-8, persona-social-5, persona-social-6, persona-venue-2, persona-venue-3
- **En el front:** tapado: solo por API — Front solo manda valores validos (mood sin pantalla, direction 0-2); el 500 de chat/venue no alcanzable por UI normal.
- **Cómo se reproduce:**
  1. Login qavf1-flow
  2. PUT /User/settings {userId:<propio>,mood:-1|3|7}
  3. POST /Swipes/interactions/swipe direction -1|3 (descubrimiento-6)
  4. POST swipe con userId 9999…/Guid.Empty (descubrimiento-8)
  5. POST /Chat/9999…|Guid.Empty/messages?userId=<propio> {content} (social-5)
  6. GET /Chat/22222222-…0001/messages?userId=9999… (social-6)
  7. POST /Venue/EV_BOG_001/rooms?name=QAVF1-x&durationHours=3 y POST /Venue/9999…/messages (venue-2, venue-3)
- **Evidencia:** tmp/qavf1/out_cuenta.txt (### F2) y tmp/qavf1/out_f2f5.txt (### F2 dups). Contrastes: radar con userId inexistente -> 404 texto plano 'Usuario not found'; accept de match inexistente -> 404 {message:'Match not found.'}; GET /Chat de match inexistente -> 200 [] (inconsistencia, ver social-5).
- **Por qué esta severidad:** códigos HTTP incorrectos que el front no puede distinguir (todo es 'error de servidor') y fuga de esquema/rutas en Development; no expone datos de usuarios ni rompe integridad (las CHECK/FK de BD frenan los valores).
- **Causa:** EncorelyApi/Middleware/ExceptionMiddleware.cs:28-34 (switch sin KeyNotFoundException/InvalidOperationException/PostgresException) y :44-45 (ex.Message+StackTrace en Development); EncorelyApplication/DTOs/Requests.cs:17-25 ([Required] sobre enums no nullables, sin [EnumDataType]); UserService.cs:32,39 y SwipeService.cs:37 (KeyNotFoundException en vez de NotFoundException); VenueService.cs:60 (InvalidOperationException); MatchService.cs:72-86 (SendMessageAsync no valida que el match exista).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/DTOs/Requests.cs
  +++ b/EncorelyApplication/DTOs/Requests.cs
   public record SwipeRequest(
       [Required] Guid UserId,
       [Required] string TrackId,
  -    [Required] SwipeDirection Direction
  +    [Required, EnumDataType(typeof(SwipeDirection))] SwipeDirection? Direction
   );
   public record UpdateSettingsRequest(
       [Required] Guid UserId,
  -    [Required] ConcertMood Mood
  +    [Required, EnumDataType(typeof(ConcertMood))] ConcertMood? Mood
   );
  --- a/EncorelyApplication/Services/SwipeService.cs (idem UserService.cs:32,39)
  -        if (user == null) throw new KeyNotFoundException("Usuario not found");
  +        if (user == null) throw new NotFoundException("Usuario");
  --- a/EncorelyApplication/Services/VenueService.cs
  -        if (room == null || !room.IsActive)
  -            throw new InvalidOperationException("La sala de venue no está activa o ha expirado.");
  +        if (room == null) throw new NotFoundException("Sala");
  +        if (!room.IsActive || room.ExpiresAt <= DateTime.UtcNow) throw new RoomExpiredException(); // AppException -> 410
  --- a/EncorelyApplication/Services/MatchService.cs
       public async Task<object> SendMessageAsync(Guid matchId, Guid senderId, string content, CancellationToken ct = default)
       {
  +        var match = await _matchQueries.GetByIdAsync(matchId) ?? throw new NotFoundException("Match");
  --- a/EncorelyApi/Middleware/ExceptionMiddleware.cs
               NotFoundException e         => (HttpStatusCode.NotFound, e.Message),
               DuplicateEmailException e   => (HttpStatusCode.Conflict, e.Message),
               InvalidCredentialsException e => (HttpStatusCode.Unauthorized, e.Message),
  +            RoomExpiredException e      => (HttpStatusCode.Gone, e.Message),
  +            PostgresException { SqlState: "23505" } => (HttpStatusCode.Conflict, "El recurso ya existe."),
  +            PostgresException { SqlState: "23503" or "23514" } => (HttpStatusCode.BadRequest, "Datos inválidos."),
               _                          => (HttpStatusCode.InternalServerError, "Internal Server Error")
           };
  @@
  -            var body = _env.IsDevelopment() && statusCode == HttpStatusCode.InternalServerError
  -                ? (object)new { statusCode = context.Response.StatusCode, message = ex.Message, stackTrace = ex.StackTrace?.ToString() }
  -                : (object)new { statusCode = context.Response.StatusCode, message };
  +            // El detalle va al log (LogError arriba), nunca al cliente
  +            var body = new { statusCode = context.Response.StatusCode, message };
  
  Recomendada: validar en el borde (DTO con enums nullables + EnumDataType, existencia de match/sala en el servicio) y excepciones de dominio tipadas; el middleware deja de filtrar stack. Alternativa: solo mapear PostgresException por SqlState en el middleware (parche rápido, pero sigue dependiendo de la BD para validar).
  ```

### I9. Email sin normalizar: sensible a mayúsculas (cuentas duplicadas por casing), sin trim y sin longitud máxima
- **Grupo:** F3 · **Área:** cuenta · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-cuenta-3, persona-cuenta-5
- **En el front:** tapado: solo por API — Login normaliza email (trim+lowercase); registro sin pantalla.
- **Cómo se reproduce:**
  1. POST /Auth/register {email:'Qavf1-MiXed@Example.com'} -> 202
  2. POST /Auth/login {email:'qavf1-mixed@example.com'} -> 401
  3. POST /Auth/register {email:'qavf1-mixed@example.com'} -> 202 (userId nuevo)
  4. POST /Auth/login {email:'DEMO@encorely.app'} -> 401
  5. POST /Auth/register {email:' qavf1-sp@example.com '} -> 202; login sin espacios 401, con espacios 200 (cuenta-3)
  6. POST /Auth/register {email:'qavf1-'+'a'*5000+'@example.com'} -> 202 (cuenta-5)
- **Evidencia:** tmp/qavf1/out_cuenta.txt (### F3); SELECT Users WHERE Email ILIKE '%qavf1%': 9 filas incl. ambos casings y el email con espacios.
- **Por qué esta severidad:** el teclado móvil autocapitaliza: un usuario puede registrarse y no poder volver a entrar, y se crean cuentas duplicadas de la misma persona.
- **Causa:** EncorelyQuery/Implementations/UsuarioQueries.cs:26 (WHERE "Email" = @Email, comparación case-sensitive); EncorelyApplication/Services/IdentityService.cs:59-71 (usa email tal cual); EncorelyApplication/DTOs/Requests.cs:11-14 (sin MaxLength); db/init/01_schema.sql:30 (UNIQUE sobre texto crudo, no sobre lower(Email)).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/DTOs/Requests.cs
   public record EmailAuthRequest(
  -    [Required, EmailAddress] string Email,
  +    [Required, EmailAddress, MaxLength(254)] string Email,
  --- a/EncorelyApplication/Services/IdentityService.cs
       public async Task<TokenResponse> RegisterWithEmailAsync(string email, string password, CancellationToken ct = default)
       {
  +        email = email.Trim().ToLowerInvariant();
  @@ LoginWithEmailAsync
  +        email = email.Trim().ToLowerInvariant();
  --- a/db/init/01_schema.sql (migración)
  +CREATE UNIQUE INDEX "UX_Users_Email_Lower" ON "Users" (lower("Email"));
  
  Recomendada: normalizar en el servicio + índice único sobre lower(Email) (previa limpieza de duplicados por casing). Alternativa: columna citext para Email (resuelve comparación y unicidad en BD sin tocar consultas).
  ```

### I10. PUT /User/settings sin campo mood resetea silenciosamente el mood a 0 (Moshpit)
- **Grupo:** F6 · **Área:** cuenta · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — updateSettings sin llamadores; no hay pantalla que use /User/settings.
- **Cómo se reproduce:**
  1. Login qavf1-flow
  2. PUT /User/settings {userId:<propio>,mood:2} -> 204; GET /User/me -> mood 'VIP'
  3. PUT /User/settings {userId:<propio>} -> 204
  4. GET /User/me
- **Evidencia:** tmp/qavf1/out_cuenta.txt (### F6)
- **Por qué esta severidad:** sobrescritura silenciosa de una preferencia del usuario ante un payload incompleto; hoy ninguna pantalla usa el endpoint, por eso no sube a crítico.
- **Causa:** EncorelyApplication/DTOs/Requests.cs:22-25 (ConcertMood no nullable); EncorelyApi/Controllers/UserController.cs:46 (asigna sin comprobar).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/DTOs/Requests.cs
   public record UpdateSettingsRequest(
       [Required] Guid UserId,
  -    [Required] ConcertMood Mood
  +    [Required, EnumDataType(typeof(ConcertMood))] ConcertMood? Mood
   );
  --- a/EncorelyApi/Controllers/UserController.cs
  -        user.Mood = request.Mood;
  +        user.Mood = request.Mood!.Value;
  
  Recomendada: enum nullable + [Required] (falla de verdad si falta) + EnumDataType (cierra también el 500 de F2). Alternativa: PATCH semántico (solo actualizar si viene el campo).
  ```

### I11. Afinidad no discriminante: isHighPriority nunca true (compara con 85.0), umbral 0.70 casi sin efecto y candidatos sin ADN/sin 25 swipes lideran el radar
- **Grupo:** F8 · **Área:** descubrimiento · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-descubrimiento-1, persona-descubrimiento-14
- **En el front:** visible: el usuario lo sufre — Radar muestra a todos 100% compatible y "Match Certificado".
- **Cómo se reproduce:**
  1. qavf1-dna (perfil default, mood Chill, 25 swipes)
  2. GET /Events/EV_BOG_001/matches?userId=<id>
  3. Repetir con targetMood=Moshpit y VIP
  4. Buscar a qavf1-flow (0 swipes) en el radar Moshpit
  5. SELECT IsHighPriority, min/max AffinityScore FROM Matches
- **Evidencia:** tmp/qavf1/out_desc.txt (### F8) y consulta BD de Matches. (`capturas/ui3-3.png`)
- **Por qué esta severidad:** el ranking y la prioridad no informan nada, pero no hay pérdida de datos; depende además de F7 para tener sentido.
- **Causa:** EncorelyApi/Controllers/EventsController.cs:75 y EncorelyWorker/KafkaConsumerWorker.cs:173 (affinity >= 85.0); EncorelyApplication/Services/CompatibilityService.cs:10-24 (coseno sin centrar) y :8 (umbral 0.70 calibrado para otra métrica); EncorelyQuery/Implementations/UsuarioQueries.cs:47 (GetByMoodExceptAsync sin filtro de SwipeCount).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Controllers/EventsController.cs
  -            var isHighPriority = affinity >= 85.0;
  +            var isHighPriority = _compatibilityService.IsHighPriority(affinity);
  --- a/EncorelyWorker/KafkaConsumerWorker.cs
  -            candidates.Add((candidate, affinity, affinity >= 85.0));
  +            candidates.Add((candidate, affinity, compatibility.IsHighPriority(affinity)));
  --- a/EncorelyApplication/Services/CompatibilityService.cs
  -        double dotProduct = (profileA.Energy * profileB.Energy) + ...
  +        // Centrar en 0.5 para que perfiles 'opuestos' den coseno negativo, luego reescalar a [0,1]
  +        double ea = profileA.Energy - 0.5, eb = profileB.Energy - 0.5; // idem Danceability, Valence
  +        ...
  +        return Math.Round((cosineSimilarity + 1) / 2, 2);
  --- a/EncorelyQuery/Implementations/UsuarioQueries.cs
  -        const string sql = "SELECT * FROM \"Users\" WHERE \"Id\" != @UserId AND \"Mood\" = @Mood";
  +        const string sql = "SELECT * FROM \"Users\" WHERE \"Id\" != @UserId AND \"Mood\" = @Mood AND \"SwipeCount\" >= 25";
  
  Recomendada: usar IsHighPriority del servicio (fix de 1 línea, inmediato) + centrar features + filtrar candidatos sin umbral. Alternativa: distancia euclidiana normalizada 1 - d/sqrt(3), más discriminante y fácil de explicar.
  ```

### I12. Radar ignora eventId (mismo resultado para evento real, inexistente o de otro catálogo) y el feed es una lista fija con fechas pasadas
- **Grupo:** F9 · **Área:** descubrimiento · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-descubrimiento-4
- **En el front:** visible: el usuario lo sufre — El radar usa feed[0]=TM_001 (evento pasado) sin selector; todos ven la misma lista. Radar tambien afirma "umbral de 100 swipes" y "247 personas" fijo.
- **Cómo se reproduce:**
  1. GET /Events/EV_BOG_001/matches?userId=<qavf1-dna>
  2. Repetir con NOEXISTE y TM_001
  3. GET /Events/feed (hoy 2026-10-13)
- **Evidencia:** tmp/qavf1/out_desc.txt (### F9) + comparación de cuerpos (3 idénticos: True). (`capturas/ui3-3.png`)
- **Por qué esta severidad:** el 'radar del concierto' muestra a toda la base de usuarios del mood, no a los asistentes; el feed solo ofrece eventos vencidos.
- **Causa:** EncorelyApi/Controllers/EventsController.cs:42-86 (eventId solo en la firma, :64 filtra solo por mood); EncorelyApplication/Services/EventService.cs:9-17 (lista hardcodeada); no existe tabla de asistencia/eventos en db/init/01_schema.sql.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Controllers/EventsController.cs
       public async Task<IActionResult> GetMatches(string eventId, ...)
       {
  +        var ev = await _eventService.GetByIdAsync(eventId, ct);
  +        if (ev is null) throw new NotFoundException("Evento");
  @@
  -        var candidateUsers = await _usuarioQueries.GetByMoodExceptAsync(moodFilter, userId);
  +        var candidateUsers = await _usuarioQueries.GetAttendeesByMoodExceptAsync(eventId, moodFilter, userId);
  --- a/db/init/01_schema.sql
  +CREATE TABLE "Events" ("Id" text PRIMARY KEY, "Name" text NOT NULL, "Venue" text NOT NULL, "Date" timestamptz NOT NULL, "Mood" smallint NOT NULL);
  +CREATE TABLE "EventAttendees" ("EventId" text REFERENCES "Events", "UserId" uuid REFERENCES "Users", PRIMARY KEY ("EventId","UserId"));
  --- a/EncorelyApplication/Services/EventService.cs
  -        return await Task.FromResult(new List<object> { ... });
  +        return await _eventQueries.GetUpcomingAsync(DateTime.UtcNow, ct); // WHERE "Date" >= now() ORDER BY "Date"
  
  Recomendada: tabla Events + EventAttendees y radar filtrado por asistencia. Alternativa mínima: validar eventId contra la lista mock y filtrar el feed por Date >= hoy (no da asistencia real).
  ```

### I13. JWT y userId del backend guardados en localStorage (AsyncStorage web) bajo prefijo encorely_ss:
- **Grupo:** S11 · **Área:** exposición · **Origen:** seguridad-b · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre — JWT y userId en localStorage (claves encorely_ss:encorely_backend_token / _user_id); valores no impresos.
- **Cómo se reproduce:**
  1. Leer src/shared/lib/secureStorage.web.ts
  2. Leer src/shared/context/AuthContext.tsx (BACKEND_TOKEN_KEY)
- **Evidencia:** secureStorage.web.ts:5-14 (import AsyncStorage; WEB_PREFIX='encorely_ss:'; setItem/getItem); AuthContext.tsx:9 BACKEND_TOKEN_KEY='encorely_backend_token', :44 secureGetItem(BACKEND_TOKEN_KEY), :51 setApiToken(storedToken).
- **Causa:** encorely-front/src/shared/lib/secureStorage.web.ts:5-14 (persiste en AsyncStorage/localStorage); encorely-front/src/shared/context/AuthContext.tsx:9,44,51 (guarda y recupera el JWT del backend desde ahí).
- **Fix propuesto** (no aplicado):
  ```diff
  En web, mantener el access token solo en memoria y renovar con un refresh token en cookie httpOnly/SameSite=strict (requiere el endpoint /Auth/refresh del fix S10); añadir CSP estricta en vercel.json. Severidad importante por el impacto XSS->robo de sesión en producción web.
  ```

### I14. Fallbacks de secretos en código (DB_PASSWORD, JWT_SECRET_KEY) y password del seed en comentario; bundle web sin secretos
- **Grupo:** S12 · **Área:** exposición · **Origen:** seguridad-b · **Estado:** confirmado · también lo reportaron: seguridad-b-12
- **En el front:** no aplica (sin pantalla) — Secretos en codigo del back/seed; sin pantalla.
- **Cómo se reproduce:**
  1. Revisar EncorelyApi/Program.cs:35 (DB_PASSWORD ?? fallback) y :132 (JWT_SECRET_KEY ?? fallback)
  2. Revisar db/init/02_seed.sql:2 (password común en comentario)
  3. grep de secretos en el bundle web (entry.bundle)
- **Evidencia:** Program.cs:35 'DB_PASSWORD' ?? <fallback literal> (valor NO transcrito; ver archivo:línea); :132 'JWT_SECRET_KEY' ?? <fallback literal>. db/init/02_seed.sql:2 comentario con la contraseña (valor NO transcrito). git status: db/ untracked. Bundle: sin AIza/supabase/clave BD/JWT.
- **Causa:** EncorelyApi/Program.cs:35 (var dbPass = ... ?? <fallback>) y :132 (var secretKey = Configuration["JWT_SECRET_KEY"] ?? <fallback>). db/init/02_seed.sql:2 (contraseña común en comentario). .gitignore ignora .env (ok), pero db/ está untracked -> riesgo de commit del seed con la password.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Program.cs
  +++ b/EncorelyApi/Program.cs
  @@
  -var secretKey = builder.Configuration["JWT_SECRET_KEY"] ?? <fallback>;
  +var secretKey = builder.Configuration["JWT_SECRET_KEY"]
  +    ?? (builder.Environment.IsDevelopment() ? "<clave-dev-no-secreta>" : throw new InvalidOperationException("JWT_SECRET_KEY requerido"));
  Igual para DB_PASSWORD (throw fuera de Development). Quitar la password del comentario de 02_seed.sql (parametrizar) y añadir db/init/*seed* a .gitignore o al control de secretos. b-12 (bundle) es informativo: sin acción de secretos; mover PROD_DEFAULT a EXPO_PUBLIC_API_URL y no servir bundle dev en Production.
  ```

### I15. DELETE /Venue/messages/{id} ('Admin endpoint') ejecutable por cualquier usuario sobre mensajes ajenos
- **Grupo:** S4 · **Área:** acceso · **Origen:** seguridad-a · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin endpoint Venue en el front.
- **Cómo se reproduce:**
  1. Token qavs-atacante postea un mensaje QAVS- en sala seed (obtiene su id)
  2. DELETE /Venue/messages/<id>?reason=QAVS-test con ese token -> 200
- **Evidencia:** DELETE /Venue/messages/<id propio QAVS>?reason=QAVS-test -> 200 {message:'Mensaje moderado exitosamente.'}. Probado solo sobre mensaje QAVS- propio; el código no distingue autor ni rol, aplica a mensajes ajenos.
- **Causa:** EncorelyApi/Controllers/VenueController.cs:58 ModerateMessage sin atributo de rol ni verificación; EncorelyApplication/Services/VenueService.cs:92 ModerateMessageAsync marca IsModerated sin comprobar quién llama. No existen roles en el sistema (todo [Authorize] genérico), así que no hay forma de distinguir moderador.
- **Fix propuesto** (no aplicado):
  ```diff
  No hay sistema de roles; opción recomendada: restringir la moderación a un claim/rol dedicado. --- a/EncorelyApi/Controllers/VenueController.cs
  +++ b/EncorelyApi/Controllers/VenueController.cs
  @@
  -    [HttpDelete("messages/{messageId}")]
  +    [Authorize(Policy = "Moderator")]
  +    [HttpDelete("messages/{messageId}")]
       public async Task<IActionResult> ModerateMessage(Guid messageId, [FromQuery] string reason = "Moderación manual", CancellationToken ct = default)
  y añadir la policy 'Moderator' (claim role=moderator) en Program.cs. Alternativa mínima: permitir borrar solo al autor del mensaje (comparar SenderId con el userId del token).
  ```

### I16. 500 por falta de validación (enum/FK/recurso inexistente); stackTrace y nombres de constraint solo en Development
- **Grupo:** S6 · **Área:** errores · **Origen:** seguridad-b · **Estado:** confirmado · también lo reportaron: seguridad-a-13, seguridad-a-17, seguridad-a-18
- **En el front:** tapado: solo por API — Front solo envia direction 0/1/2, no tiene pantalla de settings/venue; chat a match inexistente solo alcanzable editando URL (no probado).
- **Cómo se reproduce:**
  1. POST /Swipes/interactions/swipe {userId,trackId:'x',direction:99} -> 500 CK_Swipes_Direction
  2. PUT /User/settings {userId,mood:77} -> 500 CK_Users_Mood
  3. POST /Chat/{matchId inexistente}/messages -> 500 Messages_MatchId_fkey
  4. POST /Venue/QAVS-EV-1/rooms?name=...&durationHours=1 -> 500 'column "description" does not exist' (C10)
  5. POST /Venue/{sala inexistente}/messages -> 500 'La sala de venue no está activa o ha expirado.'
- **Evidencia:** 500 '23514 ... CK_Swipes_Direction'; '23514 ... CK_Users_Mood'; '23503 ... Messages_MatchId_fkey'; '42703 column "description" does not exist'; 'La sala de venue no está activa o ha expirado.' + stackTrace at VenueService.cs:60. ExceptionMiddleware.cs:40-42 condiciona message/stackTrace a _env.IsDevelopment().
- **Causa:** Falta validación en controllers/servicios: SwipesController.cs:28 y UserController.cs:41 no validan el enum (Enum.IsDefined) antes de persistir -> viola CK_Swipes_Direction/CK_Users_Mood (23514). ChatController.cs:39/MatchService.SendMessageAsync no comprueba existencia del match -> FK 23503. VenueService.CreateVenueRoomAsync + VenueRoomRepository: el INSERT referencia una columna 'description' inexistente (42703, C10). VenueService.cs:60 lanza InvalidOperationException de dominio que el middleware mapea a 500 en vez de 404/410 (I7).
- **Fix propuesto** (no aplicado):
  ```diff
  1) Validar enums y existencia antes de escribir (ValidationException/NotFoundException -> 400/404). 2) Introducir una excepción de dominio mapeable (p.ej. DomainException) y agregarla al switch de ExceptionMiddleware.cs como 409/422 en lugar de 500. 3) Corregir el INSERT de VenueRoomRepository (quitar 'description' o añadir la columna). Diff del middleware:
  --- a/EncorelyApi/Middleware/ExceptionMiddleware.cs
  +++ b/EncorelyApi/Middleware/ExceptionMiddleware.cs
  @@
                   NotFoundException e         => (HttpStatusCode.NotFound, e.Message),
  +                DomainException e           => (HttpStatusCode.UnprocessableEntity, e.Message),
                   DuplicateEmailException e   => (HttpStatusCode.Conflict, e.Message),
  Mantener el gating Development del stackTrace (ya correcto).
  ```

### I17. Login sin rate limit ni lockout; enumeración por tiempo (BCrypt solo si el usuario existe) y por 409 de registro
- **Grupo:** S8 · **Área:** autenticación · **Origen:** seguridad-b · **Estado:** confirmado · también lo reportaron: seguridad-b-7
- **En el front:** visible: el usuario lo sufre — Login UI sin lockout y con error generico en espanol; el oraculo de tiempo no se ve en UI. Register 409 sin pantalla.
- **Cómo se reproduce:**
  1. POST /Auth/login con email inexistente (password 10 chars) y medir tiempo
  2. POST /Auth/login con email seed existente y password mala y medir tiempo
  3. POST /Auth/register con email existente -> 409
- **Evidencia:** inexistente=1.7ms; existente_badpw=136.0ms; register demo@encorely.app -> 409 'A user with that email already exists.'. (No se hizo fuerza bruta: solo mediciones puntuales dentro del límite de 10 intentos.) (`capturas/ui3-5.png`)
- **Causa:** EncorelyApi/Program.cs — no hay AddRateLimiter/UseRateLimiter ni lockout. IdentityService.cs:71-73 LoginWithEmailAsync: BCrypt.Verify solo se ejecuta si user != null -> diferencia de tiempo observable. RegisterWithEmailAsync:62-64 lanza DuplicateEmailException -> 409.
- **Fix propuesto** (no aplicado):
  ```diff
  Registrar Microsoft.AspNetCore.RateLimiting (fixed window por IP+email) en /Auth/login, /register, /google y lockout por cuenta tras N fallos. Mitigar el oráculo de tiempo ejecutando BCrypt.Verify contra un hash dummy cuando el usuario no existe:
  --- a/EncorelyApplication/Services/IdentityService.cs
  @@ LoginWithEmailAsync
  -        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
  -            throw new InvalidCredentialsException();
  +        var hash = user?.PasswordHash ?? _dummyHash;
  +        var ok = BCrypt.Net.BCrypt.Verify(password, hash);
  +        if (user == null || user.PasswordHash == null || !ok)
  +            throw new InvalidCredentialsException();
  ```

### I18. Email sensible a mayúsculas: cuentas duplicadas y login que no normaliza
- **Grupo:** S9 · **Área:** autenticación · **Origen:** seguridad-b · **Estado:** confirmado
- **En el front:** tapado: solo por API — LoginScreen aplica trim().toLowerCase(): " DEMO@Encorely.app " entro. Registro sin pantalla.
- **Cómo se reproduce:**
  1. POST /Auth/register {email:'qavs-case@example.com'} -> 202
  2. POST /Auth/register {email:'QAVS-CASE@example.com'} -> 202 (segunda cuenta)
  3. POST /Auth/login {email:'qavs-Case@EXAMPLE.com'} -> 401
- **Evidencia:** register lower -> 202; register upper -> 202; login mixed case -> 401.
- **Causa:** EncorelyQuery/Implementations/UsuarioQueries.cs:26 GetByEmailAsync compara Email con igualdad exacta (WHERE "Email" = @Email); IdentityService.RegisterWithEmailAsync/LoginWithEmailAsync no normalizan; db/init/01_schema.sql:30 UQ_Users_Email es único case-sensitive (sin LOWER).
- **Fix propuesto** (no aplicado):
  ```diff
  Normalizar email (Trim + ToLowerInvariant) en Register/Login/Google antes de consultar/crear, e índice único sobre LOWER("Email"):
  --- a/EncorelyApplication/Services/IdentityService.cs
  @@
  -        var existing = await _usuarioQueries.GetByEmailAsync(email);
  +        email = email.Trim().ToLowerInvariant();
  +        var existing = await _usuarioQueries.GetByEmailAsync(email);
  y en schema: CREATE UNIQUE INDEX ON "Users" (LOWER("Email")). (Las 2 cuentas QAVS- quedaron creadas; no se pueden borrar por regla de BD solo-lectura.)
  ```

### I19. Onboarding sin Spotify es callejon sin salida
- **Grupo:** UI1-1 · **Área:** onboarding · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-10.png
- **Causa:** OnboardingStep3Screen.tsx:104-117 showComingSoonEmail/Google usan Alert.alert, que en react-native-web es un no-op: en web no se muestra nada. NavigationGuard.tsx:41-50 manda step-5/6 a step-4 sin sesion Spotify.
- **Fix propuesto** (no aplicado):
  ```diff
  --- OnboardingStep3Screen.tsx
  -function showComingSoonEmail() {
  -  Alert.alert('Proximamente', '...', [{ text: 'Entendido' }]);
  -}
  +function showComingSoonEmail(setNotice: (m: string) => void) {
  +  const msg = 'Registro con correo: proximamente. Por ahora usa Spotify.'
  +  if (Platform.OS === 'web') setNotice(msg)  // texto inline visible
  +  else Alert.alert('Proximamente', msg, [{ text: 'Entendido' }])
  +}
  (o deshabilitar el boton y mostrar badge 'Pronto'; o llamar POST /Auth/register + login)
  ```

### I20. Tab bar flotante tapa contenido
- **Grupo:** UI1-10 · **Área:** layout · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-8.png, capturas/ui2-4.png
- **Causa:** (main)/_layout.tsx:113-118 tabBar absolute bottom:20, height:64 (~84px); SettingsScreen.tsx:441 paddingBottom: 60; MatchesScreen.tsx listContent paddingBottom:40.
- **Fix propuesto** (no aplicado):
  ```diff
  --- SettingsScreen.tsx:441
  -contentContainerStyle={{ padding: 22, paddingTop: 8, gap: 6, paddingBottom: 60 }}
  +contentContainerStyle={{ padding: 22, paddingTop: 8, gap: 6, paddingBottom: 64 + 20 + 24 }}
  (idem MatchesScreen listContent; mejor useBottomTabBarHeight() + 16)
  ```

### I21. Cerrar sesion no funciona en web
- **Grupo:** UI1-12 · **Área:** auth · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-8.png
- **Causa:** SettingsScreen.tsx:401-415 handleLogout envuelve todo en Alert.alert con botones; react-native-web implementa Alert.alert como no-op, el callback onPress nunca corre. AuthContext.logout (:93-106) funciona si se invoca.
- **Fix propuesto** (no aplicado):
  ```diff
  --- SettingsScreen.tsx
   const handleLogout = () => {
  +  const doLogout = async () => { await spotifyLogout(); await logout(); router.replace('/(auth)/login') }
  +  if (Platform.OS === 'web') { if (window.confirm('Seguro que quieres salir?')) doLogout(); return }
     Alert.alert('Cerrar Sesion', ..., [{ text: 'Cancelar', style: 'cancel' }, { text: 'Cerrar Sesion', style: 'destructive', onPress: doLogout }])
  ```

### I22. Radar con cifras hardcodeadas e incoherentes
- **Grupo:** UI1-4 · **Área:** radar · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-2.png, capturas/ui2-11.png
- **Causa:** config/onboarding.ts:5 RADAR_SWIPES_THRESHOLD = 100 (backend: 25); RadarScreen.tsx:152 "247 personas" literal; RadarScreen.tsx:~147 Subtitle usa la constante sin consultar a la API; LoginScreen.tsx:132 imprime la constante.
- **Fix propuesto** (no aplicado):
  ```diff
  --- config/onboarding.ts
  -export const RADAR_SWIPES_THRESHOLD = 100;
  +export const RADAR_SWIPES_THRESHOLD = 25; // = Users.SwipeCount >= 25 del backend
  --- RadarScreen.tsx
  -<StatsText>personas con tu vibe cerca</StatsText>
  +// quitar el 247 o mostrar matches.length de getEventMatches; si 403, mostrar 'Te faltan N swipes'
  ```

### I23. Afinidad 99-100% y Match Certificado para todos; Conectar/Pasar placebo
- **Grupo:** UI1-5 · **Área:** radar-matches · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-3.png, capturas/ui2-13.png
- **Causa:** RadarMatchesScreen.tsx:68-70 handleConnect solo hace setRequested; :104-106 'Pasar' sin onPress; :61 isCertified = isHighPriority || score01 > 0.9; avatares de i.pravatar.cc (:18, :83) = host externo.
- **Fix propuesto** (no aplicado):
  ```diff
  --- RadarMatchesScreen.tsx
  -<TouchableOpacity style={styles.btnPass}>
  +<TouchableOpacity style={styles.btnPass} onPress={() => setMatches(p => p.filter(m => m.id !== item.id))}>
  ...
  -const handleConnect = (id: string) => { setRequested(prev => new Set(prev).add(id)) }
  +// requiere endpoint 'conectar' en el back; hasta entonces ocultar el boton o marcarlo 'Pronto' en vez de simular exito
  ```

### I24. Conexiones: nombres Match XXXX duplicados, aceptar al abrir y la lista no cambia
- **Grupo:** UI1-6 · **Área:** matches · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-4.png
- **Causa:** Nombres: backend devuelve displayName "Match xxxx" (PendingMatch) y ChatScreen.tsx:80-83 genera name `Match ${roomId.slice(0,4)}`. MatchesScreen.tsx:22,50-51 el estado accepted vive en useState local (openedRooms), :53-62 handleConnect acepta al tocar. Backend /Matches/pending no excluye los ya aceptados.
- **Fix propuesto** (no aplicado):
  ```diff
  --- MatchesScreen.tsx
  -const [openedRooms, setOpenedRooms] = useState<Set<string>>(new Set());
  +// el back debe devolver 'accepted' por match (o endpoint /Matches/active); derivar pending/chats de ese campo
  --- backend: PendingMatch { matchId, displayName (nombre real del otro usuario), accepted }
  --- MatchesScreen.tsx listContent
  -paddingBottom: 40
  +paddingBottom: 120
  ```

### I25. Perfil con spinners infinitos y datos genericos sin Spotify
- **Grupo:** UI1-8 · **Área:** perfil · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-9.png
- **Causa:** ProfileScreen.tsx lee user de Spotify/vibeVector; sin Spotify no hay datos y no existe estado vacio. useBackendUser ya trae displayName de User/me.
- **Fix propuesto** (no aplicado):
  ```diff
  --- ProfileScreen.tsx
  -const name = spotifyUser?.name ?? 'Encorely User'
  +const name = spotifyUser?.name ?? backendUser?.displayName ?? 'Encorely User'
  +// si !vibeVector && !isLoadingVibe: renderizar estado vacio 'Aun no hay ADN musical' en vez del spinner
  ```

### I26. Settings placebo: Guardar y toggles solo escriben localStorage
- **Grupo:** UI1-9 · **Área:** settings · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-8.png
- **Causa:** SettingsScreen.tsx:40-41 PROFILE_KEY/RADAR_VISIBLE_KEY (persistencia local); :396 usa Alert.alert (no-op en web) para confirmaciones, por eso no hay feedback; no hay llamada a updateSettings/PUT User/settings.
- **Fix propuesto** (no aplicado):
  ```diff
  --- SettingsScreen.tsx
     await AsyncStorage.setItem(PROFILE_KEY, ...)
  +  await updateUserSettings({ userId: backendUserId, mood })  // PUT /User/settings (ya existe en clients/api/user.ts)
  +  setToast('Cambios guardados')   // inline, no Alert.alert
  +useEffect(() => { precargar con getMe(backendUserId) }, [])
  +onChangeText={(t) => { setName(t); setErrors(e => ({ ...e, name: undefined })) }}
  ```

### I27. Radar invita a usuarios con <25 swipes (felicitacion + 247 personas) y luego lo bloquea con un texto tenue sin progreso
- **Grupo:** UI2-1 · **Área:** radar · **Origen:** ui2 · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Login con usuario qau2 de 24 swipes
  2. Tab Radar
  3. Pulsar Ver Personas Compatibles
- **Evidencia:** capturas/ui2-11.png, capturas/ui2-12.png
- **Causa:** RadarScreen.tsx:~147-152 no consulta swipeCount (me.swipeCount de GET User/me ya llega) ni el umbral real; RadarMatchesScreen.tsx:55-58 solo cubre el 403 con un texto.
- **Fix propuesto** (no aplicado):
  ```diff
  --- RadarScreen.tsx
  +const { swipeCount } = useBackendUser()
  +const locked = swipeCount < 25
  -<Subtitle>Felicidades! Alcanzaste el umbral de los 100 swipes...</Subtitle>
  +<Subtitle>{locked ? `Te faltan ${25 - swipeCount} swipes para desbloquear el radar.` : 'Tu vibe esta listo.'}</Subtitle>
  +<PrimaryButton disabled={locked} ...>
  ```

## Menores (17)

### M1. GET /Swipes/next-track devuelve siempre la misma canción hardcodeada
- **Grupo:** F10 · **Área:** descubrimiento · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — getNextTrack sin llamadores.
- **Cómo se reproduce:**
  1. GET /Swipes/next-track?userId=<qavf1-dna> antes de swipear
  2. Hacer 25 swipes
  3. GET /Swipes/next-track de nuevo
- **Evidencia:** tmp/qavf1/out_desc.txt (F10 antes/después)
- **Por qué esta severidad:** stub sin consumidor: el front no llama a next-track, así que el usuario no lo sufre hoy; es deuda de contrato (pasaría a importante si el deck se mueve al back).
- **Causa:** EncorelyApplication/Services/SwipeService.cs:50-60 (return new { SpotifyId = "pop_track_456" ... } / "Vampire Empire").
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/Services/SwipeService.cs
       public async Task<object> GetNextTrackAsync(Guid userId, CancellationToken ct = default)
       {
  -        var user = await _usuarioQueries.GetByIdAsync(userId);
  -        if (user != null && user.Provider != AuthProvider.Spotify) { ... return new { SpotifyId = "pop_track_456", ... }; }
  -        return new { SpotifyId = "5upAn8J6pXW4G4lWf3P4", ... };
  +        var user = await _usuarioQueries.GetByIdAsync(userId) ?? throw new NotFoundException("Usuario");
  +        var seen = await _swipeQueries.GetTrackIdsByUserAsync(userId);
  +        var next = await _trackCatalog.GetRandomExcludingAsync(seen, ct) ?? throw new NotFoundException("Track");
  +        return new { next.SpotifyId, next.Name, next.Artist, next.PreviewUrl };
  
  Recomendada: catálogo local de tracks + exclusión de los ya swipeados (sirve también al ADN de F7). Alternativa: eliminar el endpoint y documentar que el deck viene del front, para no dejar un stub en el contrato.
  ```

### M2. Chat: GET y POST de mensajes con shapes distintos (GET sin id, POST sin senderId), sin paginación, mensaje de sistema con senderId Guid.Empty pintado como del otro usuario, y sin tiempo real en ChatScreen
- **Grupo:** F17 · **Área:** social · **Origen:** api · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre — Mensaje de sistema se pinta como burbuja ajena; sin tiempo real no verificado.
- **Cómo se reproduce:**
  1. Login demo
  2. GET /api/v1/Chat/22222222-2222-4222-8222-000000000001/messages?userId=<demo> (y con &limit=2&pageSize=2)
  3. POST /Chat/…0001/messages {content:'QAVF2- objeto'} y comparar keys
- **Evidencia:** Reproducido 2026-10-14T02:18Z: "chat GET 200 13 ['content','senderId','timestamp'] system msgs 2"; "limit param ignored? 13"; POST 200 {id,content,timestamp}. grep 'signalr|HubConnection' en encorely-front/src: sin resultados. (`capturas/ui3-2.png`)
- **Por qué esta severidad:** molestia visible (bienvenida como si la escribiera el otro, sin tiempo real) y deuda de contrato; no rompe flujo ni expone datos.
- **Causa:** encorly_back/EncorelyApplication/Services/MatchService.cs:69-70 (GET proyecta {SenderId,Content,Timestamp}) vs :92 (POST devuelve {Id,Content,Timestamp}); MatchService.cs:56 SenderId = Guid.Empty para sistema; sin parámetros de página en ChatController.cs:24 ni en IMessageQueries.GetByMatchIdAsync. Front: encorely-front/src/modules/chat/screens/ChatScreen.tsx:93-97 (id inventado con timestamp-índice, isMe) y :119-121 (solo carga al montar).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyApplication/DTOs/Responses.cs
  +public record ChatMessageDto(Guid Id, Guid? SenderId, string Content, DateTime Timestamp, bool IsSystem);
  --- a/encorly_back/EncorelyApplication/Services/MatchService.cs
  +++ b/encorly_back/EncorelyApplication/Services/MatchService.cs
  -    public async Task<IEnumerable<object>> GetChatMessagesAsync(Guid roomId, CancellationToken ct = default)
  +    public async Task<IEnumerable<ChatMessageDto>> GetChatMessagesAsync(Guid roomId, DateTime? before, int limit = 50, CancellationToken ct = default)
       {
  -        var messages = await _messageQueries.GetByMatchIdAsync(roomId);
  -        return messages.OrderBy(m => m.Timestamp).Select(m => new { SenderId = m.SenderId, Content = m.Content, Timestamp = m.Timestamp });
  +        var messages = await _messageQueries.GetPageByMatchIdAsync(roomId, before, Math.Clamp(limit, 1, 100)); // ORDER BY CreatedAt DESC LIMIT @Limit
  +        return messages.OrderBy(m => m.Timestamp).Select(ToDto);
       }
  +    private static ChatMessageDto ToDto(Message m) =>
  +        new(m.Id, m.SenderId == Guid.Empty ? null : m.SenderId, m.Content, m.Timestamp, m.SenderId == Guid.Empty);
  @@ SendMessageAsync
  -        return new { message.Id, message.Content, Timestamp = message.Timestamp };
  +        return ToDto(message);
  --- a/encorely-front/src/modules/chat/screens/ChatScreen.tsx
  -      id: m.id ?? `${m.timestamp}-${idx}`,
  +      id: m.id,
  +      isSystem: m.isSystem,
  (+ render centrado para isSystem y suscripción al hub o polling cada 5 s mientras la pantalla está enfocada)
  Recomendada: DTO único + paginación por cursor (before=timestamp). Alternativa: solo añadir Id al GET y SenderId al POST (arregla keys y isMe, deja sin resolver historial ilimitado).
  ```

### M3. GET /Venue/{roomId}/messages devuelve la entidad VenueMessage cruda (roomId, senderId=userId real, isModerated) sin nombre público del remitente ni paginación
- **Grupo:** F17 · **Área:** venue · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. Login demo
  2. GET /api/v1/Venue/44444444-4444-4444-8444-000000000002/messages
  3. Repetir con ?limit=2
- **Evidencia:** Reproducido 2026-10-14T02:18Z: "venue GET 200 21 ['content','id','isModerated','roomId','senderId','timestamp']"; "venue limit 21".
- **Por qué esta severidad:** sin pantalla de Venue en el front; la fuga de userId solo importa por los IDOR ya reportados.
- **Causa:** encorly_back/EncorelyApi/Controllers/VenueController.cs:32-36 hace Ok(messages) con IEnumerable<VenueMessage>; EncorelyApplication/Services/VenueService.cs:50-54 devuelve el modelo; EncorelyQuery/Implementations/VenueMessageQueries.cs:24-29 sin LIMIT ni JOIN a Users.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyQuery/Implementations/VenueMessageQueries.cs
  +    public async Task<IEnumerable<VenueMessageView>> GetActivePageAsync(Guid roomId, DateTime? before, int limit)
  +    {
  +        using var connection = _connectionFactory.CreateConnection();
  +        const string sql = @"SELECT m.""Id"", m.""Content"", m.""CreatedAt"" AS ""Timestamp"", u.""DisplayName"" AS SenderDisplayName
  +            FROM ""VenueMessages"" m JOIN ""Users"" u ON u.""Id"" = m.""SenderId""
  +            WHERE m.""RoomId"" = @RoomId AND NOT m.""IsModerated"" AND (@Before IS NULL OR m.""CreatedAt"" < @Before)
  +            ORDER BY m.""CreatedAt"" DESC LIMIT @Limit";
  +        return await connection.QueryAsync<VenueMessageView>(sql, new { RoomId = roomId, Before = before, Limit = limit });
  +    }
  --- a/encorly_back/EncorelyApi/Controllers/VenueController.cs
  -    public async Task<IActionResult> GetMessages(Guid roomId, CancellationToken ct)
  +    public async Task<IActionResult> GetMessages(Guid roomId, [FromQuery] DateTime? before, [FromQuery] int limit = 50, CancellationToken ct = default)
       {
  -        var messages = await _venueService.GetActiveMessagesAsync(roomId, ct);
  +        var messages = await _venueService.GetActiveMessagesAsync(roomId, before, Math.Clamp(limit, 1, 100), ct);
           return Ok(messages);
  Recomendada: DTO de vista con JOIN y filtro de moderados en SQL (hoy se filtra en memoria tras traer todo). Alternativa: proyectar en VenueService a {Id,Content,Timestamp} sin senderId (quita la fuga, no da nombre).
  ```

### M4. Mensaje bloqueado por moderación responde 200 como si se hubiera publicado (sin aviso al remitente)
- **Grupo:** F19 · **Área:** venue · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. Login demo
  2. POST /api/v1/Venue/44444444-4444-4444-8444-000000000002/messages?userId=<demo> {content:'QAVF2-spam'}
  3. GET /Venue/…0002/messages
  4. POST {content:'QAVF2-<script>alert(1)</script> 🎸'}
- **Evidencia:** Reproducido 2026-10-14T02:19Z. Respuesta con keys [content,id,isModerated,timestamp] tanto si se publica como si se bloquea.
- **Por qué esta severidad:** sin pantalla de Venue en el front hoy; combinado con los falsos positivos de persona-venue-6 el usuario pierde mensajes sin saberlo.
- **Causa:** encorly_back/EncorelyApi/Controllers/VenueController.cs:43-52 devuelve Ok(...) sin distinguir isModerated; EncorelyApplication/Services/VenueService.cs:61-80 no comunica el motivo.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyApi/Controllers/VenueController.cs
  +++ b/encorly_back/EncorelyApi/Controllers/VenueController.cs
  @@ PostMessage
           var message = await _venueService.PostMessageAsync(roomId, userId, request.Content, ct);
   
  -        if (!message.IsModerated)
  -        {
  -            await _hubContext.Clients.Group($"venue_{roomId}")
  -                .SendAsync("ReceiveVenueMessage", userId, request.Content, message.Timestamp);
  -        }
  +        if (message.IsModerated)
  +            return UnprocessableEntity(new { message = "Tu mensaje no cumple las normas de la sala y no se publicó.", message.Id });
  +
  +        await _hubContext.Clients.Group($"venue_{roomId}")
  +            .SendAsync("ReceiveVenueMessage", userId, request.Content, message.Timestamp);
  Recomendada: 422 con mensaje en español (el cliente lo muestra sin lógica extra). Alternativa: mantener 200 y añadir moderationNotice en el cuerpo (compatible hacia atrás, pero fácil de ignorar en el cliente).
  ```

### M5. GET de mensajes de una sala inexistente devuelve 200 [] (indistinguible de sala vacía) y crear sala no valida eventId
- **Grupo:** F20 · **Área:** venue · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. Login demo
  2. GET /api/v1/Venue/99999999-9999-4999-8999-999999999999/messages
  3. POST /api/v1/Venue/NOEXISTE/rooms?name=QAVF2-x
  4. (contraste) POST /Venue/99999999-…/messages?userId=<demo> {content:'QAVF2-x'}
- **Evidencia:** Reproducido 2026-10-14T02:20Z. /Events/feed solo expone TM_001..TM_004; las salas seed usan EV_BOG_001/EV_BOG_002/EV_MED_001 que no están en ese catálogo.
- **Por qué esta severidad:** sin pantalla de Venue en el front; el cliente no distingue sala inválida de vacía.
- **Causa:** encorly_back/EncorelyApplication/Services/VenueService.cs:50-54 (GetActiveMessagesAsync no consulta la sala) y :36-47 (CreateVenueRoomAsync sin validar eventId); EncorelyApplication/Services/VenueService.cs:58-59 lanza InvalidOperationException que el middleware convierte en 500.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/encorly_back/EncorelyApplication/Services/VenueService.cs
  +++ b/encorly_back/EncorelyApplication/Services/VenueService.cs
  @@ GetActiveMessagesAsync
       {
  +        var room = await _roomQueries.GetByIdAsync(roomId);
  +        if (room == null) throw new NotFoundException("Sala");
           var messages = await _messageQueries.GetByRoomIdAsync(roomId);
  @@ PostMessageAsync
           var room = await _roomQueries.GetByIdAsync(roomId);
  -        if (room == null || !room.IsActive)
  -            throw new InvalidOperationException("La sala de venue no está activa o ha expirado.");
  +        if (room == null) throw new NotFoundException("Sala");
  +        if (!room.IsActive) throw new RoomExpiredException(); // nueva AppException → 409/410
  @@ CreateVenueRoomAsync
  +        if (!await _eventCatalog.ExistsAsync(eventId)) throw new NotFoundException("Evento");
  Recomendada: 404 vía NotFoundException (ya mapeada por el middleware de AppException) en GET/POST; validación de eventId cuando exista catálogo persistido. Alternativa: 204 No Content para sala vacía y 404 para inexistente desde el controller.
  ```

### M6. Contraseña truncada a 72 bytes por bcrypt y sin longitud máxima
- **Grupo:** F4 · **Área:** cuenta · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Password truncada ocurre en register (sin pantalla).
- **Cómo se reproduce:**
  1. POST /Auth/register {email:'qavf1-trunc@example.com',password:'a'*72+'X'} -> 202
  2. POST /Auth/login password 'a'*72+'Y' -> 200
  3. POST /Auth/login password 'a'*72 -> 200
  4. POST /Auth/register {email:'qavf1-p100k@example.com',password:'a'*100000} -> 202 (0.13 s)
- **Evidencia:** tmp/qavf1/out_cuenta.txt (### F4)
- **Por qué esta severidad:** comportamiento conocido de bcrypt; solo afecta a passwords > 72 bytes (raro) y no hay DoS medible.
- **Causa:** EncorelyApplication/Services/IdentityService.cs:110 (BCrypt.HashPassword sin límite) y :71 (Verify); EncorelyApplication/DTOs/Requests.cs:13 (solo MinLength(6)).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApplication/DTOs/Requests.cs
  -    [Required, MinLength(6)] string Password
  +    [Required, MinLength(8), MaxLength(72)] string Password
  
  Recomendada: MaxLength(72) en registro (y validar bytes UTF-8 en el servicio, 72 bytes != 72 chars con acentos/emoji). Alternativa: pre-hash SHA-256 + base64 antes de bcrypt (BCrypt.EnhancedHashPassword) para admitir passphrases largas; obliga a migrar hashes existentes.
  ```

### M7. Errores en inglés/técnicos, dos formatos de error y códigos HTTP incorrectos (202 en register, 400 en login, 404/200 sin userId)
- **Grupo:** F5 · **Área:** transversal · **Origen:** api · **Estado:** confirmado · también lo reportaron: persona-cuenta-1, persona-cuenta-7, persona-cuenta-10, persona-descubrimiento-15, persona-descubrimiento-16, persona-social-9, persona-social-10
- **En el front:** tapado: solo por API — Login UI traduce errores a "Email o contrasena incorrectos." / "Ingresa tu email y contrasena."; el 400 de chat si se ve (ver F13).
- **Cómo se reproduce:**
  1. POST /Auth/register {email:'noesmail'}
  2. POST /Auth/login password mala / password 'mal' (cuenta-7)
  3. POST /Auth/register duplicado
  4. POST /Auth/register body {"email":123}
  5. POST /Auth/register válido -> status (cuenta-1)
  6. GET /User/me sin userId (cuenta-10)
  7. GET radar targetMood=-1|3|VIP|vip|'' y sin userId (descubrimiento-15)
  8. POST swipe sin trackId / direction:'Right' (descubrimiento-16)
  9. POST /Chat/…/messages content ''|'   '|1001 chars (social-9)
  10. GET /Matches/pending sin userId|abc|Guid.Empty y accept de match inexistente (social-10)
- **Evidencia:** tmp/qavf1/out_cuenta.txt (### F5) y tmp/qavf1/out_f2f5.txt (### F5 dups) (`capturas/ui3-5.png`)
- **Por qué esta severidad:** cosmético/DX; la única consecuencia funcional es que el front no puede mostrar un mensaje coherente.
- **Causa:** EncorelyApplication/Exceptions/AppException.cs:10-22 (mensajes en inglés); EncorelyApi/Controllers/AuthController.cs:22,29,36 (Accepted); EncorelyApplication/DTOs/Requests.cs:11-14 (mismo DTO con MinLength para login y registro); EncorelyApi/Controllers/UserController.cs:24-27 y MatchesController.cs:20 (userId de query sin [Required], Guid.Empty pasa); EventsController.cs:46 (NotFound con string plano); Program.cs:119-123 (sin InvalidModelStateResponseFactory que unifique formato/idioma).
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Program.cs
   builder.Services.AddControllers()
  +    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
  +        new BadRequestObjectResult(new { statusCode = 400, message = "Datos inválidos.",
  +            errors = ctx.ModelState.Where(e => e.Value!.Errors.Any()).ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage)) }))
  --- a/EncorelyApplication/Exceptions/AppException.cs
  -    public NotFoundException(string resource) : base($"{resource} not found.") { }
  +    public NotFoundException(string resource) : base($"{resource} no encontrado.") { }
  -    public DuplicateEmailException() : base("A user with that email already exists.") { }
  +    public DuplicateEmailException() : base("Ya existe una cuenta con ese correo.") { }
  -    public InvalidCredentialsException() : base("Invalid email or password.") { }
  +    public InvalidCredentialsException() : base("Correo o contraseña incorrectos.") { }
  --- a/EncorelyApi/Controllers/AuthController.cs
  -        return Accepted(response);   // register
  +        return StatusCode(StatusCodes.Status201Created, response);
  --- a/EncorelyApplication/DTOs/Requests.cs
  +public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password); // sin MinLength: siempre 401 genérico
  
  Recomendada: factory única de errores + mensajes de dominio en español + userId tomado del claim del JWT (elimina de paso los 404/200 por userId ausente). Alternativa: traducir en el front (formatApiError) por código/clave de error, manteniendo la API en inglés.
  ```

### M8. POST /Venue/{eventId}/rooms sin límites de negocio: durationHours 0/-1/1000000 y name sin máximo pasan la validación
- **Grupo:** F5-b · **Área:** venue · **Origen:** api · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. POST /Venue/EV_BOG_001/rooms?name=QAVF1-d0&durationHours=0
  2. Repetir con durationHours=-1 y 1000000
- **Evidencia:** tmp/qavf1/out_f2f5.txt (venue dur 0/-1/1000000 -> 500 42703 desde VenueRoomRepository.cs:24)
- **Por qué esta severidad:** endpoint sin UI y hoy inalcanzable por C10; se vuelve importante en cuanto C10 se arregle.
- **Causa:** EncorelyApi/Controllers/VenueController.cs:25-28 (string name / int durationHours sin rangos); EncorelyApplication/Services/VenueService.cs:36-47; EncorelyRepository/Implements/VenueRoomRepository.cs:19-20.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Controllers/VenueController.cs
  -    public async Task<IActionResult> CreateRoom(string eventId, [FromQuery] string name, [FromQuery] int durationHours = 4, CancellationToken ct = default)
  +    public async Task<IActionResult> CreateRoom(string eventId, [FromQuery, Required, StringLength(80, MinimumLength = 3)] string name,
  +        [FromQuery, Range(1, 48)] int durationHours = 4, CancellationToken ct = default)
  --- a/EncorelyRepository/Implements/VenueRoomRepository.cs
  -            INSERT INTO ""VenueRooms"" (""Id"", ""Name"", ""Description"", ""Capacity"", ""IsActive"", ""CreatedAt"")
  -            VALUES (@Id, @Name, @Description, @Capacity, @IsActive, @CreatedAt)
  +            INSERT INTO ""VenueRooms"" (""Id"", ""EventId"", ""Name"", ""IsActive"", ""ExpiresAt"")
  +            VALUES (@Id, @EventId, @Name, true, @ExpiresAt)
  
  Recomendada: Range/StringLength en el controller + insertar EventId/ExpiresAt. Alternativa: validar en VenueService y lanzar excepción de dominio 400.
  ```

### M9. /Auth/*: refreshToken emitido y persistido pero sin endpoint que lo consuma; register/login devuelven 202
- **Grupo:** S10 · **Área:** exposición · **Origen:** seguridad-b · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Front no usa refreshToken ni /Auth/refresh.
- **Cómo se reproduce:**
  1. POST /Auth/login -> {accessToken,refreshToken,expiration,userId}
  2. Revisar swagger.json: no hay /Auth/refresh ni /Auth/logout
  3. POST /Auth/login con password <6 -> 400 (política filtrada)
- **Evidencia:** swagger paths Auth: /Auth/spotify,/Auth/google,/Auth/register,/Auth/login (sin refresh/logout). IdentityService.cs:79-83 y 145-150 persisten user.RefreshToken.
- **Causa:** EncorelyApplication/Services/IdentityService.cs:79-83 (ProcessIdentityAsync) y :145-150 (LoginWithEmailAsync) guardan RefreshToken; no existe AuthController.Refresh. AuthController.cs:34-39 register/login devuelven Accepted/Ok. DTOs/Requests.cs:11 EmailAuthRequest aplica [MinLength(6)] también en login.
- **Fix propuesto** (no aplicado):
  ```diff
  Implementar POST /Auth/refresh con rotación y hash del refresh token en BD, o eliminar el campo si no se usará. Quitar [MinLength(6)] del DTO de login (la política de longitud solo aplica en registro) y devolver 201 Created en register. Severidad menor: no expone secretos de terceros, es superficie muerta + UX.
  ```

### M10. GET /Venue/{room}/messages: cualquier usuario autenticado lee salas de evento sin ser participante
- **Grupo:** S13 · **Área:** acceso · **Origen:** seguridad-a · **Estado:** confirmado
- **En el front:** no aplica (sin pantalla) — Sin pantalla Venue.
- **Cómo se reproduce:**
  1. Token qavs-atacante (sin relación con la sala)
  2. GET /Venue/44444444-4444-4444-8444-000000000001/messages -> 200
- **Evidencia:** GET /Venue/44444444-...0001/messages tokenA -> 200 [{id:55555555-...0001,senderId:11111111-...0002,content:'¿Alguien se va en bus...?',...}]
- **Causa:** EncorelyApi/Controllers/VenueController.cs:34 GetMessages no comprueba membresía; VenueService.GetActiveMessagesAsync solo filtra IsModerated. No hay concepto de miembro de sala en el modelo.
- **Fix propuesto** (no aplicado):
  ```diff
  Si las salas deben ser restringidas: introducir membresía (p.ej. haber hecho Join/estar en el evento) y validarla en GetMessages (403 si no es miembro). Si son públicas por diseño, dejar como está pero no exponer senderId crudo (devolver displayName). Severidad menor: no expone PII más allá de Guids y las salas son por naturaleza grupales/efímeras.
  ```

### M11. Superficie pública: Swagger/health anónimos, sin cabeceras de seguridad; /health filtra ex.Message en fallo (no gated)
- **Grupo:** S7 · **Área:** configuración · **Origen:** seguridad-b · **Estado:** confirmado · también lo reportaron: seguridad-a-2, seguridad-a-3, seguridad-b-3, seguridad-b-4
- **En el front:** no aplica (sin pantalla) — Swagger/health: superficie del back, el front no usa /health.
- **Cómo se reproduce:**
  1. GET /swagger/v1/swagger.json -> 200; GET /docs/index.html -> 200
  2. GET /health -> 200 (anónimo)
  3. curl -i /health: revisar X-Content-Type-Options/X-Frame-Options/CSP/HSTS
- **Evidencia:** /swagger.json 200; /docs/index.html 200; /health 200; headers {X-Content-Type-Options:None, X-Frame-Options:None, CSP:None, HSTS:None, Server:Kestrel}. Program.cs:163-168 UseSwagger/UseSwaggerUI fuera de IsDevelopment (bloque IsDevelopment vacío, :170-173); Program.cs:201 dbStatus['error']=ex.Message sin condicionar por entorno.
- **Causa:** Program.cs:163-168 (Swagger incondicional), :170-173 (bloque IsDevelopment vacío), :181-219 (/health AllowAnonymous con dbStatus['error']=ex.Message en el catch, :201), ausencia de middleware de cabeceras/UseHsts y AddServerHeader=true por defecto.
- **Fix propuesto** (no aplicado):
  ```diff
  --- a/EncorelyApi/Program.cs
  +++ b/EncorelyApi/Program.cs
  @@
  -app.UseSwagger();
  -app.UseSwaggerUI(c => { ... });
  +if (app.Environment.IsDevelopment())
  +{
  +    app.UseSwagger();
  +    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Encorely API v1"); c.RoutePrefix = "docs"; });
  +}
  @@ /health catch
  -        dbStatus["error"] = ex.Message;
  +        _logger.LogError(ex, "Health DB check failed");
  +        // no exponer ex.Message al cliente anónimo
  Añadir middleware de cabeceras (X-Content-Type-Options: nosniff, X-Frame-Options: DENY, Referrer-Policy, CSP), app.UseHsts() en Production y Kestrel AddServerHeader=false; cabeceras en vercel.json para el front.
  ```

### M12. Desktop: sin max-width, estilos incoherentes
- **Grupo:** UI1-11 · **Área:** diseno · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-1.png, capturas/ui2-3.png, capturas/ui2-4.png
- **Causa:** Solo OnboardingStep3/4 (max-width: 400px) y ProfileScreen:176 limitan ancho; MatchesScreen/RadarMatchesScreen/LoginScreen no. layout/ScreenShell.tsx no aplica contenedor.
- **Fix propuesto** (no aplicado):
  ```diff
  --- layout/ScreenShell.tsx
  +<View style={{ flex: 1, width: '100%', maxWidth: 720, alignSelf: 'center' }}>{children}</View>
  ```

### M13. Accesibilidad: botones primarios sin rol y foco por Tab
- **Grupo:** UI1-13 · **Área:** accesibilidad · **Origen:** ui1 (verificado por ui2) · **Estado:** intermitente
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-1.png
- **Causa:** LoginScreen.tsx:149-152 TouchableOpacity sin accessibilityRole; ChatScreen.tsx styles.timeText fontSize 10; RadarMatchesScreen badgeText fontSize 10; (main)/_layout.tsx tabLabel fontSize 10.
- **Fix propuesto** (no aplicado):
  ```diff
  --- LoginScreen.tsx:149
   <TouchableOpacity
  +  accessibilityRole="button"
  +  accessibilityLabel="Conectar con Spotify"
  --- ChatScreen/RadarMatches: fontSize 10 -> 12 y color rgba(255,255,255,0.4) -> 0.65 (WCAG AA 4.5:1)
  ```

### M14. Discover queda en Preparando tracks infinito para usuario email
- **Grupo:** UI1-2 · **Área:** discover · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-4.png (tab bar con Discover) y observacion de texto "Preparando tracks..."
- **Causa:** SwipeScreen.tsx:109-111 fetchMoreTracks hace 'if (!token) return' ANTES de setFetchAttempted(true); loading inicia en true y el gate de SwipeScreen.tsx:~190 muestra el spinner mientras loading && tracks.length===0. El feed solo usa Spotify (spotifySwipeService), nunca GET /Swipes/next-track (clients/api/swipes.ts:6 existe sin uso).
- **Fix propuesto** (no aplicado):
  ```diff
  --- SwipeScreen.tsx
     const fetchMoreTracks = useCallback(async () => {
       const token = await getValidToken();
  -    if (!token) return;
  +    if (!token) {
  +      setLoading(false);
  +      setFetchAttempted(true);   // cae al estado vacio con mensaje/reintento
  +      return;
  +    }
  (mejor: sin token Spotify, usar getNextTrack(backendUserId) como fuente)
  ```

### M15. Login: sin validacion de formato, sin estado de carga, botones sin rol
- **Grupo:** UI1-3 · **Área:** login · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-1.png
- **Causa:** LoginScreen.tsx:67-71 y :149-152.
- **Fix propuesto** (no aplicado):
  ```diff
  --- LoginScreen.tsx
  +    if (!/^\S+@\S+\.\S+$/.test(email.trim())) { setEmailError('Escribe un email valido.'); return }
  ...
   <TouchableOpacity
  +  accessibilityRole="button"
  +  accessibilityLabel="Conectar con Spotify"
     style={[styles.btn, isLoggingIn && styles.btnLoading]}
  ```

### M16. Enviar mensaje falla en silencio: POST 400
- **Grupo:** UI1-7 · **Área:** chat · **Origen:** ui1 (verificado por ui2) · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Ver pasos en ola1/ui1.json
- **Evidencia:** capturas/ui2-5.png, capturas/ui2-6.png
- **Causa:** clients/api/matches.ts:34 body: content manda un string JSON, pero ChatController.cs:39 espera [FromBody] SendMessageRequest {Content}. ChatScreen.tsx:140 guarda el error en setError, que solo se pinta en ListEmptyComponent (:184-187); con la lista no vacia el error nunca se ve y el mensaje optimista se revierte (:141-143).
- **Fix propuesto** (no aplicado):
  ```diff
  --- clients/api/matches.ts
  -    body: content,
  +    body: { content },
  --- ChatScreen.tsx
  +{error && messages.length > 0 && <Text style={styles.sendError}>{error}</Text>}  // banner sobre el input
  -    setInputText('')   // limpiar solo tras exito, o restaurar inputText en el catch
     <StyledInput ... multiline
  +  blurOnSubmit={false} onKeyPress={e => { if (e.nativeEvent.key==='Enter' && !e.shiftKey) { e.preventDefault(); sendMessage() } }}
  ```

### M17. Chat sin limite ni contador de 1000 caracteres y boton enviar nunca deshabilitado
- **Grupo:** UI2-2 · **Área:** chat · **Origen:** ui2 · **Estado:** confirmado
- **En el front:** visible: el usuario lo sufre
- **Cómo se reproduce:**
  1. Abrir cualquier chat
  2. Escribir texto largo o dejar el campo vacio/espacios
  3. Observar input y boton
- **Evidencia:** capturas/ui2-5.png
- **Causa:** ChatScreen.tsx:195-204 StyledInput sin maxLength y TouchableOpacity sin disabled; sendMessage :119-122 return si vacio.
- **Fix propuesto** (no aplicado):
  ```diff
  --- ChatScreen.tsx
   <StyledInput
  +  maxLength={1000}
   ...
  -<TouchableOpacity onPress={sendMessage} style={styles.sendBtn}>
  +<TouchableOpacity onPress={sendMessage} disabled={inputText.trim().length === 0} style={[styles.sendBtn, inputText.trim().length === 0 && { opacity: 0.4 }]}>
  +{inputText.length > 900 && <Text style={styles.counter}>{inputText.length}/1000</Text>}
  ```

## Contraste API ↔ front

**La API responde bien, pero el front falla (UI-2):**
- `persona-cuenta/ok-3`: No hay pantalla de registro operativa: /step-3 "Continuar con correo" no llama a la API (0 peticiones, sin mensaje). Las validaciones de Auth/register no son alcanzables por un usuario real.
- `persona-descubrimiento/ok-1`: Con 24 swipes: /radar-matches muestra "Completa 25 swipes para desbloquear el radar." (coincide con 403) pero la pantalla Radar previa dice "Felicidades! Alcanzaste el umbral de los 100 swipes" y "247 personas cerca" y su CTA lleva al 403. Con 25 swipes: lista de 44 personas (coincide). Hallazgo ui2-1.
- `persona-social/ok-3`: El input no tiene maxLength ni contador ni aviso; con el envio roto (ui1-7) cualquier mensaje falla en silencio. Hallazgo ui2-2.
- `persona-social/ok-6`: pending -> aceptar (al tocar la fila: POST accept 200) -> chat abre -> ver respuesta de la otra persona OK, pero ENVIAR falla: POST Chat/{id}/messages 400 (el front manda string pelado), la burbuja optimista desaparece y no hay error visible. Ver ui1-7.

**Bugs de API que el usuario no provoca desde la UI (UI-3 "tapado"):** siguen siendo reales en seguridad.
- F12 Cada Like tras 25 swipes crea un Match automático con cualquier usuario, sin reciprocidad, sin mood y sin evento: El like que crea el match solo sale del swipe, inalcanzable sin Spotify; el resultado (matches sin reciprocidad) si aparece en listas.
- F7 El ADN musical no cambia con los swipes: perfil fijo 0.5/0.5/0.5 para todo usuario sin Spotify: Discover necesita tracks de Spotify; con login email queda en spinner "Preparando tracks..." (C13), no se pueden hacer swipes sin Spotify (no probado).
- F11 Swipes repetidos sobre la misma canción suman al umbral; direction ausente cuenta como Left; trackId sin validar: Swipes solo desde el mazo Spotify (tracks distintos); sin Spotify no hay swipes en UI.
- F2 Excepciones de validación/dominio/FK salen como 500 con message del driver y stackTrace (rutas absolutas del servidor): Front solo manda valores validos (mood sin pantalla, direction 0-2); el 500 de chat/venue no alcanzable por UI normal.
- F3 Email sin normalizar: sensible a mayúsculas (cuentas duplicadas por casing), sin trim y sin longitud máxima: Login normaliza email (trim+lowercase); registro sin pantalla.
- S6 500 por falta de validación (enum/FK/recurso inexistente); stackTrace y nombres de constraint solo en Development: Front solo envia direction 0/1/2, no tiene pantalla de settings/venue; chat a match inexistente solo alcanzable editando URL (no probado).
- S9 Email sensible a mayúsculas: cuentas duplicadas y login que no normaliza: LoginScreen aplica trim().toLowerCase(): " DEMO@Encorely.app " entro. Registro sin pantalla.
- F5 Errores en inglés/técnicos, dos formatos de error y códigos HTTP incorrectos (202 en register, 400 en login, 404/200 sin userId): Login UI traduce errores a "Email o contrasena incorrectos." / "Ingresa tu email y contrasena."; el 400 de chat si se ve (ver F13).

**Sin pantalla en el front (UI-3 "no aplica"):**
- F18 [conocido: C10] POST /Venue/{eventId}/rooms siempre 500: el INSERT referencia parámetros (@Description, @Capacity, @CreatedAt) que VenueRoom no tiene y omite EventId/ExpiresAt; el 500 devuelve stackTrace
- S2 Hubs SignalR /notificationHub y /venueHub anónimos y sin derivar identidad del token
- S3 POST /Auth/google emite JWT y crea usuario sin validar el token ante Google
- F1 Registro concurrente del mismo email: 1x202 y 7x500 (violación UQ_Users_Email sin mapear)
- F16 Notificación al aceptar match usa matchId como userId: la otra persona nunca se entera; el aceptante se notifica a sí mismo (SignalR y push)
- F19 Moderación de salas por substring sin normalizar: bloquea mensajes inocentes (episodio, podio, whatever, chateau, Scampi, melodiosa) y deja pasar variantes triviales (s p a m, sp4m, cirílico, ódio, h4te, violéncia, estafa)
- F6 PUT /User/settings sin campo mood resetea silenciosamente el mood a 0 (Moshpit)
- S12 Fallbacks de secretos en código (DB_PASSWORD, JWT_SECRET_KEY) y password del seed en comentario; bundle web sin secretos
- S4 DELETE /Venue/messages/{id} ('Admin endpoint') ejecutable por cualquier usuario sobre mensajes ajenos
- F10 GET /Swipes/next-track devuelve siempre la misma canción hardcodeada
- F17 GET /Venue/{roomId}/messages devuelve la entidad VenueMessage cruda (roomId, senderId=userId real, isModerated) sin nombre público del remitente ni paginación
- F19 Mensaje bloqueado por moderación responde 200 como si se hubiera publicado (sin aviso al remitente)
- F20 GET de mensajes de una sala inexistente devuelve 200 [] (indistinguible de sala vacía) y crear sala no valida eventId
- F4 Contraseña truncada a 72 bytes por bcrypt y sin longitud máxima
- F5-b POST /Venue/{eventId}/rooms sin límites de negocio: durationHours 0/-1/1000000 y name sin máximo pasan la validación
- S10 /Auth/*: refreshToken emitido y persistido pero sin endpoint que lo consuma; register/login devuelven 202
- S13 GET /Venue/{room}/messages: cualquier usuario autenticado lee salas de evento sin ser participante
- S7 Superficie pública: Swagger/health anónimos, sin cabeceras de seguridad; /health filtra ex.Message en fallo (no gated)

## Intermitentes y descartados

- **Intermitente** UI1-13 Accesibilidad: botones primarios sin rol y foco por Tab: Foco por Tab no concluyente con este arnes (igual que ui1); lo reproducible: boton Spotify sin rol y textos de 10px de bajo contraste. Entrar ya corregido.
- Ningún hallazgo se descartó en la verificación.

## Cobertura

- **Roles usados:** no hay roles en el sistema; se probó usuario A vs usuario B, usuarios seed (≥25 swipes), usuarios nuevos con 0, 24 y 25 swipes, cuentas Google desechables y peticiones sin token.
- **Áreas y endpoints probados:**
  - Cuenta: `POST /Auth/register`, `/Auth/login`, `/Auth/google`; `GET /User/me`; `PUT /User/settings`.
  - Descubrimiento: `GET /Swipes/next-track`, `POST /Swipes/interactions/swipe` (incluida concurrencia), `GET /Events/feed`, `GET /Events/{id}/matches`, efecto del Worker (Like → Match).
  - Social: `GET /Matches/pending`, `POST /Matches/{id}/accept`, `GET`/`POST /Chat/{id}/messages`, notificación SignalR al aceptar (cliente propio).
  - Salas: `POST /Venue/{eventId}/rooms`, `GET`/`POST /Venue/{room}/messages`, `DELETE /Venue/messages/{id}`, `POST /Playlist/dna-mix` (solo bodies inválidos).
  - Plataforma: `/health`, `/docs`, `/swagger`, negotiate de `/notificationHub` y `/venueHub`, CORS, cabeceras, rutas sensibles servidas, bundle web, rate limit de login (10 intentos).
  - Front (navegador): login email, onboarding 1-6 sin Spotify, Discover, Radar, RadarMatches, Conexiones, Chat, Perfil, Settings, recarga dura, consola.

- **No probado y por qué:**
  - Login con Spotify, `/Auth/spotify` y `/Playlist/dna-mix` con tokens reales: llaman a la API real de Spotify.
  - WebSockets de SignalR (abrir conexión y enviar por hub): regla de no atacar. Solo se probó negotiate y un cliente de escucha para la notificación de match.
  - Fuga de `/health` con la BD caída: no se tumbó la BD; verificado por código.
  - Sala expirada: requería escribir en BD.
  - Ancho móvil real de 390 px: `resize_window` no cambia el viewport en este arnés; se aproximó con iframes.
  - Navegación por teclado: resultado intermitente, posible limitación del arnés.
  - Producción (Render, Vercel, Supabase): fuera de alcance por regla de entorno local.
  - Rama `codex/actualizar-frontend` (Expo 57): no desplegada localmente.

## Antes de la próxima corrida
- Seed con usuarios por debajo del umbral (0 y 24 swipes) y un match sin aceptar entre usuarios QA, para no depender del Worker.
- `ASPNETCORE_ENVIRONMENT=Production` en una segunda pasada, para medir lo que de verdad ve un usuario en los 500.
- Pantallas de Venue, Playlist y registro no existen en el front; cuando existan, repetir UI-2 y UI-3 sobre ellas.
- Limpieza pendiente en la BD local (a confirmar con el usuario): usuarios `qa*@example.com`, cuentas `google_*@google.com`, mensajes con prefijo `QA*` en chats y salas seed, 3 mensajes de bienvenida duplicados en los matches `…0001` y `…0003`, y matches creados por el Worker con usuarios QA. Lo más simple es recrear el volumen local.
- Archivos temporales en `tmp/` (incluyen JWT de usuarios QA locales, ya vencidos): borrar con confirmación.

## Anexo — QA en Android nativo (emulador Android 15, Expo Go SDK 54)

**Cobertura:** 1 Arranque, 2 Login email, 3 Onboarding sin Spotify, 4 Discover, 5 Radar y RadarMatches, 6 Conexiones y Chat, 7 Perfil, Ajustes, Cerrar sesion, 8 Safe areas y tab bar, 9 font_scale 1.3, 10 Accesibilidad (uiautomator), 11 BACK de Android, 12 Persistencia de sesion, modo oscuro/claro del sistema

### Diferencias respecto a web

| Hallazgo web | En nativo | Nota |
|---|---|---|
| ui1-12 Cerrar sesion no funciona en web (Alert no-op) | distinto | Funciona: Alert nativo y vuelta al login. |
| ui1-1 Onboarding sin Spotify es callejon (Alert no-op) | igual | Sigue sin salida, pero ahora la alerta 'Proximamente' si se ve. |
| ui1-2 Discover Preparando tracks infinito | igual | Spinner pegado arriba, sin timeout. |
| ui1-3 Login sin validacion/estado de carga/botones sin rol | distinto | Hay validacion de vacio y mensaje de credenciales; el boton Entrar expone content-desc 'Entrar'. Nuevo en nativo: el teclado tapa el formulario. |
| ui1-4 Radar cifras hardcodeadas | igual | 247 y 100 swipes con 31 swipes. |
| ui1-5 Afinidad 100% y Pasar/Conectar placebo | igual | Pasar no es clickable; Conectar solo cambia texto. |
| ui1-6 Conexiones: nombres Match XXXX duplicados, aceptar al abrir | igual | Match 2222 repetido x4; abrir un chat agrega otro '¡Match creado!'. |
| ui1-7 Enviar mensaje falla en silencio | igual | Ademas el input se vacia. |
| ui1-8 Perfil con spinners infinitos | igual | Mas: Ajustes vacio y 'Camilo Demo' conectado. |
| ui1-9 Settings placebo (localStorage) | no aplica | No se re-probo; no hay localStorage en nativo, no se verifico donde persiste. |
| ui1-10 Tab bar flotante tapa contenido | igual | Translucida sobre cartas; padding inferior insuficiente. |
| ui1-11 Desktop sin max-width | no aplica |  |
| ui1-13 Accesibilidad: sin rol y foco por Tab | distinto | Foco por Tab no aplica; botones de icono sin content-desc y objetivos < 48dp. |
| ui2-1 Radar invita con <25 swipes y luego bloquea | igual | Copy de 100 swipes con 31 swipes. |
| ui2-2 Chat sin limite ni contador de 1000 | no probado | No se escribieron textos largos. |

### Hallazgos en nativo

- **[importante] nativo-1 — Icono, splash y adaptive icon son los de la plantilla de Expo** · Arranque / branding. Obtenido: Pantalla de carga con el icono azul de plantilla Expo (chevron sobre cuadricula) y el nombre Encorely; el menu dev tambien muestra ese icono. Los archivos assets/icon.png, android-icon-foreground.png y splash-icon.png (circulos sobre cuadricula) son los de la plantilla.. Evidencia: capturas/nativo-start3.png, capturas/nativo-persist-montage.png: icono plantilla Expo. Se leyeron los PNG de assets/.. Causa: encorely-front/assets/icon.png, splash-icon.png, android-icon-*.png; app.json (icon, splash, android.adaptiveIcon). Fix: Reemplazar los 6 assets por la marca Encorely (icono 1024, foreground adaptive con safe zone, splash-icon) antes de cualquier build.
- **[importante] nativo-2 — Android: el teclado tapa email, contrasena y boton Entrar (KeyboardAvoidingView sin comportamiento en Android)** · Login / teclado. Obtenido: El campo activo (Email) queda oculto bajo el teclado; la contrasena y Entrar tambien. Tocar donde estaba la contrasena toca teclas: en la prueba se escribio una 'g' en el email y la contrasena acabo concatenada al email ('...example.comgEncorely123!'), dando 'Ingresa tu email y contrasena'. Solo funciona usando la tecla Siguiente del teclado o cerrandolo con BACK.. Evidencia: capturas/nativo-18.png (teclado sobre campos), capturas/nativo-51.png (contrasena concatenada al email). Causa: src/modules/auth/screens/LoginScreen.tsx:112 behavior={Platform.OS === 'ios' ? 'padding' : undefined}. En Expo Go con edge-to-edge/SDK 54 no hay adjustResize efectivo.. Fix: Usar behavior='padding' tambien en Android (o 'height') o react-native-keyboard-controller; mantener ScrollView con keyboardShouldPersistTaps y scrollToFocused.
- **[importante] nativo-3 — Registro con correo y contrasena es un callejon: siempre muestra la alerta 'Proximamente' (en nativo la alerta si se ve)** · Registro correo (onboarding paso 3). Obtenido: Alerta nativa 'Proximamente: Estamos preparando el registro con correo...' incluso con campos vacios o validos. Texto al pie: 'El acceso con correo y Google se activara en una proxima version' contradice la pantalla de login que si acepta correo. Los campos aceptan texto como si fueran funcionales.. Evidencia: capturas/nativo-5.png, capturas/nativo-11.png (Alert nativo visible). Causa: src/modules/onboarding/screens/OnboardingStep3Screen.tsx:105-119 showComingSoonEmail/showComingSoonGoogle. Fix: Ocultar el formulario hasta que exista el registro (o conectarlo a POST /auth/register); dejar solo Spotify y enlace 'Ya tengo cuenta'.
- **[importante] nativo-4 — Discover queda en 'Preparando tracks...' infinito tambien en nativo (usuarios con correo)** · Discover. Obtenido: Spinner + 'Preparando tracks...' sin timeout, sin error y sin boton; spinner pegado al notch (y~230) y no centrado. No se pudieron probar gestos de swipe vs scroll.. Evidencia: capturas/nativo-21.png, capturas/nativo-52.png. Causa: modules/swipe (carga de tracks depende de Spotify). Fix: Estado vacio con CTA 'Conecta Spotify', timeout y reintento; centrar el loader.
- **[importante] nativo-5 — Radar dice 'Alcanzaste el umbral de los 100 swipes' a un usuario con 31 swipes y muestra '247 personas' frente a '8 personas' en la lista** · Radar. Obtenido: Felicitacion de 100 swipes con 31 swipes, '247 personas con tu vibe cerca' hardcodeado; al abrir la lista dice '8 personas cerca de ti'; todos '100% compatible' y 'Match Certificado'.. Evidencia: capturas/nativo-22.png, capturas/nativo-24.png. Causa: src/modules/radar/screens/RadarScreen.tsx:147-152 (texto fijo y 247 literal). Fix: Leer swipeCount y total real del API; gate en el boton, no en el copy.
- **[importante] nativo-6 — Pasar no hace nada y Conectar solo cambia el texto local a 'Solicitud enviada'** · RadarMatches. Obtenido: Pasar sin efecto (en uiautomator no es clickable); Conectar cambia a 'Solicitud enviada check' solo en estado local.. Evidencia: capturas/nativo-25.png, capturas/nativo-26.png. Causa: src/modules/radar/screens/RadarMatchesScreen.tsx:~105 (Pasar sin onPress), handleConnect:109. Fix: Implementar descarte y POST de solicitud; revertir el estado si falla.
- **[importante] nativo-7 — Mensaje enviado se borra del input y no aparece en el chat (fallo silencioso tambien en nativo)** · Chat. Obtenido: El input se vacia, no hay burbuja ni mensaje de error; logcat sin errores. Ademas cada apertura de un match agrega otra burbuja '¡Match creado!' (chat 3d83 mostro 3 con horas distintas).. Evidencia: capturas/nativo-31.png, capturas/nativo-32.png, capturas/nativo-g4.png. Causa: src/modules/chat/screens/ChatScreen.tsx (envio optimista sin manejo de error). Fix: Mantener el texto en el input hasta 200, mostrar burbuja 'no enviado' con reintento; no insertar mensaje de sistema en cada apertura.
- **[importante] nativo-8 — Perfil con spinners infinitos, nombre generico 'Encorely User' y Ajustes con el formulario vacio, pero 'Conectado como Camilo Demo' (cuenta Spotify que no existe)** · Perfil / Ajustes. Obtenido: 'Calculando tu ADN musical...' y 'Reproducciones recientes' con spinner eternos; 'Encorely User'; en Ajustes Nombre, Edad y Genero vacios aunque el usuario es Camilo Demo; seccion 'Cuenta Spotify - Conectado como Camilo Demo - Actualizar vibe' sin Spotify.. Evidencia: capturas/nativo-34.png, capturas/nativo-35.png, capturas/nativo-36.png. Causa: modules/profile, src/modules/settings/screens/SettingsScreen.tsx:558. Fix: Estados vacio/error con timeout; precargar el perfil en Ajustes; mostrar 'Conectar Spotify' si no hay token.
- **[menor] nativo-9 — La barra de tabs superpone el contenido y el ultimo elemento queda bajo ella (texto translucido encima de las cartas)** · Navegacion / safe areas. Obtenido: Texto de las cartas se lee a traves de la barra translucida; en Radar 'Volver al Inicio' queda totalmente detras al cargar (y=2229 con la barra desde y=2180) y solo es alcanzable tras scroll; en Ajustes 'Cerrar sesion' queda pegado a la barra. La barra ocupa todo el ancho y el borde toca ambos lados (el estilo declara left/right 6%). Sombras rectangulares visibles tras las tarjetas de 'Proximos pasos' (artefacto de elevation en Android).. Evidencia: capturas/nativo-22.png, capturas/nativo-28.png, capturas/nativo-36.png. Causa: src/app/(main)/_layout.tsx:112-125 tabBar absolute; faltan paddingBottom en ScrollViews. Fix: contentContainerStyle paddingBottom >= 64 + 20 + insets.bottom + 16; fondo opaco en la barra.
- **[importante] nativo-10 — Botones de icono sin etiqueta y objetivos tactiles menores a 48dp** · Accesibilidad. Obtenido: Sin content-desc: volver del chat (63x65 px = 24dp), volver de Personas Compatibles (74x77 = 28dp), engranaje de Perfil (99x99 = 38dp), volver de Ajustes (100px = 38dp), boton enviar del chat (105px = 40dp), y los 2 Switch de Privacidad (sin nombre accesible; 126px = 48dp ok). Volver del onboarding si tiene 'Volver' pero mide 105px = 40dp. Enlaces de 17-19dp de alto: 'Volver al Inicio' (45px), 'Ver mas' (51px), 'Completa tu perfil' (51px). Positivo: tabs, Entrar, Continuar con correo y Pasar/Conectar si exponen descripcion; las tabs miden 270x168 px.. Evidencia: uiautomator dumps (tmp/nativo/ui.xml) y capturas capturas/nativo-24.png, capturas/nativo-30.png, capturas/nativo-34.png. Causa: ChatScreen.tsx, RadarMatchesScreen.tsx:127, ProfileScreen, SettingsScreen.tsx (TouchableOpacity y Switch sin accessibilityLabel; solo hay 4 accessibilityLabel en todo modules/). Fix: accessibilityLabel/accessibilityRole en cada Touchable de icono y Switch; hitSlop o minWidth/minHeight 48.
- **[menor] nativo-11 — Con fuente 1.3 se truncan textos y se parte una palabra a mitad** · Fuente grande. Obtenido: Conexiones: '¡Nuevo Match! Toca para salud..' truncado (numberOfLines=1); Ajustes: 'Cone/ctado/como' partido en 3 lineas y 'Camilo Dem..' truncado junto a Actualizar vibe; 'Cerrar sesion' queda medio bajo la barra de tabs; pastilla '247 personas con tu vibe cerca' envuelve. Sin textos fuera de pantalla ni botones inutilizables. Cambiar la fuente reinicia la app con pantalla blanca.. Evidencia: capturas/nativo-font13-montage.png, capturas/nativo-font13-montage2.png, capturas/nativo-40.png. Causa: modules/matches/screens/*.tsx:76 numberOfLines={1}; SettingsScreen.tsx:558-559. Fix: Usar flexShrink y numberOfLines=2, layout vertical en la tarjeta de Spotify; revisar allowFontScaling/maxFontSizeMultiplier en titulos.
- **[menor] nativo-12 — Destello blanco al abrir la app (que es solo oscura) y arranque lento** · Arranque. Obtenido: Secuencia: launcher -> pantalla blanca con icono de plantilla (~5-9 s) -> frame gris oscuro -> frame blanco -> Discover oscuro; ~11-13 s hasta interactiva en dev con Metro (no representa produccion). Cambiar font_scale tambien muestra pantalla blanca prolongada.. Evidencia: capturas/nativo-persist-montage.png, capturas/nativo-persist-montage2.png. Causa: app.json splash.backgroundColor #0A0A0F no se refleja en Expo Go; src/app/_layout.tsx (sin SplashScreen.preventAutoHideAsync). Fix: Retener splash hasta tener fuentes y sesion cargadas; verificar en build de desarrollo, no en Expo Go.

### Ok en nativo

- Cerrar sesion funciona en nativo
- Persistencia de sesion
- Mensajes de error del login
- Teclado en el chat
- Boton BACK de Android
- Alert.alert visible en nativo
- Modo oscuro/claro del sistema
- Tab bar y safe areas superior/inferior

### No probado en nativo

- Gestos de swipe vs scroll en Discover: Discover nunca carga tarjetas con usuarios de correo (Preparando tracks infinito)
- Conectar con Spotify / OAuth nativo: Regla: sale a accounts.spotify.com
- Splash e icono finales del build y notch/cutout reales: Expo Go muestra su propio splash; el Pixel 6 emulado no tiene notch visible
- Tiempo de arranque en produccion: Solo se midio dev con Metro (~11-13 s)
- Toggles de Ajustes y Guardar cambios: Ya conocidos como placebo en web; no se cambio configuracion persistente
- Teclado en Ajustes: No se abrieron campos de Ajustes con teclado
