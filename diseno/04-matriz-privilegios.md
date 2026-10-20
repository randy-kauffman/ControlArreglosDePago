# 04. Matriz de privilegios

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **aplicada en Dev y probada con un usuario no administrador** (2026-10-09, `spikes/roles/RESULTADO.md`). Fuente de cada celda: `definicion.md` §6 Seguridad, bloque **Roles** (líneas 381 a 396), D-04, D-17, D-18, D-28, D-31 y los spikes 3 y 4. Lo que la definición dejaba abierto se resolvió con una lectura de solo GET contra Dev el 2026-10-09 (sección 4). Lo que ni así se pudo cerrar queda como duda en la sección 5, **no como privilegio**.

Reglas citadas: BP-PP-002 (sin sharing manual), BP-PP-003 (pocas business units), BP-PP-182 y BP-PP-185 (nombre de un security role). Inventario: 6.2 a 6.5 de `06-inventario-componentes.md`.

Este documento es la fuente contra la que se verifican los cuatro playbooks de `playbooks/rol/`: un privilegio que un playbook trae y esta matriz no, o al revés, es un defecto de uno de los dos.

## 0. Notación

| Símbolo | Significa | Valor en el playbook |
|---|---|---|
| **B** | Basic: solo los registros propios | `usuario` |
| **L** | Local: la business unit (`Arreglos de Pago`, incluye los registros de sus teams) | `unidad` |
| **G** | Global: la organización | `organizacion` |
| vacío | el rol no tiene el privilegio | no se lista |

`Deep` (`unidad_e_hijas`) no se usa en ninguna celda: la BU `Arreglos de Pago` no tiene hijas.

**Profundidad por defecto: L (D-18, definición línea 381, "Profundidad BU").** Las dos excepciones salen de la definición y del spike 4: el Email propio (Create, Write, Append) va en **B** (línea 387; spike 4, `RESULTADO.md` tabla de mínimos), y `prvBulkDelete` solo admite **G** (lectura de Dev, sección 4).

Roles: **Ofi** = `SR - CARP - Oficial` (6.2) · **Jef** = `SR - CARP - Jefe` (6.3) · **Ser** = `SR - CARP - Servicio` (6.4) · **Pur** = `SR - CARP - Purga` (6.5).

## 1. Principios que ordenan la matriz

1. **Los roles viajan en la solución y se crean en la BU raíz.** La plataforma copia cada uno a la BU hija `Arreglos de Pago`, enlazado al original por `parentrootroleid` (definición línea 381; verificado en Dev el 2026-10-09). A los teams y usuarios de la BU se les asigna la copia de la hija.
2. **Asignación (decisión del 2026-10-09, línea 393).** `Oficial` va al **team del sistema de la BU**: todo usuario del área lo recibe por estar en ella. `Jefe` lleva **solo lo adicional** y va al team `Arreglos de Pago - Jefatura`: un jefe suma los dos roles. Las cuentas de servicio también están en la BU, así que **reciben Oficial** además de su rol propio; se acepta porque ninguno de sus privilegios es Delete ni toca columnas solo-sistema (las protege el plugin por identidad).
3. **Nadie humano borra.** Ningún rol humano tiene Delete sobre el agregado (Cliente, Conversación, Arreglo, Cuota, Generación de formato, Email). La única excepción humana es el Delete de **Mapeo de celda** del Jefe, que el plugin limita a versiones en Borrador (línea 394). El borrado del agregado ocurre por retención, con la cuenta de servicio `Purga` (D-18, línea 396).
4. **`escribir` es sobre el registro entero.** Qué columnas puede tocar cada rol lo limitan los plugins de solo-sistema y de transición (definición §6 Lógica), no el rol.
5. **Cada Custom API se restringe con un privilegio de una tabla propia que ya está en el rol que corresponde** (D-17, línea 400): generar formato con Read de Generación (Oficial y Jefe), "Tomar", unir e iniciar conversación nueva con Write de Conversación (Oficial y Jefe), reconciliación con Create de Conversación (solo Servicio). Los roles son previos a las API (inventario, orden de construcción 9). Los privilegios de ejecución de las API no se agregan acá: la API se apoya en estos. Servicio y Purga reciben el rol Oficial por el team del sistema y por eso también pueden ejecutar `generarformato`, "Tomar", unir e iniciar conversación nueva; **aceptado por el usuario el 2026-10-10 (OA-12)**: las API no tienen sentido de negocio para esas cuentas y la escritura queda atribuida.
6. **Rol base `App Opener` en los cuatro** (`rol.py` exige un rol base; es el mínimo recomendado de la plataforma). Se lee del entorno al construir; su lista no se guarda acá. Lo que aporta es lo que la app model-driven necesita para abrirse (en Dev, 81 privilegios; ninguno sobre tablas de ARP). Ver duda D-05.
7. **Los privilegios de Email son los de `Activity`**, compartidos por todas las actividades (spike 4, hallazgo 2, `RESULTADO.md` línea 63): `prvReadActivity` en L da lectura de toda actividad de la BU, no solo de Email. No se puede acotar más desde el rol.

## 2. Tablas propias de la solución

Nombres exactos leídos de Dev (`EntityDefinitions(...)?$select=LogicalName,Privileges`, 2026-10-09). Las siete son `UserOwned`; todo privilegio admite B, L, D y G.

| Tabla | Privilegio | Ofi | Jef | Ser | Pur | Origen |
|---|---|---|---|---|---|---|
| Cliente | `prvCreatesanic_carp_tbl_cliente` | L | | | | Línea 383. El oficial crea el cliente (D-31) |
| Cliente | `prvReadsanic_carp_tbl_cliente` | L | | | L | Línea 383. Purga: línea 396 |
| Cliente | `prvWritesanic_carp_tbl_cliente` | L | | | | Línea 383 |
| Cliente | `prvAppendsanic_carp_tbl_cliente` | L | | | | Línea 383 |
| Cliente | `prvAppendTosanic_carp_tbl_cliente` | L | | | | Línea 383. Lo piden los lookups de Conversación y Arreglo (REL-01, REL-02) |
| Cliente | `prvDeletesanic_carp_tbl_cliente` | | | | L | Línea 396. "Clientes sin arreglos ni conversaciones" |
| Conversación | `prvCreatesanic_carp_tbl_conversacion` | | | L | | Línea 395. Es el privilegio de ejecución de la API de reconciliación (línea 400). El oficial **no** lo tiene: las conversaciones las crea el sistema (línea 384) |
| Conversación | `prvReadsanic_carp_tbl_conversacion` | L | | L | L | Línea 384. Servicio: línea 395. Purga: línea 396. Spike 4: Read Local para el formulario y el timeline |
| Conversación | `prvWritesanic_carp_tbl_conversacion` | L | | | | Línea 384. Privilegio de ejecución de "Tomar", unir e iniciar conversación nueva (línea 400) |
| Conversación | `prvAppendsanic_carp_tbl_conversacion` | L | | | | Línea 384. Lo pide el lookup a Cliente (REL-01) |
| Conversación | `prvAppendTosanic_carp_tbl_conversacion` | L | | | | Línea 384. Spike 4: lo exige el `regardingobjectid` del Email, mínimo Local |
| Conversación | `prvDeletesanic_carp_tbl_conversacion` | | | | L | Línea 396. Purga borra primero los Emails y después la conversación (línea 346) |
| Arreglo | `prvCreatesanic_carp_tbl_arreglo` | L | | | | Línea 385 |
| Arreglo | `prvReadsanic_carp_tbl_arreglo` | L | | L | L | Línea 385. Servicio: línea 395. Purga: línea 396 |
| Arreglo | `prvWritesanic_carp_tbl_arreglo` | L | | | | Línea 385 |
| Arreglo | `prvAppendsanic_carp_tbl_arreglo` | L | | | | Línea 385. Lo piden los lookups a Cliente, Conversación y Versión (REL-02, 03, 04) |
| Arreglo | `prvAppendTosanic_carp_tbl_arreglo` | L | | | | Línea 385 |
| Arreglo | `prvDeletesanic_carp_tbl_arreglo` | | | | L | Línea 396. Retención de 24 meses |
| Cuota | `prvReadsanic_carp_tbl_cuota` | L | | | L | Línea 386. Purga: línea 396 |
| Cuota | `prvWritesanic_carp_tbl_cuota` | L | | | | Línea 386. El plugin limita la escritura a las columnas de pago. Servicio **no** la recibe: el respaldo de D-11 no se activa (inventario 11.3) |
| Cuota | `prvDeletesanic_carp_tbl_cuota` | | | | L | Línea 396 |
| Versión de plantilla | `prvCreatesanic_carp_tbl_versionplantilla` | | L | | | Línea 394 |
| Versión de plantilla | `prvReadsanic_carp_tbl_versionplantilla` | L | | | | Línea 390. El Jefe la tiene por Oficial |
| Versión de plantilla | `prvWritesanic_carp_tbl_versionplantilla` | | L | | | Línea 394. Es también el permiso que comprueba la vista previa (línea 342) |
| Versión de plantilla | `prvAppendsanic_carp_tbl_versionplantilla` | | L | | | Línea 394 |
| Versión de plantilla | `prvAppendTosanic_carp_tbl_versionplantilla` | L | | | | Línea 390, "para fijar el lookup del arreglo" (REL-04). El Jefe lo tiene por Oficial |
| Mapeo de celda | `prvCreatesanic_carp_tbl_mapeocelda` | | L | | | Línea 394 |
| Mapeo de celda | `prvReadsanic_carp_tbl_mapeocelda` | L | | | | Opción A del usuario (2026-10-09): el team del sistema es dueño de todo y la plataforma exige que el dueño lea la tabla (`spikes/roles/RESULTADO.md`). El Jefe lo recibe por el Oficial |
| Mapeo de celda | `prvWritesanic_carp_tbl_mapeocelda` | | L | | | Línea 394 |
| Mapeo de celda | `prvAppendsanic_carp_tbl_mapeocelda` | | L | | | Línea 394. Lo pide el lookup a Versión (REL-08) |
| Mapeo de celda | `prvDeletesanic_carp_tbl_mapeocelda` | | L | | | Línea 394, "solo sobre Mapeo en Borrador": el límite lo pone el plugin (inventario 7.16) |
| Generación de formato | `prvReadsanic_carp_tbl_generacionformato` | L | | | L | Línea 391. Privilegio de ejecución de generar formato (línea 400). Purga: línea 396 |
| Generación de formato | `prvDeletesanic_carp_tbl_generacionformato` | | | | L | Línea 396 |

**Mapeo de celda: el Oficial lee, el Jefe escribe.** El Oficial tiene `Read` por la opción A del usuario del 2026-10-09 (la plataforma exige que el team dueño lea la tabla; `spikes/roles/RESULTADO.md`; duda D-01). El Jefe no tiene `Read` propio: le llega por el Oficial, porque el team Jefatura suma los dos roles. ~~El Oficial no tiene ningún privilegio y el Jefe tampoco tiene `Read` (línea 390, "Sin acceso a Mapeo").~~ Texto corregido el 2026-10-10 (inconsistencia 12 de `03-contratos-custom-api.md`); rige la tabla de arriba.

**Lo que ningún rol tiene, por tabla** (no se omitió por descuido):

- Cliente: Share. Conversación: Assign, Share, Delete humano. Arreglo: Share, Delete humano. Cuota: Create, Append, AppendTo, Assign, Share, Delete humano (las altas y bajas las hace el sistema, D-08 y D-26). Versión de plantilla: Assign, Share, Delete. Mapeo de celda: AppendTo, Assign, Share (Read lo tiene el Oficial; el Jefe lo recibe por él). Generación de formato: Create, Write, Append, AppendTo, Assign, Share, Delete humano (la crea el sistema con el `InitiatingUserId`, D-26).
- Share: ninguno. No hay sharing manual (BP-PP-002, D-18).

## 3. Tablas estándar y otros privilegios

Nombres exactos leídos de Dev el 2026-10-09. En el playbook van en `otros_privilegios` (la herramienta solo acepta como `tablas` las de la solución).

| Tabla estándar | Privilegio | Ofi | Jef | Ser | Pur | Origen |
|---|---|---|---|---|---|---|
| Email | `prvCreateActivity` | B | | | | Línea 387. Spike 4: mínimo probado, Basic |
| Email | `prvWriteActivity` | B | | | | Línea 387. **No existe un privilegio "Send"**: `SendEmail` exige Write (spike 4, hallazgo 2) |
| Email | `prvAppendActivity` | B | | | | Línea 387. El Email referencia conversación, queue y correo padre (spike 4) |
| Email | `prvReadActivity` | L | | L | L | Línea 387. Spike 4: mínimo Local para leer el correo entrante de otro propietario. Servicio: línea 395. Purga: línea 396 |
| Email | `prvAppendToActivity` | L | | | | Línea 387. Spike 4: `parentactivityid` exige AppendTo Local sobre el correo padre |
| Email | `prvDeleteActivity` | | | | L | Línea 396, "Email con sus adjuntos y Queue Item". **Ningún rol humano**: el correo es la evidencia de RF-03 (línea 387) |
| Queue | `prvReadQueue` | L | | L | | Línea 389. Spike 4: mínimo Local. Servicio: es el medio de "leer Queue Item" (sección 4, resolución R-03) |
| Queue | `prvAppendToQueue` | L | | | | Línea 389, **lo exige la plataforma para poner la queue como remitente** (spike 4, hallazgo del intento 1; D-28) |
| Usuario | `prvAppendToUser` | L | | | | Prueba de roles del 2026-10-09 (D-07): la plataforma lo exige para poner un usuario en cualquier lookup (403 `is missing prvAppendToUser`) |
| (organización) | `prvReadRecordAuditHistory` | G | | | | **Agregado el 2026-10-20 por decisión del usuario (M7, opción A)**: View Audit History, para que el Oficial vea la historia de auditoría nativa del arreglo (`diseno/07` DA-21). **Solo admite G** (Dev). El Jefe lo recibe por el Oficial. Antes: 403 en `RetrieveRecordChangeHistory` con un Oficial no admin (`pruebas-humo/tanda2b/RESULTADO.md`) |
| Mailbox | `prvReadMailbox` | | | L | | Línea 395. Oficial: "Sin acceso al Mailbox" (línea 392) |
| (organización) | `prvBulkDelete` | | | | G | Línea 396. **Solo admite G** (lectura de Dev, sección 4). El alcance del borrado lo sigue limitando el Delete de cada tabla, que es L |
| Attachment | (ninguno) | | | | | La definición pide "crear y leer" (línea 388) y "con sus adjuntos" (línea 396). **`activitymimeattachment` y `attachment` no tienen privilegios propios** (sección 4, R-01): el acceso lo da el Email |
| Queue Item | (ninguno) | | | | | La tabla no tiene privilegios propios (spike 4, hallazgo 3; Dev, R-02) |
| `msdyn_richtextfile` | (ninguno) | | | | | Línea 388: no se otorga; el editor de correo no la usa (spike 4, D-34) |

## 4. Lo que se resolvió con la lectura de Dev (solo GET, 2026-10-09)

| # | La definición dejaba abierto | Qué se leyó en Dev | Resolución |
|---|---|---|---|
| R-01 | "Los privilegios exactos de Attachment se confirman en la construcción" (línea 388; inventario 6.2; incógnita de la línea 539) | `EntityDefinitions(LogicalName='activitymimeattachment'` y `'attachment')?$select=Privileges` devuelve `[]` y `OwnershipType = None`. `privileges?$filter=contains(name,'MimeAttachment')` devuelve `[]` | **No hay privilegios que asignar**: crear y leer un adjunto de correo lo cubren `prvCreateActivity` y `prvReadActivity` del Email. Queda sin probar de punta a punta (duda D-03) |
| R-02 | Queue Item: "Read" (Servicio) y "Delete" (Purga) | `queueitem` tiene `Privileges: []`. Es lo mismo que halló el spike 4 (hallazgo 3) | Sin privilegio propio. Servicio lee los Queue Items con `prvReadQueue` (spike 4: "con `prvReadQueue` el usuario de aplicación ve los Queue Items"). Purga no recibe nada por Queue Item (duda D-04) |
| R-03 | Qué privilegio es "leer Queue Item" para Servicio | Spike 4, hallazgo 3 | `prvReadQueue` en L |
| R-04 | Base probada de Email, Queue y Conversación del Oficial | Rol `SPK Oficial Correo` de la BU raíz (`roleid fc47a380-57c3-f111-a05a-000d3a35abca`), `RetrieveRolePrivilegesRole`: **9 privilegios**, coinciden uno a uno con `spikes/manual/RESULTADO.md`: `prvCreateActivity` B, `prvWriteActivity` B, `prvAppendActivity` B, `prvReadActivity` L, `prvAppendToActivity` L, `prvReadQueue` L, `prvAppendToQueue` L, `prvReadspk_conversacion` L, `prvAppendTospk_conversacion` L | Los 7 estándar entran en Oficial tal cual. Los 2 de `spk_conversacion` son los de `sanic_carp_tbl_conversacion` (Read y AppendTo, Local). El Oficial agrega encima lo que la definición pide y el spike no probaba |
| R-05 | Nombres exactos de los privilegios de las siete tablas | `EntityDefinitions` de cada una: los ocho tipos (`Create`, `Read`, `Write`, `Delete`, `Assign`, `Share`, `Append`, `AppendTo`), con `prv<Tipo><tabla>` y los cuatro alcances admitidos | Los de la sección 2 |
| R-06 | `BulkDelete` | `privileges?$filter=contains(name,'BulkDelete')`: único `prvBulkDelete`, `canbeglobal = true` y los otros tres alcances en `false` | Purga lo recibe en G; es el único privilegio de la matriz que no sigue la profundidad BU |
| R-07 | Mailbox | `prvReadMailbox` existe con los cuatro alcances | Servicio, L |
| R-08 | Qué trae `App Opener` | Rol raíz `979144c2-989b-41a5-a146-6b3ea4130e8c`, 81 privilegios. Incluye `prvReadUser` y `prvReadTeam` en G, `prvFlow` y `prvWorkflowExecution` en G. **No** incluye ningún privilegio de Activity, Queue, Mailbox, de BulkDelete ni de las tablas de ARP. Hay una segunda copia en la BU hija (`0c218ba3-8281-4ec2-4c96-eca6c14223ab`) | Ninguna fila de esta matriz se solapa con la base. La herramienta busca por nombre y BU raíz |

## 5. Dudas y hallazgos (no se resuelven acá)

Ninguna se cerró con un privilegio inventado. Cada una dice qué la cierra.

| # | Duda | Cita | Qué la cierra |
|---|---|---|---|
| **D-01 (resuelta por la opción A del usuario: Read de Mapeo en el Oficial, el Jefe lo recibe por él)** | **El Jefe no tiene `Read` sobre Mapeo de celda.** Se le dan Create, Write, Append y Delete sobre un Mapeo que no puede leer, y el Oficial (que el Jefe también tiene) "no tiene acceso a Mapeo". Sin Read no se abre el formulario ni el subgrid de Mapeo en la Versión de plantilla (inventario 12.20, 12.21), ni se puede editar ni borrar una fila. Es un hallazgo de la matriz | Definición líneas 390 y 394; inventario 12.20 y 12.21 | El aprobador decide agregar `prvReadsanic_carp_tbl_mapeocelda` (L) al Jefe. Mientras no lo decida, el playbook no lo trae |
| **D-02 (refutada por la prueba de roles del 2026-10-09: Assign no hace falta; se quitó de todos los roles humanos)** | **El Jefe crea Versión de plantilla y Mapeo, pero no tiene Assign.** P-11 extiende el plugin que fija el team del sistema como propietario a las siete tablas, y la definición dice que el Oficial necesita Assign "solo" en Cliente y Arreglo, "se confirma en el spike". Si fijar el propietario en un Create exige Assign (es la razón por la que Cliente y Arreglo lo llevan), Versión y Mapeo lo necesitarían. No consta que el spike lo haya confirmado | Definición línea 253 y D-31 (línea 490); diccionario P-11 (línea 663) | Una prueba en Dev con un usuario no administrador con el rol Jefe creando una Versión. Si falla, es un hallazgo de la matriz |
| **D-03 (cerrada por la prueba de roles: el adjunto funciona con los privilegios de Activity)** | **Adjuntos de correo sin prueba.** Dev dice que no hay privilegios propios, pero ningún spike adjuntó un archivo con un rol no administrador (el rol del spike 4 tiene 9 privilegios y ninguno de adjuntos). Existe además `activityfileattachment` (Dev: tabla `UserOwned` con sus ocho privilegios), que la definición no nombra; no se sabe si el editor de correo la usa | Definición líneas 388 y 539; inventario 6.2 | Un envío con adjunto, y una recepción con adjunto leída, hechos con un usuario con `SR - CARP - Oficial`. Si pide `prvCreateactivityfileattachment` o similar, es un hallazgo de la matriz |
| **D-04** | **Purga y Queue Item.** Queue Item no tiene privilegios. Que borrar el Email limpie los Queue Items (línea 346, spike 3) no consta que se haya probado con el rol `Purga` y no con un administrador: puede que la cascada pida `prvDeleteQueue` o `prvReadQueue` | Definición líneas 346 y 396 | La prueba de la retención en Test (inventario 13.12) |
| **D-05** | **Rol base `App Opener` en Jefe, Servicio y Purga.** La herramienta exige un rol base. En Jefe duplica lo que ya trae Oficial (la definición dice "nada se duplica", línea 393). Es inocuo (la base no toca tablas de ARP), pero no es "solo lo adicional" en sentido estricto | `rol.py` (`CLAVES`); definición línea 393; receta `seguridad/patrones.md` §2.1 | Que el aprobador confirme que la base es `App Opener` en los cuatro, o indique otra |
| **D-06 (resuelta: queda Basic. El spike 4, `spikes/manual/RESULTADO.md` línea 96, da Basic como profundidad mínima probada de `prvWriteActivity`)** | **Email en B para Write.** La definición pone Write en B (línea 387). El spike 4 concluye que, como varios oficiales leen los correos de los demás, "la profundidad BU del diseño sigue siendo la correcta para Read, AppendTo y Write" (`RESULTADO.md` línea 105). Se sigue la definición. Si un oficial debe editar o reenviar un borrador que escribió otro, o uno que pasó al team (D-31), B no alcanza | Definición línea 387; spike 4 línea 105 | Prueba con dos oficiales distintos. Si falla, Write pasa a L en el Oficial |
| **D-07 (confirmada por la prueba de roles: se agrega `prvAppendToUser` al Oficial)** | **Lookups a `systemuser`.** Arreglo guarda cinco lookups a usuario (`sanic_oficialid`, requerido, y los de formalizó, descartó, anuló y reabrió), Conversación dos y Versión uno. `prvAppendToUser` y `prvAppendUser` existen y **`App Opener` no los trae** (Dev). La definición no los pide. Muy probablemente no hagan falta (lookups a usuario), pero no hay prueba | Dev (R-08); diccionario sección 6.2 | La prueba de alta de un Arreglo por un oficial no administrador |
| **D-08** | **Cascada de Assign.** Arreglo es padre parental de Cuota y de Generación (Cascade All). El Oficial tiene Assign en Arreglo y no en las hijas; no se sabe si la cascada exige el privilegio en la hija. La asignación a un propietario distinto del team la rechaza el plugin, así que en la práctica el caso es raro | Diccionario REL-05 y REL-06; definición línea 385 | La misma prueba de D-02 y D-07 |
| **D-09 (PENDIENTE, 2026-10-10, OA-3)** | **`SR - CARP - Servicio` y los trabajos de eliminación masiva.** El flujo recurrente de salud (inventario 11.2) debe comprobar los trabajos de eliminación masiva fallidos en las últimas 24 h (OA-3 de `03-contratos-custom-api.md`). Probablemente Servicio necesite leer esos trabajos (System Jobs) y la matriz no le da ningún privilegio para ello. **No se sabe cuál es el privilegio ni si hace falta**: no se agrega nada al rol ni a `playbooks/rol/` hasta verificarlo | Decisión OA-3 del 2026-10-10; definición, Operación (comprobaciones del flujo recurrente) | Al construir el flujo 11.2 en Dev, con un usuario con `SR - CARP - Servicio`: consultar los trabajos de eliminación masiva fallidos de las últimas 24 h. Si falla por privilegio, es un hallazgo de la matriz y se agrega entonces. Hasta que el flujo exista, revisión manual semanal de System Jobs |

## 6. Conteo por rol

Privilegios **propios del playbook** (los que se suman a `App Opener`).

| Rol | Sobre tablas de la solución | Otros privilegios | Total |
|---|---|---|---|
| `SR - CARP - Oficial` | 20 | 9 | **29** |
| `SR - CARP - Jefe` | 7 | 0 | **7** |
| `SR - CARP - Servicio` | 3 | 3 | **6** |
| `SR - CARP - Purga` | 10 | 3 | **13** |

Desglose del Oficial: Cliente 6, Conversación 4, Arreglo 6, Cuota 2, Versión de plantilla 2, Generación de formato 1; Email 5, Queue 2, Usuario 1, View Audit History 1 (2026-10-20, M7). De los 29, 9 son los del rol probado en el spike 4.

## 7. Cómo se verifica

- Cada playbook trae la lista exacta de privilegios esperados y se verifica con `rol.py --solo-verificar`: el rol tiene **exactamente** los de `App Opener` más los del playbook; uno de menos, uno de más u otro alcance es `difiere`.
- La comprobación independiente contra el XML exportado (`muestra_rol.py`) compara exactos los privilegios sobre las tablas de la solución.
- Una prueba que lea esta matriz y la compare celda por celda con los cuatro bloques JSON es deseable (receta de `power-platform-especificar`, `rol.md` §4). Hoy el proyecto no la tiene.
- **Pruebas de comportamiento, con un usuario no administrador por rol** (después de construir): las de las dudas D-02, D-03, D-06 y D-07; que un Oficial no pueda borrar nada; que un Oficial no pueda crear una Conversación ni ejecutar la API de reconciliación; que Servicio y Purga no puedan hacer lo que no figura. **Tras cambiar un rol ya asignado, quitar y volver a asociar el rol al usuario**: la caché de privilegios del principal no se refresca sola (spike 4, hallazgo 1).
- La asignación (Oficial al team del sistema, Jefe al team Jefatura, Servicio y Purga a las cuentas de servicio) y la revisión de roles ajenos con el administrador son tareas 13.6, 13.7 y 13.9 del inventario.

## 8. Fuera de esta matriz

- Los privilegios de ejecución de las cinco Custom API: inventario 8.2 a 8.6, diseño detallado pendiente. La definición los fija sobre privilegios de tabla que ya están en esta matriz (sección 1, punto 5).
- El perfil de seguridad de columna (inventario 6.1) y su asociación a los teams (13.8): los roles no dan ni quitan acceso a `sanic_cedula`, `sanic_cuentadebitar` ni `sanic_instantaneacedula`. Por eso Desbloquear y Reabrir de un no administrador dependen de que el perfil dé actualizar sobre `sanic_instantaneacedula` (2026-10-20, hallazgo de la tanda 2 de humo; ver diccionario §8).
- El acceso a la app model-driven (va con la app, 12.25) y las vistas y el dashboard de supervisión del Jefe (12.1 a 12.16): son componentes de la app, no privilegios del rol.
- Auditoría de la asignación de roles (13.11) y la verificación de roles ajenos (13.9).
- **2026-10-20, M7 (decisión del usuario, opción A):** se agregó `prvReadRecordAuditHistory` (Global) a `SR - CARP - Oficial`; es el único cambio de rol de esa fecha. El Jefe lo hereda porque también tiene el Oficial (por el team del sistema). No se agregó `prvReadAuditSummary`. Prueba con un no administrador: `pruebas-humo/lote-0/RESULTADO-auditoria.md`.
