# Manual de uso: Control de Arreglos de Pago

Para el personal de Arreglos de Pago: Oficial de Arreglos y Jefe de Arreglos.
Versión 1 (2026-10-20). Describe la aplicación tal como quedó construida y probada en el entorno de desarrollo.

> Donde dice **[captura pendiente]** se agregará una imagen de la pantalla antes de entregar el manual.

---

## 1. Qué hace la aplicación

La aplicación reúne en un solo lugar tres cosas que hoy se llevan por separado:

1. **Los correos de los clientes** que llegan al buzón del área. Cada correo nuevo abre una **conversación**; las respuestas del cliente se agregan a la misma conversación.
2. **Los arreglos de pago** que se negocian con el cliente, con su calendario de cuotas y el registro de pagos.
3. **El formato para firma** (F-NIC-0000052), que la aplicación llena sola con los datos del arreglo a partir de la plantilla aprobada.

Usted responde los correos desde la aplicación. Las respuestas salen siempre desde el buzón del área, nunca desde su correo personal.

## 2. Cómo entrar

1. Abra el enlace de la aplicación **Control de Arreglos de Pago** que le envió el administrador.
2. Inicie sesión con su cuenta del banco.
3. A la izquierda verá el menú. [captura pendiente]

Si no ve la aplicación o un menú que debería ver, avise al administrador: es un tema de permisos, no de la aplicación.

## 3. El menú

| Área | Entrada | Qué muestra | Quién la ve |
|---|---|---|---|
| **Conversaciones** | Mis conversaciones pendientes | Las conversaciones abiertas que usted tiene asignadas | Oficial y Jefe |
| | Conversaciones sin asignar | Las abiertas que nadie tomó todavía | Oficial y Jefe |
| | Conversaciones abiertas | Todas las abiertas del área | Oficial y Jefe |
| | Correos llegados a conversaciones finalizadas | Conversaciones ya cerradas a las que el cliente volvió a escribir | Oficial y Jefe |
| | Conversaciones finalizadas | Las cerradas, con sus tiempos | Oficial y Jefe |
| **Clientes y arreglos** | Clientes | Todos los clientes; la búsqueda sirve para buscar el historial por nombre o cédula | Oficial y Jefe |
| | Borradores | Arreglos en preparación | Oficial y Jefe |
| | Arreglos vigentes | Arreglos firmados y en curso | Oficial y Jefe |
| | Arreglos en riesgo | Vigentes con cuotas vencidas sin pago | Oficial y Jefe |
| | Arreglos de préstamo / de tarjeta | Vigentes por tipo de producto | Oficial y Jefe |
| | Arreglos cerrados | Terminados, anulados y descartados | Oficial y Jefe |
| | Todos los arreglos | Sin filtro | Oficial y Jefe |
| **Supervisión** | Correos sin respuesta | Conversaciones abiertas que esperan respuesta | Jefe |
| | Finalizadas sin respuesta | Cierres con resultado "Sin respuesta" | Jefe |
| | Respuestas con error de envío | Respuestas que el buzón no pudo enviar | Jefe |
| | Respuestas enviadas | Todas las respuestas del área | Jefe |
| | Borradores sin movimiento | Borradores sin cambios hace más de 30 días | Jefe |
| | Panel de supervisión | Gráficos de tiempos y las listas de pendientes | Jefe |
| **Administración del formato** | Versiones de plantilla | Las versiones del formato para firma | Jefe |

Son 20 entradas. El Oficial ve 13 y el Jefe las ve todas.

## 4. Conversaciones

### 4.1 De dónde salen

No se crean a mano. Cuando un cliente escribe al buzón del área:

- El correo entra a la **cola "Arreglos de Pago"** en menos de un minuto.
- El sistema abre una **conversación** con un número `CONV-000000`, el asunto y el remitente.
- Si el correo del remitente coincide con el de un cliente registrado, la conversación queda vinculada a ese cliente.
- Si el cliente responde a un correo que usted le envió, la respuesta se suma a la misma conversación.

### 4.2 Estados

| Estado | Qué significa |
|---|---|
| **Abierta** | Hay que atenderla |
| **Finalizada** | Se cerró con un resultado. Si el cliente vuelve a escribir, se marca "Correo nuevo en finalizada" |
| **Unida** | Sus correos se pasaron a otra conversación del mismo cliente; no se usa más |

### 4.3 El formulario de la conversación

[captura pendiente]

- **Encabezado:** estado, oficial asignado y desde cuándo espera respuesta.
- **Conversación:** número, asunto, remitente, cliente, fecha del primer correo, antigüedad y la marca de correo nuevo.
- **Finalización:** resultado, motivo y detalle (se llenan solo al finalizar), y quién y cuándo finalizó.
- **Tiempos:** tiempo hasta la primera respuesta, cantidad de respuestas y tiempo total. Los calcula el sistema.
- **Correos:** la línea de tiempo con todos los correos de la conversación.
- **Arreglos de la conversación:** los arreglos registrados desde esta conversación.

**Aviso de otra conversación abierta:** si el mismo cliente o el mismo remitente tiene otra conversación abierta, aparece un aviso arriba con su número. Si es el mismo tema, use **Unir a otra conversación**.

### 4.4 Responder un correo

1. En **Correos**, abra el correo del cliente y use **Responder**. [captura pendiente]
2. Escriba la respuesta. Puede adjuntar archivos e imágenes.
3. Envíe.

La respuesta sale desde el buzón del área aunque usted la escriba. Si alguien intenta enviarla a nombre de otro usuario, el sistema la rechaza con `CARP-EML-001`.

### 4.5 Vincular o crear el cliente

- Escriba el cliente en el campo **Cliente** y guarde. Solo se puede en una conversación abierta.
- Si el cliente no existe, use **+ Nuevo** dentro del mismo campo: se abre el formulario de cliente (nombre completo, cédula, correo y celular).

### 4.6 Reasignar (solo el Jefe)

No hay botón. El Jefe cambia el campo **Oficial asignado** y guarda. Si un Oficial lo intenta, aparece `CARP-CNV-008`.

## 5. Los botones (gestos)

Un botón hace una sola cosa y la confirma el servidor. Si el servidor rechaza la acción, usted ve un mensaje que empieza con un código `CARP-...`: el texto explica qué falta. Copie el código si tiene que avisar a soporte.

**Campos de gesto.** Resultado, motivo y detalle de la conversación, y los motivos de descarte, anulación y reapertura del arreglo, solo se guardan **con su botón**. Si escribe el motivo y guarda con Guardar o con el guardado automático, la aplicación lo detiene y le pide que use el botón.

### 5.1 En la conversación

**1. Tomar.** Aparece en una conversación abierta sin oficial, en el formulario y en las listas.
- Lo asigna a usted. Mensaje: "Conversación tomada por ...". Si ya era suya: "Ya era suya.".
- En una lista, seleccione una sola fila.
- Si otra persona la tomó en el mismo instante: "La conversación cambió en este instante. Actualice la pantalla y vuelva a intentar." (`CARP-TOM-900`). La pantalla se actualiza sola y muestra quién la tiene.
- `CARP-TOM-003`: solo se toma una conversación abierta. `CARP-TOM-004`: ya la tomó otra persona.

**2. Finalizar.** Aparece en una conversación abierta.
1. En **Finalización**, elija el **Resultado**: Con arreglo de pago, Sin arreglo de pago o Sin respuesta.
2. Si es **Sin respuesta**, llene también **Motivo** y **Detalle**.
3. Pulse **Finalizar** y confirme.
- `CARP-CNV-003`: falta el resultado. `CARP-CNV-004`: con "Sin respuesta" faltan el motivo o el detalle.

**3. Reabrir.** Aparece en una conversación finalizada. La vuelve a Abierta y limpia el resultado y la finalización. `CARP-CNV-010`: no cambie otros campos en el mismo paso.

**4. Unir a otra conversación.** Aparece en una conversación abierta.
1. Pulse el botón y elija la conversación de destino. Solo se ofrecen conversaciones abiertas.
2. Confirme. Los correos pasan al destino y **no se puede deshacer**.
3. La aplicación abre la conversación de destino e informa cuántos correos y arreglos se movieron.
- `CARP-UNI-010`: solo se unen conversaciones del mismo cliente. `CARP-UNI-006` y `-008`: una de las dos ya participa de otra unión. `CARP-UNI-004` y `-005`: las dos tienen que estar abiertas.

**5. Iniciar conversación nueva.** Aparece en una conversación finalizada que recibió un correo nuevo.
- El correo nuevo se mueve a una conversación nueva, que se abre en pantalla. La anterior sigue finalizada.
- `CARP-INI-003`: la conversación no está finalizada o no tiene correos nuevos.

### 5.2 En el arreglo

**6. Generar formato.** Aparece siempre.
- Guarde antes los cambios; si no, la aplicación se lo pide.
- El mensaje de confirmación depende del estado:
  - **Borrador sin congelar:** al generar se **congelan** los datos del acuerdo y del cliente. Podrá desbloquear antes de la firma.
  - **Vigente reabierto:** se vuelve a congelar con los datos actuales.
  - **Otros estados:** se genera el formato y el arreglo no cambia.
- Se descarga el archivo `.xlsx` listo para imprimir. Cada generación queda registrada en la pestaña **Formatos**.
- Mensajes frecuentes: `CARP-GEN-004` (el plazo supera la capacidad de la plantilla), `CARP-GEN-012` (no tiene permiso para ver los datos sensibles del cliente; pida acceso), `CARP-GEN-900` (falla temporal: no se guardó nada, intente de nuevo).

**7. Desbloquear.** Aparece en un borrador **congelado**. Descongela los datos para corregirlos. El formato que ya imprimió deja de valer: hay que generarlo e imprimirlo de nuevo.

**8. Formalizar.** Aparece en un borrador congelado, una vez que el cliente firmó.
- Confirme que el cliente firmó el formato impreso.
- El arreglo pasa a **Vigente** y recibe su número definitivo, que aparece en el mensaje.
- `CARP-ARR-019`: primero genere el formato. `CARP-ARR-020`: otro arreglo del mismo crédito se formalizó después de imprimir este formato.

**9. Descartar.** Aparece en un borrador.
1. En la pestaña **Cierre**, escriba el **Motivo de descarte**.
2. Pulse **Descartar** y confirme. No consume número y **no se puede recuperar**.
- `CARP-ARR-013`: falta el motivo.

**10. Anular.** Aparece en un arreglo vigente.
1. En **Cierre**, escriba el **Motivo de anulación**.
2. Pulse **Anular** y confirme. **No se puede revertir.** Si el cliente vuelve a negociar, registre un arreglo nuevo.
- `CARP-ARR-014`: falta el motivo.

**11. Reabrir arreglo (solo el Jefe).** Aparece en un arreglo terminado o en un vigente congelado.
1. En **Cierre**, escriba el **Motivo de reapertura**.
2. Pulse **Reabrir arreglo** y confirme. Los datos se descongelan. El próximo formato los vuelve a congelar.
- `CARP-ARR-015`: falta el motivo. `CARP-ARR-016`: solo la jefatura reabre.

**12. Registrar un pago.** No tiene botón.
1. En la pestaña **Cuotas**, abra la cuota.
2. Llene **Fecha de pago** y **Monto pagado**, las dos juntas, y guarde.
- `CARP-CUO-002`: solo se registran pagos en un arreglo vigente. `CARP-CUO-003`: faltan la fecha o el monto, o el monto no es mayor que cero.

### 5.3 En la versión de plantilla (solo el Jefe)

**13. Publicar versión.** Aparece en una versión en Borrador.
- Guarde antes los cambios.
- Al confirmar, la versión pasa a **Vigente**, la anterior pasa a **Histórica** y ya no se puede modificar. El sistema guarda la huella del archivo, la fecha y quién aprobó.
- Si el mapeo o el archivo tienen problemas, la publicación se rechaza y el mensaje los enumera (`CARP-VER-005` a `-010`).

**14. Vista previa.** Aparece en una versión en Borrador.
- Elija un arreglo cualquiera. Se descarga el formato generado con **esta** versión, sin congelar ni guardar nada.
- Sirve para revisar una plantilla nueva antes de publicarla.

## 6. Arreglos de pago

### 6.1 Registrar un arreglo

- **Desde la conversación:** use **+ Nuevo** en la sección "Arreglos de la conversación". El cliente y la conversación se llenan solos.
- **Desde el cliente:** use **+ Nuevo** en "Arreglos del cliente".

Llene los datos del acuerdo: producto, número de producto, tipo, moneda, oficial, cuenta a debitar, saldo inicial, monto abonado, plazo, cuota mensual y fecha de inicio. Al guardar, el sistema:

- toma la versión vigente de la plantilla y le asigna un número provisional;
- calcula el saldo remanente, el total a pagar y la fecha de fin;
- arma el calendario de cuotas, que verá en la pestaña **Cuotas**. Las cuotas vencen el mismo día de cada mes desde la fecha de inicio; si el mes no tiene ese día, el último día del mes.

Mensajes frecuentes al guardar:

| Código | Qué hacer |
|---|---|
| `CARP-ARR-001` | No hay versión vigente de la plantilla. Avise al Jefe |
| `CARP-ARR-003` | El plazo está fuera del rango que admite la plantilla |
| `CARP-ARR-004` | El monto abonado es mayor que el saldo inicial |
| `CARP-ARR-005` | Con una cuota menos ya se cubre el saldo. El mensaje dice con cuántas |
| `CARP-ARR-006` | Faltan datos para calcular; el mensaje dice cuáles |
| `CARP-ARR-011` | El arreglo está congelado. Desbloquee (si es borrador) o pida al Jefe que lo reabra |

**La cuenta a debitar y la cédula** se ven solo con el permiso de datos sensibles. Sin él aparecen bloqueadas y no se puede registrar el arreglo. Pida el acceso al administrador.

### 6.2 Ciclo de vida

| Estado | Cómo se llega | Qué se puede hacer |
|---|---|---|
| **Borrador** | Al registrar | Corregir, generar formato (congela), desbloquear, formalizar, descartar |
| **Vigente** | Formalizar | Registrar pagos, generar formato, anular; el Jefe puede reabrir |
| **Terminado** | Solo, cuando todas las cuotas tienen un pago registrado (un pago parcial cuenta) | Generar formato; el Jefe puede reabrir |
| **Anulado** | Anular | Solo consulta y formato |
| **Descartado** | Descartar | Solo consulta y formato |

**Congelar** significa que los datos del acuerdo y una copia de los datos del cliente (nombre, cédula, correo, celular) quedan fijos tal como salieron impresos. Mientras está congelado, el sistema no deja cambiar esos datos.

### 6.3 Las pestañas del arreglo

[captura pendiente]

- **General:** acuerdo, montos y plazo, plantilla y numeración, datos del cliente al congelar.
- **Cuotas:** el calendario y los pagos.
- **Formatos:** cada formato generado, con su fecha y versión.
- **Cierre:** formalización, descarte, anulación y reapertura, con sus motivos.

### 6.4 Historial de cambios

En el arreglo, la pestaña de **auditoría** muestra quién cambió qué y cuándo. Los montos y los datos de identificación no se registran en el historial. [captura pendiente]

## 7. Plantilla del formato (solo el Jefe)

La versión **Vigente** es la que se usa para todos los formatos. Hoy es la **v07**, aprobada por el área de negocio.

Para cambiar el formato:

1. En **Versiones de plantilla**, cree una versión nueva con **+ Nuevo**. Nace en Borrador.
2. Póngale un número con el formato `v08`: la letra v y 2 o 3 dígitos (`CARP-VER-012`).
3. Suba el archivo `.xlsx` (máximo 5 MB) y cargue el mapeo de celdas en la sección **Mapeo de la versión**. Cada fila dice qué dato va en qué hoja y celda.
4. Revise con **Vista previa** sobre un arreglo existente.
5. **Publique.**

Una versión publicada no se modifica. Para corregirla, cree otra. Si el archivo de una versión vigente se altera por fuera de la aplicación, la generación se detiene con `CARP-GEN-008`.

## 8. Panel de supervisión (solo el Jefe)

[captura pendiente]

- **Promedio de minutos por respuesta, por oficial.**
- **Tiempo total promedio por oficial.** Excluye las conversaciones finalizadas "Sin respuesta".
- **Correos sin respuesta** y **Respuestas con error de envío.**

Los tiempos se calculan con cada respuesta enviada desde la aplicación.

## 9. Lo que el sistema hace solo

- **Captura del correo:** el buzón del área se sincroniza con la aplicación. Cada correo entra a la cola y abre o continúa una conversación.
- **Salida del correo:** toda respuesta sale con el buzón del área como remitente.
- **Tiempos:** calcula el tiempo de cada respuesta, quién respondió y los tiempos de la conversación.
- **Avisos al equipo:**
  - Si la sincronización del buzón falla, envía un aviso a los destinatarios configurados.
  - Cada 30 minutos en horario hábil revisa:
    - que sigan entrando correos;
    - que no queden correos sin conversación (si los hay, los ordena solo);
    - que no haya respuestas con error de envío;
    - que no hayan fallado las depuraciones de datos antiguos.
- **Depuración:** cuando el administrador la configure, los datos con más de 2 años se eliminarán según la política de retención.

## 10. Preguntas frecuentes

**Escribí el motivo y guardé, y la aplicación no me dejó.** Es lo esperado: los motivos se guardan con su botón (Finalizar, Descartar, Anular, Reabrir arreglo).

**No veo el botón que necesito.** Los botones aparecen según el estado. Ejemplo: Formalizar solo aparece en un borrador congelado; primero genere el formato.

**Me apareció un mensaje sin código CARP.** Actualice la página (Ctrl+F5) e intente de nuevo. Si se repite, avise a soporte con la hora y lo que estaba haciendo.

**El cliente respondió a una conversación ya finalizada.** Aparece en "Correos llegados a conversaciones finalizadas". Ábrala y use **Iniciar conversación nueva**, o **Reabrir** si es el mismo tema.
