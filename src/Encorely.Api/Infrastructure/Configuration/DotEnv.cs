namespace Encorely.Api.Infrastructure.Configuration;

/// <summary>
/// Carga un archivo <c>.env</c> (KEY=VALUE) para desarrollo local. No pisa variables ya definidas,
/// así docker/CI siempre ganan sobre el archivo.
/// </summary>
public static class DotEnv
{
    public static void LoadFromNearest(string startDirectory, string fileName = ".env")
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
            {
                Load(candidate);
                return;
            }
            dir = dir.Parent;
        }
    }

    public static void Load(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
                value = value[1..^1];

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
