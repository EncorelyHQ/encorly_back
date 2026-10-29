"""Genera el AVANCE del manual técnico desde el manual completo.

Las secciones de INCLUIDAS conservan su contenido. El resto conserva solo el título (y los
títulos de sus subsecciones), sin texto: así el avance muestra los 10 capítulos, ninguno completo.
La portada y todo lo anterior al capítulo 1 se copian tal cual, salvo el tipo de documento.

Uso: python3 generar_avance.py   (escribe MANUAL-TECNICO-ENCORELY-AVANCE.md)
"""
import pathlib
import re

FUENTE = pathlib.Path(__file__).with_name('MANUAL-TECNICO-ENCORELY.md')
DESTINO = pathlib.Path(__file__).with_name('MANUAL-TECNICO-ENCORELY-AVANCE.md')

# Secciones con contenido. Un número incluye a sus subsecciones salvo que alguna se liste aparte
# en EXCLUIDAS_DENTRO (para dejar un padre incluido con hijos solo-título).
INCLUIDAS = {
    '1.1', '1.2', '1.3', '1.4',
    '2.1', '2.2',
    '3.1', '3.2',
    '4.1', '4.3',
    '5.1', '5.2', '5.8',
    '6.1', '6.2', '6.3', '6.4',
    '7.1', '7.2',
    '8.1', '8.2', '8.3',
}
EXCLUIDAS_DENTRO = {'3.2.1', '3.2.2', '3.2.3', '4.1.4', '4.1.5', '4.1.6', '4.1.7'}

texto = FUENTE.read_text(encoding='utf-8')
ini = texto.index('\n## 1. ')
cabecera, cuerpo = texto[:ini + 1], texto[ini + 1:]

cabecera = cabecera.replace('<p class="portada-tipo">Documentación técnica · Sistema</p>',
                            '<p class="portada-tipo">Documentación técnica · Avance</p>')
cuerpo = cuerpo.replace('| Tipo de documento | Manual técnico unificado |',
                        '| Tipo de documento | Manual técnico unificado · avance de entrega |')


def incluida(num):
    """Una sección tiene contenido si ella o un ancestro está en INCLUIDAS y ninguna exclusión la cubre."""
    partes = num.split('.')
    ancestros = ['.'.join(partes[:i]) for i in range(len(partes), 1, -1)]
    if any(a in EXCLUIDAS_DENTRO for a in ancestros):
        return False
    return any(a in INCLUIDAS for a in ancestros)


salida, mostrar, en_codigo = [], True, False
for linea in cuerpo.split('\n'):
    if linea.startswith('```'):
        en_codigo = not en_codigo
    m = None if en_codigo else re.match(r'^(#{2,4}) (\d+(?:\.\d+)*)\.? ', linea)
    if m:
        nivel, num = len(m.group(1)), m.group(2)
        # Los capítulos (##) solo muestran su título; el texto introductorio va si alguna sección entra.
        mostrar = nivel == 2 or incluida(num)
        salida.append(linea)
        continue
    if mostrar:
        salida.append(linea)

DESTINO.write_text(cabecera + '\n'.join(salida), encoding='utf-8')
print(DESTINO)
