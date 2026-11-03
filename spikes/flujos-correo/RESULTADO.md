# Spike F0 del plan B: correo por flujos de Power Automate (verificaciones V-1 a V-4 de `diseno/08`)

Proyecto 2026-003 · Dev (`org36e60d9d`) · iniciado el 2026-11-02 · **Estado: V-1 y V-3 terminadas; V-2 y V-4 preparadas y a la espera de que admin@ active tres flujos.**

Reglas respetadas: correo solo entre los buzones de prueba `PP_MAIL_BUZON_1` a `4` (hasta ahora salieron **0** correos reales: V-1 y V-3 no envían nada); datos ficticios con asunto `SPK-FC-`; nada borrado; el rol, las variables de entorno del área, la cola del área y el mailbox 1 **no se tocaron** (se comprobó al final: mailbox 1 sigue en SSS 2/2; la solución principal sigue con 158 componentes). Las direcciones no se escriben en el repositorio: se nombran por su variable.

## 1. Resumen

| V | Estado | Resultado en una línea |
|---|---|---|
| V-1 `SendEmail` con mailbox en None | **Hecha** | Acepta el correo (HTTP 200) y lo deja en 6 Pending Send; `requireapprovalforqueueemail` (true) no lo bloquea. Hallazgos que afinan el diseño: `senton` no es escribible y Failed exige `statecode` 0 |
| V-3 `DeliverIncomingEmail` | **Hecha** | **No** correlaciona sola (ni por asunto "RE:" ni por token). **Sí** correlaciona como SSS si se le pasa `ExtraProperties` con `inreplyto` (uso interno, no soportado). No fija dueño: S-34 no la procesaría sin una reasignación con carrera |
| V-2 Graph por *Send an HTTP request* | **Preparada, pendiente de activación** | Flujo `V2` creado apagado; plantilla validada por la plataforma |
| V-4 entrada completa con S-34 real | **Preparada, pendiente de activación** | Flujo `V4 (r3)` creado apagado; las formas de escritura de Dataverse que usa ya se ensayaron por API |
| V-4 salida integral (ventana SSS) | **Preparada, pendiente de activación y de la ventana** | Flujo `SND (r2)` creado apagado; scripts de ventana y de restauración listos, **no ejecutados** |

## 2. Componentes creados en Dev

| Componente | Nombre | Id |
|---|---|---|
| Solución sin gestionar (mismo publisher) | `sanic_carp_sol_spikecorreo` | `ff34766f-0ed7-f111-84e8-0022480c5699` |
| Cola de prueba sobre el buzón 4 (no es la del área) | `SPK - Correo por flujos (prueba, buzon 4)` | `d89c82fb-0cd7-f111-84e8-0022480c5699` |
| Su mailbox (entrega entrada y salida **None**) | (creado con la cola) | `da9c82fb-0cd7-f111-84e8-0022480c5699` |
| Variables de entorno (en la solución del spike, valor cargado desde memoria, sin direcciones en el repo) | `sanic_carp_ev_spkbuzon` (buzón 4), `..._spkteamarea` (team del área), `..._spkcola`, `..._spksalida` (buzón 1) | `0235766f…`, `8742ba76…`, `9142ba76…`, `18b8e210…` |
| Flujo V-2 | `Cloud Flow - CARP - SPK - V2 Encabezados y respuesta` | `a814fa16-0fd7-f111-84e8-0022480c5699` |
| Flujo V-4 entrada | `Cloud Flow - CARP - SPK - V4 Entrada completa (r3)` | `5d15370a-10d7-f111-84e8-0022480c5699` |
| Flujo V-4 salida | `Cloud Flow - CARP - SPK - SND Salida de respuestas (r2)` | `4c784710-10d7-f111-84e8-0022480c5699` |
| **Descartados, apagados, no activar** | `...V4 Entrada completa` (sin sufijo, plantilla inválida), `...V4 Entrada completa (r2)`, `...SND Salida de respuestas` (sin sufijo) | `b030291d…`, `e7578657…`, `c430291d…` |
| Otras filas de prueba (no se pueden borrar) | cuenta ficticia `SPK-FC Cuenta ficticia (V-3)`, correos `SPK-FC-*` de V-1, V-3 y V-4, 2 filas de `activitymimeattachment` | en `estado.json` |

Dueño de los flujos: admin@ (se crearon con `pruebas-humo/lote-d/scripts/como_servicio.py`, como EST y SAL). Conexiones: **se reutilizan** `sanic_carp_conr_outlook` y `sanic_carp_conr_dataverse` de la solución principal (son las de admin@, con las que ya se activaron EST y SAL). No se creó una referencia propia porque un service principal no puede asociarle una conexión (`ConnectionAuthorizationFailed`, `pruebas-humo/lote-d/RESULTADO.md`). Efecto: los flujos del spike dependen de esas dos referencias mientras existan.

**Aprendizaje de herramienta (también va a `tropiezos.md` si se quiere):** un service principal puede **crear** un flujo, pero **no puede modificar su definición** una vez creado (el `PATCH` de `clientdata` devuelve `ConnectionAuthorizationFailed`, aunque el flujo esté apagado) ni renombrarlo. Cada corrección de un flujo obliga a crear uno nuevo con otro nombre; por eso hay tres flujos descartados. En cambio, **intentar activar** un flujo como service principal sirve de validador gratuito de la plantilla: si la expresión es inválida responde `TemplateValidationError` (así se encontró `?['@odata.type']`, que esta plataforma no admite); si es válida responde `ConnectionAuthorizationFailed`. Los tres flujos vigentes dieron `ConnectionAuthorizationFailed`. No valida los nombres de parámetros del conector ni la semántica: eso lo dirá la primera corrida.

## 3. V-1: `SendEmail` desde una cola con mailbox en None

**Qué:** ¿`SendEmail` desde la queue de un mailbox con entrega None acepta el correo y lo deja en 6, sin rechazarlo? ¿Qué hace `requireapprovalforqueueemail` (true en Dev)? ¿Qué pasa con `IssueSend=false`?

**Cómo:** `v10_cola_prueba.py` crea la cola sobre el buzón 4 y deja su mailbox en 0/0 (nació en 2/2 con aprobación pendiente `emailrouteraccessapproval` = 2, por eso no sincronizaba); `v11_sendemail.py` crea dos salientes (de la cola al buzón 2) y llama a `SendEmail`; `v12` y `v13` ensayan las transiciones que hará el flujo de salida.

| Caso | Resultado | Evidencia |
|---|---|---|
| `IssueSend=true` | HTTP 200 (2,3 s), cuerpo `{Subject}`. `statuscode` 6, `statecode` 1 (Completed), `actualend` puesto en ese instante, `senton` nulo, `messageid` **provisional** (`<GUID@DOMINIO-DEL-BUZON-4>`, dominio en mayúsculas). Sigue en 6 a los 0, 20 y 60 s. No aparece nada en Enviados del buzón 4 ni en la bandeja del buzón 2 | `evidencia/v11_resumen.json`, `v11_sendemail.json` |
| `requireapprovalforqueueemail` = true | **No bloquea** a pesar de que el mailbox de la cola tenía la aprobación pendiente y entrega None. No se cambió el valor de la organización (es del entorno y afecta a todos los proyectos) | `v11_sendemail.json` |
| `IssueSend=false` | HTTP 200 y el correo pasa **directo a 3 Sent** (`statecode` 1), con `senton` y `actualend` puestos por el servidor y `messageid` nulo. No sale nada. Sirve de plan de respaldo para marcar un correo como enviado sin enviarlo, pero nuestro flujo no lo necesita | `v11_resumen.json` |
| 6 → 7 Sending → 3 Sent con `messageid` propio | 204 y 204; el `messageid` escrito queda grabado | `v12_detalle.json` |
| `senton` | **No es escribible**: el `PATCH` da 204 y el valor sigue nulo. En una salida cerrada por el flujo `senton` queda nulo; S-35 usa `InstanteDelEnvio(senton, actualend)` y cae en `actualend`, que es el instante en que el Oficial pulsó Enviar. El tiempo de respuesta queda medido hasta ese clic y no hasta el envío real (diferencia = latencia del flujo) | `v13_detalle.json` |
| 6 → 8 Failed | `{"statuscode": 8}` solo da **400** (`8 is not a valid status code for state code EmailState.Completed`). **Failed pertenece al estado Open (0)**: hay que escribir `{"statecode": 0, "statuscode": 8}` (204). Los motivos de estado son 1 Draft(0), 2 Completed(1), 3 Sent(1), 4 Received(1), 5 Canceled(2), 6 Pending Send(1), 7 Sending(1), 8 Failed(0) | `v12_detalle.json`, `v13_detalle.json` |
| Failed → 6 (reintento a mano) | 204 con `statecode` 1 y `statuscode` 6 | `v13_detalle.json` |

**Conclusión:** V-1 se **cumple**. El flujo SND puede filtrar por `statuscode eq 6 and directioncode eq true`. Contradice un detalle de `diseno/08` §4.6: "el flujo pone `statuscode` 8" hay que leerlo como "`statecode` 0 y `statuscode` 8". Y afina el §4.5: `senton` no lo escribe nadie; S-35 funciona con `actualend`.

## 4. V-3: `DeliverIncomingEmail`

**Qué:** ¿correlaciona una respuesta (Regarding, `correlationmethod`) como SSS?

**Cómo:** `v30_deliver.py`. Original: salientes de la cola de prueba con Regarding a una cuenta ficticia (no a una conversación, para no activar S-31/S-37), pasados a Sent con un `messageid` propio. Se llamó la acción con tres respuestas. La entrega es entre buzones de prueba (nada sale: la acción solo crea la fila).

| Caso | Resultado |
|---|---|
| Asunto `RE: <asunto del original>`, remitente y destinatario del hilo, sin cabeceras | Crea el email en 4 Received (entrante, `statecode` 1) pero **no correlaciona**: `regardingobjectid` vacío, `correlationmethod` 0 |
| Asunto con el token de seguimiento del original | **No concluyente**: la plataforma no asignó token (`trackingtoken` quedó nulo aunque se pasó `TrackingToken` en `SendEmail`), así que no había token que reconocer. Organización: `emailcorrelationenabled` true, `enablesmartmatching` sin valor |
| `ExtraProperties` = `{"@odata.type": "Microsoft.Dynamics.CRM.email", "inreplyto": "<messageid del original>"}` | **Correlaciona como SSS**: `correlationmethod` 3 (InReplyTo), `inreplyto` grabado, `regardingobjectid` = el Regarding del original, `parentactivityid` y `correlatedactivityid` = el original |

Otros datos: (1) el parámetro `Attachments` es obligatorio (falta ⇒ 400 `Required field 'Attachments' is missing`); `[]` sirve. (2) La fila nace con dueño = quien llama (aquí el service principal), BU raíz, `receivingmailboxid` nulo, y **se agregó sola a la cola** de `SubmittedBy` (los tres casos tenían un `queueitem`). (3) No hay parámetro de dueño.

**Conclusión:** V-3 se **cumple con reservas**. Documentado en Learn, `DeliverIncomingEmail` no trae In-Reply-To y no correlaciona; la propiedad interna `ExtraProperties` con `inreplyto` sí, pero está marcada "for internal use only" (no soportada, puede cambiar sin aviso). Además el dueño sale del que llama: para que S-34 la procese (exige BU del área) habría que reasignar justo después, contra la ejecución asíncrona de S-34 (carrera). **Recomendación para el plan B: mantener el `Create` explícito del §3 de `diseno/08` con búsqueda del `messageid`** (soportado, fija el dueño en el mismo `Create`, ya ensayado). `DeliverIncomingEmail` + `ExtraProperties` queda como atajo documentado, no como camino base. Esto **no contradice** a `diseno/08`: ahí se dejó como "vale una prueba".

## 5. V-2 y V-4: flujos preparados (apagados)

Los tres se generan con `generar_flujos.py` (Python, no JSON a mano), se suben con `herramientas/construir/flujo.py` por los playbooks de `playbooks/`, y cada uno deja una fila `SPK-DIAG ...` (email saliente en borrador, dueño admin@, fuera de la BU del área, S-34 la ignora) cuyo `description` es un JSON con todo lo que midió y el estado de cada acción. Así los resultados se leen por API sin pedirle capturas al usuario.

* **V2 `Encabezados y respuesta`**: disparador *When a new email arrives in a shared mailbox (V2)* sobre el buzón 4 con `subjectFilter` `SPK-FC-V2`. `GET` a Graph del mensaje (`internetMessageHeaders`, `internetMessageId`), filtra `In-Reply-To` y `References`, `createReply` con comentario, `GET` del borrador, `send`, espera 15 s y lee Enviados para comparar el `internetMessageId`.
* **V4 `Entrada completa (r3)`**: mismo disparador con `subjectFilter` `SPK-FC-V4` y adjuntos incluidos. Lee encabezados por Graph; **idempotencia** por `messageid`; **correlación** (3 variantes del In-Reply-To contra `messageid`, el más antiguo con Regarding, y comprobación de que ese Regarding sea una conversación); `Create` del `email` con dueño = team del área, partes from/to, `actualend`, `messageid`, `sender`, `torecipients`, con o sin Regarding; cierre a Received (1/4); adjuntos por Graph (`attachments`, no por la salida del disparador) con `attachmentcontentid` para las imágenes en línea; `queueitem` en la cola de prueba; categoría `CARP-registrado` en Outlook. Los pasos opcionales (cola, categoría) van en un `Scope` aparte para que su falla no oculte el resto.
* **SND `Salida de respuestas (r2)`**: disparador de Dataverse (Update de `statuscode`, filtro `statuscode eq 6 and directioncode eq true`, concurrencia 1), con guardas (Regarding es una conversación, remitente = buzón de salida del área, sigue en 6) para que **no toque los Pending Send de otros proyectos** del mismo entorno. Pasa a 7, busca el original en el buzón por `internetMessageId` del correo padre, `createReply`, completa destinatarios y cuerpo, agrega adjuntos, lee el `internetMessageId` del borrador, `send` y cierra en Sent con ese `messageid`; si falla, `statecode` 0 y `statuscode` 8.

Ensayado por API antes de armar V4 (`v40_simular_entrada.py`, sin tocar la BU del área): `Create` de un entrante con partes `from` (`addressused`) y `to` (queue), `actualend`, `messageid`, `sender`, `torecipients` (204); cierre a 1/4 (204); `activitymimeattachment` con y sin `attachmentcontentid` (204); `queueitem` por `AddToQueue` (200) y por `Create` directo (204, es lo que usa el flujo).

### 5.1 Qué falta medir (se mide al activar)

| Qué | Script | Mide |
|---|---|---|
| V-2 | `v20_v2_encabezados_y_respuesta.py` | Que el id del disparador sirva a Graph; que lleguen `internetMessageHeaders`; que el `internetMessageId` del borrador sea el que sale; que el In-Reply-To de una contestación sea el de la respuesta del flujo; latencias |
| V-4 entrada | `v21_v4_entrada.py` | Dueño y BU del área, S-34 (ruta C) sobre un entrante creado por flujo; cola; categoría; adjuntos (archivo, imagen, imagen en línea, `.eml`) contra el **mismo** correo entrando por SSS al buzón 1 |
| V-4 ruta A | `v22_v4_ruta_a.py` | Que el flujo ponga el Regarding por el In-Reply-To y S-34 entre por la ruta A, con un saliente Sent en Dataverse con el `messageid` real |
| V-4 idempotencia | `v23_idempotencia.py` | Tras *Reenviar* una corrida desde el portal, sigue habiendo un solo email |
| V-4 salida integral | `v60_salida_e2e.py` (+ `v62_restaurar_ventana.py`) | H-5 con flujo: Oficial → Pending Send → SND → Sent con `messageid` real → llega al cliente desde el buzón 1 → la contestación entra por SSS y vuelve a la misma conversación; S-35 |

### 5.2 Ventana de la salida SSS del mailbox 1 (autorizada, no ejecutada)

`v60_salida_e2e.py` guarda en `estado.json` la línea base completa del mailbox 1 (métodos, habilitados, estados, aprobación) **antes** de cambiar nada, abre la ventana solo en la fase 3 (salida 2 → 0), la cierra en un `finally` (siempre), y verifica campo por campo que el mailbox quedó igual. Si el flujo no tomó el correo, lo marca Failed **antes** de restaurar, para que SSS no lo mande de verdad. `v62_restaurar_ventana.py` restaura por separado si el script se cortó. Ensayo sobre el mailbox del spike (no el del área): cambiar la salida a 2 o a 0 cambia sola `enabledforoutgoingemail` (true/false); `outgoingemailstatus` no cambió. **Riesgo abierto:** en el mailbox 1 `outgoingemailstatus` vale 1 (éxito); no se sabe si al volver a 2 la plataforma lo deja en 1 o lo manda a "sin probar" (entonces habría que repetir *Test & Enable*). El script lo verifica y lo informa.

## 6. Riesgos y contradicciones con `diseno/08`

1. **Failed necesita `statecode` 0** (§4.6): corregir al escribir el playbook de SND.
2. **`senton` no se puede escribir** (§4.5): S-35 usará `actualend`; el tiempo de respuesta cuenta hasta el clic de Enviar.
3. **Un service principal no puede modificar un flujo ya creado** (ni siquiera apagado): afecta al procedimiento de instalación y a las correcciones del plan B; cada cambio de definición necesita a quien posee la conexión (o crear el flujo de nuevo).
4. **`DeliverIncomingEmail`** queda como atajo no soportado (ver §4).
5. **SND para el área real exige Full Access y Send As de admin@ sobre el buzón 1**, no solo sobre el 4. Hace falta para la prueba integral.
6. Parámetros del conector `HttpRequest` (`Uri`, `Method`, `Body`, `ContentType`), de Dataverse `email_activity_parties` y de `PerformBoundAction` se tomaron de Learn y de flujos existentes; la plataforma los acepta al crear, pero **no se probaron en ejecución** hasta la activación.
7. Quedan en Dev filas de prueba que no se pueden borrar (cuenta ficticia, correos `SPK-FC-*`, conversaciones que cree S-34 en V-4, tres flujos descartados). Se pueden retirar borrando después la solución `sanic_carp_sol_spikecorreo`, la cola de prueba y las filas `SPK-FC-` / `SPK-DIAG`.

## 7. Archivos

`flib.py` (utilidades) · `v10`–`v13` (V-1) · `v30` (V-3) · `v40` (ensayo de escritura) · `v50` (solución y variables) · `generar_flujos.py` + `flujos/` + `playbooks/` (flujos) · `v20`–`v23` y `v60`, `v62` (pendientes de activación) · `evidencia/` · `estado.json`.

## Corrección 2026-11-02: V4 (r4)

Al activar `V4 Entrada completa (r3)` la plataforma respondió `WorkflowRunActionInputsInvalidProperty`: la acción `Fijar_modo_final` (SetVariable) usaba `variables('varModo')` para fijar `varModo` ("Self reference is not supported"). Se reemplazó por `AppendToStringVariable` con `+registrado` y se creó `Cloud Flow - CARP - SPK - V4 Entrada completa (r4)` (`5c1c8e11-1ad7-f111-84e8-0022480c5699`), apagado. `(r3)` queda descartado. Se revisaron los tres flujos: no hay otra autorreferencia. **Aprendizaje:** el intento de activación como service principal NO detecta esta regla (responde `ConnectionAuthorizationFailed` antes); sí la detecta la activación por una persona. También se apagó `SND Salida de respuestas` (sin sufijo, descartado), que había quedado encendido por error: dos flujos de salida encendidos duplicarían los envíos.

## Corrección 2026-11-02: V4 (r5)

Al activar `(r4)`: `WorkflowOperationParametersExtraParameter`, "The API operation does not contain a definition for parameter 'item/ownerid@odata.bind'". El conector de Dataverse nombra las búsquedas por la **propiedad de navegación** del lado de la tabla, no por el nombre de la columna: en `email`, `ownerid` es `ownerid_email` (leído de `EntityDefinitions(LogicalName='email')/ManyToOneRelationships`). La Web API acepta `ownerid@odata.bind`; el conector no. Se creó `Cloud Flow - CARP - SPK - V4 Entrada completa (r5)` (`65a1ce90-1ad7-f111-84e8-0022480c5699`), apagado, con `item/ownerid_email@odata.bind`. Las otras búsquedas del flujo se cotejaron contra la metadata y coinciden (`regardingobjectid_sanic_carp_tbl_conversacion_email`, `objectid_email`, `queueid`). `(r4)` queda descartado.

## Corrección 2026-11-02: V4 (r6)

Al activar `(r5)`: `WorkflowOperationParametersExtraParameter` para `item/email_activity_parties`. En el conector, las partes de una actividad se exponen por la relación de `activitypointer` (`item/activitypointer_activity_parties`), no por la de `email`. Se creó `(r6)` con ese nombre; `(r5)` queda descartado. **Pendiente de confirmar al activar**: este nombre sale de flujos exportados conocidos, no de metadata leída; si falla, el plan es leerlo del diseñador (el usuario abre la acción y copia la vista de código).
