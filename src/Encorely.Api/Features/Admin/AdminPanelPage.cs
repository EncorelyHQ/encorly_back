using System.Security.Cryptography;
using System.Text;

namespace Encorely.Api.Features.Admin;

/// <summary>
/// Panel web de administración (GET /admin): una sola página autocontenida, sin dependencias externas.
/// La página es pública; los datos los protege la API con el JWT de rol admin. Los tokens viven solo
/// en memoria del script. La CSP autoriza el script y los estilos inline por hash (sin 'unsafe-inline').
/// </summary>
public static class AdminPanelPage
{
    private static readonly string Document = BuildDocument();
    private static readonly string ContentSecurityPolicy =
        $"default-src 'none'; script-src '{Hash(Script)}'; style-src '{Hash(Css)}'; connect-src 'self'; " +
        "img-src 'self' https: data:; media-src https:; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";

    public static IEndpointRouteBuilder MapAdminPanel(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin", (HttpContext ctx) =>
        {
            ctx.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Content(Document, "text/html; charset=utf-8");
        }).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    private static string Hash(string content) => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static string BuildDocument() =>
        "<!doctype html>\n<html lang=\"es\">\n<head>\n<meta charset=\"utf-8\">\n" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<meta name=\"robots\" content=\"noindex\">\n" +
        "<title>Administración · Encorely</title>\n<style>" + Css + "</style>\n</head>\n<body>\n" + Body +
        "\n<script>" + Script + "</script>\n</body>\n</html>\n";

    private const string Css = """

        :root{color-scheme:light dark;--bg:#f6f4f0;--fg:#1c1b1f;--muted:#55515c;--line:#d6d1c8;--accent:#3d2a52;--accent-fg:#fff;--danger:#a4160f;--ok:#17613a;--card:#fff;--head:#1c1b1f;--head-fg:#f6f4f0}
        @media (prefers-color-scheme:dark){:root{--bg:#141317;--fg:#eeebf2;--muted:#b3adbd;--line:#3a3642;--accent:#cdb4e6;--accent-fg:#141317;--danger:#ff9a91;--ok:#7fd6a2;--card:#1d1b22;--head:#0d0c0f;--head-fg:#eeebf2}}
        *{box-sizing:border-box}
        [hidden]{display:none!important}
        body{margin:0;background:var(--bg);color:var(--fg);font:1rem/1.5 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        header{background:var(--head);color:var(--head-fg);display:flex;align-items:center;justify-content:space-between;gap:1rem;padding:.75rem 1rem}
        .brand{font-weight:700;letter-spacing:.08em;text-transform:uppercase;font-size:.875rem;margin:0}
        main{max-width:72rem;margin:0 auto;padding:1.5rem 1rem 3rem}
        h1{font-size:1.5rem;margin:0 0 1rem}
        h2{font-size:1.15rem;margin:0 0 .75rem}
        h3{font-size:1rem;margin:1rem 0 .25rem}
        .card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:1rem;margin-bottom:1rem}
        .login{max-width:26rem;margin:2rem auto}
        label{display:block;font-weight:600;margin:.75rem 0 .25rem}
        input:not([type=checkbox]),select,textarea{width:100%;min-height:44px;padding:.5rem .75rem;font:inherit;color:var(--fg);background:var(--bg);border:1px solid var(--muted);border-radius:6px}
        textarea{min-height:5.5rem;resize:vertical}
        input[type=checkbox]{width:1.2rem;height:1.2rem;margin:0}
        :focus-visible{outline:3px solid var(--accent);outline-offset:2px}
        header :focus-visible{outline-color:var(--head-fg)}
        .check{display:flex;align-items:center;gap:.6rem;margin-top:1rem}
        .check label{margin:0;font-weight:400}
        button{min-height:44px;padding:.4rem 1rem;font:inherit;font-weight:600;color:var(--accent-fg);background:var(--accent);border:1px solid var(--accent);border-radius:6px;cursor:pointer}
        button.secondary{background:transparent;color:var(--fg);border-color:var(--muted)}
        button.danger{background:var(--danger);border-color:var(--danger);color:#fff}
        @media (prefers-color-scheme:dark){button.danger{color:#141317}}
        header button.secondary{color:var(--head-fg);border-color:var(--head-fg)}
        button[disabled]{opacity:.6;cursor:progress}
        .row{display:flex;flex-wrap:wrap;gap:.5rem;align-items:flex-end}
        .row>*{flex:1 1 12rem}
        .row>button{flex:0 0 auto}
        .actions{display:flex;flex-wrap:wrap;gap:.5rem;margin-top:1rem}
        .actions>p{flex:1 0 100%;margin:0}
        .grid{display:grid;gap:0 1rem;grid-template-columns:repeat(auto-fit,minmax(15rem,1fr))}
        [role=tablist]{display:flex;flex-wrap:wrap;gap:.25rem;border-bottom:1px solid var(--line);margin-bottom:1rem}
        [role=tab]{background:transparent;color:var(--fg);border:1px solid transparent;border-bottom:0;border-radius:6px 6px 0 0}
        [role=tab][aria-selected=true]{background:var(--card);border-color:var(--line);color:var(--accent);box-shadow:inset 0 -3px 0 var(--accent)}
        .table-wrap{overflow-x:auto}
        table{width:100%;border-collapse:collapse;font-size:.9375rem}
        th,td{text-align:left;padding:.5rem .6rem;border-bottom:1px solid var(--line);vertical-align:top}
        th{color:var(--muted);font-weight:600;white-space:nowrap}
        td button{min-height:36px;padding:.2rem .7rem;margin:0 .25rem .25rem 0}
        .muted{color:var(--muted);font-size:.875rem}
        .msg{margin:.75rem 0;padding:.6rem .9rem;border-radius:6px;border:1px solid var(--line)}
        .msg:empty{display:none}
        .msg.err{border-color:var(--danger);color:var(--danger)}
        .msg.ok{border-color:var(--ok);color:var(--ok)}
        .msg ul{margin:.25rem 0 0;padding-left:1.25rem}
        .tag{display:inline-block;padding:0 .5rem;border:1px solid var(--muted);border-radius:99px;font-size:.8125rem;white-space:nowrap}
        .tag.good{border-color:var(--ok);color:var(--ok)}
        .tag.bad{border-color:var(--danger);color:var(--danger)}
        .thumb{width:48px;height:48px;object-fit:cover;border-radius:4px;background:var(--line)}
        blockquote{margin:.4rem 0;padding:.4rem .75rem;border-left:3px solid var(--line);white-space:pre-wrap;overflow-wrap:anywhere}
        audio{width:100%;margin-top:.5rem}
        .itunes{list-style:none;margin:.5rem 0 0;padding:0}
        .itunes li{display:flex;gap:.75rem;align-items:center;padding:.4rem 0;border-bottom:1px solid var(--line)}
        .itunes .info{flex:1;min-width:0;overflow-wrap:anywhere}
        @media (prefers-reduced-motion:reduce){*{scroll-behavior:auto!important;transition:none!important;animation:none!important}}
        """;

    private const string Body = """
        <header>
        <p class="brand">Encorely · Administración</p>
        <button id="logout" class="secondary" type="button" hidden>Cerrar sesión</button>
        </header>
        <main>
        <section id="login-view" class="card login" aria-labelledby="login-title">
        <h1 id="login-title">Iniciar sesión</h1>
        <form id="login-form" autocomplete="on">
        <label for="login-email">Correo electrónico</label>
        <input id="login-email" name="email" type="email" autocomplete="username" required maxlength="254">
        <label for="login-password">Contraseña</label>
        <input id="login-password" name="password" type="password" autocomplete="current-password" required>
        <div class="actions"><button id="login-go" type="submit">Entrar</button></div>
        <div id="login-msg" class="msg" role="alert"></div>
        </form>
        </section>
        <div id="app-view" hidden>
        <div id="denied" class="msg err" role="alert" hidden>Tu cuenta no tiene permisos de administración.</div>
        <div id="panel">
        <div role="tablist" aria-label="Secciones">
        <button role="tab" id="tab-events" aria-controls="sec-events" aria-selected="true" type="button">Eventos</button>
        <button role="tab" id="tab-songs" aria-controls="sec-songs" aria-selected="false" tabindex="-1" type="button">Canciones</button>
        <button role="tab" id="tab-reports" aria-controls="sec-reports" aria-selected="false" tabindex="-1" type="button">Reportes</button>
        </div>

        <section id="sec-events" role="tabpanel" aria-labelledby="tab-events">
        <h1>Eventos</h1>
        <div class="card row">
        <div><label for="ev-filter">Mostrar</label>
        <select id="ev-filter"><option value="upcoming">Próximos</option><option value="all">Próximos y pasados</option></select></div>
        <button id="ev-new" type="button">Nuevo evento</button>
        </div>
        <div id="ev-list-msg" class="msg" role="status"></div>
        <div class="card table-wrap"><table>
        <thead><tr><th scope="col">Nombre</th><th scope="col">Fecha (Colombia)</th><th scope="col">Lugar</th><th scope="col">Estado</th><th scope="col">Asistentes</th><th scope="col">Acciones</th></tr></thead>
        <tbody id="ev-body"></tbody></table></div>
        <form id="ev-form" class="card" hidden novalidate>
        <h2 id="ev-form-title">Nuevo evento</h2>
        <div class="grid">
        <div><label for="ev-name">Nombre</label><input id="ev-name" required maxlength="200"></div>
        <div><label for="ev-lineup">Lineup (separado por comas)</label><input id="ev-lineup" required></div>
        <div><label for="ev-venue">Lugar</label><input id="ev-venue" required maxlength="200"></div>
        <div><label for="ev-city">Ciudad</label><input id="ev-city" required maxlength="80"></div>
        <div><label for="ev-starts">Fecha y hora (hora de Colombia)</label><input id="ev-starts" type="datetime-local" required></div>
        <div><label for="ev-image">Imagen (URL, opcional)</label><input id="ev-image" type="url" maxlength="500"></div>
        </div>
        <label for="ev-desc">Descripción</label><textarea id="ev-desc" maxlength="2000"></textarea>
        <div class="check"><input id="ev-pub" type="checkbox" checked><label for="ev-pub">Publicado</label></div>
        <div id="ev-msg" class="msg" role="alert"></div>
        <div class="actions"><button id="ev-save" type="submit">Guardar evento</button><button id="ev-cancel" class="secondary" type="button">Cancelar</button></div>
        </form>
        </section>

        <section id="sec-songs" role="tabpanel" aria-labelledby="tab-songs" hidden>
        <h1>Canciones</h1>
        <form id="sg-search" class="card row">
        <div><label for="sg-q">Buscar por título o artista</label><input id="sg-q" type="search"></div>
        <div><label for="sg-genre-filter">Género</label><select id="sg-genre-filter"><option value="">Todos</option></select></div>
        <button type="submit">Buscar</button>
        <button id="sg-new" class="secondary" type="button">Nueva canción</button>
        </form>
        <div id="sg-list-msg" class="msg" role="status"></div>
        <div class="card table-wrap"><table>
        <thead><tr><th scope="col">Portada</th><th scope="col">Título</th><th scope="col">Artista</th><th scope="col">Género</th><th scope="col">Likes / Dislikes</th><th scope="col">Estado</th><th scope="col">Acciones</th></tr></thead>
        <tbody id="sg-body"></tbody></table></div>
        <div id="sg-panel" class="card" hidden>
        <h2>Buscar en iTunes</h2>
        <form id="it-form" class="row">
        <div><label for="it-term">Artista y/o título</label><input id="it-term" type="search" required></div>
        <button type="submit">Buscar en iTunes</button>
        </form>
        <div id="it-msg" class="msg" role="status"></div>
        <ul id="it-list" class="itunes"></ul>
        <form id="sg-form" novalidate>
        <h2 id="sg-form-title">Nueva canción</h2>
        <div class="grid">
        <div><label for="sg-title">Título</label><input id="sg-title" required maxlength="200"></div>
        <div><label for="sg-artist">Artista</label><input id="sg-artist" required maxlength="200"></div>
        <div><label for="sg-genre">Género</label><select id="sg-genre" required></select></div>
        <div><label for="sg-cover">Portada (URL)</label><input id="sg-cover" type="url" required maxlength="500"></div>
        <div><label for="sg-preview">Preview (URL, opcional)</label><input id="sg-preview" type="url" maxlength="500"></div>
        <div><label for="sg-itunes">iTunesTrackId (opcional)</label><input id="sg-itunes" inputmode="numeric" pattern="[0-9]*"></div>
        </div>
        <audio id="sg-audio" controls preload="none" hidden></audio>
        <div class="check"><input id="sg-active" type="checkbox" checked><label for="sg-active">Activa (aparece en el feed)</label></div>
        <div id="sg-msg" class="msg" role="alert"></div>
        <div class="actions"><button id="sg-save" type="submit">Guardar canción</button><button id="sg-cancel" class="secondary" type="button">Cancelar</button></div>
        </form>
        </div>
        </section>

        <section id="sec-reports" role="tabpanel" aria-labelledby="tab-reports" hidden>
        <h1>Reportes</h1>
        <div class="card row">
        <div><label for="rp-status">Estado</label>
        <select id="rp-status"><option value="open">Abiertos</option><option value="resolved">Resueltos</option><option value="dismissed">Descartados</option></select></div>
        </div>
        <div id="rp-msg" class="msg" role="status"></div>
        <div id="rp-list"></div>
        </section>
        </div>
        </div>
        </main>
        """;

    private const string Script = """

        'use strict';
        const API = '/api/v1';
        const COT = '-05:00';
        let accessToken = null;
        let refreshToken = null;
        let refreshing = null;
        let events = [];
        let songs = [];
        let genres = [];
        let editingEvent = null;
        let editingSong = null;

        const $ = (id) => document.getElementById(id);

        function el(tag, props, children) {
          const node = document.createElement(tag);
          if (props) {
            for (const key of Object.keys(props)) {
              if (key === 'text') node.textContent = props[key];
              else if (key === 'class') node.className = props[key];
              else if (key.startsWith('on')) node.addEventListener(key.slice(2), props[key]);
              else if (props[key] === true) node.setAttribute(key, '');
              else if (props[key] !== false && props[key] != null) node.setAttribute(key, props[key]);
            }
          }
          for (const child of children || []) node.append(child);
          return node;
        }

        function setMsg(node, text, kind) {
          node.className = 'msg' + (kind ? ' ' + kind : '');
          node.replaceChildren(text ? document.createTextNode(text) : '');
        }

        function showProblem(node, problem, fallback) {
          node.className = 'msg err';
          const title = (problem && problem.title) || fallback || 'No se pudo completar la operación.';
          const parts = [document.createTextNode(title)];
          const errors = problem && problem.errors;
          if (errors && typeof errors === 'object') {
            const list = el('ul');
            for (const field of Object.keys(errors)) {
              const msgs = Array.isArray(errors[field]) ? errors[field] : [errors[field]];
              list.append(el('li', { text: field + ': ' + msgs.join(' ') }));
            }
            if (list.childNodes.length) parts.push(list);
          }
          node.replaceChildren(...parts);
        }

        class ApiError extends Error {
          constructor(status, problem) { super((problem && problem.title) || 'Error ' + status); this.status = status; this.problem = problem; }
        }

        async function doRefresh() {
          if (!refreshToken) return false;
          if (!refreshing) {
            refreshing = (async () => {
              try {
                const res = await fetch(API + '/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken }) });
                if (!res.ok) return false;
                const data = await res.json();
                accessToken = data.accessToken;
                refreshToken = data.refreshToken;
                return true;
              } catch { return false; }
              finally { refreshing = null; }
            })();
          }
          return refreshing;
        }

        async function call(method, path, body, retry) {
          const headers = {};
          if (body !== undefined) headers['Content-Type'] = 'application/json';
          if (accessToken) headers['Authorization'] = 'Bearer ' + accessToken;
          let res;
          try {
            res = await fetch(API + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
          } catch {
            throw new ApiError(0, { title: 'No hay conexión con el servidor.' });
          }
          if (res.status === 401 && retry !== false) {
            if (await doRefresh()) return call(method, path, body, false);
            endSession();
            throw new ApiError(401, { title: 'Tu sesión expiró. Inicia sesión de nuevo.' });
          }
          if (res.status === 204) return null;
          let data = null;
          try { data = await res.json(); } catch { data = null; }
          if (!res.ok) throw new ApiError(res.status, data);
          return data;
        }

        // ---------- sesión ----------
        function endSession() {
          accessToken = null;
          refreshToken = null;
          $('app-view').hidden = true;
          $('login-view').hidden = false;
          $('logout').hidden = true;
          $('login-password').value = '';
        }

        $('login-form').addEventListener('submit', async (e) => {
          e.preventDefault();
          const btn = $('login-go');
          const msg = $('login-msg');
          btn.disabled = true;
          setMsg(msg, 'Ingresando…', '');
          try {
            const res = await fetch(API + '/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ email: $('login-email').value, password: $('login-password').value }) });
            const data = await res.json().catch(() => null);
            if (!res.ok) { showProblem(msg, data, res.status === 401 ? 'Correo o contraseña incorrectos.' : 'No se pudo iniciar sesión.'); return; }
            accessToken = data.accessToken;
            refreshToken = data.refreshToken;
            $('login-password').value = '';
            setMsg(msg, '', '');
            $('login-view').hidden = true;
            $('app-view').hidden = false;
            $('logout').hidden = false;
            await enterPanel();
          } catch {
            setMsg(msg, 'No hay conexión con el servidor.', 'err');
          } finally {
            btn.disabled = false;
          }
        });

        $('logout').addEventListener('click', async () => {
          const token = refreshToken;
          const bearer = accessToken;
          endSession();
          if (!token) return;
          try {
            await fetch(API + '/auth/logout', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + bearer }, body: JSON.stringify({ refreshToken: token }) });
          } catch { /* la sesión local ya se cerró */ }
        });

        async function enterPanel() {
          $('denied').hidden = true;
          $('panel').hidden = false;
          try {
            await loadEvents();
          } catch (err) {
            if (err.status === 403) { $('panel').hidden = true; $('denied').hidden = false; }
            return;
          }
          loadGenres();
        }

        // ---------- pestañas ----------
        const tabs = ['events', 'songs', 'reports'];
        function selectTab(name, focus) {
          for (const t of tabs) {
            const on = t === name;
            const tab = $('tab-' + t);
            tab.setAttribute('aria-selected', on ? 'true' : 'false');
            tab.tabIndex = on ? 0 : -1;
            $('sec-' + t).hidden = !on;
            if (on && focus) tab.focus();
          }
          if (name === 'songs' && !songs.length) loadSongs();
          if (name === 'reports') loadReports();
        }
        for (const t of tabs) {
          $('tab-' + t).addEventListener('click', () => selectTab(t, false));
          $('tab-' + t).addEventListener('keydown', (e) => {
            const i = tabs.indexOf(t);
            if (e.key === 'ArrowRight') { e.preventDefault(); selectTab(tabs[(i + 1) % tabs.length], true); }
            else if (e.key === 'ArrowLeft') { e.preventDefault(); selectTab(tabs[(i + tabs.length - 1) % tabs.length], true); }
            else if (e.key === 'Home') { e.preventDefault(); selectTab(tabs[0], true); }
            else if (e.key === 'End') { e.preventDefault(); selectTab(tabs[tabs.length - 1], true); }
          });
        }

        // ---------- utilidades ----------
        const fmt = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'America/Bogota' });
        function toLocalInput(iso) {
          return new Date(new Date(iso).getTime() - 5 * 3600 * 1000).toISOString().slice(0, 16);
        }
        function fromLocalInput(value) {
          return value.length === 16 ? value + ':00' + COT : value + COT;
        }
        function tag(text, kind) { return el('span', { class: 'tag' + (kind ? ' ' + kind : ''), text }); }
        function btn(text, onclick, cls) { return el('button', { type: 'button', class: cls || 'secondary', text, onclick }); }

        // ---------- eventos ----------
        async function loadEvents() {
          const msg = $('ev-list-msg');
          const includePast = $('ev-filter').value === 'all';
          try {
            events = await call('GET', '/admin/events?includePast=' + includePast);
            setMsg(msg, '', '');
            renderEvents();
          } catch (err) {
            if (err.status !== 403) showProblem(msg, err.problem, err.message);
            throw err;
          }
        }
        $('ev-filter').addEventListener('change', () => loadEvents().catch(() => {}));

        function renderEvents() {
          const body = $('ev-body');
          body.replaceChildren();
          if (!events.length) body.append(el('tr', {}, [el('td', { colspan: 6, class: 'muted', text: 'No hay eventos.' })]));
          for (const ev of events) {
            const actions = el('td');
            const showActions = () => {
              actions.replaceChildren(btn('Editar', () => openEventForm(ev)), btn('Borrar', askDelete, 'danger'));
            };
            const askDelete = () => {
              actions.replaceChildren(
                el('span', { class: 'muted', text: '¿Borrar este evento y sus asistencias? ' }),
                btn('Sí, borrar', async () => {
                  try { await call('DELETE', '/admin/events/' + ev.id); await loadEvents(); }
                  catch (err) { showProblem($('ev-list-msg'), err.problem, err.message); showActions(); }
                }, 'danger'),
                btn('Cancelar', showActions));
              actions.querySelector('button').focus();
            };
            showActions();
            body.append(el('tr', {}, [
              el('td', { text: ev.name }),
              el('td', { text: fmt.format(new Date(ev.startsAt)) }),
              el('td', { text: ev.venue + ', ' + ev.city }),
              el('td', {}, [tag(ev.isPublished ? 'Publicado' : 'Borrador', ev.isPublished ? 'good' : '')]),
              el('td', { text: String(ev.attendeeCount) }),
              actions]));
          }
        }

        function openEventForm(ev) {
          editingEvent = ev || null;
          $('ev-form-title').textContent = ev ? 'Editar evento' : 'Nuevo evento';
          $('ev-name').value = ev ? ev.name : '';
          $('ev-lineup').value = ev ? ev.artistLineup.join(', ') : '';
          $('ev-venue').value = ev ? ev.venue : '';
          $('ev-city').value = ev ? ev.city : '';
          $('ev-starts').value = ev ? toLocalInput(ev.startsAt) : '';
          $('ev-image').value = ev && ev.imageUrl ? ev.imageUrl : '';
          $('ev-desc').value = ev && ev.description ? ev.description : '';
          $('ev-pub').checked = ev ? ev.isPublished : true;
          setMsg($('ev-msg'), '', '');
          $('ev-form').hidden = false;
          $('ev-name').focus();
        }
        $('ev-new').addEventListener('click', () => openEventForm(null));
        $('ev-cancel').addEventListener('click', () => { $('ev-form').hidden = true; });

        $('ev-form').addEventListener('submit', async (e) => {
          e.preventDefault();
          const msg = $('ev-msg');
          const starts = $('ev-starts').value;
          if (!starts) { setMsg(msg, 'Indica la fecha y la hora del evento.', 'err'); return; }
          const payload = {
            name: $('ev-name').value.trim(),
            artistLineup: $('ev-lineup').value.split(',').map((s) => s.trim()).filter(Boolean),
            venue: $('ev-venue').value.trim(),
            city: $('ev-city').value.trim(),
            startsAt: fromLocalInput(starts),
            imageUrl: $('ev-image').value.trim() || null,
            description: $('ev-desc').value.trim() || null,
            isPublished: $('ev-pub').checked,
          };
          $('ev-save').disabled = true;
          try {
            if (editingEvent) await call('PUT', '/admin/events/' + editingEvent.id, payload);
            else await call('POST', '/admin/events', payload);
            $('ev-form').hidden = true;
            await loadEvents();
            setMsg($('ev-list-msg'), 'Evento guardado.', 'ok');
          } catch (err) {
            showProblem(msg, err.problem, err.message);
          } finally {
            $('ev-save').disabled = false;
          }
        });

        // ---------- canciones ----------
        async function loadGenres() {
          try {
            genres = await call('GET', '/genres');
          } catch { genres = []; }
          const filter = $('sg-genre-filter');
          const select = $('sg-genre');
          filter.replaceChildren(el('option', { value: '', text: 'Todos' }));
          select.replaceChildren();
          for (const g of genres) {
            filter.append(el('option', { value: g.slug, text: g.name }));
            select.append(el('option', { value: g.slug, text: g.name }));
          }
        }

        async function loadSongs() {
          const msg = $('sg-list-msg');
          const params = new URLSearchParams();
          if ($('sg-q').value.trim()) params.set('query', $('sg-q').value.trim());
          if ($('sg-genre-filter').value) params.set('genre', $('sg-genre-filter').value);
          try {
            songs = await call('GET', '/admin/songs?' + params.toString());
            setMsg(msg, '', '');
            renderSongs();
          } catch (err) { showProblem(msg, err.problem, err.message); }
        }
        $('sg-search').addEventListener('submit', (e) => { e.preventDefault(); loadSongs(); });

        function renderSongs() {
          const body = $('sg-body');
          body.replaceChildren();
          if (!songs.length) body.append(el('tr', {}, [el('td', { colspan: 7, class: 'muted', text: 'No hay canciones.' })]));
          for (const s of songs) {
            body.append(el('tr', {}, [
              el('td', {}, [el('img', { class: 'thumb', src: s.coverUrl, alt: '', width: 48, height: 48, loading: 'lazy', referrerpolicy: 'no-referrer' })]),
              el('td', { text: s.title }),
              el('td', { text: s.artist }),
              el('td', { text: s.genre }),
              el('td', { text: s.likes + ' / ' + s.dislikes }),
              el('td', {}, [tag(s.isActive ? 'Activa' : 'Inactiva', s.isActive ? 'good' : 'bad')]),
              el('td', {}, [
                btn('Editar', () => openSongForm(s)),
                btn(s.isActive ? 'Desactivar' : 'Activar', () => toggleSong(s))])]));
          }
        }

        function songPayload(s, overrides) {
          return Object.assign({ title: s.title, artist: s.artist, genre: s.genre, coverUrl: s.coverUrl,
            previewUrl: s.previewUrl || null, iTunesTrackId: s.iTunesTrackId || null, isActive: s.isActive }, overrides);
        }
        async function toggleSong(s) {
          try { await call('PUT', '/admin/songs/' + s.id, songPayload(s, { isActive: !s.isActive })); await loadSongs(); }
          catch (err) { showProblem($('sg-list-msg'), err.problem, err.message); }
        }

        function setPreview(url) {
          const audio = $('sg-audio');
          audio.pause();
          if (url) { audio.src = url; audio.hidden = false; }
          else { audio.removeAttribute('src'); audio.load(); audio.hidden = true; }
        }
        function fillSongForm(v) {
          $('sg-title').value = v.title || '';
          $('sg-artist').value = v.artist || '';
          if (v.genre && genres.some((g) => g.slug === v.genre)) $('sg-genre').value = v.genre;
          $('sg-cover').value = v.coverUrl || '';
          $('sg-preview').value = v.previewUrl || '';
          $('sg-itunes').value = v.iTunesTrackId ? String(v.iTunesTrackId) : '';
          setPreview(v.previewUrl);
        }
        function openSongForm(s) {
          editingSong = s || null;
          $('sg-panel').hidden = false;
          $('sg-form-title').textContent = s ? 'Editar canción' : 'Nueva canción';
          fillSongForm(s || {});
          $('sg-active').checked = s ? s.isActive : true;
          setMsg($('sg-msg'), '', '');
          $('sg-title').focus();
        }
        $('sg-new').addEventListener('click', () => openSongForm(null));
        $('sg-cancel').addEventListener('click', () => { setPreview(null); $('sg-panel').hidden = true; });
        $('sg-preview').addEventListener('change', () => setPreview($('sg-preview').value.trim()));

        $('it-form').addEventListener('submit', async (e) => {
          e.preventDefault();
          const msg = $('it-msg');
          const list = $('it-list');
          list.replaceChildren();
          setMsg(msg, 'Buscando…', '');
          try {
            const results = await call('GET', '/admin/itunes/search?term=' + encodeURIComponent($('it-term').value.trim()));
            setMsg(msg, results.length ? '' : 'iTunes no devolvió resultados.', '');
            for (const r of results) {
              list.append(el('li', {}, [
                el('img', { class: 'thumb', src: r.coverUrl, alt: '', width: 48, height: 48, referrerpolicy: 'no-referrer' }),
                el('div', { class: 'info', text: r.title + ' — ' + r.artist + (r.primaryGenre ? ' (' + r.primaryGenre + ')' : '') }),
                btn('Usar', () => {
                  const slug = (genres.find((g) => g.name.toLowerCase() === (r.primaryGenre || '').toLowerCase()) || {}).slug;
                  fillSongForm({ title: r.title, artist: r.artist, coverUrl: r.coverUrl, previewUrl: r.previewUrl, iTunesTrackId: r.iTunesTrackId, genre: slug });
                  $('sg-genre').focus();
                })]));
            }
          } catch (err) { showProblem(msg, err.problem, err.message); }
        });

        $('sg-form').addEventListener('submit', async (e) => {
          e.preventDefault();
          const msg = $('sg-msg');
          const itunes = $('sg-itunes').value.trim();
          const payload = {
            title: $('sg-title').value.trim(),
            artist: $('sg-artist').value.trim(),
            genre: $('sg-genre').value,
            coverUrl: $('sg-cover').value.trim(),
            previewUrl: $('sg-preview').value.trim() || null,
            iTunesTrackId: /^[0-9]+$/.test(itunes) ? Number(itunes) : null,
            isActive: $('sg-active').checked,
          };
          $('sg-save').disabled = true;
          try {
            if (editingSong) await call('PUT', '/admin/songs/' + editingSong.id, payload);
            else await call('POST', '/admin/songs', payload);
            setPreview(null);
            $('sg-panel').hidden = true;
            await loadSongs();
            setMsg($('sg-list-msg'), 'Canción guardada.', 'ok');
          } catch (err) {
            showProblem(msg, err.problem, err.message);
          } finally {
            $('sg-save').disabled = false;
          }
        });

        // ---------- reportes ----------
        const REASONS = { spam: 'Spam', harassment: 'Acoso', inappropriate: 'Contenido inapropiado', fakeProfile: 'Perfil falso', underage: 'Menor de edad', other: 'Otro' };
        const STATUSES = { open: 'Abierto', resolved: 'Resuelto', dismissed: 'Descartado' };
        const ACCOUNT = { active: 'activa', banned: 'suspendida' };

        function userLine(label, u) {
          return el('p', { class: 'muted', text: label + ': ' + (u ? u.displayName + ' <' + u.email + '> · ' + (ACCOUNT[u.status] || u.status) : 'cuenta eliminada') });
        }

        // Confirmación en el sitio y no con el diálogo nativo: no bloquea la página y la lee un lector de pantalla.
        function confirmIn(box, question, yesText, onYes, restore) {
          const yes = btn(yesText, async () => {
            yes.disabled = true;
            try { await onYes(); } finally { yes.disabled = false; }
          }, 'danger');
          box.replaceChildren(el('p', { text: question }), yes, btn('Cancelar', restore));
          yes.focus();
        }

        function evidenceNode(ev) {
          let text = ev.plaintext;
          try {
            const parsed = JSON.parse(ev.plaintext);
            if (parsed && typeof parsed === 'object' && parsed.b != null) text = String(parsed.b);
          } catch { /* texto crudo */ }
          return el('div', {}, [tag(ev.verified ? 'Verificado' : 'No verificado', ev.verified ? 'good' : 'bad'), el('blockquote', { text })]);
        }

        async function loadReports() {
          const msg = $('rp-msg');
          try {
            const reports = await call('GET', '/admin/reports?status=' + $('rp-status').value);
            setMsg(msg, reports.length ? '' : 'No hay reportes en este estado.', '');
            $('rp-list').replaceChildren(...reports.map(reportCard));
          } catch (err) { showProblem(msg, err.problem, err.message); }
        }
        $('rp-status').addEventListener('change', loadReports);

        function reportCard(r) {
          const noteId = 'note-' + r.id;
          const note = el('textarea', { id: noteId, maxlength: 1000 });
          const err = el('div', { class: 'msg', role: 'alert' });
          const who = r.reported ? r.reported.displayName : 'esta cuenta';
          const act = (action) => async () => {
            try {
              await call('POST', '/admin/reports/' + r.id + '/resolve', { action, note: note.value.trim() || null });
              await loadReports();
            } catch (e2) { showProblem(err, e2.problem, e2.message); showActions(); }
          };
          const actions = el('div', { class: 'actions' });
          const showActions = () => {
            const buttons = [];
            if (r.status === 'open') buttons.push(
              btn('Descartar', act('dismiss')),
              btn('Resuelto', act('resolve')),
              btn('Suspender cuenta', () => confirmIn(actions,
                '¿Suspender a ' + who + '? Cierra todos sus matches, revoca sus sesiones y los matches no se recuperan al reactivarla.',
                'Sí, suspender', act('ban'), showActions), 'danger'));
            if (r.reported && r.reported.status === 'banned') buttons.push(
              btn('Reactivar cuenta', () => confirmIn(actions,
                '¿Reactivar a ' + who + '? Podrá volver a entrar y registrarse con su correo; sus matches cerrados siguen cerrados.',
                'Sí, reactivar', async () => {
                  try {
                    await call('PUT', '/admin/users/' + r.reported.id + '/status', { status: 'active' });
                    await loadReports();
                  } catch (e2) { showProblem(err, e2.problem, e2.message); showActions(); }
                }, showActions)));
            actions.replaceChildren(...buttons);
          };
          showActions();
          const children = [
            el('h2', { text: (REASONS[r.reason] || r.reason) + ' · ' + (STATUSES[r.status] || r.status) }),
            el('p', { class: 'muted', text: fmt.format(new Date(r.createdAt)) }),
            userLine('Reporta', r.reporter),
            userLine('Reportado', r.reported),
            el('p', { class: 'muted', text: 'Otras personas que reportaron esta cuenta: ' + r.priorReportsAgainstUser }),
          ];
          if (r.detail) children.push(el('p', { text: r.detail }));
          if (r.evidencePurgedAt) children.push(el('p', { class: 'muted',
            text: 'El detalle y el texto de la evidencia se borraron por retención el ' + fmt.format(new Date(r.evidencePurgedAt)) + '.' }));
          else if (r.evidence.length) children.push(el('h3', { text: 'Evidencia' }), ...r.evidence.map(evidenceNode));
          if (r.status === 'open') children.push(el('label', { for: noteId, text: 'Nota de resolución (opcional)' }), note);
          else if (r.resolutionNote) children.push(el('p', { text: 'Nota: ' + r.resolutionNote }));
          if (actions.childElementCount) children.push(actions, err);
          return el('article', { class: 'card' }, children);
        }
        """;
}
