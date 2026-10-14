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

/// <summary>Guid obligatorio y distinto de 00000000-….</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not Guid g || g != Guid.Empty;

    public override string FormatErrorMessage(string name) => "Es obligatorio.";
}

public static class Text
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Recorta y convierte vacío en null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
