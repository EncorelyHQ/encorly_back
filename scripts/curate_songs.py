#!/usr/bin/env python3
"""Genera src/Encorely.Api/Data/Seed/songs.json a partir de una lista curada de canciones.

Atribución: Previews de 30 s vía iTunes Search API, uso académico.
Solo stdlib. Uso: python3 scripts/curate_songs.py
Pausa ~1 s entre llamadas y reintenta con backoff ante 403/429 (límite de la API pública de iTunes).
Los candidatos que no aparecen en iTunes se omiten: hay candidatos de sobra por género.
"""
import json
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src/Encorely.Api/Data/Seed/songs.json"
GENRES = ROOT / "src/Encorely.Api/Data/Seed/genres.json"

# (título, artista, slug de género). Peso mayor en reguetón, vallenato, salsa, champeta, cumbia,
# pop latino, rock en español e indie colombiano; el resto con varios candidatos de reserva.
CATALOG = [
    # reggaeton
    ("Provenza", "Karol G", "reggaeton"), ("Bichota", "Karol G", "reggaeton"), ("Mi Gente", "J Balvin", "reggaeton"),
    ("Ginza", "J Balvin", "reggaeton"), ("Ay Vamos", "J Balvin", "reggaeton"), ("Normal", "Feid", "reggaeton"),
    ("Chorrito Pa las Animas", "Feid", "reggaeton"), ("Tusa", "Karol G", "reggaeton"), ("Gasolina", "Daddy Yankee", "reggaeton"),
    ("Pepas", "Farruko", "reggaeton"), ("Dákiti", "Bad Bunny", "reggaeton"), ("Sensualidad", "Bad Bunny", "reggaeton"),
    ("Hawái", "Maluma", "reggaeton"), ("Felices los 4", "Maluma", "reggaeton"),
    ("Ojitos Lindos", "Bad Bunny", "reggaeton"), ("Si Antes Te Hubiera Conocido", "Karol G", "reggaeton"),
    ("Qué Más Pues", "J Balvin", "reggaeton"),
    # trap-latino
    ("Mamichula", "Trueno", "trap-latino"), ("Mala Mía", "Anuel AA", "trap-latino"), ("Te Boté", "Casper Mágico", "trap-latino"),
    ("Soltera", "Lunay", "trap-latino"), ("Baila Baila Baila", "Ozuna", "trap-latino"), ("Ella Quiere Beber", "Anuel AA", "trap-latino"),
    # pop-latino
    ("Waka Waka", "Shakira", "pop-latino"), ("Hips Don't Lie", "Shakira", "pop-latino"), ("La Bicicleta", "Carlos Vives", "pop-latino"),
    ("Bzrp Music Sessions, Vol. 53", "Shakira", "pop-latino"), ("Besos en Guerra", "Morat", "pop-latino"),
    ("Cómo Te Atreves", "Morat", "pop-latino"), ("Eres Mi Todo", "Fonseca", "pop-latino"), ("Te Mando Flores", "Fonseca", "pop-latino"),
    ("Bailando", "Enrique Iglesias", "pop-latino"), ("Vivir Mi Vida", "Marc Anthony", "pop-latino"),
    ("Fotografía", "Juanes", "pop-latino"), ("A Dios le Pido", "Juanes", "pop-latino"),
    # vallenato
    ("Cásate Conmigo", "Silvestre Dangond", "vallenato"), ("Cásate Conmigo", "Silvestre Dangond", "vallenato"),
    ("Dime Cómo", "Silvestre Dangond", "vallenato"), ("Mi Sueño", "Silvestre Dangond", "vallenato"),
    ("Pa' Mayte", "Carlos Vives", "vallenato"), ("Volví a Nacer", "Carlos Vives", "vallenato"), ("La Gota Fría", "Carlos Vives", "vallenato"),
    ("Fruta Fresca", "Carlos Vives", "vallenato"), ("La Tierra del Olvido", "Carlos Vives", "vallenato"),
    ("Mi Dios Lo Hizo", "Peter Manjarrés", "vallenato"), ("Te Quiero", "Peter Manjarrés", "vallenato"),
    ("Por Ella", "Jessi Uribe", "vallenato"), ("Nada Fue Un Error", "Jessi Uribe", "vallenato"),
    ("Cumbiamba", "Diomedes Díaz", "vallenato"), ("Mi Muchacha", "Diomedes Díaz", "vallenato"),
    ("Tarde Lo Conocí", "Diomedes Díaz", "vallenato"), ("Me Voy a Tomar el Mundo", "Jorge Celedón", "vallenato"),
    # salsa
    ("Cali Pachanguero", "Grupo Niche", "salsa"), ("Una Aventura", "Grupo Niche", "salsa"), ("Gotas de Lluvia", "Grupo Niche", "salsa"),
    ("Sin Sentimientos", "Grupo Niche", "salsa"), ("La Rebelión", "Joe Arroyo", "salsa"), ("En Barranquilla Me Quedo", "Joe Arroyo", "salsa"),
    ("Tania", "Joe Arroyo", "salsa"), ("Pedro Navaja", "Rubén Blades", "salsa"), ("La Vida Es Un Carnaval", "Celia Cruz", "salsa"),
    ("Quimbara", "Celia Cruz", "salsa"), ("Aguanile", "Héctor Lavoe", "salsa"), ("El Cantante", "Héctor Lavoe", "salsa"),
    ("Hasta Ayer", "Marc Anthony", "salsa"),
    # champeta
    ("El Serrucho", "Mr. Black", "champeta"), ("El Clavo", "Mr. Black", "champeta"), ("Apretaito al Pickup", "Mr. Black", "champeta"),
    ("Bandida", "Mr. Black", "champeta"), ("Yo Voy Ganao", "Systema Solar", "champeta"),
    ("La Invité a Bailar", "Kevin Flórez", "champeta"), ("Asómate a la Ventana", "Kevin Flórez", "champeta"),
    ("El Chocho", "Charles King", "champeta"), ("El Martillo", "Charles King", "champeta"), ("La Cucharita", "Charles King", "champeta"),
    ("Pacha Pacha", "Mr. Black", "champeta"), ("Kuchá Kuchá", "Mr. Black", "champeta"), ("La Champeta", "Systema Solar", "champeta"),
    # cumbia
    ("La Pollera Colorá", "Totó la Momposina", "cumbia"), ("Curura", "Totó la Momposina", "cumbia"),
    ("Cumbia Sobre el Mar", "Quantic", "cumbia"), ("Se Me Perdió la Cadenita", "Los Corraleros de Majagual", "cumbia"),
    ("Caribe Soy", "Bomba Estéreo", "cumbia"), ("Cómo Te Voy a Olvidar", "Los Ángeles Azules", "cumbia"),
    ("Mil Horas", "Los Ángeles Azules", "cumbia"), ("17 Años", "Los Ángeles Azules", "cumbia"),
    ("El Año Viejo", "Los Hispanos", "cumbia"), ("La Cumbia Cienaguera", "Rodolfo y su Típica", "cumbia"),
    ("Mi Cucu", "Los Gaiteros de San Jacinto", "cumbia"), ("Las Pilanderas", "Totó la Momposina", "cumbia"),
    # bachata
    ("Obsesión", "Aventura", "bachata"), ("Eres Mía", "Romeo Santos", "bachata"), ("Propuesta Indecente", "Romeo Santos", "bachata"),
    ("Darte un Beso", "Prince Royce", "bachata"), ("Stand by Me", "Prince Royce", "bachata"),
    ("Bachata Rosa", "Juan Luis Guerra", "bachata"), ("Burbujas de Amor", "Juan Luis Guerra", "bachata"),
    # rock-en-espanol
    ("Florecita Rockera", "Aterciopelados", "rock-en-espanol"), ("Bolero Falaz", "Aterciopelados", "rock-en-espanol"),
    ("Siembra", "Aterciopelados", "rock-en-espanol"), ("Persiana Americana", "Soda Stereo", "rock-en-espanol"),
    ("De Música Ligera", "Soda Stereo", "rock-en-espanol"), ("La Célula Que Explota", "Enanitos Verdes", "rock-en-espanol"),
    ("Lamento Boliviano", "Enanitos Verdes", "rock-en-espanol"), ("Rayando el Sol", "Maná", "rock-en-espanol"),
    ("Oye Mi Amor", "Maná", "rock-en-espanol"), ("Mala Hierba", "Juanes", "rock-en-espanol"), ("Es Por Ti", "Juanes", "rock-en-espanol"),
    ("La Camisa Negra", "Juanes", "rock-en-espanol"),
    # indie-alternativo
    ("Hasta el Fin", "Monsieur Periné", "indie-alternativo"), ("La Muerte", "Monsieur Periné", "indie-alternativo"),
    ("Mi Libertad", "Monsieur Periné", "indie-alternativo"), ("Nuestra Canción", "Monsieur Periné", "indie-alternativo"),
    ("Soy Yo", "Bomba Estéreo", "indie-alternativo"), ("Fuego", "Bomba Estéreo", "indie-alternativo"),
    ("Ojos Así", "Bomba Estéreo", "indie-alternativo"), ("Telepatía", "Kali Uchis", "indie-alternativo"),
    ("Después de la Playa", "Bomba Estéreo", "indie-alternativo"), ("Ojalá", "Esteman", "indie-alternativo"),
    ("Ahora Me Doy Cuenta", "Esteman", "indie-alternativo"), ("Amor Cobarde", "Diamante Eléctrico", "indie-alternativo"),
    ("Aquí y Ahora", "Diamante Eléctrico", "indie-alternativo"),
    # electronica
    ("Titanium", "David Guetta", "electronica"), ("Don't You Worry Child", "Swedish House Mafia", "electronica"),
    ("Strobe", "deadmau5", "electronica"), ("Levels", "Avicii", "electronica"), ("Wake Me Up", "Avicii", "electronica"),
    ("Animals", "Martin Garrix", "electronica"), ("Lean On", "Major Lazer", "electronica"),
    # hip-hop
    ("Somos Pacífico", "ChocQuibTown", "hip-hop"), ("De Donde Vengo Yo", "ChocQuibTown", "hip-hop"), ("Oye Mujer", "ChocQuibTown", "hip-hop"),
    ("Pa' Olvidarte", "ChocQuibTown", "hip-hop"), ("Lose Yourself", "Eminem", "hip-hop"),
    ("Es Épico", "Canserbero", "hip-hop"), ("Quién Dijo Que No Se Puede", "Residente", "hip-hop"),
    ("Atrévete-te-te", "Calle 13", "hip-hop"), ("Gangsta's Paradise", "Coolio", "hip-hop"),
    # musica-del-pacifico
    ("Te Invito", "Herencia de Timbiquí", "musica-del-pacifico"), ("Sabrás", "Herencia de Timbiquí", "musica-del-pacifico"),
    ("Vive a Tu Manera", "Herencia de Timbiquí", "musica-del-pacifico"), ("Coca por Coco", "Herencia de Timbiquí", "musica-del-pacifico"),
    ("La Juana", "Grupo Canalon de Timbiquí", "musica-del-pacifico"), ("Tío Guachupesito", "Grupo Canalon de Timbiquí", "musica-del-pacifico"),
    ("Ay Lucecita", "Las Cantadoras de Pogue", "musica-del-pacifico"), ("Salte", "Alexis Play", "musica-del-pacifico"),
    # popular
    ("Mi Primer Millón", "Yeison Jiménez", "popular"), ("Aunque Me Duela", "Yeison Jiménez", "popular"),
    ("La Cama Vacía", "Paola Jara", "popular"), ("Cómo Me Duele", "Paola Jara", "popular"),
    ("Y Qué Hago Yo", "Paola Jara", "popular"), ("Cuando Te Vayas", "Paola Jara", "popular"),
    ("Cantinero", "Darío Gómez", "popular"), ("Nadie es Eterno", "Darío Gómez", "popular"),
    ("Voy a Beber", "Jessi Uribe", "popular"), ("Esa Mujer", "Yeison Jiménez", "popular"),
    ("Ron y Vino", "Giovanny Ayala", "popular"), ("Aventurero", "Yeison Jiménez", "popular"),
    ("Por Qué la Envidia", "Yeison Jiménez", "popular"), ("Murió el Amor", "Paola Jara", "popular"),
    ("Salud por Él", "Paola Jara", "popular"), ("Mala Mujer", "Paola Jara", "popular"), ("Como Si Nada", "Paola Jara", "popular"),
    # regional-mexicano
    ("Ella Baila Sola", "Peso Pluma", "regional-mexicano"), ("Qué Pasaría", "Fuerza Regida", "regional-mexicano"),
    ("El Rey", "Vicente Fernández", "regional-mexicano"), ("Volver Volver", "Vicente Fernández", "regional-mexicano"),
    ("Mi Ex Tenía Razón", "Grupo Frontera", "regional-mexicano"), ("El Azul", "Junior H", "regional-mexicano"),
    ("La Chona", "Los Tucanes de Tijuana", "regional-mexicano"), ("Adiós Amor", "Christian Nodal", "regional-mexicano"),
    ("La Puerta Negra", "Los Tigres del Norte", "regional-mexicano"),
]

HEADERS = {"User-Agent": "encorely-seed-curation/1.0 (academic project)"}


def norm(text: str) -> str:
    """Minúsculas, sin acentos ni puntuación, espacios colapsados."""
    text = unicodedata.normalize("NFD", text.lower())
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return " ".join("".join(c if c.isalnum() else " " for c in text).split())


def search(term: str, country: str) -> list[dict]:
    query = urllib.parse.urlencode({"term": term, "country": country, "media": "music", "entity": "song", "limit": 10})
    url = f"https://itunes.apple.com/search?{query}"
    delay = 20
    for _ in range(5):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=20) as r:
                return json.load(r).get("results", [])
        except urllib.error.HTTPError as e:
            if e.code not in (403, 429):
                return []
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            pass
        time.sleep(delay)
        delay *= 2
    return []


def pick(title: str, artist: str, results: list[dict]) -> dict | None:
    t, a = norm(title), norm(artist)
    for r in results:
        rt, ra = norm(r.get("trackName", "")), norm(r.get("artistName", ""))
        if (a in ra or ra in a) and t in rt and r.get("previewUrl") and r.get("artworkUrl100"):
            return r
    return None


def main() -> None:
    valid = {g["slug"] for g in json.loads(GENRES.read_text(encoding="utf-8"))}
    # Incremental: lo ya resuelto en una corrida previa se conserva (la API pública limita con 403 en rachas).
    songs = json.loads(OUT.read_text(encoding="utf-8")) if OUT.exists() else []
    seen_ids = {s["iTunesTrackId"] for s in songs}
    seen_keys = {(norm(s["title"]), norm(s["artist"])) for s in songs}
    for title, artist, genre in CATALOG:
        assert genre in valid, genre
        key = (norm(title), norm(artist))
        if key in seen_keys:
            continue
        seen_keys.add(key)
        hit = None
        for country in ("CO", "US"):
            hit = pick(title, artist, search(f"{artist} {title}", country))
            time.sleep(1)
            if hit:
                break
        if not hit or hit["trackId"] in seen_ids:
            print(f"  sin resultado: {artist} - {title}", flush=True)
            continue
        seen_ids.add(hit["trackId"])
        songs.append({
            "title": title,
            "artist": artist,
            "genre": genre,
            "coverUrl": hit["artworkUrl100"].replace("100x100bb", "600x600bb"),
            "previewUrl": hit["previewUrl"],
            "iTunesTrackId": hit["trackId"],
        })
        print(f"ok {genre:20} {artist} - {title}", flush=True)
    OUT.write_text(json.dumps(songs, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    per_genre = {g: sum(s["genre"] == g for s in songs) for g in sorted(valid)}
    print(f"\n{len(songs)} canciones; por género: {per_genre}")
    low = [g for g, n in per_genre.items() if n < 3]
    if low:
        print(f"ATENCIÓN: géneros con menos de 3: {low}")


if __name__ == "__main__":
    main()
