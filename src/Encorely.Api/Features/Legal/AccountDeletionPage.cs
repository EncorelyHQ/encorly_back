using System.Net;
using System.Security.Cryptography;
using System.Text;
using Encorely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Encorely.Api.Features.Legal;

/// <summary>
/// Páginas públicas HTML (sin dependencias externas): borrado de cuenta (URL exigida por Google Play),
/// términos y política de tratamiento de datos. Cada una fija su propia CSP; el script y los estilos
/// inline se autorizan por hash, sin 'unsafe-inline'.
/// </summary>
public static class AccountDeletionPage
{
    public static void MapAccountDeletionPage(this IEndpointRouteBuilder app)
    {
        app.MapGet("/cuenta/eliminar", (HttpContext ctx) => Html(ctx, DeletionPage(), withScript: true))
            .AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/legal/terminos", (HttpContext ctx, IOptions<LegalOptions> legal) => Html(ctx, TermsPage(legal.Value), withScript: false))
            .AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/legal/privacidad", (HttpContext ctx, IOptions<LegalOptions> legal) => Html(ctx, PrivacyPage(legal.Value), withScript: false))
            .AllowAnonymous().ExcludeFromDescription();
    }

    private static IResult Html(HttpContext ctx, string html, bool withScript)
    {
        var script = withScript ? $" script-src '{Hash(DeletionScript)}';" : "";
        ctx.Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; style-src '{Hash(Css)}';{script} connect-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        ctx.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static string Hash(string content) => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static string Layout(string title, string body, bool withScript) => $$"""
        <!doctype html>
        <html lang="es">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex">
        <title>{{title}} · Encorely</title>
        <style>{{Css}}</style>
        </head>
        <body>
        <main>
        <p class="brand">Encorely</p>
        {{body}}
        <footer>Encorely es un proyecto académico. No es un servicio comercial.</footer>
        </main>
        {{(withScript ? $"<script>{DeletionScript}</script>" : "")}}
        </body>
        </html>
        """;

    private const string Css = """

        :root{color-scheme:light dark;--bg:#faf8f5;--fg:#1d1b20;--muted:#5c5866;--line:#d9d4cc;--accent:#7a2e8e;--danger:#a4160f;--card:#fff}
        @media (prefers-color-scheme:dark){:root{--bg:#16141a;--fg:#f1eef5;--muted:#b5afc0;--line:#3a3544;--accent:#d9a3ec;--danger:#ff8f86;--card:#201d27}}
        *{box-sizing:border-box}
        body{margin:0;background:var(--bg);color:var(--fg);font:1rem/1.65 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        main{max-width:42rem;margin:0 auto;padding:2rem 1rem 3rem}
        .brand{font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--accent);margin:0 0 1rem;font-size:.875rem}
        h1{font-size:1.75rem;line-height:1.2;margin:0 0 .5rem}
        h2{font-size:1.15rem;margin:2rem 0 .5rem}
        p,li{max-width:65ch}
        .meta,footer{color:var(--muted);font-size:.875rem}
        footer{margin-top:3rem;padding-top:1rem;border-top:1px solid var(--line)}
        ul{padding-left:1.25rem}
        form{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:1.25rem;margin-top:1.5rem}
        label{display:block;font-weight:600;margin:1rem 0 .25rem}
        label:first-child{margin-top:0}
        input[type=email],input[type=password]{width:100%;min-height:44px;padding:.5rem .75rem;font:inherit;color:var(--fg);background:var(--bg);border:1px solid var(--muted);border-radius:8px}
        input:focus-visible,button:focus-visible{outline:3px solid var(--accent);outline-offset:2px}
        .check{display:flex;gap:.6rem;align-items:flex-start;margin-top:1rem}
        .check input{width:1.25rem;height:1.25rem;margin-top:.3rem;flex:none}
        .check label{margin:0;font-weight:400}
        button{margin-top:1.25rem;min-height:44px;padding:.5rem 1.25rem;font:inherit;font-weight:600;color:#fff;background:var(--danger);border:0;border-radius:8px;cursor:pointer}
        @media (prefers-color-scheme:dark){button{color:#16141a}}
        button[disabled]{opacity:.6;cursor:progress}
        #msg{margin-top:1rem;padding:.75rem 1rem;border-radius:8px;border:1px solid var(--line)}
        #msg:empty{display:none}
        #msg.ok{border-color:var(--accent)}
        #msg.err{border-color:var(--danger)}
        """;

    private const string DeletionScript = """

        const form = document.getElementById('f');
        const msg = document.getElementById('msg');
        const btn = document.getElementById('go');
        function show(text, kind) { msg.textContent = text; msg.className = kind; }
        form.addEventListener('submit', async (e) => {
          e.preventDefault();
          btn.disabled = true;
          show('Procesando…', '');
          try {
            const res = await fetch('/api/v1/account-deletion', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ email: form.email.value, password: form.password.value }),
            });
            if (res.status === 204) { form.reset(); show('Tu cuenta y tus datos fueron eliminados. Esta acción no se puede deshacer.', 'ok'); }
            else if (res.status === 401) show('Correo o contraseña incorrectos.', 'err');
            else if (res.status === 429) show('Demasiados intentos. Espera un momento e inténtalo de nuevo.', 'err');
            else if (res.status === 400) show('Revisa que el correo y la contraseña estén bien escritos.', 'err');
            else show('No pudimos procesar la solicitud. Inténtalo más tarde.', 'err');
          } catch {
            show('No hay conexión con el servidor. Inténtalo más tarde.', 'err');
          } finally {
            form.password.value = '';
            btn.disabled = false;
          }
        });
        """;

    private static string DeletionPage() => Layout("Eliminar cuenta", """
        <h1>Eliminar mi cuenta</h1>
        <p>Desde aquí puedes borrar tu cuenta de Encorely sin necesidad de abrir la aplicación. También puedes hacerlo desde el perfil de la app.</p>
        <h2>Qué se elimina</h2>
        <ul>
          <li>Tu perfil: correo, nombre, fecha de nacimiento, ciudad, biografía y géneros.</li>
          <li>Tus canciones deslizadas, tus likes, tus eventos, tus artistas importados y las reacciones que dejaste y recibiste.</li>
          <li>Tus matches y los mensajes de esos chats, incluidos los que recibieron las otras personas.</li>
          <li>Tus llaves públicas de cifrado, tokens de sesión, dispositivos de notificaciones y registros de consentimiento.</li>
        </ul>
        <h2>Qué puede conservarse</h2>
        <ul>
          <li>Los reportes de moderación en los que participaste se conservan sin tu identidad vinculada, como evidencia de seguridad. El texto de los mensajes revelados y el detalle del reporte se borran 180 días después de resolverse el reporte, o de inmediato si la cuenta reportada es la que se elimina.</li>
        </ul>
        <p><strong>La eliminación es inmediata e irreversible.</strong> Las personas con las que tenías un match dejarán de verte.</p>
        <form id="f" autocomplete="on">
          <label for="email">Correo electrónico</label>
          <input id="email" name="email" type="email" autocomplete="email" required maxlength="254">
          <label for="password">Contraseña</label>
          <input id="password" name="password" type="password" autocomplete="current-password" required>
          <div class="check">
            <input id="sure" type="checkbox" required>
            <label for="sure">Entiendo que mi cuenta y mis datos se borrarán de forma permanente.</label>
          </div>
          <button id="go" type="submit">Eliminar mi cuenta</button>
          <div id="msg" role="status" aria-live="polite"></div>
        </form>
        <p class="meta">Más información en la <a href="/legal/privacidad">política de tratamiento de datos</a>.</p>
        """, withScript: true);

    private static string VersionLine(LegalOptions o)
        => $"<p class=\"meta\">Versión {WebUtility.HtmlEncode(o.ConsentVersion)} · Vigente desde {o.EffectiveDate:yyyy-MM-dd}</p>";

    private static string ContactSentence(LegalOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.PrivacyContactEmail))
            return "Para consultas o reclamos usa el contacto publicado en la ficha de la aplicación.";
        var email = WebUtility.HtmlEncode(o.PrivacyContactEmail);
        return $"Para consultas y reclamos sobre tus datos escribe a <a href=\"mailto:{email}\">{email}</a>.";
    }

    private static string TermsPage(LegalOptions o) => Layout("Términos y condiciones", $$"""
        <h1>Términos y condiciones</h1>
        {{VersionLine(o)}}
        <p>Encorely es un <strong>proyecto académico</strong> de una aplicación de citas basada en gustos musicales, dirigida a personas en Colombia. Al crear una cuenta aceptas estos términos.</p>
        <h2>1. Mayoría de edad</h2>
        <p>Solo pueden usar Encorely personas de 18 años o más. Si descubrimos que una cuenta pertenece a un menor de edad, será eliminada.</p>
        <h2>2. Tu cuenta</h2>
        <p>Eres responsable de la veracidad de tu información y de mantener en reserva tu contraseña. Una persona, una cuenta.</p>
        <h2>3. Conducta</h2>
        <p>No se permite acosar, suplantar, enviar spam, publicar contenido ilegal o inapropiado, ni usar la aplicación para fines distintos a conocer personas de forma respetuosa. Puedes bloquear y reportar a otros usuarios; los reportes son revisados por moderación y las cuentas que incumplan pueden ser suspendidas.</p>
        <h2>4. Chats</h2>
        <p>Los mensajes están cifrados de extremo a extremo: el servidor no puede leerlos. Si reportas a alguien, solo se revisan los mensajes que tú decidas revelar.</p>
        <h2>5. Encuentros en persona</h2>
        <p>Encorely no verifica la identidad de las personas ni se responsabiliza por los encuentros fuera de la aplicación. Toma precauciones: cita en lugares públicos y avisa a alguien de confianza.</p>
        <h2>6. Disponibilidad y responsabilidad</h2>
        <p>Al ser un proyecto académico, el servicio se ofrece «tal cual», sin garantías de disponibilidad, y puede modificarse o cerrarse.</p>
        <h2>7. Eliminación de la cuenta</h2>
        <p>Puedes eliminar tu cuenta en cualquier momento desde la app o en <a href="/cuenta/eliminar">/cuenta/eliminar</a>.</p>
        <h2>8. Datos personales</h2>
        <p>El tratamiento de tus datos se rige por la <a href="/legal/privacidad">política de tratamiento de datos</a> y la Ley 1581 de 2012.</p>
        <h2>9. Ley aplicable</h2>
        <p>Estos términos se rigen por las leyes de la República de Colombia.</p>
        """, withScript: false);

    private static string PrivacyPage(LegalOptions o) => Layout("Política de tratamiento de datos", $$"""
        <h1>Política de tratamiento de datos personales</h1>
        {{VersionLine(o)}}
        <p>Esta política se expide en cumplimiento de la Ley 1581 de 2012 y sus decretos reglamentarios (régimen de protección de datos personales de Colombia). Encorely es un <strong>proyecto académico</strong>.</p>
        <h2>1. Responsable del tratamiento</h2>
        <p>El equipo del proyecto Encorely, responsable de la aplicación y de este servicio. {{ContactSentence(o)}}</p>
        <h2>2. Datos que tratamos</h2>
        <ul>
          <li>Cuenta: correo electrónico, nombre para mostrar y contraseña (guardada únicamente como hash, nunca en claro).</li>
          <li>Perfil: fecha de nacimiento, ciudad, biografía y géneros musicales favoritos.</li>
          <li>Uso: canciones que te gustan o descartas, eventos a los que dices asistir, likes, matches, reacciones que dejas y recibes, bloqueos y reportes.</li>
          <li>Spotify, si lo conectas: tus artistas más escuchados (nombre, imagen y géneros) y, si entras con Spotify, tu identificador de Spotify y el correo de esa cuenta. No guardamos tus tokens de Spotify.</li>
          <li>Técnicos: tokens de sesión, token de notificaciones push del dispositivo y registros de consentimiento (versión y fecha).</li>
          <li>Chats: los mensajes están <strong>cifrados de extremo a extremo</strong>. El servidor solo almacena texto cifrado y metadatos (quién, cuándo); <strong>no puede leer el contenido</strong>.</li>
        </ul>
        <p>No tratamos datos de menores de edad ni datos sensibles de forma deliberada.</p>
        <h2>3. Finalidades</h2>
        <ul>
          <li>Crear y administrar tu cuenta y autenticarte.</li>
          <li>Calcular tu afinidad musical y sugerirte personas compatibles.</li>
          <li><strong>Perfil público:</strong> mostrar a cualquier otra persona usuaria de Encorely tu nombre, edad, ciudad, biografía, géneros, artistas y estadísticas (canciones calificadas, géneros explorados y eventos marcados), y tu nombre junto a las reacciones que dejes en otros perfiles. Puedes apagarlo cuando quieras en tu perfil («Mostrar mi perfil a otros»): apagado, nadie más lo ve ni puede reaccionarle, y tus reacciones dejan de mostrarse. Quien te bloquea o a quien bloqueas no ve tu perfil.</li>
          <li>Permitir el chat entre personas con match y enviarte avisos (nuevo match, nuevo mensaje).</li>
          <li>Moderación y seguridad: atender reportes, bloqueos y suspender cuentas que incumplan los términos.</li>
          <li>Cumplir obligaciones legales y acreditar tu consentimiento.</li>
        </ul>
        <p>No vendemos tus datos ni los usamos para publicidad de terceros.</p>
        <h2>4. Mayoría de edad</h2>
        <p>El servicio es solo para personas de 18 años o más.</p>
        <h2>5. Tus derechos como titular</h2>
        <p>Tienes derecho a conocer, actualizar y rectificar tus datos; a solicitar prueba de la autorización otorgada; a ser informado del uso que se les da; a presentar quejas ante la Superintendencia de Industria y Comercio; a revocar la autorización y a solicitar la supresión de tus datos; y a acceder gratuitamente a ellos.</p>
        <h2>6. Cómo ejercer tus derechos</h2>
        <ul>
          <li><strong>Acceso y copia:</strong> en la app, opción «Exportar mis datos» del perfil. Incluye tus datos, no el contenido de los mensajes (que no podemos leer).</li>
          <li><strong>Rectificación:</strong> edita tu perfil en la app.</li>
          <li><strong>Supresión y revocatoria:</strong> elimina tu cuenta desde la app o en <a href="/cuenta/eliminar">/cuenta/eliminar</a>. El borrado es inmediato e incluye tus mensajes. Los reportes de moderación se conservan sin tu identidad vinculada (ver «Conservación»).</li>
        </ul>
        <h2>7. Seguridad</h2>
        <p>Usamos conexiones cifradas, contraseñas con hash, sesiones con tokens de corta duración y cifrado de extremo a extremo en el chat.</p>
        <h2>8. Conservación</h2>
        <p>Conservamos tus datos mientras tengas la cuenta activa. Al eliminarla, se suprimen de forma inmediata. Los mensajes cifrados se borran del servidor cuando tu teléfono los descarga, o a los 30 días si nadie los descargó. Excepción: un reporte de moderación se conserva sin tu identidad vinculada, y el texto de los mensajes revelados y el detalle del reporte se borran 180 días después de resolverse (o en cuanto se elimina la cuenta reportada).</p>
        <h2>9. Cambios</h2>
        <p>Si esta política cambia, publicaremos una nueva versión y te pediremos aceptarla de nuevo en la app.</p>
        """, withScript: false);
}
