"""Convierte un .md de reportes/ en .html + .pdf legibles (python-markdown + Chrome headless).
Uso: python3 generar_pdf.py ARCHIVO.md ["Título de la cabecera"]

- Bloques ```mermaid se renderizan como diagramas (mermaid.js en Chrome).
- Bloques ```plantuml se renderizan en local a SVG con PlantUML (para UML con notación estricta,
  p. ej. despliegue con nodos 3D). Requiere ~/.local/share/plantuml/plantuml-*.jar y Java (mise).
- Si el .md contiene una línea [[TOC]], ahí se inserta una tabla de contenido con número de
  página real: se calcula en una primera pasada y se rellena en la segunda. Entran los títulos
  numerados (1., 1.1, 1.1.1) y los "Anexo …" de nivel ##, ### y ####.
"""
import html as htmllib
import pathlib
import re
import subprocess
import sys

import markdown

src = pathlib.Path(sys.argv[1]).resolve()
# Título opcional para la cabecera y <title>; por defecto, el del reporte completo.
titulo = sys.argv[2] if len(sys.argv) > 2 else 'Encorely — Estado del sistema y plan de reconstrucción'
html_path = src.with_suffix('.html')
pdf_path = src.with_suffix('.pdf')
FIN_TOC = 'FINTOCMARCA'

texto = src.read_text(encoding='utf-8')

# Los diagramas se sacan antes de convertir para que markdown no los toque.
mermaids = []
def _guardar_mermaid(m):
    mermaids.append(m.group(1))
    return f'\nMERMAIDPH{len(mermaids) - 1}\n'
texto = re.sub(r'^```mermaid\n(.*?)\n```$', _guardar_mermaid, texto, flags=re.S | re.M)

plantumls = []
def _guardar_plantuml(m):
    plantumls.append(m.group(1))
    return f'\nPLANTUMLPH{len(plantumls) - 1}\n'
texto = re.sub(r'^```plantuml\n(.*?)\n```$', _guardar_plantuml, texto, flags=re.S | re.M)


def svg_plantuml(codigo):
    """Renderiza en local; sin Graphviz se usa el layout interno smetana."""
    import glob, shutil
    jar = sorted(glob.glob(str(pathlib.Path.home() / '.local/share/plantuml/plantuml-*.jar')))[-1]
    java = subprocess.run(['mise', 'where', 'java@temurin-17.0.20+8'], capture_output=True, text=True).stdout.strip() + '/bin/java'
    if not shutil.which('dot') and '!pragma layout' not in codigo:
        codigo = codigo.replace('@startuml', '@startuml\n!pragma layout smetana', 1)
    out = subprocess.run([java, '-Djava.awt.headless=true', '-jar', jar, '-tsvg', '-pipe', '-charset', 'UTF-8'],
                         input=codigo, capture_output=True, text=True, check=True).stdout
    return out[out.index('<svg'):]

md = markdown.Markdown(extensions=['tables', 'fenced_code', 'toc', 'attr_list', 'sane_lists'],
                       extension_configs={'toc': {'toc_depth': '2-4'}})
body = md.convert(texto)
for i, codigo in enumerate(plantumls):
    svg = svg_plantuml(codigo)
    dim = re.search(r'width:(\d+)px;height:(\d+)px', svg)
    # Los diagramas horizontales (despliegue) ocupan todo el ancho para que su texto se lea al imprimir.
    clase = 'plantuml ancho' if dim and int(dim.group(1)) > 1.3 * int(dim.group(2)) else 'plantuml'
    body = body.replace(f'<p>PLANTUMLPH{i}</p>', f'<div class="{clase}">{svg}</div>')
for i, codigo in enumerate(mermaids):
    body = body.replace(f'<p>MERMAIDPH{i}</p>', f'<div class="mermaid">{htmllib.escape(codigo)}</div>')
# Un título seguido de su diagrama (con a lo sumo un párrafo en medio) no se separa entre páginas.
body = re.sub(r'(<h([2-4])[^>]*>(?:(?!</h[2-4]>).)*</h\2>\s*(?:<p>(?:(?!</p>).)*</p>\s*)?<div class="(?:mermaid|plantuml(?: ancho)?)">.*?</div>)',
              r'<div class="junto">\1</div>', body, flags=re.S)


def entradas_toc(tokens, out):
    for t in tokens:
        nombre = htmllib.unescape(re.sub(r'<[^>]+>', '', t['name'])).strip()
        if t['level'] in (2, 3, 4) and re.match(r'(\d+(\.\d+)*\.?\s|Anexo\b)', nombre):
            out.append((t['level'], nombre, t['id']))
        entradas_toc(t['children'], out)
    return out


entradas = entradas_toc(md.toc_tokens, []) if '[[TOC]]' in texto else []


def html_toc(paginas):
    filas = [f'<li class="t{n}"><a href="#{i}"><span>{htmllib.escape(t)}</span><b></b><em>{paginas.get(i, "··")}</em></a></li>'
             for n, t, i in entradas]
    return (f'<nav class="toc-paginas"><h2 class="toc-titulo">Tabla de contenido</h2><ol>{"".join(filas)}</ol>'
            f'<p class="fin-toc">{FIN_TOC}</p></nav>')


css = """
@page { size: A4; margin: 22mm 20mm 24mm 20mm;
  @bottom-center { content: counter(page) " / " counter(pages); font: 9pt 'Archivo', sans-serif; color: #6b6259; }
  @top-right { content: "{titulo_css}"; font: 8.5pt 'Archivo', sans-serif; color: #9a8f84; }
}
@page :first { @top-right { content: none; } @bottom-center { content: none; } }
@page portada { margin: 0; @top-right { content: none; } @bottom-center { content: none; } }
:root { --ink: #1d1a17; --muted: #5c544c; --line: #e3ddd5; --soft: #f7f3ee; --accent: #3d3631; }
html { font-size: 10.6pt; }
body { font-family: 'Source Serif 4', Georgia, serif; color: var(--ink); line-height: 1.55; margin: 0; background: #fff; }
h1, h2, h3, h4 { font-family: 'Archivo', system-ui, sans-serif; line-height: 1.2; color: var(--ink); break-after: avoid; }
h1 { font-size: 24pt; font-weight: 800; letter-spacing: -0.01em; margin: 0 0 6mm; }
h2 { font-size: 16pt; font-weight: 750; margin: 11mm 0 4mm; padding-top: 3mm; border-top: 2.5pt solid var(--accent); break-before: page; }
h3 { font-size: 12.5pt; font-weight: 700; margin: 7mm 0 2.5mm; }
h4 { font-size: 11pt; font-weight: 700; margin: 5mm 0 2mm; color: var(--muted); }
h1 + p, h1 + p + p { font-size: 12pt; color: var(--muted); }
p { margin: 0 0 3mm; orphans: 3; widows: 3; }
ul, ol { margin: 0 0 3.5mm; padding-left: 6mm; }
li { margin: 0.8mm 0; }
li > p { margin: 0; }
strong { font-weight: 700; }
a { color: var(--accent); text-decoration: none; }
code { font-family: 'IBM Plex Mono', ui-monospace, monospace; font-size: 0.86em; background: var(--soft); padding: 0.4mm 1.2mm; border-radius: 1mm; overflow-wrap: anywhere; }
pre { background: var(--soft); border: 0.6pt solid var(--line); border-radius: 2mm; padding: 3mm 4mm; white-space: pre-wrap; overflow-wrap: anywhere; break-inside: avoid; font-size: 8.6pt; line-height: 1.45; }
pre code { background: none; padding: 0; font-size: inherit; }
table { width: 100%; border-collapse: collapse; margin: 2mm 0 5mm; font-family: 'Archivo', system-ui, sans-serif; font-size: 8.8pt; line-height: 1.4; }
thead { display: table-header-group; }
tr { break-inside: avoid; }
th { text-align: left; font-weight: 700; background: var(--soft); border-bottom: 1.2pt solid var(--ink); padding: 1.8mm 2.2mm; vertical-align: bottom; }
td { border-bottom: 0.5pt solid var(--line); padding: 1.6mm 2.2mm; vertical-align: top; overflow-wrap: break-word; }
td code { overflow-wrap: anywhere; }
blockquote { margin: 3mm 0 4mm; padding: 2.5mm 4mm; border-left: 2.5pt solid var(--accent); background: var(--soft); color: var(--muted); }
hr { border: none; border-top: 0.6pt solid var(--line); margin: 6mm 0; }
img { max-width: 100%; max-height: 120mm; display: block; margin: 3mm auto 1mm; border: 0.6pt solid var(--line); border-radius: 1.5mm; break-inside: avoid; }
.toc { font-family: 'Archivo', system-ui, sans-serif; font-size: 10pt; }
.mermaid { margin: 4mm 0 5mm; text-align: center; break-inside: avoid; }
.mermaid svg { max-width: 100%; max-height: 190mm; height: auto; }
.junto { break-inside: avoid; display: inline-block; width: 100%; }
.junto > h2 { break-before: page; }
.plantuml { margin: 4mm 0 5mm; text-align: center; break-inside: avoid; }
.plantuml svg { max-width: 100%; max-height: 200mm; height: auto !important; width: auto !important; }
.plantuml.ancho svg { width: 100% !important; }
.toc-paginas { break-before: page; break-after: page; font-family: 'Archivo', system-ui, sans-serif; }
.toc-paginas ol { list-style: none; padding: 0; margin: 0; }
.toc-paginas li { margin: 0; }
.toc-paginas a { display: flex; align-items: baseline; gap: 2mm; color: var(--ink); padding: 0.9mm 0; }
.toc-paginas b { flex: 1; border-bottom: 0.8pt dotted #a69b8f; transform: translateY(-1mm); }
.toc-paginas em { font-style: normal; font-variant-numeric: tabular-nums; min-width: 7mm; text-align: right; }
.toc-paginas .t2 { font-weight: 700; font-size: 10.5pt; margin-top: 2mm; }
.toc-paginas .t3 { padding-left: 6mm; font-size: 9.6pt; }
.toc-paginas .t4 { padding-left: 12mm; font-size: 9pt; color: var(--muted); }
.fin-toc { color: #fff; font-size: 1pt; margin: 0; }
/* Portada empresarial neutra: blanco y negro cálido, grises solo para jerarquía; el talón perforado es el único gesto de marca. */
.portada { page: portada; break-after: page; width: 210mm; height: 297mm; display: flex; background: #fff; color: #1d1a17; font-family: 'Archivo', sans-serif; }
.portada p { margin: 0; }
.portada-cuerpo { flex: 1; padding: 30mm 16mm 24mm 22mm; display: flex; flex-direction: column; }
.portada-marca { font-family: 'Big Shoulders Display', 'Big Shoulders', 'Archivo', sans-serif; font-weight: 800; font-size: 24pt; letter-spacing: 0.08em; color: #1d1a17; }
.portada-tipo { margin-top: 58mm !important; font-family: 'IBM Plex Mono', monospace; font-size: 8.5pt; letter-spacing: 0.14em; text-transform: uppercase; color: #7a7066; }
.portada-titulo { margin-top: 5mm !important; font-family: 'Big Shoulders Display', 'Big Shoulders', 'Archivo', sans-serif; font-weight: 800; font-size: 48pt; line-height: 0.95; text-transform: uppercase; color: #1d1a17; }
.portada-sub { margin-top: 7mm !important; max-width: 118mm; font-size: 11pt; line-height: 1.5; color: #5c544c; }
.portada-datos { margin-top: auto; border-top: 0.8pt solid #1d1a17; padding-top: 6mm; display: grid; gap: 4.5mm; }
.portada-datos span { display: block; font-family: 'IBM Plex Mono', monospace; font-size: 7.5pt; letter-spacing: 0.14em; text-transform: uppercase; color: #7a7066; margin-bottom: 1mm; }
.portada-datos p { font-size: 10.5pt; color: #1d1a17; }
.portada-talon { width: 46mm; padding: 30mm 8mm 24mm 9mm; border-left: 0.8pt dashed #b8afa5; display: flex; flex-direction: column; justify-content: flex-end; }
.talon-etq { font-family: 'IBM Plex Mono', monospace; font-size: 7pt; letter-spacing: 0.14em; text-transform: uppercase; color: #7a7066; }
.talon-val { font-family: 'IBM Plex Mono', monospace; font-size: 9pt; color: #1d1a17; margin-bottom: 5mm !important; }
"""
css = css.replace("{titulo_css}", titulo.replace(" — ", " · "))

MERMAID_JS = """<script type="module">
import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.esm.min.mjs';
// deterministicIds: con el reloj virtual de Chrome los IDs por hora se repiten y los diagramas se pisan.
mermaid.initialize({ startOnLoad: false, deterministicIds: true, theme: 'neutral', fontFamily: 'Archivo, sans-serif',
  flowchart: { htmlLabels: false }, er: { useMaxWidth: true } });
await mermaid.run({ querySelector: '.mermaid' });
</script>"""


def imprimir(cuerpo, intentos=3):
    html = f"""<!doctype html>
<html lang="es"><head><meta charset="utf-8"><title>{titulo}</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Archivo:wght@400;600;700;800&family=Big+Shoulders+Display:wght@700;800&family=IBM+Plex+Mono:wght@400;500&family=Source+Serif+4:ital,opsz,wght@0,8..60,400;0,8..60,600;0,8..60,700;1,8..60,400&display=swap" rel="stylesheet">
<style>{css}</style>{MERMAID_JS if mermaids else ''}</head><body>{cuerpo}</body></html>"""
    html_path.write_text(html, encoding='utf-8')
    subprocess.run(['google-chrome', '--headless=new', '--disable-gpu', '--no-pdf-header-footer',
                    '--virtual-time-budget=30000', '--run-all-compositor-stages-before-draw',
                    f'--print-to-pdf={pdf_path}', html_path.as_uri()], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    # Si mermaid no cargó del CDN, el PDF trae el código crudo de los diagramas: se reintenta.
    if mermaids and intentos > 1:
        txt = subprocess.run(['pdftotext', str(pdf_path), '-'], capture_output=True, text=True).stdout
        if re.search(r'^(erDiagram|flowchart|sequenceDiagram|graph )|Syntax error', txt, re.M):
            imprimir(cuerpo, intentos - 1)


def paginas_reales():
    """Primera página, posterior a la tabla de contenido, donde aparece cada título."""
    total = int(re.search(r'Pages:\s+(\d+)', subprocess.run(['pdfinfo', str(pdf_path)], capture_output=True, text=True).stdout).group(1))
    norm = lambda s: re.sub(r'\s+', ' ', s).strip()
    textos = [norm(subprocess.run(['pdftotext', '-f', str(p), '-l', str(p), str(pdf_path), '-'],
                                  capture_output=True, text=True).stdout) for p in range(1, total + 1)]
    inicio = next((i for i, t in enumerate(textos) if FIN_TOC in t), 0) + 1
    res, desde = {}, inicio
    for _, nombre, ident in entradas:
        clave = norm(nombre)[:45]
        for p in range(desde, total):
            if clave in textos[p]:
                res[ident] = p + 1
                desde = p  # los títulos van en orden: no buscar hacia atrás
                break
    return res


marca = '<p>[[TOC]]</p>'
if entradas and marca in body:
    imprimir(body.replace(marca, html_toc({})))
    imprimir(body.replace(marca, html_toc(paginas_reales())))
else:
    imprimir(body.replace(marca, ''))
print(pdf_path)
