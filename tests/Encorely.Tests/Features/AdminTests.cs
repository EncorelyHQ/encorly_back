using System.Net;
using System.Text;
using Encorely.Api.Features.Admin;
using Encorely.Api.Features.Auth;
using Encorely.Api.Features.Events;
using Encorely.Api.Features.Matching;
using Encorely.Api.Features.Moderation;
using Encorely.Api.Features.Music;
using Encorely.Api.Features.Users;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Database;
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
    public async Task El_historial_del_reportado_cuenta_personas_y_no_reportes()
    {
        var admin = await AdminAsync();
        var reported = await api.CreateUserAsync("Troll");
        var insistent = await api.CreateUserAsync("Insistente");
        var other = await api.CreateUserAsync("Otra");
        var openId = Guid.Empty;
        await api.WithDbAsync(async db =>
        {
            // La misma persona ya tuvo 3 reportes cerrados contra él; otra persona tiene uno abierto.
            for (var i = 0; i < 3; i++)
                db.Reports.Add(new Report { ReporterId = insistent.Id, ReportedUserId = reported.Id, Reason = ReportReason.Spam, Status = ReportStatus.Dismissed });
            var open = new Report { ReporterId = other.Id, ReportedUserId = reported.Id, Reason = ReportReason.Harassment };
            db.Reports.Add(open);
            await db.SaveChangesAsync(Ct);
            openId = open.Id;
        });

        var queue = await admin.Client.GetAsync("/api/v1/admin/reports?status=open", Ct).Expect<List<AdminReportDto>>(HttpStatusCode.OK);
        Assert.Equal(1, Assert.Single(queue, r => r.Id == openId).PriorReportsAgainstUser);
    }

    [Fact]
    public async Task La_evidencia_se_borra_a_los_180_dias_de_resolver_o_al_borrarse_la_cuenta_reportada()
    {
        var reporter = await api.CreateUserAsync("Ana");
        var reported = await api.CreateUserAsync("Troll");
        var now = DateTimeOffset.UtcNow;
        Report Make(ReportStatus status, DateTimeOffset? resolvedAt, Guid? reportedId) => new()
        {
            ReporterId = reporter.Id, ReportedUserId = reportedId, Reason = ReportReason.Harassment, Detail = "me insulta",
            Status = status, ResolvedAt = resolvedAt, CreatedAt = now.AddDays(-400),
            Evidence = [new ReportEvidence { MessageId = Guid.NewGuid(), Plaintext = "{\"b\":\"insulto\"}", Verified = true }],
        };
        var old = Make(ReportStatus.Resolved, now.AddDays(-Report.EvidenceRetentionDays - 1), reported.Id);
        var recent = Make(ReportStatus.Dismissed, now.AddDays(-10), reported.Id);
        var openOld = Make(ReportStatus.Open, null, reported.Id);
        var orphan = Make(ReportStatus.Open, null, null);   // la cuenta reportada ya se borró
        await api.WithDbAsync(async db =>
        {
            db.Reports.AddRange(old, recent, openOld, orphan);
            await db.SaveChangesAsync(Ct);
        });

        await api.Service<MaintenanceService>().RunOnceAsync(Ct);

        await api.WithDbAsync(async db =>
        {
            var byId = await db.Reports.AsNoTracking().Where(r => new[] { old.Id, recent.Id, openOld.Id, orphan.Id }.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, Ct);
            foreach (var purged in new[] { byId[old.Id], byId[orphan.Id] })
            {
                Assert.NotNull(purged.EvidencePurgedAt);
                Assert.Null(purged.Detail);
                var ev = Assert.Single(purged.Evidence);
                Assert.Equal("", ev.Plaintext);
                Assert.True(ev.Verified);   // queda el resultado de la verificación
            }
            Assert.Equal(ReportStatus.Dismissed, byId[orphan.Id].Status);
            Assert.NotNull(byId[orphan.Id].ResolvedAt);
            foreach (var kept in new[] { byId[recent.Id], byId[openOld.Id] })
            {
                Assert.Null(kept.EvidencePurgedAt);
                Assert.Equal("me insulta", kept.Detail);
                Assert.Equal("{\"b\":\"insulto\"}", Assert.Single(kept.Evidence).Plaintext);
            }
        });
    }

    [Fact]
    public async Task Canciones_se_paginan_con_offset_sin_repetir_ni_saltar()
    {
        var admin = await AdminAsync();
        var prefix = "Paginado" + Guid.NewGuid().ToString("N")[..8];
        await api.WithDbAsync(async db =>
        {
            var genreId = await db.Genres.Select(g => g.Id).FirstAsync(Ct);
            // Mismo artista y título: solo el id desempata el orden.
            for (var i = 0; i < 5; i++)
                db.Songs.Add(new Song { Title = "Igual", Artist = prefix, GenreId = genreId, CoverUrl = "https://example.com/c.jpg" });
            await db.SaveChangesAsync(Ct);
        });

        async Task<List<AdminSongDto>> Page(int offset) => await admin.Client
            .GetAsync($"/api/v1/admin/songs?query={prefix}&limit=2&offset={offset}", Ct).Expect<List<AdminSongDto>>(HttpStatusCode.OK);
        var all = (await Page(0)).Concat(await Page(2)).Concat(await Page(4)).Select(s => s.Id).ToList();
        Assert.Equal(5, all.Distinct().Count());
        Assert.Empty(await Page(6));
        await admin.Client.GetAsync("/api/v1/admin/songs?offset=-1", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Eventos_se_paginan_con_offset_sin_repetir_ni_saltar()
    {
        var admin = await AdminAsync();
        // En 1990, antes que cualquier otro evento de la BD compartida: con includePast quedan al principio y los tests en
        // paralelo (que crean eventos futuros) no corren el offset. Misma hora para los 5: solo el id desempata.
        var at = new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await api.WithDbAsync(async db =>
        {
            for (var i = 0; i < 5; i++)
                db.Events.Add(new Event { Name = $"Paginado {i}", Venue = "Teatro", City = "Bogotá", StartsAt = at, ArtistLineup = ["Uno"] });
            await db.SaveChangesAsync(Ct);
        });

        async Task<List<AdminEventDto>> Page(int offset) => await admin.Client
            .GetAsync($"/api/v1/admin/events?includePast=true&limit=2&offset={offset}", Ct).Expect<List<AdminEventDto>>(HttpStatusCode.OK);
        var ids = (await Page(0)).Concat(await Page(2)).Concat(await Page(4)).Where(e => e.StartsAt == at).Select(e => e.Id).ToList();
        Assert.Equal(5, ids.Distinct().Count());
        Assert.DoesNotContain(await admin.Client.GetAsync("/api/v1/admin/events", Ct).Expect<List<AdminEventDto>>(HttpStatusCode.OK), e => e.StartsAt == at);
        await admin.Client.GetAsync("/api/v1/admin/events?limit=0", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Reportes_se_paginan_con_cursor_y_no_se_saltan_al_resolver()
    {
        var admin = await AdminAsync();
        var reporter = await api.CreateUserAsync();
        // Todos con el mismo createdAt, en un instante que ningún otro test usa: solo el id desempata y el cursor de partida
        // deja fuera la cola compartida.
        var at = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(Random.Shared.Next(1, 500_000));
        await api.WithDbAsync(async db =>
        {
            for (var i = 0; i < 5; i++)
            {
                var reported = new User { Email = $"p{Guid.NewGuid():N}@test.encorely.local", PasswordHash = "x", DisplayName = $"Reportado {i}" };
                db.Users.Add(reported);
                db.Reports.Add(new Report { ReporterId = reporter.Id, ReportedUserId = reported.Id, Reason = ReportReason.Spam, CreatedAt = at });
            }
            await db.SaveChangesAsync(Ct);
        });

        var start = at.AddTicks(-10);
        async Task<List<AdminReportDto>> Page(DateTimeOffset afterAt, Guid afterId) => await admin.Client
            .GetAsync($"/api/v1/admin/reports?limit=2&afterAt={Uri.EscapeDataString(afterAt.ToString("O"))}&afterId={afterId}", Ct)
            .Expect<List<AdminReportDto>>(HttpStatusCode.OK);

        var first = await Page(start, Guid.Empty);
        Assert.Equal(2, first.Count);
        // Resolver uno de la primera página no corre la cola: la siguiente sigue después del último recibido.
        await admin.Client.PostJson($"/api/v1/admin/reports/{first[0].Id}/resolve", new { action = "dismiss" }).Expect(HttpStatusCode.OK);
        var second = await Page(first[^1].CreatedAt, first[^1].Id);
        var third = await Page(second[^1].CreatedAt, second[^1].Id);
        var ids = first.Concat(second).Concat(third).Where(r => r.CreatedAt == at).Select(r => r.Id).ToList();
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(2, second.Count);
        Assert.Single(third);

        await admin.Client.GetAsync($"/api/v1/admin/reports?afterId={Guid.NewGuid()}", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
        await admin.Client.GetAsync("/api/v1/admin/reports?limit=201", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Reportes_filtra_por_status_sin_distinguir_mayusculas_y_explica_el_error_en_espanol()
    {
        var admin = await AdminAsync();
        foreach (var status in new[] { "open", "resolved", "dismissed", "Open", "RESOLVED" })
            await admin.Client.GetAsync($"/api/v1/admin/reports?status={status}", Ct).Expect<List<AdminReportDto>>(HttpStatusCode.OK);
        foreach (var status in new[] { "1", "closed" })
        {
            var problem = await admin.Client.GetAsync($"/api/v1/admin/reports?status={status}", Ct).ExpectProblem(HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal("Usa uno de estos valores: open, resolved, dismissed.", problem.GetProperty("errors").GetProperty("status")[0].GetString());
        }
    }

    [Fact]
    public async Task Resolver_el_mismo_reporte_a_la_vez_aplica_una_sola_accion()
    {
        var admin = await AdminAsync();
        var other = await AdminAsync();
        var reporter = await api.CreateUserAsync();
        var reported = await api.CreateUserAsync();
        var reportId = await api.WithDbAsync(async db =>
        {
            var report = new Report { ReporterId = reporter.Id, ReportedUserId = reported.Id, Reason = ReportReason.Harassment };
            db.Reports.Add(report);
            await db.SaveChangesAsync(Ct);
            return report.Id;
        });

        var results = await Task.WhenAll(
            admin.Client.PostJson($"/api/v1/admin/reports/{reportId}/resolve", new { action = "ban", note = "Acoso" }),
            other.Client.PostJson($"/api/v1/admin/reports/{reportId}/resolve", new { action = "dismiss" }));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], results.Select(r => r.StatusCode).Order());
        var winner = await results.Single(r => r.IsSuccessStatusCode).Read<AdminReportDto>();
        // La cuenta queda como dice la acción que ganó: nunca un ban con el reporte descartado.
        var status = await api.WithDbAsync(db => db.Users.Where(u => u.Id == reported.Id).Select(u => u.Status).SingleAsync(Ct));
        Assert.Equal(winner.Status == ReportStatus.Resolved ? UserStatus.Banned : UserStatus.Active, status);
    }

    [Fact]
    public async Task Admin_no_puede_suspender_a_otro_admin_ni_por_reporte()
    {
        var admin = await AdminAsync();
        var other = await AdminAsync();
        await admin.Client.PutJson($"/api/v1/admin/users/{other.Id}/status", new { status = "banned" })
            .ExpectProblem(HttpStatusCode.Forbidden, "cannot_change_admin_status");

        var reportId = await api.WithDbAsync(async db =>
        {
            var report = new Report { ReporterId = admin.Id, ReportedUserId = other.Id, Reason = ReportReason.Harassment };
            db.Reports.Add(report);
            await db.SaveChangesAsync(Ct);
            return report.Id;
        });
        await admin.Client.PostJson($"/api/v1/admin/reports/{reportId}/resolve", new { action = "ban" })
            .ExpectProblem(HttpStatusCode.Forbidden, "cannot_change_admin_status");
        // El rechazo revierte toda la resolución: el reporte sigue abierto.
        Assert.Equal(ReportStatus.Open, await api.WithDbAsync(db => db.Reports.Where(r => r.Id == reportId).Select(r => r.Status).SingleAsync(Ct)));
        await other.Client.GetAsync("/api/v1/me", Ct).Expect(HttpStatusCode.OK);
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
