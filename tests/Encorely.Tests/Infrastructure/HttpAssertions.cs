using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Encorely.Tests.Infrastructure;

public static class Http
{
    /// <summary>Mismas opciones que la API: camelCase y enums como string camelCase.</summary>
    public static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return o;
    }

    public static Task<HttpResponseMessage> PostJson<T>(this HttpClient c, string url, T body) => c.PostAsJsonAsync(url, body, Json);
    public static Task<HttpResponseMessage> PutJson<T>(this HttpClient c, string url, T body) => c.PutAsJsonAsync(url, body, Json);
    public static Task<HttpResponseMessage> PatchJson<T>(this HttpClient c, string url, T body) => c.PatchAsJsonAsync(url, body, Json);

    public static async Task<T> Read<T>(this HttpResponseMessage r)
        => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Verifica el status y, si falla, muestra el cuerpo para diagnosticar.</summary>
    public static async Task<HttpResponseMessage> Expect(this Task<HttpResponseMessage> call, HttpStatusCode status)
    {
        var r = await call;
        if (r.StatusCode != status)
            Assert.Fail($"Se esperaba {(int)status} y llegó {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return r;
    }

    public static async Task<T> Expect<T>(this Task<HttpResponseMessage> call, HttpStatusCode status)
        => await (await call.Expect(status)).Read<T>();

    /// <summary>ProblemDetails con el code estable esperado.</summary>
    public static async Task<JsonElement> ExpectProblem(this Task<HttpResponseMessage> call, HttpStatusCode status, string? code = null)
    {
        var r = await call.Expect(status);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (code is not null) Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }
}
