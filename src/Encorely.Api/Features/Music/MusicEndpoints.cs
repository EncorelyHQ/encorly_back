using System.ComponentModel;
using System.Security.Claims;
using Encorely.Api.Infrastructure.Auth;
using Encorely.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Encorely.Api.Features.Music;

public static class MusicEndpoints
{
    public static void MapMusic(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("").WithTags("Música");

        g.MapGet("/genres", GetGenres).WithName("GetGenres").WithSummary("Catálogo de géneros").AllowAnonymous();
        g.MapGet("/songs/feed", GetFeed).WithName("GetSongFeed")
            .WithSummary("Canciones aún no calificadas por mí, mezclando mis géneros y exploración").Problems(400, 401);
        g.MapPost("/swipes", PostSwipe).WithName("Swipe")
            .WithSummary("Califica una canción (like/dislike). Repetir la misma canción no suma").Problems(400, 401, 404).UserWrites();
        g.MapGet("/me/dna", GetDna).WithName("GetMyDna").WithSummary("Mi ADN musical (vector de géneros) y estado del radar").Problems(401);
        g.MapPost("/me/spotify/import", ImportSpotify).WithName("ImportSpotify")
            .WithSummary("Importa top artistas de Spotify (leídos en el dispositivo) para enriquecer el ADN; avisa los géneros que no mapean").Problems(400, 401).UserWrites();
    }

    private static async Task<Ok<IReadOnlyList<GenreDto>>> GetGenres(MusicService music, CancellationToken ct)
        => TypedResults.Ok(await music.GetGenresAsync(ct));

    private static async Task<Ok<IReadOnlyList<SongDto>>> GetFeed(
        ClaimsPrincipal user, MusicService music, CancellationToken ct, [Description("1–50, por defecto 20")] int limit = 20)
        => TypedResults.Ok(await music.GetFeedAsync(user.UserId(), limit, ct));

    private static async Task<Ok<SwipeResponse>> PostSwipe(SwipeRequest request, ClaimsPrincipal user, MusicService music, CancellationToken ct)
        => TypedResults.Ok(await music.SwipeAsync(user.UserId(), request, ct));

    private static async Task<Ok<DnaResponse>> GetDna(ClaimsPrincipal user, MusicService music, CancellationToken ct)
        => TypedResults.Ok(await music.GetDnaAsync(user.UserId(), ct));

    private static async Task<Ok<SpotifyImportResponse>> ImportSpotify(SpotifyImportRequest request, ClaimsPrincipal user, MusicService music, CancellationToken ct)
        => TypedResults.Ok(await music.ImportSpotifyAsync(user.UserId(), request, ct));
}
