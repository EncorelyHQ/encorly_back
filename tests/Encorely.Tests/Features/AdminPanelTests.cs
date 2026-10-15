using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Encorely.Tests.Infrastructure;

namespace Encorely.Tests.Features;

public sealed partial class AdminPanelTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Sha256(string content) => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    [GeneratedRegex("<style>(.*?)</style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();

    [GeneratedRegex("<script>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex ScriptBlock();

    [Fact]
    public async Task Admin_sirve_html_con_csp_por_hash_y_no_store()
    {
        var res = await api.Anonymous().GetAsync("/admin", Ct);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
        var html = await res.Content.ReadAsStringAsync(Ct);

        var csp = string.Join(' ', res.Headers.GetValues("Content-Security-Policy"));
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.Contains("default-src 'none'", csp);
        Assert.Contains("connect-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("base-uri 'none'", csp);
        Assert.Contains($"script-src '{Sha256(ScriptBlock().Match(html).Groups[1].Value)}'", csp);
        Assert.Contains($"style-src '{Sha256(StyleBlock().Match(html).Groups[1].Value)}'", csp);
        Assert.Contains("no-store", res.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Admin_no_guarda_tokens_en_el_navegador_ni_usa_innerHTML_y_trae_las_tres_secciones()
    {
        var html = await api.Anonymous().GetStringAsync("/admin", Ct);

        Assert.DoesNotContain("localStorage", html);
        Assert.DoesNotContain("sessionStorage", html);
        Assert.DoesNotContain("innerHTML", html);
        Assert.DoesNotContain("document.cookie", html);
        Assert.DoesNotContain("confirm(", html);
        Assert.Contains("id=\"sec-events\"", html);
        Assert.Contains("id=\"sec-songs\"", html);
        Assert.Contains("id=\"sec-reports\"", html);
        Assert.Contains("Tu cuenta no tiene permisos de administración.", html);
    }

    [Fact]
    public async Task Suspender_pide_confirmacion_y_el_panel_permite_reactivar()
    {
        var html = await api.Anonymous().GetStringAsync("/admin", Ct);

        // El botón de la tarjeta solo abre el paso de confirmación; el ban sale de "Sí, suspender".
        Assert.Contains("btn('Suspender cuenta', () => confirmIn(", html);
        Assert.Contains("'Sí, suspender', act('ban')", html);
        Assert.Contains("los matches no se recuperan al reactivarla", html);
        Assert.Contains("'/admin/users/' + r.reported.id + '/status', { status: 'active' }", html);
    }
}
