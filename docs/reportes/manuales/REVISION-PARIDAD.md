# Revisión de paridad con las diapositivas

Documentos: `MANUAL-TECNICO-ENCORELY.md` v1.2 (capítulos 2 a 10; 92 páginas en el PDF) y `ANALISIS-DEL-SOFTWARE-ENCORELY.md` v1.0 (completo; 23 páginas). Fecha: 2026-10-21.

Abreviaturas de los decks:
- **MT**: *Manual Técnico y Análisis del Software* (diap. 4 a 9: manual; 10 a 15: análisis).
- **OP**: *Operatividad del Sistema*.
- **DS**: *Despliegue del Software*.
- **BD**: *Estructura de la BD y Tablas de Contenido*.
- **UD**: *Manual de usuario y diagramas de despliegue* (solo la parte de diagrama de despliegue, diap. 10 a 14).

Criterio: ✅ hay una diapositiva que lo pide o lo describe de forma explícita · ⚠️ cabe bajo un título de la diap. MT-6, pero ninguna diapositiva describe ese contenido, o lo excede mucho · ❌ ninguna diapositiva lo pide ni lo describe.

---

## 1. Resumen

| Documento | Secciones auditadas | ✅ | ⚠️ | ❌ |
|---|---|---|---|---|
| Manual técnico, capítulos 2 a 10 (### y ####) | 131 | 98 | 25 | 8 |
| Manual técnico, agrupando 6.2.1–6.2.5 y 6.3.1–6.3.25 en una fila cada uno | 103 | 70 | 25 | 8 |
| Análisis del software (### y los ## sin subsecciones) | 26 | 24 | 2 | 0 |

El ✅ del manual está inflado por las 25 fichas de tabla. Si se agrupan, una de cada tres secciones del manual no tiene respaldo directo en las diapositivas. Los capítulos 7, 8 y 10 concentran casi todo lo que está fuera de paridad (20 de las 33 secciones ⚠️/❌).

**Las 5 recomendaciones de mayor impacto**

1. **Capítulo 7, comprimir.** El catálogo de 68 operaciones (7.2, unas 3 páginas) pasa a una tabla por módulo de unas 13 filas que remite al contrato. 7.5, 7.6 y 7.7 se fusionan en 7.9; 7.3 y 7.4 quedan en 2 o 3 líneas dentro de 7.1, y 7.8 se quita (su procedimiento pasa a 10.5). Ahorro: unas 4 páginas.
2. **Capítulo 10, quitar los anexos que nadie pide.** Se quitan 10.10 (matriz de cumplimiento, unas 3,5 páginas) y 10.8 (archivos de referencia). 10.3 queda en las 5 filas más frecuentes por lado, 10.5 en 5 líneas y 10.6 en las 4 filas a las que remiten otras secciones. Ahorro: unas 5,5 páginas.
3. **Capítulo 8, ajustar a lo que dice la diap. MT-8** ("autenticación, autorización, cifrado, segmentación y auditoría"). 8.5 se queda solo con la tabla de algoritmos, sin el modelo de amenazas. 8.4, 8.6, 8.7 y 8.8 se fusionan en 3 viñetas dentro de 8.2 y 8.3, 8.10 se quita y 8.9 queda en 3 líneas. A cambio hay que **agregar** segmentación y auditoría, que la diapositiva nombra y el manual no cubre. Ahorro neto: unas 2 páginas.
4. **Redibujar los diagramas de despliegue de 5.8 con notación UML real.** UD-11 y UD-12 piden el nodo como prisma (caja 3D) y el artefacto como rectángulo con ícono de documento. Hoy son cajas planas de un flowchart de Mermaid con el artefacto escrito como texto. Es el hueco de forma más visible frente a una diapositiva explícita.
5. **Poner responsables con nombre y un dueño por aspecto operativo.** OP-12 dice "Cada aspecto… debe tener dueño y métrica", MT-14 y MT-15 piden "responsables" por módulo y MT-5 pide "Definir responsables". Hoy todas las celdas dicen "Equipo Encorely" y la ficha 9.6 no tiene columna de dueño. Hay que asignar una persona por rol (Análisis, capítulo 3), por módulo (Análisis, 6.1, 6.2 y 6.6) y por aspecto (Manual, 9.6).

---

## 2. Manual técnico, por capítulo

### Capítulo 2. Objetivo, alcance y descripción general

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 2.1 Objetivo del sistema | ✅ | MT · 6 · "Objetivo, alcance y descripción general del sistema" | Mantener |
| 2.2 Alcance de la versión 1 | ⚠️ | MT · 6 · "alcance" | El alcance sí está pedido; la tabla de decisiones D1–D15 no (son decisiones de arquitectura y de diseño, como D5 "dirección visual"). Reducir a dentro/fuera del alcance y dejar solo D15, que explica que no hay producción |
| 2.3 Descripción general | ✅ | MT · 6 · "descripción general del sistema" | Mantener |

Veredicto: en paridad salvo la tabla de decisiones de 2.2.

### Capítulo 3. Requisitos de hardware, software y red

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 3.1 Ambientes del sistema | ⚠️ | MT · 5 · "Convenciones para nombres de… ambientes" (solo como convención); DS · 5 · "Preparación del entorno" | Ninguna diapositiva pide inventariar ambientes. Mantener (5 filas) porque los capítulos 5 y 9 dependen de la distinción entre local y producción propuesta |
| 3.2 Requisitos de hardware | ✅ | MT · 6 · "Requisitos de hardware, software y red" | Mantener |
| 3.2.1 Estación de desarrollo | ✅ | MT · 6 · ídem | Mantener |
| 3.2.2 Dispositivos móviles | ✅ | MT · 6 · ídem | Mantener |
| 3.2.3 Servidores de producción | ✅ | MT · 6 · ídem; DS · 5 · "Alistar servidores, redes, permisos" | Mantener |
| 3.3 Requisitos de software | ✅ | MT · 6 · ídem; MT · 5 · "Documentar prerrequisitos, dependencias" | Mantener |
| 3.3.1 Software para el back | ✅ | ídem | Mantener |
| 3.3.2 Software para la app | ✅ | ídem | Quitar el párrafo final sobre pnpm (ya está en 10.3.2) |
| 3.4 Requisitos de red | ✅ | MT · 6 · "red"; DS · 5 · "Alistar servidores, redes" | Mantener |

Veredicto: en paridad.

### Capítulo 4. Arquitectura y diagramas de componentes

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 4.1 Estilo arquitectónico y decisiones | ⚠️ | MT · 11 · "Arquitecto de software: define arquitectura, patrones y decisiones técnicas" (es la función de un rol, no un contenido pedido) | Fusionar en la introducción de 4.2 con 3 líneas; repite D3 y D4 de 2.2 |
| 4.2 Descripción de la arquitectura del sistema | ✅ | MT · 8 · "Descripción de la arquitectura del sistema" | Mantener |
| 4.2.1 Capa de presentación | ✅ | MT · 8 · "interfaz web, móvil o escritorio" | Mantener |
| 4.2.2 Capa de aplicación | ✅ | MT · 8 · "reglas de negocio, servicios y APIs" | Mantener |
| 4.2.3 Capa de datos | ✅ | MT · 8 · "bases de datos, archivos y mecanismos de persistencia" | Mantener |
| 4.2.4 Infraestructura | ✅ | MT · 8 · "servidores, nube, redes, almacenamiento y balanceadores" | Agregar el balanceador TLS (sale en 5.8.1 y no aquí) |
| 4.2.5 Integraciones | ✅ | MT · 8 · "sistemas externos, servicios web, APIs, colas y proveedores" | Mantener |
| 4.2.6 Seguridad | ✅ | MT · 8 · "autenticación, autorización, cifrado, segmentación y auditoría" | Agregar segmentación y auditoría (ver sección 4) |
| 4.2.7 Operación | ✅ | MT · 8 · "monitoreo, logs, respaldos, alta disponibilidad y recuperación ante fallos" | Agregar respaldos, alta disponibilidad (una sola instancia, declarada) y recuperación |
| 4.3 Diagrama de arquitectura lógica | ✅ | MT · 9 · "Ejemplo de arquitectura lógica" | Mantener |
| 4.4 Diagrama de componentes | ✅ | MT · 6 · "Arquitectura y diagramas de componentes" | Mantener |
| 4.4.1 Componentes de la app | ✅ | MT · 6 · ídem; MT · 5 · "Diagramas normalizados… UML" | Mantener |
| 4.4.2 Componentes de la API | ✅ | ídem | Mantener |
| 4.5 Relaciones y flujo de información | ⚠️ | MT · 14 · "Describir las relaciones entre módulos y el flujo de información" (lo pide el bloque de Análisis, no el del manual) | Llevar el contenido a *Análisis*, 6.3, que es donde la diapositiva lo pide y hoy solo remite aquí |
| 4.5.1 Pipeline de una petición en la API | ❌ | Ninguna | Quitar: el orden de los middlewares no lo pide ninguna diapositiva y sus viñetas repiten 8.2 y 8.3 |
| 4.5.2 Secuencia del match recíproco | ⚠️ | MT · 5 · "preferiblemente UML" (genérico); MT · 14 · "flujo de información" | Mover a *Análisis*, 6.3 |
| 4.5.3 Secuencia del envío de un mensaje cifrado | ⚠️ | ídem | Mover a *Análisis*, 6.3 |

Veredicto: el núcleo (4.2 a 4.4) calca MT-8 y MT-9. Lo que sobra es 4.1 y 4.5, que pertenece al documento de análisis.

### Capítulo 5. Instalación, configuración y despliegue

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 5.1 Instalación local de la API y la base de datos | ✅ | DS · 7 · "Manual de instalación: Instrucciones técnicas para instalar y configurar el sistema"; MT · 5 · "procedimientos y criterios de validación" | Reducir "Variantes" a 2 líneas |
| 5.2 Instalación local de la app | ✅ | ídem | Mantener |
| 5.3 Configuración | ✅ | MT · 6 · "Instalación, configuración y despliegue"; DS · 5 · "ajustar parámetros del entorno real" | Mantener |
| 5.3.1 Variables de entorno de la API | ✅ | DS · 5 · ídem | Mantener (es el parámetro real del entorno) |
| 5.3.2 Variables de entorno de la app | ✅ | DS · 5 · ídem | Mantener |
| 5.3.3 Configuración estática de la app | ⚠️ | DS · 5 · "ajustar parámetros del entorno" (`app.json` es configuración de build, no del entorno) | Reducir a 3 filas: identificadores, perfiles de `eas.json` y `allowBackup` |
| 5.4 Integración continua | ⚠️ | MT · 11 · "DevOps… automatiza despliegues" (rol); OP · 10 · "CI/CD + changelog" (herramienta del ejemplo) | Reducir a 4 líneas: qué corre cada workflow y que la CI en verde es requisito para desplegar. Quitar el bloque de comandos locales y el resultado del 2026-10-20 |
| 5.5 Plan de despliegue a producción | ✅ | DS · 7 · "Plan de despliegue: Cronograma, roles y ventana de ejecución acordados" | Mantener |
| 5.5.1 Estado actual del despliegue | ⚠️ | Ninguna lo pide; se apoya en OP · 3 · "reflejar el comportamiento real del sistema" | Mantener en 2 líneas: sin esto, 5.5.2 parece un despliegue que ya ocurrió |
| 5.5.2 Cronograma, responsables y ventana | ✅ | DS · 5 · "Cronograma, responsables, ventana de tiempo y plan de rollback" | Poner responsables con nombre (ver sección 4) |
| 5.5.3 Actividades de la fase de despliegue | ✅ | DS · 5 y 6 · las 8 actividades, de "Planificación" a "Monitoreo posterior" | Ampliar la actividad 6 a usuarios finales (ver sección 4) |
| 5.5.4 Procedimiento de despliegue de la API | ✅ | DS · 5 · "Instalación y configuración"; MT · 5 · "procedimientos y criterios de validación" | Mantener |
| 5.5.5 Procedimiento de build y publicación de la app | ✅ | DS · 5 · "Puesta en marcha (go-live)" | Mantener |
| 5.6 Plan de rollback | ✅ | DS · 6 · "El plan de reversión (rollback)… acompaña toda la secuencia"; DS · 7 · "Plan de rollback" | Mantener |
| 5.7 Productos del despliegue | ✅ | DS · 7 · los 6 productos | Mantener |
| 5.8 Diagramas de despliegue | ✅ | UD · 10 · "¿Qué es un diagrama de despliegue?" | Mantener |
| 5.8.1 Despliegue objetivo en producción | ✅ | UD · 14 · "Despliegue distribuido en la nube" (balanceador, contenedor, base gestionada) | Redibujar con la notación de UD-12 (ver sección 4) |
| 5.8.2 Despliegue actual de la demo local | ✅ | UD · 10 · "Documentar cómo está distribuido un sistema entre servidores…" | Ídem |

Veredicto: es el capítulo mejor alineado (DS-5, DS-6, DS-7 y UD-10 a UD-14 casi punto por punto). Sobran 5.3.3 y el detalle de 5.4, y falla la forma de los diagramas UML.

### Capítulo 6. Base de datos y modelo de información

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 6.1 Motor, conexión y convenciones | ✅ | MT · 12 · "Describir el motor de base de datos… Registrar conexiones, dependencias"; MT · 5 · "Convenciones para nombres de… bases de datos" | Quitar la fila "Concurrencia" (advisory locks), que excede |
| 6.2 Diagrama entidad-relación | ✅ | BD · 6 · "El diagrama entidad-relación no reemplaza la ficha técnica: la complementa"; MT · 12 · "ERD" | Mantener |
| 6.2.1 a 6.2.5 Vistas del ERD (5 secciones) | ✅ | BD · 6 · ídem | Mantener |
| 6.3 Fichas técnicas de las tablas | ✅ | BD · 4 · "Lo que no puede faltar en la ficha técnica de cada tabla"; BD · 5 · ejemplo "Tabla: clientes" | Mantener |
| 6.3.1 a 6.3.25 Fichas por tabla (25 secciones) | ✅ | BD · 4 y 5 · nombre y propósito, campos y tipos, PK, FK, restricciones e índices | Mantener |
| 6.4 Diccionario de datos | ✅ | BD · 4 · "Diccionario de datos: Glosario que explica el significado de cada campo en lenguaje de negocio" | Mantener |
| 6.5 Almacenamiento en el dispositivo | ✅ | MT · 8 · "Capa de datos: bases de datos, archivos y mecanismos de persistencia" | Mantener |
| 6.5.1 Tabla local messages | ✅ | BD · 4 · ficha técnica | Mantener |
| 6.5.2 Tabla local sync_state | ✅ | BD · 4 · ídem | Mantener |
| 6.6 Migraciones | ✅ | MT · 12 · "Registrar… migraciones"; BD · 3 · "Cada vez que cambia el esquema" | Mover el "Procedimiento para crear una migración" a 10.5 |
| 6.7 Datos semilla | ⚠️ | Ninguna pide datos semilla; DS · 5 · "Migración de datos" habla de datos existentes, no de seed | Reducir a 3 líneas: es la validación de la actividad 4 de 5.5.3 (15 géneros, 120 canciones) |
| 6.8 Relación entre componentes y estructuras de datos | ✅ | MT · 12 · "Relacionar componentes del software con las estructuras de datos que utilizan" (bloque de Análisis) | Mantener. *Análisis*, 4.6 solo remite aquí: o se trae la tabla allá o se deja así sabiendo que el análisis queda débil |
| 6.9 Política de respaldo de la base de datos | ✅ | MT · 12 · "políticas de respaldo"; OP · 7 · "RTO / RPO" | Duplica 9.5.1. Dejar una línea que remita a 9.5 |
| 6.10 Retención y purga de datos | ❌ | Ninguna | Mover la tabla a 9.4 (fila `MaintenanceService`) y actualizar las remisiones de 8.9 y 9.6 |
| 6.11 Mantenimiento de este capítulo | ✅ | BD · 3 · "Cuándo se actualiza: Cada vez que cambia el esquema"; BD · 9 · "Actualizar el diccionario de datos con cada cambio de esquema" | Mantener (2 líneas) |

Veredicto: en paridad fuerte con el deck BD. Solo sobran 6.7, 6.10 y la duplicación de 6.9.

### Capítulo 7. Integraciones, APIs y servicios externos

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 7.1 API REST v1 | ⚠️ | MT · 6 · "Integraciones, APIs y servicios externos" (título); MT · 8 · "APIs" (componente); ninguna dice qué documentar de la API | Reducir a 5 filas (base, contrato, autenticación, formato, errores); quitar la lista de códigos de error |
| 7.2 Catálogo de operaciones | ⚠️ | Ninguna pide un inventario de endpoints; lo excede mucho (68 filas, unas 3 páginas) | Reemplazar por una tabla por módulo (prefijo, n.º de operaciones, auth) y remitir a `openapi.v1.json` |
| 7.3 Rutas fuera del contrato | ⚠️ | MT · 8 · "Operación: monitoreo" (solo para los health checks) | Fusionar en 7.1 con 2 líneas |
| 7.4 Tiempo real con SignalR | ⚠️ | Ninguna describe tiempo real; es un componente interno de la capa de aplicación | Reducir a 3 líneas (qué es, que solo avisa y la lista de eventos) |
| 7.5 Notificaciones push | ✅ | MT · 8 · "Integraciones: sistemas externos, servicios web… proveedores" | Fusionar en 7.9 |
| 7.6 iTunes Search | ✅ | MT · 8 · ídem | Fusionar en 7.9 |
| 7.7 Spotify | ✅ | MT · 8 · ídem | Fusionar en 7.9; dejar 2 líneas para PKCE y el límite de 5 cuentas |
| 7.8 Cliente de la API en la app | ❌ | Ninguna | Quitar. Su procedimiento pasa a 10.5 y la tabla de archivos ya está en *Análisis*, 6.2 (`lib/api`) |
| 7.9 Resumen de servicios externos | ✅ | MT · 8 · ídem; MT · 15 · "Integraciones · API externa · REST/JSON · Proveedor externo" | Usarla como sección principal del capítulo, con la columna "Si falla" |

Veredicto: es el capítulo más fuera de paridad. Las diapositivas piden documentar sistemas externos y proveedores, no la API propia endpoint por endpoint. Puede bajar de 8 a unas 4 páginas.

### Capítulo 8. Seguridad, perfiles y permisos

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 8.1 Perfiles de usuario y permisos | ✅ | MT · 6 · "Seguridad, perfiles y permisos" | Mantener |
| 8.2 Autenticación y sesiones | ✅ | MT · 8 · "Seguridad: autenticación" | Absorber aquí 8.4 en 2 viñetas (límite de login y bloqueo por intentos) |
| 8.3 Autorización | ✅ | MT · 8 · "autorización" | Absorber aquí 8.7 en 1 viñeta |
| 8.4 Protección contra abuso | ⚠️ | Ninguna nombra el rate limiting; cabe bajo "Seguridad" | Fusionar en 8.2. Hay que dejar las 2 viñetas porque 9.6 (fila Seguridad) y RB-06 remiten a estos límites |
| 8.5 Cifrado de extremo a extremo del chat | ⚠️ | MT · 8 · "cifrado" (una sola palabra) | Excede mucho. Dejar la tabla de algoritmos en unas 6 filas y quitar el modelo de amenazas (11 filas) y el párrafo de límites |
| 8.6 Seguridad en el dispositivo | ⚠️ | MT · 8 · "cifrado" (solo SecureStore y SQLCipher) | Fusionar 2 líneas en 8.5 y quitar el resto (ESLint, regex del deep link) |
| 8.7 Cabeceras, superficie expuesta y registros | ❌ | Ninguna; "auditoría" (MT-8) no es esto | Quitar, o dejar una viñeta en 8.3 ("logs sin cuerpos ni tokens") |
| 8.8 Gestión de secretos | ⚠️ | MT · 5 · "evitar contraseñas, secretos, llaves privadas" (se refiere al documento y ya está en 1.7) | Reducir a 3 líneas: dónde viven, cuáles genera el proveedor y qué pasa al rotar `BANNED_EMAIL_KEY` |
| 8.9 Datos personales y Ley 1581 | ⚠️ | OP · 4 · "cumplimiento normativo" (como beneficio); MT · 11 · "Seguridad: … cumplimiento" (rol) | Reducir a 3 líneas que remitan a `docs/privacidad-y-tiendas.md` |
| 8.10 Riesgos residuales aceptados | ❌ | Ninguna; "gestiona riesgos" (MT-11) es la función de un rol | Quitar; llevar D6 (sin forward secrecy) a una línea de 8.5 |

Veredicto: MT-8 respalda autenticación, autorización y cifrado, pero el capítulo dedica más espacio a controles que ninguna diapositiva nombra. Además faltan dos que sí nombra: segmentación y auditoría.

### Capítulo 9. Operación, monitoreo, respaldo y recuperación

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 9.1 Estado operativo real y objetivos propuestos | ✅ | OP · 3 · "Debe reflejar el comportamiento real del sistema, no solo su diseño teórico" | Reducir la tabla a las 8 capacidades que luego usan 9.2, 9.3 y 9.6 |
| 9.2 Disponibilidad y rendimiento | ✅ | OP · 5 · "Horario de operación… SLA… Capacidad, tiempos de respuesta y umbrales… Plan de escalamiento" | Mantener |
| 9.3 Monitoreo y observabilidad | ✅ | OP · 6 · "Componentes monitoreados / Alertas y umbrales / Herramientas de observabilidad / Tableros y reportes" | Mantener |
| 9.4 Tareas programadas del sistema | ⚠️ | MT · 14 · "jobs" (como tipo de componente); OP · 8 · "mantenimiento preventivo" | Mantener (6 filas): recibe la tabla de purgas de 6.10 y 9.6 la usa |
| 9.5 Respaldo y recuperación | ✅ | OP · 7 · "Aspecto 3 · Respaldo y recuperación" | Mantener |
| 9.5.1 Política de respaldo | ✅ | OP · 7 · "RTO… RPO… Frecuencia de respaldo… Tipo de respaldo" | Mantener |
| 9.5.2 Procedimiento de respaldo manual | ✅ | MT · 5 · "procedimientos y criterios de validación"; MT · 4 · "recuperación del sistema" | Mantener |
| 9.5.3 Procedimiento de restauración | ✅ | ídem | Mantener |
| 9.6 Ficha de operatividad | ✅ | OP · 10 · "Ejemplo de ficha de operatividad" (aspecto, descripción, objetivo o métrica, herramienta) | Agregar la columna "Dueño" (OP-12) |
| 9.7 Flujo operativo ante una falla | ✅ | OP · 11 · "1. Monitoreo · 2. Alerta · 3. Respuesta · 4. Recuperación" | Mantener |

Veredicto: es el capítulo con mejor paridad del manual (calca OP-3 a OP-11). Solo falta el dueño de cada aspecto.

### Capítulo 10. Solución de incidentes, mantenimiento y anexos

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 10.1 Gestión de incidentes | ✅ | OP · 8 · "Clasificación de incidentes por severidad e impacto… escalamiento y responsables por nivel… lecciones aprendidas" | Quitar la subtabla "Mantenimiento preventivo y correctivo", que repite 10.4 y 10.5 |
| 10.2 Runbooks de incidentes | ✅ | OP · 10 · "Runbook + guardia on-call"; OP · 11 · "El responsable ejecuta el runbook" | Mantener |
| 10.3 Solución de problemas en desarrollo | ⚠️ | MT · 6 · "Solución de incidentes" (esto no son incidentes de producción, son problemas del entorno de desarrollo) | Reducir |
| 10.3.1 Problemas frecuentes del back | ⚠️ | ídem | Dejar las 5 filas más frecuentes |
| 10.3.2 Problemas frecuentes de la app | ⚠️ | ídem | Dejar las 5 filas más frecuentes |
| 10.4 Mantenimiento preventivo | ✅ | OP · 8 · "Procedimientos de mantenimiento preventivo y correctivo" | Mantener |
| 10.5 Mantenimiento correctivo y evolutivo | ⚠️ | OP · 8 · "correctivo" sí; "evolutivo" no aparece, y el contenido es una guía para agregar un endpoint | Reducir a 5 líneas de correctivo (hotfix, prueba que reproduce, CI, ventana) y recibir aquí los procedimientos de 6.6 y 7.8 |
| 10.6 Pendientes técnicos conocidos | ❌ | Ninguna | Dejar solo las 4 filas a las que remiten otras secciones (CI en `Dev`, `/swagger`, registro de borrados, avisos dentro de la app) |
| 10.7 Anexo A. Glosario | ⚠️ | MT · 6 · "anexos" (genérico); MT · 5 · "Lenguaje técnico claro" | Mantener (1 página; sostiene los términos de los capítulos 8 y 9) |
| 10.8 Anexo B. Archivos de referencia | ❌ | Ninguna | Quitar; las rutas ya aparecen en cada sección |
| 10.9 Anexo C. Plantillas de aceptación e incidentes | ✅ | DS · 7 · "Informe de aceptación"; OP · 8 · "Registro de lecciones aprendidas después de cada incidente" | Mantener |
| 10.10 Anexo D. Matriz de cumplimiento | ❌ | Ninguna diapositiva pide autoevaluar el cumplimiento | Quitar del manual (unas 3,5 páginas). Si se quiere entregar, que vaya en una nota aparte para el docente |

Veredicto: lo que respaldan OP-8, OP-10 y DS-7 (10.1, 10.2, 10.4 y 10.9) está bien. La mitad del capítulo es material propio del equipo.

---

## 3. Análisis del software, por capítulo

### Capítulo 1. Control documental

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 1.1 Ficha del documento | ⚠️ | MT · 5 · "Control de versiones" (es el estándar del manual técnico; el bloque de Análisis no pide ficha) | Fusionar en 1.2: repite versión, fecha y autores. Es lo mismo que ya se hizo en el manual 1.2 |
| 1.2 Control y aprobación | ✅ | MT · 5 · "fecha, versión, autor, cambios y aprobaciones" | Mantener |
| 1.3 Historial de cambios | ✅ | MT · 5 · ídem | Mantener |

Veredicto: en paridad salvo la duplicación de 1.1.

### Capítulo 2. Análisis del software

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 2 (sin subsecciones) | ✅ | MT · 10 · "Comprender el problema, la solución, sus tecnologías, actores y componentes permite evaluar su fu[ncionamiento]" | Quitar la tabla de capítulos, que repite la tabla de contenido; dejar los dos párrafos |

### Capítulo 3. Perfiles y roles del equipo de trabajo

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 3 (sin subsecciones) | ✅ | MT · 11 · los 8 roles, de "Product Owner" a "Seguridad" | Llenar "Asumido por" con nombres (ver sección 4) |

### Capítulo 4. Lenguaje de programación y modelamiento de datos

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 4.1 Lenguajes, versiones, frameworks y librerías | ✅ | MT · 12 · "Identificar lenguajes y versiones… Registrar frameworks y librerías principales" | Mantener |
| 4.2 Motor de base de datos y conexión | ✅ | MT · 12 · "Describir el motor de base de datos" | Mantener |
| 4.3 Entidades, tablas, relaciones, claves y restricciones | ✅ | MT · 12 · "Documentar entidades, tablas, relaciones, claves y restricciones" | Débil: lista las tablas pero no sus relaciones (ver sección 4) |
| 4.4 Modelo entidad-relación | ✅ | MT · 12 · "Representar el modelo mediante ERD o UML" | Débil: solo remite al manual. Agregar un ERD global simplificado (25 entidades con sus relaciones, sin columnas) |
| 4.5 Conexiones, dependencias, migraciones, índices y respaldo | ✅ | MT · 12 · "Registrar conexiones, dependencias, migraciones, índices y políticas de respaldo" | Mantener |
| 4.6 Relación entre componentes y estructuras de datos | ✅ | MT · 12 · "Relacionar componentes del software con las estructuras de datos que utilizan" | Débil: solo remite al manual 6.8. Traer esa tabla aquí y en el manual dejar la remisión, porque la diapositiva lo pide en este bloque |

Veredicto: los títulos calcan MT-12. Tres secciones (4.3, 4.4 y 4.6) son remisiones al manual y no se sostienen si el análisis se lee como entregable aparte.

### Capítulo 5. Descripción y análisis del problema

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 5.1 Contexto | ✅ | MT · 13 · "Contexto: ¿qué situación origina la necesidad del sistema?" | Débil: describe el prototipo anterior, no la necesidad de negocio. Agregar 3 líneas sobre quién va solo a conciertos y por qué |
| 5.2 Problema central | ✅ | MT · 13 · "describir la situación actual sin confundirla con la solución" | Mantener |
| 5.3 Causas | ✅ | MT · 13 · "factores técnicos, operativos, humanos o de proceso" | Mantener |
| 5.4 Impacto | ✅ | MT · 13 · "costos, tiempos, errores, riesgos, experiencia del usuario y continuidad" | Mantener |
| 5.5 Usuarios afectados y actores | ✅ | MT · 13 · "identificar actores y necesidades" | Mantener |
| 5.6 Requisitos funcionales | ✅ | MT · 13 · "separar requisitos funcionales y no funcionales" | Mantener |
| 5.7 Requisitos no funcionales | ✅ | MT · 13 · ídem | Mantener |
| 5.8 Objetivo de la solución y cómo se mide | ✅ | MT · 13 · "establecer qué debe mejorar y cómo se medirá" | Mantener |

Veredicto: en paridad completa con MT-13. Solo hay que dar más peso al contexto de negocio.

### Capítulo 6. Módulos y componentes del software

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 6.1 Módulos del back | ✅ | MT · 14 · "propósito, entradas, salidas, dependencias y responsables" | Responsable con nombre |
| 6.2 Módulos de la app | ✅ | MT · 14 · ídem | Responsable con nombre |
| 6.3 Relaciones entre módulos y flujo de información | ✅ | MT · 14 · "Describir las relaciones entre módulos y el flujo de información" | Traer las secuencias del manual 4.5.2 y 4.5.3 |
| 6.4 Componentes críticos y puntos únicos de falla | ✅ | MT · 14 · "Identificar componentes críticos y puntos únicos de falla" | Mantener |
| 6.5 Trazabilidad | ✅ | MT · 14 · "Relacionar módulos con requisitos, base de datos, integraciones y controles de seguridad" | Mantener |
| 6.6 Matriz básica de documentación de componentes | ✅ | MT · 15 · "Módulo · Componente · Tecnología · Dependencias · Responsable" | Responsable con nombre |

Veredicto: en paridad completa con MT-14 y MT-15.

### Capítulo 7. Conclusión

| Sección | Estado | Diapositiva (deck · n.º · frase) | Recomendación |
|---|---|---|---|
| 7 (sin subsecciones) | ⚠️ | MT · "Conclusión" (diapositiva sin número, después de la 15) · "permite relacionar problema, requisitos, arquitectura, tecnologías y componentes". Es la conclusión del docente, no un apartado pedido | Mantener: son 5 líneas y repiten la idea de la diapositiva |

---

## 4. Lo que piden las diapositivas y falta o está débil

| Diapositiva (deck · n.º · frase) | Estado en los documentos | Acción |
|---|---|---|
| UD · 11 y 12 · nodo "Se dibuja como un prisma (caja 3D)"; artefacto "Rectángulo con ícono de documento" | Débil. 5.8.1 y 5.8.2 son cajas planas de un flowchart y el artefacto va como texto dentro del nodo | Redibujar en draw.io o PlantUML (`node`, `artifact`), y exportar SVG |
| MT · 8 · "Seguridad: … segmentación y auditoría" | Falta. Ni 4.2.6 ni el capítulo 8 tratan la segmentación (red interna entre el balanceador y el contenedor, base sin acceso público) ni la auditoría (rastro de las acciones de administración) | Agregar 2 a 4 líneas a 4.2.6 y 8.3. Si no hay registro de auditoría de acciones admin, declararlo |
| MT · 8 · "Operación: … respaldos, alta disponibilidad y recuperación ante fallos" | Débil en 4.2.7: solo nombra health checks, logs, limpieza y CI | Agregar una línea: una sola instancia (sin alta disponibilidad, declarado) y respaldo y recuperación en 9.5 |
| MT · 8 · "Infraestructura: … balanceadores" | Débil en 4.2.4 (el balanceador solo aparece en 5.8.1) | Agregar "balanceador TLS de Render" |
| OP · 12 · "Cada aspecto —disponibilidad, monitoreo, respaldo, mantenimiento— debe tener dueño y métrica" | Falta: 9.6 tiene métrica pero no dueño | Agregar la columna "Dueño" con una persona |
| MT · 5 · "Definir responsables"; MT · 11 · roles; MT · 14 · "responsables"; MT · 15 · columna "Responsable" | Débil: todas las celdas dicen "Equipo Encorely" (Manual 1.4 y 5.5.2; Análisis 3, 6.1, 6.2 y 6.6) | Asignar a una persona por rol y por módulo; "compartido" queda como nota, no como valor |
| DS · 5 · "Capacitación de usuarios: Entrenar a quienes usarán el sistema en su operación diaria" | Débil: la actividad 6 de 5.5.3 capacita a moderación y a la guardia; para el usuario final solo publica el manual | Agregar la capacitación del usuario final (onboarding dentro de la app y el manual de usuario como material) |
| MT · 12 · "Representar el modelo mediante ERD o UML" | Débil en *Análisis*, 4.4: solo remite al manual | ERD global simplificado en el análisis |
| MT · 12 · "Documentar entidades, tablas, relaciones, claves y restricciones" | Débil en *Análisis*, 4.3: no hay relaciones | Resolverlo con el ERD anterior y una tabla de relaciones 1:N principales |
| MT · 12 · "Relacionar componentes del software con las estructuras de datos" | Débil en *Análisis*, 4.6: solo remite | Mover la tabla del manual 6.8 al análisis |
| MT · 14 · "Describir las relaciones entre módulos y el flujo de información" | Débil en *Análisis*, 6.3: 4 pasos en texto; los diagramas están en el manual 4.5 | Mover las secuencias UML al análisis |
| MT · 13 · "Contexto: ¿qué situación origina la necesidad del sistema?" | Débil en *Análisis*, 5.1: justifica reconstruir el sistema, no por qué existe | Agregar el contexto de negocio |
| DS · 7 · "Software en producción: La versión final, operando en el entorno real del usuario" | No aplica por la decisión D15; está declarado en 5.5.1 y 5.7 | Ninguna; ya está declarado |

---

## 5. Estimación de páginas si se aplican las recomendaciones

Manual técnico (92 páginas hoy; rangos tomados del PDF):

| Capítulo | Páginas hoy | Cambio estimado | Origen principal |
|---|---|---|---|
| 2 | 2 (p. 12–13) | −0,3 | Tabla D1–D15 |
| 3 | 4 (p. 14–17) | −0,2 | Párrafo de pnpm |
| 4 | 6 (p. 18–23) | −1,5 | 4.1 fusionada, 4.5.1 quitada, 4.5.2 y 4.5.3 al análisis |
| 5 | 13 (p. 24–36) | −1,0 | 5.3.3, 5.4 y variantes de 5.1 |
| 6 | 22 (p. 37–58) | −1,0 | 6.7, 6.9 y 6.10; las fichas no se tocan |
| 7 | 8 (p. 59–66) | −4,0 | Catálogo 7.2, fusión en 7.9 y 7.8 |
| 8 | 6 (p. 67–72) | −2,0 | Modelo de amenazas, 8.4 y 8.6 a 8.10; +0,2 por segmentación y auditoría |
| 9 | 8 (p. 73–80) | −0,3 | Tabla de 9.1; +columna de dueño en 9.6 |
| 10 | 12 (p. 81–92) | −5,5 | 10.10 (unas 3,5 p.), 10.8, 10.3, 10.5 y 10.6 |
| **Total** | **92** | **≈ −16** | **≈ 76 páginas** |

Análisis del software (23 páginas hoy): −0,5 por fusionar 1.1 y quitar la tabla del capítulo 2, y +1,5 a +2 por el ERD global, la tabla de 6.8 y las dos secuencias UML. Neto: unas **24 a 25 páginas**.

Entre los dos documentos, de 115 páginas se pasa a unas 100, y lo que se mueve queda en el documento donde la diapositiva lo pide.
