# Guía de pruebas manuales del Spike 4

Proyecto 2026-003 "Control de Arreglos de Pago". Duración estimada: 15 minutos. Se prueba, desde la interfaz, lo que ningún script puede probar: el botón Responder y el editor de correo.

**Reglas simples**
- Trabajar solo con el correo `[SPIKE4] Prueba manual` y solo con las direcciones `buzondevpp1@55xljh.onmicrosoft.com` (la cola `Spike`) y `buzondevpp2@55xljh.onmicrosoft.com` (el "cliente"). No enviar nada a otra dirección.
- No borrar nada. Si algo falla, anotar el mensaje de error tal cual aparece (o hacer una captura) y seguir con la prueba siguiente.

**Links**
- App: <https://org36e60d9d.crm.dynamics.com/main.aspx?appid=8f8b7e54-59c3-f111-a05a-000d3a35abca>
- Conversación "Prueba manual SPIKE4" dentro de la app: <https://org36e60d9d.crm.dynamics.com/main.aspx?appid=8f8b7e54-59c3-f111-a05a-000d3a35abca&pagetype=entityrecord&etn=spk_conversacion&id=8e376bc1-5bc3-f111-a05a-000d3a35abca>
- Correo recibido (por si hace falta abrirlo directo): <https://org36e60d9d.crm.dynamics.com/main.aspx?appid=8f8b7e54-59c3-f111-a05a-000d3a35abca&pagetype=entityrecord&etn=email&id=80376bc1-5bc3-f111-a05a-000d3a35abca>

## Prueba 1. Responder desde el timeline

1. Abrir el link de la conversación. El formulario tiene dos columnas; a la derecha está el **timeline** (la línea de tiempo de actividades). Debe aparecer un correo recibido con asunto `[SPIKE4] Prueba manual`.
   - Si el timeline no aparece o está vacío, anotarlo y avisar en el chat antes de seguir.
2. En el timeline, expandir el correo recibido (clic sobre la tarjeta) y pulsar **Responder** (Reply).
3. Se abre la ventana del correo nuevo. Revisar tres campos antes de enviar:
   - **Para**: debe ser solo `buzondevpp2@55xljh.onmicrosoft.com`.
   - **De**: debe ser la cola **Spike** (`buzondevpp1@...`). Si aparece el nombre de una persona (el usuario con el que se inició sesión), cambiarlo a `Spike`. Si no deja cambiarlo, anotarlo.
   - **Asunto**: queda como `RE: [SPIKE4] Prueba manual`. No tocarlo.
4. Escribir una línea de texto en el cuerpo (por ejemplo "Respuesta de prueba 1") y pulsar **Enviar**.
5. Anotar: ¿se envió sin error? ¿El correo pasó a estado "Enviado" en el timeline (puede tardar hasta 1 minuto)?

## Prueba 2. Pegar una imagen en esa misma respuesta

Se hace **en la misma respuesta de la Prueba 1, antes de pulsar Enviar** (paso 4).

1. Tomar una captura de pantalla pequeña (en Windows: `Windows + Mayús + S`; en Mac: `Cmd + Ctrl + Mayús + 4`), de modo que quede en el portapapeles.
2. Hacer clic dentro del texto de la respuesta y pegar con `Ctrl + V` (`Cmd + V` en Mac). La imagen debe verse dentro del texto.
3. Esperar un par de segundos a que termine de cargar (que no tenga indicador de carga) y recién entonces pulsar **Enviar**.
4. Anotar si hubo algún aviso o error al pegar.

## Prueba 1b (opcional, conviene hacerla). Responder desde el formulario del propio correo

El botón Responder del timeline y el del formulario del correo son comandos distintos y pueden comportarse distinto, por eso conviene probar los dos.

1. En el timeline, hacer clic sobre el **asunto** del correo recibido para abrirlo en su propio formulario.
2. En la barra de comandos de ese formulario, pulsar **Responder** y repetir los controles de la Prueba 1 (Para, De, Asunto). Texto sugerido: "Respuesta de prueba 1b". No pegar imagen en esta.
3. Enviar y anotar si salió sin error.

## Prueba 3. El cliente responde cambiando el asunto (solo si puede abrir el buzón 2)

Requiere poder abrir `buzondevpp2@55xljh.onmicrosoft.com` en Outlook web (<https://outlook.office.com>) o de escritorio. **Si no tiene acceso a ese buzón, omitir esta prueba** y decirlo en el chat.

1. Esperar a que la respuesta de la Prueba 1 llegue al buzón 2 (suele tardar menos de 1 minuto).
2. En el buzón 2, abrir el mensaje recibido `RE: [SPIKE4] Prueba manual` y pulsar **Responder** (no "Reenviar").
3. **Cambiar el asunto** a `[SPIKE4] Asunto cambiado a mano` (conservar la etiqueta `[SPIKE4]`). Escribir una línea y enviar.
4. Indicar en el chat si se usó Outlook web o Outlook de escritorio (importa para el resultado).

## Al terminar

Avisar en el chat cuando termine, junto con: qué pruebas se hicieron (1, 2, 1b, 3), qué mostró el campo **De** en cada respuesta, y cualquier error o aviso que haya aparecido. Con eso se corre la verificación automática (`verificar.py`) y se informan los veredictos.
