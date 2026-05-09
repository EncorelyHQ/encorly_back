#!/usr/bin/env python3
"""
Genera la documentación del proyecto Encorely (backend) en PDF con portada estilo APA (7.ª ed., adaptación estudiantil en español).
Requisitos: ver README en esta carpeta o usar el entorno .venv-doc del repositorio.
"""

from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("MPLCONFIGDIR", str(Path(__file__).resolve().parent / ".matplotlib-cache"))

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch, Rectangle
from fpdf import FPDF

BASE = Path(__file__).resolve().parent
BUILD = BASE / "pdf_build"
OUT_PDF = BASE / "Encorely_Documentacion_Proyecto_APA.pdf"


BODY_PT = 12
LINE_BODY = 8  # ~interlineado doble con cuerpo 12 pt

def dejavu_paths() -> tuple[str, str]:
    mpl_dir = Path(matplotlib.matplotlib_fname()).parent
    d = mpl_dir / "fonts" / "ttf"
    return str(d / "DejaVuSans.ttf"), str(d / "DejaVuSans-Bold.ttf")


def document_font_paths() -> tuple[str, str]:
    """Times New Roman (macOS en Fonts/Supplemental); si no existe, DejaVu."""
    supplemental = Path("/System/Library/Fonts/Supplemental")
    regular = supplemental / "Times New Roman.ttf"
    bold = supplemental / "Times New Roman Bold.ttf"
    if regular.is_file() and bold.is_file():
        return str(regular), str(bold)
    return dejavu_paths()


def ensure_assets() -> tuple[str, str, str]:
    BUILD.mkdir(parents=True, exist_ok=True)
    arch = BUILD / "fig_arquitectura.png"
    nav = BUILD / "fig_navegacion.png"
    er = BUILD / "fig_er.png"
    if not arch.exists():
        draw_architecture(arch)
    if not nav.exists():
        draw_navigation(nav)
    if not er.exists():
        draw_er(er)
    return str(arch), str(nav), str(er)


def draw_architecture(path: Path) -> None:
    fig, ax = plt.subplots(figsize=(10, 5.5))
    ax.set_xlim(0, 12)
    ax.set_ylim(0, 7)
    ax.axis("off")

    def box(x, y, w, h, text, fc="#e8f4fc"):
        p = FancyBboxPatch(
            (x, y), w, h, boxstyle="round,pad=0.02,rounding_size=0.15", linewidth=1.2, edgecolor="#333", facecolor=fc
        )
        ax.add_patch(p)
        ax.text(x + w / 2, y + h / 2, text, ha="center", va="center", fontsize=9, wrap=True)

    box(0.4, 5.1, 2.4, 1.1, "Cliente\n(app móvil / web)", "#fff8e6")
    box(4.2, 5.1, 2.8, 1.1, "API REST + SignalR\n(EncorelyApi\n.NET 9)", "#e8f4fc")
    box(8.6, 5.4, 2.8, 0.8, "Servicios externos\nSpotify / Google", "#f0f0f0")

    box(3.5, 3.0, 2.5, 1.0, "PostgreSQL\n(Dapper)", "#e6f7e6")
    box(6.8, 3.0, 2.0, 1.0, "Redis\n(caché)", "#e6f7e6")
    box(9.4, 3.0, 2.2, 1.0, "Apache Kafka", "#e6f7e6")

    box(5.4, 1.0, 3.6, 1.0, "EncorelyWorker\n(consumidor de eventos)", "#fde8e8")

    for x1, y1, x2, y2 in [
        (2.8, 5.65, 4.2, 5.65),
        (7.0, 5.65, 8.6, 5.75),
        (5.6, 5.1, 4.75, 4.0),
        (5.6, 5.1, 7.8, 4.0),
        (5.6, 5.1, 10.5, 4.0),
        (10.5, 3.5, 7.2, 2.0),
    ]:
        ax.add_patch(FancyArrowPatch((x1, y1), (x2, y2), arrowstyle="-|>", mutation_scale=12, color="#444", lw=1.2))

    ax.text(6, 6.65, "JWT / HTTPS", fontsize=8, ha="center", style="italic")
    ax.text(6, 0.3, "Eventos de swipe → Kafka → refinamiento del perfil musical", fontsize=8, ha="center")

    fig.text(0.02, 0.02, "Fuente: elaboración propia a partir del código del repositorio.", fontsize=7, color="#555")
    fig.tight_layout()
    fig.savefig(path, dpi=160, bbox_inches="tight")
    plt.close(fig)


def draw_navigation(path: Path) -> None:
    fig, ax = plt.subplots(figsize=(8.5, 10))
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 11)
    ax.axis("off")

    nodes = [
        (5, 10.0, "Inicio / Auth\n(Spotify, Google, email)"),
        (5, 8.5, "Perfil\nGET /User/me"),
        (5, 7.0, "Sound Swipe\nnext-track + swipe"),
        (5, 5.5, "¿Umbral ≥ 25 swipes\ny perfil musical listo?"),
        (5, 4.0, "Funciones sociales"),
        (5, 2.5, "Radar GET /Events/{id}/matches\nMatches / Chat / Playlist DNA"),
        (5, 1.0, "Venue salas POST .../Venue/{eventId}/rooms\n+ SignalR /venueHub"),
    ]

    for x, y, t in nodes:
        w, h = 4.2, 0.95
        if "Funciones" in t:
            w, h = 3.6, 0.85
        p = FancyBboxPatch(
            (x - w / 2, y - h / 2),
            w,
            h,
            boxstyle="round,pad=0.02,rounding_size=0.12",
            linewidth=1.1,
            edgecolor="#333",
            facecolor="#eef6ff",
        )
        ax.add_patch(p)
        ax.text(x, y, t, ha="center", va="center", fontsize=8.5)

    chain = [(5, 9.55, 5, 8.9), (5, 8.05, 5, 7.45), (5, 6.55, 5, 5.95), (5, 5.05, 5, 4.45), (5, 3.55, 5, 3.05), (5, 2.05, 5, 1.45)]
    for x1, y1, x2, y2 in chain:
        ax.add_patch(FancyArrowPatch((x1, y1), (x2, y2), arrowstyle="-|>", mutation_scale=12, color="#444", lw=1.2))

    ax.text(5.7, 5.5, "Si no: seguir\nen swipe", fontsize=7.5, color="#a04444", ha="left")

    fig.text(0.02, 0.02, "Fuente: elaboración propia.", fontsize=7, color="#555")
    fig.tight_layout()
    fig.savefig(path, dpi=160, bbox_inches="tight")
    plt.close(fig)


def draw_er(path: Path) -> None:
    """Diagrama entidad-relación (modelo lógico + persistencia PostgreSQL vía Dapper)."""

    fig, ax = plt.subplots(figsize=(13.5, 9))
    ax.set_xlim(-0.2, 13.2)
    ax.set_ylim(-0.3, 10.3)
    ax.axis("off")
    ax.set_facecolor("#f8fafc")

    header_h = 0.42
    edge = "#1e3a5f"
    header_fill = "#1e40af"
    body_fill = "#ffffff"

    def er_entity(x: float, y: float, w: float, h: float, title: str, attrs: list[str], *, dashed: bool = False) -> tuple[float, float, float, float]:
        """Rectángulo (x,y) esquina inferior izquierda; devuelve bbox."""
        lw = 1.35 if not dashed else 1.2
        ls = "-" if not dashed else "--"
        body_h = h - header_h
        ax.add_patch(Rectangle((x, y), w, body_h, facecolor=body_fill, edgecolor=edge, linewidth=lw, linestyle=ls, zorder=2))
        ax.add_patch(Rectangle((x, y + body_h), w, header_h, facecolor=header_fill, edgecolor=edge, linewidth=lw, linestyle=ls, zorder=3))
        ax.text(x + w / 2, y + body_h + header_h / 2, title, ha="center", va="center", fontsize=9.5, fontweight="bold", color="white", zorder=4)
        yy = y + body_h - 0.12
        for line in attrs:
            ax.text(x + 0.1, yy, line, ha="left", va="top", fontsize=7.2, color="#1e293b", family="sans-serif", zorder=4)
            yy -= 0.255
        return (x, y, w, h)

    def anchor_bc(b: tuple[float, float, float, float]) -> tuple[float, float]:
        x, y, w, _ = b
        return (x + w / 2, y)

    def anchor_tc(b: tuple[float, float, float, float]) -> tuple[float, float]:
        x, y, w, h = b
        return (x + w / 2, y + h)

    def anchor_mr(b: tuple[float, float, float, float]) -> tuple[float, float]:
        x, y, w, h = b
        return (x + w, y + h / 2)

    def anchor_ml(b: tuple[float, float, float, float]) -> tuple[float, float]:
        x, y, _, h = b
        return (x, y + h / 2)

    def link(p1: tuple[float, float], p2: tuple[float, float], label: str | None = None, *, rad: float = 0.0, color: str = "#475569"):
        style = "arc3,rad=%s" % rad if rad != 0 else "arc3,rad=0"
        ax.add_patch(
            FancyArrowPatch(
                p1,
                p2,
                arrowstyle="-|>",
                mutation_scale=11,
                lw=1.35,
                color=color,
                linestyle="-",
                connectionstyle=style,
                shrinkA=4,
                shrinkB=4,
                zorder=1,
            )
        )
        if label:
            mx, my = (p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2
            ax.text(mx + 0.08, my + 0.08, label, fontsize=7.2, color="#0f172a", bbox=dict(boxstyle="round,pad=0.25", facecolor="#fef9c3", edgecolor="#eab308", linewidth=0.6), zorder=5)

    # --- Entidades (coordenadas en unidades del diagrama) ---
    users = er_entity(
        0.45,
        5.85,
        2.55,
        2.38,
        "Users",
        [
            "PK  Id",
            "SpotifyId, DisplayName, Email",
            "Provider, PasswordHash?",
            "SwipeCount, Mood",
            "CreatedAt",
            "RefreshToken?",
        ],
    )

    musical = er_entity(
        3.65,
        6.35,
        2.65,
        1.62,
        "MusicalProfiles",
        [
            "PK  Id",
            "FK  UserId → Users",
            "Energy, Danceability",
            "Valence, Tempo",
        ],
    )

    matches = er_entity(
        9.05,
        5.82,
        2.85,
        1.78,
        "Matches",
        [
            "PK  Id",
            "FK  UserId1 → Users",
            "FK  UserId2 → Users",
            "AffinityScore",
            "IsHighPriority",
            "CreatedAt",
        ],
    )

    swipes = er_entity(
        0.45,
        3.42,
        2.55,
        1.52,
        "Swipes",
        ["PK  Id", "FK  UserId → Users", "TrackId", "Direction", "CreatedAt"],
    )

    messages = er_entity(
        9.05,
        3.38,
        2.85,
        1.82,
        "Messages",
        ["PK  Id", "FK  MatchId → Matches", "FK  SenderId → Users", "Content", 'CreatedAt (persistencia)'],
    )

    venue_rooms = er_entity(
        4.05,
        0.45,
        3.05,
        1.45,
        "VenueRooms",
        ["PK  Id", "EventId (ref. externa)", "Name", "ExpiresAt (dominio)"],
    )

    venue_msg = er_entity(
        8.55,
        0.45,
        3.05,
        1.45,
        "VenueMessages",
        ["PK  Id", "FK  RoomId → VenueRooms", "FK  SenderId → Users", "Content", "IsModerated", "Timestamp"],
    )

    ev_ext = er_entity(
        4.55,
        2.05,
        2.55,
        0.78,
        "Evento (proveedor externo)",
        ["Identificador string (sin tabla local)"],
        dashed=True,
    )

    # --- Relaciones (flechas hacia la entidad referenciada por la FK) ---
    link(anchor_tc(swipes), anchor_bc(users), "N : 1")
    link(anchor_ml(musical), anchor_mr(users), "1 : 1")
    link(anchor_ml(matches), anchor_mr(users), "N : M\n(2 FK)", rad=0.12)
    link(anchor_tc(messages), anchor_bc(matches), "N : 1")
    link(anchor_ml(messages), anchor_mr(users), "N : 1\n(remitente)", rad=-0.18)
    link(anchor_tc(venue_rooms), anchor_bc(ev_ext), "N : 1\n(ref.)", color="#b45309")
    link(anchor_ml(venue_msg), anchor_mr(venue_rooms), "N : 1")
    link(anchor_ml(venue_msg), anchor_mr(users), "N : 1\n(remitente)", rad=0.15)

    # Leyenda
    leg_x, leg_y = 0.45, 0.05
    ax.add_patch(FancyBboxPatch((leg_x, leg_y), 6.6, 1.18, boxstyle="round,pad=0.02,rounding_size=0.15", facecolor="#eff6ff", edgecolor="#64748b", linewidth=1))
    ax.text(leg_x + 0.15, leg_y + 0.72, "Leyenda", fontsize=8.5, fontweight="bold", color="#0f172a")
    ax.text(
        leg_x + 0.15,
        leg_y + 0.42,
        "PK = clave primaria   |   FK = clave foránea   |   Flechas apuntan a la entidad referenciada",
        fontsize=7.3,
        color="#334155",
    )
    ax.text(
        leg_x + 0.15,
        leg_y + 0.18,
        "Sin tabla Event local: EventId enlaza el catálogo externo. Persistencia VenueRooms: revisar columnas frente al modelo de dominio.",
        fontsize=6.9,
        color="#475569",
    )

    ax.text(
        6.55,
        9.65,
        "Modelo entidad-relación — Encorely (PostgreSQL, esquema referido por repositorios Dapper)",
        fontsize=10,
        ha="center",
        fontweight="bold",
        color="#0f172a",
    )

    fig.text(0.015, 0.012, "Fuente: EncorelyModels, INSERT en EncorelyRepository y contratos de dominio.", fontsize=7.5, color="#475569")
    fig.tight_layout()
    fig.savefig(path, dpi=185, bbox_inches="tight", facecolor=fig.get_facecolor())
    plt.close(fig)


class DocPDF(FPDF):
    def __init__(self, font_regular: str, font_bold: str):
        super().__init__()
        self.font_regular = font_regular
        self.font_bold = font_bold
        # Márgenes ~2,54 cm (1 pulgada), estilo APA
        self.set_margins(left=25, top=20, right=25)
        self.set_auto_page_break(auto=True, margin=22)
        self.add_font("Doc", "", font_regular)
        self.add_font("Doc", "B", font_bold)

    def header(self):
        if self.page_no() <= 1:
            return
        self.set_font("Doc", "", BODY_PT)
        self.set_x(self.l_margin)
        usable = self.w - self.r_margin - self.l_margin
        half = usable / 2
        self.cell(half, 9, "ENCORELY", align="L")
        self.cell(half, 9, str(self.page_no()), align="R")
        self.ln(11)

    def footer(self):
        pass

    def section_title(self, text: str, level: int = 1):
        self.set_x(self.l_margin)
        self.set_font("Doc", "B", BODY_PT)
        self.multi_cell(0, LINE_BODY, text)
        self.ln(2)
        self.set_x(self.l_margin)

    def body_text(self, text: str):
        self.set_x(self.l_margin)
        self.set_font("Doc", "", BODY_PT)
        self.multi_cell(0, LINE_BODY, text)
        self.ln(3)
        self.set_x(self.l_margin)

    def bullet(self, items: list[str]):
        self.set_font("Doc", "", BODY_PT)
        for it in items:
            self.set_x(self.l_margin)
            self.multi_cell(0, LINE_BODY, "- " + it)
        self.ln(2)
        self.set_x(self.l_margin)


def title_page(pdf: DocPDF):
    pdf.add_page()
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Doc", "B", BODY_PT)
    pdf.ln(36)
    pdf.multi_cell(0, LINE_BODY, "Encorely", align="C")
    pdf.ln(6)
    pdf.set_font("Doc", "", BODY_PT)
    pdf.multi_cell(0, LINE_BODY, "It's the beat, don't go alone.", align="C")
    pdf.ln(14)
    pdf.set_font("Doc", "", BODY_PT)
    for line in ("Emmanuel Dávila", "Juan Diego Calle", "Camilo Rincón"):
        pdf.multi_cell(0, LINE_BODY, line, align="C")
        pdf.ln(2)
    pdf.ln(10)
    pdf.multi_cell(0, LINE_BODY, "Universidad Tecnológica de Antioquia", align="C")
    pdf.ln(12)
    pdf.multi_cell(0, LINE_BODY, "Medellín, Colombia mayo, 2026", align="C")


def main():
    fr, fb = document_font_paths()
    arch_png, nav_png, er_png = ensure_assets()

    pdf = DocPDF(fr, fb)
    title_page(pdf)

    # --- Sección 1 ---
    pdf.add_page()
    pdf.section_title("1. Definición del proyecto")
    pdf.section_title("1.1. Título del proyecto", 2)
    pdf.body_text(
        "It's the beat, don't go alone. El backend Encorely es el motor de servicios para esta aplicación social "
        "orientada a conciertos y festivales, que modela el \"ADN musical\" de cada persona a partir de interacciones "
        "tipo swipe sobre pistas de audio y calcula afinidad entre usuarios para habilitar radar de compatibilidad, "
        "conversaciones y salas grupales asociadas a un evento."
    )

    pdf.section_title("1.2. Descripción general del problema a resolver", 2)
    pdf.body_text(
        "En eventos en vivo es difícil identificar de forma rápida y objetiva a personas con gustos musicales "
        "compatibles. Las redes sociales genéricas no utilizan señales auditivas ni comportamiento de escucha; "
        "Encorely propone inferir preferencias reales mediante swipes sobre tracks y características de audio "
        "(energía, bailabilidad, valencia, tempo), aplicar un umbral mínimo de interacciones para estabilizar el perfil "
        "y ofrecer compatibilidad cuantificada (distancia euclidiana / umbral de afinidad) antes de exponer contacto "
        "social sensible como chat o radar por evento."
    )

    pdf.section_title("1.3. Público objetivo", 2)
    pdf.bullet(
        [
            "Personas que asisten a conciertos o festivales y desean conocer a otras personas con afinidad musical verificada.",
            "Usuarios que ya utilizan Spotify u otros proveedores integrados para autenticación y enriquecimiento de datos.",
            "Equipos de producto que integran el backend mediante REST y canales en tiempo real (SignalR).",
        ]
    )

    pdf.section_title("1.4. Alcance del proyecto", 2)
    pdf.body_text("Incluye:")
    pdf.bullet(
        [
            "API REST en ASP.NET Core (.NET 9) con documentación Swagger en la ruta /docs.",
            "Persistencia en PostgreSQL mediante Dapper; modelo de usuarios, swipes, perfiles musicales, matches, mensajes y venue.",
            "Arquitectura orientada a eventos con Apache Kafka y worker asíncrono (EncorelyWorker) para procesar swipes.",
            "Autenticación JWT y login multi-proveedor (Spotify, Google, correo).",
            "SignalR: hub de notificaciones y hub de salas de venue.",
            "Integración con Spotify para tracks y generación de playlist \"DNA mix\" entre dos usuarios.",
            "Caché Redis configurada en la API.",
        ]
    )
    pdf.body_text("No incluye (fuera de alcance de este repositorio backend):")
    pdf.bullet(
        [
            "Aplicación cliente completa (interfaz móvil o web); solo se define la superficie de API y tiempo real.",
            "Gestión comercial de venta de entradas o pagos.",
            "Moderación avanzada basada en IA (existe moderación básica por palabras clave en venue).",
            "Garantías legales de uso de datos de terceros más allá de lo implementado en integraciones.",
        ]
    )

    # --- Sección 2 ---
    pdf.add_page()
    pdf.section_title("2. Análisis de requerimientos")
    pdf.section_title("2.1. Requerimientos funcionales", 2)
    pdf.bullet(
        [
            "Autenticación: login/registro con Spotify, Google y correo electrónico; emisión de respuestas coherentes con IdentityService.",
            "Gestión de perfil: consultar datos del usuario y actualizar estado de ánimo (Moshpit, Chill, VIP).",
            "Swipes: obtener siguiente track sugerido y registrar swipe (like/dislike/superlike) disparando procesamiento asíncrono.",
            "Eventos: listar feed de eventos cercanos y obtener candidatos compatibles para un evento con filtro de mood opcional.",
            "Umbral social: el radar de matches por evento exige al menos 25 swipes registrados.",
            "Matches: listar pendientes, aceptar match y obtener identificador de sala de chat.",
            "Chat 1-1: leer historial y enviar mensajes asociados a un match (con validaciones según proveedor).",
            "Venue: crear sala temporal por evento, listar y publicar mensajes; moderación manual vía endpoint DELETE.",
            "Playlist compartida: generar mezcla DNA entre dos usuarios con tokens de Spotify.",
            "Tiempo real: notificaciones de match y ADN completado; difusión de mensajes de venue por SignalR.",
        ]
    )

    pdf.section_title("2.2. Requerimientos no funcionales (deseables)", 2)
    pdf.bullet(
        [
            "Seguridad: JWT bearer, HTTPS en despliegue, variables de entorno para secretos y cadena de base de datos.",
            "Rendimiento y escalabilidad: desacoplamiento vía Kafka para cómputo pesado de perfil musical; Redis para caché.",
            "Disponibilidad: posibilidad de desplegar en contenedores (Docker Compose documentado).",
            "Usabilidad del contrato API: Swagger UI en /docs; nombres camelCase en JSON.",
            "Mantenibilidad: capas Domain, Application, Infrastructure, Api y Worker.",
            "Observabilidad (mejora sugerida en el proyecto): health checks y dead-letter para Kafka.",
            "Privacidad: minimizar datos en logs; moderación de contenido grupal básica.",
        ]
    )

    # --- Sección 3 ---
    pdf.add_page()
    pdf.section_title("3. Diseño de la solución")
    pdf.section_title("3.1. Arquitectura general", 2)
    pdf.image(arch_png, x=12, w=186)
    pdf.ln(6)
    pdf.body_text(
        "La aplicación cliente consume REST y WebSockets (SignalR). La API orquesta casos de uso, persiste en PostgreSQL "
        "y publica eventos a Kafka; el worker consume dichos eventos para actualizar el perfil musical. Redis actúa como "
        "caché distribuida según la configuración en Program.cs."
    )

    pdf.add_page()
    pdf.section_title("3.2. Diagrama de flujo de navegación de la app", 2)
    pdf.image(nav_png, x=10, w=190)
    pdf.ln(4)
    pdf.body_text(
        "Flujo simplificado: autenticación, construcción del perfil mediante swipes, comprobación del umbral de 25 interacciones "
        "y habilitación de funciones sociales (radar, chat, venue, playlist)."
    )

    pdf.add_page()
    pdf.section_title("3.3. Modelo entidad-relación de la base de datos", 2)
    pdf.image(er_png, x=8, w=194)
    pdf.ln(4)
    pdf.body_text(
        "El diagrama muestra cada tabla con atributos PK/FK, cardinalidades en las relaciones y el evento musical como "
        "entidad externa (sin fila local). Las flechas siguen la dirección referencial habitual (hacia la entidad "
        "referenciada). El esquema físico en PostgreSQL nombra tablas entre comillas (por ejemplo \"Users\", \"Swipes\") "
        "y las FK se materializan mediante los campos indicados en los INSERT de los repositorios Dapper."
    )

    pdf.add_page()
    pdf.section_title("3.4. Especificación de endpoints de la API", 2)
    endpoints = [
        ("POST", "/api/v1/Auth/spotify", "Cuerpo JSON: token (string).", "Login con token de Spotify; respuesta 202 Accepted."),
        ("POST", "/api/v1/Auth/google", "Cuerpo JSON: token (string).", "Login con Google Identity Token; 202 Accepted."),
        ("POST", "/api/v1/Auth/register", "Cuerpo: email, password.", "Registro por correo; 202 Accepted."),
        ("POST", "/api/v1/Auth/login", "Cuerpo: email, password.", "Login por correo; 200 OK."),
        ("GET", "/api/v1/User/me", "Query obligatorio: userId (GUID).", "Perfil, proveedor, swipeCount y mood."),
        ("PUT", "/api/v1/User/settings", "Cuerpo: userId, mood (enum de concierto).", "Actualización de preferencia; 204 No Content."),
        ("GET", "/api/v1/Swipes/next-track", "Query: userId.", "Siguiente track para evaluar."),
        ("POST", "/api/v1/Swipes/interactions/swipe", "Cuerpo: userId, trackId, direction (enum).", "Registra swipe; 202 Accepted."),
        ("GET", "/api/v1/Events/feed", "Sin parámetros.", "Lista de eventos cercanos."),
        ("GET", "/api/v1/Events/{eventId}/matches", "Query: userId; opcional targetMood.", "Radar de compatibles; 403 si swipeCount < 25."),
        ("GET", "/api/v1/Chat/{roomId}/messages", "Query: userId.", "Historial de mensajes del match."),
        ("POST", "/api/v1/Chat/{matchId}/messages", "Query: userId; cuerpo: texto plano.", "Envío de mensaje; respuesta con id y marca temporal."),
        ("GET", "/api/v1/Matches/pending", "Query: userId.", "Matches pendientes de aceptación."),
        ("POST", "/api/v1/Matches/{matchId}/accept", "Query: userId.", "Aceptar match; cuerpo de respuesta incluye roomId."),
        ("POST", "/api/v1/Venue/{eventId}/rooms", "Query: name; opcional durationHours (por defecto 4).", "Crea sala temporal ligada al evento."),
        ("GET", "/api/v1/Venue/{roomId}/messages", "Sin query obligatorio.", "Mensajes no moderados de la sala."),
        ("POST", "/api/v1/Venue/{roomId}/messages", "Query: userId; cuerpo: texto.", "Publicar mensaje; puede moderarse por palabras clave."),
        ("DELETE", "/api/v1/Venue/messages/{messageId}", "Query opcional: reason.", "Moderación manual de un mensaje."),
        ("POST", "/api/v1/Playlist/dna-mix", "Query: userId1, userId2, accessToken1, accessToken2.", "Genera playlist compartida tipo DNA mix."),
    ]
    for method, route, params, desc in endpoints:
        pdf.set_x(pdf.l_margin)
        pdf.set_font("Doc", "B", BODY_PT)
        pdf.multi_cell(0, LINE_BODY, f"{method}  {route}")
        pdf.set_x(pdf.l_margin)
        pdf.set_font("Doc", "", BODY_PT)
        pdf.multi_cell(0, LINE_BODY, f"{params} {desc}")
        pdf.ln(2)

    pdf.ln(4)
    pdf.set_font("Doc", "B", BODY_PT)
    pdf.multi_cell(0, LINE_BODY, "SignalR (WebSockets)")
    pdf.set_font("Doc", "", BODY_PT)
    pdf.bullet(
        [
            "/notificationHub — grupos por usuario; eventos MatchFound, DnaCompleted.",
            "/venueHub — salas de venue; métodos JoinVenueRoom, LeaveVenueRoom, SendVenueMessage; eventos ReceiveVenueMessage, UserJoined, UserLeft.",
        ]
    )

    pdf.add_page()
    pdf.section_title("Referencias")
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Doc", "", BODY_PT)
    pdf.multi_cell(
        0,
        LINE_BODY,
        "Equipo Encorely. (2026). Encorely backend [Software]. Repositorio del proyecto. "
        "Documentación interactiva: Swagger UI en /docs cuando la API está en ejecución.",
    )

    pdf.output(OUT_PDF)
    print(f"Generado: {OUT_PDF}")


if __name__ == "__main__":
    main()
