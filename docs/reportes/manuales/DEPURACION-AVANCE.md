# Depuración por bloque del manual técnico (secciones del avance)

Documento: `MANUAL-TECNICO-ENCORELY.md`, de la v1.3 (respaldo intacto en `MANUAL-TECNICO-ENCORELY.v1.3.md`) a la versión depurada. Fecha: 2026-10-22.

Alcance: las secciones que el avance muestra con contenido (listas `INCLUIDAS` y `EXCLUIDAS_DENTRO` de `generar_avance.py`): 1.1–1.4, 2.1, 2.2, 3.1, 3.2, 4.1 (introducción y 4.1.1–4.1.3), 4.2, 4.3, 5.1, 5.2, 5.8, 6.1–6.4, 7.1, 7.2, 8.1–8.3, 9.1, 9.7, 10.1 y 10.2. Ningún capítulo tiene texto entre el `## N.` y su primera `###`, así que no hubo introducciones de capítulo que depurar.

Decks (se citan como `deck · n.º de diapositiva · frase`):
- **MT**: *Manual Técnico y Análisis del Software* (n.º impreso en la diapositiva: 4 a 15).
- **OP**: *Operatividad del Sistema* (n.º impreso: 03 a 12).
- **DS**: *Despliegue del Software* (n.º del pie `n / 09`).
- **BD**: *Estructura de la BD y Tablas de Contenido* (n.º del pie `n / 10`).
- **UD**: *Manual de usuario y diagramas de despliegue* (n.º impreso: 04 a 14).

Criterio: un bloque (párrafo, frase, viñeta, fila, columna, nota, diagrama o etiqueta de diagrama) se queda solo si una diapositiva pide ese tipo de información y el bloque es el dato de Encorely que la responde. Lo que se va lleva su tipo entre corchetes:
`[Implementación]` detalle de implementación o de comportamiento no pedido · `[Remisión]` puntero a otro documento o sección · `[Justificación]` porqué de una decisión o de un dato · `[Columna]` columna de tabla que no es un atributo pedido · `[Historia]` historia, verificación puntual o decisión interna (D1–D16) · `[Meta]` frase de transición o explicación del propio documento · `[Advertencia]` aviso para desarrolladores · `[Fila]` fila que ninguna diapositiva pide · `[Comparación]` lo que el sistema no usa o no hace.

## Resumen

| Capítulo | Bloques quitados |
|---|---|
| 1. Control documental | 13 |
| 2. Objetivo, alcance y descripción | 4 |
| 3. Requisitos | 7 |
| 4. Arquitectura | 9 |
| 5. Instalación y despliegue | 12 |
| 6. Base de datos | 39 |
| 7. Integraciones y APIs | 6 |
| 8. Seguridad | 7 |
| 9. Operación | 5 |
| 10. Incidentes | 2 |
| **Total** | **104** |

Ajustes de referencia fuera del avance (no son depuración): 5.5.1 y 5.7 nombraban "D15", que solo se definía en el párrafo quitado de 2.2; quedan como "(ver 2.2)" y "No aplica en esta entrega; preparado".

---

## Capítulo 1

### 1.1 Control documental

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Filas Versión vigente, Fecha de emisión, Elaborado por, Aprobado por | Se queda | MT · 5 · "Control de versiones: fecha, versión, autor, cambios y aprobaciones" |
| Filas Responsable de mantenerlo, Periodicidad de actualización | Se queda | MT · 5 · "Definir responsables y periodicidad de actualización" |
| Filas Código del documento, Sistema, Tipo de documento | Se queda (con duda) | MT · 6 · "Portada, control documental e historial de cambios": identifican el documento controlado, pero ninguna diapositiva enumera esos campos. "Tipo de documento" además lo usa `generar_avance.py` para marcar el avance |
| Fila "Docente \| Cristian Alzate Botero" | Se va | [Fila] Ninguna diapositiva pide el docente en el control documental |
| Fila "Base del código documentado \| … commit `aaa3dfc` …" | Se va | [Historia] Trazabilidad del código, no del documento; no pedida |
| Fila "Contrato de la API \| `v1.0.0-draft.17` …" | Se va | [Fila] Versión del contrato, no del documento |
| Fila "Estado del sistema \| … (decisión D15)" | Se va | [Historia] Referencia a decisión interna D15 |
| Fila "Revisado por \| Equipo Encorely (revisión cruzada…)" | Se va | [Fila] MT · 5 pide "aprobaciones", no revisión |
| Fila "Ubicación del original \| `reportes/manuales/…`" | Se va | [Fila] No pedida |
| Fila "Clasificación \| Uso académico… (ver 1.7)" | Se va | [Fila] No pedida; además remitía a 1.7, fuera del avance |

### 1.2 Historial de cambios

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla completa (Versión, Fecha, Autores, Cambios, Aprobación; filas 0.1 a 1.3) | Se queda | MT · 5 · "Control de versiones: fecha, versión, autor, cambios y aprobaciones" |

### 1.3 Propósito y público del documento

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| "Este manual explica cómo está construido, configurado, desplegado y mantenido Encorely." | Se queda | MT · 4 · "Documento que explica cómo está construido, configurado, desplegado y mantenido un sistema" |
| "Está dirigido a desarrollo (back y front), administración de base de datos e infraestructura y operación (DevOps)." | Se queda (frase mínima: es la columna Público de la tabla quitada, sin texto nuevo) | MT · 4 · "comprensible para desarrolladores, administradores, soporte y responsables de infraestructura" |
| Tabla de públicos, columna "Qué busca aquí" | Se va | [Columna] No es un atributo pedido |
| Tabla de públicos, columna "Capítulos" | Se va | [Columna] No pedida; al quedar solo "Público", la tabla pasa a la frase de arriba |
| Fila "Seguridad y cumplimiento \| Perfiles, permisos…" | Se va | [Fila] MT · 4 no nombra ese público |
| Párrafo "El análisis del software (problema, requisitos…) está en el documento complementario… Cuando este documento y el código no coinciden, manda el código. Las fuentes que se usaron…" | Se va | [Remisión] y [Meta]: puntero a otro documento, regla de precedencia y fuentes del redactor; ninguna diapositiva los pide |

### 1.4 Responsables y periodicidad de actualización

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla Capítulo, Responsable, Disparador de actualización, Plazo (10 filas) | Se queda | MT · 5 · "Definir responsables y periodicidad de actualización"; BD · 3 · "Cuándo se actualiza: Cada vez que cambia el esquema de la base de datos" |
| "En cada release… el equipo completo revisa el documento y agrega la fila del historial…" | Se queda | MT · 5 · "periodicidad de actualización"; MT · 5 · "Control de versiones" |
| "Cada cambio sube la versión del documento: … 0.1 … 1.0." | Se queda | MT · 4 · "Debe mantenerse actualizado y controlado mediante versiones" |
| Párrafo "Cada capítulo lo actualiza la persona cuyo rol cubre ese tema (roles en *Análisis del software*…). La revisión cruzada…" | Se va | [Remisión] y [Meta]: repite la tabla y remite a otro documento |
| Celda cap. 7: "; si cambia un módulo, también la matriz de *Análisis del software*, sección 6.6" | Se va | [Remisión] Disparador de otro documento, no de este manual |

## Capítulo 2

### 2.1 Objetivo del sistema

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Párrafo "Encorely conecta a personas mayores de edad… El objetivo técnico es…" | Se queda | MT · 6 · "Objetivo, alcance y descripción general del sistema" |
| Párrafo "El problema que origina el sistema… están en *Análisis del software* (ENC-AS-001), capítulo 5." | Se va | [Remisión] |

### 2.2 Alcance de la versión 1

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| "Dentro del alcance: los requisitos funcionales RF-01 a RF-17…" | Se queda | MT · 6 · "Objetivo, alcance y descripción general del sistema". La remisión "(ver *Análisis del software*, sección 5.6)" se deja como frase mínima: sin ella los códigos RF no se pueden ubicar |
| Lista "Fuera del alcance" (5 elementos: salas, Google/Apple, otras APIs, tiendas y hosting, QA en iOS) | Se queda (era la columna "Excluido") | MT · 6 · "alcance" |
| Párrafo "Decisión D15 (de `reportes/06-decisiones-y-alcance-v1.md`)… Por eso el manual distingue…" | Se va | [Historia] Decisión interna D15; el dato (no hay tiendas ni hosting) ya está en la lista de fuera del alcance |
| ", por decisión del dueño del producto" | Se va | [Justificación] |
| Columna "Motivo" (5 celdas: moderación en tiempo real, solo correo y Spotify, superficie de fallo, D15, sin Mac) | Se va | [Columna] y [Justificación] |

### 2.3 Descripción general

Fuera del avance (solo título); no se depura.

## Capítulo 3

### 3.1 Ambientes del sistema

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla Ambiente, Dónde corre, Estado (5 filas) | Se queda (con duda) | MT · 5 · "Convenciones para nombres de servidores, bases de datos, APIs, módulos y ambientes"; DS · 5 · "Preparación del entorno: Alistar servidores, redes, permisos y dependencias". Ninguna diapositiva pide inventariar ambientes |
| Columna "Para qué" | Se va | [Columna] |
| "(D15)" en la celda Estado de Producción | Se va | [Historia] Decisión interna |

### 3.2 Requisitos de hardware

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| 3.2.1 tabla Recurso, Mínimo, Recomendado (4 filas) | Se queda | MT · 6 · "Requisitos de hardware, software y red" |
| 3.2.2 tabla Dispositivo, Requisito (4 filas) | Se queda | MT · 6 · "Requisitos de hardware" |
| 3.2.3 "Propuesta, no desplegada." | Se queda (frase mínima) | OP · 03 · "Debe reflejar el comportamiento real del sistema, no solo su diseño teórico". Sin ella la tabla se leería como servidores existentes |
| 3.2.3 tabla Nodo, Servicio, Plan (3 filas) | Se queda | MT · 6 · "Requisitos de hardware"; DS · 5 · "Alistar servidores" |
| 3.2.1 párrafo "Valores recomendados por el equipo para correr al mismo tiempo… no una medición de consumo." | Se va | [Meta] Nota de contexto sobre la tabla |
| 3.2.1 columna "Por qué" | Se va | [Columna] y [Justificación] |
| 3.2.3 "preparada en `encorly_back/render.yaml`" | Se va | [Implementación] Archivo fuente de la propuesta |
| 3.2.3 columna "Notas" (una instancia y SignalR; TLS y respaldo; AAB e IPA) | Se va | [Columna] y [Justificación] |
| 3.2.3 "(sin suspensión por inactividad)" en el plan `starter` | Se va | [Justificación] |

### 3.3 y 3.4

Fuera del avance (solo título); no se depuran. Único cambio: una línea en blanco que faltaba entre la tabla de 3.3.2 y el título de 3.4.

## Capítulo 4

### 4.1 Descripción de la arquitectura del sistema (introducción y 4.1.1–4.1.3)

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| "El back es un monolito modular: un solo proceso ASP.NET Core 10 con una carpeta por feature… y la infraestructura transversal en `Infrastructure/`." | Se queda | MT · 8 · "Descripción de la arquitectura del sistema" |
| "La app es un cliente nativo que guarda en el teléfono solo tokens, llaves y el historial cifrado del chat." | Se queda | MT · 8 · "Capa de presentación: interfaz web, móvil o escritorio" |
| "El contrato `contracts/openapi.v1.json` une los dos lados…" | Se queda | MT · 4 · "Describe infraestructura, software, configuración, arquitectura, dependencias e integraciones" |
| 4.1.1 "Componentes en Encorely…" y "Tecnología…" | Se queda | MT · 8 · "Capa de presentación: interfaz web, móvil o escritorio"; MT · 12 · "Identificar lenguajes y versiones" |
| 4.1.2 "Componentes en Encorely…" y "Tecnología…" | Se queda | MT · 8 · "Capa de aplicación: reglas de negocio, servicios y APIs"; MT · 14 · "jobs" |
| 4.1.3 "Componentes en Encorely…" y "Tecnología…" | Se queda | MT · 8 · "Capa de datos: bases de datos, archivos y mecanismos de persistencia" |
| ", sin Kafka, Redis ni Worker" | Se va | [Comparación] |
| Párrafo "La descripción sigue las siete capas de la cátedra. Cada subsección dice…" | Se va | [Meta] |
| 4.1.1 "con CSP por hashes" | Se va | [Implementación] Control de seguridad, no tecnología de la capa |
| 4.1.1 "Detalle de los módulos de la app: *Análisis del software*, sección 6.2." | Se va | [Remisión] |
| 4.1.2 "Detalle de los módulos del back: *Análisis del software*, sección 6.1." | Se va | [Remisión] |
| 4.1.3 "Detalle en el capítulo 6." | Se va | [Remisión] |

### 4.2 Diagrama de arquitectura lógica

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Diagrama (usuarios, presentación, aplicación, datos, infraestructura) | Se queda | MT · 9 · "Ejemplo de arquitectura lógica: USUARIOS / CLIENTES · PRESENTACIÓN · APLICACIÓN · DATOS · INFRAESTRUCTURA" |
| Cajas Integraciones, Seguridad y Operación del diagrama | Se queda | MT · 8 · "Integraciones… Seguridad… Operación…" |
| Etiqueta "rate limit" en la caja Seguridad | Se queda (con duda) | MT · 8 nombra "autenticación, autorización, cifrado, segmentación y auditoría"; el límite de frecuencia protege la autenticación (8.2) |
| Párrafo "Sigue el esquema de la cátedra… Integraciones, seguridad y operación… se dibujan al lado." | Se va | [Meta] |

### 4.3 Diagrama de componentes

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| 4.3.1 y 4.3.2 diagramas, con sus etiquetas | Se queda | MT · 6 · "Arquitectura y diagramas de componentes"; MT · 14 · "Componentes: servicios, clases, APIs, interfaces, procesos, jobs y conectores" |
| Párrafo "Qué hace cada módulo… está en *Análisis del software*, secciones 6.1 y 6.2." | Se va | [Remisión] |
| Párrafo "Las relaciones entre módulos y el flujo de información… están en *Análisis del software*, sección 6.3." | Se va | [Remisión] |

## Capítulo 5

### 5.1 Instalación local de la API y la base de datos

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Prerrequisitos (4 viñetas) | Se queda | MT · 5 · "Documentar prerrequisitos, dependencias, procedimientos y criterios de validación" |
| Pasos 1 a 5 con sus comandos y comentarios de valores | Se queda | MT · 5 · "procedimientos"; DS · 7 · "Manual de instalación: Instrucciones técnicas para instalar y configurar el sistema" |
| Criterio de validación (4 viñetas) | Se queda | MT · 5 · "criterios de validación" |
| Variantes: puertos alternos y todo en contenedores | Se queda | MT · 5 · "procedimientos"; DS · 7 · "Instrucciones técnicas para instalar y configurar" |
| "porque el seed solo inserta lo que falta" | Se va | [Justificación] |
| Párrafo "Verificado el 2026-10-20 en un clon limpio de `dev`…" | Se va | [Historia] |
| "(`ASPNETCORE_URLS` no sirve porque `appsettings.Development.json` fija `Urls`)" | Se va | [Advertencia] |

### 5.2 Instalación local de la app

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Prerrequisitos (3 viñetas) | Se queda | MT · 5 · "Documentar prerrequisitos, dependencias…" |
| Pasos 1 a 4 con sus comandos | Se queda | MT · 5 · "procedimientos"; DS · 7 · "Manual de instalación" |
| Criterio de validación (4 viñetas) | Se queda | MT · 5 · "criterios de validación" |
| Comentarios del paso 4 (`# expo run:android: genera android/…`, `# expo start --dev-client, Metro…`) | Se va | [Implementación] Explican la herramienta |
| "(referencia del 2026-10-20: 56 suites y 490 pruebas en verde)" | Se va | [Historia] |
| "(eso indica que falta `EXPO_PUBLIC_API_URL`)" | Se va | [Advertencia] Diagnóstico, no criterio |
| Párrafo "La app corre en un development build… no en Expo Go… dos cuentas en dos dispositivos. Los pasos 3 y 4 no se re-ejecutaron…" | Se va | [Justificación] e [Historia] |

### 5.8 Diagramas de despliegue

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| 5.8.1 diagrama PlantUML (nodos «device» y «execution environment», artefactos, rutas) | Se queda | UD · 10 · "Documentar cómo está distribuido un sistema entre servidores, nubes o dispositivos"; UD · 11 · "Nodo… Artefacto… Ruta de comunicación"; UD · 14 · "Despliegue distribuido en la nube" |
| 5.8.1 etiquetas de ruta con protocolo, puerto y red ("HTTP 8080 (red privada)", "TCP 5432 (PostgreSQL con TLS, red privada)", "canal push del sistema operativo") | Se queda | UD · 11 · "línea etiquetada con el protocolo o red que usan para comunicarse (HTTPS, JDBC, TCP/IP)" |
| 5.8.1 "Los nodos de Encorely alojan sus artefactos: …" | Se queda | UD · 10 · "Comunicar a operaciones (DevOps) qué componente vive en cada nodo" |
| 5.8.1 "Expo Push, FCM y APNs, Spotify e iTunes son servicios de terceros donde Encorely no despliega artefactos." | Se queda (con duda) | UD · 11 · "Nodo… aloja y ejecuta componentes de software": explica por qué esos nodos no tienen artefacto |
| 5.8.1 "Hay una sola réplica del contenedor; el balanceador no reparte carga entre varias." | Se queda | UD · 14 · "El balanceador distribuye tráfico entre varias réplicas del mismo artefacto — así el diagrama documenta escalabilidad horizontal" |
| 5.8.2 diagrama PlantUML de la demo local, con etiquetas de protocolo, puerto y dirección | Se queda | UD · 10 · "Documentar cómo está distribuido un sistema"; UD · 11 · "Ruta de comunicación" |
| 5.8.1 texto de uso en 6 etiquetas: "(REST /api/v1 y WSS /hubs/chat)" (queda "HTTPS y WSS 443"), "(/admin)", "(canje PKCE)", "(PKCE y top artists)", "(búsqueda admin)", "(portadas y muestras)" | Se va | [Implementación] La etiqueta de ruta lleva protocolo o red, no el uso |
| 5.8.1 "La API no tiene estado en disco: todo el estado persistente del servidor está en `encorely-db`." | Se va | [Justificación] |
| 5.8.1 "(ver alta disponibilidad en 9.2)" | Se va | [Remisión] A una sección fuera del avance |
| 5.8.2 texto de uso en 4 etiquetas: "(bundle JS)", "(REST y WebSocket)" (WebSocket pasa a la etiqueta como protocolo) | Se va | [Implementación] |
| 5.8.2 párrafo "Este es el despliegue que existe y se usa hoy… Con `PUSH_ENABLED=false`… no sale tráfico hacia Expo Push." | Se va | [Meta] El título ya dice que es la demo local; la segunda frase explica una ausencia |

## Capítulo 6

### 6.1 Motor, conexión y convenciones

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Filas Motor y Acceso | Se queda | MT · 12 · "Describir el motor de base de datos"; "Registrar frameworks y librerías principales" |
| Fila Cadena de conexión | Se queda | MT · 12 · "Registrar conexiones, dependencias, migraciones, índices" |
| Filas Base de datos y Nombres | Se queda | MT · 5 · "Convenciones para nombres de… bases de datos" |
| Filas Identificadores, Enums, Fechas | Se queda | BD · 4 · "Campos y tipos de datos… Llave primaria (PK)" |
| Fila Tablas | Se queda | MT · 12 · "Documentar entidades, tablas, relaciones, claves y restricciones" |
| Fila Dependencias de la base ("Ninguna extensión de Postgres") | Se queda | MT · 12 · "Registrar conexiones, dependencias" |
| Tabla "Tablas por área" | Se queda (con duda) | MT · 12 · "Documentar entidades, tablas"; ninguna diapositiva pide agruparlas |
| Fila "Pool \| El de Npgsql por defecto; una instancia de la API comparte el pool…" | Se va | [Implementación] |
| "legibles y sin CHECK numéricos" | Se va | [Justificación] |
| "; `sha256()` nativo se usa en una migración de datos" | Se va | [Implementación] |

### 6.2 Diagrama entidad-relación

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Las cinco vistas erDiagram (6.2.1 a 6.2.5) con sus etiquetas de relación | Se queda | BD · 6 · "Ejemplo: diagrama entidad-relación… muestra de un vistazo cómo se conectan las tablas"; MT · 12 · "Representar el modelo mediante ERD" |
| "El modelo se presenta en cinco vistas; `users` y `matches` se repiten como ancla entre vistas. El detalle de cada campo está en las fichas de 6.3." | Se queda (frase mínima para leer un ERD partido) | BD · 6 · "El diagrama entidad-relación no reemplaza la ficha técnica: la complementa" |
| "para que se pueda leer en una página" | Se va | [Justificación] |
| "Cada tabla aparece con su llave primaria, sus llaves foráneas y los campos que definen su regla principal" | Se va | [Meta] |
| "Las cinco vistas juntas cubren las 25 tablas y todas sus relaciones." | Se va | [Meta] |
| 6.2.4 nota "`delivered_message_ids.match_id` y `message_franking_records.match_id` guardan el match sin llave foránea: deben sobrevivir al borrado del mensaje." | Se va | [Justificación] El dato "Sin FK" ya está en las fichas 6.3.20 y 6.3.21 |

### 6.3 Fichas técnicas de las tablas

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Línea "Propósito: …" de las 25 fichas (propósito y rol, PK, FK con ON DELETE, índices, UNIQUE, CHECK) | Se queda | BD · 4 · "Nombre y propósito de la tabla… Llave primaria (PK)… Llaves foráneas y relaciones… Restricciones e índices" |
| "Componentes: …" de cada ficha | Se queda | MT · 12 · "Relacionar componentes del software con las estructuras de datos que utilizan" |
| Tablas Campo, Tipo, Nulo, Restricción o defecto, Significado (todas las filas de las 25 fichas) | Se queda | BD · 4 · "Campos y tipos de datos… NOT NULL, UNIQUE, valores por defecto"; BD · 5 · ejemplo "Campo · Tipo · Restricción · Descripción" |
| Párrafo "Convenciones de las fichas: **Nulo** indica… `timestamptz` abrevia… "Sin FK" señala… Los valores… están en 6.4. Las columnas `created_at` y `updated_at` las asigna la API…" | Se va | [Meta] e [Implementación] |
| users: "(Postgres admite varios `NULL` en el único)" | Se va | [Implementación] |
| banned_emails: "(los vetos anteriores a la llave quedaron en SHA-256 y se siguen consultando)" | Se va | [Historia] |
| swipes: "La PK compuesta garantiza que repetir una canción no suma." | Se va | [Justificación] |
| user_genre_scores: "El peso efectivo es `max(0, likes − 0,5·dislikes + …)`… Se actualiza con un upsert atómico en la misma transacción del swipe." | Se va | [Implementación] |
| user_artists: "Cada importación reemplaza la lista." | Se va | [Implementación] |
| profile_reactions: "reaccionar de nuevo cambia el tipo. No cuenta para el match ni envía push." | Se va | [Implementación] y [Comparación] |
| profile_reactions: "Bloquear borra las del par y suspender las de la cuenta." | Se va | [Implementación] |
| user_box_keys: "Nunca guarda llaves privadas." | Se va | [Comparación] |
| chat_messages: "El servidor no puede leerlos; se borran con el ack del receptor o al cumplir `CHAT_MESSAGE_RETENTION_DAYS`." | Se va | [Implementación] |
| message_franking_records: "Nunca guarda ciphertext; vence a los 180 días." | Se va | [Comparación] e [Implementación] |
| blocks: "Un bloqueo cierra el match, saca a ambas del radar mutuo y borra likes y reacciones del par." | Se va | [Implementación] |
| reports: "; repetir el reporte suma evidencia al mismo" | Se va | [Implementación] |
| encrypted_backup: "con una frase que el servidor nunca ve" y "se reemplaza completo" | Se va | [Implementación] |
| device_link_request: "; el servidor solo ve material cifrado" | Se va | [Comparación] |
| users.show_profile_to_others: "(requiere además el consentimiento de esa finalidad)" | Se va | [Implementación] Regla de negocio, no significado del campo |
| users.birth_date: "; se exige 18 años o más para terminar el onboarding" | Se va | [Implementación] |
| refresh_tokens.family_id: "(viaja en el claim `sid` del access token)" | Se va | [Implementación] |
| refresh_tokens.token_hash: "; el token en claro nunca se guarda" | Se va | [Comparación] |
| device_push_tokens.token: "; si otra cuenta lo registra, pasa a ella" | Se va | [Implementación] |
| device_push_tokens.session_id: "; al cerrarla se borra el token" | Se va | [Implementación] |
| songs.preview_url: "; puede faltar" | Se va | [Meta] Repite el NULL |
| user_artists.image_url: "solo se guarda si es https de `i.scdn.co`" (queda "Imagen del artista") | Se va | [Implementación] |
| profile_reactions.target_name: "(agregado por la migración `PublicProfileConsent`)" y ", para que la lista no dependa del perfil actual" | Se va | [Historia] y [Justificación] |
| matches.id: "(también es la sal del HKDF del chat)" | Se va | [Implementación] |
| chat_messages.id: "; forma parte del AAD e impide duplicados" | Se va | [Implementación] |
| chat_messages.recipient_box_key_id: "; si no es la vigente, el envío da 409" | Se va | [Implementación] |
| chat_messages.ciphertext: "con relleno a múltiplos de 256 bytes" | Se va | [Implementación] |
| reports.detail: "; se borra 180 días después de resolver" | Se va | [Implementación] |
| encrypted_backup.ciphertext: "(hasta 15 MB por petición)" | Se va | [Implementación] |
| encrypted_backup.kdf_algorithm: "; la app usa `scrypt`" | Se va | [Implementación] |

### 6.4 Diccionario de datos

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| "Valores permitidos de los campos enumerados:" y su tabla (11 filas) | Se queda | BD · 4 · "Diccionario de datos: Glosario que explica el significado de cada campo en lenguaje de negocio" |
| "Términos de negocio…" y su tabla (8 términos) | Se queda | BD · 4 · ídem ("Glosario… en lenguaje de negocio") |
| "(se guardan como texto con estos nombres exactos)" | Se va | [Implementación] Repite la fila Enums de 6.1 |

## Capítulo 7

### 7.1 API REST v1

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Fila Base (`/api/v1`, con la nota de que se omite en las tablas) | Se queda | MT · 5 · "Convenciones para nombres de servidores, bases de datos, APIs…"; la nota es la frase mínima para leer las rutas de 7.2 |
| Fila Contrato (archivo, 68 operaciones, versiones en `CHANGELOG.md`) | Se queda | MT · 8 · "Capa de aplicación: reglas de negocio, servicios y APIs"; MT · 4 · "Debe mantenerse actualizado y controlado mediante versiones" |
| Fila Autenticación | Se queda | MT · 8 · "Seguridad: autenticación, autorización" |
| Fila Formato | Se queda | MT · 5 · "Convenciones para nombres de… APIs"; MT · 15 · "REST / JSON" |
| "Fuera del contrato, la API sirve `/health/…`, el hub…, el panel `/admin` y las páginas…" | Se queda | MT · 8 · "Capa de aplicación: … servicios y APIs" |
| "generado desde el código; `OpenApiContractTests` falla si no coincide" | Se va | [Implementación] |
| ", con la marca **Rompe** en lo que obliga a ajustar el cliente" | Se va | [Implementación] |
| Fila "Errores \| `application/problem+json`…" | Se va | [Fila] Ninguna diapositiva pide el formato de error de la API |
| "`/openapi/v1.json` y `/swagger` solo existen en `Development` (ver 10.6 sobre `/swagger`)." | Se va | [Implementación] y [Remisión] |

### 7.2 Operaciones por módulo

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla Módulo, Rutas, Autenticación (13 filas) | Se queda (con duda) | MT · 6 · "Integraciones, APIs y servicios externos"; MT · 5 · "Convenciones para nombres de… APIs"; MT · 8 · "autenticación". Ninguna diapositiva pide inventariar la API propia |
| Párrafo "'Pública' significa sin token. El detalle de cada operación… está en `openapi.v1.json`." | Se va | [Meta] y [Remisión] |
| Columna "Operaciones" | Se va | [Columna] |

## Capítulo 8

### 8.1 Perfiles de usuario y permisos

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla Perfil, Cómo se obtiene, Qué puede hacer, Qué no puede hacer (6 perfiles) | Se queda | MT · 6 · "Seguridad, perfiles y permisos" |
| Matriz de permisos por módulo (9 filas) | Se queda | MT · 6 · "perfiles y permisos"; MT · 8 · "autorización" |
| "Perfiles técnicos de operación (propuestos, con mínimo privilegio)… cuentas de Expo, Apple y Google con dos factores." | Se queda | MT · 6 · "Seguridad, perfiles y permisos" |
| "; secretos solo en el proveedor y en el gestor de secretos del equipo" | Se va | [Fila] Gestión de secretos (8.7, fuera del avance); no es un perfil ni un permiso |

### 8.2 Autenticación y sesiones

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Viñetas BCrypt, access token JWT, refresh token, comprobación por petición, correo inexistente con hash ficticio, límite por IP en registro/login/refresh, bloqueo por intentos, sesión en la app | Se queda | MT · 8 · "Seguridad: autenticación, autorización, cifrado…" |
| "Las escrituras de cada persona tienen un límite de 120 por minuto y los reportes de 10 por hora." | Se va | [Implementación] No es autenticación |
| "Detrás de un proxy, `FORWARDED_TRUSTED_NETWORKS` es obligatorio; sin él…" | Se va | [Advertencia] |
| "El 429 se decide antes de calcular el hash y también para correos inexistentes, así que no delata cuentas." | Se va | [Justificación] |
| "y un único refresh en vuelo para no disparar la revocación por reuso" | Se va | [Implementación] y [Justificación] |

### 8.3 Autorización

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Viñetas identidad desde el JWT, política de respaldo, pertenencia (404), rol admin en `/admin/*` | Se queda | MT · 8 · "Seguridad: … autorización" |
| Viñeta "Cada endpoint con recursos tiene su prueba 'A no accede a lo de B'." | Se va | [Historia] Evidencia de pruebas, no un control |
| Viñeta "Las respuestas no exponen más de lo necesario: health checks sin detalle…" | Se va | [Fila] Minimización de datos; MT · 8 no la nombra |

## Capítulo 9

### 9.1 Estado operativo real y objetivos propuestos

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| "Encorely no está en producción. Este capítulo separa dos cosas:" y sus dos viñetas | Se queda | OP · 03 · "Debe reflejar el comportamiento real del sistema, no solo su diseño teórico" |
| Tabla Capacidad operativa, Existe hoy, Propuesto para producción (8 filas) | Se queda | OP · 06 · "Componentes monitoreados… Alertas y umbrales… Logs, métricas y trazas"; OP · 07 · "Frecuencia de respaldo"; OP · 08 · "mantenimiento preventivo" (filas Limpieza y Receipts de push, con duda) |
| "(D15)" y "Por eso" | Se va | [Historia] |
| "Se puede comprobar en el código y en la demo local." | Se va | [Meta] |
| ": no hay tráfico real que medir" | Se va | [Justificación] |
| Columna "Evidencia" (archivos de código, tests, 9.4, 9.5.2) | Se va | [Columna] |

### 9.7 Flujo operativo ante una falla

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Diagrama pasos 1 a 4 (Monitoreo, Alerta, Respuesta, Recuperación) | Se queda | OP · 11 · "1. Monitoreo · 2. Alerta · 3. Respuesta · 4. Recuperación" |
| Nodo "Clasificar severidad SEV1 a SEV4" | Se queda | OP · 08 · "Clasificación de incidentes por severidad e impacto" |
| Nodo "Escalar al siguiente nivel o aplicar rollback de 5.6" | Se queda | OP · 08 · "Procedimiento de escalamiento"; DS · 6 · "Si algo falla… volver a la versión anterior sin improvisar" |
| Nodo "Paso 5 · Documentación" | Se queda | OP · 11 · "se documenta el incidente"; OP · 08 · "Registro de lecciones aprendidas" |
| Párrafo "Ejemplo aplicado: el monitor externo detecta…" | Se queda | OP · 11 · "Ejemplo aplicado al sistema…: flujo operativo ante una falla" |
| Nodo "Comunicación · aviso a usuarios si SEV1 o SEV2" y su arista | Se va | [Fila] OP · 08 pide comunicación previa de mantenimiento, no aviso de incidentes |

## Capítulo 10

### 10.1 Gestión de incidentes

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla de severidad: columnas Severidad, Impacto | Se queda | OP · 08 · "Clasificación de incidentes por severidad e impacto" |
| Columnas Respuesta objetivo, Resolución o mitigación objetivo | Se queda | OP · 12 · "Cada aspecto… debe tener dueño y métrica"; OP · 10 · "Incidentes… Escalamiento en 10 min" |
| Columna Ejemplos | Se queda (con duda) | OP · 10 · "Incidentes: Caída de la pasarela de pagos externa" (la ficha ejemplo describe el incidente tipo) |
| Tabla de escalamiento (Nivel, Quién, Qué hace, Escala cuando) | Se queda | OP · 08 · "Procedimiento de escalamiento y responsables por nivel" |
| "Comunicación: mantenimiento programado anunciado con 48 horas de anticipación…" | Se queda | OP · 08 · "Ventanas de mantenimiento y comunicación previa a los usuarios" |
| "Lecciones aprendidas: …" (las dos frases) | Se queda | OP · 08 · "Registro de lecciones aprendidas después de cada incidente"; OP · 12 · "Esta documentación debe actualizarse junto con la evolución del sistema" |
| "en SEV1 y SEV2, aviso al inicio, cada hora mientras dure y al cierre" | Se va | [Fila] Ninguna diapositiva pide comunicación durante incidentes |
| "La app no tiene hoy un mecanismo de avisos dentro de la app (pendiente en 10.6)." | Se va | [Meta] y [Remisión] |

### 10.2 Runbooks de incidentes

| Bloque | Se queda / Se va | Diapositiva o razón |
|---|---|---|
| Tabla RB-01 a RB-10 (ID, Síntoma, Diagnóstico, Acción, Validación) | Se queda | OP · 10 · "Runbook + guardia on-call"; OP · 11 · "El responsable ejecuta el runbook y contiene el impacto"; MT · 5 · "procedimientos y criterios de validación" |

---

## Bloques que se quedan con duda

1. **1.1, filas Código, Sistema y Tipo de documento.** MT-6 pide "control documental" sin decir qué campos. Se dejan como identificación del documento; "Tipo de documento" además es la fila que `generar_avance.py` reescribe para marcar el avance.
2. **3.1 Ambientes, tabla completa.** Solo MT-5 ("convenciones para nombres de… ambientes") y DS-5 ("Preparación del entorno") la respaldan. Si se quita, 3.1 queda vacía.
3. **7.2 Operaciones por módulo.** Ninguna diapositiva pide inventariar la API propia. Quedan módulo, ruta y autenticación, que responden a convenciones de nombres (MT-5) y autenticación (MT-8).
4. **2.2, remisión "(ver *Análisis del software*, sección 5.6)".** Es la única remisión a otro documento que queda en el avance. Se deja porque sin ella no se pueden ubicar RF-01 a RF-17.
5. **10.1, columna Ejemplos**, **6.1, tabla Tablas por área**, **9.1, filas Limpieza de datos y Revisión de receipts**, **5.8.1, frase de servicios de terceros sin artefactos** y **4.2, etiqueta "rate limit"**: tienen respaldo indirecto.
6. **1.2 Historial de cambios.** Esta depuración no agrega una fila nueva porque la regla era no añadir contenido. MT-5 pide registrar cada cambio, así que conviene agregar la fila 1.4 antes de entregar.
