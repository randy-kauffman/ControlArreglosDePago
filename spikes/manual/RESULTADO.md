# Spike 4: identidad no administradora, app de pruebas y pruebas manuales

Proyecto 2026-003 "Control de Arreglos de Pago". Fecha de ejecución: 2026-10-08 (19:50 a 21:10 UTC). Estado: las tareas A, B y C están terminadas y verificadas; la tarea D está **preparada y a la espera de que el usuario haga las tres pruebas** (ver el final).

## Alcance y método

- Entorno: `https://org36e60d9d.crm.dynamics.com/` (`pac org who`: Org "Dev", Environment ID `9c8994b8-b1bc-ee23-9184-2f6346502613`). Ningún otro entorno.
- Dataverse como administrador: la cuenta de servicio del estudio (`dv.py` del spike 2, System Administrator). Dataverse como **no administrador**: el service principal de correo (`PP_MAIL_*`) autenticado contra Dataverse, dado de alta como usuario de aplicación (autorizado por el usuario) en la BU raíz, con **solo** los roles de prueba `SPK Lector Cliente` (tarea A) y `SPK Oficial Correo` (tarea B). Nunca tuvo System Administrator ni System Customizer. Ningún secreto ni token se imprimió ni se guardó.
- Correo: Graph con `PP_MAIL_*`, solo entre los buzones 1 a 4 y con asunto `[SPIKE4]` (la biblioteca aborta antes de llamar a Graph si no se cumple).
- Regla de verificación (`herramientas/patrones.md` §2.2): ningún resultado se afirma por el código HTTP; cada afirmación sale de releer (roles del usuario, privilegios del rol, Email en Dataverse, mensaje en el buzón 2 por Graph, app publicada, exportación de la solución).
- No se tocó ninguna configuración de la organización, el mailbox ni la queue `Spike`. No se tocaron el paquete de plugins, la Custom API ni `spk_versionplantilla` (otro agente). Solo se **leyó** la solución con `ExportSolution`, y una vez se **importó un ZIP mínimo** que contiene únicamente la app y su sitemap (ver tarea C); se comprobó antes y después que los 20 componentes de la solución seguían todos y que el estado de los steps de plugin no cambió.

## Tabla de veredictos

| # | Pregunta | Veredicto | Evidencia resumida | Impacto |
|---|---|---|---|---|
| A | 2c: sin perfil de columna, ¿quick find encuentra la fila por la cédula? ¿Y con perfil? | **CONFIRMADO** el comportamiento esperado: **sin perfil NO la encuentra (y no da error); con perfil SÍ la encuentra** | Principal no administrador, rol solo de lectura. Sin perfil: la fila se ve (por nombre), `spk_cedula` viene `null`, quick find por cédula devuelve 0 filas con HTTP 200. Con perfil: `spk_cedula` visible y quick find devuelve `Q2-beto` | **D-12 y D-19 se cierran con la salvedad de abajo**: la seguridad por columna sobre la cédula es compatible con la búsqueda del oficial que está en el perfil. El plan B de D-19 no se activa |
| B | D-28: ¿puede un no administrador crear un Email con la queue como remitente, Regarding a la conversación, `parentactivityid`, y enviarlo con `SendEmail`? | **CONFIRMADO**, con un rol de **9 privilegios** | Envío verificado por Graph en el buzón 2: `From` y `Sender` = la queue, `In-Reply-To` = el correo original, misma raíz de `Thread-Index`, 23 s después de `SendEmail`; Email en Dataverse con estado Sent, propietario = el usuario de aplicación | **D-28 se corrige**: "Send Email as Another User" **no** hizo falta; sí hacen falta AppendTo y Read sobre Queue. "Send" y "Queue Item: Read" del diseño no existen como privilegios (ver abajo) |
| C | App model-driven con timeline | **CONFIRMADO** | App publicada, `clienttype` 4, tablas `spk_conversacion` y `email` + sitemap, formulario publicado con el timeline | URL abajo |
| D | Tres pruebas manuales | **PREPARADAS, NO EJECUTADAS** | Correo `[SPIKE4] Prueba manual`, conversación y guía listos; `verificar.py` corre y reporta "pendiente" en las tres | Los resultados salen cuando el usuario avise |

## Tarea A. Búsqueda por cédula sin perfil de columna (pregunta 2c del spike 2)

1. **Alta del usuario de aplicación** (`s00_usuario_app.py`): `POST systemusers` con `applicationid` = `PP_MAIL_CLIENT_ID`, BU raíz → 204. Releído: `isdisabled = false`, `accessmode = 4` (no interactivo), BU raíz. Rol asociado `SPK Lector Cliente` → 204. Releído por el administrador: roles = `['SPK Lector Cliente']`. (Nota de método: el propio principal no puede leer su asociación de roles; la relectura la hace el administrador.)
2. **Corrida de `s65` adaptada** (`s10_q2c.py`, sin suplantación: el principal se autentica como sí mismo; `WhoAmI` coincide con el usuario de aplicación). Se esperan 45 s tras cada cambio de pertenencia al perfil. Datos: filas `Q2-ana` (`001-000000-0001A`) y `Q2-beto` (`001-000000-0002B`). Quick find se emula con el mismo FetchXML que usa la interfaz (`isquickfindfields='1'` sobre `spk_nombre` y `spk_cedula`). Evidencia: `evidencia/q2c_resultado.json`.

| Consulta | Escenario B: rol de lectura, **sin** perfil | Escenario A: rol de lectura, **con** `SPK Perfil Cedula Lectura` |
|---|---|---|
| Leer filas `Q2-*` con `spk_cedula` | HTTP 200, 2 filas, `spk_cedula = null` en las dos | HTTP 200, 2 filas, cédulas visibles |
| Quick find (nombre OR cédula) con `001-000000-0002%` | HTTP 200, **0 filas** | `Q2-beto` |
| `like` solo sobre `spk_cedula` | 0 filas | `Q2-beto` |
| Control: quick find por nombre `Q2-be%` | `Q2-beto` | `Q2-beto` |
| `$filter=spk_cedula eq '<valor exacto>'` | 0 filas | `Q2-beto` |
| `$filter=spk_cedula ne '<valor>'` (¿canal para deducir valores?) | 0 filas (no hay fuga) | `Q2-ana` |

Respuestas: un principal **sin** perfil **no encuentra** la fila buscando por cédula con quick find y **ve la columna vacía** (`null`, sin error ni aviso). Filtrar por la columna con `eq` o `ne` tampoco devuelve filas, así que no hay un canal para deducir cédulas. **Con** perfil la encuentra y ve el valor.

Consecuencias de diseño y matices:
- **D-12 y D-19 se cierran a favor de la seguridad por columna**: la unicidad por plugin (D-07) y la búsqueda por cédula son compatibles con ella, siempre que todo oficial que deba buscar esté en el perfil.
- El fallo es **silencioso**: un usuario fuera del perfil recibe "sin resultados", no un error. Conviene una prueba de aceptación y que el alta de usuarios incluya la pertenencia al perfil.
- El perfil se asigna a **usuarios o equipos**, nunca a roles (`seguridad/patrones.md`). Aquí se probó con un usuario. Que un **equipo de grupo de Entra** (el team ARP del diseño) como miembro del perfil dé el mismo resultado no se probó, aunque el mecanismo documentado es el mismo.
- No probado: la interfaz real (cuadro de búsqueda de la vista) ni Dataverse search (búsqueda por relevancia); se probó la consulta equivalente por Web API. La documentación dice que la búsqueda por relevancia respeta la seguridad por columna.
- Límite: el principal es un usuario de aplicación y no una persona. La evaluación de privilegios y de la seguridad por columna no distingue entre ambos, pero no se probó con un usuario interactivo.

## Tarea B. Enviar con la queue como remitente sin ser administrador (D-28)

### Método

`s20_b_preparar.py` envía por Graph un correo entrante (buzón 2 a buzón 1, `[SPIKE4] Tarea B correo entrante`), espera a que exista como Email, crea la conversación `[SPIKE4] Tarea B conversacion` (como administrador: la crea el sistema) y fija el Regarding del correo entrante. `s21_b_rol.py` crea `SPK Oficial Correo` en la BU raíz y dentro de la solución (pertenencia releída) y agrega **solo** los privilegios que faltan (la receta de seguridad advierte que `AddPrivilegesRole` pisa el alcance de los existentes). `s23_b_probar.py` hace el intento completo **como el usuario de aplicación** y se detiene en el primer fallo de escritura: leer la conversación, el correo entrante y la queue; crear el Email (`from` = queue `Spike`, `to` = buzón 2, `regardingobjectid` = conversación, `parentactivityid` = correo entrante); `SendEmail`. El rol de lectura se retiró del usuario para que solo contara el de prueba.

### Cronología de intentos (`evidencia/b_intentos.jsonl`)

| Intento | Rol | Resultado y mensaje |
|---|---|---|
| 1 | Lo que dice el diseño, profundidad BU (Local): Create, Read, Write, Append, AppendTo de Email; Read de Queue; Read, Append, AppendTo de la conversación; `prvSendAsUser`. (19 privilegios con los 9 de base) | **FALLO** al crear el Email, HTTP 403: `Principal user (Id=…, type=8, roleCount=1, privilegeCount=19, accessMode='4 Non-interactive', …), is missing prvAppendToQueue privilege (Id=041fdb4c-bbb8-4792-90db-51c19d067ca2) on OTC=2020 for entity 'queue' (LocalizedName='Queue'). context.Caller=…. Consider adding missed privilege to one of the principal (user/team) roles.` |
| 2 | + `prvAppendToQueue` (Local) | **FALLO con el mismo mensaje**, a los 20 s, a los 110 s y a los 4 min: el principal seguía con `privilegeCount=19` aunque el rol ya tenía 20 |
| 2d | Igual, pero quitando y volviendo a asociar el rol al usuario | **ENVIADO**. El recuento del principal pasó a 20 |
| FINAL | Conjunto mínimo (9 privilegios, tabla de abajo) | **ENVIADO**, verificado por Graph |

### Hallazgos no obvios

1. **La caché de privilegios del principal no se actualiza al agregar privilegios al rol** (al menos durante 4 minutos). Lo que la refresca es **quitar y volver a asociar el rol al usuario**. Verificado con una sonda: un alta prohibida (`POST spk_clientes`) devuelve 403 cuyo mensaje trae `privilegeCount=N`, que es el recuento en caché. En todas las pruebas posteriores el recuento coincidió con el del rol solo después de re-asociar. Para pruebas de seguridad: re-asociar el rol tras cada cambio, o la prueba da un falso "falta el privilegio".
2. **"Send" no existe como privilegio de Email.** Los privilegios de Email son los de Activity, compartidos por todas las actividades: `prvCreateActivity`, `prvReadActivity`, `prvWriteActivity`, `prvAppendActivity`, `prvAppendToActivity` (y Delete, Assign, Share). `SendEmail` exige `prvWriteActivity` (error observado al quitarlo: `is missing prvWriteActivity privilege on OTC=4216 for entity 'socialactivity'`). **El nombre de la tabla en el mensaje es engañoso**: la plataforma nombra una tabla cualquiera de las que comparten ese privilegio (aparecieron `socialactivity`, `adx_inviteredemption`, `recurringappointmentmaster`, `fax`).
3. **"Queue Item: Read" tampoco existe como privilegio**: la tabla `queueitem` no tiene privilegios propios (`EntityDefinitions(...)?$select=Privileges` devuelve lista vacía). Con `prvReadQueue` el usuario de aplicación ve los Queue Items del correo (1 de 1 releído).
4. **`prvSendAsUser` ("Send Email as Another User") no hizo falta** para enviar con la queue como remitente. Existe (profundidades Local, Deep y Global; no admite Basic). Condiciones del entorno: el mailbox de la queue `Spike` usa Server-Side Synchronization, está aprobado por el administrador de O365 (`isemailaddressapprovedbyo365admin = true`) y tiene el envío habilitado. No se probó en ningún otro entorno ni con una queue propiedad de un team.
5. **El remitente llega limpio**: `From` y `Sender` son `Buzon Dev PP 1 <buzondevpp1@…>` (la queue), sin "en nombre de". El Email saliente queda con propietario = el usuario que lo creó (el de aplicación), `sender` = la dirección de la queue e `inreplyto` = Message-ID del correo original (releído justo después de `SendEmail`; viene de crear con `parentactivityid`).
6. **`parentactivityid` exige `AppendTo` sobre el correo padre** (`prvAppendToActivity` con acceso al correo entrante, que pertenece a otro usuario) y el Email nuevo necesita `prvAppendActivity` (error al faltar: `is missing prvAppendActivity privilege …`).
7. **Profundidad**: para registros de **otro propietario** (correo entrante, queue, conversación) Basic no alcanza y el mensaje cambia a `user with id … does not have ReadAccess right(s) for record … of entity Activity. Consider assigning a role with the level BusinessUnitLevel to the user or team.` (igual con `AppendToAccess` para la queue, el correo entrante y la conversación). Para el Email **propio** (Create, Write, Append) basta Basic. No se probó si Basic cubre registros de un team al que el usuario pertenece.

### Qué quita la poda (cada privilegio retirado de a uno, con re-asociación; `evidencia/b_poda.json`)

| Privilegio retirado | Resultado | Mensaje exacto (extracto) |
|---|---|---|
| `prvSendAsUser` | envía | n/a: no es necesario |
| `prvAppendspk_conversacion` | envía | n/a: no es necesario (es el privilegio de la conversación como *padre* que adjunta hijos; aquí la conversación es el destino, y lo que pide es `AppendTo`) |
| `prvReadspk_conversacion` | envía | el paso de lectura previo falla (`is missing prvReadspk_conversacion privilege on OTC=11192 for entity 'spk_conversacion'`), pero la creación y el envío salen |
| Los 9 privilegios que trae todo rol nuevo (lectura de plugins, SDK messages y SharePoint) | envía | n/a: no son necesarios |
| `prvCreateActivity` | falla al crear | `is missing prvCreateActivity privilege … on OTC=10418 for entity 'adx_inviteredemption'` |
| `prvWriteActivity` | falla en `SendEmail` | `is missing prvWriteActivity privilege … on OTC=4216 for entity 'socialactivity'` |
| `prvAppendActivity` | falla al crear | `is missing prvAppendActivity privilege … on OTC=4204 for entity 'fax'` |
| `prvAppendToActivity` | falla al crear | `is missing prvAppendToActivity privilege … on OTC=4204 for entity 'fax'` |
| `prvReadActivity` | falla al leer el entrante | `is missing prvReadActivity privilege … on OTC=4251 for entity 'recurringappointmentmaster'` |
| `prvReadQueue` | falla al leer la queue | `is missing prvReadQueue privilege … on OTC=2020 for entity 'queue'` |
| `prvAppendToQueue` | falla al crear | `is missing prvAppendToQueue privilege … on OTC=2020 for entity 'queue'` |
| `prvAppendTospk_conversacion` | falla al crear | `is missing prvAppendTospk_conversacion privilege … on OTC=11192 for entity 'spk_conversacion'` |

(En cada mensaje el prefijo es `Principal user (Id=32122bbd-…, type=8, roleCount=1, privilegeCount=N, accessMode='4 Non-interactive', …), ` y el sufijo `context.Caller=…. Consider adding missed privilege to one of the principal (user/team) roles.`; el código HTTP es 403.)

### Lista final de privilegios mínimos (alimenta el rol "ARP Oficial")

Rol `SPK Oficial Correo`, 9 privilegios, releídos con `RetrieveRolePrivilegesRole`, y envío final verificado por Graph:

| Privilegio | Se le llama en el diseño | Profundidad mínima probada | Para qué |
|---|---|---|---|
| `prvCreateActivity` | Email: Create | Basic | Crear la respuesta |
| `prvWriteActivity` | Email: Write (el "Send" del diseño) | Basic | `SendEmail` actualiza el Email |
| `prvAppendActivity` | Email: Append | Basic | El Email referencia conversación, queue y correo padre |
| `prvReadActivity` | Email: Read | **Local** (BU) | Leer el correo entrante de otro propietario |
| `prvAppendToActivity` | Email: AppendTo | **Local** | `parentactivityid` (el entrante recibe un hijo) |
| `prvReadQueue` | Queue: Read | **Local** | La queue remitente |
| `prvAppendToQueue` | (no estaba en el diseño) | **Local** | `from` = queue |
| `prvAppendTospk_conversacion` | Conversación: AppendTo | **Local** | `regardingobjectid` |
| `prvReadspk_conversacion` | Conversación: Read | **Local** | **No lo pide la API**; se deja porque el formulario y el timeline de la conversación lo necesitan (no probado en pantalla) |

Cambios al texto del rol "ARP Oficial" de `definicion.md` (sin modificarlo): (1) quitar "Send" de Email; el envío lo cubre Write; (2) agregar **AppendTo de Queue**; (3) quitar "Queue Item: Read" (no existe; Read de Queue lo cubre); (4) Conversación: Append no hace falta para esta acción (puede seguir si otra función lo pide, que no es esta); (5) "Send Email as Another User" **no es necesario** en este entorno (ver D-28). Profundidad: Basic alcanza para Create, Write y Append del Email propio, pero como D-31 reasigna el Email a un team y varios oficiales leen los correos de los demás, **la profundidad BU del diseño sigue siendo la correcta** para Read, AppendTo y Write; la prueba con Basic solo demuestra el mínimo con registros propios.

### Impacto en D-28, D-12, D-19

- **D-28** ("el rol del oficial incluye el privilegio de enviar en nombre de otro"): se confirma el objetivo (un no administrador envía con la queue como remitente, en el hilo) y se corrige el medio: no es "Send Email as Another User" sino Read y AppendTo de la queue. Queda abierto lo que este spike no puede probar: el permiso **Send As de Exchange** de la identidad de sincronización (aquí el envío sale por Server-Side Sync con el mailbox aprobado por el administrador de O365) y una queue propiedad de un team.
- **D-12 y D-19**: ver la tarea A.
- **D-05**: reconfirmado que un no administrador puede crear la respuesta con `parentactivityid` (a nivel de API).

## Tarea C. App model-driven mínima

- App `Spikes Arreglos de Pago` (`spk_SpikesArreglosPago`), **`appid = 8f8b7e54-59c3-f111-a05a-000d3a35abca`**.
- **URL**: <https://org36e60d9d.crm.dynamics.com/main.aspx?appid=8f8b7e54-59c3-f111-a05a-000d3a35abca>
- Releído en la capa publicada (`s33_c_verificar_app.py`): `clienttype = 4` (Unified Interface), publicada 2026-10-08 21:02:43 UTC, estado activo. `RetrieveUnpublishedMultiple()` y `GET appmodules` coinciden. `RetrieveAppComponents` (resueltos a nombre lógico): `spk_conversacion`, `email` y el sitemap. `ValidateApp`: éxito, sin errores (solo el aviso esperado de que las tablas entran con todas sus vistas y formularios). Sitemap `spk_SpikesArreglosPagoSitemap`, `isappaware = true`, entradas "Conversaciones" (`spk_conversacion`) y "Correos" (`email`), títulos en 1033 y 3082. La app y el sitemap figuran en la solución `SpikesArreglosPago` (tipos 80 y 62). Roles con acceso: System Administrator y System Customizer (el usuario Randy Kauffman es System Administrator).
- **Formulario principal de `spk_conversacion`** (`df785489-…`): no tenía timeline. Se agregó una segunda columna (45 %) con una celda sin etiqueta y `rowspan` 14 que contiene `<control id="notescontrol" classid="{06375649-C143-495E-A496-C962E5B4488E}" />`, el mismo control que usan los formularios reales del entorno con timeline (el de HB incidente y el de la cuenta). Se verificó que `GET systemforms` antes de publicar **seguía mostrando el XML viejo** y que después de `PublishXml` de la tabla trae el control; y que la exportación de la solución lo contiene solo en `spk_conversacion`. **No pude ver el timeline renderizado** (no hay sesión de navegador de una persona): lo confirma la Prueba 1 de la guía. Si no aparece, la causa más probable es el comportamiento de renderizado del control, no el XML.

### Trampa reproducida: `AddAppComponents` con tablas

`AddAppComponents` con `{"@odata.type": "Microsoft.Dynamics.CRM.entity", "entityid": "<MetadataId>"}` responde 204 y **no agrega la tabla pedida: agrega siempre la tabla de metadatos `entity`** (`objectid` `9d0f025b-…`), ignorando el GUID. Probado con `email`, `spk_conversacion` y `account`, con el orden de las propiedades al revés, con `logicalname` y con la cabecera de solución. `ValidateApp` pasa en verde y lista las tablas verdaderas como "requeridas por el sitemap". `RemoveAppComponents` sí quitó la referencia basura. El tipo `entitymetadata` da 400 y `appmodulecomponents` no admite `POST`. `RetrieveAppComponents` sobre una app sin publicar devuelve 404 (solo ve la capa publicada), por lo que no se puede verificar antes de publicar.

Solución (la misma que usó el proyecto M-PPP): `ImportSolution`. Para no pisar el trabajo del otro agente se importó un **ZIP mínimo** (2,8 KB) con la misma solución y versión, cuya `solution.xml` lista solo el sitemap y la app, y cuyo `customizations.xml` trae solo `AppModuleSiteMaps` y `AppModules` con las tablas en `AppModuleComponents` (`s32_c_importar_app.py`). Import: 15 resultados `success`, 0 errores; **los 20 componentes de la solución antes y después, sin pérdidas ni altas**; el estado de los steps de plugin sin cambios. La importación **reemplazó** la lista de componentes de la app (desapareció la referencia basura a `entity`).

## Tarea D. Pruebas manuales (preparadas)

- Correo `[SPIKE4] Prueba manual` enviado de buzón 2 a buzón 1; apareció como Email a los 31 s (`80376bc1-5bc3-f111-a05a-000d3a35abca`); conversación `Prueba manual SPIKE4` (`8e376bc1-5bc3-f111-a05a-000d3a35abca`), con el Email en su Regarding (releído).
- Guía para la persona: `spikes/manual/GUIA.md` (links directos a la app y al registro de la conversación dentro de la app; pruebas 1, 2, 1b opcional y 3 con la salvedad de que se omite si no hay acceso al buzón 2).
- `verificar.py`, solo lectura. Antes de las pruebas imprime "PENDIENTE" en las tres; se probó además con los datos de la tarea B (`VERIFICAR_AUTOPRUEBA_CON_B=1`): la prueba 1 da CONFIRMADO con las respuestas enviadas, la 2 queda PENDIENTE porque no hay imagen y la 3 PENDIENTE porque el cliente no respondió. Qué hace por prueba:
  1. Para cada respuesta saliente con Regarding a la conversación: estado, `sender`, `parentactivityid` frente al correo original, `inreplyto`, y por Graph si llegó al buzón 2 (emparejando por Message-ID) con `In-Reply-To` igual al original.
  2. Dónde quedó la imagen: etiquetas `<img>` del cuerpo (`data:image` = Base64, `cid:` = adjunto, URL con `msdyn_richtextfiles(guid)`), filas de `msdyn_richtextfile` desde el envío (y las vinculadas a ese Email por `msdyn_parentid`, que puede venir con o sin llaves), `activitymimeattachment` con `objectid` = el Email y, por Graph, qué recibe el cliente (imágenes en el cuerpo y adjuntos).
  3. Si el buzón 2 respondió a la respuesta (empareja por `In-Reply-To` de sus Enviados, no por asunto), si el asunto cambió, y el Regarding del Email que se registró en Dataverse (más `correlationmethod` y `correlatedsubjectchanged`).
  Ejecución: `python3 verificar.py` (o con `--json` para guardar `evidencia/verificar_manual.json`).
- Los veredictos de estas tres pruebas **no están en este documento**: se agregan cuando el usuario avise.

## No probado o con límite

- Quick find en la interfaz y búsqueda por relevancia; equipo de Entra como miembro del perfil (tarea A).
- Send As de Exchange de la identidad de sincronización; queue propiedad de un team; Basic sobre registros de un team; otro entorno (tarea B).
- Timeline renderizado, Responder del timeline y del formulario, imagen pegada, y respuesta del cliente con asunto cambiado (tarea D, pendiente del usuario).
- Que `prvReadspk_conversacion` baste para abrir el formulario de la conversación: se deja por criterio, no está probado en pantalla.

## Tropiezos de método

- **Falso "falta el privilegio" por caché** (hallazgo 1 de la tarea B): costó tres reintentos hasta entender que había que re-asociar el rol.
- **Un defecto mío en `s23_b_probar.py`**: durante la poda, los envíos que sí salieron se borraron de Dataverse a continuación por un `else` mal ubicado (se pretendía borrar solo los borradores de intentos fallidos). Los correos ya habían salido y el resultado de cada prueba quedó registrado, pero esas copias no sobreviven en Dataverse. Está corregido. Las conclusiones de qué privilegios sobran no dependen de esas copias: las respalda la corrida **FINAL** con el conjunto mínimo, verificada por Graph.
- `RemovePrivilegeRole` es una acción **enlazada** al rol con el parámetro `Privilege` (objeto `privilege`), no con `PrivilegeId`; sin enlazar da 404. `RetrieveRolePrivilegesRole` puede tardar unos segundos en reflejar cambios recién escritos.
- El correo con asunto idéntico a varios intentos obliga a emparejar por Message-ID, no por asunto.

## Inventario de lo creado y limpieza

Dentro de la solución `SpikesArreglosPago`: rol `SPK Oficial Correo` (9 privilegios), app `Spikes Arreglos de Pago`, sitemap `spk_SpikesArreglosPagoSitemap`, y el cambio de formulario en `spk_conversacion` (viaja con la tabla). Fuera de la solución: el usuario de aplicación (`SPIKE4 SP - DEV - Pruebas de Correo`, con el rol `SPK Oficial Correo` y miembro del perfil `SPK Perfil Cedula Lectura`), y datos: Emails `[SPIKE4]` (y sus copias registradas por las queues), dos conversaciones.

1. `python3 spikes/manual/s99_limpieza.py` (ensayo en seco) lista todo. `--si-borrar` borra, en este orden: `msdyn_richtextfile` de esos Emails, Emails `[SPIKE4]` uno por uno (por la cascada huérfana del spike 3), conversaciones, app y sitemap, quita roles y pertenencia al perfil del usuario de aplicación y lo **desactiva** (no se borra: dejó huellas de auditoría en los correos; se comprobó que `isdisabled` se puede escribir y releer), y borra el rol `SPK Oficial Correo`.
2. **Ejecutarlo antes de `../plataforma/s99_limpieza.py --si-borrar`**, que borra las tablas (con ellas el cambio del formulario), `SPK Lector Cliente`, el perfil, la solución y el publisher.
3. Los correos en los buzones de Exchange pueden quedar.

## Archivos (`spikes/manual/`, sin secretos)

| Archivo | Para qué |
|---|---|
| `lib.py` | Clientes: administrador, principal de correo contra Dataverse, Graph con prefijo `[SPIKE4]` |
| `s00_usuario_app.py`, `s10_q2c.py` | Tarea A |
| `s20_b_preparar.py`, `s21_b_rol.py`, `s22_b_asignar.py`, `s23_b_probar.py`, `s24_b_verificar.py`, `s25_b_poda.py`, `s26_b_final.py` | Tarea B |
| `s30_c_formulario.py`, `s31_c_app.py`, `s32_c_importar_app.py`, `s33_c_verificar_app.py` | Tarea C (`s31` termina con la aserción que detectó la trampa de `AddAppComponents`; `s32` la resuelve) |
| `s40_d_preparar.py`, `GUIA.md`, `verificar.py` | Tarea D |
| `s99_limpieza.py` | Limpieza (ensayo en seco por defecto) |
| `evidencia/` | `q2c_resultado.json`, `b_intentos.jsonl`, `b_poda.json`, `rol_oficial_privilegios.json`, `b_envio_verificado.json`, `c_formulario_publicado.xml.json`, `c_import_customizations.xml.json` |
| `estado.json` | Ids de la corrida |

## Tarea D. Veredictos de las pruebas manuales (agregados el 2026-10-09)

El usuario hizo las pruebas 1, 2, 1b y 3 desde la app el 2026-10-08 y el 2026-10-09 (UTC). `verificar.py` se corrió después de cada una; su salida quedó en la conversación de trabajo y no en `evidencia/` (no se guardó con `--json`), y los correos de prueba se borraron con la limpieza del 2026-10-09, así que no se puede volver a correr.

| Prueba | Veredicto | Detalle |
|---|---|---|
| 1. Responder desde el timeline | **Confirmado** | La respuesta salió con `parentactivityid` = correo original e `In-Reply-To` correcto; el hilo se conservó en el buzón 2. El formulario deja poner la queue en "De", **pero no la propone por defecto**: el remitente por defecto es el usuario. De ahí el plugin pre-create que fija la queue como remitente |
| 2. Imagen pegada en la respuesta | **Confirmado: Base64 dentro del cuerpo** | Sin filas en `msdyn_richtextfile` ni `activitymimeattachment` para la imagen: viaja y se purga con el Email |
| 1b. Responder desde el formulario del correo | **Confirmado** (el script dijo REFUTADO: falso negativo) | El usuario respondió sobre su propia respuesta, no sobre el correo del cliente; `parentactivityid` quedó bien respecto del correo al que se respondió |
| 3. El cliente responde cambiando el asunto | **Confirmado** | Desde Outlook web (buzón 2), asunto "Otro asunto": conservó `In-Reply-To`, `References` y `Thread-Index`, y Dataverse lo correlacionó a su conversación (`correlationmethod` 3) |

Sin probar: Outlook de escritorio, Gmail y clientes móviles. Ruido del entorno: "Track emails sent between users as two activities" está activo, así que los correos entre buzones sincronizados aparecen duplicados (no pasa con clientes reales).
