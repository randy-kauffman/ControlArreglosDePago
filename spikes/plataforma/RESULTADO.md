# Spike 2 de plataforma: resultados

Proyecto 2026-003 "Control de Arreglos de Pago". Fecha de ejecución: 2026-10-08.

## Alcance y método

- Entorno: `https://org36e60d9d.crm.dynamics.com/` (comprobado con `pac org who`: Org "Dev", Environment ID `9c8994b8-b1bc-ee23-9184-2f6346502613`). No se tocó ningún otro entorno.
- Autenticación: credenciales de cliente del estudio, leídas de `~/projects/pp_secrets.env` (mismo mecanismo que `dataverse_api.py` del proyecto 2026-001). El cliente `dv.py` se niega a correr si `DATAVERSE_URL` no es la URL autorizada. Ningún secreto ni token se imprimió ni se guardó. La cuenta de servicio es `# PowerPlatform-Dev` (rol System Administrator).
- Contenedor: solución no administrada `SpikesArreglosPago`, publisher propio `spikesarreglospago` (prefijo `spk`, prefijo de opciones 92838 asignado por el sistema).
- Regla de verificación aplicada (`herramientas/patrones.md` §2.2): ningún resultado se afirma por el código de respuesta. Cada afirmación se hizo releyendo el estado real. Dos casos del propio spike confirman la regla: un alta de cuotas que devolvió 400 con la propiedad de navegación mal escrita (se detectó porque la relectura vino vacía), y un `PATCH` de la vista Quick Find que devolvió 204 y cuya relectura inmediata seguía mostrando el valor viejo hasta publicar.
- No se modificó nada de la organización, ni otras soluciones, ni usuarios, ni roles de terceros. Lo único fuera de las tablas de prueba que se creó (siempre dentro de la solución y sin asignar a nadie): el rol `SPK Lector Cliente` y el perfil de columna `SPK Perfil Cedula Lectura`.

## Tabla de veredictos

| # | Pregunta | Resultado | Evidencia resumida | Impacto en el diseño |
|---|---|---|---|---|
| 1 | Clave alterna (producto, número de producto, número de arreglo) con número vacío | **CONFIRMADO** | Clave `Active` a los 135 s. Tres filas `(Préstamo, P-001, vacío)` aceptadas (204) y releídas. Duplicado con número informado: HTTP 412 `Entity Key ... violated`. Formalizar un borrador a un número libre: 204; a uno ya usado: 412 | **D-33 se cierra**: la clave sirve, el plan B (clave de texto con GUID) no se activa. Matiz: con valores nulos la clave no impone unicidad (dice la doc y se observó), así que dos borradores idénticos son posibles |
| 2a | Cédula con seguridad por columna dentro de una clave alterna | **REFUTADO** | Crear la clave sobre la columna segura: 400 `must not be protected by FLS`. Asegurar una columna que ya está en una clave: 400 `not securable as it is part of entity keys` | **D-07 se confirma**: la unicidad de la cédula va por plugin, no por clave alterna |
| 2b | Columna segura como find column de la vista Quick Find | **CONFIRMADO** (a nivel de metadato) | La vista aceptó `spk_cedula` (ya segura) y también `spk_correo` agregada antes de asegurarla; relectura del `fetchxml` tras publicar muestra ambas | Se puede configurar. Que funcione para un usuario concreto es 2c |
| 2c | Un principal sin perfil de columna, ¿encuentra la fila buscando por la cédula con quick find? | **NO PROBADO** | La cuenta de servicio es System Administrator (ve todo, ver doc); como administrador el filtro de quick find sobre la cédula encuentra la fila, lo cual no prueba nada. No existe en el entorno un principal humano sin ese rol y con Read sobre una tabla custom (ver "Qué hace falta") | **D-12 y D-19 siguen abiertos**; el plan B de D-19 no se activa ni se descarta. Queda un script listo (`s65`) |
| 3 | Concurrencia optimista (`If-Match` con ETag viejo) | **CONFIRMADO** | `PATCH` con `If-Match` del ETag viejo: 412 `The version of the existing record doesn't match the RowVersion property provided`. Relectura: la fila conservó el valor del oficial A y el ETag vigente. Dos "Tomar" con el mismo ETag: el primero 204, el segundo 412 | **D-04 se cierra** para la Custom API "Tomar" |
| 4 | Columna de fórmula con `Now()`/`UTCNow()` | **CONFIRMADO con matices** | Fórmula creada, releída (`SourceType = 3`) y evaluada al leer (5.02 -> 5.03 entre lecturas). `Now()` exige columna fecha-hora UserLocal; `UTCNow()` exige TimeZoneIndependent; mezclar da 405 | **D-27 se cierra**, con tres ajustes (abajo): behavior de la columna base, `DateDiff` cuenta límites de hora, y la doc no soporta definir fórmulas por código |
| 5 | Operadores de fecha relativa sobre DateOnly | **CONFIRMADO con matices** | `olderthan-x-days` solo no sirve (valor > 0 y omite "ayer"); `last-x-days` incluye hoy; `lt` solo con fecha literal. La combinación `olderthan-x-days 1` OR `yesterday` devuelve exactamente fecha < hoy, también dentro de un filtro relacionado `link-type="any"` y en una vista guardada | **D-11**: la vista funciona sin código; **el respaldo con flujo diario no se activa**. Riesgo residual de zona horaria (abajo) |
| 6 | `msdyn_richtextfile` | **CONFIRMADO** la estructura; **NO PROBADO** el llenado por el editor | Existe, es UserOwned, 42 columnas, sin filas. Se vincula al padre solo por tres columnas de texto, sin relación ni lookup | **D-34**: la purga por correo es posible solo filtrando por texto. Hay que verificar con una imagen real que el editor llene esas columnas |
| 7 | `maxuploadfilesize` y `blockedattachments` | **CONFIRMADO** | `maxuploadfilesize = 5242880` (5 MiB); lista de 104 extensiones bloqueadas (valores por defecto) | **RNF-11** coincide con el supuesto de 5 MB. Se acuerda con el administrador antes de Prod |

## Evidencia y análisis por pregunta

### Pregunta 1. Clave alterna con valores nulos (D-33)

Tabla `spk_arreglo`: `spk_producto` (choice local, Préstamo 928380000 / Tarjeta 928380001), `spk_numeroproducto` (texto 50), `spk_numeroarreglo` (entero, opcional). Clave `spk_clave_arreglo` sobre las tres. Script: `s03_clave.py`, `s30_q1_clave_nulos.py`.

- `POST EntityDefinitions(LogicalName='spk_arreglo')/Keys` -> 204. Relectura cada 10 s: `EntityKeyIndexStatus` pasó por `Pending` y quedó `Active` a los **135 s** con la tabla vacía (la receta del estudio estima 110 a 120 s). `KeyAttributes` se devuelve ordenado alfabéticamente.
- Altas, con la clave `Active` (todas releídas con `$filter=startswith(spk_nombre,'Q1-')`):

| Fila | Valores | Resultado |
|---|---|---|
| A1, A2, A3 | Préstamo, P-001, vacío (tres borradores iguales) | 204, 204, 204; releídas las tres con número `None` |
| B1 | Préstamo, P-001, 1 | 204 |
| B2 (duplicado) | Préstamo, P-001, 1 | **412** `Entity Key Clave arreglo violated. A record with the same value for Numero de arreglo, Numero de producto, Producto already exists.`; no existe en la relectura |
| C1, C2 | Tarjeta, P-001, vacío / 1 | 204, 204 (otro producto, mismo número, no choca) |

- Formalizar (flujo real: `PATCH spk_numeroarreglo`): A1 a 2 -> 204; A2 a 1 (ya de B1) -> **412** igual que el duplicado; A2 a 3 -> 204. Relectura final: A1=2, A2=3, A3 vacío, B1=1, C1 vacío, C2=1.
- `GET spk_arreglos(spk_producto=928380000,spk_numeroproducto='P-001',spk_numeroarreglo=1)` -> 200, devuelve B1.
- Doc oficial, coincide: "When NULL values are used in alternate key columns, uniqueness will not be enforced" (https://learn.microsoft.com/power-apps/maker/data-platform/define-alternate-keys-reference-records#limits-in-creating-alternate-keys).

Conclusión: la clave de D-33 funciona como se diseñó. Consecuencia que conviene escribir en la definición: la clave **no protege a los borradores** entre sí (dos borradores del mismo producto y número son válidos para la plataforma); protege los números formalizados, incluso en la carrera de dos formalizaciones simultáneas (la segunda recibe 412). Si el negocio no quiere dos borradores del mismo crédito, es regla de plugin, no de clave.

### Pregunta 2. Seguridad por columna (D-19, D-12, D-07)

Tabla `spk_cliente`, columna `spk_cedula` (texto 20, `IsSecured = true`, agregada **después** de la tabla por `/Attributes`, como indica la receta del estudio). Scripts: `s60` a `s66`.

**2a, en los dos sentidos** (`s61_q2a_clave.py`):
- Clave sobre la columna ya segura: `400 Attribute - Name : spk_cedula ... Attributes defined for EntityKey must not be protected by FLS`. Relectura de `Keys`: lista vacía.
- Clave sobre `spk_numerocliente` (común) -> 204; luego `PUT IsSecured=true` sobre esa columna -> `400 The field Numero de cliente is not securable as it is part of entity keys ( Clave numero cliente ). Please remove the field from all entity keys to make it securable.` Relectura: `IsSecured = False`.
- Doc: "Columns that have the Enable column security property enabled can't be used as an alternate key" (https://learn.microsoft.com/power-apps/maker/data-platform/define-alternate-keys-reference-records). La columna primaria tampoco admite seguridad (`CanBeSecuredForRead = false`, observado; la doc lo dice).

**2b** (`s62_q2b_quickfind.py`): la vista Quick Find (`querytype 4`, `isquickfindquery = true`) creada por la plataforma tenía como find column solo `spk_nombre`. Se agregó `spk_cedula` (segura) -> la relectura tras publicar muestra `['spk_nombre', 'spk_cedula']`. Sentido inverso: se agregó `spk_correo` (común) a las find columns y después se aseguró (`PUT IsSecured=true` -> 204, relectura `IsSecured = True`), sin rechazo. La columna sigue en las find columns.

**2c** (`s63_q2c.py`): con la cuenta de servicio (System Administrator) el filtro `like` de quick find sobre la cédula encuentra la fila (`001-000000-0002%` -> `Q2-beto`; `%0001A` -> `Q2-ana`). Eso solo demuestra que el administrador ve todo. La doc de seguridad por columna lo advierte: "Column-level security doesn't apply for users who have the system administrator role... To verify the configured results, you must use an account that doesn't have the system administrator security role" (https://learn.microsoft.com/power-platform/admin/field-level-security).

*Qué hace falta para cerrar 2c.* Los únicos roles con Read sobre una tabla custom recién creada son System Administrator, System Customizer, Service Reader/Writer y Support User; quienes los tienen sin ser administradores son identidades de servicio de Microsoft (por ejemplo `# Microsoft Copilot Studio`), que no corresponde usar. Los usuarios `# User 1..10` no tienen privilegios sobre tablas custom. Habría que asignar un rol a un usuario existente o crear un usuario de prueba, lo que está fuera de lo autorizado. Queda preparado, sin asignar:
1. Rol `SPK Lector Cliente` (Read sobre `spk_cliente` y `spk_arreglo`, nivel organización), en la solución.
2. Perfil `SPK Perfil Cedula Lectura` (Read sobre `spk_cliente.spk_cedula`), en la solución.
3. `s65_q2c_sin_admin.py`: usa la cabecera `MSCRMCallerID` desde la cuenta de servicio, de modo que no hace falta iniciar sesión como el usuario. Se corre en dos escenarios: **B** usuario con el rol y sin perfil (se espera cédula enmascarada, y hay que ver si la búsqueda la encuentra: si la encuentra, hay un canal para deducir valores); **A** usuario también en el perfil (se espera que la encuentre; es el caso real del oficial).

Observación para el diseño: el caso que importa a RF-13 es el **A** (oficial con perfil que sí ve la cédula y debe poder buscarla); el **B** es el control negativo. La doc afirma que Dataverse search respeta la seguridad por columna ("A user that doesn't have permission to the restricted column won't be able to search or see results for the column", https://learn.microsoft.com/power-apps/user/relevance-search#understand-search-results), pero no dice nada equivalente de quick find. Sin esa prueba, el plan B de D-19 sigue pendiente.

### Pregunta 3. Concurrencia optimista (D-04)

`s40_q3_etag.py` sobre una fila de `spk_arreglo`:

| Paso | Resultado |
|---|---|
| Lectura 0 | `@odata.etag = W/"12561651"` |
| `PATCH` oficial A, sin `If-Match` | 204; relectura: ETag `W/"12561653"`, nombre de A |
| `PATCH` oficial B con `If-Match: W/"12561651"` (viejo) | **412** `The version of the existing record doesn't match the RowVersion property provided.` |
| Relectura | nombre de A intacto, ETag y `versionnumber` sin cambio |
| `PATCH` con el ETag vigente | 204; relectura con el valor nuevo y ETag distinto |
| Dos "Tomar" con el mismo ETag | primero 204, segundo **412**; relectura: gana el primero |
| `If-Match: *` | 204: no protege, solo exige que exista (no usarlo) |

`IsOptimisticConcurrencyEnabled = True` en `spk_arreglo`, `email` y `msdyn_richtextfile`. El spike usó el Web API; la Custom API de D-04 correrá en un plugin con el SDK (`ConcurrencyBehavior.IfRowVersionMatches` y `RowVersion`), que es el mismo mecanismo según la doc (https://learn.microsoft.com/power-apps/developer/data-platform/optimistic-concurrency y https://learn.microsoft.com/power-apps/developer/data-platform/webapi/perform-conditional-operations-using-web-api#apply-optimistic-concurrency); esa ruta del SDK no se ejercitó en el spike.

### Pregunta 4. Columna de fórmula con `Now()` (D-27)

Tabla `spk_conversacion`: `spk_primercorreo` (fecha-hora UserLocal) y `spk_primercorreotzi` (fecha-hora TimeZoneIndependent). Scripts `s21` a `s25`.

- `POST EntityDefinitions(LogicalName='spk_conversacion')/Attributes` con `SourceType: 3` y `FormulaDefinition: "DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)"` (Decimal, precisión 0) -> 204. Relectura: `SourceType = 3`, `SourceTypeMask = 1` (referencia columnas del mismo registro, válida), `FormulaDefinition` igual al enviado, `IsValidForCreate/Update = false`.
- Variantes:

| Columna | Fórmula | Resultado |
|---|---|---|
| `spk_antignow_plano` (Decimal 0) | `DateDiff(spk_primercorreo, Now(), TimeUnit.Hours)` | creada |
| `spk_antigint` (Entero) | igual | creada |
| `spk_antigmin` (Decimal 2) | `DateDiff(spk_primercorreo, Now(), TimeUnit.Minutes) / 60` | creada |
| `spk_antigtziutc` (Decimal 0) | `DateDiff(spk_primercorreotzi, UTCNow(), TimeUnit.Hours)` | creada |
| (rechazada) | `UTCNow()` contra la columna UserLocal | 405 `This operation cannot be performed on values which are of different Date Time Behaviors.` |
| (rechazada) | `Now()` contra la columna TimeZoneIndependent | 405, el mismo mensaje |

- Valores releídos con filas de hace 5 h, 30 h y 10 min (lectura 1 a las 18:03:36 UTC y lectura 2 a las 18:04:46): `spk_antigmin` 5.02 -> 5.03 y 30.02 -> 30.03: **se evalúa al leer**, sin proceso periódico. `spk_antignow_plano` y `spk_antigint`: 5 y 30.
- **Trampa de `DateDiff`**: cuenta límites de reloj, no horas transcurridas. La fila de hace 10 minutos (17:52 -> 18:02) da **1** hora. Para "antigüedad en horas reales" usar minutos / 60 con precisión 2, o aceptar que "1" significa "cruzó una hora de reloj".
- Exportada con `ExportSolution`: la fórmula viaja en `Formulas/spk_conversacion-FormulaDefinitions.yaml` como `spk_antigmin: =DateDiff(...)`, o sea, es portable con la solución.
- Doc oficial, que coincide: "The `Now` function can be used with formula columns. `Now()` has user local behavior and `UTCNow()` has time zone independent behavior" y las reglas de DateDiff entre behaviors (https://learn.microsoft.com/power-apps/maker/data-platform/formula-columns#guidelines-and-limitations). La misma página: no se ordena en model-driven por una columna de fórmula que use `UTCNow()`, no se puede usar en un rollup, no se pueden asegurar columnas de fórmula, y no dispara plugins ni flujos.
- **Advertencia de soporte**: https://learn.microsoft.com/power-apps/developer/data-platform/specialized-columns dice "We don't support defining the formulas with code... The only supported way to change this value is through the Power Apps editor". La API la aceptó y la evalúa, pero es una vía no soportada oficialmente. Alternativa soportada: crear la columna en el editor de Power Apps (mismo resultado, queda en la solución).

Alternativa si algo de esto no se quisiera usar: no hace falta ninguna. Si se descartara la fórmula, lo que admite la plataforma es calcular la antigüedad en la vista/gráfico a partir de "sin respuesta desde" (la propia columna base) o en un flujo recurrente, con el costo de la columna desactualizada que D-27 quería evitar.

### Pregunta 5. Operadores de fecha relativa sobre DateOnly (D-11)

Tablas `spk_arreglo` (padre) y `spk_cuota` (hija, lookup `spk_ArregloId`, relación `spk_arreglo_spk_cuota` creada y releída). Columna `spk_fechavencimiento` DateOnly. Fecha de hoy: 2026-10-08. Zona horaria del principal: `timezonecode 92` = UTC (releído). Filas sembradas y releídas: `hace10` 09-28, `ayer` 10-07, `ayer-pagada` 10-07, `hoy` 10-08, `mañana` 10-09. Script `s51_q5_fechas.py`.

| Operador | HTTP | Devuelve | Sirve para "fecha < hoy" |
|---|---|---|---|
| `olderthan-x-days 0` | 400 `The condition value should be greater than zero.` | n/a | No |
| `olderthan-x-days 1` | 200 | hace10 | **No**: omite "ayer" |
| `olderthan-x-days 2` | 200 | hace10 | No |
| `lt '2026-10-08'` (literal) | 200 | hace10, ayer, ayer-pagada | Correcto, pero la fecha es fija (no sirve en una vista) |
| `last-x-days 1` | 200 | ayer, ayer-pagada, **hoy** | **No**: incluye hoy |
| `last-x-days 11` | 200 | hace10, ayer, ayer-pagada, hoy | No |
| `last-seven-days` | 200 | ayer, ayer-pagada, hoy | No |
| `yesterday` | 200 | ayer, ayer-pagada | Solo ayer |
| `today` / `tomorrow` | 200 | hoy / mañana | Correcto |
| **`olderthan-x-days 1` OR `yesterday`** (`<filter type="or">`) | 200 | hace10, ayer, ayer-pagada | **Sí**: es exactamente fecha < hoy |

Con "sin pago" (`spk_fechapago` null) la combinación devuelve `ayer` y `hace10` (y no `ayer-pagada`), correcto.

Filtro relacionado (el de D-11, `s52_q5_vista.py`): arreglos con alguna cuota vencida sin pago, `link-entity link-type="any"` con la combinación anterior -> `Q5-PA` (que tiene dos cuotas vencidas, y aparece una sola vez). Vista guardada `SPK Arreglos en riesgo` (en la solución) con ese `fetchxml`, publicada, releída (contiene `yesterday` y `link-type='any'`) y ejecutada con `GET spk_arreglos?savedQuery=<id>` -> 200, `['Q5-PA']`.

Doc oficial: `olderthan-x-hours` y `olderthan-x-minutes` no se pueden aplicar a columnas DateOnly (https://learn.microsoft.com/power-apps/developer/data-platform/fetchxml/reference/operators); la doc de `olderthan-x-days` no dice nada sobre DateOnly, y el comportamiento observado es el de arriba.

*Riesgo no probado.* "Hoy" lo resuelve la plataforma en la zona horaria del usuario que ejecuta. El principal del spike está en UTC; no se pudo cambiar la zona para probar otra, porque eso es modificar la configuración de un principal que usan otros proyectos. Si el usuario o el flujo que ejecuta la consulta tiene una zona horaria distinta de la de la oficina, el límite entre "ayer" y "hoy" se desplaza esas horas y, cerca de la medianoche, una cuota puede figurar vencida unas horas antes o después. Conviene confirmar con un usuario real de la zona horaria de la oficina (y fijar la zona del usuario de servicio si se activa algún flujo) antes de producción.

### Pregunta 6. `msdyn_richtextfiles` (D-34), solo lectura

Scripts `s11_q6.py`, `s12_q6_privilegios.py`. No se creó ninguna imagen ni fila.

- Existe: `EntityDefinitions(LogicalName='msdyn_richtextfile')` -> 200. `EntitySetName = msdyn_richtextfiles`, `IsManaged = true`, `ObjectTypeCode 10362`, `PrimaryNameAttribute msdyn_name`. Auditoría desactivada. 0 filas en el entorno (agregado `$apply=aggregate($count)` = 0).
- **Ownership: `UserOwned`** (usuario o team; no organización). Columna `ownerid` de tipo Owner; relaciones `user_msdyn_richtextfile`, `team_msdyn_richtextfile`, `business_unit_msdyn_richtextfile`.
- Columnas (42): `msdyn_name`, `msdyn_parententityname`, `msdyn_parentid`, `msdyn_parententity_fieldname` (las tres de texto, 100), `msdyn_imageblob` (imagen, 10 MB), `msdyn_fileblob` (archivo, 128 MB), más las de sistema.
- **Vínculo con el padre: no hay relación ni lookup** hacia `email` ni hacia ninguna otra tabla de negocio. Las relaciones 1:N existentes son solo las genéricas de sistema (`syncerror`, `asyncoperation`, `fileattachment`, etc.), todas por `regardingobjectid`/`objectid` de esas tablas hacia la fila. El único vínculo posible es **por texto**: `msdyn_parententityname` (por ejemplo `email`), `msdyn_parentid` (el GUID como texto) y `msdyn_parententity_fieldname` (la columna del editor). La doc de la tabla las lista como columnas escribibles (https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/msdyn_richtextfile).
- **NO PROBADO**: que el editor del timeline llene esas tres columnas con el Email correcto (requiere subir una imagen por la UI) y con qué formato de GUID. Sin filas de muestra no se puede afirmar. Hay que hacer esa prueba con una imagen real antes de construir la purga de D-34.
- Privilegios (releídos): `Basic User` tiene Read y Create en profundidad **Basic** (solo filas propias). Delete lo tienen solo Service Deleter, System Administrator y System Customizer, todos a nivel organización. Implicaciones (inferencia, no probada): (1) como cada imagen es del usuario que la subió, un segundo oficial verá la imagen de su colega solo si el rol de oficial da Read sobre `msdyn_richtextfile` a nivel de unidad de negocio u organización, no Basic; (2) el rol "ARP Purga" necesita `prvDeletemsdyn_richtextfile` a nivel organización o BU para que el BulkDelete alcance imágenes ajenas.
- Doc: el editor guarda las imágenes en `msdyn_richtextfiles` cuando el usuario tiene permisos y en Base64 dentro de la columna cuando no (https://learn.microsoft.com/power-apps/maker/model-driven-apps/rich-text-editor-control#best-practices-for-using-the-rich-text-editor).

### Pregunta 7. Adjuntos (RNF-11), solo lectura

`GET organizations?$select=maxuploadfilesize,blockedattachments` (`s10_q6_q7.py`):

- `maxuploadfilesize = 5242880` bytes = **5 MiB**, igual al valor por defecto de la doc (máximo posible 131.072.000 bytes = 125 MiB aprox.; la doc dice "128 MB"). El límite se mide sobre el archivo en Base64.
- `blockedattachments`: `ade;adp;app;asa;ashx;asmx;asp;bas;bat;cdx;cer;chm;class;cmd;com;config;cpl;crt;csh;dll;exe;fxp;hlp;hta;htr;htw;ida;idc;idq;inf;ins;isp;its;jar;js;jse;ksh;lnk;mad;maf;mag;mam;maq;mar;mas;mat;mau;mav;maw;mda;mdb;mde;mdt;mdw;mdz;msc;msh;msh1;msh1xml;msh2;msh2xml;mshxml;msi;msp;mst;ops;pcd;pif;prf;prg;printer;pst;reg;rem;scf;scr;sct;shb;shs;shtm;shtml;soap;stm;tmp;url;vb;vbe;vbs;vsmacros;vss;vst;vsw;ws;wsc;wsf;wsh` (lista por defecto; no bloquea `xlsx`, `pdf`, `docx`, `png`, `jpg`).
- No se modificó nada. Cuando se exceda: error `0x80044a02 Attachment file size is too big` (tamaño) o `0x80043e09 AttachmentBlocked` (extensión) (https://learn.microsoft.com/power-apps/developer/data-platform/attachment-annotation-files#file-size-limits).

## Cambios de diseño que se desprenden (para decidir; `definicion.md` no se tocó)

1. **D-33**: se cierra a favor de la clave alterna (producto, número, arreglo). Documentar que los borradores no quedan protegidos entre sí por la clave.
2. **D-07**: se cierra; la cédula no puede ser clave alterna (probado), unicidad por plugin.
3. **D-12 y D-19**: siguen abiertos por 2c. Es el único supuesto del spike que no se pudo contestar. Antes de construir RF-13 hay que correr `s65` en los dos escenarios (A y B) con un usuario no administrador. Hasta entonces el plan B de D-19 (cédula sin seguridad por columna) sigue siendo el camino seguro.
4. **D-04**: se cierra (412 comprobado en la tabla de prueba).
5. **D-27**: se cierra. Condiciones: la columna base "sin respuesta desde" debe ser fecha-hora con comportamiento UserLocal si se usa `Now()` (o TimeZoneIndependent si se usa `UTCNow()`, que además deshabilita ordenar por esa columna); la antigüedad en horas reales se calcula con minutos / 60; crear la fórmula en el editor de Power Apps o aceptar que la API es una vía no soportada.
6. **D-11**: el respaldo con flujo diario **no se activa**: la vista usa `olderthan-x-days 1` OR `yesterday`, dentro del filtro relacionado. Anotar el riesgo de zona horaria.
7. **D-34 y riesgo "imágenes fuera del alcance de la purga"**: la relación con el correo es solo por texto y no está probado que el editor la llene; `msdyn_richtextfile` es UserOwned. Ajustar el rol de oficial (Read a nivel BU/organización) y el de purga (Delete), y hacer la prueba con una imagen real.
8. **RNF-11**: 5 MiB y lista por defecto, sin cambios.

## Hallazgos adicionales

- Actualizar el `fetchxml` de la vista Quick Find con solo `{fetchxml}` devuelve `400 An unexpected error occurred.`, incluso sin cambios. Con `{fetchxml, layoutxml, name, returnedtypecode}` devuelve 204. En vistas normales no hace falta. Y la relectura inmediata mostró el valor viejo hasta publicar la tabla.
- El `@odata.bind` de un lookup es sensible a mayúsculas: `spk_ArregloId@odata.bind` funciona, `spk_arregloid@odata.bind` da `400` genérico.
- `RetrieveMetadataChanges` no filtra por `SourceType` (`Unable to evaluate query`); se filtra del lado del cliente.
- El `ExportSolution` verificó el contenido: 4 tablas (tipo 1, `behavior 0`), rol (20), perfil (70), 2 claves, 3 columnas con `IsSecured`, la vista y el archivo de fórmulas.

## Inventario de lo creado (releído con `s98_inventario.py`)

Publisher `spikesarreglospago`. Solución `SpikesArreglosPago` (unmanaged, 1.0.0.0). Componentes raíz: tablas `spk_arreglo` (12 filas), `spk_cuota` (5), `spk_cliente` (2), `spk_conversacion` (3), rol `SPK Lector Cliente` (sin usuarios), perfil `SPK Perfil Cedula Lectura` (sin usuarios ni equipos). Dentro de las tablas: clave `spk_clave_arreglo` (Active), clave `spk_clave_numerocliente`, relación `spk_arreglo_spk_cuota`, columnas de fórmula, la vista `SPK Arreglos en riesgo` y la vista Quick Find de `spk_cliente` con find columns modificadas. Columnas `spk_cedula`, `spk_telefono`, `spk_correo` con seguridad de columna. El spike quedó para inspección: no se borró nada.

## Cómo borrarlo

Orden, y por qué: la tabla hija primero (con ella se van sus relaciones y lookups); cada tabla se lleva sus columnas, claves, vistas y filas; borrar la solución no administrada solo borra el contenedor, no sus componentes; el publisher al final.

1. Script, que solo borra componentes que están en la solución y por nombre exacto: `python3 spikes/plataforma/s99_limpieza.py --si-borrar` (sin la bandera hace un ensayo en seco). Hace, en este orden: `DELETE EntityDefinitions(LogicalName='spk_cuota')`, luego `spk_arreglo`, `spk_cliente`, `spk_conversacion` (unos 40 s cada una, con relectura hasta 404); `DELETE fieldsecurityprofiles(<id>)` del perfil `SPK Perfil Cedula Lectura`; `DELETE roles(<id>)` del rol `SPK Lector Cliente`; `DELETE solutions(<id>)`; `DELETE publishers(<id>)`.
2. Alternativa manual: en make.powerapps.com, Soluciones -> `SpikesArreglosPago` -> eliminar cada tabla desde la solución con "Eliminar del entorno"; luego perfil de columna y rol desde el centro de administración; al final la solución, y el publisher desde Soluciones -> Publicadores.
3. Solo la solución (deja los componentes en la solución `Default`): `pac solution delete --solution-name SpikesArreglosPago` (con el perfil `SANIC-ESTUDIO-DEV` activo; `pac` puede colgarse en sesión sin escritorio, ver `herramientas/patrones.md` §3).
4. No quedan cambios fuera de estos componentes: ni configuración de la organización, ni usuarios, ni roles ajenos.

## Archivos (`spikes/plataforma/`, sin secretos)

| Archivo | Para qué |
|---|---|
| `dv.py`, `spk_lib.py` | Cliente del Web API (con la guarda de entorno) y constructores de metadatos |
| `s00_solucion.py`, `s01_tablas.py`, `s02_pertenencia.py` | Publisher, solución, tablas y verificación de pertenencia |
| `s03_clave.py`, `s30_q1_clave_nulos.py` | Pregunta 1 |
| `s60`, `s61`, `s62`, `s63`, `s64`, `s66` (`s6x_q2*.py`) | Pregunta 2 y preparación de rol y perfil |
| `s65_q2c_sin_admin.py` | Pregunta 2c: **no ejecutado**, listo para un usuario no administrador |
| `s40_q3_etag.py` | Pregunta 3 |
| `s20` a `s25` | Pregunta 4 |
| `s50_relacion.py`, `s51_q5_fechas.py`, `s52_q5_vista.py` | Pregunta 5 |
| `s10_q6_q7.py`, `s11_q6.py`, `s12_q6_privilegios.py` | Preguntas 6 y 7 (solo lectura) |
| `s70_exportar.py`, `s98_inventario.py` | Verificación independiente por exportación e inventario |
| `s99_limpieza.py` | Limpieza (no se ejecuta sola) |

## Limpieza ejecutada (2026-10-09)

Se corrieron, en orden, las limpiezas de `sincronizacion/`, `manual/`, `sandbox/` y `plataforma/` con `--si-borrar`, y se borró a mano la queue `Spike` (primero su único queue item; el mailbox se fue con la queue), que dejó libre el buzón 1 para la queue real del área. `plataforma/s99_limpieza.py` no pudo borrar `spk_cuota` ni `spk_arreglo` en la primera pasada: la vista `SPK Arreglos en riesgo` (sobre `spk_arreglo`, con vínculo a `spk_cuota`) y la relación `spk_arreglo_spk_cuota` las referenciaban (`RetrieveDependenciesForDelete`, tipos 26 y 10). Se borró la vista y después las dos tablas, hija primero. Releído: sin tablas `spk_*`, sin roles `SPK`, sin la solución `SpikesArreglosPago` ni su publisher. El usuario de aplicación de correo quedó deshabilitado y sin roles. Queda en el entorno el correo automático "Your mailbox is now connected to Dynamics 365", que el spike 3 conservó a propósito.
