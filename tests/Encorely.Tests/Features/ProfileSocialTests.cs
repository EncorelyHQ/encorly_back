using System.Net;
using System.Net.Http.Headers;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Profiles;
using Encorely.Api.Features.Users;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Encorely.Tests.Features;

/// <summary>Perfil social (draft.10): perfil ajeno, artistas importados, estadísticas, reacciones y afinidad con señal.</summary>
public sealed class ProfileSocialTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Image = "https://i.scdn.co/image/ab6761610000e5eb";

    private async Task<Guid> SharedUpcomingEventAsync(params TestUser[] users)
    {
        var ev = new Event { Name = $"Evento {Guid.NewGuid():N}", Venue = "Movistar Arena", City = "Bogotá", StartsAt = DateTimeOffset.UtcNow.AddDays(7) };
        await api.WithDbAsync(async db =>
        {
            db.Events.Add(ev);
            foreach (var u in users) db.EventAttendances.Add(new EventAttendance { EventId = ev.Id, UserId = u.Id });
            await db.SaveChangesAsync(Ct);
        });
        return ev.Id;
    }

    private static Task ImportAsync(TestUser user, params object[] artists)
        => user.Client.PostJson("/api/v1/me/spotify/import", new { topArtists = artists }).Expect(HttpStatusCode.OK);

    private static object Artist(string name, string genre = "reggaeton", string? imageUrl = Image) => new { name, genres = new[] { genre }, imageUrl };

    private static Task<HttpResponseMessage> GetProfile(TestUser viewer, Guid targetId) => viewer.Client.GetAsync($"/api/v1/users/{targetId}/profile", Ct);

    private static Task<HttpResponseMessage> React(TestUser viewer, Guid targetId, string kind, string key, string reaction)
        => viewer.Client.PutJson($"/api/v1/users/{targetId}/reactions", new { targetKind = kind, targetKey = key, reaction });

    [Fact]
    public async Task Perfil_ajeno_trae_artistas_generos_stats_y_lo_comun()
    {
        var viewer = await api.CreateUserAsync("Ana", null, "reggaeton");
        var target = await api.CreateUserAsync("Beto", null, "reggaeton", "salsa");
        await ImportAsync(target, Artist("Bad Bunny"), Artist("Karol G", imageUrl: "http://i.scdn.co/inseguro"),
            Artist("Grupo Niche", "salsa", "https://evil.example/x.jpg"), Artist("  bad   BUNNY "), Artist("!!!"));
        await ImportAsync(viewer, Artist("Bad Bunny"));
        await SharedUpcomingEventAsync(viewer, target);

        var profile = await GetProfile(viewer, target.Id).Expect<PublicProfileDto>(HttpStatusCode.OK);

        Assert.Equal("Beto", profile.DisplayName);
        Assert.Equal(["bad-bunny", "karol-g", "grupo-niche"], profile.Artists.Select(a => a.Key));
        Assert.Equal([Image, null, null], profile.Artists.Select(a => a.ImageUrl));
        Assert.Equal([true, false, false], profile.Artists.Select(a => a.Shared));
        Assert.Equal(["bad-bunny"], profile.Common.SharedArtists);
        Assert.Contains("reggaeton", profile.Common.SharedGenres);
        Assert.NotNull(profile.Common.Affinity);
        Assert.Contains(profile.Genres, g => g.Slug == "salsa");
        Assert.Equal(new ProfileStatsDto(0, 0, 1), profile.Stats);
        Assert.Empty(profile.MyReactions);
    }

    [Fact]
    public async Task Sin_relacion_con_bloqueo_o_suspendido_da_404_y_con_match_activo_se_ve()
    {
        var viewer = await api.CreateUserAsync();
        var stranger = await api.CreateUserAsync();
        await GetProfile(viewer, stranger.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await GetProfile(viewer, Guid.NewGuid()).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");

        var blocker = await api.CreateUserAsync();
        var banned = await api.CreateUserAsync(configure: u => u.Status = UserStatus.Banned);
        await SharedUpcomingEventAsync(viewer, blocker, banned);
        await GetProfile(viewer, blocker.Id).Expect(HttpStatusCode.OK);
        await api.WithDbAsync(async db =>
        {
            db.Blocks.Add(new Block { BlockerId = blocker.Id, BlockedId = viewer.Id });
            await db.SaveChangesAsync(Ct);
        });
        await GetProfile(viewer, blocker.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await GetProfile(viewer, banned.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");

        var matched = await api.CreateUserAsync();
        await api.WithDbAsync(async db =>
        {
            var (a, b) = Match.OrderPair(viewer.Id, matched.Id);
            db.Matches.Add(new Match { UserAId = a, UserBId = b });
            await db.SaveChangesAsync(Ct);
        });
        await GetProfile(viewer, matched.Id).Expect(HttpStatusCode.OK);

        // Un evento ya pasado no cuenta.
        var past = await api.CreateUserAsync();
        await api.WithDbAsync(async db =>
        {
            var ev = new Event { Name = "Pasado", Venue = "V", City = "Bogotá", StartsAt = DateTimeOffset.UtcNow.AddDays(-3) };
            db.Events.Add(ev);
            db.EventAttendances.AddRange(new EventAttendance { EventId = ev.Id, UserId = viewer.Id }, new EventAttendance { EventId = ev.Id, UserId = past.Id });
            await db.SaveChangesAsync(Ct);
        });
        await GetProfile(viewer, past.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
    }

    [Fact]
    public async Task Reacciones_se_reemplazan_se_agregan_sin_identidad_y_se_quitan()
    {
        var target = await api.CreateUserAsync("Diana", null, "salsa");
        var ana = await api.CreateUserAsync();
        var beto = await api.CreateUserAsync();
        await ImportAsync(target, Artist("Grupo Niche", "salsa"));
        await SharedUpcomingEventAsync(target, ana, beto);

        await React(ana, target.Id, "artist", "grupo-niche", "meToo").Expect(HttpStatusCode.NoContent);
        await React(ana, target.Id, "artist", "Grupo-Niche", "letsGo").Expect(HttpStatusCode.NoContent);
        await React(beto, target.Id, "artist", "grupo-niche", "letsGo").Expect(HttpStatusCode.NoContent);
        await React(beto, target.Id, "genre", "salsa", "sendOne").Expect(HttpStatusCode.NoContent);

        var seen = await GetProfile(ana, target.Id).Expect<PublicProfileDto>(HttpStatusCode.OK);
        Assert.Equal([new MyReactionDto(ReactionTargetKind.Artist, "grupo-niche", ReactionType.LetsGo)], seen.MyReactions);

        var me = await target.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(
            [
                new ReceivedReactionDto(ReactionTargetKind.Artist, "grupo-niche", "Grupo Niche", ReactionType.LetsGo, 2),
                new ReceivedReactionDto(ReactionTargetKind.Genre, "salsa", "Salsa", ReactionType.SendOne, 1),
            ],
            me.ReactionsReceived);
        Assert.Equal(["grupo-niche"], me.Artists.Select(a => a.Key));
        Assert.Equal(1, me.Stats.EventsMarked);

        await ana.Client.DeleteAsync($"/api/v1/users/{target.Id}/reactions?targetKind=artist&targetKey=grupo-niche", Ct).Expect(HttpStatusCode.NoContent);
        await ana.Client.DeleteAsync($"/api/v1/users/{target.Id}/reactions?targetKind=artist&targetKey=grupo-niche", Ct).Expect(HttpStatusCode.NoContent);
        await ana.Client.DeleteAsync($"/api/v1/users/{target.Id}/reactions?targetKind=song&targetKey=x", Ct).ExpectProblem(HttpStatusCode.BadRequest);
        me = await target.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Equal(1, me.ReactionsReceived.Single(r => r.TargetKind == ReactionTargetKind.Artist).Count);

        // Bloquear borra las reacciones del par.
        await target.Client.PostJson("/api/v1/blocks", new { userId = beto.Id }).Expect(HttpStatusCode.NoContent);
        me = await target.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.Empty(me.ReactionsReceived);
        Assert.False(await api.WithDbAsync(db => db.ProfileReactions.AnyAsync(r => r.FromUserId == beto.Id, Ct)));
    }

    [Fact]
    public async Task Reaccionar_valida_objetivo_relacion_y_tipo()
    {
        var target = await api.CreateUserAsync(null, null, "salsa");
        var viewer = await api.CreateUserAsync();
        var stranger = await api.CreateUserAsync();
        await ImportAsync(target, Artist("Grupo Niche", "salsa"));
        await SharedUpcomingEventAsync(target, viewer);

        await React(viewer, viewer.Id, "genre", "salsa", "meToo").ExpectProblem(HttpStatusCode.BadRequest, "cannot_react_self");
        await React(viewer, target.Id, "artist", "bad-bunny", "meToo").ExpectProblem(HttpStatusCode.BadRequest, "reaction_target_not_found");
        await React(viewer, target.Id, "genre", "vallenato", "meToo").ExpectProblem(HttpStatusCode.BadRequest, "reaction_target_not_found");
        await React(viewer, target.Id, "genre", "salsa", "me_too").ExpectProblem(HttpStatusCode.BadRequest);
        await React(stranger, target.Id, "genre", "salsa", "meToo").ExpectProblem(HttpStatusCode.NotFound, "user_not_found");

        await React(viewer, target.Id, "genre", "salsa", "sendOne").Expect(HttpStatusCode.NoContent);
        var export = await viewer.Client.GetAsync("/api/v1/me/export", Ct).Expect<DataExportResponse>(HttpStatusCode.OK);
        var given = Assert.Single(export.Reactions);
        Assert.Equal((ReactionTargetKind.Genre, "salsa", ReactionType.SendOne), (given.TargetKind, given.TargetKey, given.Reaction));
    }

    [Fact]
    public async Task Sin_senal_suficiente_la_afinidad_es_null_en_perfil_y_radar()
    {
        var viewer = await api.CreateUserAsync("Ana", null, "reggaeton");
        var target = await api.CreateUserAsync("Beto", null, "reggaeton");
        var eventId = await SharedUpcomingEventAsync(viewer, target);
        // El radar exige RadarMinSwipes (3) al que mira; menos que AffinityMinSwipes (40) no da señal.
        await api.WithDbAsync(async db =>
        {
            var songs = await db.Songs.OrderBy(s => s.Id).Take(3).Select(s => s.Id).ToListAsync(Ct);
            db.Swipes.AddRange(songs.Select(id => new Swipe { UserId = viewer.Id, SongId = id, Direction = SwipeDirection.Like }));
            await db.SaveChangesAsync(Ct);
        });

        await using var strict = api.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Matching:AffinityMinSwipes"] = "40" })));
        var client = strict.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewer.AccessToken);

        Assert.Null((await client.GetAsync($"/api/v1/users/{target.Id}/profile", Ct).Expect<PublicProfileDto>(HttpStatusCode.OK)).Common.Affinity);
        Assert.Null(Assert.Single(await client.GetAsync($"/api/v1/events/{eventId}/radar", Ct).Expect<List<RadarEntryDto>>(HttpStatusCode.OK)).Affinity);

        // Importar Spotify da señal: con los dos, vuelve la afinidad.
        await ImportAsync(viewer, Artist("Bad Bunny"));
        Assert.Null((await client.GetAsync($"/api/v1/users/{target.Id}/profile", Ct).Expect<PublicProfileDto>(HttpStatusCode.OK)).Common.Affinity);
        await ImportAsync(target, Artist("Karol G"));
        Assert.Equal(100, (await client.GetAsync($"/api/v1/users/{target.Id}/profile", Ct).Expect<PublicProfileDto>(HttpStatusCode.OK)).Common.Affinity);
        Assert.Equal(100, Assert.Single(await client.GetAsync($"/api/v1/events/{eventId}/radar", Ct).Expect<List<RadarEntryDto>>(HttpStatusCode.OK)).Affinity);
    }

    [Theory]
    [InlineData("Bad Bunny", "bad-bunny")]
    [InlineData("  Beyoncé ", "beyonce")]
    [InlineData("AC/DC", "ac-dc")]
    [InlineData("!!!", "")]
    public void La_key_del_artista_normaliza_el_nombre(string name, string key) => Assert.Equal(key, ArtistKey.Of(name));

    [Theory]
    [InlineData("https://i.scdn.co/image/abc", true)]
    [InlineData("http://i.scdn.co/image/abc", false)]
    [InlineData("https://i.scdn.co:8443/image/abc", false)]
    [InlineData("https://user@i.scdn.co/image/abc", false)]
    [InlineData("https://evil.example/i.scdn.co.jpg", false)]
    [InlineData(null, false)]
    public void Solo_se_guardan_imagenes_https_del_CDN_de_Spotify(string? url, bool kept)
        => Assert.Equal(kept, ArtistKey.SafeImageUrl(url) is not null);
}
