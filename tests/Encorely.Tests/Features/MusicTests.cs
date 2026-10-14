using System.Net;
using Encorely.Api.Features.Music;
using Encorely.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Encorely.Tests.Features;

public sealed class MusicTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Canciones propias del test, con título único, para no depender del catálogo sembrado.</summary>
    private async Task<List<Guid>> CreateSongsAsync(string genreSlug, int count, bool active = true)
    {
        var ids = new List<Guid>();
        await api.WithDbAsync(async db =>
        {
            var genreId = await db.Genres.Where(g => g.Slug == genreSlug).Select(g => g.Id).SingleAsync(Ct);
            for (var i = 0; i < count; i++)
            {
                var song = new Song { Title = $"Test {Guid.NewGuid():N}", Artist = "Artista de prueba", GenreId = genreId, CoverUrl = "https://example.com/c.jpg", IsActive = active };
                db.Songs.Add(song);
                ids.Add(song.Id);
            }
            await db.SaveChangesAsync(Ct);
        });
        return ids;
    }

    private static Task<HttpResponseMessage> Swipe(TestUser u, Guid songId, string direction)
        => u.Client.PostJson("/api/v1/swipes", new { songId, direction });

    private static async Task<DnaResponse> Dna(TestUser u)
        => await u.Client.GetAsync("/api/v1/me/dna", Ct).Expect<DnaResponse>(HttpStatusCode.OK);

    private static double Weight(DnaResponse dna, string slug) => dna.Genres.FirstOrDefault(g => g.Genre == slug)?.Weight ?? 0;

    [Fact]
    public async Task Genres_es_publico_y_viene_ordenado_por_nombre()
    {
        var genres = await api.Anonymous().GetAsync("/api/v1/genres", Ct).Expect<List<GenreDto>>(HttpStatusCode.OK);
        Assert.True(genres.Count >= 15);
        Assert.Equal(genres.OrderBy(g => g.Name, StringComparer.Ordinal).Select(g => g.Id), genres.Select(g => g.Id));
    }

    [Fact]
    public async Task Seed_carga_al_menos_70_canciones_con_preview_y_3_por_genero()
    {
        var (withPreview, perGenre, genreCount) = await api.WithDbAsync(async db => (
            await db.Songs.CountAsync(s => s.PreviewUrl != null, Ct),
            await db.Songs.Where(s => s.IsActive).GroupBy(s => s.GenreId).Select(g => g.Count()).ToListAsync(Ct),
            await db.Genres.CountAsync(Ct)));

        Assert.True(withPreview >= 70, $"Solo {withPreview} canciones con preview");
        Assert.Equal(genreCount, perGenre.Count);
        Assert.All(perGenre, n => Assert.True(n >= 3));
    }

    [Fact]
    public async Task Feed_requiere_sesion()
        => await api.Anonymous().GetAsync("/api/v1/songs/feed", Ct).ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(100000)]
    public async Task Feed_con_limit_fuera_de_rango_da_400(int limit)
    {
        var user = await api.CreateUserAsync();
        await user.Client.GetAsync($"/api/v1/songs/feed?limit={limit}", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Feed_con_limit_no_numerico_da_400_nunca_500()
    {
        var user = await api.CreateUserAsync();
        await user.Client.GetAsync("/api/v1/songs/feed?limit=abc", Ct).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Feed_respeta_limit_y_solo_trae_canciones_no_calificadas()
    {
        var user = await api.CreateUserAsync();
        var first = await user.Client.GetAsync("/api/v1/songs/feed?limit=10", Ct).Expect<List<SongDto>>(HttpStatusCode.OK);
        Assert.Equal(10, first.Count);
        Assert.Equal(10, first.Select(s => s.Id).Distinct().Count());
        Assert.All(first, s => Assert.False(string.IsNullOrEmpty(s.GenreName)));

        foreach (var s in first) await Swipe(user, s.Id, "like").Expect(HttpStatusCode.OK);

        var next = await user.Client.GetAsync("/api/v1/songs/feed?limit=50", Ct).Expect<List<SongDto>>(HttpStatusCode.OK);
        Assert.NotEmpty(next);
        Assert.Empty(next.Select(s => s.Id).Intersect(first.Select(s => s.Id)));
    }

    [Fact]
    public async Task Feed_vacio_cuando_el_usuario_califico_todo()
    {
        var user = await api.CreateUserAsync();
        var rated = await RateAllActiveSongsAsync(user.Id, except: null);

        var feed = await user.Client.GetAsync("/api/v1/songs/feed?limit=50", Ct).Expect<List<SongDto>>(HttpStatusCode.OK);
        // Otros tests pueden crear canciones nuevas en paralelo; ninguna de las ya calificadas puede reaparecer.
        Assert.Empty(feed.Select(s => s.Id).Intersect(rated));
    }

    [Fact]
    public async Task Like_cambia_el_adn_y_suma_un_swipe()
    {
        var user = await api.CreateUserAsync(null, null, "bachata");
        var songs = await CreateSongsAsync("electronica", 2);
        var before = await Dna(user);
        Assert.Equal(0, before.SwipeCount);
        Assert.Equal(0, Weight(before, "electronica"));

        var res = await Swipe(user, songs[0], "like").Expect<SwipeResponse>(HttpStatusCode.OK);
        Assert.True(res.Counted);
        Assert.Equal(1, res.SwipeCount);

        var after = await Dna(user);
        Assert.Equal(1, after.SwipeCount);
        Assert.True(Weight(after, "electronica") > 0);
        Assert.True(Weight(after, "bachata") < Weight(before, "bachata"));
        Assert.Equal(1.0, after.Genres.Sum(g => g.Weight), 3);
        Assert.Equal(after.Genres.OrderByDescending(g => g.Weight).Select(g => g.Genre), after.Genres.Select(g => g.Genre));
    }

    [Fact]
    public async Task Repetir_la_misma_cancion_no_cuenta_ni_cambia_el_adn()
    {
        var user = await api.CreateUserAsync(null, null, "bachata");
        var song = (await CreateSongsAsync("electronica", 1))[0];

        await Swipe(user, song, "like").Expect(HttpStatusCode.OK);
        var dna = await Dna(user);

        // Incluso con la dirección contraria: la primera calificación manda.
        var again = await Swipe(user, song, "dislike").Expect<SwipeResponse>(HttpStatusCode.OK);
        Assert.False(again.Counted);
        Assert.Equal(1, again.SwipeCount);
        Assert.Equal(dna, await Dna(user), new DnaComparer());
    }

    [Fact]
    public async Task Diez_swipes_concurrentes_de_la_misma_cancion_suman_uno()
    {
        var user = await api.CreateUserAsync(null, null, "bachata");
        var song = (await CreateSongsAsync("electronica", 1))[0];

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Swipe(user, song, "like").Expect<SwipeResponse>(HttpStatusCode.OK)));

        Assert.Equal(1, results.Count(r => r.Counted));
        Assert.All(results, r => Assert.Equal(1, r.SwipeCount));
        var (swipes, likes) = await api.WithDbAsync(async db => (
            await db.Swipes.CountAsync(s => s.UserId == user.Id, Ct),
            await db.UserGenreScores.Where(s => s.UserId == user.Id).SumAsync(s => s.Likes, Ct)));
        Assert.Equal(1, swipes);
        Assert.Equal(1, likes);
    }

    [Fact]
    public async Task Swipes_concurrentes_de_canciones_distintas_no_pierden_actualizaciones()
    {
        var user = await api.CreateUserAsync();
        var songs = await CreateSongsAsync("electronica", 8);

        await Task.WhenAll(songs.Select(id => Swipe(user, id, "like").Expect(HttpStatusCode.OK)));

        var likes = await api.WithDbAsync(db => db.UserGenreScores.Where(s => s.UserId == user.Id).SumAsync(s => s.Likes, Ct));
        Assert.Equal(8, likes);
    }

    [Fact]
    public async Task Swipe_de_cancion_inexistente_o_inactiva_da_404()
    {
        var user = await api.CreateUserAsync();
        await Swipe(user, Guid.NewGuid(), "like").ExpectProblem(HttpStatusCode.NotFound, "song_not_found");

        var inactive = (await CreateSongsAsync("electronica", 1, active: false))[0];
        await Swipe(user, inactive, "like").ExpectProblem(HttpStatusCode.NotFound, "song_not_found");
        Assert.Equal(0, (await Dna(user)).SwipeCount);
    }

    [Fact]
    public async Task Swipe_con_body_invalido_da_400_nunca_500()
    {
        var user = await api.CreateUserAsync();
        var song = (await CreateSongsAsync("electronica", 1))[0];

        await user.Client.PostJson("/api/v1/swipes", new { songId = song }).ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/swipes", new { direction = "like" }).ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/swipes", new { songId = Guid.Empty, direction = "like" }).ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/swipes", new { songId = "no-es-guid", direction = "like" }).ExpectProblem(HttpStatusCode.BadRequest);
        Assert.Equal(0, (await Dna(user)).SwipeCount);
    }

    [Fact]
    public async Task Dislike_baja_el_peso_relativo_del_genero()
    {
        var liker = await api.CreateUserAsync(null, null, "bachata");
        var mixed = await api.CreateUserAsync(null, null, "bachata");
        var songs = await CreateSongsAsync("electronica", 3);

        await Swipe(liker, songs[0], "like").Expect(HttpStatusCode.OK);
        await Swipe(liker, songs[1], "like").Expect(HttpStatusCode.OK);
        await Swipe(mixed, songs[0], "like").Expect(HttpStatusCode.OK);
        await Swipe(mixed, songs[1], "like").Expect(HttpStatusCode.OK);
        await Swipe(mixed, songs[2], "dislike").Expect(HttpStatusCode.OK);

        Assert.True(Weight(await Dna(mixed), "electronica") < Weight(await Dna(liker), "electronica"));
    }

    [Fact]
    public async Task Radar_se_desbloquea_al_llegar_al_umbral()
    {
        var user = await api.CreateUserAsync();
        var songs = await CreateSongsAsync("electronica", 3);

        var dna = await Dna(user);
        Assert.Equal(3, dna.RadarThreshold);
        Assert.False(dna.RadarUnlocked);

        await Swipe(user, songs[0], "like").Expect(HttpStatusCode.OK);
        await Swipe(user, songs[1], "dislike").Expect(HttpStatusCode.OK);
        Assert.False((await Dna(user)).RadarUnlocked);

        await Swipe(user, songs[2], "like").Expect(HttpStatusCode.OK);
        dna = await Dna(user);
        Assert.Equal(3, dna.SwipeCount);
        Assert.True(dna.RadarUnlocked);
    }

    [Fact]
    public async Task Los_swipes_de_A_no_cambian_el_adn_ni_el_feed_de_B()
    {
        var a = await api.CreateUserAsync(null, null, "bachata");
        var b = await api.CreateUserAsync(null, null, "bachata");
        var target = (await CreateSongsAsync("electronica", 1))[0];
        // B ya calificó todo salvo la canción objetivo: su feed debe seguir ofreciéndola aunque A la califique.
        await RateAllActiveSongsAsync(b.Id, except: target);
        var dnaB = await Dna(b);

        await Swipe(a, target, "like").Expect(HttpStatusCode.OK);

        Assert.Equal(dnaB, await Dna(b), new DnaComparer());
        var feedB = await b.Client.GetAsync("/api/v1/songs/feed?limit=50", Ct).Expect<List<SongDto>>(HttpStatusCode.OK);
        Assert.Contains(feedB, s => s.Id == target);
        Assert.Equal(1, (await Dna(a)).SwipeCount);
    }

    [Fact]
    public async Task Spotify_mapea_generos_ignora_los_desconocidos_y_la_segunda_importacion_reemplaza()
    {
        var user = await api.CreateUserAsync(null, null, "bachata");
        var salsaSong = (await CreateSongsAsync("salsa", 1))[0];
        await Swipe(user, salsaSong, "like").Expect(HttpStatusCode.OK);

        var first = await user.Client.PostJson("/api/v1/me/spotify/import", new
        {
            topArtists = new[]
            {
                new { name = "A1", genres = new[] { "colombian pop", "Reggaetón" } },
                new { name = "A2", genres = new[] { "salsa choke", "genero-inventado" } },
                new { name = "A3", genres = new[] { "Rock en Español" } },
            },
        }).Expect<DnaResponse>(HttpStatusCode.OK);
        Assert.True(Weight(first, "pop-latino") > 0);
        Assert.True(Weight(first, "reggaeton") > 0);
        Assert.True(Weight(first, "champeta") > 0);
        Assert.True(Weight(first, "rock-en-espanol") > 0);
        Assert.Equal(1, first.SwipeCount);

        var second = await user.Client.PostJson("/api/v1/me/spotify/import", new
        {
            topArtists = new[] { new { name = "B1", genres = new[] { "vallenato" } } },
        }).Expect<DnaResponse>(HttpStatusCode.OK);
        Assert.True(Weight(second, "vallenato") > 0);
        foreach (var gone in new[] { "pop-latino", "reggaeton", "champeta", "rock-en-espanol" })
            Assert.Equal(0, Weight(second, gone));
        // Los likes de los swipes sobreviven a la importación.
        Assert.True(Weight(second, "salsa") > 0);
        Assert.Equal(1, second.SwipeCount);

        // Reimportar lo mismo no acumula.
        var again = await user.Client.PostJson("/api/v1/me/spotify/import", new
        {
            topArtists = new[] { new { name = "B1", genres = new[] { "vallenato" } } },
        }).Expect<DnaResponse>(HttpStatusCode.OK);
        Assert.Equal(second, again, new DnaComparer());
    }

    [Fact]
    public async Task Spotify_escala_el_aporte_total_a_un_maximo_de_10()
    {
        var user = await api.CreateUserAsync(null, null, "bachata");
        var artists = Enumerable.Range(0, 50).Select(i => new { name = $"Artista {i}", genres = new[] { "reggaeton" } }).ToArray();

        var dna = await user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = artists }).Expect<DnaResponse>(HttpStatusCode.OK);

        // bachata declarado pesa 2 y reggaetón importado queda topado en 10: 10 / 12.
        Assert.Equal(10.0 / 12.0, Weight(dna, "reggaeton"), 3);
    }

    [Fact]
    public async Task Spotify_con_body_invalido_da_400_y_sin_artistas_no_rompe()
    {
        var user = await api.CreateUserAsync();
        await user.Client.PostJson("/api/v1/me/spotify/import", new { }).ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = new[] { new { name = "x" } } }).ExpectProblem(HttpStatusCode.BadRequest);
        var tooManyGenres = Enumerable.Range(0, 21).Select(i => $"g{i}").ToArray();
        await user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = new[] { new { name = "x", genres = tooManyGenres } } })
            .ExpectProblem(HttpStatusCode.BadRequest);
        await user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = Array.Empty<object>() }).Expect(HttpStatusCode.OK);
        await api.Anonymous().PostJson("/api/v1/me/spotify/import", new { topArtists = Array.Empty<object>() })
            .ExpectProblem(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Spotify_de_A_no_cambia_el_adn_de_B()
    {
        var a = await api.CreateUserAsync(null, null, "bachata");
        var b = await api.CreateUserAsync(null, null, "bachata");
        var dnaB = await Dna(b);

        await a.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = new[] { new { name = "A", genres = new[] { "cumbia" } } } })
            .Expect(HttpStatusCode.OK);

        Assert.Equal(dnaB, await Dna(b), new DnaComparer());
        Assert.Equal(0, await api.WithDbAsync(db => db.UserGenreScores.CountAsync(s => s.UserId == b.Id, Ct)));
    }

    [Theory]
    [InlineData("colombian pop", "pop-latino")]
    [InlineData("Reggaetón", "reggaeton")]
    [InlineData("latin trap", "trap-latino")]
    [InlineData("salsa choke", "champeta")]
    [InlineData("salsa colombiana", "salsa")]
    [InlineData("música popular colombiana", "popular")]
    [InlineData("Rock en Español", "rock-en-espanol")]
    [InlineData("latin alternative", "indie-alternativo")]
    [InlineData("rap latina", "hip-hop")]
    [InlineData("corrido", "regional-mexicano")]
    [InlineData("edm", "electronica")]
    [InlineData("bachata", "bachata")]
    public void SpotifyGenreMap_mapea_a_slugs_sin_distinguir_acentos(string spotify, string expected)
        => Assert.Equal([expected], SpotifyGenreMap.Map([spotify]));

    [Theory]
    [InlineData("k-pop")]
    [InlineData("metal sinfonico")]
    [InlineData("")]
    public void SpotifyGenreMap_ignora_lo_desconocido(string spotify)
        => Assert.Empty(SpotifyGenreMap.Map([spotify]));

    private async Task<HashSet<Guid>> RateAllActiveSongsAsync(Guid userId, Guid? except)
    {
        var ids = new HashSet<Guid>();
        await api.WithDbAsync(async db =>
        {
            var songs = await db.Songs.Where(s => s.IsActive && s.Id != except).Select(s => s.Id).ToListAsync(Ct);
            foreach (var id in songs)
            {
                db.Swipes.Add(new Swipe { UserId = userId, SongId = id, Direction = SwipeDirection.Dislike });
                ids.Add(id);
            }
            await db.SaveChangesAsync(Ct);
        });
        return ids;
    }

    private sealed class DnaComparer : IEqualityComparer<DnaResponse>
    {
        public bool Equals(DnaResponse? x, DnaResponse? y) =>
            x is not null && y is not null && x.SwipeCount == y.SwipeCount && x.RadarUnlocked == y.RadarUnlocked
            && x.Genres.Select(g => (g.Genre, g.Weight)).SequenceEqual(y.Genres.Select(g => (g.Genre, g.Weight)));

        public int GetHashCode(DnaResponse obj) => obj.SwipeCount;
    }
}
