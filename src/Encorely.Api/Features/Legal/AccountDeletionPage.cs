namespace Encorely.Api.Features.Legal;

public static class AccountDeletionPage
{
    /// <summary>Página pública /cuenta/eliminar (URL de borrado exigida por Google Play).</summary>
    public static void MapAccountDeletionPage(this IEndpointRouteBuilder app)
        => app.MapGet("/cuenta/eliminar", () => Results.Content("<!doctype html><title>Eliminar cuenta · Encorely</title>", "text/html; charset=utf-8"))
            .AllowAnonymous().ExcludeFromDescription();
}
