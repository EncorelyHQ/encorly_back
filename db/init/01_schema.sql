-- Esquema reconstruido a partir del SQL de Dapper del backend (el original vivia en Supabase).
-- Postgres 16, DB encorely_db. Tablas/columnas PascalCase entre comillas.
-- Convenciones:
--   * Guid -> uuid, DateTime -> timestamptz (el codigo escribe DateTime.UtcNow, Kind=Utc; Npgsql 8 exige timestamptz).
--   * Enums C# -> integer (Dapper los envia/lee como Int32; ver UsuarioQueries.GetByMoodExceptAsync: (int)mood).
--   * Columnas sin propiedad en el modelo C# son inofensivas: Dapper ignora columnas extra en SELECT *.

BEGIN;

-- Fuente: EncorelyModels/Usuario.cs, EncorelyRepository/Implements/UsuarioRepository.cs:19-47
CREATE TABLE IF NOT EXISTS "Users" (
    "Id"                     uuid PRIMARY KEY,
    "SpotifyId"              text        NOT NULL,              -- para Custom el codigo guarda el email aqui
    "DisplayName"            text        NOT NULL,
    "Email"                  text        NOT NULL,
    "Provider"               integer     NOT NULL DEFAULT 2,    -- AuthProvider: 0 Spotify, 1 Google, 2 Custom
    "PasswordHash"           text        NULL,                  -- BCrypt.Net-Next (UsuarioService/IdentityService)
    "SwipeCount"             integer     NOT NULL DEFAULT 0,    -- umbral 25 (ChatController, EventsController, Worker)
    "Mood"                   integer     NOT NULL DEFAULT 1,    -- ConcertMood: 0 Moshpit, 1 Chill, 2 VIP
    "CreatedAt"              timestamptz NOT NULL DEFAULT now(),
    "RefreshToken"           text        NULL,
    "RefreshTokenExpiryTime" timestamptz NULL,
    -- Extras de demo: NO los usa el backend actual (el front genera avatares con pravatar por seed).
    "ProfileImageUrl"        text        NULL,
    "City"                   text        NULL,
    "TopGenres"              text[]      NULL,
    "TopArtists"             text[]      NULL,
    CONSTRAINT "CK_Users_Provider" CHECK ("Provider" IN (0, 1, 2)),
    CONSTRAINT "CK_Users_Mood"     CHECK ("Mood" IN (0, 1, 2)),
    CONSTRAINT "UQ_Users_Email"     UNIQUE ("Email"),
    CONSTRAINT "UQ_Users_SpotifyId" UNIQUE ("SpotifyId")
);
CREATE INDEX IF NOT EXISTS "IX_Users_Mood" ON "Users" ("Mood");

-- Fuente: EncorelyModels/MusicalProfile.cs, MusicalProfileRepository.cs:19-23 (ON CONFLICT ("UserId") -> UNIQUE)
CREATE TABLE IF NOT EXISTS "MusicalProfiles" (
    "Id"           uuid PRIMARY KEY,
    "UserId"       uuid             NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
    "Energy"       double precision NOT NULL DEFAULT 0.5,
    "Danceability" double precision NOT NULL DEFAULT 0.5,
    "Valence"      double precision NOT NULL DEFAULT 0.5,
    "Tempo"        double precision NOT NULL DEFAULT 120,
    CONSTRAINT "UQ_MusicalProfiles_UserId" UNIQUE ("UserId")
);

-- Fuente: EncorelyModels/Swipe.cs, SwipeRepository.cs:19-21, SwipeQueries.cs
CREATE TABLE IF NOT EXISTS "Swipes" (
    "Id"        uuid PRIMARY KEY,
    "UserId"    uuid        NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
    "TrackId"   text        NOT NULL,
    "Direction" integer     NOT NULL,                            -- SwipeDirection: 0 Left, 1 Right, 2 Down
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "CK_Swipes_Direction" CHECK ("Direction" IN (0, 1, 2))
);
CREATE INDEX IF NOT EXISTS "IX_Swipes_UserId"         ON "Swipes" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Swipes_UserId_TrackId" ON "Swipes" ("UserId", "TrackId");

-- Fuente: EncorelyModels/Match.cs, MatchRepository.cs:19-21, MatchQueries.cs
-- No existe columna de estado: "aceptado" = existe mensaje de bienvenida (SenderId = Guid.Empty) en Messages.
CREATE TABLE IF NOT EXISTS "Matches" (
    "Id"             uuid PRIMARY KEY,
    "UserId1"        uuid             NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
    "UserId2"        uuid             NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
    "AffinityScore"  double precision NOT NULL DEFAULT 0,        -- coseno 0..1 (CompatibilityService)
    "IsHighPriority" boolean          NOT NULL DEFAULT false,
    "CreatedAt"      timestamptz      NOT NULL DEFAULT now(),
    CONSTRAINT "CK_Matches_DistinctUsers" CHECK ("UserId1" <> "UserId2")
);
CREATE INDEX IF NOT EXISTS "IX_Matches_UserId1" ON "Matches" ("UserId1");
CREATE INDEX IF NOT EXISTS "IX_Matches_UserId2" ON "Matches" ("UserId2");
-- Un solo match por pareja sin importar el orden (el Worker deduplica en codigo).
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Matches_Pair"
    ON "Matches" ((LEAST("UserId1", "UserId2")), (GREATEST("UserId1", "UserId2")));

-- Fuente: MessageRepository.cs:19-21, MessageQueries.cs:19,27 (columna real "CreatedAt" -> propiedad Timestamp)
-- SenderId sin FK: el mensaje de bienvenida usa Guid.Empty (MatchService.AcceptMatchAsync).
CREATE TABLE IF NOT EXISTS "Messages" (
    "Id"        uuid PRIMARY KEY,
    "MatchId"   uuid        NOT NULL REFERENCES "Matches" ("Id") ON DELETE CASCADE,
    "SenderId"  uuid        NOT NULL,
    "Content"   text        NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_Messages_MatchId_CreatedAt" ON "Messages" ("MatchId", "CreatedAt");

-- Fuente: VenueRoomRepository.cs:19-21 (Id, Name, Description, Capacity, IsActive, CreatedAt),
--         VenueRoomQueries.cs:35 (WHERE "IsActive" = true), VenueRoom.cs (EventId, ExpiresAt, via SELECT *).
-- EventId/ExpiresAt tienen DEFAULT porque VenueRoomRepository.CreateAsync no los inserta.
CREATE TABLE IF NOT EXISTS "VenueRooms" (
    "Id"          uuid PRIMARY KEY,
    "EventId"     text        NOT NULL DEFAULT '',
    "Name"        text        NOT NULL,
    "Description" text        NULL,
    "Capacity"    integer     NOT NULL DEFAULT 0,
    "IsActive"    boolean     NOT NULL DEFAULT true,
    "ExpiresAt"   timestamptz NOT NULL DEFAULT (now() + interval '4 hours'),
    "CreatedAt"   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_VenueRooms_EventId"  ON "VenueRooms" ("EventId");
CREATE INDEX IF NOT EXISTS "IX_VenueRooms_IsActive" ON "VenueRooms" ("IsActive");

-- Fuente: VenueMessageRepository.cs:19-21,34-35, VenueMessageQueries.cs:19,27
CREATE TABLE IF NOT EXISTS "VenueMessages" (
    "Id"          uuid PRIMARY KEY,
    "RoomId"      uuid        NOT NULL REFERENCES "VenueRooms" ("Id") ON DELETE CASCADE,
    "SenderId"    uuid        NOT NULL,
    "Content"     text        NOT NULL,
    "IsModerated" boolean     NOT NULL DEFAULT false,
    "CreatedAt"   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_VenueMessages_RoomId_CreatedAt" ON "VenueMessages" ("RoomId", "CreatedAt");

COMMIT;
