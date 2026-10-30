using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Encorely.Tests.Infrastructure;

/// <summary>Entorno de host con nombre fijo, para probar validadores de opciones por entorno.</summary>
public sealed class TestEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Encorely.Api";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
