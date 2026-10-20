# Tanda 3 de humo: correo real por la cola (2026-10-20, Dev)

Autorizada por el usuario el 2026-10-20: hasta ~8 correos con asunto `HUMO-T3-…`, solo desde los buzones de prueba 2 a 4 hacia el buzón 1 (la cola `Arreglos de Pago`), más las respuestas que salen desde la cola. `t3lib.Correo` lo hace cumplir antes de llamar a Graph (remitente 2..4, destinatario exactamente el buzón 1, prefijo y tope de 8 contado en `estado.json`). En la evidencia los buzones van por el nombre de su variable. No se borró nada. Datos ficticios (`HUMO-`).

Las acciones de personas se hicieron como `# User 8` (Oficial, rol por el team del sistema) y `# User 9` (Jefe, por Jefatura) con `MSCRMCallerID`, después de 13.7 (`pruebas-humo/lote-0/RESULTADO.md`).

**Resultado: 73 de 73 verificaciones OK. Ningún caso refutado.**

## Correos de la tanda (exactos)

| # | Desde | Hacia | Asunto | Qué prueba |
|---|---|---|---|---|
| 1 | `PP_MAIL_BUZON_2` | `PP_MAIL_BUZON_1` | `HUMO-T3-A Consulta de arreglo de pago` | entrada nueva (caso A) |
| — | cola (`PP_MAIL_BUZON_1`) | `PP_MAIL_BUZON_2` | `RE: HUMO-T3-A …` (R1) | respuesta del Oficial |
| 2 | `PP_MAIL_BUZON_2` | `PP_MAIL_BUZON_1` | `RE: HUMO-T3-A …` | respuesta del cliente en el mismo hilo |
| 3 | `PP_MAIL_BUZON_3` | `PP_MAIL_BUZON_1` | `HUMO-T3-C Pago de cuota atrasada` | entrada nueva (caso C) |
| — | cola (`PP_MAIL_BUZON_1`) | `PP_MAIL_BUZON_3` | `RE: HUMO-T3-C …` (R2) | respuesta del Oficial |
| 4 | `PP_MAIL_BUZON_3` | `PP_MAIL_BUZON_1` | `RE: HUMO-T3-C …` | correo nuevo en una Finalizada |

4 correos por Graph (2 del buzón 2, 2 del buzón 3; el buzón 4 no se usó) y 2 respuestas desde la cola. Total 6, dentro del tope de 8. Un borrador saliente (`HUMO-T3-H28 …`, para S-37) se creó y **no se envió**: queda en Borrador en Dataverse.

## Resultado por caso

| Caso | Veredicto | Evidencia | Qué se midió |
|---|---|---|---|
| Entrada por la sincronización | **CONFIRMADO** | `10_*` | El correo 1 aparece como Email en Dataverse a los ~31 s del envío, con `receivingmailboxid` = el mailbox de la cola. Al primer sondeo ya tenía conversación |
| H-26 | **CONFIRMADO** | `00_*`, `10_*`, `30_*` | Todo Email entrante nace con `ownerid` = el team del sistema (dueño de la queue) y `owningbusinessunit` = la BU del área. La auditoría muestra que lo crea `# SSSAdminProd` (el usuario de la sincronización del servidor, accessmode 4) **ya con ese dueño**: S-34 no tuvo que asignar (PA-ENT-14). Lo mismo para los 10 correos que trajo la primera sincronización |
| H-25 | **RESPONDIDO** | `10_*`, `30_*` | Correo nuevo: nace sin Regarding y SYSTEM lo escribe ~9 s después (S-34, **ruta C**). Respuesta del cliente: **el `Create` de la sincronización ya trae el Regarding** a la conversación (`correlationmethod` 3, In-Reply-To; un único registro de auditoría, la creación); S-34 entra por la **ruta A** y da `Unida`. No hubo correlación tardía en ningún caso |
| Ruta C, DC-14 | **CONFIRMADO** | `10_*`, `40_*` | Conversación nueva Abierta, del team, en la BU, `CONV-001191`, `fechaprimercorreo` = `sinrespuestadesde` = `actualend`. Con un Cliente activo con el correo del remitente se enlaza a él; sin ninguno (buzón 3) queda sin cliente |
| H-27 (a, b, c) | **CONFIRMADO** | `10_*`, `20_*` | SYSTEM escribe el Regarding de un Email ya cerrado (Received, 1/4) y sigue cerrado; el dueño ya era el team (sin `Assign` en Dev). S-35 escribe `sanic_respondidoporid` y `sanic_tiemporespuesta` sobre un Email Sent |
| Tomar (con usuario real) | **CONFIRMADO** | `20_*`, `40_*` | User 8 toma A y C: 200, `yaasignada` false, en 0,6-1,7 s |
| S-31 | **CONFIRMADO** | `20_*` | Remitente por defecto (el propio usuario) ⇒ se reemplaza por la queue al crear (DC-07). Otro usuario (User 9) como remitente ⇒ `CARP-EML-001`, sin fila |
| H-28 (a, b, c) | **CONFIRMADO** | `20_*` | (a) El `Update` con Regarding a la conversación sobre un borrador saliente pasa (204). (b) S-37 deja como remitente **solo** la queue: antes `[User 8]`, después `[queue]`, no acumula. (c) Después del envío el remitente sigue siendo solo la queue y S-37 no rechazó el `Update` interno del envío |
| H-5 | **CONFIRMADO** | `20_*`, `30_*` | R1, creada por User 8 con `parentactivityid` = el correo del cliente, pasa a Sent en ~31 s (`SendEmail` como User 8: 200, basta Write) y llega al buzón 2 **con remitente = buzón 1**. El cliente contesta (correo 2) y su respuesta entra a la **misma** conversación (~2 min hasta Dataverse); no se crea ninguna conversación nueva. Lo mismo con R2 hacia el buzón 3 |
| H-6 / S-35 | **CONFIRMADO** | `20_*`, `40_*` | Al pasar a Sent: `sanic_respondidoporid` = User 8 (`createdby`), `sanic_tiemporespuesta` = 2 min (llegada → `senton`, truncado) en R1 y 0 en R2. Conversación A: 1 respuesta, pendiente vacío, primera respuesta 2. Cuando contesta el cliente, el pendiente vuelve a su llegada y la cantidad sigue en 1 |
| Finalizar (gesto 2, usuario real) | **CONFIRMADO** | `40_*` | User 8 finaliza C con "Sin arreglo de pago": 204, `finalizadoporid` = User 8, fecha y `tiempototal` (1 min) |
| PA-ENT-07 | **CONFIRMADO** | `40_*` | El correo 4 entra a la Finalizada C, que **sigue Finalizada**, con `correonuevoenfinalizada` = sí y pendiente = su llegada (RF-15) |
| Iniciar conversación nueva (gesto 5, camino feliz) | **CONFIRMADO** | `40_*` | Antes del correo nuevo: `CARP-INI-003`. Después, como User 8: 200 en 1,65 s, `correosmovidos` = 1. La nueva: Abierta, sin oficial, del team, pendiente y primer correo = llegada del correo 4, sin cliente. C1 y R2 se quedan en la anterior, que queda Finalizada, sin indicador ni pendiente. Segunda llamada: `CARP-INI-003` (PA-INI-11). Era lo que el lote B no pudo probar |
| 2.4 / H-8 (usuarios reales) | **CONFIRMADO** | `50_*` | User 8 (Oficial) no puede reasignar A a otro: `CARP-CNV-008`. User 9 (Jefe por Jefatura) sí: 204 |
| Unir (gesto 4, usuario real) | **CONFIRMADO** | `50_*` | User 8 une la nueva del caso C (sin cliente) a A (con cliente): 200, 1 correo movido, 0 arreglos. Origen Unida (1/159460001), `unidaaid` = A, `finalizadoporid` = User 8, sin pendiente. Destino: pendiente = el mínimo de los dos, 1 respuesta procesada, conserva su cliente |
| H-7 | **PARCIAL** | `50_*` | `reconciliarcorreos` llamada como la cuenta de servicio de Dev (`admin@…`, decisión del usuario): lote con `minutosminimos` 0 ⇒ 200, `encontrados` 0 (ningún correo de la BU quedó sin conversación); con `emailid` del correo 1 ⇒ `yatenia` en A. Falta la llamada **desde el flujo** 11.2 |
| Gráfico 12.15 | **CON DATOS** | `90_*` | Ya hay 2 respuestas con oficial y tiempo (2 y 0 min, User 8). El gráfico deja de estar vacío |
| Trabajos asíncronos | **OK** | `90_*` | 11 trabajos de los plugins CARP desde las 19:30, todos Succeeded. Los 37 steps siguen habilitados |

## Hallazgos

1. **La primera sincronización trae la bandeja existente.** Al habilitar el mailbox (18:09), la sincronización importó los 10 correos que ya estaban en la bandeja del buzón 1 (de spikes del 2026-10-08 en adelante, más el aviso "Your mailbox is now connected"), y S-34 creó **10 conversaciones** (`00_*`). En Dev es inofensivo (son correos de prueba), pero **en Prod el buzón real de BAC traería su bandeja al conectarlo** y crearía una conversación por correo viejo, todas como pendientes. Hay que decidir antes del pase a Prod: vaciar o archivar la bandeja del buzón real antes de conectarlo, o aceptar la carga inicial y finalizarla. No es un defecto del código.
2. **Avisos del sistema.** El correo "Your mailbox is now connected" también abrió una conversación. Cualquier aviso automático que llegue al buzón del área va a hacer lo mismo (el diseño registra todo entrante).
3. **El dueño del Email entrante lo pone la plataforma** (el dueño de la queue) antes de S-34; el `Assign` del paso 7 de 2.10 no se ejerció. Queda como red para correos que entren por otra vía.
4. **La cuenta de servicio de Dev es System Administrator** y no tiene los roles `SR - CARP - Servicio` ni `SR - CARP - Purga` (13.6). La llamada funcionó por el administrador, no por el rol del diseño. En Test y Prod hace falta la cuenta con los roles 6.4 y 6.5 y sin administrador.

## Pendiente

- H-7 desde el flujo 11.2 (`CARP-SAL`), cuando tenga su conexión.
- H-32 sigue sin reproducir: el Oficial real lee los correos del área (`prvReadActivity` Local), así que el camino de `0x80040220` no aparece.
- Prueba en el navegador del usuario: la lista sigue en `lote-a`, `lote-b`, `lote-c` y `lote-e`. Con estos datos ya se ven la conversación A (`CONV-001191`, asignada a User 9, con 4 correos), la C Finalizada y la Unida.

## Filas creadas (GUID en `estado.json`)

| Clave | GUID |
|---|---|
| Cliente `cliente-t3` (correo del buzón 2) | `5248e4a9-bdcc-f111-a05b-00224808ffcd` |
| Conversación A (`CONV-001191`) | `f8680dc1-bdcc-f111-a05b-000d3a340023` |
| Conversación C (Finalizada) | `f8564bb1-becc-f111-a05b-000d3a340023` |
| Conversación nueva del caso C (hoy Unida a A) | `ff519ddf-becc-f111-a05b-00224808ffcd` |
| Emails: correo 1, R1, correo 2, borrador H-28 | `a428aabc-bdcc-f111-a05b-00224808ffcd`, `970034fd-bdcc-f111-a05b-00224808ffcd`, `61e8de62-becc-f111-a05b-00224808ffcd`, `8c0034fd-bdcc-f111-a05b-00224808ffcd` |
| Emails: correo 3, R2, correo 4 | `240757ab-becc-f111-a05b-00224808ffcd`, `228f04bb-becc-f111-a05b-00224808ffcd`, `000927d0-becc-f111-a05b-00224808ffcd` |

## Scripts

`t00_linea_base.py`, `t10_entrada_nueva.py`, `t20_tomar_y_responder.py`, `t30_respuesta_cliente.py`, `t40_finalizada_e_iniciar_nueva.py`, `t50_jefe_unir_reconciliar.py`, `t90_cierre.py`, sobre `t3lib.py`. Son reanudables: cada envío queda registrado en `estado.json` y no se repite.
