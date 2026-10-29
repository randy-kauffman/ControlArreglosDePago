# 08. Alternativa: captura y envío de correo con flujos de Power Automate en lugar de la sincronización del lado del servidor

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **análisis del arquitecto (2026-10-28), sin construir**. Pregunta del usuario: no se sabe si BAC Credomatic va a permitir o probar la sincronización del lado del servidor (SSS) del buzón del área en Dataverse. ¿Se puede replicar con flujos la operativa que hoy hace SSS, de forma que para el Oficial y el Jefe sea **transparente**?

Fuentes del proyecto: `diseno/03` (S-31, S-34 y su `RegistradorDeCorreo`, S-35, S-37, reconciliación 1.2), `diseno/04` (privilegios de Email y Queue), `diseno/07` §8 (flujos EST y SAL), `pruebas-humo/tanda3/RESULTADO.md` (H-25 a H-28), `spikes/dobles/RESULTADO.md` (8). Lo marcado **[verificar en Dev]** se apoya en documentación o en inferencia y no está medido.

Lectura hecha en Dev el 2026-10-28 (solo lectura, metadatos):

| Columna de `email` | IsValidForCreate | IsValidForUpdate |
|---|---|---|
| `messageid`, `directioncode`, `sender`, `torecipients`, `trackingtoken`, `baseconversationindexhash` | sí | sí |
| `inreplyto`, `conversationindex`, `correlationmethod` | **no** | **no** |

`mailbox.outgoingemaildeliverymethod`: 0 None, 1 Microsoft Dynamics 365 for Outlook, 2 Server-Side Synchronization. `incomingemaildeliverymethod`: además 3 Forward Mailbox. `email.statuscode`: 1 Draft, 2 Completed, 3 Sent, 4 Received, 5 Canceled, 6 Pending Send, 7 Sending, 8 Failed.

## 1. Respuesta corta

**Sí, con condiciones.** La parte que ven los usuarios (conversaciones, línea de tiempo, botón Responder, estados de envío, vistas, panel) puede quedar igual, porque los plugins se disparan sobre la fila `email` y no sobre quién la creó. Las condiciones:

1. **TI de BAC igual tiene que dar permisos en Exchange**: Full Access y Send As del buzón compartido a la cuenta de servicio. No se evita a Exchange; se evita la aprobación del buzón en Power Platform y la confianza servidor a servidor de SSS.
2. **El flujo tiene que hacer a mano lo que SSS hace solo**: correlacionar la respuesta del cliente con su conversación (In-Reply-To) y grabar el `messageid` real de cada respuesta enviada. Si no lo hace bien, la respuesta del cliente abre una conversación nueva, y eso **sí se nota**.
3. **La cuenta de servicio necesita licencia Premium** (el conector de Dataverse es premium) y buzón con licencia de Exchange para la conexión de Outlook.
4. **Esfuerzo nuevo de unas 12 a 14 jornadas** (sección 8), contra una entrega fijada para el 2026-10-31 (RNF-09). Hay que hablarlo antes de empezar.

## 2. Qué hace hoy SSS y qué asumen nuestros plugins

| Hoy (medido en la tanda 3) | Quién lo hace | Con flujos |
|---|---|---|
| Lee el buzón cada ~5 min y crea el `email` entrante (H-25: llega en ~31 s en la prueba) | SSS (`# SSSAdminProd`) | Flujo de entrada |
| El entrante nace con dueño = team de la queue y BU del área (H-26). S-34 lo exige: si `owningbusinessunit` no es la del área, `CARP-ENT-004` | SSS | El flujo pone `ownerid` = team del área en el `Create` |
| Correlación: la respuesta del cliente ya trae Regarding a la conversación (`correlationmethod` 3, In-Reply-To) y S-34 entra por la **ruta A** (H-25) | SSS | **El flujo** busca el `email` cuyo `messageid` es el In-Reply-To y pone el Regarding. `inreplyto` no se puede escribir por API (tabla de arriba y `spikes/dobles` (8)), así que la ruta B de S-34 queda sin uso |
| Entrada a la queue (queueitem) | SSS | `AddToQueue` desde el flujo |
| Adjuntos y cuerpo HTML | SSS | `activitymimeattachment` por adjunto, con `attachmentcontentid` para imágenes en línea **[verificar en Dev]** |
| Remitente = queue en la respuesta (S-31 al crear, S-37 al modificar) | plugins | **Sin cambio** |
| `SendEmail` deja el correo en Pending Send; SSS lo toma cada ~5 min y lo pasa a Sent | SSS | Flujo de salida que toma los Pending Send, envía por Exchange y pasa a Sent |
| S-35 al pasar a Sent: `sanic_respondidoporid` = `createdby` y `sanic_tiemporespuesta` | plugin, filtro `statuscode` | **Sin cambio**, si el flujo escribe `statuscode` 3 |
| Falla de envío: `statuscode` 8, vista "Respuestas con error de envío" (filtro `statuscode` 8) | SSS | El flujo pone 8 |
| Alertas del buzón (flujo EST) y salud (CARP-SAL) | SSS + flujos | EST deja de aplicar (no hay mailbox SSS). SAL cambia de fuente: corridas fallidas de los flujos y diferencia buzón contra Dataverse |

Fuente del ciclo de SSS: [Server-side synchronization frequency](https://learn.microsoft.com/power-platform/admin/server-side-synchronization#server-side-synchronization-frequency) (entrada cada 5 min después de cada ciclo; salida: "scans your environment for outgoing email messages that are in a Pending Send status and have been sent by using the Send SDK request ... every five minutes").

## 3. Entrada: flujo `Cloud Flow - CARP - ENT - Capturar correo entrante` (propuesto)

1. **Disparador:** Office 365 Outlook, *When a new email arrives in a shared mailbox (V2)*, con **Include Attachments = Yes** (por defecto es No y el contenido llega nulo: [Issues triggering emails with attachments from shared mailbox](https://learn.microsoft.com/troubleshoot/power-platform/power-automate/flow-run-issues/issues-triggering-emails-with-attachments-from-shared-mailbox)). Solo dispara con correo nuevo en la carpeta, no al mover entre carpetas ([Troubleshoot common issues with email in flows](https://learn.microsoft.com/power-automate/email-troubleshooting#frequently-asked-questions)). Límite del conector: 300 llamadas cada 60 s por conexión (misma página). La latencia del disparador no está documentada con un número: **[verificar en Dev]**, se mide contra los ~31 s de SSS.
   - Limitación conocida: los adjuntos `.eml`, `.msg` e `.ics` no vienen en la salida del disparador ([Q&A con reproducción de Microsoft](https://learn.microsoft.com/answers/a/2013015)). SSS sí los trae. Es una diferencia visible si los clientes reenvían correos.
2. **Encabezados:** la salida del disparador trae el Internet Message-Id, pero **no** el In-Reply-To. Se lee con la acción *Send an HTTP request* del mismo conector contra Graph: `GET /users/{buzón}/messages/{id}?$select=internetMessageId,internetMessageHeaders,conversationId` **[verificar en Dev]**.
3. **Idempotencia:** antes de crear, se busca un `email` con `messageid` = Internet Message-Id. Si existe, termina sin hacer nada. Así un redisparo o un reintento no duplica (SSS hace lo mismo por `messageid`).
4. **Correlación (lo que hacía SSS):** si hay In-Reply-To, se busca el `email` con `messageid` en las tres variantes de DC-08 (tal cual, con `<>` y sin ellos) y Regarding a `sanic_carp_tbl_conversacion`; se elige el más antiguo, igual que `ICorreos.BuscarPorMessageId`. Si aparece, el `Create` lleva ese Regarding y S-34 entra por la ruta A. Si no, va sin Regarding y S-34 crea la conversación por la ruta C, como hoy con un correo nuevo.
5. **`Create` del `email`:**
   - `directioncode` = false;
   - `subject`, `description` (HTML), `sender`, `torecipients`, `messageid`;
   - partes: `from` con `addressused` (y `partyid` si el remitente es un Cliente o un contacto: S-34 no lo necesita, usa `sender`), `to` = la queue del área, `cc`;
   - `ownerid` = team del sistema del área (lo exige S-34 por BU, H-26);
   - opcional: `receivingmailboxid`.
6. **Adjuntos:** un `activitymimeattachment` por adjunto (`objectid` = el email, `body` en base64, `filename`, `mimetype`; para imágenes en línea, `attachmentcontentid` con el `cid` del HTML) **[verificar en Dev]** que la línea de tiempo las muestre. El tope es `Organization.MaxUploadFileSize`, 5 MB por defecto ([Email activity tables](https://learn.microsoft.com/power-apps/developer/data-platform/email-activity-entities#email-attachments)). SSS aplica el mismo tope, así que no cambia. Un adjunto que no entra se registra en el detalle de la corrida y se avisa.
7. **Cerrar como recibido:** `Update` a `statecode` 1, `statuscode` 4 (Received). H-27 ya probó que S-34 y SYSTEM escriben sin problema sobre un Email cerrado.
8. **Queue:** `AddToQueue` sobre la queue del área ([Use Web API actions](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/use-web-api-actions#bound-actions)), para que el queueitem exista como con SSS. Ninguna vista de la app depende de él hoy, pero mantiene la paridad.
9. **Marca en Outlook:** se pone una categoría `CARP-registrado` al mensaje. La reconciliación del flujo SAL compara los mensajes sin categoría de las últimas 24 h contra Dataverse.

**Alternativa nativa a probar en la entrada:** la acción `DeliverIncomingEmail`, ligada a la colección `emails`, crea el email "desde un mensaje entrante" con `MessageId`, `From`, `To`, `Cc`, `ReceivedOn`, `Body` y `Attachments` ([DeliverIncomingEmail](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/reference/deliverincomingemail)). **No tiene parámetro In-Reply-To** (`ExtraProperties` es "for internal use only"), así que no se sabe si correlaciona como SSS. Vale una prueba en Dev (V-3). Si correlaciona sola, se ahorra el paso 4. Si no, queda el `Create` explícito.

**Qué pasa con nuestros plugins en la entrada:**
- **S-34:** sin cambio de código. Corre asíncrono sobre el `Create`, con cualquier creador. Lee `owningbusinessunit`, que el flujo fija por el dueño. La ruta B (variantes de `inreplyto`) nunca va a encontrar nada, porque la columna queda vacía; no rompe, solo no se usa.
- **S-32:** la cuenta de servicio es "persona" según DC-02. El flujo no escribe `sanic_tiemporespuesta` ni `sanic_respondidoporid`, así que no salta `CARP-EML-003`.
- **S-31:** no aplica (no es saliente).
- **Reconciliación 1.2:** sin cambio. Sigue buscando correos de la BU sin conversación.

## 4. Salida: flujo `Cloud Flow - CARP - SND - Enviar respuestas` (propuesto)

El Oficial sigue usando **Responder → Enviar** en la línea de tiempo. S-31 y S-37 ponen la queue como remitente, sin cambios.

1. **Qué hace `SendEmail` sin SSS de salida:** Learn dice que el correo queda en **Pending Send** y no se envía mientras no haya un medio de envío configurado ([Why does the email message I sent have a Pending Send status?](https://learn.microsoft.com/power-platform/admin/why-email-message-sent-have-pending-send-status)). Ese estado es justo el que toma el flujo. Hay que comprobar dos cosas antes de construir **(V-1, [verificar en Dev])**:
   - que con el mailbox de la queue en `outgoingemaildeliverymethod` = 0 (None), `SendEmail` acepte el correo y lo deje en 6 en vez de rechazarlo;
   - que la regla de aprobación `requireapprovalforqueueemail` (hoy true en Dev) no lo bloquee.
2. **Disparador:** Dataverse, *When a row is added, modified or deleted*, tabla `email`, Modificar, columnas `statuscode`, filtro `statuscode eq 6 and directioncode eq true`. Después, una condición: el Regarding es una conversación y el remitente es la queue del área. **Concurrencia del disparador = 1**, para que dos corridas no tomen el mismo correo.
3. **Bloqueo:** primero se pasa el correo a `statuscode` 7 (Sending). Una segunda corrida ya no cumple el filtro.
4. **Envío conservando el hilo:** con Graph, por *Send an HTTP request* del conector de Outlook **[verificar en Dev]**:
   - `POST /users/{buzón}/messages/{idOriginal}/createReply` con el cuerpo, los destinatarios y los adjuntos del `email` de Dataverse;
   - `GET` del borrador para leer su `internetMessageId`;
   - `POST .../send`.

   Exchange pone el In-Reply-To y las References. `idOriginal` es el id de Outlook del correo del cliente: hay que guardarlo en la entrada. Hace falta una columna nueva en `email` (`sanic_idoutlook`, T(200)) o buscarlo por `internetMessageId`. La acción *Reply to email (V3)* conserva el hilo, pero no devuelve el `internetMessageId`. *Send an email from a shared mailbox (V2)* ([Create flows for popular email scenarios](https://learn.microsoft.com/power-automate/email-top-scenarios#send-an-email-from-a-distribution-list-or-shared-mailbox)) no conserva el hilo ni devuelve el id. Por eso las dos quedan descartadas.
5. **Cierre:** `Update` del `email` a `statecode` 1, `statuscode` 3 (Sent) y `messageid` = el `internetMessageId` real. **Ese `messageid` es lo que permite que la respuesta del cliente vuelva a la misma conversación (paso 4 de la entrada).** `senton` lo pone el servidor (`spikes/dobles` (8)). S-35 despierta por `statuscode` y calcula los tiempos, como hoy.
6. **Error:** se reintenta 3 veces con espera. Si sigue fallando, `statuscode` 8 (Failed) y el correo aparece en "Respuestas con error de envío", como con SSS. SAL lo cuenta.
7. **Adjuntos y cuerpo:** los `activitymimeattachment` del correo se mandan como adjuntos. Las imágenes pegadas en el editor son adjuntos con `attachmentcontentid` y van como adjuntos en línea **[verificar en Dev]**. Es el punto con más riesgo de diferencia visible para el cliente final.

## 5. Permisos, licencias y límites

| Tema | SSS | Flujos |
|---|---|---|
| Exchange | Aprobar el buzón en Power Platform (administrador de Exchange o global) y Test & Enable; confianza S2S Dataverse ↔ Exchange ([Approve email](https://learn.microsoft.com/power-platform/admin/connect-exchange-online#approve-email)) | **Full Access y Send As** del buzón compartido a la cuenta de servicio ([Create a shared mailbox, permisos](https://learn.microsoft.com/microsoft-365/admin/email/create-a-shared-mailbox?view=o365-worldwide#which-permissions-should-you-use)). Full Access sola no deja enviar. Lo da el administrador de Exchange de BAC |
| Cuenta de servicio | Dueña de flujos de monitoreo (13.6) | Además, buzón con licencia de Exchange Online y **Power Automate Premium** o **Process** por flujo; Dataverse es premium ([Request limits](https://learn.microsoft.com/power-platform/admin/api-request-limits-allocations#request-limits-in-power-automate)) |
| Límites | Throughput de SSS, sin SLA | Premium: 40 000 solicitudes por usuario cada 24 h; Process: 250 000 por flujo (misma página). Estimado: ~25 acciones por correo entrante y ~15 por respuesta; 300 correos al día son ~12 000, entra en Premium |
| DLP | — | Office 365 Outlook y Dataverse en el mismo grupo (Business) de la política del entorno |
| Acceso condicional | — | Si BAC limita Graph o los conectores por IP o dispositivo, la conexión de Outlook puede fallar ([Known issues for connectors](https://learn.microsoft.com/connectors/common/known-issues)) |
| Rol `SR - CARP - Servicio` | — | Le faltan Create, Write y Append de Email en la BU (hoy los tiene el Oficial en B, `04` líneas 87 a 91), `prvAppendToQueue`, AddToQueue y `prvAssign` para dar el email al team. Es un **cambio de rol**, lo decide el usuario. D-10 (Callback Registration) aplica también al disparador de Dataverse del flujo de salida |

## 6. Lo que no se replica igual (diferencias visibles o de operación)

1. **Correlación de respuestas.** SSS correlaciona por In-Reply-To, por token de seguimiento y por conversación de Exchange. El flujo solo por In-Reply-To contra `messageid`. Si un cliente responde desde un cliente de correo que no manda In-Reply-To, o si el `messageid` saliente no se grabó (paso 4.5 fallido), se abre una conversación nueva. El aviso de otra conversación abierta y el gesto Unir lo mitigan, pero el usuario lo nota.
2. **Adjuntos `.msg`, `.eml` e `.ics`** no llegan por el disparador (sección 3, punto 1). Habría que bajarlos por Graph (`$value`), con más trabajo.
3. **Imágenes en línea y formato** del cuerpo, de entrada y de salida, dependen del armado del flujo y hay que probarlas caso por caso.
4. **Latencia** de entrada y salida distinta a la de SSS. Probablemente parecida o mejor, pero sin SLA documentado en ninguno de los dos.
5. **Monitoreo y soporte.** Desaparecen el muro de alertas del mailbox, el panel *Server-Side Synchronization Item Level Monitoring* y el flujo EST. Las fallas pasan a ser corridas fallidas de flujos, que SAL tiene que vigilar. Soporte de Microsoft acompaña SSS como función del producto; un flujo propio lo mantiene el equipo.
6. **Ventaja:** no hay "primera sincronización" que importe toda la bandeja (hallazgo de la tanda 3). El flujo arranca con el correo nuevo.

## 7. Comparación

| Criterio | SSS (hoy, probado) | Flujos (propuesto) |
|---|---|---|
| Lo que ve el usuario | Probado, 73/73 en la tanda 3 | Igual si se cumplen V-1 a V-4; diferencias de la sección 6 |
| Plugins | Sin cambios | Sin cambios de código (S-31, S-34, S-35, S-37, reconciliación) |
| Exigencia a TI de BAC | Aprobar el buzón y confiar en la sincronización de Dataverse | Full Access y Send As a una cuenta de servicio, Graph habilitado y DLP |
| Licencias | Las de los usuarios | Más: Premium o Process para la cuenta de servicio |
| Construcción nueva | 0 | 2 flujos, 1 columna, cambio de rol, playbooks y humo (~12 a 14 jornadas) |
| Riesgo técnico | Bajo (probado) | Medio: correlación, adjuntos y bloqueo de concurrencia |
| Operación | Microsoft mantiene la sincronización | El equipo mantiene dos flujos críticos |
| Reversibilidad | — | Alta: los plugins no cambian. Pasar de uno a otro es cambiar el mailbox y encender o apagar los flujos. **Nunca los dos sobre el mismo buzón**, porque duplican la entrada |

## 8. Recomendación

1. **Seguir con SSS como camino principal** y pedirle a TI de BAC la respuesta ya. Es lo construido y probado. Para TI son dos operaciones en el admin center (aprobar y Test & Enable) más el acceso restringido del buzón, que ya está en el procedimiento de instalación.
2. **Tener los flujos como plan B diseñado**, no construido, hasta que BAC responda. Si BAC dice que no, hacer primero las verificaciones V-1 a V-4 en Dev (≈2 jornadas). Recién con eso confirmado, construir.
3. Si se construye, **hacerlo sin tocar los plugins**: todo lo nuevo vive en dos flujos, una columna y el cambio de rol de Servicio. Por eso el riesgo para lo que ya funciona es bajo.

Esfuerzo estimado del plan B:

| Lote | Contenido | Jornadas |
|---|---|---|
| F0 | Verificaciones V-1 a V-4 en Dev | 2 |
| F1 | Flujo ENT con playbook (idempotencia, correlación, adjuntos, queue, categoría) | 3 |
| F2 | Columna `sanic_idoutlook`, flujo SND con playbook (bloqueo, Graph createReply/send, Sent con `messageid`, Failed) | 3,5 |
| F3 | Cambio de rol de Servicio (decisión del usuario), ajuste de SAL, retiro de EST para este modo, procedimiento de instalación | 1,5 |
| F4 | Humo "tanda 4": repetir H-5, H-6, H-25 a H-28 y S-31 con flujos | 2 |
| F5 | Manual y documentación | 0,5 |
| **Total** | | **12,5** |

Con la entrega fijada para el 2026-10-31 (RNF-09), el plan B no entra en el plazo actual. Si BAC rechaza SSS, hay que acordar una fecha nueva.

## 9. Verificaciones previas en Dev (antes de construir)

| # | Qué | Cómo, sin romper lo que funciona |
|---|---|---|
| V-1 | `SendEmail` desde una queue con mailbox `outgoingemaildeliverymethod` = 0 deja el correo en 6 (Pending Send) y no lo rechaza; efecto de `requireapprovalforqueueemail` | Queue de prueba **nueva** sobre el buzón de prueba 4, sin SSS, fuera del área (sin variable). Correo de prueba a un buzón 2 o 3. Nada sale, porque nadie lo envía |
| V-2 | Graph por *Send an HTTP request*: leer `internetMessageHeaders` de un entrante del buzón 4; `createReply` y `send` desde el buzón 4 y leer `internetMessageId` | Conexión de la cuenta de servicio con Full Access y Send As sobre el buzón 4. Solo correos entre los buzones de prueba 1 a 4 |
| V-3 | `DeliverIncomingEmail`: ¿correlaciona una respuesta como SSS? | Llamarla con un mensaje del buzón 4 que responde a uno enviado en V-2 |
| V-4 | Entrada completa por flujo con S-34 real: dueño, BU, ruta A, adjuntos e imágenes en línea | **Solo la entrada**, sobre el buzón 4 y con el dueño = team del área. S-34 lo procesa como del área. El buzón 1 con SSS queda intacto. La salida de prueba integral (H-5 con flujo) exige apagar SSS de salida del buzón 1 en una ventana acordada y volver a encenderlo, porque S-31 fija como remitente la queue del buzón 1. **Pide aprobación explícita del usuario** |

Regla para todas: correos solo entre los buzones de prueba 1 a 4, nunca el buzón de BAC, nada borrado.

## 10. Impacto en lo existente

- **No cambian:** el código de plugins y sus 5827 pruebas, las 5 Custom API, los 37 steps, la app (vistas, formularios, comandos y panel), el flujo SAL en su estructura, la plantilla y los datos.
- **Cambian si se construye el plan B:**
  - playbooks nuevos `playbooks/flujo/carp-ent.md` y `carp-snd.md`;
  - columna `sanic_idoutlook` en `email` (diccionario y playbook de tabla);
  - rol `SR - CARP - Servicio` (`04`);
  - SAL con dos comprobaciones nuevas (corridas fallidas de ENT y SND, y mensajes sin categoría);
  - EST no aplica en ese modo;
  - `manual/procedimiento-instalacion.md` con una sección "modo flujos" en vez de cola y buzón con SSS;
  - inventario: grupo 11 con dos filas nuevas.
- **Selección de modo por entorno:** una variable de entorno nueva (`sanic_carp_ev_modocorreo`: `sss` | `flujos`) deja apagados los flujos ENT y SND cuando el entorno usa SSS. Se instala la misma solución en los dos casos.
