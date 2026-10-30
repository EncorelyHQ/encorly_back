# Nota: corrección de la §5.9 de la auditoría 03 ("¿3D? No")

2026-10-15 · Acompaña a `11-plan-front-perfil-social.md`.

## Qué decía la §5.9

No usar 3D ni en la app ni en la landing. Gasta batería y GPU en un contexto donde ambas escasean, y ningún objeto del dominio mejora al girarlo. Three.js suma peso y empeora el LCP.

## Qué cambió y por qué

Para la **app**, el perfil social sí usa profundidad, con un alcance mucho más chico que el que la auditoría descartó:

- **Sin WebGL ni librerías 3D.** Solo `perspective`, `rotateX`, `rotateY` y `scale` de Reanimated, que corren en el hilo de UI. No agregan dependencias ni assets, así que el costo de descarga es cero.
- **Un objeto del dominio que sí mejora al inclinarlo.** La boleta del perfil se inclina hacia atrás al bajar, y eso deja claro que el contenido pasa por encima. Las secciones entran inclinadas y los artistas giran hacia el centro del carrusel.
- **Solo en las pantallas del perfil** (Tú, perfil de otra persona y el laboratorio). Descubrir, el chat, los conciertos y los ajustes siguen planos.
- **Reduce motion lo apaga todo** (`useMotion()`), y con la fuente al 150 % o más los stickers casi no se inclinan.

Para la **landing** la §5.9 sigue vigente: nada de Three.js.

## Lo que falta medir

En el emulador (x86_64, render por software `swangle`) los frames no sirven como medida: la mediana fue 300 ms con el motion encendido y 200 ms con reduce motion. Eso dice que el 3D cuesta, pero no cuánto en un teléfono real. Antes de lanzar hay que medir en un Android de gama baja con la GPU de verdad. Si baja de 60 fps, se recorta primero `Carousel3D`, como dice el plan.

## Errata

El PR 3 (#11) y su commit `823078c` citan "03 §5.9" como origen del arreglo de `GenreBars` (porcentajes que no sumaban 100). Esa cita está mal: el problema venía del plan del perfil social, no de esta auditoría. El comentario del código ya se corrigió.
