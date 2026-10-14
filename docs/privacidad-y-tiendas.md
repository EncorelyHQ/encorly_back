# Datos personales y requisitos de tiendas (lado servidor)

Inventario de lo que la API recolecta y guarda, para llenar el formulario **Data Safety** de Google Play, las
**App Privacy details** de App Store y la política de tratamiento de datos (Ley 1581 de 2012). La política pública está en
`/legal/privacidad` y los términos en `/legal/terminos` (versión en `GET /api/v1/legal/current`).

## 1. Qué datos guarda el servidor

| Dato | Para qué | Obligatorio | Retención | Visible para otros usuarios |
|---|---|---|---|---|
| Correo electrónico | Cuenta e inicio de sesión | Sí | Hasta borrar la cuenta | No |
| Contraseña | Inicio de sesión | Sí | Solo el hash BCrypt, hasta borrar la cuenta | No |
| Nombre visible | Perfil | Sí | Hasta borrar la cuenta | Sí (radar, matches) |
| Fecha de nacimiento | Verificar 18+ y mostrar la edad | Sí (onboarding) | Hasta borrar la cuenta | Solo la edad |
| Ciudad | Perfil y eventos | Sí (onboarding) | Hasta borrar la cuenta | Sí |
| Bio | Perfil | No | Hasta borrar la cuenta | Sí |
| Géneros favoritos, swipes, ADN musical | Calcular afinidad | Sí | Hasta borrar la cuenta | Solo afinidad y géneros en común |
| Gustos importados de Spotify | Enriquecer el ADN (opcional) | No | Se reemplazan en cada importación | No (solo pesan en el ADN) |
| Asistencia a eventos | Radar del evento | No | Hasta borrar la cuenta | Sí, a otros asistentes del mismo evento |
| Likes y matches | Match recíproco | No | Hasta borrar la cuenta o deshacer el match | El like solo se revela si hay match |
| Mensajes del chat | Entregarlos | No | **Solo ciphertext.** Hasta el ack de entrega o el borrado de la cuenta | No (el servidor no puede leerlos) |
| Llaves públicas (identidad y caja) | Cifrado de extremo a extremo | Para chatear | Hasta borrar la cuenta | Sí, a los matches |
| Respaldo cifrado | Restaurar historial y llaves | No | Hasta que el usuario lo borre o borre la cuenta | No (cifrado con su passphrase) |
| Token push (Expo) | Avisar "mensaje nuevo" o "match nuevo" sin contenido | No | Hasta cerrar sesión o borrar la cuenta | No |
| Sesiones (refresh tokens) | Mantener la sesión | Sí | Solo su hash; 30 días tras vencer se borran | No |
| Bloqueos y reportes | Seguridad y moderación | No | Los reportes se conservan (anonimizados si se borra la cuenta) | No |
| Aceptación de términos | Prueba del consentimiento | Sí | Hasta borrar la cuenta | No |
| Dirección IP | Límite de intentos | — | **No se guarda**: solo en memoria para el rate limit | No |

No se usan los datos para publicidad, no se venden ni se comparten con terceros. Encargados del tratamiento que reciben
datos: el proveedor de base de datos/hosting (cuando se despliegue) y **Expo Push** (recibe el token push y un aviso sin
contenido, que reenvía a FCM/APNs).

## 2. Respuestas para Google Play · Data Safety

- ¿Recolecta o comparte datos? **Recolecta; no comparte** (el envío a Expo/FCM es procesamiento en nombre de la app).
- Cifrado en tránsito: **Sí** (HTTPS en producción).
- El usuario puede pedir el borrado: **Sí**, en la app (Perfil → Borrar cuenta) y en la web (`/cuenta/eliminar`).
- Tipos de datos: Información personal (correo, nombre, fecha de nacimiento → edad, ciudad), Mensajes (otros mensajes
  en la app: cifrados de extremo a extremo; el servidor no puede leerlos), Actividad en la app (interacciones: swipes,
  likes, eventos), Identificadores del dispositivo (token push). Ninguno es opcional para el funcionamiento salvo bio,
  Spotify, push y respaldo.
- Propósitos: Funcionalidad de la app, Administración de la cuenta, Seguridad y prevención de fraude.

## 3. Respuestas para App Store · App Privacy

- Data Linked to You: Contact Info (Email), User Content (Other User Content: perfil), Identifiers (User ID, Device ID
  por el token push), Usage Data (Product Interaction), Sensitive Info: **no**.
- Data Used to Track You: **ninguno**.
- Mensajes: no se declaran como "Emails or Text Messages" recolectados porque el servidor solo tiene ciphertext que no
  puede descifrar; se menciona el cifrado de extremo a extremo en la política.

## 4. Requisitos que el servidor ya cumple

| Requisito | Dónde |
|---|---|
| Borrado de cuenta desde la app | `DELETE /api/v1/me` |
| URL web de borrado (Google Play) | `GET /cuenta/eliminar` → `POST /api/v1/account-deletion` |
| Reportar y bloquear (contenido generado por usuarios, ambas tiendas) | `POST /api/v1/reports`, `POST /api/v1/blocks`; moderación en `/api/v1/admin/reports` |
| Mayoría de edad 18+ | Validación de `birthDate` en el perfil |
| Política y términos versionados, consentimiento registrado | `/legal/*`, tabla `user_consents`, `POST /api/v1/me/consents` |
| Acceso a los datos (Ley 1581) | `GET /api/v1/me/export` |
| Cuenta demo para revisores | `revisor@encorely.demo` (seed con `DEMO_USER_PASSWORD` y `SEED_REVIEWER_ONLY=true`, que no siembra el admin; la contraseña se entrega en la consola de cada tienda, nunca en el repo) |

Lo que corresponde al front (manifiesto `PrivacyInfo.xcprivacy`, textos de permisos, TalkBack/VoiceOver, capturas) está
en el repo `encorely-front`.
