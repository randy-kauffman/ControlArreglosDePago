# Spike 3 de sincronización: resultados

Proyecto 2026-003 "Control de Arreglos de Pago". Fecha de ejecución: 2026-10-08 (19:29 a 19:55 UTC).

## Alcance y método

- Entorno: `https://org36e60d9d.crm.dynamics.com/` (comprobado con `pac org who`: Org "Dev", Environment ID `9c8994b8-b1bc-ee23-9184-2f6346502613`). No se tocó ningún otro entorno.
- Dataverse: credenciales de cliente del estudio leídas de `~/projects/pp_secrets.env` (`dv.py`, copia del helper del spike 2; se niega a correr si la URL no es la autorizada). La cuenta de servicio es `# PowerPlatform-Dev` (System Administrator).
- Correo: Microsoft Graph con `PP_MAIL_*` (`graph.py`). La biblioteca valida antes de cada envío que remitente y destinatarios estén entre `PP_MAIL_BUZON_1` a `4` y que el asunto lleve el prefijo `[SPIKE3]`; cualquier otra dirección aborta sin llamar a Graph. `PP_MAIL_BUZON_1` es `buzondevpp1@55xljh.onmicrosoft.com`, el buzón sincronizado (verificado). Ningún secreto ni token se imprimió ni se guardó.
- Regla de verificación (`herramientas/patrones.md`): ningún resultado se afirma por el código HTTP. Cada afirmación sale de releer el Email, los Queue Items, los adjuntos o el mensaje en el buzón destino por Graph.
- No se cambió ninguna configuración de la organización, de los mailboxes, de las queues ni del perfil de servidor. Lo único que se modificó fuera de datos de prueba: la tabla `spk_conversacion` de la solución `SpikesArreglosPago` pasó a tener actividades habilitadas (ver Preparación).
- Polling: cada 30 s (más fino que los 60 s pedidos) con un máximo de 25 minutos por caso. Ningún caso llegó cerca del máximo. Las demoras no dependen del intervalo porque se calculan con las marcas de tiempo de las propias filas (`createdon`, `senton`, `actualend`) y de Graph (`receivedDateTime`, `sentDateTime`), no con la hora en que el script miró.

## Hallazgo previo que condiciona la lectura de todo el spike

El entorno de desarrollo **no tiene una sola queue sobre el buzón 1, tiene dos**, y el buzón 2 también está sincronizado:

| Queue | Dirección | Dueño de la queue | Mailbox (entrada / salida) | Estado entrada / salida |
|---|---|---|---|---|
| `POC BAC – Cola Servicio` | buzón 1 | `# PowerPlatform-Dev` (service principal) | Server-Side Synchronization / Server-Side Synchronization | Success / Success |
| `Spike` (creada hoy 19:29) | buzón 1 | Randy Kauffman | Server-Side Synchronization / Server-Side Synchronization | Success / Success |
| `POC BAC – Cola Reclamos` | buzón 2 | `# PowerPlatform-Dev` | Server-Side Synchronization / Server-Side Synchronization | Success / Success |
| `POC BAC – Cola Consultas` | buzón 3 | `# PowerPlatform-Dev` | sin método, deshabilitado | Not Run / Not Run |

Las tres queues habilitadas tienen `incomingemailfilteringmethod` = All email messages (0), mailbox aprobado por el administrador de O365 y `enabledforincomingemail` / `enabledforoutgoingemail` en verdadero. Evidencia releída: `evidencia/preparacion_queues_mailboxes.json`. El spike no se detuvo porque las condiciones del encargo se cumplen (queue habilitada, éxito en entrada y salida), pero el doble registro tiene tres efectos que hay que tener presentes:

1. Cada correo que llega al buzón 1 produce **un solo Email** pero **dos Queue Items** (uno por queue, cada uno con el dueño de su queue) y dos participantes "Para" que apuntan a las dos queues. `receivingmailboxid` alterna entre `Spike` y `POC BAC – Cola Servicio` de un correo a otro.
2. El propietario del Email no se puede atribuir a una queue concreta (ver caso 1).
3. Todo lo que se envía al buzón 2 (el "cliente") se registra además como Email entrante en Dataverse por la queue `POC BAC – Cola Reclamos`. Eso generó Emails extra de prueba y, de rebote, evidencia adicional de correlación (casos 2 y 3).

Para los envíos desde Dataverse se usó la queue `Spike` como remitente (la creada hoy para este spike). Ambas comparten dirección, así que el remitente que ve el destinatario es el mismo.

## Preparación

- Queue y mailbox del buzón 1: ver tabla anterior. Filtro de entrada: "All email messages". Salida: sin errores (`outgoingemailstatus` Success; el primer correo saliente de prueba salió).
- Configuración de organización relevante (solo lectura, `organizations`): `emailcorrelationenabled = true`, `enablesmartmatching = null` (apagado), `trackingprefix = CRM:;`, `maxuploadfilesize = 5242880` (5 MB), `blockedmimetypes = null`, lista de extensiones bloqueadas estándar.
- `spk_conversacion` ya existía del spike 2 (3 filas de prueba, `HasActivities = false`). Se la habilitó para actividades con `PUT EntityDefinitions(...)` (`HasActivities = true`, 204; releído `true`; aparece la relación `spk_conversacion_Emails` con propiedad de navegación `regardingobjectid_spk_conversacion_email`). **La habilitación de actividades no se puede revertir**; la limpieza de la tabla completa (script del spike 2) la resuelve. Script `s01_tabla_actividades.py`.
- Relación Regarding Email -> `spk_conversacion`: `Assign`, `Delete`, `Reparent`, `Share`, `Unshare` = Cascade; `Archive`, `Merge`, `RollupView` = NoCascade.

## Tabla de veredictos

| # | Caso | Resultado | Evidencia releída | Demora medida | Impacto en el diseño |
|---|---|---|---|---|---|
| 1 | Correo nuevo de remitente desconocido (buzón 2 -> buzón 1) | **CONFIRMADO**, salvo el propietario (NO CONCLUYENTE por la doble queue) | Email `b01009db-4fc3-f111-a05a-000d3a35abca`: `statuscode` Received, `directioncode` entrante, `regardingobjectid` vacío, `correlationmethod` 0, `messageid` y `conversationindex` presentes, `inreplyto` vacío, `sender` = buzón 2, `actualend` = `receivedDateTime` de Graph. Queue Items: 2 (uno por queue), `statecode` 0, `workerid` vacío, dueño = dueño de su queue. Propietario del Email: `# PowerPlatform-Dev` en 6 de 6 correos, aunque `receivingmailboxid` alternó. Archivo `evidencia/caso1_inspeccion.json` | 7 s desde el envío (5 s desde `receivedDateTime`) | D-03: la fecha de llegada es `actualend`. D-31/ítem 1: el Email llega con Regarding vacío; el plugin crea la conversación. El propietario real por queue hay que repetirlo con una sola queue (ver "No probado") |
| 2 | Respuesta desde Dataverse en el hilo, queue como remitente, `SendEmail` | **CONFIRMADO** con una condición que **refuta** una parte del supuesto: el hilo solo se conserva si el Email se crea con `parentactivityid` | V1 (`6686a075-50c3-f111-a05a-000d3a35abca`, Regarding a la conversación `6386a075-50c3-f111-a05a-000d3a35abca`, `parentactivityid` = correo original): `SendEmail` HTTP 200, estado Sent, `senton` 19:43:23. En el buzón 2 por Graph: `From` y `sender` = `Buzon Dev PP 1 <buzondevpp1@...>` (iguales, sin "en nombre de"), `In-Reply-To` = Message-ID del original, `Thread-Index` con la misma raíz que el original. V2 (`6b86a075-...`, solo Regarding, sin parent): sale igual, pero **sin `In-Reply-To`** y con un `Thread-Index` nuevo, o sea un hilo nuevo. El asunto no recibió token de seguimiento (`TrackingToken` vacío). El `messageid` del Email saliente se completa tras el envío y coincide con el `Message-ID` que vio Graph | `SendEmail` -> Sent (`senton`): 14 a 18 s. `SendEmail` -> llegada al buzón 2: 17 a 21 s | D-05: la respuesta debe crearse con el correo al que responde como `parentactivityid` (campo solo de creación, no editable después; `inreplyto` es de solo lectura). Hay que comprobar en la app que el botón Responder del timeline lo completa; si no, plan B: plugin pre-create sobre Email saliente con Regarding a una conversación que fije `parentactivityid` = último correo entrante. D-28: el remitente llega limpio. Queda pendiente el oficial no administrador (ver "No probado") |
| 3 | El cliente responde en el mismo hilo (Graph `createReply`) | **CONFIRMADO** (supuesto central de D-32) | Email `140a9dc5-50c3-f111-a05a-000d3a35abca`: `regardingobjectid` = `6386a075-...` (la conversación) sin intervención de nadie, `correlationmethod` = 3 (InReplyTo), `parentactivityid` y `correlatedactivityid` = `6686a075-...` (el Email saliente de Dataverse), `inreplyto` = Message-ID de V1. Evidencia adicional no planeada: la copia de V1 recibida en el buzón 2 (`76501782-...`, registrada por la queue del buzón 2) quedó con Regarding a la conversación, `correlationmethod` InReplyTo y `parentactivityid` = el correo original | 6 s desde el envío (3 s desde `receivedDateTime`) | D-32 y supuesto de arquitectura (confianza media): sube a alta para clientes que responden con Exchange Online o Outlook web. El plan B (búsqueda por `inreplyto` en el plugin) se mantiene como red de seguridad barata |
| 4 | Respuesta con el asunto cambiado | **CONFIRMADO** para respuestas generadas por Graph/Exchange; **NO PROBADO** para Outlook de escritorio | Email `2a0a9dc5-50c3-f111-a05a-000d3a35abca`, asunto "[SPIKE3] Caso 4 asunto distinto 133831": `regardingobjectid` = `6386a075-...`, `correlationmethod` 3, `correlatedsubjectchanged` = true, `inreplyto` conservado. El `conversationindex` cambió (Exchange abre un hilo nuevo al cambiar el asunto), así que la correlación dependió solo de `In-Reply-To` | 7 s | La sección de Integraciones asume que "algunos clientes quitan el encabezado". Con Graph/OWA eso no ocurrió. Microsoft Learn es contradictoria sobre Outlook de escritorio (una página dice que lo conserva; la FAQ de Dynamics 365 App for Outlook dice que lo quita por diseño). Como Graph no permite fabricar un correo sin `In-Reply-To`, ese caso no se pudo simular. La unión manual (RF-14) sigue siendo necesaria; no se cierra el riesgo |
| 5 | Correo suelto en hilo nuevo del mismo remitente, mismo asunto exacto que el caso 1 | **CONFIRMADO** (no correlaciona) | Email `11853bfa-50c3-f111-a05a-000d3a35abca`, asunto idéntico al del caso 1, mismo remitente y destinatario, enviado cuando el caso 1 ya estaba registrado con Regarding: `regardingobjectid` vacío, `correlationmethod` 0, `parentactivityid` vacío, `inreplyto` vacío, `conversationindex` distinto | 4 s | D-32: un correo en hilo nuevo abre conversación nueva, como asume el diseño. Condición: smart matching apagado (`enablesmartmatching` null) y sin token de seguimiento en el asunto; si alguien activa smart matching en la organización, esto deja de valer |
| 6 | Adjuntos de entrada (buzón 3 -> buzón 1): PDF + PNG, e imagen inline `cid` | **CONFIRMADO** | 6A (`72bd9ae9-...`): `attachmentcount` 2; dos `activitymimeattachment` (`comprobante.pdf` 592 B `application/pdf`, `foto.png` 79 B `image/png`), con `objecttypecode` email y `attachmentnumber` 1 y 2; el SHA-256 del `body` leído coincide con los bytes enviados (`s13_cuerpo_adjuntos.py`). 6B (`8cbd9ae9-...`): `attachmentcount` 1; un `activitymimeattachment` (`logo.png`, 79 B) con `attachmentcontentid` = `logo1`; el cuerpo conserva `<img src="cid:logo1" data-attachment-id="<id del activitymimeattachment>">`. La tabla `msdyn_richtextfile` no recibió ninguna fila ese día | 34 s (6A y 6B, enviados juntos) | D-34: los adjuntos e imágenes inline **entrantes** son `activitymimeattachment`, no `msdyn_richtextfile`. La purga de lo que llega por correo se resuelve con `activitymimeattachment` (más el blob, ver caso 8). `msdyn_richtextfile` solo aplica a imágenes pegadas por el oficial en el editor del timeline, cosa que este spike no pudo probar (necesita interfaz) |
| 7 | Adjunto de salida (PDF + PNG vía `activitymimeattachment`) | **CONFIRMADO** | Email `d6263e26-51c3-f111-a05a-000d3a35abca`: antes de enviar `attachmentcount` 2; tras `SendEmail` estado Sent. Por Graph en el buzón 2: dos adjuntos (`salida.pdf` application/pdf, `salida.png` image/png, `isInline` falso) con SHA-256 idéntico al enviado. Para crearlos: `POST activitymimeattachments` con `objectid_email@odata.bind`, `objecttypecode` "email", `filename`, `mimetype`, `body` en Base64 | `SendEmail` -> Sent 14 s; -> llegada 16 s | D-34 y RNF-11: la salida con adjuntos funciona con el límite de 5 MB de la organización (no se probó el límite) |
| 8a | Borrado en cascada: borrar un Email con adjuntos | **CONFIRMADO** | Email 6A con 2 `activitymimeattachment`, 2 filas `attachment` (blob) y 2 Queue Items. `DELETE emails(...)` 204. Releído con estado exacto: Email, 2 `activitymimeattachment`, 2 Queue Items y 2 filas `attachment` ya no existen. Repetido con un borrador propio (R1): ídem | inmediato | La purga de un Email limpia todo lo suyo |
| 8b | Borrado en cascada: borrar la conversación (Regarding, Delete = Cascade) | **REFUTADO en parte**: se van Email, adjuntos y Queue Items, pero **queda huérfana la fila `attachment` (el blob)** | Email 6B (1 adjunto inline, 2 Queue Items) reasignado a una conversación descartable y luego `DELETE spk_conversacions(...)` 204: Email, `activitymimeattachment` y Queue Items desaparecieron; la fila `attachment` `99bd9ae9-4fc3-f111-a05a-000d3a35abca` sigue existiendo. Reproducido con un borrador propio (R2, `attachment` `ef241299-51c3-f111-a05a-000d3a35abca`) frente a R1 borrado directo, que sí limpió el blob. Persistía al menos 10 minutos después (ver Seguimiento). El entorno tiene un trabajo diario "WFE - Borrar archivos adjuntos huerfanos" que pertenece a otro proyecto: no se puede contar con él en producción | n/a | RNF-03 y D-34: la purga de datos Sensible no puede borrar la conversación y confiar en la cascada. Debe borrar **primero los Emails** (uno por uno) y después la conversación. Hay que decidir si se agrega un trabajo de limpieza de `attachment` huérfanos. Ver también la cascada de `Assign` y `Share` de esta misma relación |

## Demoras (RNF-08)

| Tramo | Medidas | Valor |
|---|---|---|
| Envío por Graph -> Email en Dataverse (`createdon`), un correo | caso 1, 3, 4, 5 | 7, 6, 7 y 4 s |
| Idem, tres correos enviados casi a la vez (uno con dos adjuntos, uno inline) | caso 6A y 6B | 34 s en ambos; el caso 1 del mismo lote tardó 7 s |
| `receivedDateTime` de Graph -> `createdon` | los 6 entrantes | 2 a 5 s, y 28 y 29 s en el lote de tres |
| `SendEmail` -> estado Sent (`senton`) | V1, V2, caso 7 | 18, 14 y 14 s |
| `SendEmail` -> llegada al destino (`receivedDateTime` en buzón 2) | V1, V2, caso 7 | 21, 17 y 16 s |

Todas las demoras de entrada fueron de segundos, no de minutos: no se observó el ciclo de espera de 5 minutos. El entorno estaba ocioso, con un buzón activo por vez y volumen casi nulo, así que **no se debe extrapolar a producción**. RNF-08 (5 a 15 minutos) no se refuta ni se estrecha con estos datos: es un piso de comportamiento bajo carga ligera, no una medición del peor caso.

## Qué columnas del Email usar (plugin del ítem 1)

| Necesidad | Columna | Comentario (releído) |
|---|---|---|
| Fecha de llegada (D-03) | `actualend` | Igual al `receivedDateTime` de Exchange en 6 de 6 correos. `createdon` llega entre 2 y 29 s después; no usarlo. `senton` es **nulo** en los entrantes y `sortdate` también |
| Fecha de envío de una respuesta (D-05) | `senton` | En los salientes es la hora real de envío (coincide con el `sentDateTime` de Graph con 1 s de tolerancia). `actualend` en los salientes se fija en el momento de la llamada a `SendEmail` (0 a 2 s de diferencia) y no es la hora de salida |
| Message-ID | `messageid` | Con los `<` `>`, tal como lo emite Exchange. En los salientes de Dataverse el dominio está en mayúsculas (`@BUZONDEVPP1.55XLJH.ONMICROSOFT.COM`) y se completa al enviar. Normalizar (quitar espacios, minúsculas) antes de calcular la huella SHA-256 de D-03. El mismo Message-ID puede aparecer en dos Emails (el saliente y la copia que registra la queue de otro buzón sincronizado); la clave alterna de la conversación lo absorbe |
| In-Reply-To | `inreplyto` | Solo lectura (no se puede escribir en creación). Vacío en correos nuevos |
| Hilo de Exchange | `conversationindex` (hex) y `baseconversationindexhash` | Cambia si el cliente cambia el asunto; no sirve como respaldo de correlación |
| Cómo se correlacionó | `correlationmethod` (3 = InReplyTo, 0 = ninguno), `parentactivityid`, `correlatedactivityid`, `correlatedsubjectchanged` | Con correlación por `In-Reply-To` el Email ya llega con `regardingobjectid` = el de su correo padre y `parentactivityid` = ese correo. No se pudo distinguir si el Regarding está en el Target del create o se escribe en un update posterior (`modifiedon` es 0 a 1 s mayor que `createdon` en todos); el plugin es asíncrono, así que debe **releer el Email por id** y no confiar en el Target |
| Remitente | `sender` (dirección en texto), `submittedby` (nombre y dirección) | `_emailsender_value` apunta a una queue si la dirección del remitente pertenece a una; en producción el cliente no será una queue |
| Buzón receptor | `receivingmailboxid`, `acceptingentityid` | Alternan entre las dos queues del buzón 1 por la doble configuración |
| Cuerpo | `description` (HTML) y `safedescription` | |
| Adjuntos | `attachmentcount` y `activitymimeattachment` (`objectid` = el Email) | `attachmentcontentid` identifica la imagen inline |

## Impacto en el diseño (decisiones y supuestos)

| Decisión o supuesto | Efecto del spike |
|---|---|
| D-02 (Queue con mailbox, filtro All email messages) | Funciona como se diseñó. Con la queue `Spike` y la de POC sobre el mismo buzón hay un solo Email y dos Queue Items; en producción debe haber una sola queue por buzón |
| D-03 (fecha del primer correo tomada del mensaje) | Se cierra: usar `actualend`. Huella SHA-256 del Message-ID normalizado |
| D-05 (responder con la queue como remitente, registrar tiempos) | Sale y llega limpio; tiempo de respuesta con `senton`. **Cambia**: crear la respuesta con `parentactivityid`; sin él el hilo se rompe. Verificar el botón Responder; plan B con plugin pre-create |
| D-28 (privilegio de envío en nombre de otro + Send As de la identidad de sincronización) | Parcial. Con administrador funciona y el remitente llega sin "en nombre de". Según Microsoft Learn, el oficial necesita el privilegio "Send Email as Another User" con profundidad suficiente y acceso de lectura a la queue (el consentimiento del usuario aplica a usuarios, no a queues). No se pudo distinguir si el envío funcionó por Send As de la identidad de sincronización o por la aprobación del administrador de O365 del mailbox (`isemailaddressapprovedbyo365admin = true`) |
| D-31 (propietario por plugin) | La relación Regarding tiene `Assign` = Cascade y `Share` = Cascade: reasignar o compartir la conversación se propaga a todos sus Emails. Fijar el propietario del Email sigue siendo tarea del plugin del ítem 1 al registrarlo |
| D-32 (agrupación por `In-Reply-To`) | Confirmado el supuesto central con Exchange Online. Falta Outlook de escritorio |
| D-34 (adjuntos e imágenes) | Entrantes: `activitymimeattachment`, no `msdyn_richtextfile`. La visualización de la imagen inline en el timeline se resuelve por `cid` contra el adjunto (no probada en pantalla). El blob `attachment` queda huérfano al borrar por conversación |
| RNF-03 / purga | Borrar Emails antes que la conversación; evaluar un trabajo de limpieza de `attachment` huérfanos |
| RNF-08 | Demoras de segundos en un entorno ocioso. Sin cambio de la cifra aceptada |
| Supuesto de arquitectura sobre correlación (confianza media) | Sube a alta para Exchange Online / OWA; media para Outlook de escritorio |

## No probado (queda pendiente, no se simuló)

- **Oficial con rol no administrador** enviando con la queue como remitente: privilegio "Send Email as Another User", acceso de lectura a la queue y, si aplica, el permiso de Exchange. Todo lo anterior se probó con el service principal (System Administrator).
- **Propietario real del Email con una sola queue.** Con la doble queue siempre quedó el service principal. Para cerrarlo hay que repetir el caso 1 con una sola queue sobre el buzón 1 (no se tocó la configuración).
- **Cliente con Outlook de escritorio** que cambia el asunto: Graph no permite fabricar un correo sin `In-Reply-To`.
- **Botón Responder del timeline** (¿completa `parentactivityid`?) y **editor de correo del timeline** (imágenes en `msdyn_richtextfile`): requieren la interfaz.
- Límite de 5 MB (`maxuploadfilesize`) y extensiones bloqueadas: no se envió un adjunto grande ni uno bloqueado.
- Comportamiento bajo volumen (ciclo de 5 minutos, correos repetidos, reintentos).
- Si el Regarding por correlación ya está presente en el Target del create del Email (afecta si un plugin síncrono podría usarlo).

## Seguimiento pendiente

- La fila `attachment` huérfana del caso 6B (`99bd9ae9-4fc3-f111-a05a-000d3a35abca`) y la de R2 (`ef241299-51c3-f111-a05a-000d3a35abca`) se dejaron a propósito. Releer pasadas las 09:00 UTC del 2026-10-09 (hora a la que corre el trabajo del otro proyecto) para saber si el trabajo de WFE las limpia, con la consulta `GET attachments?$select=attachmentid&$filter=attachmentid eq <id>`. El resultado del polling de 25 minutos está en `huerfanos.log`.

## Tropiezos de método (para quien repita el spike)

- `attachment` no admite `Retrieve` por clave (HTTP 400); para saber si existe hay que consultar con `$filter=attachmentid eq <id>`. Una primera comprobación que trataba cualquier respuesta distinta de 404 como "existe" dio un falso positivo con ese 400 y se corrigió: el script ahora aborta ante cualquier estado que no sea 200 o 404.
- `$filter=contains(subject,'[SPIKE3]')` en `emails` devolvió filas sin ese texto; el filtrado se hace en el script.
- Graph `sendMail` no devuelve el Message-ID. Se crea un borrador, se lee su `internetMessageId` y después se lo envía, para poder emparejar con Dataverse sin depender del asunto.

## Inventario de lo creado y limpieza

Dentro de la solución `SpikesArreglosPago` (no administrada): la relación `spk_conversacion_Emails` y la habilitación de actividades de `spk_conversacion`. Datos de prueba (no son componentes de solución):

- 10 Emails con "[SPIKE3]" en el asunto, entre ellos los registrados por la queue del buzón 2, con sus Queue Items y adjuntos. Ids en `estado.json` y `evidencia/`.
- 1 fila `spk_conversacion` "[SPIKE3] conversacion 133831" (`6386a075-50c3-f111-a05a-000d3a35abca`).
- 2 filas huérfanas de `attachment` (ver Seguimiento).
- Correos en los buzones de Exchange 1 a 3 (pueden quedar).

Limpieza:

1. `python3 s99_limpieza.py` hace un ensayo en seco y lista lo que borraría. Con `--si-borrar` borra primero los Emails con "[SPIKE3]" creados desde las 19:30 UTC del 2026-10-08 y después las conversaciones "[SPIKE3]" (en ese orden, por el caso 8b). No toca el Email "Your mailbox is now connected to Dynamics 365" ni las filas del spike 2.
2. La tabla `spk_conversacion` y el resto de la solución se limpian con `../plataforma/s99_limpieza.py --si-borrar` (spike 2).

## Archivos

Scripts en esta carpeta, sin secretos: `dv.py` y `graph.py` (clientes), `lib.py`, `s00` y `s05` (queues y mailboxes), `s01` (actividades en `spk_conversacion`), `s10` a `s13` (entrada, casos 1 y 6), `s20` y `s21` (caso 2), `s30` (casos 3 y 4), `s40` (caso 5), `s50` (caso 7), `s60`, `s61` y `s62` (caso 8), `s90` (listado), `s99` (limpieza). Evidencia releída en `evidencia/*.json` y estado de la corrida en `estado.json`.
