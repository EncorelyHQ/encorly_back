# Requisitos del manual técnico y del análisis del software (checklist de la rúbrica)

Fuente: guías de clase del docente Cristian Alzate Botero — *Manual Técnico y Análisis del Software*, *Operatividad del Sistema*, *Despliegue del Software*, *Estructura de la BD y Tablas de Contenido* y *Manual de Usuario y Diagrama de Despliegue*.

Documentos:

- **MT** = `MANUAL-TECNICO-ENCORELY.md` (ENC-MT-001, versión 1.3).
- **AS** = `ANALISIS-DEL-SOFTWARE-ENCORELY.md` (ENC-AS-001, versión 1.1).

Cada ítem indica el documento y la sección donde se cumple. Este checklist es interno: desde la versión 1.3 el manual ya no lleva matriz de cumplimiento (la auditoría de paridad está en `REVISION-PARIDAD.md`).

## Checklist 1 — Manual técnico (MT)

### 0. Qué es el manual técnico (diap. 4)
- [x] MT-1. Explica cómo está construido, configurado, desplegado y mantenido el sistema; comprensible para desarrollo, administración, soporte e infraestructura; controlado por versiones → MT 1.3, 1.1 y 1.2; capítulos 4, 5, 9 y 10.

### A. Estándares de elaboración (diap. 5) y herramientas (diap. 7)
- [x] A1. Control de versiones del documento: fecha, versión, autor, cambios y aprobaciones → MT portada, 1.1 (incluye la línea de aprobación) y 1.2.
- [x] A2. Lenguaje técnico claro, preciso y consistente → todo MT; reglas en MT 1.6.
- [x] A3. Convenciones de nombres para servidores, bases de datos, APIs, módulos y ambientes → MT 1.5.
- [x] A4. Diagramas normalizados y legibles (UML, BPMN o de arquitectura) → MT 4.2, 4.3.1, 4.3.2, 5.8.1 y 5.8.2 (PlantUML), 6.2.1 a 6.2.5, 9.7; AS 4.4 (ERD global) y 6.3.1 y 6.3.2 (secuencias UML).
- [x] A5. Sin contraseñas, secretos, llaves privadas ni datos sensibles → MT 1.7 y 8.7.
- [x] A6. Prerrequisitos, dependencias, procedimientos y **criterios de validación** documentados → MT 5.1, 5.2, 5.5.4, 5.5.5, 5.6, 9.5.2, 9.5.3 y 10.5 (crear una migración e integrar un draft del contrato).
- [x] A7. Responsables y periodicidad de actualización del documento, con un responsable con nombre por capítulo → MT 1.1 y 1.4.
- [x] A8. Herramientas para elaborar el manual (solo las usadas) → MT 1.8.

### B. Estructura obligatoria, en este orden (diap. 6)
- [x] B1. Portada, control documental e historial de cambios → MT portada y capítulo 1.
- [x] B2. Objetivo, alcance y descripción general del sistema → MT capítulo 2 (2.1, 2.2, 2.3).
- [x] B3. Requisitos de hardware, software y red → MT capítulo 3.
- [x] B4. Arquitectura y diagramas de componentes → MT capítulo 4.
- [x] B5. Instalación, configuración y despliegue → MT capítulo 5.
- [x] B6. Base de datos y modelo de información → MT capítulo 6.
- [x] B7. Integraciones, APIs y servicios externos → MT capítulo 7.
- [x] B8. Seguridad, perfiles y permisos → MT capítulo 8.
- [x] B9. Operación, monitoreo, respaldo y recuperación → MT capítulo 9.
- [x] B10. Solución de incidentes, mantenimiento y anexos → MT capítulo 10 (anexos A y B en 10.7 y 10.8).
- [x] B11. Ningún capítulo `##` fuera de los diez; los títulos son literales de la diapositiva 6 → MT tabla de contenido.

### C. Arquitectura por capas y arquitectura lógica (diap. 8 y 9)
- [x] C1. Capa de presentación → MT 4.1.1.
- [x] C2. Capa de aplicación → MT 4.1.2.
- [x] C3. Capa de datos → MT 4.1.3 (detalle en el capítulo 6).
- [x] C4. Infraestructura, incluido el balanceador → MT 4.1.4 (detalle en 3.2.3 y 5.8).
- [x] C5. Integraciones → MT 4.1.5 (detalle en el capítulo 7, servicios externos en 7.4).
- [x] C6. Seguridad: autenticación, autorización, cifrado, **segmentación y auditoría** → MT 4.1.6; detalle en MT 8.2, 8.3, 8.4, 8.5 (segmentación) y 8.6 (auditoría).
- [x] C7. Operación: monitoreo, logs, respaldos, **alta disponibilidad** (no existe hoy; declarada y propuesta como objetivo) y recuperación → MT 4.1.7; detalle en MT 9.2 (alta disponibilidad), 9.3 y 9.5.
- [x] C8. Diagrama de arquitectura lógica (usuarios → presentación → aplicación → datos → infraestructura) → MT 4.2.
- [x] C9. Diagramas de componentes → MT 4.3. Relaciones y flujo de información → AS 6.3 (con las secuencias 6.3.1 y 6.3.2).

### E. Base de datos (por cada tabla)
- [x] E1. Ficha técnica: nombre y propósito; campos con tipo y longitud; PK; FK y relaciones; restricciones (NOT NULL, UNIQUE, DEFAULT) e índices → MT 6.3.1 a 6.3.25 (servidor), 6.5.1 y 6.5.2 (dispositivo).
- [x] E2. Diccionario de datos: significado de cada campo en lenguaje de negocio → MT columna "Significado" de cada ficha y 6.4.
- [x] E3. Diagrama entidad-relación que complementa las fichas → MT 6.2.1 a 6.2.5.
- [x] E4. Nombres de tablas y campos **idénticos** a los del código real → MT 6.3 (de `AppDbContextModelSnapshot.cs`) y 6.5 (de `sqliteStore.ts`).
- [x] E5. Cuándo se actualiza: con cada cambio de esquema → MT 1.4, 10.5 (paso 5 del procedimiento de migración) y 6.10.

### H. Tabla de contenido
- [x] H1. Listado jerárquico numerado (1, 1.1, 1.1.1) con número de página real → MT tabla de contenido, justo después de la portada.
- [x] H2. Cada entrada coincide literalmente con el título del documento → MT tabla de contenido, generada desde los títulos.
- [x] H3. Formato consistente (sangría por nivel y línea de puntos) → MT tabla de contenido.

### F. Despliegue
- [x] F1. Plan de despliegue: cronograma, responsables, ventana de tiempo y **plan de rollback** (acompaña toda la secuencia) → MT 5.5.2 (responsable con nombre por actividad) y 5.6 (responsable por tipo de rollback).
- [x] F2. Actividades: planificación → preparación del entorno → instalación y configuración → migración de datos → pruebas de aceptación → capacitación → go-live → monitoreo posterior → MT 5.5.3 (la capacitación incluye a las personas usuarias finales: onboarding en la app y manual de usuario).
- [x] F3. Productos: plan de despliegue, manual de instalación, software en producción, documentación de usuario, informe de aceptación y plan de rollback → MT 5.7 y plantilla de 10.8.

### F4. Diagrama de despliegue
- [x] F4. **Diagrama de despliegue UML**: nodos («device» / «execution environment») dibujados como prisma, artefactos con ícono de documento dentro de su nodo y rutas de comunicación etiquetadas con protocolo y puerto → MT 5.8.1 (producción) y 5.8.2 (demo actual), en PlantUML.

### G. Operatividad
- [x] G1. Disponibilidad y rendimiento: horario de operación y ventanas de mantenimiento, SLA, capacidad, tiempos de respuesta y umbrales, plan de escalamiento ante degradación → MT 9.2.
- [x] G2. Monitoreo y observabilidad: componentes monitoreados, alertas y umbrales, herramientas (logs, métricas, trazas), tableros → MT 9.3.
- [x] G3. Respaldo y recuperación: **RTO**, **RPO**, frecuencia y tipo de respaldo → MT 9.5.1 (procedimientos en 9.5.2 y 9.5.3).
- [x] G4. Mantenimiento e incidentes: preventivo y correctivo, ventanas y comunicación, clasificación por severidad e impacto, escalamiento y responsables por nivel, lecciones aprendidas → MT 10.1 (escalamiento por nivel con responsables con nombre), 10.2, 10.4, 10.5 y plantilla de 10.8.
- [x] G5. **Ficha de operatividad**: Aspecto | Descripción | Objetivo/Métrica | Herramienta | **Dueño** (cada aspecto con dueño y métrica, OP-12) → MT 9.6.
- [x] G6. **Flujo operativo ante una falla**: monitoreo → alerta → respuesta (runbook) → recuperación y documentación → MT 9.7.
- [x] G7. Refleja el comportamiento **real** del sistema y distingue objetivos de mediciones → MT 9.1 y marca "objetivo" en 9.2 y 9.6.

## Checklist 2 — Análisis del software (AS, diap. 10 a 15)

- [x] D0. Análisis del software: comprender el problema, la solución, sus tecnologías, actores y componentes (diap. 10) → AS capítulo 2.
- [x] D1. Perfiles y roles del equipo: Product Owner, analista, arquitecto, desarrollador, DBA, QA, DevOps, seguridad, con la persona que asume cada uno (diap. 11) → AS capítulo 3.
- [x] D2. Lenguajes y versiones, frameworks y librerías principales, motor de BD (diap. 12) → AS 4.1 y 4.2.
- [x] D3. Entidades, tablas, relaciones, claves y restricciones; ERD/UML; conexiones, dependencias, migraciones, índices y política de respaldo; relación componente ↔ estructuras de datos (diap. 12) → AS 4.3 (tabla de relaciones 1:N), 4.4 (ERD global simplificado), 4.5 y 4.6 (tabla componente ↔ tablas); fichas y vistas del ERD en MT capítulo 6.
- [x] D4. Descripción y análisis del problema: contexto, problema central (sin confundirlo con la solución), causas, impacto, usuarios afectados, requisitos **funcionales y no funcionales separados**, objetivo de la solución **y cómo se mide** (diap. 13) → AS 5.1 (con el contexto de negocio) a 5.8.
- [x] D5. Módulos y componentes: por módulo, propósito, entradas, salidas, dependencias y responsables; relaciones y flujo de información; **componentes críticos y puntos únicos de falla**; relación con requisitos, BD, integraciones y seguridad (diap. 14) → AS 6.1 y 6.2 (responsable con nombre), 6.3 (secuencias UML), 6.4 y 6.5.
- [x] D6. **Matriz básica de documentación de componentes**: Módulo | Componente | Tecnología | Dependencias | Responsable (diap. 15) → AS 6.6 (responsable con nombre).
- [x] D7. Conclusión (diapositiva final) → AS capítulo 7.

## Checklist 3 — Agregado en la paridad estricta (MT 1.3 / AS 1.1)

- [x] S1. Segmentación (MT-8) → MT 4.1.6 y 8.5.
- [x] S2. Auditoría (MT-8): lo que se registra hoy y lo que falta, con objetivo → MT 4.1.6 y 8.6.
- [x] S3. Balanceador (MT-8, UD-14) → MT 4.1.4 y 5.8.1.
- [x] S4. Alta disponibilidad (MT-8): una sola instancia, declarada; objetivo propuesto → MT 4.1.7 y 9.2.
- [x] S5. Capacitación de usuarios finales (DS-5) → MT 5.5.2 y 5.5.3, actividad 6.
- [x] S6. Dueño por aspecto operativo (OP-12) → MT 9.6, columna "Dueño".
- [x] S7. Responsables con nombre (MT-5, MT-11, MT-14, MT-15) → MT 1.4, 5.5.2, 5.6, 9.6, 10.1; AS 3, 6.1, 6.2 y 6.6.
- [x] S8. Contexto de negocio (MT-13) → AS 5.1.
- [x] S9. ERD global, relaciones 1:N y relación componentes ↔ estructuras de datos (MT-12) → AS 4.3, 4.4 y 4.6.
- [x] S10. Relaciones entre módulos y flujo de información con secuencias UML (MT-14) → AS 6.3.1 y 6.3.2.
