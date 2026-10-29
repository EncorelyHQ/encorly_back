# 03 · Auditoría de diseño — Encorely (app nativa + landing)

> ⚠️ DEGRADED: single-context. Corrí el critique y el audit de *impeccable* en un solo contexto y no en dos subagentes aislados: el navegador solo lo podía usar un agente a la vez y el proyecto estaba en modo solo lectura. Por esa misma restricción no guardé el snapshot en `.impeccable/critique/`.
> Fecha: 2026-10-13 · Rama landing: `feat/reconstruction` · Canvas principal: **390×844 (móvil nativo)**. La web de Expo solo sirvió para revisar en local; lo que pasa únicamente en web aparece aparte (§3.1) y con prioridad baja.
> Evidencia: capturas nuevas `qa-enjambre/2026-10-13-2104/capturas/diseno-1…15.png` (son JPEG con extensión .png), las capturas previas `ui1-*`, `ui2-*`, `ui3-*` y el código, citado como `archivo:línea`. Ya documentados en `01-auditoria-externa.md`, aquí solo se referencian: **C15** (UI que miente), **I16** (accesibilidad) e **I17** (no hay design system).

---

## 1. Veredicto

Encorely no tiene identidad visual. Junta los recursos por defecto de un generador: fondo casi negro con un gradiente morado igual en 10 pantallas, magenta `#F366FF` con glow en botones, títulos y tab bar, glassmorphism, emojis en vez de iconos, Golos + Inter y copy de pitch deck ("Similitud coseno >70% = match garantizado", "No es azar, es ciencia rítmica").
Por eso se nota que lo hizo una IA: nada del diseño viene del dominio. Ninguna pantalla habla de conciertos, boletas, carteles, setlists ni de Bogotá o Medellín. Cambias "música" por "fitness" y la UI sirve igual.
Además, cada superficie es una marca distinta. El login usa rojo/violeta y la fuente del sistema, la app usa magenta con Golos y la landing usa violeta→magenta con gradientes animados.
El texto mezcla voseo argentino, inglés y tuteo, y la app muestra cifras inventadas (247 personas, 100 % "Match Certificado" para todos, "Night Vibes").
No se arregla puliendo: hay que reemplazar el mundo visual, conservando el producto, el flujo y los componentes funcionales.

**Puntajes (impeccable):** app, Nielsen **12/40** y audit técnico **6/20** (*Poor*). Landing, Nielsen **19/32** (H7 y H10 n/a) y audit **11/20** (*Acceptable*).

---

## 2. Inventario de "tells" de diseño genérico/IA

| # | Tell | Evidencia | Por qué delata plantilla |
|---|---|---|---|
| T1 | **Emojis como iconos** (~30 usos) | `LoginScreen.tsx:120` (🎵 de logo), `:128-131` (⚡🎯📡), `RadarScreen.tsx:144` (🌍 a 60 px), `:152` (📡), `RadarMatchesScreen.tsx:88` (🌟), `:113` ("Conectar 🎵"), `MatchesScreen.tsx:74,76` (🌟👋), `OnboardingStep1Screen.tsx:227`, `SwipeScreen.tsx:282` (⚙️). El "logo" de Spotify es el carácter `♪` (`LoginScreen.tsx:32`, `SpotifyConnectCard.tsx:162`). Capturas `diseno-12`, `diseno-13`, `diseno-14` | Es lo que hace un LLM cuando no hay un sistema de iconos. Cada SO renderiza el emoji distinto (el 🌍 de Android no se parece al de iOS), no hereda color ni peso y no se puede tematizar |
| T2 | **Magenta neón + glow** | `#F366FF` hardcodeado **83 veces**. Sombras `neonMagenta` en `designTokens.ts:37`. Text-shadow con glow en `SwipeScreen.tsx:78-80` ("¡RADAR DESBLOQUEADO!"), en el título "Radar Social" y en los sellos (`SwipeCard.tsx:419-427`). Tab bar con `shadowColor:'#F366FF'` (`(main)/_layout.tsx:122`). Landing: `cta-glow` en bucle infinito (`index.css:88,152`) | La paleta "violeta/magenta sobre negro con resplandor" es la que más sale cuando se le pide a un modelo algo "moderno y musical". El glow sin desplazamiento no da profundidad, solo decora |
| T3 | **El mismo gradiente de fondo copiado en 10 pantallas** | `['#181818','#2a1a3a','#181818']` en `ScreenShell.tsx:20`, `Chat:150`, `Settings:425`, `Profile:621`, `Radar:137`, `Matches:102`, `RadarMatches:124`, `OnboardingScreen:401`, `HomeScreen:364`, `SwipeCard:315` | Se pega una banda morada al centro "para que no sea negro plano". No viene de ninguna luz ni escena, y en móvil hace banding en pantallas OLED |
| T4 | **Glassmorphism decorativo** | `BlurView` en la tab bar, las tarjetas del radar, el overlay del swipe y el header y composer del chat. Landing: `.glass` y `.glass-panel` en `index.css:176-193`, donde el comentario dice literalmente "Glassmorfismo SIN desenfoque" | El vidrio no separa capas sobre contenido real: debajo solo hay un fondo liso. En Android `BlurView` cuesta GPU y batería, justo lo que escasea en un concierto |
| T5 | **Texto con gradiente animado** | `index.css:142` (el detector de impeccable lo marca), en el H1 del hero (`Hero.tsx:58`) y en las cifras (`SocialProof.tsx:46`) | Es el tell número 1 de las landings generadas. El énfasis debería venir del peso o el tamaño |
| T6 | **Eyebrow en píldora sobre cada heading** | Medí **14** etiquetas en mayúsculas con tracking en la landing (`SectionHeading.tsx:17`), entre ellas "En 4 pasos", "Lo que te llevas", "Pruébalo", "Tu diferencia" y "Antes de que preguntes". En la app: "ENCORELY" y "NUEVO AQUÍ" (`diseno-13`) | Es el andamiaje de las plantillas SaaS. El título no se sostiene solo y por eso le ponen una etiqueta encima |
| T7 | **Tarjetas idénticas y centradas, más la fila de métricas** | Features = 3 tarjetas icono+badge+título+texto+waveform (`diseno-3`). Valores = 3 columnas centradas con icono ("Conexión auténtica / Diversidad musical / Experiencia vibrante", `diseno-7`). Fila "4D · 100% · $0" (`diseno-6`). Marquee de géneros | Es el esqueleto de cualquier landing SaaS. Los "valores" son tres sustantivos abstractos que cualquier startup podría firmar |
| T8 | **Fondo animado con notas flotantes y auroras** | `GlobalBackground.tsx`: 18 iconos de notas en *drift/bob* y 3 auroras con `blur-[150px]`. Con la página cargada medí **109 animaciones activas** (`document.getAnimations()`) | "Música" se dice con 🎵 flotando. Es ruido visual que no aporta información y gasta batería en móvil |
| T9 | **Copy con jerga técnica y hype** | "Similitud coseno >70% = match garantizado" y "Vector de Vibe desde tus canciones reales" (`LoginScreen.tsx:128-129`), "Tu tribu musical te espera", "No es azar, es ciencia rítmica", "Calidad garantizada", "nunca te pierdas un beat", "plataformas que ya amas" (`es.json`) | Usa la fórmula "No es X, es Y", promesas absolutas y anglicismos de marketing. Encima, el usuario de 20 años no sabe qué es un coseno |
| T10 | **Cifras y datos inventados** | "247 personas con tu vibe cerca" está hardcodeado (`RadarScreen.tsx:152`, **C15**). En la misma sesión la lista dice "0 personas cerca de ti" (`diseno-12`). "Match Certificado" aparece en todos con 100 % (`RadarMatchesScreen.tsx:75-88`, `diseno-14`). "Night Vibes" y "Última actualización: Hoy" son fijos (`HomeScreen.tsx:416,428`). La demo de la landing usa "Midnight City Vibes — The Synthetics" (`SoundSwipeDemo.tsx:21`) | Es relleno plausible típico de un generador. Una persona real lo detecta en segundos y desde ahí deja de creer el resto |
| T11 | **Idiomas mezclados** | La tab dice "Discover" y las demás están en español. Además "Tu Music DNA", "Vibe Explorer", sellos "LIKE/NOPE" (`SwipeCard.tsx:321,324`), "Sound-Swipe", "Live Hub", "The Lineup", footer "Don't miss the beat, don't go alone." y "energy, danceability, valencia y tempo" (`diseno-13`) | Son nombres de feature puestos al azar, sin una regla de voz |
| T12 | **Voseo rioplatense para público colombiano** | "Conectá… descubrí… encontrá… como vos… acá" (`OnboardingStep1Screen.tsx:235`), "Creá tu cuenta", "podés" (`Step3:151`), "Iniciá sesión" (`Step4:45`), "Elegí… Seleccioná" (`Step5:145`), "Dale que falta poco" (`Step2:212`), "Completá 25 swipes" (`RadarMatchesScreen.tsx:54`), "Probá de nuevo" (`SpotifyAuthContext.tsx:187`). Conviven con 18 imperativos en tuteo | Huella típica de copy generado sin anclaje regional. En Colombia se lee de inmediato como "esto no lo escribió alguien de acá" |
| T13 | **Tipografía por defecto** | Inter (cuerpo) + Golos Text (titulares 700/900). El login no usa ninguna de las dos: `fontWeight:'900'` sin familia, o sea la fuente del sistema (`LoginScreen.tsx:263`, estilo `appName`) | Inter es la fuente por defecto de toda la generación de UI. Golos, en esos pesos, es "Inter con más grosor". Ninguna dice concierto ni Colombia |
| T14 | **Iconografía mezclada** | Ionicons (app), Lucide (landing), emojis, Unicode (`♪ ✦ ✓ →`). Lucide `Music` hace de logo de TikTok y `Twitter` de X (`Footer.tsx:23-25`, además con `href:'#'`). El logo de Ticketmaster sale como una raya azul ilegible (`Hero.tsx:98`, `diseno-10`) | Hay cuatro sistemas sin pesos ni trazos comunes |
| T15 | **Mockups 3D de iPhone flotante con tilt, *bob* y una mano** | `Hero.tsx` (tilt con el cursor más `animate-bob`), `HowItWorks` (4 renders, uno con mano, `diseno-2`). El mockup del hero dice "No vuelvas a ir solo a un concierto" y la app real dice "No vayas solo a tu próximo show" | Son renders genéricos que no corresponden a la UI real, y el tilt+flotación es un patrón de plantilla |
| T16 | **Celebraciones en mayúsculas con glow** | "¡ONBOARDING LISTO!" y "¡RADAR DESBLOQUEADO!" (`SwipeScreen.tsx:254,276`), "¡Felicidades! Alcanzaste el umbral…" incluso con 0 swipes (**C15**) | Son gamificación de plantilla con un tono de videojuego que no tiene nada que ver con un concierto |
| T17 | **Clon visual de Tinder** | Sellos "LIKE"/"NOPE" rotados ±15° con borde de 4 px (`SwipeCard.tsx:321-327`, estilos `:390-430`) | Copia literal del patrón más reconocible del género. Nada de eso viene de la escena musical |
| T18 | **Datos de prueba sin tratamiento** | "Encorely User" (`ProfileScreen.tsx:647`), "Match 3d83", "Match 2222", "qa1-flow1" (`diseno-14`, `ui1-pantalla-conexiones`) | No hay un fallback con nombre ni avatar de iniciales, así que la UI queda con aspecto de seed |
| T19 | **Valores sueltos en vez de una escala** | 20 radios distintos (2–50, 99, 999) y unos 40 valores de padding/margin (de 1 a 120). 17 tamaños de fuente distintos (10, 11, 12, 13, 14, 15, 16, 17…) | Cada pantalla se generó por separado, sin escala (**I17**) |

---

## 3. Auditoría por dimensión (móvil nativo primero)

| Dimensión | Estado | Evidencia | Fix |
|---|---|---|---|
| **Jerarquía visual** | 🔴 | Radar: el 🌍 de 60 px, el título con glow, un párrafo de felicitación, la píldora de cifra falsa y un CTA magenta compiten en el mismo bloque centrado (`diseno-12`). Radar-matches: 3 tarjetas por pantalla, cada una con 2 botones enormes (`diseno-14`), así que lo dominante es "Conectar", no la persona. Login: 3 "features" con emojis entre el logo y la acción (`diseno-13`) | Cada pantalla tiene un solo foco y la persona va antes que el botón. En radar-matches, la tarjeta es la persona (foto, nombre, 2–3 artistas en común, concierto) y la acción va en el gesto o en un botón compacto |
| **Tipografía** | 🔴 | Familias reales: **Golos Text** 600/700/900 y **Inter** 400/500/700, más la fuente del sistema en el login. 17 tamaños (mínimo 10 px en la tab bar y en la hora del chat). No hay escala ni `lineHeight` coherente. `Inter_500Medium` está hardcodeado 43 veces fuera del tema. Nada contempla `fontScale`/Dynamic Type | Un sistema de 3 familias con roles fijos (§5.3). Escala de 8 pasos con un mínimo de 12 pt. Probar con fontScale 1.0, 1.35 y 2.0. `maxFontSizeMultiplier` solo en display |
| **Color y contraste** | 🔴 | Ratios calculados: blanco sobre `#F366FF` = **2.57** (todos los CTA primarios y la burbuja propia). Blanco sobre verde Spotify = **2.59**. Hora en burbuja propia (`rgba(255,255,255,.4)` sobre magenta) = **1.43**. Placeholder del chat = **2.70**. Chevrons 0.3 = **2.71**. Disclaimer del login `#4B4B6A`/`#0A0A0F` = **2.36**. Placeholders de settings = **2.06**. "Próximos pasos" 0.4 = **3.82**. Violeta `#A855F7` como texto = **4.49**. Pasan: magenta como texto sobre `#181818` = 6.90 y textDim 0.6 = 7.01. Landing: casi todo pasa (fg-subtle 7.79, CTA con ink 7.84). Lo que falla ahí es el logo Ticketmaster `#3A7DFF` (5.34, pero de 20 px de ancho) | Paleta nueva verificada (§5.2): texto oscuro sobre el acento y nunca blanco. Opacidades blancas solo a partir de 0.6. El botón de Spotify en negro sobre verde (8.12), como pide su guía de marca |
| **Espaciado / grid / ritmo** | 🟡 | Hay un ritmo base de 12/16/24 pero se rompe: unos 40 valores, `paddingBottom:40` inline, tarjetas a 99/999 de radio mezcladas con 12/20/24/32. La tab bar flotante (`position:'absolute'`, `_layout.tsx:114`) tapa contenido: en settings se superpone a "Visible en Radar Social" (`diseno-14`) | Escala de 4 pt con 7 pasos, gutter de 20 y 3 radios. Tab bar nativa no flotante dentro del safe area, o, si queda flotante, `contentInset` = altura + 16 en todas las listas |
| **Iconografía** | 🔴 | Ionicons + emojis + Unicode en la app. Lucide en la landing. Iconos de 18–24 px sin hitSlop (back del chat `ChatScreen.tsx:153`). Solo hay `hitSlop` en 1 archivo | Una sola librería vectorial (§5.5). Wrapper `<Icon>` con tamaños 20/24 y `hitSlop` automático hasta 44 pt |
| **Componentes y estados** | 🔴 | **Loading:** spinner infinito "Preparando tracks…" sin salida para cuentas de email (`diseno-12`), y "Calculando tu ADN musical…" que nunca termina (perfil, `diseno-14`). **Error:** se muestra crudo "API 401: Unauthorized" (`diseno-12`). **Empty:** "Aún no hay mensajes. ¡Saluda primero! 👋" en gris al 0.4. **Sistema:** los "¡Match creado!" del chat salen como burbujas del otro usuario, duplicados (`diseno-15`, `ui1-pantalla-chat`). No hay skeletons. Toggle magenta con perilla verde azulada (`diseno-14`) | Primitivas con los 5 estados (default, pressed, disabled, loading, error). `EmptyState` y `ErrorState` con una acción concreta. Mensajes de sistema como fila centrada, no como burbuja. Skeletons con la forma real del contenido |
| **Motion** | 🔴 | Hay bucles infinitos sin control: `PulsingDot` (`LoginScreen.tsx:45`), el halo del perfil (`ProfileScreen.tsx:373`) y las barras del SoundWave. **0 usos de `useReducedMotion`** en la app (**I16**). Haptics solo en el swipe (bien) y en la meta de swipes. Landing: maneja `prefers-reduced-motion` con un "kill" global de 0.001 ms (`index.css:133`), que según impeccable apaga también feedback útil, y tiene 109 animaciones activas | Un solo momento de autor (§5.6), springs solo para gestos y `useReducedMotion()` + `ReduceMotion.System` de Reanimated en todo. Sin bucles salvo el indicador de audio cuando de verdad suena |
| **Copy / voz** | 🔴 | Voseo y tuteo mezclados (T12), inglés de feature (T11), jerga técnica (T9), errores técnicos crudos y signos de exclamación por todas partes | Guía de voz (§5.7): tuteo colombiano, frases cortas, sin anglicismos de producto y errores que dicen qué pasó y qué hacer |
| **Coherencia app ↔ login ↔ landing** | 🔴 | Hay **tres marcas**. Login: `loginTheme.ts` (fondo `#0A0A0F`, primario rojo `#EF4444`, violeta `#7C3AED`, fuente del sistema, glow rojo alrededor del 🎵). App: `designTokens.ts` (`#181818`, magenta). Landing: `ink-950 #07060C`, escala violeta→magenta y logo con bisel. El splash usa `#0A0A0F` (`app.json`) y la app `#181818`, lo que produce un flash al entrar. Las reglas también chocan: la landing promete radar a los 25 swipes, la app exige 100 (`config/onboarding.ts`) y RadarMatches pide "Completá 25" | Un solo token source compartido (§6) y un mismo número de umbral en producto y marketing |
| **Responsive (móvil)** | 🟡 | `Dimensions.get('window')` se lee en el módulo (`SwipeCard.tsx:23`, `SwipeStack.tsx:6`), así que no reacciona a cambios de tamaño, split-screen ni foldables. El CTA "Ver Personas Compatibles" se parte en dos líneas alineado a la izquierda dentro de la píldora (`diseno-12`). El botón de enviar mide 40×40 | `useWindowDimensions()`, botones con `minHeight:48` y texto centrado que se ajusta, y targets de 44 pt o más |
| **Accesibilidad** | 🔴 | Solo 4 `accessibilityLabel` y 4 `accessibilityRole` frente a unos 47 táctiles. El swipe solo funciona con gesto (2.5.7). Tab labels de 10 px. Los contrastes de arriba. `userInterfaceStyle: "dark"` fijo (ignora el modo del sistema). Detalle completo en **I16** | Labels y roles en las primitivas, botones ✕/♥ y `accessibilityActions` en la tarjeta, mínimo de 12 pt, y probar con VoiceOver/TalkBack y fontScale 2.0 |

### 3.1 Solo web (prioridad baja, el producto es nativo)
- En desktop no hay `maxWidth`: el login se estira a 1568 px y los botones de radar-matches miden unos 750 px (`ui1-pantalla-login`, `ui1-pantalla-radar-matches`).
- `Alert.alert` no hace nada en web (`LoginScreen.tsx:25` y la validación de `OnboardingStep5Screen.tsx:145`). En nativo sí funciona, pero conviene reemplazarlo igual por un Toast propio, por coherencia de marca.
- El foco de los inputs es el anillo blanco del navegador, no uno de marca.

---

## 4. Pantalla por pantalla

### 4.1 App (canvas 390×844)

| Pantalla | Problema principal | Fix concreto |
|---|---|---|
| **Login** (`diseno-13`, `ui1-pantalla-login`) | Es otra marca (rojo/violeta, fuente del sistema, glow rojo sobre un 🎵), tiene jerga matemática y 3 rutas de entrada en vertical | Usa el mismo tema que la app. Arriba, el logotipo y una frase ("Encuentra con quién ir al próximo concierto"). En la zona del pulgar, el botón Spotify (negro sobre verde, con el logo SVG oficial) y debajo "Entrar con correo" como botón secundario que abre el formulario. Hay que quitar las 3 features |
| **Onboarding (6 pasos)** (`diseno-13`, `ui1-pantalla-onboarding-step1`) | Usa voseo, círculo con 🎵, eyebrow "ENCORELY", chips decorativos ("Swipe real") y 6 pasos para algo que cabe en 3. En el paso 3 hay 3 métodos de registro con "cuando estén listos" (promete algo que no existe) | Reducir a 3 pasos: 1) qué es, en una sola pantalla con una foto real de público; 2) conectar Spotify o elegir 3 artistas; 3) primera ronda de canciones. Quitar los métodos que no existen. Indicador de progreso tipo "talón numerado" (1/3) |
| **Discover / swipe** (`ui1-pantalla-discover`, `SwipeCard.tsx`) | Con cuenta de email se queda cargando sin fin. La tarjeta es un clon de Tinder (LIKE/NOPE), tiene glass sobre la portada, el artista en magenta con text-shadow y el swipe solo funciona con gesto | Si no hay feed: un `EmptyState` que explique y ofrezca "Conectar Spotify" o "Elegir artistas". La tarjeta es una **portada a sangre** con título y artista sobre un degradado de la propia portada (no morado). Debajo, una fila fija con ✕ / ▶ / ♥ de 56 pt en la zona del pulgar. Sellos "PASO"/"ME GUSTA" sin rotación. Haptic `selection` al cruzar el umbral |
| **Radar** (`diseno-12`, `ui1-pantalla-radar`) | Felicitación falsa, el 🌍, "247" inventado, y 2 de los "Próximos pasos" bloqueados con candado | Hacer el radar **por evento**: lista de próximos conciertos reales (cartel con fecha, venue y ciudad) y, en cada uno, cuántas personas compatibles van, con dato real o sin cifra. Quitar los pasos bloqueados en lugar de exhibir candados |
| **Radar-matches** (`diseno-14`) | Todos son "100 % · Match Certificado 🌟". Cada tarjeta tiene 2 botones gigantes y "Pasar" no hace nada (**C15**). La etiqueta "Moshpit" queda sin contexto | Una **boleta por persona**: foto, nombre, edad, "Van a: Andrés Cepeda · 22 nov · Movistar Arena", 3 artistas en común con miniaturas y el porcentaje solo si es real y no es 100 en todos. La acción va con gesto horizontal o un único botón "Conectar". Fuera el sello "Certificado" |
| **Conexiones** (`ui1-pantalla-conexiones`, `diseno-14`) | Nombres tipo "Match 3d83", ⭐ en todos, "¡Nuevo Match! Toca para saludar 👋" repetido 10 veces en magenta | Fila de lista nativa: avatar con iniciales si no hay foto, nombre real y, como subtítulo, el **motivo del match** ("Los dos escuchan Bomba Estéreo"). Separar "Nuevos" (carrusel de avatares) de "Chats" (lista), como hacen las apps de mensajería nativas |
| **Chat** (`diseno-15`, `ui1-pantalla-chat`, `ui2-6/7`) | Los mensajes de sistema salen como burbujas ajenas y duplicadas. La burbuja propia es magenta con texto blanco (2.57) y hora a 1.43. El composer tiene un placeholder a 2.70 y botón de 40 pt. Back sin label. No hay ninguna señal de cifrado | Ver §5.8 (E2EE). Burbuja propia en acento con tinta oscura (6.60) y la ajena en `surface-2` con texto crema (13.5). Mensajes de sistema como **fila perforada centrada**. Composer con `minHeight:48`, send de 44 pt y label "Enviar" |
| **Perfil** (`diseno-14`, `ui1-pantalla-perfil`) | Avatar genérico con anillo magenta, "Encorely User", badge "✦ Vibe Explorer" sin significado y dos spinners eternos | El perfil como **"mi pase"**: foto, nombre, ciudad, "Mis artistas" (top 5 reales), "Próximos conciertos" y, en lugar del radar chart, una barra de géneros simple. Si no hay datos: un `EmptyState` con acción, nunca un spinner infinito |
| **Settings** (`ui1-pantalla-settings`, `diseno-14`) | El formulario de perfil está dentro de una tarjeta. Placeholders a 2.06. El toggle es magenta con perilla verde azulada. La tab bar tapa contenido. Los toggles de privacidad son placebo (**C15**) | Usar el patrón nativo de lista agrupada (`SectionList`, filas de 48 pt o más) y `Switch` nativo con `trackColor` del tema. Editar perfil en una pantalla aparte. Quitar los toggles que no persisten en backend |

### 4.2 Landing (web, se audita como web)

| Sección | Problema principal | Fix concreto |
|---|---|---|
| **Navbar** | Píldora de vidrio flotante y el logo con bisel y waveform (`logo-color-bisel.webp`) | Barra plana, logotipo tipográfico y un solo CTA |
| **Hero** (`diseno-1`, `diseno-11`) | H1 con texto en gradiente animado, mockup 3D con tilt y *bob*, logo Ticketmaster ilegible, eyebrow. **En móvil el input de email mide 22 px de alto** (`WaitlistForm.tsx:85`: `flex-1` dentro de `flex-col` colapsa la altura de `h-13`) | H1 sólido con peso de cartel. Una foto o composición real de público colombiano, o la "boleta" de Encorely como objeto central. Input de 48 px (`sm:flex-1` solo en fila). Quitar la fila "Se conecta con" hasta que la integración exista |
| **Cómo funciona** (`diseno-2`) | 4 renders de iPhone, uno con mano, que no coinciden con la UI real. Badges numerados con glow | Capturas reales de la app nueva dentro de un marco simple, o 3 pasos tipográficos tipo **setlist** (1. Conecta, 2. Califica, 3. Ve con alguien) |
| **Features** (`diseno-3`) | 3 tarjetas idénticas con badge en inglés, waveform decorativo y "Calidad garantizada". Promete "sincronización directa con Ticketmaster" sin que exista | Fusionar con "Cómo funciona". Quitar lo que no existe (Ticketmaster) o etiquetarlo como "Próximamente" |
| **Demo Sound-Swipe** (`diseno-4`) | Tarjeta con gradiente violeta, track inventado y botón ♥ con glow | Usar canciones reales de la escena (artistas colombianos con preview) y el mismo componente visual que la app nueva |
| **Music DNA** (`diseno-5`) | El mockup del radar chart "Energy/Valence/Tempo" se apoya en audio-features deprecados (**I20**) | Mostrar "artistas en común" en lugar de un radar de 4 ejes que el producto ya no puede calcular |
| **Integraciones** (`diseno-8`) | Tarjetas glass. Ticketmaster no está integrado | Eliminar la sección o reducirla a una línea honesta |
| **Social proof** (`diseno-6`) | Marquee de géneros más "4D / 100% / $0" con texto en gradiente: la plantilla de hero-metric | Eliminar. Cuando haya prueba real, poner citas de personas reales o la cifra de lista de espera real |
| **Valores** (`diseno-7`) | Tres sustantivos abstractos centrados con iconos | Eliminar. La voz del producto ya cuenta los valores |
| **Waitlist / FAQ** | Eyebrows, y la FAQ dice "Más adelante habrá funciones premium" | Bien resuelto en lo funcional (aria-live, honeypot). Quitar los eyebrows y escribir las preguntas en la voz nueva |
| **Footer** (`diseno-9`) | Tagline en inglés, "© 2025", redes con `href="#"` y el icono `Music` haciendo de TikTok | Tagline en español, año dinámico y ocultar redes inexistentes |

---

## 5. Dirección visual propuesta

### 5.1 Concepto

**"La boleta compartida": Encorely se ve como lo que guardas después de un concierto (la boleta, la manilla, el setlist pegado en la tarima), y cada match es una boleta para dos.**

Lo visual sale de objetos reales de la escena: talones perforados, números de serie, sellos de entrada, manillas Tyvek de color plano y la tipografía condensada de los carteles de festival. No hay neón, ni vidrio, ni auroras.

#### Alternativas

| | A · **Boleta y manilla** (RECOMENDADA) | B · **Afiche de poste** |
|---|---|---|
| Idea | Superficies cálidas de "backstage" casi negro y una tarjeta de papel crema con muesca de talón para los objetos importantes (match, evento, verificación de chat). Un único acento color manilla naranja | El afiche tipográfico impreso con tipos de madera y tintas fluorescentes que se pega en postes y muros para anunciar conciertos en Colombia: fondos de papel periódico, rosa y amarillo flúor, letras condensadas enormes |
| Pros | Se adueña de un objeto que todo asistente conoce y guarda. Da una forma propia (la muesca) que se vuelve firma de marca. Funciona en oscuro (venue) y claro (papel). Escala bien a componentes nativos chicos. La verificación E2EE encaja de forma natural como "número de serie" | Es inconfundiblemente colombiana y muy memorable en la landing y en redes |
| Contras | Exige disciplina: la muesca solo en 2–3 componentes, o se vuelve un disfraz | Se asocia a verbenas y música tropical más que a indie, rock o electrónica de Bogotá. Los flúor fallan contraste en textos pequeños y la estética es difícil de sostener en 30 pantallas de app sin caer en parodia. Funciona mejor como **campaña** (posts, afiches del lanzamiento) que como sistema de UI |
| Veredicto | **Elegida para app y landing** | Reservarla como lenguaje de campaña: afiches por concierto en redes que conviven con A |

Descarté una tercera opción, "cabina de DJ" (LEDs, vúmetros, oscuro técnico), porque es justamente donde ya está el producto hoy.

### 5.2 Paleta (ratios WCAG verificados)

**Tema "Venue" (oscuro, por defecto de noche y sigue el modo del sistema):**

| Token | Hex | Uso | Ratio verificado |
|---|---|---|---|
| `bg` | `#121110` | fondo (negro cálido, no morado) | — |
| `surface` | `#1C1A18` | tarjetas, filas | — |
| `surface2` | `#262320` | burbuja ajena, inputs | — |
| `border` | `#3A3531` | divisores decorativos | 1.56 (decorativo, no requiere) |
| `borderInput` | `#736B61` | borde de campos | **3.60** / bg · 3.31 / surface (≥3:1 de componente UI) |
| `text` | `#F4EEE4` | texto principal (crema) | **16.34** / bg · 13.54 / surface2 |
| `textMuted` | `#B5ACA0` | secundario | **8.42** / bg · 7.74 / surface |
| `textSubtle` | `#9A9286` | terciario, placeholders | **6.13** / bg · 5.64 / surface · 5.08 / surface2 |
| `accent` (manilla) | `#FF6A2B` | CTA, burbuja propia, tab activa | texto `#121110` encima: **6.60**. Como texto sobre bg: **6.60**, sobre surface2: 5.47. **Blanco encima: 2.86, prohibido** |
| `accentPressed` | `#FF8A55` | estado pressed | `#121110` encima: 8.10 |
| `ticket` (papel) | `#F2EADB` | tarjeta-boleta | tinta `#1A1714`: **14.93** · muted `#6B6257`: 5.01 · acento oscuro `#A8360A`: 5.49 |
| `verified` (sello) | `#7FD99A` | E2EE verificado, éxito | sobre bg: **11.04** · tinta encima: 10.45 |
| `danger` | `#FF7A7A` | errores | sobre bg: **7.47** |

**Tema "Papel" (claro, para el día y el modo claro del sistema):**

| Token | Hex | Ratio |
|---|---|---|
| `bg` | `#F6F1E8` | — |
| `text` | `#1A1714` | **15.86** |
| `textMuted` | `#5E564C` | **6.41** |
| `textSubtle` | `#756C61` | **4.58** |
| `accentText` | `#B83A0A` | **5.12** (el acento como texto en claro se oscurece) |
| `accent` (relleno) | `#FF6A2B` con texto `#1A1714` | **6.25** |
| `verified` | `#1F7A3E` | **4.77** |
| `danger` | `#B42318` | **5.84** |
| `borderInput` | `#8C8377` | **3.32** |

Reglas:
- El acento ocupa como máximo ~10 % de la pantalla.
- Spotify verde solo en el botón y el logo oficial, nunca como color de marca.
- `app.json` → `"userInterfaceStyle": "automatic"` + `useColorScheme()`.

### 5.3 Sistema tipográfico (Google Fonts, paquetes `@expo-google-fonts/*`)

| Rol | Familia | Pesos | Por qué |
|---|---|---|---|
| Display / cartel | **Big Shoulders Display** | 700, 800 | Es condensada, de rótulo urbano y cartel de festival: nombres de eventos y artistas largos caben en 390 pt sin cortar. Solo de 22 pt para arriba |
| UI / cuerpo | **Archivo** | 400, 500, 600, 700 | Grotesca sólida con buena legibilidad en tamaños chicos y soporte latino completo. Tiene más carácter que Inter sin ser decorativa |
| Datos de boleta | **IBM Plex Mono** | 500 | Solo para datos medibles: hora, puerta, fecha, % real y el **código de seguridad E2EE**. No se usa como "estética tech" |

Escala nativa (pt, line-height), con `allowFontScaling` activo:

| Token | Tamaño/LH | Familia | `maxFontSizeMultiplier` |
|---|---|---|---|
| `display` | 40/40 | Big Shoulders 800, mayúsculas, tracking 0 | 1.3 |
| `title1` | 28/32 | Big Shoulders 700 | 1.4 |
| `title2` | 22/28 | Archivo 700 | 1.6 |
| `headline` | 17/22 | Archivo 600 | — |
| `body` | 16/24 | Archivo 400 | — |
| `callout` | 15/20 | Archivo 500 | — |
| `footnote` | 13/18 | Archivo 400 | — |
| `caption` | 12/16 | Archivo 500 (mínimo absoluto) | — |
| `data` | 13/18 | Plex Mono 500, `fontVariant:['tabular-nums']` | — |

En la landing van las mismas familias con `clamp()`: display de `clamp(3rem, 9vw, 7rem)` y cuerpo de 18 px con medida de 65ch.

### 5.4 Grid y spacing
- Base de 4 pt. Tokens `space`: 4, 8, 12, 16, 24, 32, 48. Gutter de pantalla: 20. Separación entre secciones: 32. Dentro de un grupo: 8–12.
- Radios: 3 valores. `sm 8` (inputs y chips), `md 16` (tarjetas), `full` (avatares y píldoras). La **muesca de talón** (semicírculos de 10 pt a los lados más una línea perforada) se dibuja con `react-native-svg` y solo aparece en: tarjeta de match, tarjeta de evento y la pantalla de verificación del chat.
- Zona del pulgar: la acción primaria siempre en el tercio inferior. Targets de 44 pt o más (iOS) y 48 dp (Android), con `hitSlop` en el wrapper.
- Safe areas con `react-native-safe-area-context` en el `Screen`. Tab bar **nativa** (sin flotar ni glow) con 4 ítems en español: *Descubrir · Conciertos · Chats · Tú*.

### 5.5 Iconografía
- **Phosphor** (`phosphor-react-native` en la app y `@phosphor-icons/react` en la landing): una familia, pesos `regular` para la UI y `fill` para el estado activo de la tab. Tiene glifos del dominio: `Ticket`, `MicrophoneStage`, `Waveform`, `VinylRecord`, `MapPin`, `LockKey`, `ShieldCheck`, `QrCode`.
- Lucide sería la alternativa (ya está en la landing), pero es el set por defecto de shadcn y del "look IA", y tiene menos glifos de música.
- Logos de terceros con SVG oficial y sus reglas de marca (Spotify: logo y texto en negro o blanco sobre verde). Cero emojis en la UI. Los emojis solo pueden aparecer en el contenido que escribe el usuario.

### 5.6 Motion (Reanimated 4)
- **Gestos con spring, interfaz con timing.** El swipe va con `withSpring` (damping ≈ 18, stiffness ≈ 180) e inercia real. Las transiciones de UI son de 160–240 ms con ease-out `Easing.bezier(0.2, 0, 0, 1)`. Sin ease-in en entradas.
- **Un solo momento de autor:** al hacer match, la boleta se **rasga por la línea perforada** y el talón queda en "Conexiones" (≈ 450 ms) + `Haptics.notificationAsync(Success)`. Nada más compite con eso.
- Haptics: `selectionAsync` en chips y toggles, `impactAsync(Light)` al cruzar el umbral del swipe y `Success` en el match.
- Sin bucles infinitos. La única excepción son las barras de audio mientras suena el preview.
- **Reduced motion:** `useReducedMotion()` en las primitivas y `reduceMotion: ReduceMotion.System` en las animaciones. La alternativa es un crossfade de 120 ms que conserva el cambio de estado: el rasgado se vuelve un fade del sello "Match" y el swipe se resuelve con los botones.
- Landing: sustituir el kill global de 0.001 ms por variantes explícitas.

### 5.7 Voz y copy
Tuteo colombiano, cercano y concreto. Habla de planes, conciertos y gente, no de vectores. Sin "¡!" por defecto, sin anglicismos de feature. Los errores dicen qué pasó y qué hacer. Pequeño glosario: *parche* (grupo para ir), *plan*, *toque* (concierto pequeño), *boleta*, siempre con mesura.

| Dónde | Antes | Después |
|---|---|---|
| `LoginScreen.tsx:129` | "🎯 Similitud coseno >70% = match garantizado" | "Te mostramos quién escucha lo mismo que tú y va al mismo concierto." |
| `OnboardingStep1Screen.tsx:235` | "Conectá tu música, descubrí tu vibe y encontrá personas que escuchan como vos." | "Conecta tu música y encuentra con quién ir al próximo concierto." |
| `RadarScreen.tsx:147-148` | "¡Felicidades! Alcanzaste el umbral de los 100 swipes. Tu Vibe ha sido calculado y el Radar Social está listo para futuras conexiones." | "Ya tenemos tu perfil musical. Elige un concierto y mira quién más va." |
| `MatchesScreen.tsx:76` | "¡Nuevo Match! Toca para saludar 👋" | "A los dos les gusta Bomba Estéreo. Escríbele." (con el artista real en común) |
| `RadarMatchesScreen.tsx:54` | "Completá 25 swipes para desbloquear el radar." | "Califica 12 canciones más para ver gente compatible." (con el conteo real que falta y un solo umbral) |
| Error 401 (`diseno-12`) | "API 401: Unauthorized" | "Tu sesión se cerró. Vuelve a entrar para seguir." + botón "Entrar" |
| `SwipeCard.tsx:321,324` | "LIKE" / "NOPE" | "ME GUSTA" / "PASO" |

### 5.8 Chat 1:1 con cifrado de extremo a extremo: cómo comunicarlo sin parecer genérico
Condición previa (**C15**): no se muestra ninguna señal de cifrado hasta que el E2EE esté implementado de verdad, con un protocolo auditado como Signal/libsignal, llaves en `expo-secure-store` y el servidor sin acceso al texto. Un candado falso es peor que ninguno.

1. **Indicador discreto y una sola vez.** Bajo el nombre en el header va un `footnote` con `LockKey` de Phosphor: "Cifrado de extremo a extremo · Sin verificar". No es un banner en cada mensaje ni un escudo enorme.
2. **Verificación como ritual de concierto.** Al tocar el indicador se abre una **boleta** (tarjeta papel con muesca) que tiene:
   - el **código de seguridad** de 60 dígitos en 12 bloques de 5, en IBM Plex Mono, maquetado como el número de serie de una entrada;
   - un **QR** con el texto "Cuando se vean en el concierto, escaneen sus códigos".
   El caso de uso real es conocerse en persona, así que la verificación se hace ahí mismo.
3. **Sello de verificado.** Tras escanear, la boleta recibe un sello en `verified #7FD99A` (11.04 sobre bg) con forma de sello de entrada: "Verificado en persona · Movistar Arena · 22 nov". El header pasa a "Verificado" con `ShieldCheck` relleno. Haptic Success.
4. **Cambio de llave.** Aparece una fila de sistema centrada y perforada (no una burbuja): "El código de seguridad de Ana cambió. Verifícalo antes de compartir tu ubicación." Lleva color `danger` solo en el icono, no en todo el bloque.
5. **Mensajes de sistema** ("Hicieron match por Bomba Estéreo") como divisor perforado centrado, en `textSubtle`, nunca como mensaje de la otra persona.
6. Burbujas: la propia en `accent` con tinta `#121110` (6.60) y la ajena en `surface2` con `text` (13.54). La hora en `caption` 12 pt, en `textMuted` o en tinta al 70 % (≥4.5).

### 5.9 ¿3D? No.
- **App:** no. Gasta batería y GPU en un contexto (concierto, poca señal, batería baja) donde ambas escasean, y no hay ningún objeto del dominio que mejore al girarlo.
- **Landing:** no. Three.js agrega unos 150 KB gzip más los assets, empeora el LCP en las redes móviles colombianas y competiría con el mensaje. El tilt 3D de mockups que existe hoy (`Hero.tsx`) ya es parte del problema (T15).
- Si se quiere un gesto memorable, basta con la boleta rasgándose en 2D (SVG + `clip-path`), que comunica el concepto mejor que una escena WebGL. Reconsiderarlo solo para una campaña puntual, por ejemplo un cartel interactivo de un festival, fuera del flujo principal.

---

## 6. Design system mínimo

### 6.1 Tokens (fuente única)
`packages/tokens/tokens.ts`, consumido por la app con StyleSheet y exportado a CSS variables para la landing con un script de build:

```ts
// tokens.ts
const palette = {
  ink900: '#121110', ink800: '#1C1A18', ink700: '#262320', ink600: '#3A3531', ink500: '#736B61',
  cream100: '#F4EEE4', cream300: '#B5ACA0', cream400: '#9A9286',
  paper: '#F2EADB', paperLight: '#F6F1E8', inkDark: '#1A1714',
  band500: '#FF6A2B', band400: '#FF8A55', band700: '#B83A0A',
  stamp: '#7FD99A', stampDark: '#1F7A3E', alert: '#FF7A7A', alertDark: '#B42318',
} as const

export const themes = {
  venue: {
    bg: palette.ink900, surface: palette.ink800, surface2: palette.ink700,
    border: palette.ink600, borderInput: palette.ink500,
    text: palette.cream100, textMuted: palette.cream300, textSubtle: palette.cream400,
    accent: palette.band500, accentPressed: palette.band400, onAccent: palette.ink900,
    ticket: palette.paper, onTicket: palette.inkDark,
    verified: palette.stamp, danger: palette.alert,
  },
  papel: {
    bg: palette.paperLight, surface: '#FFFFFF', surface2: '#EDE5D7',
    border: '#D9CFBF', borderInput: '#8C8377',
    text: palette.inkDark, textMuted: '#5E564C', textSubtle: '#756C61',
    accent: palette.band500, accentPressed: palette.band400, onAccent: palette.inkDark,
    accentText: palette.band700,
    ticket: palette.paper, onTicket: palette.inkDark,
    verified: palette.stampDark, danger: palette.alertDark,
  },
} as const

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, '2xl': 32, '3xl': 48, gutter: 20 } as const
export const radius = { sm: 8, md: 16, full: 999 } as const
export const touch = { min: 44, comfortable: 48 } as const

export const font = {
  display: 'BigShouldersDisplay_800ExtraBold',
  title: 'BigShouldersDisplay_700Bold',
  body: 'Archivo_400Regular', bodyMedium: 'Archivo_500Medium',
  bodySemibold: 'Archivo_600SemiBold', bodyBold: 'Archivo_700Bold',
  data: 'IBMPlexMono_500Medium',
} as const

export const type = {
  display:  { fontFamily: font.display, fontSize: 40, lineHeight: 40, maxFontSizeMultiplier: 1.3 },
  title1:   { fontFamily: font.title, fontSize: 28, lineHeight: 32, maxFontSizeMultiplier: 1.4 },
  title2:   { fontFamily: font.bodyBold, fontSize: 22, lineHeight: 28 },
  headline: { fontFamily: font.bodySemibold, fontSize: 17, lineHeight: 22 },
  body:     { fontFamily: font.body, fontSize: 16, lineHeight: 24 },
  callout:  { fontFamily: font.bodyMedium, fontSize: 15, lineHeight: 20 },
  footnote: { fontFamily: font.body, fontSize: 13, lineHeight: 18 },
  caption:  { fontFamily: font.bodyMedium, fontSize: 12, lineHeight: 16 },
  data:     { fontFamily: font.data, fontSize: 13, lineHeight: 18, fontVariant: ['tabular-nums'] },
} as const

export const motion = {
  duration: { fast: 160, base: 220, slow: 320, signature: 450 },
  easeOut: [0.2, 0, 0, 1] as const,
  spring: { damping: 18, stiffness: 180 },
} as const

export type Theme = typeof themes.venue
```

`useTheme()` resuelve `venue` o `papel` con `useColorScheme()`, y cada pantalla crea sus estilos con `useMemo(() => makeStyles(theme), [theme])`.

### 6.2 Componentes base, en orden de construcción
1. `ThemeProvider` + `useTheme` + carga de fuentes y splash (mismo `bg` que el splash: se acaba el flash).
2. `Text` (variantes de `type`, `allowFontScaling`, color por token).
3. `Icon` (wrapper de Phosphor, tamaños 20/24, `hitSlop` hasta 44 y `accessibilityLabel` obligatorio si es solo icono).
4. `Button` (primary/secondary/ghost/destructive · md 48 / lg 56 · loading · disabled · haptic · `accessibilityRole="button"`).
5. `Screen` (safe area, header nativo, insets para la tab bar) y `ListRow` (48 pt o más, chevron, switch nativo).
6. `TextField` (label visible, helper, error con `danger`, placeholder `textSubtle`).
7. `Chip` / `ToggleChip`, `Avatar` (iniciales como fallback).
8. `Card` y **`TicketCard`** (muesca SVG y perforación: el componente firma).
9. `EmptyState`, `ErrorState`, `Skeleton`, `Toast` (sustituye `Alert.alert`).
10. Tab bar nativa (`@react-navigation/bottom-tabs` vía expo-router, sin blur).
11. Dominio: `SwipeCard` (+ botones accesibles), `MatchTicket`, `EventTicket`, `ChatBubble`, `SystemRow`, `E2EEBadge`, `SafetyNumberTicket`.

### 6.3 Qué borrar
- `src/shared/theme/loginTheme.ts` completo (es la segunda marca).
- De `designTokens.ts`: `glassLight`, `glassNeon`, `glassDark`, `shadows.neonMagenta` y `blur`. El archivo se reemplaza por `tokens.ts`.
- Los 10 `LinearGradient` de fondo y `DEFAULT_SCREEN_GRADIENT` (`ScreenShell.tsx:20`). Todos los `BlurView` decorativos (tab bar, tarjetas, swipe, chat). Todos los `text-shadow` con glow.
- Código muerto: `modules/home/screens/HomeScreen.tsx` (462 líneas, no lo importa nadie, tiene el "Night Vibes" hardcodeado) y `modules/auth/screens/OnboardingScreen.tsx` (443 líneas, la ruta `(auth)/onboarding.tsx` es `@deprecated` y redirige).
- Debug en UI: "⚙️ Reset Swipes (Debug)" (`SwipeScreen.tsx:278-282`, **I18**).
- **styled-components vs StyleSheet → quedarse con `StyleSheet` + tokens tipados.** Razones:
  - (a) es nativo y no parsea CSS en runtime en cada render;
  - (b) styled-components v6 con `@types/styled-components-react-native` v5 es el origen de 83 de los 85 errores de `tsc` (**I19**);
  - (c) Reanimated trabaja con objetos de estilo, no con template strings;
  - (d) hoy conviven 18 archivos con styled, 7 con StyleSheet y estilos inline, y unificar en la opción nativa es el camino de menor riesgo.

  Hay que desinstalar `styled-components`, `@types/styled-components-react-native` y `styled.d.ts`. Alternativa en una línea: `react-native-unistyles` v3 si más adelante hacen falta breakpoints o tablet. Hoy no.
- Landing: `GlobalBackground.tsx`, `.text-gradient`, `.cta-glow`, `.ring-gradient`, `.glass*`, las keyframes `aurora/float/drift/bob/gradient-pan`, los eyebrows de `SectionHeading`, y `SocialProof` y `Values` completos.

---

## 7. Prioridad

### Quick wins visuales (1 día, sin rediseñar)
1. Quitar todos los emojis de la UI y poner Ionicons (ya instalado) mientras llega Phosphor.
2. Texto oscuro `#181818` sobre magenta en CTA y burbuja propia (2.57 → 6.90), y negro sobre el verde Spotify (2.59 → 8.12).
3. Unificar el login con el tema de la app: borrar `loginTheme.ts` y usar Golos/Inter mientras tanto. Igualar el `bg` del splash.
4. Pasar todo el copy a tuteo y traducir "Discover/LIKE/NOPE/Music DNA/Vibe Explorer".
5. Quitar las cifras y sellos falsos: "247", "Match Certificado", "Night Vibes", la felicitación con 0 swipes (**C15**). Un solo umbral de swipes en app y landing.
6. Mensajes de sistema del chat como fila centrada. Error 401 humanizado y `EmptyState` en Discover para cuentas sin Spotify.
7. Subir los mínimos: texto 12 pt, opacidades 0.6 o más, targets de 44 pt (send, back, chevrons), tab labels de 12.
8. Quitar los text-shadow con glow y el `shadowColor` magenta de la tab bar.
9. Landing: arreglar el input de 22 px en móvil (`WaitlistForm.tsx:85`), quitar el texto en gradiente, `GlobalBackground`, `SocialProof`/`Values`, los eyebrows, Ticketmaster y las redes con `#`.
10. `"userInterfaceStyle": "automatic"`.

### Rediseño por fases
| Fase | Alcance | Duración aprox. |
|---|---|---|
| **F0 · Decisión** | Validar el concepto "boleta compartida" con 5 usuarios reales (18–30, Bogotá/Medellín). Escribir PRODUCT.md (público, voz, qué es real y qué no) y fijar el umbral de swipes | 2 días |
| **F1 · Fundaciones** | `tokens.ts` + ThemeProvider (venue/papel) + fuentes + primitivas 1–9 de §6.2. Migrar de styled-components a StyleSheet | 1 semana |
| **F2 · Núcleo** | Login + onboarding en 3 pasos + Discover (portada a sangre, botones accesibles, reduced motion, haptics) | 1 semana |
| **F3 · Social** | Conciertos/radar por evento, `MatchTicket`, Conexiones, Chat con `E2EEBadge` y `SafetyNumberTicket` (cuando el protocolo exista), el momento de autor "rasgar boleta" | 1–2 semanas |
| **F4 · Perfil y ajustes** | "Mi pase", lista agrupada nativa y privacidad con persistencia real | 3–4 días |
| **F5 · Landing** | Reconstruir con la misma identidad: hero tipográfico de cartel, 3 pasos tipo setlist, capturas reales y waitlist. Campaña en dirección B para redes | 1 semana |
| **F6 · Verificación** | VoiceOver/TalkBack, fontScale 2.0, contraste en ambos temas, dispositivo Android de gama media (blur y rendimiento), luz de concierto (brillo bajo) | 3 días |
