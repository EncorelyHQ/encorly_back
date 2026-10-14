using System.Net;
using System.Text;
using Encorely.Api.Features.Admin;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Encorely.Tests.Features;

public sealed class AdminTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<TestUser> AdminAsync() => api.CreateUserAsync("Admin", u => u.Role = UserRole.Admin);

    private static object EventBody(string name, int days = 10) => new
    {
        name,
        artistLineup = new[] { "Bomba Estéreo", "Monsieur Periné" },
        venue = "Movistar Arena",
        city = "Bogotá",
        startsAt = DateTimeOffset.UtcNow.AddDays(days),
        description = "Prueba",
    };

    [Fact]
    public async Task Usuario_normal_recibe_403_y_anonimo_401()
    {
        var user = await api.CreateUserAsync();
        await user.Client.GetAsync("/api/v1/admin/events", Ct).ExpectProblem(HttpStatusCode.Forbidden);
        await user.Client.PostJson("/api/v1/admin/events", EventBody("Hackeo")).ExpectProblem(HttpStatusCode.Forbidden);
        await api.Anonymous().GetAsync("/api/v1/admin/reports", Ct).ExpectProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rol_quitado_en_bd_corta_el_acceso_aunque_el_jwt_diga_admin()
    {
        var admin = await AdminAsync();
        await admin.Client.GetAsync("/api/v1/admin/events", Ct).Expect(HttpStatusCode.OK);
        await api.WithDbAsync(db => db.Users.Where(u => u.Id == admin.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.User), Ct));
        await admin.Client.GetAsync("/api/v1/admin/events", Ct).ExpectProblem(HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Crud_de_eventos()
    {
        var admin = await AdminAsync();
        var name = $"Festival {Guid.NewGuid():N}";
        var created = await admin.Client.PostJson("/api/v1/admin/events", EventBody(name)).Expect<AdminEventDto>(HttpStatusCode.Created);
        Assert.Equal(name, created.Name);
        Assert.True(created.IsPublished);

        Assert.True(await api.WithDbAsync(db => db.Events.AnyAsync(e => e.Id == created.Id && e.IsPublished, Ct)));

        var updated = await admin.Client.PutJson($"/api/v1/admin/events/{created.Id}", EventBody(name + " II"))
            .Expect<AdminEventDto>(HttpStatusCode.OK);
        Assert.EndsWith(" II", updated.Name);

        await admin.Client.DeleteAsync($"/api/v1/admin/events/{created.Id}", Ct).Expect(HttpStatusCode.NoContent);
        await admin.Client.DeleteAsync($"/api/v1/admin/events/{created.Id}", Ct).ExpectProblem(HttpStatusCode.NotFound, "event_not_found");
        await admin.Client.PutJson($"/api/v1/admin/events/{Guid.NewGuid()}", EventBody("x y z")).ExpectProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Evento_invalido_da_400()
    {
        var admin = await AdminAsync();
        await admin.Client.PostJson("/api/v1/admin/events", new
        {
            name = "A",
            artistLineup = Array.Empty<string>(),
            venue = "V",
            city = "",
            startsAt = DateTimeOffset.UtcNow,
            imageUrl = "no-es-url",
        }).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Canciones_alta_duplicado_y_desactivacion()
    {
        var admin = await AdminAsync();
        var trackId = Random.Shared.NextInt64(1_000_000_000, 9_000_000_000);
        object Body(bool active = true, string genre = "salsa") => new
        {
            title = "Canción de prueba",
            artist = $"Artista {trackId}",
            genre,
            coverUrl = "https://example.com/cover.jpg",
            previewUrl = "https://example.com/preview.m4a",
            iTunesTrackId = trackId,
            isActive = active,
        };

        var song = await admin.Client.PostJson("/api/v1/admin/songs", Body()).Expect<AdminSongDto>(HttpStatusCode.Created);
        Assert.Equal("salsa", song.Genre);
        await admin.Client.PostJson("/api/v1/admin/songs", Body()).ExpectProblem(HttpStatusCode.Conflict, "song_duplicate");
        await admin.Client.PostJson("/api/v1/admin/songs", Body(genre: "polka")).ExpectProblem(HttpStatusCode.BadRequest, "unknown_genre");

        var off = await admin.Client.PutJson($"/api/v1/admin/songs/{song.Id}", Body(active: false)).Expect<AdminSongDto>(HttpStatusCode.OK);
        Assert.False(off.IsActive);

        var list = await admin.Client.GetAsync($"/api/v1/admin/songs?query={trackId}", Ct).Expect<List<AdminSongDto>>(HttpStatusCode.OK);
        Assert.Single(list);
        await admin.Client.GetAsync("/api/v1/admin/songs?limit=0", Ct).ExpectProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task No_se_cambia_el_genero_de_una_cancion_ya_calificada()
    {
        var admin = await AdminAsync();
        var user = await api.CreateUserAsync();
        var song = await admin.Client.PostJson("/api/v1/admin/songs", new
        {
            title = "Calificada",
            artist = $"A {Guid.NewGuid():N}",
            genre = "cumbia",
            coverUrl = "https://example.com/c.jpg",
        }).Expect<AdminSongDto>(HttpStatusCode.Created);
        await api.WithDbAsync(async db =>
        {
            db.Swipes.Add(new Swipe { UserId = user.Id, SongId = song.Id, Direction = SwipeDirection.Like });
            await db.SaveChangesAsync(Ct);
        });

        await admin.Client.PutJson($"/api/v1/admin/songs/{song.Id}", new
        {
            title = "Calificada",
            artist = song.Artist,
            genre = "salsa",
            coverUrl = "https://example.com/c.jpg",
        }).ExpectProblem(HttpStatusCode.Conflict, "song_has_swipes");
    }

    [Fact]
    public async Task Resolver_reporte_con_ban_suspende_cierra_matches_y_revoca_sesiones()
    {
        var admin = await AdminAsync();
        var reporter = await api.CreateUserAsync("Ana");
        var reported = await api.CreateUserAsync("Troll");
        var reportId = Guid.Empty;
        var matchId = Guid.Empty;
        await api.WithDbAsync(async db =>
        {
            var (a, b) = Match.OrderPair(reporter.Id, reported.Id);
            var match = new Match { UserAId = a, UserBId = b };
            db.Matches.Add(match);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = reported.Id, FamilyId = Guid.NewGuid(), TokenHash = TokenService.CreateRefreshToken().Hash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            });
            var report = new Report
            {
                ReporterId = reporter.Id, ReportedUserId = reported.Id, MatchId = match.Id, Reason = ReportReason.Harassment,
                Evidence = [new ReportEvidence { MessageId = Guid.NewGuid(), Plaintext = "{\"b\":\"hola\"}", Verified = true }],
            };
            db.Reports.Add(report);
            await db.SaveChangesAsync(Ct);
            reportId = report.Id;
            matchId = match.Id;
        });

        var open = await admin.Client.GetAsync("/api/v1/admin/reports", Ct).Expect<List<AdminReportDto>>(HttpStatusCode.OK);
        var dto = Assert.Single(open, r => r.Id == reportId);
        Assert.Equal("Troll", dto.Reported!.DisplayName);
        Assert.True(Assert.Single(dto.Evidence).Verified);

        var resolved = await admin.Client.PostJson($"/api/v1/admin/reports/{reportId}/resolve", new { action = "ban", note = "Acoso" })
            .Expect<AdminReportDto>(HttpStatusCode.OK);
        Assert.Equal(ReportStatus.Resolved, resolved.Status);

        await api.WithDbAsync(async db =>
        {
            Assert.Equal(UserStatus.Banned, (await db.Users.SingleAsync(u => u.Id == reported.Id, Ct)).Status);
            var match = await db.Matches.SingleAsync(m => m.Id == matchId, Ct);
            Assert.Equal(MatchStatus.Closed, match.Status);
            Assert.Equal(MatchCloseReason.Banned, match.CloseReason);
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == reported.Id && t.RevokedAt == null, Ct));
        });
        await reported.Client.GetAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.Forbidden, "account_banned");
        await admin.Client.PostJson($"/api/v1/admin/reports/{reportId}/resolve", new { action = "dismiss" })
            .ExpectProblem(HttpStatusCode.Conflict, "report_already_resolved");
    }

    [Fact]
    public async Task Admin_no_puede_suspenderse_a_si_mismo_y_puede_reactivar()
    {
        var admin = await AdminAsync();
        var user = await api.CreateUserAsync();
        await admin.Client.PutJson($"/api/v1/admin/users/{admin.Id}/status", new { status = "banned" }).ExpectProblem(HttpStatusCode.BadRequest);
        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "banned" }).Expect(HttpStatusCode.NoContent);
        await user.Client.GetAsync("/api/v1/me", Ct).ExpectProblem(HttpStatusCode.Forbidden);
        await admin.Client.PutJson($"/api/v1/admin/users/{user.Id}/status", new { status = "active" }).Expect(HttpStatusCode.NoContent);
        await admin.Client.PutJson($"/api/v1/admin/users/{Guid.NewGuid()}/status", new { status = "banned" }).ExpectProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Busqueda_itunes_mapea_resultados_y_valida_el_termino()
    {
        const string itunesJson = """
            {"resultCount":1,"results":[{"trackId":123,"trackName":"La Luz","artistName":"Kali Uchis",
            "artworkUrl100":"https://is1.mzstatic.com/x/100x100bb.jpg","previewUrl":"https://audio/p.m4a","primaryGenreName":"Pop"}]}
            """;
        await using var factory = api.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddHttpClient<ITunesClient>(c => c.BaseAddress = new Uri(ITunesClient.BaseAddress))
                .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(itunesJson))));
        var admin = await AdminAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);

        var results = await client.GetAsync("/api/v1/admin/itunes/search?term=kali%20uchis", Ct).Expect<List<ITunesTrackDto>>(HttpStatusCode.OK);
        var track = Assert.Single(results);
        Assert.Equal(123, track.ITunesTrackId);
        Assert.EndsWith("600x600bb.jpg", track.CoverUrl);
        await client.GetAsync("/api/v1/admin/itunes/search?term=k", Ct).ExpectProblem(HttpStatusCode.BadRequest);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
