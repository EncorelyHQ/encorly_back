using System.ComponentModel.DataAnnotations;

namespace Encorely.Api.Infrastructure.Http;

/// <summary>
/// <see cref="AllowedValuesAttribute"/> sin distinguir mayúsculas y con mensaje en español. Hereda de él para que el
/// OpenAPI siga publicando los valores como enum; el original compara con Equals y rechazaba "Open" aunque el handler
/// lo parsea con ignoreCase.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AllowedValuesIgnoreCaseAttribute : AllowedValuesAttribute
{
    public AllowedValuesIgnoreCaseAttribute(params string[] values) : base(values)
        => ErrorMessage = "Usa uno de estos valores: " + string.Join(", ", values) + ".";

    public override bool IsValid(object? value) => value is string text
        ? Values.Any(v => string.Equals(v as string, text, StringComparison.OrdinalIgnoreCase))
        : base.IsValid(value);
}
