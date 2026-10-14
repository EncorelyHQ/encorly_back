using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Database;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Encorely.Tests.Features;

public sealed class EventsTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Ciudad única por test: la lista queda bajo control aunque la BD sea compartida.
    private static string UniqueCity(string stem = "Cúcuta") => $"{stem}{Guid.NewGuid():N}";

    private Task<Guid> AddEventAsync(string city, TimeSpan fromNow, bool published = true, string? name = null)
        => api.WithDbAsync(async db =>
        {
            var ev = new Event
            {
                Name = name ?? $"Evento {Guid.NewGuid():N}",
                Venue = "Teatro de prueba",
                City = city,
                StartsAt = DateTimeOffset.UtcNow + fromNow,
                IsPublished = published,
                ArtistLineup = ["Artista Uno", "Artista Dos"],
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            return ev.Id;
        });

    private Task<Guid> FutureEventAsync(string? city = null) => AddEventAsync(city ?? UniqueCity(), TimeSpan.FromDays(10));

    private Task AttendDirectAsync(Guid userId, Guid eventId) => api.WithDbAsync(async db =>
    {
        db.EventAttendances.Add(new EventAttendance { UserId = userId, EventId = eventId });
        await db.SaveChangesAsync();
    });

    private Task<EventDto> AttendAsync(TestUser user, Guid eventId)
        => user.Client.PutAsync($"/api/v1/events/{eventId}/attendance", null, Ct).Expect<EventDto>(HttpStatusCode.OK);

    /// <summary>Swipes (para desbloquear el radar) y scores de géneros con control exacto.</summary>
    private Task PrepareDnaAsync(TestUser user, int swipes, params (string Slug, int Likes, int Dislikes, double Imported)[] scores)
        => api.WithDbAsync(async db =>
        {
            var genres = await db.Genres.ToDictionaryAsync(g => g.Slug, g => g.Id);
            var genreId = genres["reggaeton"];
            for (var i = 0; i < swipes; i++)
            {
                var song = new Song { Title = $"Canción {Guid.NewGuid():N}", Artist = "Artista", GenreId = genreId, CoverUrl = "https://example.test/c.jpg" };
                db.Songs.Add(song);
                db.Swipes.Add(new Swipe { UserId = user.Id, SongId = song.Id, Direction = SwipeDirection.Like });
            }
            foreach (var s in scores)
                db.UserGenreScores.Add(new UserGenreScore { UserId = user.Id, GenreId = genres[s.Slug], Likes = s.Likes, Dislikes = s.Dislikes, ImportedWeight = s.Imported });
            await db.SaveChangesAsync();
        });

    private static Task<HttpResponseMessage> GetRadar(TestUser user, Guid eventId) => user.Client.GetAsync($"/api/v1/events/{eventId}/radar", Ct);

    // ---------- Eventos ----------

    [Fact]
    public async Task Lista_solo_futuros_publicados_y_por_fecha()
    {
        var city = UniqueCity();
        var late = await AddEventAsync(city, TimeSpan.FromDays(30));
        var soon = await AddEventAsync(city, TimeSpan.FromDays(2));
        await AddEventAsync(city, TimeSpan.FromDays(-3));
        await AddEventAsync(city, TimeSpan.FromDays(5), published: false);
        var user = await api.CreateUserAsync();

        var list = await user.Client.GetAsync($"/api/v1/events?city={Uri.EscapeDataString(city)}", Ct).Expect<List<EventDto>>(HttpStatusCode.OK);

        Assert.Equal([soon, late], list.Select(e => e.Id));
        Assert.All(list, e => Assert.True(e.StartsAt > DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Filtro_de_ciudad_tolera_acentos_y_mayusculas()
    {
        var city = UniqueCity("Cúcuta");
        var id = await AddEventAsync(city, TimeSpan.FromDays(4));
        var user = await api.CreateUserAsync();

        var plain = city.ToLowerInvariant().Replace("ú", "u");
        var list = await user.Client.GetAsync($"/api/v1/events?city={plain}", Ct).Expect<List<EventDto>>(HttpStatusCode.OK);
        Assert.Equal([id], list.Select(e => e.Id));

        var unknown = await user.Client.GetAsync("/api/v1/events?city=Atlantida-no-existe", Ct).Expect<List<EventDto>>(HttpStatusCode.OK);
        Assert.Empty(unknown);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-5")]
    [InlineData("abc")]
    public async Task Limit_invalido_da_400(string limit)
    {
        var user = await api.CreateUserAsync();
        await user.Client.GetAsync($"/api/v1/events?limit={limit}", Ct).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Limit_valido_recorta_la_lista()
    {
        var city = UniqueCity();
        await AddEventAsync(city, TimeSpan.FromDays(2));
        await AddEventAsync(city, TimeSpan.FromDays(3));
        var user = await api.CreateUserAsync();

        var list = await user.Client.GetAsync($"/api/v1/events?city={city}&limit=1", Ct).Expect<List<EventDto>>(HttpStatusCode.OK);
        Assert.Single(list);
    }

    [Fact]
    public async Task Lista_sin_token_da_401()
        => await api.Anonymous().GetAsync("/api/v1/events", Ct).ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");

    [Fact]
    public async Task Detalle_existente_y_pasado_se_pueden_ver()
    {
        var user = await api.CreateUserAsync();
        var past = await AddEventAsync(UniqueCity(), TimeSpan.FromDays(-2));
        var dto = await user.Client.GetAsync($"/api/v1/events/{past}", Ct).Expect<EventDto>(HttpStatusCode.OK);
        Assert.Equal(past, dto.Id);
        Assert.False(dto.IAmGoing);
        Assert.Equal(0, dto.AttendeeCount);
    }

    [Fact]
    public async Task Detalle_inexistente_o_no_publicado_da_404()
    {
        var user = await api.CreateUserAsync();
        await user.Client.GetAsync($"/api/v1/events/{Guid.NewGuid()}", Ct).ExpectProblem(HttpStatusCode.NotFound, "event_not_found");

        var hidden = await AddEventAsync(UniqueCity(), TimeSpan.FromDays(3), published: false);
        await user.Client.GetAsync($"/api/v1/events/{hidden}", Ct).ExpectProblem(HttpStatusCode.NotFound, "event_not_found");
        await user.Client.PutAsync($"/api/v1/events/{hidden}/attendance", null, Ct).ExpectProblem(HttpStatusCode.NotFound, "event_not_found");
        await user.Client.DeleteAsync($"/api/v1/events/{hidden}/attendance", Ct).ExpectProblem(HttpStatusCode.NotFound, "event_not_found");
    }

    [Fact]
    public async Task Id_que_no_es_guid_da_404_nunca_500()
    {
        var user = await api.CreateUserAsync();
        var r = await user.Client.GetAsync("/api/v1/events/no-es-un-guid", Ct);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Attend_es_idempotente_y_el_conteo_sube_una_sola_vez()
    {
        var id = await FutureEventAsync();
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();

        var first = await AttendAsync(a, id);
        var second = await AttendAsync(a, id);
        Assert.Equal(1, first.AttendeeCount);
        Assert.Equal(1, second.AttendeeCount);
        Assert.True(second.IAmGoing);

        var viaB = await AttendAsync(b, id);
        Assert.Equal(2, viaB.AttendeeCount);

        var detailForA = await a.Client.GetAsync($"/api/v1/events/{id}", Ct).Expect<EventDto>(HttpStatusCode.OK);
        Assert.Equal(2, detailForA.AttendeeCount);
        Assert.True(detailForA.IAmGoing);
    }

    [Fact]
    public async Task Attend_en_evento_pasado_da_400_event_past()
    {
        var past = await AddEventAsync(UniqueCity(), TimeSpan.FromMinutes(-5));
        var user = await api.CreateUserAsync();
        var body = await user.Client.PutAsync($"/api/v1/events/{past}/attendance", null, Ct).ExpectProblem(HttpStatusCode.BadRequest, "event_past");
        Assert.Equal("El evento ya pasó.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unattend_quita_la_asistencia_y_es_idempotente()
    {
        var id = await FutureEventAsync();
        var user = await api.CreateUserAsync();
        await AttendAsync(user, id);

        var once = await user.Client.DeleteAsync($"/api/v1/events/{id}/attendance", Ct).Expect<EventDto>(HttpStatusCode.OK);
        var twice = await user.Client.DeleteAsync($"/api/v1/events/{id}/attendance", Ct).Expect<EventDto>(HttpStatusCode.OK);
        Assert.False(once.IAmGoing);
        Assert.Equal(0, twice.AttendeeCount);
    }

    [Fact]
    public async Task Unattend_de_B_no_toca_la_asistencia_de_A()
    {
        var id = await FutureEventAsync();
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await AttendAsync(a, id);

        var dto = await b.Client.DeleteAsync($"/api/v1/events/{id}/attendance", Ct).Expect<EventDto>(HttpStatusCode.OK);
        Assert.Equal(1, dto.AttendeeCount);
        Assert.False(dto.IAmGoing);
    }

    [Fact]
    public async Task MisEventos_lista_solo_futuros_propios_por_fecha()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var late = await AddEventAsync(UniqueCity(), TimeSpan.FromDays(20));
        var soon = await AddEventAsync(UniqueCity(), TimeSpan.FromDays(3));
        var past = await AddEventAsync(UniqueCity(), TimeSpan.FromDays(-3));
        var others = await FutureEventAsync();
        await AttendDirectAsync(a.Id, late);
        await AttendDirectAsync(a.Id, soon);
        await AttendDirectAsync(a.Id, past);
        await AttendDirectAsync(b.Id, others);

        var mine = await a.Client.GetAsync("/api/v1/me/events", Ct).Expect<List<EventDto>>(HttpStatusCode.OK);
        Assert.Equal([soon, late], mine.Select(e => e.Id));
        Assert.All(mine, e => Assert.True(e.IAmGoing));

        var theirs = await b.Client.GetAsync("/api/v1/me/events", Ct).Expect<List<EventDto>>(HttpStatusCode.OK);
        Assert.Equal([others], theirs.Select(e => e.Id));
    }

    // ---------- Radar ----------

    [Fact]
    public async Task Radar_de_evento_inexistente_da_404()
    {
        var user = await api.CreateUserAsync();
        await GetRadar(user, Guid.NewGuid()).ExpectProblem(HttpStatusCode.NotFound, "event_not_found");
    }

    [Fact]
    public async Task Radar_sin_ir_al_evento_da_403_not_attending_aunque_otro_si_vaya()
    {
        var id = await FutureEventAsync();
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await PrepareDnaAsync(a, 3);
        await PrepareDnaAsync(b, 3);
        await AttendDirectAsync(a.Id, id);

        var body = await GetRadar(b, id).ExpectProblem(HttpStatusCode.Forbidden, "not_attending");
        Assert.Equal("Marca que vas al evento para ver quién más va.", body.GetProperty("title").GetString());
        await GetRadar(a, id).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Radar_bloqueado_por_umbral_indica_cuantas_canciones_faltan()
    {
        var id = await FutureEventAsync();
        var user = await api.CreateUserAsync();
        await PrepareDnaAsync(user, 1);
        await AttendDirectAsync(user.Id, id);

        var body = await GetRadar(user, id).ExpectProblem(HttpStatusCode.Forbidden, "radar_locked");
        Assert.Equal("Te faltan 2 canciones por calificar.", body.GetProperty("detail").GetString());

        await PrepareDnaAsync(user, 2);
        await GetRadar(user, id).Expect(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Radar_solo_muestra_asistentes_de_ese_evento()
    {
        var id = await FutureEventAsync();
        var other = await FutureEventAsync();
        var me = await api.CreateUserAsync("Yo");
        var here = await api.CreateUserAsync("Aquí");
        var elsewhere = await api.CreateUserAsync("Allá");
        await PrepareDnaAsync(me, 3, ("reggaeton", 3, 0, 0));
        await AttendDirectAsync(me.Id, id);
        await AttendDirectAsync(here.Id, id);
        await AttendDirectAsync(elsewhere.Id, other);

        var radar = await GetRadar(me, id).Expect<List<RadarEntryDto>>(HttpStatusCode.OK);
        Assert.Equal([here.Id], radar.Select(r => r.UserId));
        Assert.DoesNotContain(radar, r => r.UserId == me.Id);
    }

    [Fact]
    public async Task Radar_excluye_bloqueados_en_ambas_direcciones_y_baneados()
    {
        var id = await FutureEventAsync();
        var me = await api.CreateUserAsync("Yo");
        var blockedByMe = await api.CreateUserAsync("Bloqueado por mí");
        var blocksMe = await api.CreateUserAsync("Me bloqueó");
        var banned = await api.CreateUserAsync("Baneado", u => u.Status = UserStatus.Banned);
        var visible = await api.CreateUserAsync("Visible");
        await PrepareDnaAsync(me, 3);
        foreach (var u in new[] { me, blockedByMe, blocksMe, banned, visible }) await AttendDirectAsync(u.Id, id);
        await api.WithDbAsync(async db =>
        {
            db.Blocks.Add(new Block { BlockerId = me.Id, BlockedId = blockedByMe.Id });
            db.Blocks.Add(new Block { BlockerId = blocksMe.Id, BlockedId = me.Id });
            await db.SaveChangesAsync();
        });

        var radar = await GetRadar(me, id).Expect<List<RadarEntryDto>>(HttpStatusCode.OK);
        Assert.Equal([visible.Id], radar.Select(r => r.UserId));
    }

    [Fact]
    public async Task Radar_ordena_por_afinidad_real_con_valores_distintos()
    {
        var id = await FutureEventAsync();
        var me = await api.CreateUserAsync("Yo", null, "reggaeton");
        var same = await api.CreateUserAsync("Igual", null, "reggaeton");
        var similar = await api.CreateUserAsync("Parecido", null, "reggaeton", "salsa");
        var opposite = await api.CreateUserAsync("Opuesto", null, "salsa");
        await PrepareDnaAsync(me, 3, ("reggaeton", 6, 0, 0));
        await PrepareDnaAsync(same, 0, ("reggaeton", 6, 0, 0));
        await PrepareDnaAsync(similar, 0, ("reggaeton", 4, 0, 0), ("salsa", 3, 0, 0));
        await PrepareDnaAsync(opposite, 0, ("salsa", 6, 0, 0));
        foreach (var u in new[] { me, same, similar, opposite }) await AttendDirectAsync(u.Id, id);

        var radar = await GetRadar(me, id).Expect<List<RadarEntryDto>>(HttpStatusCode.OK);

        Assert.Equal([same.Id, similar.Id, opposite.Id], radar.Select(r => r.UserId));
        Assert.Equal(100, radar[0].Affinity);
        Assert.InRange(radar[1].Affinity, 1, 99);
        Assert.Equal(0, radar[2].Affinity);
        Assert.Equal(3, radar.Select(r => r.Affinity).Distinct().Count());
        Assert.Equal(["reggaeton"], radar[0].SharedGenres);
        Assert.Empty(radar[2].SharedGenres);
    }

    [Fact]
    public async Task Radar_afinidad_en_sql_coincide_con_Dna_de_csharp()
    {
        var id = await FutureEventAsync();
        var me = await api.CreateUserAsync("Yo", null, "salsa", "bachata");
        var other = await api.CreateUserAsync("Otro", null, "reggaeton", "vallenato", "salsa");
        await PrepareDnaAsync(me, 3, ("reggaeton", 5, 1, 0), ("salsa", 2, 1, 1.5), ("cumbia", 1, 4, 0));
        await PrepareDnaAsync(other, 0, ("reggaeton", 3, 2, 0.25), ("vallenato", 4, 0, 0), ("hip-hop", 0, 3, 0));
        await AttendDirectAsync(me.Id, id);
        await AttendDirectAsync(other.Id, id);

        var (expectedAffinity, expectedShared) = await api.WithDbAsync(async db =>
        {
            async Task<Dictionary<int, double>> VectorOf(Guid userId) => Dna.Vector(
                await db.UserGenreScores.Where(s => s.UserId == userId).ToListAsync(),
                await db.UserGenres.Where(g => g.UserId == userId).Select(g => g.GenreId).ToListAsync());
            var mine = await VectorOf(me.Id);
            var theirs = await VectorOf(other.Id);
            var slugs = await db.Genres.ToDictionaryAsync(g => g.Id, g => g.Slug);
            return (Dna.Affinity(mine, theirs), Dna.Shared(mine, theirs).Select(g => slugs[g]).ToList());
        });

        var radar = await GetRadar(me, id).Expect<List<RadarEntryDto>>(HttpStatusCode.OK);
        var entry = Assert.Single(radar);
        Assert.Equal(expectedAffinity, entry.Affinity);
        Assert.InRange(entry.Affinity, 1, 99);
        Assert.Equal(expectedShared, entry.SharedGenres);
        Assert.Equal(["reggaeton", "salsa"], entry.SharedGenres.Order());
    }

    [Fact]
    public async Task Radar_respeta_RadarMinAffinity()
    {
        var id = await FutureEventAsync();
        var me = await api.CreateUserAsync("Yo", null, "reggaeton");
        var close = await api.CreateUserAsync("Cercano", null, "reggaeton");
        var far = await api.CreateUserAsync("Lejano", null, "salsa");
        await PrepareDnaAsync(me, 3, ("reggaeton", 6, 0, 0));
        await PrepareDnaAsync(close, 0, ("reggaeton", 6, 0, 0));
        await PrepareDnaAsync(far, 0, ("salsa", 6, 0, 0));
        foreach (var u in new[] { me, close, far }) await AttendDirectAsync(u.Id, id);

        await using var strict = api.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Matching:RadarMinAffinity"] = "50" })));
        var client = strict.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", me.AccessToken);

        var radar = await client.GetAsync($"/api/v1/events/{id}/radar", Ct).Expect<List<RadarEntryDto>>(HttpStatusCode.OK);
        Assert.Equal([close.Id], radar.Select(r => r.UserId));

        var all = await GetRadar(me, id).Expect<List<RadarEntryDto>>(HttpStatusCode.OK);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Radar_marca_likedByMe_y_matched_y_trae_edad_y_ciudad()
    {
        var id = await FutureEventAsync();
        var me = await api.CreateUserAsync("Yo");
        var liked = await api.CreateUserAsync("Gustado", u => { u.City = "Medellín"; u.BirthDate = new DateOnly(1990, 1, 1); });
        var matched = await api.CreateUserAsync("Pareja");
        var closed = await api.CreateUserAsync("Match cerrado");
        var plain = await api.CreateUserAsync("Sin nada");
        await PrepareDnaAsync(me, 3);
        foreach (var u in new[] { me, liked, matched, closed, plain }) await AttendDirectAsync(u.Id, id);
        await api.WithDbAsync(async db =>
        {
            db.UserLikes.Add(new UserLike { FromUserId = me.Id, ToUserId = liked.Id });
            db.UserLikes.Add(new UserLike { FromUserId = plain.Id, ToUserId = me.Id }); // en sentido contrario no cuenta
            db.UserLikes.Add(new UserLike { FromUserId = me.Id, ToUserId = matched.Id });
            var (x, y) = Match.OrderPair(me.Id, matched.Id);
            db.Matches.Add(new Match { UserAId = x, UserBId = y });
            var (p, q) = Match.OrderPair(me.Id, closed.Id);
            db.Matches.Add(new Match { UserAId = p, UserBId = q, Status = MatchStatus.Closed, CloseReason = MatchCloseReason.Unmatched });
            await db.SaveChangesAsync();
        });

        var radar = (await GetRadar(me, id).Expect<List<RadarEntryDto>>(HttpStatusCode.OK)).ToDictionary(r => r.UserId);

        Assert.True(radar[liked.Id].LikedByMe);
        Assert.False(radar[liked.Id].Matched);
        Assert.True(radar[matched.Id].LikedByMe);
        Assert.True(radar[matched.Id].Matched);
        Assert.False(radar[closed.Id].Matched);
        Assert.False(radar[plain.Id].LikedByMe);
        Assert.Equal("Medellín", radar[liked.Id].City);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        Assert.Equal(Encorely.Api.Features.Users.User.AgeOn(new DateOnly(1990, 1, 1), today), radar[liked.Id].Age);
    }

    // ---------- Seeds ----------

    private static JsonElement ReadSeed(string file)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream($"Encorely.Api.Data.Seed.{file}")
            ?? throw new InvalidOperationException($"Falta el recurso {file}");
        return JsonSerializer.Deserialize<JsonElement>(stream);
    }

    [Fact]
    public void Seed_de_eventos_tiene_al_menos_20_futuros_bien_formados()
    {
        var events = ReadSeed("events.json").EnumerateArray().ToList();
        Assert.True(events.Count >= 20);
        Assert.Equal(events.Count, events.Select(e => e.GetProperty("name").GetString()).Distinct().Count());
        foreach (var e in events)
        {
            Assert.InRange(e.GetProperty("daysFromNow").GetInt32(), 2, 180);
            Assert.Contains(e.GetProperty("city").GetString(), new[] { "Bogotá", "Medellín" });
            Assert.NotEmpty(e.GetProperty("artistLineup").EnumerateArray());
            Assert.True(TimeOnly.TryParse(e.GetProperty("localTime").GetString(), out _));
            Assert.Contains("demostración", e.GetProperty("description").GetString());
        }
    }

    [Fact]
    public void Seed_de_usuarios_demo_es_valido_y_referencia_datos_existentes()
    {
        var genres = ReadSeed("genres.json").EnumerateArray().Select(g => g.GetProperty("slug").GetString()!).ToHashSet();
        var events = ReadSeed("events.json").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToHashSet();
        var users = ReadSeed("demo-users.json").EnumerateArray().ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Equal(16, users.Count);
        Assert.Equal(16, users.Select(u => u.GetProperty("email").GetString()).Distinct().Count());
        Assert.Contains(users, u => u.GetProperty("email").GetString() == "admin@encorely.demo" && u.GetProperty("admin").GetBoolean());
        Assert.Contains(users, u => u.GetProperty("email").GetString() == "revisor@encorely.demo");
        foreach (var u in users)
        {
            Assert.EndsWith("@encorely.demo", u.GetProperty("email").GetString());
            Assert.InRange(Encorely.Api.Features.Users.User.AgeOn(DateOnly.Parse(u.GetProperty("birthDate").GetString()!), today), 19, 35);
            Assert.Contains(u.GetProperty("city").GetString(), new[] { "Bogotá", "Medellín" });
            Assert.NotEmpty(u.GetProperty("genres").EnumerateArray());
            Assert.All(u.GetProperty("genres").EnumerateArray(), g => Assert.Contains(g.GetString()!, genres));
            Assert.All(u.GetProperty("likes").EnumerateObject(), l => Assert.Contains(l.Name, genres));
            Assert.All(u.GetProperty("attends").EnumerateArray(), a => Assert.Contains(a.GetString()!, events));
            Assert.False(u.TryGetProperty("password", out _));
        }
    }
}
