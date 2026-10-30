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

    /// <summary>Acepta la versión de consentimiento con la finalidad "perfil público": sin ella el perfil no se muestra.</summary>
    private async Task ConsentAsync(params TestUser[] users)
    {
        await api.WithDbAsync(async db =>
        {
            foreach (var u in users) db.UserConsents.Add(new UserConsent { UserId = u.Id, Version = ProfileService.PublicProfileConsentSince });
            await db.SaveChangesAsync(Ct);
        });
    }

    private async Task<TestUser> VisibleUserAsync(string? name = null, params string[] genres)
    {
        var user = await api.CreateUserAsync(name, null, genres);
        await ConsentAsync(user);
        return user;
    }

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
        var viewer = await VisibleUserAsync("Ana", "reggaeton");
        var target = await VisibleUserAsync("Beto", "reggaeton", "salsa");
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
    public async Task CanLike_coincide_con_lo_que_responderia_post_likes()
    {
        var me = await VisibleUserAsync("Ana");
        var other = await VisibleUserAsync("Beto");
        async Task<bool> CanLike(TestUser viewer, Guid target)
            => (await GetProfile(viewer, target).Expect<PublicProfileDto>(HttpStatusCode.OK)).CanLike;

        // Sin evento en común el perfil se ve, pero el like falla: canLike lo anticipa.
        Assert.False(await CanLike(me, other.Id));
        await me.Client.PostJson("/api/v1/likes", new { targetUserId = other.Id }).ExpectProblem(HttpStatusCode.Forbidden, "no_shared_event");

        await SharedUpcomingEventAsync(me, other);
        Assert.True(await CanLike(me, other.Id));
        await me.Client.PostJson("/api/v1/likes", new { targetUserId = other.Id }).Expect(HttpStatusCode.OK);
        Assert.True(await CanLike(other, me.Id));

        // Con match ya no hay nada que ofrecer, y en el perfil propio nunca.
        await other.Client.PostJson("/api/v1/likes", new { targetUserId = me.Id }).Expect(HttpStatusCode.OK);
        Assert.False(await CanLike(me, other.Id));
        Assert.False(await CanLike(me, me.Id));
    }

    [Fact]
    public async Task Perfil_dice_si_ya_le_di_like_y_si_hay_match()
    {
        var me = await VisibleUserAsync("Ana");
        var other = await VisibleUserAsync("Beto");
        async Task<(bool Liked, bool Matched)> State(TestUser viewer, Guid target)
        {
            var p = await GetProfile(viewer, target).Expect<PublicProfileDto>(HttpStatusCode.OK);
            return (p.LikedByMe, p.Matched);
        }

        Assert.Equal((false, false), await State(me, other.Id));
        await api.WithDbAsync(async db =>
        {
            db.UserLikes.Add(new UserLike { FromUserId = me.Id, ToUserId = other.Id });
            await db.SaveChangesAsync(Ct);
        });
        Assert.Equal((true, false), await State(me, other.Id));
        // El like es de una sola dirección: el otro no lo ve como suyo.
        Assert.Equal((false, false), await State(other, me.Id));

        await api.WithDbAsync(async db =>
        {
            var (a, b) = Match.OrderPair(me.Id, other.Id);
            db.Matches.Add(new Match { UserAId = a, UserBId = b });
            await db.SaveChangesAsync(Ct);
        });
        Assert.Equal((true, true), await State(me, other.Id));
        Assert.Equal((false, true), await State(other, me.Id));
        Assert.Equal((false, false), await State(me, me.Id));
    }

    [Fact]
    public async Task Cualquiera_ve_el_perfil_salvo_bloqueo_suspension_interruptor_apagado_o_sin_consentimiento()
    {
        var viewer = await VisibleUserAsync();
        var stranger = await VisibleUserAsync();
        // Sin match ni evento en común: se ve igual.
        await GetProfile(viewer, stranger.Id).Expect(HttpStatusCode.OK);
        await GetProfile(viewer, Guid.NewGuid()).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");

        var noConsent = await api.CreateUserAsync();
        await GetProfile(viewer, noConsent.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        var me = await noConsent.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK);
        Assert.True(me.ShowProfileToOthers);
        Assert.False(me.ProfileVisible);
        // El dueño siempre puede pedir su propio perfil.
        await GetProfile(noConsent, noConsent.Id).Expect(HttpStatusCode.OK);

        var hidden = await VisibleUserAsync();
        Assert.True((await hidden.Client.PatchJson("/api/v1/me", new { showProfileToOthers = false }).Expect<MeResponse>(HttpStatusCode.OK)) is { ShowProfileToOthers: false, ProfileVisible: false });
        await GetProfile(viewer, hidden.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await GetProfile(hidden, hidden.Id).Expect(HttpStatusCode.OK);
        var export = await hidden.Client.GetAsync("/api/v1/me/export", Ct).Expect<DataExportResponse>(HttpStatusCode.OK);
        Assert.False(export.Profile.ShowProfileToOthers);

        var blocker = await VisibleUserAsync();
        var banned = await api.CreateUserAsync(configure: u => u.Status = UserStatus.Banned);
        await ConsentAsync(banned);
        await api.WithDbAsync(async db =>
        {
            db.Blocks.Add(new Block { BlockerId = blocker.Id, BlockedId = viewer.Id });
            await db.SaveChangesAsync(Ct);
        });
        await GetProfile(viewer, blocker.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await GetProfile(blocker, viewer.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        await GetProfile(viewer, banned.Id).ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
    }

    [Fact]
    public async Task Bloquear_y_reportar_funcionan_sin_match_ni_evento_en_comun()
    {
        var victim = await VisibleUserAsync();
        var stranger = await VisibleUserAsync();
        await victim.Client.PostJson("/api/v1/reports", new { userId = stranger.Id, reason = "harassment", detail = "Reacciones insistentes" })
            .Expect(HttpStatusCode.Accepted);
        await victim.Client.PostJson("/api/v1/blocks", new { userId = stranger.Id }).Expect(HttpStatusCode.NoContent);
        await React(stranger, victim.Id, "genre", "reggaeton", "meToo").ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
    }

    [Fact]
    public async Task Reacciones_se_reemplazan_se_agregan_y_se_quitan()
    {
        var target = await VisibleUserAsync("Diana", "salsa");
        var ana = await VisibleUserAsync();
        var beto = await VisibleUserAsync();
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
        var target = await VisibleUserAsync(null, "salsa");
        var viewer = await VisibleUserAsync();
        var blocked = await VisibleUserAsync();
        var hidden = await api.CreateUserAsync(null, null, "salsa");
        await ImportAsync(target, Artist("Grupo Niche", "salsa"));
        await target.Client.PostJson("/api/v1/blocks", new { userId = blocked.Id }).Expect(HttpStatusCode.NoContent);

        await React(viewer, viewer.Id, "genre", "salsa", "meToo").ExpectProblem(HttpStatusCode.BadRequest, "cannot_react_self");
        await React(viewer, target.Id, "artist", "bad-bunny", "meToo").ExpectProblem(HttpStatusCode.BadRequest, "reaction_target_not_found");
        await React(viewer, target.Id, "genre", "vallenato", "meToo").ExpectProblem(HttpStatusCode.BadRequest, "reaction_target_not_found");
        await React(viewer, target.Id, "genre", "salsa", "me_too").ExpectProblem(HttpStatusCode.BadRequest);
        await React(blocked, target.Id, "genre", "salsa", "meToo").ExpectProblem(HttpStatusCode.NotFound, "user_not_found");
        // Sin el consentimiento del perfil público tampoco se le puede reaccionar.
        await React(viewer, hidden.Id, "genre", "salsa", "meToo").ExpectProblem(HttpStatusCode.NotFound, "user_not_found");

        await React(viewer, target.Id, "genre", "salsa", "sendOne").Expect(HttpStatusCode.NoContent);
        var export = await viewer.Client.GetAsync("/api/v1/me/export", Ct).Expect<DataExportResponse>(HttpStatusCode.OK);
        var given = Assert.Single(export.Reactions);
        Assert.Equal((ReactionTargetKind.Genre, "salsa", ReactionType.SendOne), (given.TargetKind, given.TargetKey, given.Reaction));
    }

    [Fact]
    public async Task Sin_senal_suficiente_la_afinidad_es_null_en_perfil_y_radar()
    {
        var viewer = await VisibleUserAsync("Ana", "reggaeton");
        var target = await VisibleUserAsync("Beto", "reggaeton");
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

    [Fact]
    public async Task Recibidas_muestran_quien_paginan_y_respetan_los_interruptores()
    {
        var owner = await VisibleUserAsync("Dueña", "salsa");
        var reactors = new List<TestUser>();
        foreach (var name in new[] { "Ana", "Beto", "Caro" }) reactors.Add(await VisibleUserAsync(name));
        foreach (var r in reactors) await React(r, owner.Id, "genre", "salsa", "letsGo").Expect(HttpStatusCode.NoContent);

        var first = await owner.Client.GetAsync("/api/v1/me/reactions/received?limit=2", Ct).Expect<ReceivedReactionPage>(HttpStatusCode.OK);
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        var second = await owner.Client.GetAsync($"/api/v1/me/reactions/received?limit=2&before={first.NextCursor}", Ct)
            .Expect<ReceivedReactionPage>(HttpStatusCode.OK);
        Assert.Null(second.NextCursor);
        var all = first.Items.Concat(second.Items).ToList();
        Assert.Equal(["Caro", "Beto", "Ana"], all.Select(i => i.From.DisplayName));
        Assert.All(all, i => Assert.Equal(("salsa", "Salsa", ReactionType.LetsGo), (i.TargetKey, i.TargetName, i.Reaction)));
        Assert.Equal(reactors.Select(r => r.Id).Reverse(), all.Select(i => i.From.UserId));

        await owner.Client.GetAsync("/api/v1/me/reactions/received?before=no-es-un-cursor", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await owner.Client.GetAsync("/api/v1/me/reactions/received?limit=0", Ct).ExpectProblem(HttpStatusCode.BadRequest);

        // Quien apaga su perfil deja de mostrarse en las reacciones que dejó (se conservan).
        await reactors[0].Client.PatchJson("/api/v1/me", new { showProfileToOthers = false }).Expect(HttpStatusCode.OK);
        var page = await owner.Client.GetAsync("/api/v1/me/reactions/received", Ct).Expect<ReceivedReactionPage>(HttpStatusCode.OK);
        Assert.Equal(["Caro", "Beto"], page.Items.Select(i => i.From.DisplayName));
        Assert.Equal(2, (await owner.Client.GetAsync("/api/v1/me", Ct).Expect<MeResponse>(HttpStatusCode.OK)).ReactionsReceived.Single().Count);

        // Con el perfil propio apagado no se muestran, pero vuelven al encenderlo.
        await owner.Client.PatchJson("/api/v1/me", new { showProfileToOthers = false }).Expect(HttpStatusCode.OK);
        Assert.Empty((await owner.Client.GetAsync("/api/v1/me/reactions/received", Ct).Expect<ReceivedReactionPage>(HttpStatusCode.OK)).Items);
        await owner.Client.PatchJson("/api/v1/me", new { showProfileToOthers = true }).Expect(HttpStatusCode.OK);
        Assert.Equal(2, (await owner.Client.GetAsync("/api/v1/me/reactions/received", Ct).Expect<ReceivedReactionPage>(HttpStatusCode.OK)).Items.Count);
    }

    [Fact]
    public async Task Los_artistas_suman_los_de_Me_gusta_despues_de_Spotify_con_tope_de_12()
    {
        var viewer = await VisibleUserAsync();
        var target = await VisibleUserAsync();
        await ImportAsync(target, Artist("Bad Bunny"));
        var liked = await api.WithDbAsync(async db =>
        {
            var songs = await db.Songs.Where(s => s.IsActive).OrderBy(s => s.Artist).ToListAsync(Ct);
            db.Swipes.AddRange(songs.Select(song => new Swipe { UserId = target.Id, SongId = song.Id, Direction = SwipeDirection.Like }));
            await db.SaveChangesAsync(Ct);
            return songs.Select(song => ArtistKey.Of(song.Artist)).Distinct().Count();
        });

        var profile = await GetProfile(viewer, target.Id).Expect<PublicProfileDto>(HttpStatusCode.OK);
        Assert.Equal("bad-bunny", profile.Artists[0].Key);
        Assert.Equal(Math.Min(12, 1 + liked), profile.Artists.Count);
        Assert.Equal(profile.Artists.Count, profile.Artists.Select(a => a.Key).Distinct().Count());
        Assert.All(profile.Artists.Skip(1), a => Assert.Null(a.ImageUrl));

        // Se puede reaccionar a un artista que viene de "Me gusta".
        await React(viewer, target.Id, "artist", profile.Artists[^1].Key, "meToo").Expect(HttpStatusCode.NoContent);
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
