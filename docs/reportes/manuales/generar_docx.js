// Genera el .docx del avance del manual técnico desde su Markdown, con el mismo estilo de la entrega del 22 de octubre.
//
// Uso (docx-js no es dependencia del repo):
//   npm i --prefix /tmp/docxgen docx@9
//   NODE_PATH=/tmp/docxgen/node_modules node generar_docx.js MANUAL-TECNICO-ENCORELY-AVANCE.md MANUAL-TECNICO-ENCORELY-AVANCE.docx
//
// Los diagramas (bloques mermaid y plantuml) salen de diagramas/NN.png, en el orden en que aparecen en el Markdown.
// Si cambia un diagrama, se vuelve a renderizar su PNG: PlantUML con `-tpng` y `skinparam dpi 300`; Mermaid, con una
// captura del HTML que genera generar_pdf.py.
const fs = require('fs')
const path = require('path')
const {
  AlignmentType, BorderStyle, Document, Footer, Header, HeadingLevel, ImageRun, LevelFormat, Packer,
  PageNumber, Paragraph, ShadingType, Table, TableCell, TableOfContents, TableRow, TextRun, VerticalAlign, WidthType,
} = require('docx')

const [, , entrada, salida] = process.argv
const dir = path.dirname(path.resolve(entrada))
const lineas = fs.readFileSync(entrada, 'utf-8').split('\n')

const TINTA = '1D1A17'
const GRIS = '6B6259'
const LINEA = 'E3DDD5'
const FONDO = 'F5F2EE'
const ANCHO = 9638 // A4 menos márgenes de 1134 DXA
const SANS = 'Arial'
const MONO = 'Consolas'

// --- Texto en línea: **negrita**, `código` y escapes de Markdown ---
function runs(texto, base = {}) {
  const out = []
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g
  let i = 0
  for (const m of texto.matchAll(re)) {
    if (m.index > i) out.push(new TextRun({ ...base, text: limpiar(texto.slice(i, m.index)) }))
    const t = m[0]
    if (t.startsWith('**')) out.push(new TextRun({ ...base, text: limpiar(t.slice(2, -2)), bold: true }))
    else out.push(new TextRun({ ...base, text: t.slice(1, -1), font: MONO, size: (base.size || 21) - 3, shading: { type: ShadingType.CLEAR, fill: FONDO, color: 'auto' } }))
    i = m.index + t.length
  }
  if (i < texto.length) out.push(new TextRun({ ...base, text: limpiar(texto.slice(i)) }))
  return out
}
const limpiar = (t) => t.replace(/\\([_*`|\\])/g, '$1')

// --- Portada ---
function portada(html) {
  const uno = (clase) => (html.match(new RegExp(`class="${clase}">([^<]*)<`)) || [])[1] || ''
  const datos = [...html.matchAll(/<span>([^<]*)<\/span><p>([^<]*)<\/p>/g)].map((m) => [m[1], m[2]])
  const talon = [...html.matchAll(/talon-etq">([^<]*)<\/p><p class="talon-val">([^<]*)</g)].map((m) => [m[1], m[2]])
  const etiqueta = (t) => new Paragraph({ spacing: { before: 160, after: 40 }, children: [new TextRun({ text: t.toUpperCase(), font: SANS, size: 13, color: GRIS, characterSpacing: 40 })] })
  const sinBorde = { style: BorderStyle.NONE, size: 0, color: 'FFFFFF' }
  const izquierda = [
    new Paragraph({ children: [new TextRun({ text: uno('portada-marca'), font: SANS, bold: true, size: 32, color: TINTA, characterSpacing: 60 })] }),
    new Paragraph({ spacing: { before: 2400, after: 120 }, children: [new TextRun({ text: uno('portada-tipo').toUpperCase(), font: SANS, size: 13, color: GRIS, characterSpacing: 40 })] }),
    new Paragraph({ spacing: { after: 300, line: 860, lineRule: 'exact' }, children: [new TextRun({ text: uno('portada-titulo').toUpperCase(), font: SANS, bold: true, size: 76, color: TINTA })] }),
    new Paragraph({ spacing: { after: 3000 }, children: [new TextRun({ text: uno('portada-sub'), font: SANS, size: 18, color: GRIS })] }),
    new Paragraph({ border: { top: { style: BorderStyle.SINGLE, size: 12, color: TINTA, space: 6 } }, children: [] }),
    ...datos.flatMap(([e, v]) => [etiqueta(e), new Paragraph({ children: [new TextRun({ text: v, font: SANS, size: 18, color: TINTA })] })]),
  ]
  const derecha = talon.flatMap(([e, v]) => [etiqueta(e), new Paragraph({ children: [new TextRun({ text: v, font: MONO, size: 17, color: TINTA })] })])
  return new Table({
    width: { size: ANCHO, type: WidthType.DXA },
    columnWidths: [7238, 2400],
    borders: { top: sinBorde, bottom: sinBorde, left: sinBorde, right: sinBorde, insideHorizontal: sinBorde, insideVertical: sinBorde },
    rows: [new TableRow({
      height: { value: 13600, rule: 'exact' },
      children: [
        new TableCell({ width: { size: 7238, type: WidthType.DXA }, borders: { top: sinBorde, bottom: sinBorde, left: sinBorde, right: sinBorde }, children: izquierda }),
        new TableCell({
          width: { size: 2400, type: WidthType.DXA }, verticalAlign: VerticalAlign.BOTTOM,
          margins: { left: 200 },
          borders: { top: sinBorde, bottom: sinBorde, right: sinBorde, left: { style: BorderStyle.DASHED, size: 6, color: GRIS } },
          children: derecha,
        }),
      ],
    })],
  })
}

// --- Tablas ---
function celdas(fila) {
  return fila.trim().replace(/^\||\|$/g, '').split(/(?<!\\)\|/).map((c) => c.trim())
}
function tabla(filas) {
  const cab = celdas(filas[0])
  const cuerpo = filas.slice(2).map(celdas)
  const n = cab.length
  // Cada columna recibe al menos el ancho de su palabra más larga (sin partirla) y el resto se reparte según el largo del texto.
  const texto = (f, j) => limpiar((f[j] || '').replace(/\*\*|`/g, ''))
  const minimo = Array.from({ length: n }, (_, j) => Math.max(560, 180 + 95 * Math.max(...[cab, ...cuerpo].map((f) => Math.max(0, ...texto(f, j).split(/\s+/).map((w) => w.length))))))
  const largo = Array.from({ length: n }, (_, j) => Math.max(1, ...[cab, ...cuerpo].map((f) => Math.min(texto(f, j).length, 80))))
  const base = minimo.reduce((a, b) => a + b, 0)
  let anchos
  if (base >= ANCHO) anchos = minimo.map((m) => Math.floor((m * ANCHO) / base))
  else {
    const total = largo.reduce((a, b) => a + b, 0)
    anchos = minimo.map((m, j) => m + Math.floor(((ANCHO - base) * largo[j]) / total))
  }
  anchos[n - 1] += ANCHO - anchos.reduce((a, b) => a + b, 0)
  const borde = (color, size) => ({ style: BorderStyle.SINGLE, color, size })
  const nada = { style: BorderStyle.NONE, color: 'FFFFFF', size: 0 }
  const celda = (texto, j, esCab) => new TableCell({
    width: { size: anchos[j], type: WidthType.DXA },
    margins: { top: 60, bottom: 60, left: 90, right: 90 },
    shading: esCab ? { type: ShadingType.CLEAR, fill: FONDO, color: 'auto' } : undefined,
    borders: { top: borde(LINEA, 4), left: nada, right: nada, bottom: esCab ? borde(TINTA, 10) : borde(LINEA, 4) },
    children: [new Paragraph({ spacing: { after: 0, line: 260 }, children: runs(texto, { font: SANS, size: 17, bold: esCab || undefined, color: TINTA }) })],
  })
  return new Table({
    width: { size: ANCHO, type: WidthType.DXA },
    columnWidths: anchos,
    rows: [
      new TableRow({ tableHeader: true, children: cab.map((c, j) => celda(c, j, true)) }),
      ...cuerpo.map((f) => new TableRow({ cantSplit: true, children: Array.from({ length: n }, (_, j) => celda(f[j] || '', j, false)) })),
    ],
  })
}

// --- Código y diagramas ---
function codigo(bloque, sangria) {
  return bloque.map((l, k) => new Paragraph({
    indent: { left: sangria },
    shading: { type: ShadingType.CLEAR, fill: FONDO, color: 'auto' },
    spacing: { before: k === 0 ? 80 : 0, after: k === bloque.length - 1 ? 160 : 0, line: 240 },
    keepLines: true,
    children: [new TextRun({ text: l || ' ', font: MONO, size: 16, color: TINTA })],
  }))
}
let nDiagrama = 0
function diagrama() {
  nDiagrama += 1
  const archivo = path.join(dir, 'diagramas', `${String(nDiagrama).padStart(2, '0')}.png`)
  const png = fs.readFileSync(archivo)
  const w = png.readUInt32BE(16)
  const h = png.readUInt32BE(20)
  const ancho = 640
  const alto = Math.min(Math.round((ancho * h) / w), 860)
  return new Paragraph({
    alignment: AlignmentType.CENTER, spacing: { before: 120, after: 200 }, keepNext: false,
    children: [new ImageRun({ type: 'png', data: png, transformation: { width: Math.round((alto * w) / h) < ancho ? Math.round((alto * w) / h) : ancho, height: alto } })],
  })
}

// --- Recorrido del Markdown ---
const hijos = []
let i = 0
let lista = 0
const parrafo = (texto, extra = {}) => new Paragraph({ spacing: { after: 140, line: 300 }, ...extra, children: runs(texto) })

while (i < lineas.length) {
  const l = lineas[i]
  if (l.startsWith('<section class="portada">')) {
    let html = ''
    while (!lineas[i].startsWith('</section>')) html += lineas[i++]
    i += 1
    hijos.push(portada(html))
    continue
  }
  if (l.startsWith('[[TOC]]')) {
    hijos.push(
      new Paragraph({ pageBreakBefore: true, spacing: { after: 240 }, children: [new TextRun({ text: 'Tabla de contenido', font: SANS, bold: true, size: 32, color: TINTA })] }),
      new TableOfContents('Tabla de contenido', { hyperlink: true, headingStyleRange: '1-3' }),
    )
    i += 1
    continue
  }
  const h = l.match(/^(#{2,4}) (.*)$/)
  if (h) {
    const nivel = [HeadingLevel.HEADING_1, HeadingLevel.HEADING_2, HeadingLevel.HEADING_3][h[1].length - 2]
    hijos.push(new Paragraph({ heading: nivel, keepNext: true, children: runs(h[2]) }))
    i += 1
    continue
  }
  const fence = l.match(/^( *)```(\w*)$/)
  if (fence) {
    const sangria = fence[1].length
    const bloque = []
    i += 1
    while (!lineas[i].match(/^ *```$/)) bloque.push(lineas[i++].slice(sangria))
    i += 1
    if (fence[2] === 'mermaid' || fence[2] === 'plantuml') hijos.push(diagrama())
    else hijos.push(...codigo(bloque, sangria ? 360 : 0))
    continue
  }
  if (l.startsWith('|')) {
    const filas = []
    while (i < lineas.length && lineas[i].startsWith('|')) filas.push(lineas[i++])
    hijos.push(tabla(filas), new Paragraph({ spacing: { after: 120 }, children: [] }))
    continue
  }
  const vineta = l.match(/^- (.*)$/)
  const numero = l.match(/^(\d+)\. (.*)$/)
  if (vineta || numero) {
    if (numero && numero[1] === '1') lista += 1
    let texto = (vineta || numero)[vineta ? 1 : 2]
    i += 1
    while (i < lineas.length && lineas[i].match(/^ {2,}\S/) && !lineas[i].match(/^ *```/)) texto += ' ' + lineas[i++].trim()
    hijos.push(new Paragraph({
      numbering: vineta ? { reference: 'vinetas', level: 0 } : { reference: 'numeros', level: 0, instance: lista },
      spacing: { after: 80, line: 300 }, children: runs(texto),
    }))
    continue
  }
  if (l.match(/^ {3}\S/)) {
    let texto = l.trim()
    i += 1
    while (i < lineas.length && lineas[i].match(/^ {3}\S/)) texto += ' ' + lineas[i++].trim()
    hijos.push(parrafo(texto, { indent: { left: 360 } }))
    continue
  }
  if (l.trim() === '' || l.startsWith('<')) {
    i += 1
    continue
  }
  let texto = l.trim()
  i += 1
  while (i < lineas.length && lineas[i].trim() && !lineas[i].match(/^(#|\||- |\d+\. |```| |<)/)) texto += ' ' + lineas[i++].trim()
  hijos.push(parrafo(texto))
}

const titulo = 'Encorely · Manual técnico del sistema (avance)'
const doc = new Document({
  title: titulo,
  creator: 'Equipo Encorely',
  features: { updateFields: true },
  styles: {
    default: { document: { run: { font: 'Georgia', size: 21, color: TINTA }, paragraph: { spacing: { after: 140, line: 300 } } } },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: SANS, size: 32, bold: true, color: TINTA }, paragraph: { pageBreakBefore: true, spacing: { before: 0, after: 240 }, outlineLevel: 0, border: { top: { style: BorderStyle.SINGLE, size: 6, color: TINTA, space: 8 } } } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: SANS, size: 25, bold: true, color: TINTA }, paragraph: { spacing: { before: 320, after: 120 }, outlineLevel: 1 } },
      { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: SANS, size: 22, bold: true, color: GRIS }, paragraph: { spacing: { before: 240, after: 100 }, outlineLevel: 2 } },
    ],
  },
  numbering: {
    config: [
      { reference: 'vinetas', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 240 } } } }] },
      { reference: 'numeros', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 300 } } } }] },
    ],
  },
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1418, right: 1134, bottom: 1134, left: 1134, header: 708, footer: 708 } } },
    headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: titulo, font: SANS, size: 14, color: GRIS })] })] }) },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT, ' / ', PageNumber.TOTAL_PAGES], font: SANS, size: 14, color: GRIS })] })] }) },
    children: hijos,
  }],
})

Packer.toBuffer(doc).then((b) => {
  fs.writeFileSync(salida, b)
  console.log(`${salida} · ${nDiagrama} diagramas`)
})
