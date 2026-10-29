# Auditoría móvil nativa (iOS/Android con Expo): Encorely

**Fecha:** 2026-10-13 · **Alcance:** `encorely-front` (rama local `main` @ `ff11541` + 5 archivos modificados sin commit), rama remota `codex/actualizar-frontend` (`48f99c9`, Expo 57) y las partes de `encorly_back` que afectan al móvil (auth, push, hubs). **Método:** solo lectura. Leí el código, ejecuté `expo config --type public` y `expo install --check` (sin instalar nada) y leí la rama remota con `gh api`. Contrasté con la documentación vigente de Expo, Spotify, Google Play y App Store. **No ejecuté** `expo-doctor` porque `npx` pedía descargarlo. No hay emulador ni build nativo: nada de esto se probó en un dispositivo.
**Etiquetas:** *Verificado* = lo comprobé en código, configuración o salida de herramienta. *Sospecha fundada* = el código lo implica, pero hay que confirmarlo en un dispositivo.
**No repito** lo de `01-auditoria-externa.md` ni lo de `02-enjambre-qa.md`. Solo los cito cuando ser app móvil les cambia la prioridad (§4).

---

## 1. Veredicto

La app **nunca se ha compilado como app nativa**, y en su base actual (SDK 54 con `expo-av`) **no puede ir a ninguna tienda** sin pasar antes por cuatro arreglos:
- **Pipeline:** no hay `eas.json` en `main`, no hay *dev client*, la URL del API apunta a `localhost` y el icono y el splash son los de la plantilla de Expo.
- **Sesión:** el JWT dura 30 min y no tiene *refresh*. Además, desde marzo de 2026 Spotify en modo desarrollo admite **5 usuarios** y no devuelve el email.
- **Cumplimiento:** faltan eliminación de cuenta, reportar/bloquear, política de privacidad y edad mínima.
- **Notificaciones:** no hay push de punta a punta.

La capa de UI nativa es aprovechable: usa `react-native-safe-area-context`, `GestureHandlerRootView`, Reanimated 4, SecureStore y haptics. Lo que necesita es reestructurar la navegación y corregir el teclado, las *insets* y la accesibilidad.
**Base recomendada:** partir de la rama **Expo 57**. Antes hay que rebasar encima los cambios locales sin commit y, antes de hacer merge, generar un primer build nativo en EAS.

## 2. Scorecard

| Dimensión | Estado | Comentario |
|---|---|---|
| Configuración nativa (`app.json`/EAS) | 🔴 | Sin `eas.json` ni perfiles; icono y splash de plantilla; sin control de `buildNumber`/`versionCode`; el API cae a `localhost` |
| Dependencias / SDK | 🔴 | SDK 54 con `expo-av` (eliminado en SDK 55), que inyecta un permiso de micrófono que no se usa; 10 paquetes desalineados; deps muertas |
| Auth nativa | 🔴 | PKCE bien hecho con `openAuthSessionAsync`, pero el JWT del back no se renueva; Spotify en modo desarrollo limita a 5 usuarios y no da email; no hay Google nativo |
| Push notifications | 🔴 | No existe: ni `expo-notifications`, ni endpoint de token, ni envío (el código está comentado); el back usa *topic* = userId |
| Tiempo real | 🟡 | El front no usa SignalR (para móvil es lo correcto), pero tampoco hay push ni refresco al volver a la app |
| UX nativa | 🟡 | Safe areas correctas en lo básico; chat y ajustes son *tabs ocultas* (sin swipe-back); tab bar ignora las *insets*; teclado del chat mal compensado |
| Accesibilidad nativa | 🔴 | 8 props de accesibilidad para 75 táctiles; el swipe solo funciona con gesto; botones de icono sin etiqueta; no considera Dynamic Type |
| Offline / red | 🔴 | Sin timeout, reintento ni NetInfo; el *cold start* de Render deja spinners colgados |
| Distribución y cumplimiento | 🔴 | Faltan política de privacidad, eliminación de cuenta, reportar/bloquear y edad mínima; hace falta cuenta demo para revisión |
| Testing nativo | 🔴 | Cero builds nativos. La rama 57 solo validó la exportación del bundle JS/Hermes |

---

## 3. Hallazgos priorizados

### Críticos

**[C1] SDK 54 + `expo-av`: callejón tecnológico y permiso de micrófono fantasma** · *Verificado*
- **Evidencia:**
  - `package.json:21` (`expo-av ~16.0.8`).
  - `SwipeCard.tsx:15,198-238` usa `Audio.Sound`.
  - `node_modules/@expo/prebuild-config/build/plugins/withDefaultPlugins.js:227` aplica `expo-av` como *legacy plugin* automático aunque no esté en `plugins`.
  - `expo-av/plugin/build/withAV.js:5-14` añade `android.permission.RECORD_AUDIO`, `MODIFY_AUDIO_SETTINGS` y `NSMicrophoneUsageDescription` = "Allow $(PRODUCT_NAME) to access your microphone" (en inglés y genérico).
  - Expo eliminó `expo-av` en SDK 55.
  - Expo Go de las tiendas solo ejecuta el SDK más reciente (hoy 57): con SDK 54 ni siquiera hay Expo Go en un iPhone físico.
- **Impacto:**
  - El binario declara un micrófono que no usa. Google Play exige justificarlo en Data Safety y Apple puede preguntar o rechazar por 5.1.1.
  - Quedarse en 54 obliga a migrar dos veces.
  - La carga de audio tiene una carrera: si la tarjeta se desmonta antes de `createAsync`, el sonido queda huérfano (`SwipeCard.tsx:198-238`; en 01 era M8, en nativo se oye).
- **Fix:** adoptar la rama 57, que ya migró a `expo-audio` con el plugin explícito:
  ```json
  ["expo-audio", { "microphonePermission": false, "recordAudioAndroid": false, "enableBackgroundPlayback": false }]
  ```
  Si se mantuviera 54: `["expo-av", { "microphonePermission": false }]` y `android.blockedPermissions: ["android.permission.RECORD_AUDIO"]`.
- **Esfuerzo:** S (con la rama) / M (sin ella).

**[C2] No existe pipeline nativo: sin EAS, sin *dev client*, sin entornos** · *Verificado*
- **Evidencia:**
  - `main` no tiene `eas.json`.
  - `expo-dev-client` no está instalado (`node_modules/expo-dev-client` no existe).
  - `src/config/api.ts:19-33`: sin `EXPO_PUBLIC_API_URL`, en desarrollo cae a `http://localhost:5000`, que en un dispositivo es el propio teléfono y en el emulador Android no es el host.
  - No hay `.env`/`.env.example` en el front.
  - No hay `runtimeVersion`/`updates`.
  - La rama 57 trae `eas.json` solo con perfiles `validation-*` (sin `development`, `preview` ni `production`, sin `submit`, sin `env`).
- **Impacto:**
  - Nadie puede probar en un dispositivo hoy.
  - Las variables `EXPO_PUBLIC_*` se incrustan **al compilar**: si el build se hace sin la variable, el binario de producción apunta al fallback.
  - En dispositivo, `http://IP-LAN` lo bloquean ATS en iOS y la política de *cleartext* de Android en builds de release.
- **Fix:**
  ```json
  {
    "cli": { "version": ">= 16.0.0", "appVersionSource": "remote" },
    "build": {
      "development": { "developmentClient": true, "distribution": "internal",
        "env": { "EXPO_PUBLIC_API_URL": "https://<tunel-o-staging>" } },
      "preview": { "distribution": "internal", "channel": "preview",
        "android": { "buildType": "apk" },
        "env": { "EXPO_PUBLIC_API_URL": "https://encorly-back.onrender.com" } },
      "production": { "channel": "production", "autoIncrement": true,
        "env": { "EXPO_PUBLIC_API_URL": "https://api.<dominio>" } }
    },
    "submit": { "production": {} }
  }
  ```
  Además: `npx expo install expo-dev-client expo-updates`, `"runtimeVersion": { "policy": "appVersion" }` y quitar el fallback a `localhost` en builds que no sean de desarrollo (que falle de forma ruidosa si falta la variable).
- **Esfuerzo:** M.

**[C3] Sesión móvil imposible: JWT de 30 min, sin *refresh* y sin manejo de 401** · *Verificado* (amplía 01-C12 y 02-M9)
- **Evidencia:**
  - `TokenService.cs:39` (`AddMinutes(30)`).
  - `IdentityService.cs:75-85` emite `RefreshToken` pero no hay endpoint que lo consuma.
  - `AuthContext.tsx:63-74` solo guarda `accessToken`; el `refreshToken` se descarta.
  - `http.ts:69-83` lanza `ApiError` en 401 sin reintentar ni cerrar sesión.
- **Impacto:** en móvil la app se reabre horas o días después, no se recarga como una pestaña web. Pasados 30 min todas las pantallas fallan en silencio aunque `NavigationGuard` cree que hay sesión, porque Spotify sí se renueva y el back no. Para el usuario la app se ve rota.
- **Fix:**
  - Back: `POST /Auth/refresh` con rotación y revocación.
  - Front: guardar el refresh en SecureStore y usar un interceptor *single-flight*:
    ```ts
    let refreshing: Promise<string | null> | null = null
    async function withAuth<T>(fn: (t: string | null) => Promise<T>): Promise<T> {
      try { return await fn(sessionToken) }
      catch (e) {
        if (!(e instanceof ApiError) || e.status !== 401) throw e
        refreshing ??= refreshSession().finally(() => { refreshing = null })
        const t = await refreshing
        if (!t) { await onSessionExpired(); throw e }
        return fn(t)
      }
    }
    ```
  - Opcional: biometría con `expo-local-authentication` para desbloquear la sesión guardada.
- **Esfuerzo:** M.

**[C4] Spotify como login primario no sirve para una app pública** · *Verificado* (documentación) / *Sospecha fundada* (efecto en el back)
- **Evidencia:**
  - Política de Spotify desde el 11 de febrero de 2026 (apps nuevas) y el 9 de marzo de 2026 (existentes): en modo desarrollo, **5 usuarios por app**, el dueño necesita Premium activo y `GET /me` **ya no devuelve `email` ni `country`**.
  - La cuota extendida es solo para organizaciones (según fuentes de terceros, ≥250k MAU).
  - `encorly_back/EncorelyApplication/Services/SpotifyService.cs:13` vincula la identidad usando `profile.Email`.
  - El front usa `/recommendations` y `/audio-features` (`swipeFeed.ts:34`, `spotifyApi.ts:215`), deprecados para apps nuevas desde noviembre de 2024 (01-I20).
  - `preview_url` llega `null` en apps nuevas, así que el Sound-Swipe se queda mudo.
- **Impacto:**
  - No se puede pasar de 5 usuarios en producción.
  - El vínculo Spotify↔back probablemente crea usuarios con email nulo o choca con la restricción `UQ_Users_Email`.
  - El revisor de Apple/Google **no está en la allowlist**: o entra con una cuenta demo por email (Guideline 2.1) o hay rechazo. Hoy el camino email es un callejón sin salida (01-C13, 02-I19).
- **Fix:**
  - Hacer de email (y luego Google/Apple) el login primario y de Spotify un *conector opcional*.
  - Sacar el cálculo de vibe y el feed del front hacia el back.
  - Dejar de depender de `preview_url`, `recommendations` y `audio-features` (alternativas: previews de Deezer o iTunes Search API, revisando sus licencias).
- **Esfuerzo:** L.

**[C5] Sin eliminación de cuenta dentro de la app** · *Verificado*
- **Evidencia:** no hay `DELETE /User` ni equivalente (grep de `[HttpDelete]`: solo `VenueController.cs:55`). `SettingsScreen.tsx` solo tiene "Cerrar sesión".
- **Impacto:** rechazo seguro en App Store (5.1.1(v), obligatorio desde 2022) y en Google Play (política de eliminación de cuenta: dentro de la app **y** con URL web pública).
- **Fix:** `DELETE /api/v1/User/me`, que tome la identidad del JWT (no de la query, ver 01-C1), con borrado o anonimización en cascada (swipes, matches, mensajes, salas) y revocación del refresh token. En la UI: Ajustes → "Eliminar cuenta" con confirmación de doble paso, y una página web con el formulario para Play.
- **Esfuerzo:** M.

**[C6] Contenido generado por usuarios sin reportar ni bloquear** · *Verificado*
- **Evidencia:** no hay endpoints ni UI de *report*/*block* (el grep de `block|report` en back y front no encuentra nada). Hay chat 1:1 (`ChatScreen.tsx`) y salas de evento (`VenueController.cs:40`). La moderación es por *substring* (02-I7). `DELETE /Venue/messages/{id}` lo puede ejecutar cualquiera (02-I15).
- **Impacto:** App Store 1.2 exige filtrar contenido, permitir reportar, bloquear usuarios abusivos y publicar un contacto. Google Play (política UGC) exige lo mismo. En una app para conocer desconocidos es motivo de rechazo y, además, un riesgo real de acoso.
- **Fix:**
  - Tablas `user_blocks(blocker_id, blocked_id)` y `reports(reporter_id, target_type, target_id, reason, created_at, status)`.
  - Filtrar bloqueados en radar, matches y chat.
  - Menú "⋯" en el chat y en el perfil con "Reportar" y "Bloquear".
  - Un correo de contacto y un SLA de revisión de 24 h declarados.
- **Esfuerzo:** M.

**[C7] Sin política de privacidad ni términos, y edad sin mínimo** · *Verificado*
- **Evidencia:**
  - `SettingsScreen.tsx:579-593` responde "Próximamente" en Términos y Privacidad.
  - `SettingsScreen.tsx:368-372` acepta una edad de 1 a 120 y además solo la guarda en AsyncStorage (02-I26).
  - Se piden edad y género.
- **Impacto:**
  - Ambas tiendas exigen una URL de política de privacidad para publicar.
  - La Ley 1581 de 2012 y el Decreto 1377 de 2013 exigen autorización previa, expresa e informada, y un aviso de privacidad.
  - El tratamiento de datos de menores está restringido (art. 7).
  - Una app de encuentros entre desconocidos que admite a un niño de 12 años es inaceptable, y los cuestionarios de clasificación de edad de Apple y Google la llevarían a 17+/18+.
- **Fix:**
  - Pantalla de consentimiento en el registro (checkbox sin marcar por defecto con enlaces).
  - Fecha de nacimiento, no edad, y bloqueo si es menor de 18.
  - Guardar la edad en el back, con la versión de política aceptada y la fecha.
  - Publicar privacidad y términos en la landing.
  - Clasificación 18+ en ambas tiendas.
- **Esfuerzo:** M (legal aparte).

**[C8] Push notifications inexistentes de punta a punta** · *Verificado*
- **Evidencia:**
  - No están `expo-notifications` ni `expo-device` (`package.json`).
  - `FirebasePushNotificationService.cs:21-35`: el envío está comentado (`// var response = await FirebaseMessaging...SendAsync`) y usa `Topic = userId`.
  - No hay tabla ni endpoint de *device tokens*.
  - `Program.cs:41-48` captura y oculta el fallo de inicialización de Firebase.
  - 02-I6: la notificación de match va al usuario equivocado.
- **Impacto:** en una app de matches y chat, sin push el usuario no se entera de un match ni de un mensaje si no abre la app. Además, los *topics* de FCM los suscribe el cliente: con el GUID de otra persona, cualquier cliente con el SDK de Firebase podría suscribirse a sus notificaciones.
- **Fix (de punta a punta):**
  1. `npx expo install expo-notifications expo-device` y añadir el plugin en `app.json` con icono y color de notificación Android.
  2. Pedir permiso **en contexto**: tras el primer match, no al abrir la app. Android 13+ exige `POST_NOTIFICATIONS` en tiempo de ejecución.
  3. Llamar a `Notifications.setNotificationChannelAsync('chat', { importance: MAX })` y `('matches', …)` antes de pedir el token en Android.
  4. Obtener el token con `getExpoPushTokenAsync({ projectId })`, o `getDevicePushTokenAsync()` si se envía directo por FCM v1.
  5. Back: `POST /api/v1/devices { token, platform }` y `DELETE` al cerrar sesión; tabla `user_devices(user_id, token, platform, last_seen)`; envío por **Expo Push API** (resuelve APNs y FCM) o FCM v1 con tokens, **nunca por topic**.
  6. Credenciales: clave APNs (.p8) y `google-services.json`/clave FCM v1 cargadas en EAS (`eas credentials`).
  7. Deep link al tocar la notificación: `data: { url: 'encorely://chat/<roomId>' }`, `Notifications.addNotificationResponseReceivedListener` y `getLastNotificationResponseAsync()` para el arranque en frío.
  8. Limpiar tokens inválidos con los *receipts* de Expo (`DeviceNotRegistered`).
  - El push no funciona en Expo Go Android desde SDK 53: hace falta *dev build*.
- **Esfuerzo:** L.

### Importantes

**[I1] Chat, Ajustes y Radar-matches son *tabs ocultas*, no pantallas apiladas** · *Verificado* (estructura) / *Sospecha fundada* (comportamiento del back)
- **Evidencia:** `src/app/(main)/_layout.tsx:85-106` declara `chat/[id]`, `settings` y `radar-matches` como `Tabs.Screen` con `href: null`. `ChatScreen.tsx:153` usa `router.back()`.
- **Impacto:**
  - Sin transición nativa de *push* y **sin swipe-back de borde en iOS**.
  - En Android, el botón atrás sigue el `backBehavior` de las tabs (por defecto vuelve a la primera tab, no a Conexiones).
  - Al abrir el chat desde una push en frío, `router.back()` no tiene historial.
  - En Ajustes la tab bar sigue visible.
- **Fix:**
  ```
  src/app/(main)/_layout.tsx      -> <Stack />
  src/app/(main)/(tabs)/_layout.tsx -> <Tabs /> (index, radar, matches, profile)
  src/app/(main)/chat/[id].tsx, settings.tsx, radar-matches.tsx -> pantallas del Stack
  ```
  Y en el chat: `router.canGoBack() ? router.back() : router.replace('/(main)/(tabs)/matches')`.
- **Esfuerzo:** M.

**[I2] Tab bar flotante con posición fija que ignora *insets* y *edge-to-edge*** · *Verificado* (código) / *Sospecha fundada* (aspecto visual)
- **Evidencia:**
  - `(main)/_layout.tsx:113-127`: `position:'absolute'`, `bottom: Platform.OS==='ios'?30:20`, `height:64`.
  - Las listas reservan poco espacio: `MatchesScreen.tsx:174` y `RadarMatchesScreen.tsx:167` con `paddingBottom:40`, `SettingsScreen.tsx:441` con `60`.
  - SDK 54 fuerza *edge-to-edge* en Android.
- **Impacto:**
  - Con navegación de 3 botones en Android (unos 48 dp), la barra se monta sobre la barra del sistema.
  - En iPhone sin botón Home queda a una distancia arbitraria del *home indicator*.
  - El último ítem de Conexiones, Radar y Ajustes queda tapado (02-I20, peor en nativo).
- **Fix:**
  ```tsx
  const insets = useSafeAreaInsets()
  tabBarStyle: { ...styles.tabBar, bottom: insets.bottom + 12 }
  // en listas:
  const tabH = useBottomTabBarHeight()
  contentContainerStyle={{ paddingBottom: tabH + 24 }}
  ```
  Alternativa: valorar `expo-router/unstable-native-tabs` (tab bar nativa, *liquid glass* en iOS 26), que da accesibilidad y *insets* gratis.
- **Esfuerzo:** S.

**[I3] Teclado del chat mal compensado** · *Verificado* (iOS por lectura) / *Sospecha fundada* (Android)
- **Evidencia:**
  - `ChatScreen.tsx:149` aplica `paddingBottom: insets.bottom` **fuera** del `KeyboardAvoidingView`.
  - `:164-165` usa `keyboardVerticalOffset` de 10/20, que no corresponde a la altura real de la cabecera.
  - `LoginScreen.tsx:112` usa `behavior: undefined` en Android.
- **Impacto:**
  - En iOS queda un hueco de unos 34 pt entre el input y el teclado.
  - En Android con *edge-to-edge*, `behavior='height'` es inconsistente (el input puede quedar tapado o saltar).
  - El commit `7b8e771` ("fix chat keyboard") se validó en web.
- **Fix:**
  ```tsx
  import { KeyboardAvoidingView, KeyboardProvider } from 'react-native-keyboard-controller'
  // root: <KeyboardProvider>…
  <KeyboardAvoidingView behavior="padding" keyboardVerticalOffset={headerHeight} style={{ flex: 1 }}>
  ```
  Además: quitar el `paddingBottom` del contenedor (usar `edges={['bottom']}` solo con el teclado cerrado) y `FlatList inverted` en lugar de `scrollToEnd` en `onContentSizeChange`.
- **Esfuerzo:** S-M.

**[I4] Audio de las previews: sigue sonando al cambiar de tab e ignora el modo silencio** · *Verificado* por lectura
- **Evidencia:**
  - `SwipeCard.tsx:196-241`: la reproducción automática depende solo de `isFront`.
  - No hay `useIsFocused`/`useFocusEffect` en todo `src`.
  - Las tabs mantienen montadas las pantallas.
  - `playsInSilentModeIOS: true` (`:204`) sin configuración de interrupciones ni *ducking*.
  - `progress` se actualiza con `setState` cada 250 ms (`:217-221`) y vuelve a renderizar toda la tarjeta.
- **Impacto:**
  - Al ir a Radar o Perfil la canción sigue sonando.
  - En un concierto o en clase con el móvil en silencio, la app suena igual.
  - La rama 57 (`usePreviewAudio.ts`) arregla la carrera de desmontaje, **pero no el foco ni el modo silencio**.
- **Fix:**
  ```ts
  const focused = useIsFocused()
  usePreviewAudio(track.previewUrl, isFront && focused)
  setAudioModeAsync({ playsInSilentMode: false, interruptionMode: 'duckOthers', shouldPlayInBackground: false })
  ```
  Además: no reproducir automáticamente hasta el primer toque (o un ajuste "Autoplay") y llevar el progreso a un `SharedValue` de Reanimated.
- **Esfuerzo:** S.

**[I5] Red: sin timeout, reintento ni detección de conexión** · *Verificado*
- **Evidencia:** `http.ts:66` hace `fetch` sin `AbortController` ni timeout. No hay `@react-native-community/netinfo`. Los errores se tragan (`SpotifyAuthContext.tsx:107-108`, 01-I12).
- **Impacto:**
  - En Android el `fetch` de RN (OkHttp) puede colgar indefinidamente.
  - El *cold start* de Render free (de 30 a más de 60 s) deja spinners infinitos.
  - En 4G de concierto, con red saturada, no hay feedback ni reintento.
- **Fix:**
  ```ts
  const ctrl = new AbortController(); const t = setTimeout(() => ctrl.abort(), 15_000)
  try { res = await fetch(url, { ...init, signal: ctrl.signal }) } finally { clearTimeout(t) }
  ```
  Además: TanStack Query con `onlineManager` y NetInfo, `focusManager` y AppState, `retry: 2` con *backoff* solo en GET, un banner "Sin conexión", y en el back un plan sin *sleep* o un *ping* de calentamiento.
- **Esfuerzo:** M.

**[I6] Accesibilidad nativa (VoiceOver/TalkBack) prácticamente ausente** · *Verificado*
- **Evidencia:**
  - 75 `TouchableOpacity`/`Pressable` frente a 8 props `accessibilityLabel`/`Role` en todo `src`.
  - El swipe solo funciona con `Gesture.Pan()` (`SwipeCard.tsx:252`), sin botones ni `accessibilityActions`.
  - Hay botones solo de icono sin etiqueta: atrás y enviar en el chat (`ChatScreen.tsx:153,198`), play (`SwipeCard.tsx:337`) y las tabs custom (`_layout.tsx:20-29`, que pisan el `style` del navegador).
  - Objetivos táctiles por debajo de 44 pt: chevron atrás de 24 pt sin `hitSlop` (`ChatScreen.tsx:153`), botón enviar de 40×40 (`:253`).
  - Etiquetas de tab de 10 pt dentro de alturas fijas de 48/64 (`_layout.tsx:112-141`) que se cortan con Dynamic Type o la fuente de Android al 200 %.
- **Impacto:** el flujo principal (swipe) es inaccesible para lectores de pantalla. También es un riesgo de rechazo menor.
- **Fix:**
  ```tsx
  <Animated.View accessible accessibilityLabel={`${track.name}, ${track.artistName}`}
    accessibilityActions={[{ name: 'like', label: 'Me gusta' }, { name: 'nope', label: 'Paso' }]}
    onAccessibilityAction={(e) => onSwipe(e.nativeEvent.actionName === 'like' ? 'right' : 'left')}>
  ```
  Además: botones visibles ✕/♥, `hitSlop={12}`, mínimo 44×44 y quitar las alturas fijas en contenedores de texto.
- **Esfuerzo:** M.

**[I7] Arranque: el splash se oculta antes de conocer la sesión y el Keychain sobrevive a la reinstalación** · *Verificado* (código) / *Sospecha fundada* (efecto)
- **Evidencia:**
  - `_layout.tsx:34-38` oculta el splash cuando cargan las fuentes, no cuando carga la sesión.
  - `src/app/index.tsx:4` redirige siempre a `step-1` y el guard imperativo corrige después.
  - Los tokens van al Keychain (`secureStorage.ts`), pero `onboarding_complete` va a AsyncStorage (`useOnboardingState.ts:54`).
  - En iOS el Keychain **persiste tras desinstalar** y AsyncStorage no.
- **Impacto:**
  - Destello del onboarding paso 1 en cada arranque con sesión.
  - Tras reinstalar en iOS: sesión de Spotify vieja con el onboarding "incompleto", lo que manda a `step-5` en un estado incoherente.
- **Fix:**
  - Mantener el splash hasta que `authLoading`, `spotifyLoading` y `onboardingLoaded` sean `false`.
  - Sustituir `NavigationGuard` por `Stack.Protected guard={…}` (expo-router ≥5).
  - Detectar la primera ejecución:
    ```ts
    if (!(await AsyncStorage.getItem('installed'))) { await secureDeleteItems(ALL_KEYS); await AsyncStorage.setItem('installed','1') }
    ```
- **Esfuerzo:** S-M.

**[I8] Paridad visual en Android: el *glassmorphism* desaparece y hay flashes blancos** · *Sospecha fundada*
- **Evidencia:**
  - Hay `BlurView` en la tab bar, cabeceras e **ítems de lista** (`MatchesScreen.tsx:68,85`, `ChatScreen.tsx:23,48`), todos sin `experimentalBlurMethod`. En Android, `expo-blur` sin ese método pinta solo un tinte semitransparente (`BlurView.types.d.ts:7-11`).
  - `userInterfaceStyle: "dark"` sin `expo-system-ui`: Android no lo aplica, los `Alert` del sistema salen claros y el fondo raíz es blanco durante las transiciones.
- **Impacto:** el diseño se ve plano y distinto en Android, aparecen flashes blancos al navegar o abrir el teclado y, si se activa el blur real en filas de lista, cuesta rendimiento.
- **Fix:**
  - `npx expo install expo-system-ui` y `"backgroundColor": "#0A0A0F"` en `app.json`.
  - En Android usar un fondo sólido `rgba` en las listas y reservar el blur real para la tab bar.
- **Esfuerzo:** S.

**[I9] Tiempo real en móvil: diseño a decidir antes de escribir código** · *Verificado* (estado) / recomendación
- **Evidencia:**
  - El front no usa SignalR.
  - Los hubs son anónimos (01-C16).
  - `Program.cs` no tiene `JwtBearerEvents.OnMessageReceived` para leer `access_token` de la query (necesario para WebSockets).
  - El chat no hace polling ni refresca al volver a la app (02-M2).
- **Impacto:** si se conecta SignalR "como en web": iOS suspende la app pocos segundos después de pasar a segundo plano, el socket muere y en Android Doze lo corta. Un reintento agresivo gasta batería y datos.
- **Fix (recomendado):**
  - Push para despertar (C8) y `fetch` en `AppState → active` y al enfocar la pantalla.
  - SignalR **solo con el chat abierto y en primer plano**, con `withAutomaticReconnect([0,2000,10000,30000])`, `accessTokenFactory: getFreshToken`, `stop()` en `AppState !== 'active'` y un refetch de mensajes al reconectar.
  - En el back: `[Authorize]` en los hubs, identidad tomada de `Context.UserIdentifier` y `OnMessageReceived`.
- **Esfuerzo:** M.

**[I10] Identidad del binario y assets de tienda sin preparar** · *Verificado*
- **Evidencia:**
  - `assets/icon.png`, `android-icon-foreground.png` y `splash-icon.png` son los de la **plantilla de Expo** (logo "A" azul y rejilla gris; revisados visualmente).
  - No hay `ios.buildNumber`/`android.versionCode` ni `appVersionSource`.
  - `bundleIdentifier`/`package` = `com.encorely.app`: es irreversible una vez publicado; conviene confirmar que se controla el dominio y que no está tomado en Play.
  - `extra.spotifyRedirectUriWeb` = `http://localhost:8081/...` (`app.json:31`): Spotify ya no acepta `localhost` en las redirect URIs.
  - No hay nombre localizado ni `ios.infoPlist.CFBundleDisplayName`.
- **Impacto:**
  - Rechazo por icono o splash *placeholder*.
  - Builds que colisionan en número de versión.
  - Bundle ID que no se puede cambiar después.
- **Fix:** icono propio 1024 sin transparencia, *adaptive icon* con la zona segura de 66 %, splash con el plugin `expo-splash-screen` (`image`, `imageWidth`, `dark`), `appVersionSource: remote` y `autoIncrement`, y quitar el `localhost`.
- **Esfuerzo:** S (más el diseño).

**[I11] Redirect de Spotify en nativo sin registrar y Google Sign-In inexistente** · *Sospecha fundada*
- **Evidencia:**
  - `config/spotify.ts:57-66`: en *dev build* la redirect es `encorely://spotify-callback`; en Expo Go es `exp://<ip>:<port>/--/spotify-callback`, que cambia con cada red y no se puede registrar.
  - La doc vigente de Spotify exige HTTPS o *loopback* y no menciona esquemas propios; históricamente se aceptaban.
  - El botón de Google es un `Alert` "próximamente" (`OnboardingStep3Screen.tsx:112-115`).
  - `/Auth/google` del back no valida el token (01-C3).
- **Impacto:**
  - Si el dashboard rechaza `encorely://`, no hay login de Spotify en nativo.
  - Con Google como login social, App Store 4.8 obliga a ofrecer una opción equivalente que proteja la privacidad (en la práctica, **Sign in with Apple**).
- **Fix:**
  - Registrar `encorely://spotify-callback` en el dashboard y probarlo en el primer *dev build*. Plan B: Universal Link/App Link HTTPS (`https://encorely.app/spotify-callback` con `associatedDomains` e `intentFilters`).
  - Google: `@react-native-google-signin/google-signin` con *client IDs* iOS/Android/Web y validación del `idToken` en el back con `GoogleJsonWebSignature.ValidateAsync`.
  - Sign in with Apple: `expo-apple-authentication`.
  - `useProxy` está deprecado y el código no lo usa, lo cual es correcto.
- **Esfuerzo:** M.

**[I12] La rama Expo 57 no lleva los cambios locales y no se ha compilado en nativo** · *Verificado*
- **Evidencia:**
  - Parte de `ff11541`. En `main` local hay 5 archivos modificados sin commit (`LoginScreen.tsx` +255 líneas con login por email, `http.ts`/`AuthContext.tsx` con `setApiToken` y sesión persistida).
  - La rama la hizo otra persona (`edav404`) en Windows (`docs/upgrade/build-all.txt`).
  - Su README admite que "la exportación JavaScript/Hermes no constituye un build nativo APK/IPA".
- **Impacto:** riesgo de perder el trabajo local o de mergear algo que compila a bundle pero crashea al arrancar. Hay módulos nativos nuevos y RN 0.86 retiró `absoluteFillObject`.
- **Fix:** ver §7.
- **Esfuerzo:** S.

### Menores

- **[M1] Dependencias muertas y desalineadas** · *Verificado*. `lottie-react-native` no se importa en ningún sitio y la rama 57 lo conserva. `victory-native` se eliminó del código (`HomeScreen.tsx:15`) pero sigue en `package.json`. `expo install --check` lista 10 paquetes desalineados (`expo@54.0.33` frente a `~54.0.37`, `@types/jest@30` frente a `29.5.14`, etc.). El `.npmrc` tiene `prefix=/home/edav404/...`, que rompe `npx` en esta máquina. Fix: `pnpm remove lottie-react-native victory-native` y adoptar el `.npmrc` de la rama. **S**
- **[M2] APIs deprecadas de Reanimated 4** · *Verificado*. `runOnJS` (`SwipeCard.tsx:268-270`, también en la rama) pasa a `scheduleOnRN` de `react-native-worklets`. `Dimensions.get` a nivel de módulo (`SwipeCard.tsx:23`, `SwipeStack.tsx:6`) pasa a `useWindowDimensions`. `Gesture.Pan()` sin `activeOffsetX([-10,10])` captura también los movimientos verticales. **S**
- **[M3] styled-components en filas de lista** · *Sospecha fundada*. `MessageBubble`/`MessageText` con props dinámicas (`ChatScreen.tsx:32-46`) recalculan el CSS en cada render de cada fila. No es bloqueante con pocos mensajes, pero es la deuda de 01 (dos sistemas de estilos). Fix: `StyleSheet` más variantes en las filas de lista primero. **M**
- **[M4] Imágenes sin caché** · *Verificado*. `Image` de RN para carátulas y avatares (`SwipeCard.tsx:313`, `MatchesScreen.tsx:70`, `ProfileScreen.tsx:688`). Fix: `expo-image` con `cachePolicy="memory-disk"` y `placeholder`, y precargar la siguiente carátula del stack. Además, los avatares de `pravatar.cc` (01-M5) son **caras de personas reales falsas en una app de encuentros**: en tienda eso cuenta como contenido engañoso. **S**
- **[M5] Fuentes cargadas en tiempo de ejecución** · *Verificado*. `useFonts` con 7 pesos retrasa el splash (`_layout.tsx:23-31`) y el plugin `expo-font` está declarado sin `fonts`. Fix: incrustarlas al compilar con `["expo-font", { "fonts": ["node_modules/@expo-google-fonts/inter/500Medium/Inter_500Medium.ttf", …] }]` y reducir a 4 pesos. **S**
- **[M6] PKCE sin reanudación en arranque en frío** · *Sospecha fundada*. Si Android mata la app mientras Custom Tabs está abierto, el deep link `encorely://spotify-callback?code=…` abre la ruta `spotify-callback.tsx`, pero el intercambio solo existe para web (`SpotifyAuthContext.tsx:163`). Resultado: spinner infinito. Fix: en nativo, leer `code` con `useLocalSearchParams` en esa ruta y completar el intercambio con el verifier guardado. **S**
- **[M7] Haptics bien encapsulados, pero sin uso semántico** · *Verificado*. `shared/lib/haptics.ts` es correcto. Falta `notificationAsync(Success)` al hacer match, que es justo el momento de mayor emoción. **S**
- **[M8] iPad en modo compatibilidad** · *Sospecha fundada*. Con `supportsTablet:false`, la app sigue corriendo en iPad (en modo compatibilidad) y Apple a veces revisa en iPad. Hay que verificar el layout a 1024×768 escalado. **S**
- **[M9] Permisos por defecto de la plantilla Android** · *Sospecha fundada*. El prebuild suele añadir `SYSTEM_ALERT_WINDOW` y `READ/WRITE_EXTERNAL_STORAGE`. Fix: `android.blockedPermissions` y revisar el `AndroidManifest` del primer build. **S**
- **[M10] SecureStore: límite de 2048 bytes por valor** · *Verificado*: hoy no se supera. Los valores guardados son el JWT (unos 500 B), los tokens de Spotify (unos 300 B) y JSON pequeños. Si en el futuro se guardan perfiles o vibe extendidos, hay que moverlos a AsyncStorage o MMKV y dejar en SecureStore solo los secretos. Además conviene `keychainAccessible: AFTER_FIRST_UNLOCK` en los tokens que tenga que leer un handler de push en segundo plano. **S**

---

## 4. Hallazgos de 01/02 que cambian de prioridad por ser app móvil

| Hallazgo | Antes | Ahora | Por qué |
|---|---|---|---|
| 01-C12 / 02-M9 JWT 30 min sin refresh | Crítico | **Crítico bloqueante** | En móvil las sesiones duran semanas; sin refresh, la app "muere" cada media hora (C3) |
| 01-I20 APIs deprecadas de Spotify | Importante | **Crítico** | Desde marzo de 2026: 5 usuarios, `/me` sin email y previews nulas; el revisor no puede entrar (C4) |
| 01-C13 / 02-I19 sin Spotify es callejón sin salida | Crítico | **Crítico bloqueante de tienda** | La cuenta demo por email debe dar acceso completo (App Store 2.1) |
| 01-M7 sin términos ni privacidad | Menor | **Crítico** | Requisito para publicar en ambas tiendas y por la Ley 1581 (C7) |
| 01-I10 / 02-I7 / 02-I15 moderación | Importante | **Crítico** | App Store 1.2 y Play UGC exigen reportar y bloquear (C6) |
| 02-I6 notificación al usuario equivocado | Importante | **Crítico** (incluido en C8) | En móvil la push es el canal principal; hoy además no existe |
| 01-M6 / 02-M2 chat sin tiempo real | Menor | **Importante** | Sin push ni refetch en foreground no hay conversación posible (I9) |
| 01-M8 / 02-I20 tab bar tapa contenido | Menor / Importante | **Importante** | Con *edge-to-edge* y nav bar Android empeora (I2) |
| 01-M5 avatares `pravatar.cc` | Menor | **Importante** | Caras falsas en una app de encuentros: contenido engañoso para la revisión |
| 01-I16 / 02-M13 accesibilidad | Importante | Importante | Cambia la naturaleza: en nativo es VoiceOver/TalkBack y Dynamic Type, no Tab/foco (I6) |
| 01-C14 / 02-I21 logout no funciona en web | Crítico | **Menor (solo web)** | `Alert.alert` funciona en iOS/Android; sigue roto en web |
| 01-I15 / 02-I13 tokens en localStorage | Importante | **Menor (solo web)** | En nativo van a Keychain/Keystore (`secureStorage.ts`); bien resuelto |
| 01-M8 sonido huérfano al desmontar | Menor | **Importante** | En nativo se oye; además sigue sonando al cambiar de tab (I4) |
| 02-M12 desktop sin max-width | Menor | **Irrelevante** | El producto es móvil (portrait, sin tablet) |
| 01-I26 dos lockfiles | Importante | Importante, ya resuelto en la rama 57 | EAS Build elige el gestor por lockfile; con dos es indeterminado |
| 01-I25 CI | Importante | Importante | La rama 57 añade CI JS. Falta un job de EAS Build/Workflows |

---

## 5. Checklist de requisitos de tiendas

| Requisito | App Store | Google Play | Estado |
|---|---|---|---|
| Cuenta de desarrollador | Apple Developer Program (99 USD/año); D-U-N-S si es organización | 25 USD único. **Cuenta personal nueva: prueba cerrada con ≥12 testers durante 14 días antes de producción** | ❌ por confirmar |
| Build firmado y subido | EAS Build + `eas submit` (no hace falta Mac); SDK 57 exige Xcode 26.4, cubierto por la imagen de EAS | AAB con targetSdk ≥36 (obligatorio desde el 31-08-2026; SDK 54 y 57 ya apuntan a 36) | ❌ sin pipeline (C2) |
| Icono, splash y capturas | Icono 1024 sin alfa; capturas 6.9" | Icono 512, *feature graphic* 1024×500, capturas | ❌ de plantilla (I10) |
| URL de política de privacidad | Obligatoria | Obligatoria (ficha y dentro de la app) | ❌ (C7) |
| Etiquetas de privacidad / Data Safety | App Privacy: email, nombre, ID, mensajes, edad, género, historial musical, interacciones | Formulario Data Safety (mismo inventario más cifrado en tránsito y opción de borrado) | ❌ |
| Privacy manifest (iOS) | `PrivacyInfo.xcprivacy` con las *required-reason APIs* (UserDefaults de AsyncStorage, etc.); configurable en `ios.privacyManifests` | n/a | ❌ revisar en el primer build |
| Eliminación de cuenta | Dentro de la app (5.1.1(v)) | Dentro de la app **y** URL web | ❌ (C5) |
| Reportar, bloquear y moderar contenido | 1.2 (filtro, reporte, bloqueo y contacto) | Política UGC | ❌ (C6) |
| Clasificación por edad | Cuestionario (contacto entre desconocidos y UGC); declarar 18+ | IARC más público objetivo 18+ (fuera de Families) | ❌ verificación de edad mínima (C7) |
| Cuenta demo para revisión | Obligatoria si hay login (2.1) | Instrucciones de acceso en Play Console | ❌ el flujo por email no llega al valor (C4) |
| Login social | Si se añade Google, ofrecer Sign in with Apple o un equivalente (4.8) | — | ⚠️ al implementar Google (I11) |
| ATT (App Tracking Transparency) | Solo si hay *tracking* entre apps | — | ✅ no aplica hoy; no añadir SDKs de ads/atribución sin ATT |
| Permisos justificados | Strings `NS*UsageDescription` en español y específicos | Permisos mínimos; micrófono declarado exige justificación | ❌ micrófono fantasma de `expo-av` (C1) |
| Branding y términos de Spotify | Atribución y enlace a Spotify; cumplir la Developer Policy | Igual | ⚠️ revisar la Developer Policy (uso de datos y ML) antes de publicar |
| Ley 1581 (Colombia) | Autorización previa e informada, aviso de privacidad, canal de consultas/reclamos y política de tratamiento | Igual | ❌ |

---

## 6. Plan de testing nativo (sin emulador ni Android SDK en la máquina)

1. **Expo Go: descartado como plataforma principal.**
   - No soporta SDK 54 en iPhone físico.
   - No sirve para el redirect `encorely://` de Spotify ni para push en Android.
   - Con SDK 57 solo vale para pruebas rápidas de UI en un Android físico.
2. **Dev build por EAS (base del flujo).**
   - `eas build -p android --profile development` genera un APK de distribución interna: se instala con el enlace o QR en un Android físico y se conecta a `npx expo start --dev-client` por túnel.
   - iOS: requiere Apple Developer. `eas device:create` registra el UDID para builds ad hoc. Alternativa: TestFlight (perfil `production` más `eas submit`), que no necesita UDID y da hasta 10 000 testers.
   - Un build de simulador iOS no sirve sin Mac.
3. **Backend accesible desde el dispositivo.** El staging de Render por HTTPS o un túnel HTTPS (`cloudflared`/`ngrok`) hacia el back local, inyectado con `EXPO_PUBLIC_API_URL` en el perfil `development`. Nada de `http://IP-LAN`.
4. **Smoke manual por build (unos 15 min).** Registro por email, login con Spotify (redirect nativo), onboarding, 25 swipes con audio (silencio, llamada entrante, cambio de tab), match, chat con teclado (iOS y Android con gestos y con 3 botones), cerrar y reabrir tras 31 min (refresh), modo avión, fuente al 200 %, VoiceOver/TalkBack en el swipe, botón atrás en Android en el chat y eliminar cuenta.
5. **E2E con Maestro.** Flujos YAML en `.maestro/`:
   - `login_email.yaml`
   - `swipe_25.yaml` (con botones ✕/♥ y `testID`)
   - `chat_send.yaml`
   - `delete_account.yaml`

   Se ejecutan en la nube con **EAS Workflows** (job `maestro` sobre el build de `preview`, emulador Android y simulador iOS administrados), así no hace falta SDK local. Para correrlos en local, un Android físico por USB solo necesita `platform-tools` (adb), no el SDK completo.
6. **CI.** Usar la CI JS de la rama (typecheck, Jest, `expo install --check`, exportación) más un workflow EAS en PR a `main`: `build preview android` → `maestro`. Dejar `eas update --channel preview` para OTA de cambios solo JS (`runtimeVersion: appVersion`).
7. **Prueba cerrada en Play.** Si la cuenta es personal, reclutar 12 testers ya, porque los 14 días cuentan para producción.

## 7. Recomendación de base: Expo 57 desde `codex/actualizar-frontend`

**Partir de la rama.** Motivos:
- **`expo-av` ya no existe** en 55+, y la rama ya lo migró a `expo-audio` con mejor manejo del ciclo de vida (cancelación y `remove()`) y 6 tests.
- **Expo Go y las librerías nuevas** solo apuntan al SDK actual. SDK 54 es el último con arquitectura legacy y ya va 3 versiones por detrás.
- **Lo validado en la rama:** `expo install --check` limpio, Expo Doctor 21/21, typecheck y 30 tests en verde, bundles Hermes de iOS y Android exportados, `expo-57.0.28` (por encima de la regresión de memoria de Hermes en ≤57.0.8, dato de fuente de terceros), un solo lockfile (pnpm), CI y `.npmrc` saneado.
- **Plataforma:** RN 0.86 con iOS ≥16.4 y Android ≥7 es una base razonable para un lanzamiento en 2026.

**Condiciones antes del merge:**
1. Rebasar o portar los 5 archivos sin commit de `main` local (login por email y `setApiToken`) encima de la rama. Coordinarlo con `edav404` para no pisarse (01-I27).
2. Añadir los perfiles `development`/`preview`/`production` y `expo-dev-client` (C2), y **hacer un primer build Android en EAS e instalarlo en un teléfono** antes del merge. La rama nunca se compiló en nativo.
3. Revisar `ignore-scripts=true` en `.npmrc`: si algún paquete nativo necesita *postinstall*, el build de EAS fallará de forma opaca. La primera build lo confirma.
4. Retirar `lottie-react-native`, cambiar `runOnJS` por `scheduleOnRN` y aplicar I4 (foco y modo silencio) sobre `usePreviewAudio`.
5. Mantener `engines.node >=24` y `.node-version` y alinear `mise` local (Node 24.16 instalado: ✅).

**Alternativa en una línea:** quedarse en SDK 54 solo tendría sentido para una entrega académica web inmediata. Para el producto móvil significa migrar dos veces.

**Orden sugerido de reconstrucción (móvil):**
1. Rama 57 más EAS y *dev build* (C1, C2, I12).
2. Auth: refresh, login por email como primario y Spotify como conector (C3, C4, I11).
3. Navegación con Stack y tabs, *insets* y teclado (I1, I2, I3, I7).
4. Cumplimiento: borrar cuenta, reportar/bloquear, privacidad y edad (C5, C6, C7).
5. Push de punta a punta y refetch en foreground (C8, I9).
6. Red, accesibilidad, audio y assets (I4, I5, I6, I8, I10).
7. Maestro, prueba cerrada en Play y TestFlight.

---

### Fuentes
- Expo SDK 57 (versiones, Xcode 26.4, targetSdk 36): https://docs.expo.dev/versions/v57.0.0/
- Eliminación de `expo-av` en SDK 55: https://expo.dev/blog/upgrading-to-sdk-55
- Expo Go solo soporta el último SDK y *dev builds*: https://docs.expo.dev/develop/development-builds/introduction/ · https://docs.expo.dev/workflow/upgrading-expo-sdk/
- Regresión de Hermes en expo ≤57.0.8 (fuente de terceros): https://www.typingmind.com/skills/expo-expo-upgrade
- Spotify, modo desarrollo de febrero de 2026: https://developer.spotify.com/documentation/web-api/tutorials/february-2026-migration-guide · https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security · https://developer.spotify.com/documentation/web-api/references/changes/july-2026
- Spotify, reglas de redirect URI: https://developer.spotify.com/documentation/web-api/concepts/redirect_uri
- Google Play, target API 36 desde el 31-08-2026: https://developer.android.com/google/play/requirements/target-sdk · https://support.google.com/googleplay/android-developer/answer/11926878
- App Store Review Guidelines 1.2, 2.1, 4.8 y 5.1.1(v): https://developer.apple.com/app-store/review/guidelines/ (de conocimiento previo, no reconsultado en esta sesión)
