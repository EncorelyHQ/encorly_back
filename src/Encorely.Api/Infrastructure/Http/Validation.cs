using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Encorely.Api.Infrastructure.Http;

/// <summary>byte[] (base64 en JSON) con longitud exacta o en rango.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class ByteLengthAttribute(int min, int max) : ValidationAttribute
{
    public ByteLengthAttribute(int exact) : this(exact, exact) { }

    public int Min { get; } = min;
    public int Max { get; } = max;

    public override bool IsValid(object? value) => value is null || value is byte[] b && b.Length >= Min && b.Length <= Max;

    public override string FormatErrorMessage(string name) => Min == Max
        ? $"Debe medir exactamente {Min} bytes (base64)."
        : $"Debe medir entre {Min} y {Max} bytes (base64).";
}

/// <summary>BCrypt ignora lo que pase de 72 bytes: se rechaza en el borde en vez de truncar en silencio.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class MaxUtf8BytesAttribute(int max) : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string s || Encoding.UTF8.GetByteCount(s) <= max;

    public override string FormatErrorMessage(string name) => $"No puede superar {max} bytes.";
}

/// <summary>Largo máximo de cada texto de una lista (MaxLength sobre la lista solo cuenta elementos).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class ItemMaxLengthAttribute(int max) : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not IEnumerable<string?> items || items.All(i => i is null || i.Length <= max);

    public override string FormatErrorMessage(string name) => $"Cada elemento puede tener hasta {max} caracteres.";
}

/// <summary>Guid obligatorio y distinto de 00000000-….</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not Guid g || g != Guid.Empty;

    public override string FormatErrorMessage(string name) => "Es obligatorio.";
}

/// <summary>Una lista no puede traer null entre sus elementos ([null] rompería el servicio con un 500).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class NoNullItemsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null or string || value is not System.Collections.IEnumerable items) return true;
        foreach (var item in items)
            if (item is null) return false;
        return true;
    }

    public override string FormatErrorMessage(string name) => "No puede contener elementos vacíos.";
}

/// <summary>
/// Postgres no admite U+0000 en texto (22021): se rechaza en el borde de JSON con 400 en vez de dejar que llegue a la BD.
/// </summary>
public sealed class NoNulCharStringConverter : System.Text.Json.Serialization.JsonConverter<string>
{
    public override string? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is not null && value.Contains('\0'))
            throw new System.Text.Json.JsonException("El texto no puede contener el carácter nulo (U+0000).");
        return value;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, string value, System.Text.Json.JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

public static class Text
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Recorta y convierte vacío en null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
