using System.Buffers.Text;
using System.Text;

namespace Encorely.Api.Features.Profiles;

/// <summary>Posición en la lista de reacciones recibidas. Opaco para el cliente (base64url): no se arma ni se interpreta.</summary>
public sealed record ReactionCursor(DateTimeOffset At, Guid From, ReactionTargetKind Kind, string Key)
{
    public static string Format(ProfileReaction r)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{r.UpdatedAt.UtcTicks}|{r.FromUserId:N}|{(int)r.TargetKind}|{r.TargetKey}"));

    public static ReactionCursor? Parse(string cursor)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|', 4);
            if (parts.Length != 4 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParseExact(parts[1], "N", out var from)
                || !int.TryParse(parts[2], out var kind) || !Enum.IsDefined((ReactionTargetKind)kind)
                || ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
                return null;
            return new ReactionCursor(new DateTimeOffset(ticks, TimeSpan.Zero), from, (ReactionTargetKind)kind, parts[3]);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
