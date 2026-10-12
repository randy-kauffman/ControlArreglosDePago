# Verificación de los supuestos de plataforma V-1 a V-7 (2026-10-10)

Entorno de desarrollo (`https://org36e60d9d.crm.dynamics.com/`, único entorno tocado). Cierra la sección 0.9 de `diseno/03-contratos-custom-api.md`. Todo con datos ficticios `ZZV-…`. Scripts en esta carpeta; la evidencia, sin secretos, en `evidencia/*.json`.

## Método

- **Plugin de prueba**, compilado en Linux (`net462`, `dotnet build`) y registrado como paquete `sanic_ZZVProbe` **fuera de la solución** `sanic_carp_sol_controlarreglospago` (sin cabecera `MSCRM.SolutionUniqueName`). Dos clases (proyecto en `src/Zzv.Probe/`):
  - `ZzvObserva`: solo escribe en `ITracingService` lo que ve (mensaje, etapa, modo, profundidad, `UserId`, `InitiatingUserId`, mensaje padre, parámetros, pre-imágenes). Los textos solo se imprimen si empiezan con `ZZV-`.
  - `ZzvComando`: ejecuta una orden escrita en `Cliente.sanic_nombrecompleto` (`Update` de una columna, `Update` con `statecode` y `statuscode`, `SetStateRequest`, `Update` de un lookup o de un booleano) con `CreateOrganizationService(null)` (SYSTEM) o con el usuario del contexto, y traza el resultado o la excepción completa. Nunca relanza.
- **Observación sin tocar la org**: el Plugin Trace Log de la organización ya estaba en `All` (valor 2). No se cambió; se leyó `plugintracelogs` y al final se borraron las trazas del spike. Los steps (18 en total, nombre `ZZV-…`) se registraron por Web API: `Update` con filtro `statecode,statuscode` en PreOperation y PostOperation, `SetState` y `SetStateDynamicEntity` en ambas etapas, `Update` con filtro de columna y pre-imagen, y `Update` sin filtro y con filtro `sanic_archivo` sobre la Versión.
- **Usuarios**: administrador (service principal del estudio, `System Administrator`); no administrador: el usuario de aplicación de correo (`PP_MAIL_*`), solo con el rol Oficial heredado del team por defecto de la BU. Se habilitó y se agregó temporalmente al perfil `CSP - CARP - Datos sensibles` (V-6) y al team `Arreglos de Pago - Jefatura` (V-7); todo devuelto (sección Limpieza).
- **Atribución de trazas**: cada traza se asigna a su paso por `performanceexecutionstarttime` (ms). `createdon` no sirve: la plataforma escribe las trazas por lotes con hasta 30 s de atraso.
- **Todo se verificó releyendo**: el estado de las filas, los pasos de steps e imágenes, la pertenencia a la solución y los miembros de perfil y team. Un 2xx nunca se tomó como prueba.

## Resultados

| Supuesto | Veredicto | Evidencia | Qué significa para el diseño |
|---|---|---|---|
| **V-1** Un `Update` con `statecode`/`statuscode` dispara los steps de `Update` con esos filtros, venga del formulario, de `Xrm.WebApi` o de `SetStateRequest` | **CONFIRMADO**, con tres matices | PATCH (= `Xrm.WebApi` = formulario) con ambas columnas, en Conversación y Arreglo: se ejecutaron `Update` PreOperation (20) y PostOperation (40), `MessageName = "Update"`, profundidad 1. `SetStateRequest` (SDK dentro de un plugin, como usuario y como SYSTEM): también `Update` pre y post, a profundidad 2, con `statecode` y `statuscode` en el `Target`. `Update` por SDK con ambas columnas: igual. Control: un PATCH de otra columna no dispara los steps de estado (solo el de su columna). `evidencia/30_estado_v1_v2_v5.json` | (1) El step `SetState` **no hace falta y nunca se dispara**: con steps de `SetState` registrados en ambas etapas en las dos tablas, las evidencias suman 0 trazas con `MessageName = "SetState"` frente a 42 de `SetStateDynamicEntity` y 67 de `Update`. El "Plan: registrar además un step `SetState` por tabla" del supuesto no aporta nada. (2) Cuando cambia el estado, la plataforma dispara además `SetStateDynamicEntity` (pre y post) un nivel más abajo; un `SetStateRequest` llega a los plugins **como `SetStateDynamicEntity`**, con `FromSandbox = True`, y de ahí cae en un `Update`. Un plugin de `Update` no debe asumir profundidad 1. (3) Un PATCH con **solo `statecode`** llega al `Target` sin `statuscode` (ver V-5). Un PATCH con solo `statuscode` (mismo estado) dispara `Update` y no `SetStateDynamicEntity`. El Web API no tiene la acción `SetState` (404 en la forma ligada y en la no ligada) |
| **V-2** El código (SYSTEM) puede hacer `Update` sobre una fila **inactiva** de Conversación y Arreglo (columnas que no son el estado) | **CONFIRMADO** | Dentro del plugin, `WhoAmI` del servicio `CreateOrganizationService(null)` devuelve el usuario de nombre `SYSTEM`. Sobre una Conversación Finalizada y un Arreglo Terminado: `Update` de `sanic_correonuevoenfinalizada` (true y false), de `sanic_detallesinrespuesta`, de `sanic_comentarios` y del lookup `sanic_conversacionid` (repuntado), todos sin error y releídos con el valor nuevo y el estado intacto. Comparación: el administrador por Web API, el administrador por SDK y **un usuario Oficial sin privilegios de administrador** también pueden, con el mismo resultado | La plataforma **no bloquea** las escrituras en filas inactivas de estas dos tablas, ni a SYSTEM ni a las personas. La bandera `RechazarEscriturasEnInactivas` (por defecto `false`) y cualquier regla de "solo lectura" de un Terminado o Finalizada tiene que imponerla un plugin; la plataforma no la da. RF-15 y el repunte de arreglos terminados al unir quedan viables. Un Oficial puede además reabrir un Arreglo Terminado y una Conversación Finalizada con un PATCH |
| **V-3** `email.statuscode`: Received = 4, Sent = 3; `directioncode` falso = entrante | **CONFIRMADO** | `StatusAttributeMetadata` de `email`: 1 Draft, 2 Completed, 3 Sent, 4 Received, 5 Canceled, 6 Pending Send, 7 Sending, 8 Failed. `BooleanAttributeMetadata` de `directioncode`: falso = Incoming, verdadero = Outgoing. Contraste de solo lectura con los correos de Dev: 12 con `statuscode` 4, todos `directioncode` falso y `statecode` 1; 535 con `statuscode` 3, todos verdadero y `statecode` 1; 1 borrador (1) con verdadero. No se envió ni creó ningún correo. `evidencia/10_metadatos.json` | Los valores del diseño son correctos. Ambos están en `statecode` 1 (Completed): un correo entrante ya llega cerrado |
| **V-4** Existe `sdkmessagefilter` para `CommitFileBlocksUpload` sobre `sanic_carp_tbl_versionplantilla` | **REFUTADO** (hay un sustituto que sí funciona) | `CommitFileBlocksUpload`, `InitializeFileBlocksUpload` y `UploadBlock` tienen un único filtro cada uno, con `primaryobjecttypecode = none` e `iscustomprocessingstepallowed = false`; ninguno para la tabla. Registrar un step: 400 `Custom SdkMessageProcessingStep is not allowed on the specified message and entity` (con el filtro `none`) y 400 `… is not allowed on the message with SdkMessageId …` (sin filtro). Para la tabla sí hay filtro, con step permitido, en `Update`, `Retrieve`, `Create`, `Delete`, `Assign`, `SetState`, `SetStateDynamicEntity`, `GrantAccess`, `ModifyAccess`, `RevokeAccess`, `RetrievePrincipalAccess`, entre otros, y ninguno de archivo. Sustituto probado: al subir un archivo (por bloques con `InitializeFileBlocksUpload`/`UploadBlock`/`CommitFileBlocksUpload`, o con un PATCH a la columna) la plataforma ejecuta un **`Update`** de la Versión, con `MessageName = "Update"`, mensaje padre `CommitFileBlocksUpload` y `Target` con `sanic_archivo` (el id del archivo) más el id. Un step de `Update` con filtro `sanic_archivo` (PreOperation) se disparó con la subida y no con un PATCH de `sanic_huella`. `evidencia/60_v4_archivo.json`, `61_extras_trazas.json` | **El step S-27 no puede registrarse sobre `CommitFileBlocksUpload`.** Se puede hacer como un step de `Update` en la Versión con filtro `sanic_archivo` (o leyendo `Target.Contains("sanic_archivo")`). Ojo: cuando se **reemplaza** un archivo con PATCH directo, la plataforma hace primero un `Update` como SYSTEM (`InitiatingUserId` = el llamador) que deja `sanic_archivo` en nulo y luego otro del llamador con el id nuevo; el plugin debe distinguir las dos pasadas. No se probó el rechazo con `InvalidPluginExecutionException` dentro de ese step |
| **V-5** `statecode` Activo→Inactivo y la reapertura Terminado→Vigente aceptados como un único `Update` con ambas columnas | **CONFIRMADO** | Arreglo Vigente (0 / 159460001) → Terminado (1 / 2) con un PATCH de `statecode` y `statuscode`: relectura 1 / 2. Terminado → Vigente con un PATCH de `statecode = 0, statuscode = 159460001`: HTTP 204, relectura `statecode` 0, `statuscode` 159460001. También Anulado (1 / 159460002) → Vigente, y por SDK como SYSTEM (`Update` con ambas columnas). Variante: un PATCH de **solo `statecode = 0`** sobre un Terminado deja `statuscode = 1` (Borrador), no Vigente. En Conversación, solo `statecode = 1` deja `statuscode = 2` (Finalizada) y solo `statecode = 0` deja 1 (Abierta) | La reapertura de un Terminado **debe llevar las dos columnas**; con solo `statecode` el arreglo vuelve a Borrador (el estado por defecto del `statecode` 0). Es lo que el diseño ya prevé, pero conviene que la validación de la reapertura compruebe que el `Target` trae `statuscode` |
| **V-6** Una pre-imagen incluye columnas con seguridad por columna con su valor real para el usuario del perfil | **PARCIAL** | Step de `Update` sobre Arreglo (PreOperation) con pre-imagen que pide `sanic_cuentadebitar` y `sanic_instantaneacedula`. (a) Administrador: las dos columnas con su valor real. (b) Usuario no administrador **con** el perfil: las dos con su valor real; si el `Target` trae la columna protegida, el `Target` trae el valor nuevo y la pre-imagen el anterior. (c) Usuario no administrador **sin** el perfil: **las dos columnas no están en la pre-imagen** (no vienen en nulo, directamente no existen en la entidad), aunque la fila las tiene; la lectura directa por Web API da `null`. SYSTEM (código de un plugin) también las ve. Prueba extra: el mismo step con `impersonatinguserid` = el administrador y el llamador (c) sin perfil: la pre-imagen trae las dos columnas con su valor real (`UserId` = administrador, `InitiatingUserId` = el llamador). `evidencia/40_v6_pre_imagen.json`, `40_v6_lecturas_directas.json`, `61_extras_trazas.json` | Para el usuario del perfil se cumple. Pero la pre-imagen se filtra con la seguridad por columna **del usuario con el que corre el step**: un llamador sin perfil la recibe sin esas columnas, y una detección de cambios (DC-01) que compare "ausente" con "presente" vería un cambio que no existe. Si el step de Arreglo se registra para correr **en el contexto de un usuario con el perfil** (campo `impersonatinguserid` del step), la pre-imagen trae los valores reales para cualquier llamador (probado con un administrador; no con una cuenta de servicio con solo el perfil). La alternativa es leer las columnas protegidas con SYSTEM dentro del plugin. El diseño sigue diciendo que el perfil se da al team del área: no se probó la asociación por team, solo la directa al usuario |
| **V-7** `RetrievePrincipalAccess` responde `WriteAccess` para el llamador sobre una versión | **CONFIRMADO** | Función ligada a `systemuser` con `Target` = la Versión. Administrador: `ReadAccess, WriteAccess, AppendAccess, AppendToAccess, CreateAccess, DeleteAccess, ShareAccess, AssignAccess`; un PATCH real: 204. Usuario Jefe (rol `SR - CARP - Jefe` por el team Jefatura): `ReadAccess, WriteAccess, AppendAccess, AppendToAccess, CreateAccess`; un PATCH real: 204. Usuario solo Oficial: `ReadAccess, AppendToAccess`, sin `WriteAccess`; un PATCH real: 403. La llamada del propio usuario no administrador con su propio id da la misma respuesta que la del administrador indicando el principal. `evidencia/50_v7_principal_access.json` | El supuesto se sostiene y la respuesta coincide con lo que la plataforma deja hacer de verdad en los tres casos. El llamador no administrador puede preguntar por sí mismo sin elevar privilegios. Al agregar al usuario al team Jefatura el cambio se vio en el primer intento, sin espera |

## Hallazgos no obvios

1. **`SetStateRequest` llega al plugin como `SetStateDynamicEntity`**, nunca como `SetState`, y de ahí genera un `Update` anidado con `statecode` y `statuscode` en el `Target`. Los steps de `SetState` no se ejecutan en ningún camino probado, aunque existan los filtros para estas tablas.
2. **Un `Update` de estado no trae siempre las dos columnas**: con solo `statecode` el `Target` no lleva `statuscode`, y la plataforma elige el estado por defecto (Borrador para el arreglo, Abierta o Finalizada para la conversación).
3. **La pre-imagen respeta la seguridad por columna del usuario del step**, no la del sistema: sin perfil faltan las columnas (V-6).
4. **Las filas inactivas se pueden escribir** (V-2), también por una persona con el rol Oficial. Toda inmutabilidad de un Terminado o Finalizada depende de plugins.
5. **El archivo se sube con un `Update` oculto**: `CommitFileBlocksUpload` no admite steps, pero provoca un `Update` con `sanic_archivo` en el `Target` (V-4).
6. **Los timestamps de las trazas engañan**: `createdon` es el momento del volcado en lote (hasta 30 s de atraso); la hora real está en `performanceexecutionstarttime`. La cabecera `Date` del servidor y la hora del plugin difieren cerca de 1 s.

## Tropiezos

- La primera corrida de V-1 atribuía las trazas por `createdon`: quedaban en el paso equivocado. Se abortó (sus filas quedaron registradas en `estado.json` y se borraron en la limpieza) y se repitió con atribución por `performanceexecutionstarttime`. Un `pkill -f` con el nombre del script mató además el shell de trabajo; sin consecuencias en Dev.
- El nombre primario de la Versión admite 10 caracteres (el de Conversación, 20; el de Cliente, 200): la primera corrida de V-6 falló al crear la fila de prueba por eso, antes de tocar nada más.
- `sanic_cedula` y `sanic_nombrecompleto` son obligatorias en Cliente, y Arreglo exige tres lookups (cliente, oficial, versión). La fila de prueba de Arreglo necesita los tres.

## Lo que no se pudo probar o queda abierto

- **V-1, formulario**: no hay navegador. Se tomó el PATCH del Web API como equivalente de `Xrm.WebApi` y del guardado del formulario (usan el mismo mensaje `Update`).
- **V-1, `SetStateRequest`** solo se ejecutó desde dentro de un plugin (SDK en sandbox). No se probó desde un cliente SDK externo (`ServiceClient`).
- **V-6**: el perfil se asoció al usuario directamente, no por el team del área; la pre-imagen con `impersonatinguserid` se probó solo con el administrador; la pre-imagen en PostOperation para el llamador sin perfil no se probó (sí en PreOperation).
- **V-4**: no se probó un `InvalidPluginExecutionException` desde el step de `Update` filtrado por `sanic_archivo`, ni la subida por un usuario no administrador.
- **Rastro de auditoría**: las tablas tienen auditoría y la organización la tiene activa; los registros de auditoría de las filas de prueba (creación, cambios y borrado de Conversación, Arreglo y Versión) **no se pueden borrar** por el Web API y quedan en `audit`. Contienen solo valores `ZZV-…`.

## Limpieza (releída con `v91_verificar_limpieza.py`, `evidencia/90_limpieza_verificada.json`)

| Elemento | Antes | Después |
|---|---|---|
| Componentes de la solución `sanic_carp_sol_controlarreglospago` | 26 | 26, ninguno nuevo y ninguno faltante |
| Paquete `sanic_ZZVProbe`, ensamblado `Zzv.Probe`, sus 2 tipos | no existían | GET devuelve 404 en los tres; la lista de paquetes y de ensamblados no administrados es idéntica a la inicial |
| Steps `ZZV-…` (18) con sus imágenes | no existían | 0 |
| Filas de prueba (19 registradas; Cliente, Versión, Conversación, Arreglo) | tablas vacías | tablas vacías (también Cuota, Generación de formato y Mapeo) |
| Trazas de `Zzv.Probe.*` en `plugintracelogs` | 0 | 0 (180 borradas; se repitió la lectura 40 s después) |
| `plugintracelogsetting` de la organización | 2 (`All`) | 2: nunca se cambió |
| Usuario de aplicación de correo | deshabilitado, `accessmode` 4, BU `Arreglos de Pago`, sin roles propios, solo en el team por defecto | idéntico (comparado campo por campo) |
| Perfil `CSP - CARP - Datos sensibles`: miembros | 0 usuarios, 0 teams | 0 usuarios, 0 teams |
| Team `Arreglos de Pago - Jefatura`: miembros | 0 | 0 |
| Registros fuera de lo anterior | — | sin Custom API `ZZV`, sin correos creados ni enviados, sin cambios en roles ni en metadatos |

## Cómo reproducir

`python3 v00_linea_base.py` (línea base) → `v10_metadatos.py` (V-3, V-4 metadatos) → compilar `src/Zzv.Probe` con `dotnet build -c Release` → `v20_registrar_paquete.py` → `v21_registrar_steps.py` → `v30_estado_v1_v2_v5.py` (V-1, V-2, V-5) → `v40_v6_pre_imagen.py` (V-6, habilita al usuario y lo agrega al perfil) → `v50_v7_principal_access.py` (V-7) → `v60_v4_archivo.py` y `v61_extras.py` (V-4, V-6 con suplantación) → `v90_limpieza.py` y `v91_verificar_limpieza.py`. `reasignar.py` reatribuye trazas de un JSON de corrida. Las credenciales salen de las variables y archivos ya configurados del estudio (`herramientas/credenciales.py`); ningún script imprime secretos ni tokens.


## V-8 (2026-10-11): autonumérico de Arreglo en el Create

Misma infraestructura y mismas reglas que V-1 a V-7 (Dev, único entorno; plugin fuera de la solución; datos `ZZV-…`; todo releído). Pregunta: la primaria `sanic_nombre` de `sanic_carp_tbl_arreglo` es autonumérica (`ARR-{SEQNUM:6}`, 20 caracteres, opcional, válida para Create y Update). ¿Qué trae el `Target` de un `Create` en cada etapa, con y sin valor del llamador? Cierra el "supuesto sin verificar en Dev" de `diseno/03-contratos-custom-api.md` §2.5 (`ArregloCrearStep`, paso 2).

### Veredicto

| Supuesto | Veredicto | Evidencia | Qué significa para el diseño |
|---|---|---|---|
| **V-8** "La plataforma completa el autonumérico **después** del PreOperation de Create, así que el `Target` solo trae `sanic_nombre` si lo envió el cliente" (diseno 03, §2.5 `ArregloCrearStep`, paso 2) | **REFUTADO** | Sin enviarlo, el `Target` ya trae `sanic_nombre = ARR-00xxxx` en **PreValidation (10)**, en PreOperation (20) y en PostOperation (40); la fila guarda ese valor (A1, A3, A4, A5, N1, N3). Enviándolo (`ZZV-NOMBRE`), el `Target` trae `ZZV-NOMBRE` en las tres etapas y **la fila guarda `ZZV-NOMBRE`**, no el autonumérico (A2, N4). `null` y `''` equivalen a no enviarlo (A4, A5): la plataforma pone el autonumérico. El `Target` tiene la misma forma con y sin valor del llamador (las mismas 22 claves en PreValidation). | El paso 2 tal como está escrito **rechazaría todas las altas** (la clave `sanic_nombre` siempre está). Hay que reescribir la regla: ver "Qué significa para el diseño". |

### Resultados por caso

Etapas observadas por `ZzvNombre` (steps `Create` en 10, 20 y 40, síncronos; `Update` en 20 con filtro `sanic_nombre`). "Fila" es la relectura con `GET` después de cada paso. Detalle por traza en `evidencia/70_v8_autonumerico.json` (y las trazas completas en `70_v8_trazas.json` y `70_v8_trazas_2.json`).

| Caso | Quién | El llamador envía | `Target.sanic_nombre` en 10 / 20 / 40 | Fila releída |
|---|---|---|---|---|
| A1 | admin | nada | `ARR-001002` / `ARR-001002` / `ARR-001002` | `ARR-001002` |
| A2 | admin | `ZZV-NOMBRE` | `ZZV-NOMBRE` en las tres | **`ZZV-NOMBRE`** (se guarda el de la persona) |
| A3 | admin | nada (tras A2) | `ARR-001003` en las tres | `ARR-001003`: el valor enviado en A2 **no consumió** número |
| A4 / A5 | admin | `null` / `''` | `ARR-001004` / `ARR-001005` en las tres | igual |
| A6 (extra) | admin | `Update` con `sanic_nombre = ZZV-UPD` | PreOperation trae `ZZV-UPD` (y solo `sanic_nombre` + auditoría) | `ZZV-UPD`: **el Update también lo permite** |
| N3 | no admin (Oficial) | nada | `ARR-001013` en las tres | `ARR-001013` |
| N4 | no admin | `ZZV-NOMBRE` | `ZZV-NOMBRE` en las tres | **`ZZV-NOMBRE`** |
| N1 / N2 | no admin | nada / `ZZV-NOMBRE`, con las dos columnas protegidas | 10 y 20 con el valor; **no hay PostOperation** | **403** `does not have create permissions to … secured field on entity Arreglo`: el Oficial sin perfil no puede enviar `sanic_cuentadebitar` / `sanic_instantaneacedula` (V-6). El error ocurre **después** de PreOperation. Sin fila |

Con la forma de la plataforma:

| Caso | Quién | El llamador envía | Fila releída |
|---|---|---|---|
| B0 | admin | `ARR-777777` | **`ARR-777777`** (se guarda; la plataforma no valida el formato) |
| B0b | admin | nada | `ARR-001007`: el secuencial siguió donde iba, no saltó a 777778 |
| N5 | no admin | `ARR-777778` | **`ARR-777778`** |

Qué puede hacer un plugin con la columna (steps de prueba de rango 2, de a uno; los observadores de rango 1 ven el antes y las etapas siguientes ven el después):

| Caso | Plugin | El llamador envía | Lo que ven PreOp / PostOp | Fila releída |
|---|---|---|---|---|
| B1 | **quita** `sanic_nombre` en **PreValidation** | `ZZV-NOMBRE` | `ARR-001008` / `ARR-001008` | **`ARR-001008`**: la plataforma **vuelve a asignar** el autonumérico |
| B1b | quita en PreValidation | nada | PreVal tenía `ARR-001009`; PreOp ve `ARR-001010` | `ARR-001010`: **se descartó el 001009** (un número perdido por alta) |
| B2 / B2b | **quita** en **PreOperation** | `ZZV-NOMBRE` / nada | PostOp: la clave **no está** | **vacío (`null`)** en ambos: en PreOperation la plataforma ya no completa. Peligroso: deja la primaria sin valor |
| B3 / B3b | **fija** `ZZV-FIJO` en PreOperation | nada / `ZZV-NOMBRE` | PostOp ve `ZZV-FIJO` | `ZZV-FIJO` en ambos: sobrescribir en PreOperation funciona |

Los números se consumen aunque el alta falle o el plugin los descarte: el 403 de N1 gastó `ARR-001006` y no quedó fila con él.

### Respuestas a las tres preguntas

1. **Sin valor del llamador**: sí, el `Target` trae `sanic_nombre`, con el autonumérico ya asignado (`ARR-001002`), tanto en PreValidation como en PreOperation.
2. **Con valor del llamador** (`ZZV-NOMBRE`): el `Target` lo trae tal cual y **se guarda ese valor**; el autonumérico no se asigna ni se consume. Lo mismo para un valor con la forma de la plataforma (`ARR-777777`) y para el no administrador.
3. **PostOperation**: el `Target` trae el valor definitivo (el autonumérico, o el de la persona; o `ZZV-FIJO` si un plugin lo fijó). Salvo que un plugin lo haya quitado en PreOperation, y entonces no hay clave y la fila queda vacía.

### Qué significa para el diseño

- **El paso 2 de `ArregloCrearStep` (§2.5) está mal planteado**: `sanic_nombre` figura siempre en el `Target`, así que "si trae alguna columna solo-sistema ⇒ `CARP-ARR-009`" rechazaría todas las altas (el diseño ya decía "hay que escalarlo"). La presencia de la clave **no distingue** a la persona de la plataforma: el `Target` de PreValidation es idéntico en forma con y sin valor del llamador, y en el `Target` no hay ninguna marca que diga quién puso el valor (no se miraron las variables compartidas del contexto).
- **Única diferencia observable: el formato.** El valor de la plataforma cumple `^ARR-\d{6}$` (formato `ARR-{SEQNUM:6}`; no se probó qué pasa si la secuencia pasa de 999999); el de una persona puede ser cualquier cosa. Una persona que envíe exactamente `ARR-nnnnnn` es **indistinguible** de la plataforma en el Create (B0, N5), también siendo no administrador.
- **Etapa**: la decisión se puede tomar en PreValidation o en PreOperation si solo se rechaza (el valor está en el `Target` en ambas). Si se quiere **neutralizar** en lugar de rechazar, tiene que ser en **PreValidation**: quitar la clave allí hace que la plataforma asigne un autonumérico nuevo (B1), y quitarla en PreOperation deja la primaria vacía (B2). El costo de quitar siempre en PreValidation es un número perdido por cada alta en que el llamador no envió nada (B1b); los huecos ya son normales (altas fallidas, N1).
- **Opciones, con su compromiso**: (a) rechazar en el Create (`CARP-ARR-009`) cuando el valor **no** cumple `^ARR-\d{6}$`: claro para el cliente, pero deja pasar un `ARR-nnnnnn` falsificado; (b) quitar `sanic_nombre` del `Target` en PreValidation para toda persona (la plataforma reasigna): hermético, sin error, con huecos en la secuencia y sin avisar al cliente que mandó el valor; (c) combinar las dos, con ambos costos. Decisión del usuario.
- **Update**: aquí sí se distingue sin ambigüedad. La plataforma no agrega `sanic_nombre` a un `Update`, así que si el `Target` lo trae, lo mandó una persona (A6); un step de `Update` con filtro `sanic_nombre` lo rechaza limpiamente (la plataforma no lo impide por sí sola: la fila quedó con `ZZV-UPD`).
- **Hallazgo lateral**: un Oficial sin el perfil `CSP - CARP - Datos sensibles` no puede **crear** un Arreglo que envíe `sanic_cuentadebitar` o `sanic_instantaneacedula` (403 de seguridad por columna, después de PreOperation; N1, N2). El alta del Borrador por una persona exige que esas columnas estén en su perfil con permiso de creación, o que no se envíen. No se probó qué permisos concede el perfil.

### Lo que no se probó

- El `Create` se hizo siempre por Web API (`POST`), que el diseño toma como equivalente de `Xrm.WebApi` y del formulario (V-1). No se probó un `Create` desde un plugin ni desde un cliente SDK; otro camino podría completar el autonumérico en otro punto. No se probó un `Create` de Arreglo desde un formulario real (no hay navegador).
- No se probó si el número de `GetNextAutoNumberValue` serviría para distinguir (sería una carrera con altas concurrentes).

### Limpieza (releída con `v70_91_verificar_limpieza.py`, `evidencia/70_limpieza_verificada.json`)

| Elemento | Antes | Después |
|---|---|---|
| Componentes de la solución `sanic_carp_sol_controlarreglospago` | 26 | 26, ninguno nuevo y ninguno faltante (también se comparó justo después de registrar el paquete y los steps) |
| Paquete `sanic_ZZVProbe`, ensamblado `Zzv.Probe`, sus 3 tipos | no existían | GET devuelve 404 en los cinco; la lista de paquetes y de ensamblados no administrados es idéntica a la inicial |
| Steps `ZZV-…` (7: 4 observadores y 3 mutadores) | 0 | 0 (sin imágenes); los steps sobre Arreglo vuelven a ser 38, todos de la plataforma |
| Filas de prueba (20: Arreglo, Cliente, Versión) | todas las tablas vacías | todas las tablas vacías (también Conversación, Cuota, Generación de formato y Mapeo) |
| Trazas de `Zzv.Probe.*` | 0 | 0 (59 borradas; se repitió la lectura 25 s después) |
| `plugintracelogsetting` de la organización | 2 | 2: no se cambió |
| Usuario de aplicación de correo | deshabilitado, `accessmode` 4, BU `Arreglos de Pago`, sin roles propios, solo en el team por defecto | idéntico, comparado campo por campo (`usuario_igual: true`) |
| Perfil `CSP - CARP - Datos sensibles` y team Jefatura: miembros | 0 y 0 | 0 y 0 (no se tocaron) |

Efectos que **no** se pueden revertir: (1) el contador del autonumérico de Arreglo quedó adelantado (el último número usado fue `ARR-001013`): el primer Arreglo real de Dev saldrá con `ARR-001014` o más (no se reinició la semilla: sería cambiar configuración de la org); (2) las filas de auditoría de las filas de prueba (valores `ZZV-…`, `ARR-…`) quedan en `audit`.

### Cómo reproducir

`python3 v70_00_linea_base.py` → compilar `src/Zzv.Probe` (`dotnet build -c Release`; la clase nueva es `Nombre.cs`) → `v70_10_registrar.py` → `v70_20_v8_autonumerico.py` (administrador y no administrador; habilita al usuario) → `v70_30_actualizar_y_mutadores.py` (versión 1.0.3.0 con los modos `#quitar` y `#fijar`) → `v70_40_v8_mutadores_y_no_admin.py` → `v70_90_limpieza.py` y `v70_91_verificar_limpieza.py`. `v70_lib.py` reutiliza `vlib.py` y guarda su estado en `estado_v70.json` (solo GUIDs; no toca `estado.json`). `Zzv.Probe.csproj` pasó a la versión de ensamblado 1.0.3.0.
