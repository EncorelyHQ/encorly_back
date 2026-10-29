# Plan del front: perfil social, stickers y motion

2026-10-14 · Aprobado por Camilo.

**Estado:** el plan está completo. Todo quedó mergeado en `Dev` (`c8dbb64`):
- #9: PR 1
- #10: PR 2
- #11: PR 3
- #12: draft.12
- #13: PR 4
- #14: PR 5 y cierre

El draft.13 del back no cambia el contrato ni requiere cambios en el front. El 0.2 (`expo install --fix`) sigue pendiente.

**Desvíos del plan:**
- El hook de scroll se llama `useScreenScroll()` porque Reanimated ya exporta un `useScrollOffset`. La pantalla de prueba es `encorely://dev-motion` y está en la raíz para abrirla sin sesión. "Temporada" pasa al PR 4, porque hoy `MeScreen` no la tiene.

**QA en el emulador:**
- **Verificado:** reduce motion, la fuente al 200 %, la etiqueta pegada, la hoja de reacciones, "Así te ven" con conteos y las etiquetas que lee TalkBack (revisadas con `uiautomator`). Para el perfil se usó el laboratorio `encorely://dev-profile`, con datos de prueba.
- **Sin certificar:** los fps. El emulador cae a render por software (`swangle`), con 300 ms de p50 contra 200 ms con reduce motion. Hay que medirlos en un teléfono real.
- **Probado con sesión real (2026-10-15):** la API local (`dev` del back, draft.13) corre contra `encorely_db`, ya migrada. Con dos cuentas QA (`qa.ana.10151105@` y `qa.bruno.10151105@encorely.test`, contraseña de prueba `QA-perfil-local-2026`) se probó todo de punta a punta:
  - Tú y "Así te ven" con conteos reales.
  - `/reactions`.
  - El perfil ajeno desde el radar, el match, el chat y una reacción, con "70 % de gustos en común" (72 redondeado).
  - Una reacción hecha en la app que llegó al servidor.
  - "Me interesa" que terminó en match.
  - El interruptor de privacidad, que devuelve 404 al otro y oculta las reacciones.
  - La pantalla de no disponible.
- **Arreglado tras la prueba** (#15, mergeado):
  - "Escribir" cuando ya hay match.
  - El 404 ya no deja el nombre en caché ni insinúa la razón.
  - La perilla del switch ahora se ve.
  - Los artistas sin foto se muestran como cartel con el nombre.

**Base:** rama nueva `feat/perfil-social` desde `Dev`, contra el contrato draft.11 del back.
**Fuera de este plan:** el chat, Descubrir y los eventos. Solo se tocan donde enlazan al perfil.

## 0. Antes de empezar (bloqueos)

| # | Qué | De quién depende |
|---|---|---|
| 0.1 | Pushear `35528f4` (draft.11) y mergear en `dev` del back los PRs #4 → #5 → #6, en ese orden | Tu OK en la sesión del back |
| 0.2 | Volver a poner `Dev` del front en verde: `pnpm exec expo install --fix`, que se puede correr cuando los parches de Expo cumplan la edad mínima (≈ mañana), en un PR aparte | Nadie |

Mientras 0.1 no esté en `dev`, el front puede generar los tipos desde la rama del back. No hay que esperar.

## 1. Contrato y datos (PR 1)

Corre en paralelo con el PR 2. Sin UI nueva, solo que todo compile y tenga hooks listos.

- **`pnpm gen:api`** contra el draft.11. En `types.ts` van los alias nuevos: `PublicProfile`, `ProfileArtist`, `ProfileStats`, `ReactionType`, `ReactionTargetKind`, `ReceivedReactionPage`.
- **`endpoints.ts`:**
  - `usersApi.profile(id)`
  - `usersApi.react(id, body)` y `usersApi.unreact(id, kind, key)`
  - `meApi.receivedReactions(cursor)`
  - `showProfileToOthers` dentro de `meApi.update`
- **Hooks:**
  - `useProfile(userId)`
  - `useReaction(userId)`, con cambio optimista y vuelta atrás si falla
  - `useReceivedReactions()`, una lista infinita con `nextCursor`
- **Errores, mapeados en un solo lugar:**
  - `404 user_not_found` → "Este perfil no está disponible", sin decir por qué.
  - `400 reaction_target_not_found` → recargar el perfil.
  - `cannot_react_self` → no debería llegar nunca: la UI no ofrece reaccionarse a uno mismo.
- **La importación de Spotify** empieza a mandar `imageUrl` (`images[0].url`).
- **Lo que rompe el contrato:**
  - `affinity` puede venir vacía (`RadarPersonRow`, sus fixtures).
  - `/me` trae campos nuevos (la ficha de prueba de `profileForm.test`).
- **Consentimiento 2026-10-14:**
  - Revisar que la pantalla `consent` explique el perfil público.
  - Revisar que el banner aparezca cuando `profileVisible` está en false aunque el interruptor esté encendido.
- **FIN-06** (el 429 del login ahora cuenta por correo + IP): no cambia la forma de los datos, pero hay que confirmar que el mensaje sigue siendo correcto.

**Hecho cuando:** `pnpm verify` pasa y hay tests de los hooks con un servidor falso, como en el chat.

## 2. Base de motion y stickers (PR 2)

No depende del back.

- **`useMotion()` en `src/ui`:** un solo lugar que decide reduce motion (el del sistema más `ReduceMotion`). Lo usan las piezas nuevas. Los ~30 sitios viejos se migran después, sin prisa.
- **`Screen` con scroll animado:** `Animated.ScrollView` y un contexto `useScrollOffset()` para que cualquier hijo lea el scroll en el hilo de UI.
- **Piezas nuevas:**
  - `ScrollReveal`: entra con `rotateX` de 14° a 0, `translateY` de 28 a 0 y opacidad, según la posición y el scroll.
  - `ParallaxTicket`: la boleta que se inclina hasta 12° y se escala a 0,92.
  - `Carousel3D`: un `FlatList` horizontal con `rotateY` de ±18°.
- **`Sticker` en `src/ui`:**
  - Variantes papel, stamp, banda y tinta, en tamaños título, chip y etiqueta.
  - El ángulo es fijo y lo pasa quien lo usa.
  - La sombra dura va como una `View` desplazada detrás, porque en Android `elevation` no da sombra sin blur.
  - Con la fuente del sistema al 150 % o más, la inclinación baja a ±1° y el texto hace salto de línea.
- **`StickerTitle`:** se pega a medida que la sección entra, con el mismo progreso del scroll.
- **`ReactionTag`:** se pega con un spring de 440 ms y `haptics.selection`.
- **Tokens:** se agrega el contorno `#FFFDF8` y los colores de sticker en `tokens.ts`.

**Hecho cuando:** hay una pantalla de prueba solo en desarrollo, medida en el emulador x86_64 a 60 fps al hacer scroll, que funciona con reduce motion, con la fuente al 200 % y con TalkBack.

## 3. Números honestos (PR 3)

Es chico y se puede sacar apenas esté el PR 1.

- **Radar:**
  - Muestra "80 % de gustos en común", redondeado a múltiplos de 5, siempre con la razón al lado.
  - Si `affinity` viene vacía, solo muestra "Les gusta rock e indie".
  - Una hoja "¿Cómo lo calculamos?" explica el cálculo y aclara que con más canciones se afina.
- **`GenreBars`:** los porcentajes se calculan sobre el total y hay una fila "Otros", así que suman 100. Se actualiza el texto de abajo.
- **"Temporada":** usa los `stats` del back: "marcados", no "asistidos".

## 4. Tú, "Así te ven" y privacidad (PR 4)

- **`MeScreen` reorganizada:**
  - Arriba la boleta con tus géneros como stickers y la bio, que hoy no se muestra.
  - Debajo "Tu música" (con sticker de título), "Tus artistas" (`Carousel3D`) y "Tu temporada".
  - Los ajustes siguen abajo como lista nativa, sin stickers.
- **Pestañas "Tu perfil | Así te ven":** la segunda pinta tu propio perfil público (`GET /users/{tuId}/profile`), con los conteos de reacciones.
- **Pantalla nueva `/(app)/reactions`:** las reacciones recibidas con nombre, paginadas. Tocar una abre el perfil de esa persona, y desde cada fila se puede bloquear o reportar.
- **Privacidad:**
  - El interruptor "Mostrar mi perfil a otros" (`PATCH /me`).
  - Si `profileVisible` está en false, un aviso que dice por qué: o el interruptor está apagado, o falta aceptar la versión nueva, con su botón.

## 5. Perfil de otra persona y reacciones (PR 5)

- **Ruta `/(app)/person/[userId]`.** Se entra desde:
  - una fila del radar
  - la pantalla de match
  - el encabezado del chat
  - una reacción recibida
- **Pantalla:**
  - `ParallaxTicket` con nombre, edad, ciudad, géneros como stickers y bio.
  - "En común" con el porcentaje, cuando lo hay.
  - "Lo que suena": `Carousel3D` con fotos (`expo-image`). Los artistas de "Me gusta" no traen foto y se muestran como tarjeta tipográfica.
  - "Su temporada".
  - Abajo, "Me interesa" (`POST /likes` sin evento: el contrato lo permite).
- **Hoja de reacciones:** los tres stickers, la de cada uno marcada, y "Quitar mi sticker". Usa el hook optimista del PR 1.
- **Menú ⋯:** Bloquear y Reportar, con las APIs que ya existen. El reporte por acoso usa `harassment`.
- **Estados:**
  - Mientras carga, el esqueleto de la boleta.
  - Con 404, "Este perfil no está disponible".
  - Sin red, el error de siempre con Reintentar.

## 6. Cierre: QA y documentación (siempre al final de cada PR, y una vez al cierre)

- **Tests:**
  - El redondeo y el texto de la afinidad.
  - Que `GenreBars` sume 100.
  - El reductor optimista de las reacciones.
  - La paginación de las recibidas.
  - Los mapeos de errores.
- **En el emulador:** venue y papel, reduce motion, fuente al 200 %, TalkBack y fps al hacer scroll. Hay que cuidar la RAM: el build solo para x86_64.
- **Documentación:**
  - `ARCHITECTURE.md`: 3D solo con transformaciones nativas, cuándo van stickers y cómo se manejan los números.
  - Una nota en `reportes/` que corrija la §5.9 de la auditoría 03.

## Orden y tamaño

```
0.1 back ──┐
0.2 Expo ──┤
           ├─ PR1 datos ──┬─ PR3 números ─┐
PR2 motion+stickers ──────┴───────────────┼─ PR4 Tú ─ PR5 perfil ajeno ─ cierre
```

Estimado: **PR1** ≈ 1 sesión · **PR2** 1–2 · **PR3** ½ · **PR4** 1–2 · **PR5** 2 · **cierre** ½. Unas 6 a 8 sesiones en total, cada una con su PR a `Dev`.

## Riesgos

- **Sombra dura en Android:** la solución de la `View` desplazada se valida en el PR 2. Si no se ve bien, queda solo el contorno.
- **Rendimiento del 3D en teléfonos de gama baja:** si en el emulador baja de 60 fps, se recorta primero el carrusel.
- **Fotos de Spotify:** el `imageUrl` solo llega a quien vuelva a importar después de este cambio. Los perfiles anteriores se ven sin foto hasta entonces.
