# Playbook: tabla · sanic_carp_tbl_conversacion

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.2",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "prefijo_opciones": 15946,
  "idiomas": [
    1033,
    3082
  ]
}
```

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.2. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Las etiquetas son las mismas, en español y sin tildes (BP-PP-197), en los dos idiomas. La descripción va en prosa con tildes: no es un nombre visible.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_conversacion",
  "displayname": {
    "1033": "Conversacion",
    "3082": "Conversacion"
  },
  "displayname_plural": {
    "1033": "Conversaciones",
    "3082": "Conversaciones"
  },
  "descripcion": "Hilo de correos de ida y vuelta entre el área y un cliente. Agrupa los correos entrantes y las respuestas mediante la columna Regarding del Email; se finaliza a mano con un resultado.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": true,
  "auditoria": true,
  "motivos_estado": [
    {
      "statecode": 0,
      "valor": 1,
      "origen": "defecto",
      "etiqueta": {
        "1033": "Abierta",
        "3082": "Abierta"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 2,
      "origen": "defecto",
      "etiqueta": {
        "1033": "Finalizada",
        "3082": "Finalizada"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 159460001,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Unida",
        "3082": "Unida"
      },
      "descripcion": ""
    }
  ],
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Numero",
      "3082": "Numero"
    },
    "descripcion": "Número correlativo de la conversación, que el área cita al hablar de ella. Lo asigna el sistema al crear.",
    "largo": 20,
    "requerida": false,
    "autonumerico": "CONV-{SEQNUM:6}"
  },
  "columnas": [
    {
      "nombre": "sanic_huellamessageid",
      "displayname": {
        "1033": "Huella del Message-ID",
        "3082": "Huella del Message-ID"
      },
      "descripcion": "Huella SHA-256 del Message-ID normalizado del primer correo. Es la clave alterna que garantiza que un correo reprocesado no cree dos conversaciones; la huella evita que un Message-ID largo supere el límite de tamaño de la clave.",
      "tipo": "texto",
      "largo": 64,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechaprimercorreo",
      "displayname": {
        "1033": "Fecha del primer correo",
        "3082": "Fecha del primer correo"
      },
      "descripcion": "Fecha y hora de llegada del primer correo, tomada de actualend del Email (coincide con la recepción en Exchange) y no de createdon, para no sumar la demora de la sincronización. Inicio del tiempo total.",
      "tipo": "fechahora",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_asunto",
      "displayname": {
        "1033": "Asunto",
        "3082": "Asunto"
      },
      "descripcion": "Asunto del primer correo de la conversación, copiado por el sistema al crearla. Permite que la vista de conversaciones muestre de qué se trata sin abrir el timeline.",
      "tipo": "texto",
      "largo": 400,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_remitente",
      "displayname": {
        "1033": "Remitente",
        "3082": "Remitente"
      },
      "descripcion": "Dirección del remitente del primer correo, copiada por el sistema al crear la conversación. Permite ver de quién es cuando el cliente está vacío (remitente desconocido), antes de que el oficial lo vincule o lo cree.",
      "tipo": "texto",
      "largo": 320,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_sinrespuestadesde",
      "displayname": {
        "1033": "Sin respuesta desde",
        "3082": "Sin respuesta desde"
      },
      "descripcion": "Fecha del primer correo del cliente que todavía no tiene respuesta; vacía si no falta responder nada. Las vistas de pendientes y la antigüedad salen de ella. Debe ser de comportamiento Usuario local: la fórmula de antigüedad usa Now().",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_correonuevoenfinalizada",
      "displayname": {
        "1033": "Correo nuevo en finalizada",
        "3082": "Correo nuevo en finalizada"
      },
      "descripcion": "Lo marca el sistema cuando llega un correo a una conversación finalizada; el correo cuenta como sin respuesta hasta que el oficial decide reabrir o iniciar una conversación nueva.",
      "tipo": "sino",
      "etiqueta_si": {
        "1033": "Si",
        "3082": "Si"
      },
      "etiqueta_no": {
        "1033": "No",
        "3082": "No"
      },
      "defecto": false,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_resultado",
      "displayname": {
        "1033": "Resultado",
        "3082": "Resultado"
      },
      "descripcion": "Resultado con el que se finalizó la conversación. Se limpia al reabrir.",
      "tipo": "choice",
      "choice": "sanic_carp_ch_resultadoconversacion",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_motivosinrespuesta",
      "displayname": {
        "1033": "Motivo de sin respuesta",
        "3082": "Motivo de sin respuesta"
      },
      "descripcion": "Motivo de la finalización cuando el resultado es Sin respuesta.",
      "tipo": "choice",
      "choice": "sanic_carp_ch_motivosinrespuesta",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_detallesinrespuesta",
      "displayname": {
        "1033": "Detalle de sin respuesta",
        "3082": "Detalle de sin respuesta"
      },
      "descripcion": "Explicación del motivo de la finalización cuando el resultado es Sin respuesta.",
      "tipo": "memo",
      "largo": 1000,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_fechafinalizacion",
      "displayname": {
        "1033": "Fecha de finalizacion",
        "3082": "Fecha de finalizacion"
      },
      "descripcion": "Fecha y hora en que la conversación dejó de estar Abierta, por finalización o por unión. Alimenta la retención de 2 años.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_tiempoprimerarespuesta",
      "displayname": {
        "1033": "Tiempo de primera respuesta (minutos)",
        "3082": "Tiempo de primera respuesta (minutos)"
      },
      "descripcion": "Minutos desde el primer correo del cliente que esperaba respuesta hasta la primera respuesta enviada. Persistido.",
      "tipo": "entero",
      "minimo": 0,
      "maximo": 2147483647,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_tiempototal",
      "displayname": {
        "1033": "Tiempo total (minutos)",
        "3082": "Tiempo total (minutos)"
      },
      "descripcion": "Minutos desde el primer correo hasta la finalización. Persistido; se recalcula en la próxima finalización si la conversación se reabre.",
      "tipo": "entero",
      "minimo": 0,
      "maximo": 2147483647,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_cantidadrespuestas",
      "displayname": {
        "1033": "Cantidad de respuestas",
        "3082": "Cantidad de respuestas"
      },
      "descripcion": "Respuestas enviadas desde la solución en esta conversación.",
      "tipo": "entero",
      "minimo": 0,
      "maximo": 2147483647,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_antiguedad",
      "displayname": {
        "1033": "Antiguedad (horas)",
        "3082": "Antiguedad (horas)"
      },
      "descripcion": "Horas desde que el último correo del cliente espera respuesta, calculada al leer.",
      "tipo": "formula",
      "expresion": "DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes) / 60",
      "resultado": "decimal",
      "precision": 2,
      "depende_de": [
        "sanic_sinrespuestadesde"
      ],
      "requerida": false,
      "protegida": false,
      "auditoria": false
    }
  ]
}
```

### Conteo de columnas

El diccionario (§4.2) lista 15 columnas propias que no son lookup, la primaria incluida, y 4 lookups. Este playbook declara **15**: la primaria más 14 en `columnas[]` (de ellas, 13 las crea la herramienta y 1 es una fórmula de paso manual). Los lookups no están: nacen con su relación (ver «Fuera de alcance»).

### Viene del diseño (no lo decide este playbook)

- Nombre lógico de la tabla y de cada columna, tipo, largo, rango, requerida, auditoría y choice de cada columna: `02-diccionario-datos.md` §4.2. Ninguna columna de Conversación lleva seguridad por columna.
- Auditoría de la tabla = `true` y auditoría de cada columna según la columna «Aud» (D-20, P-15); la primaria hereda `true` (§4.2, líneas 312 y 319).
- Propiedad `usuario` (DD-01), `notas` `false` y `actividades` `true` (necesaria para que la Conversación sea el Regarding del Email; irreversible; §4.2, líneas 310 y 311).
- Primaria: `sanic_nombre`, autonumérica `CONV-{SEQNUM:6}`, largo 20, requerida `false` (P-02, §11.2 línea 680; §4.2 línea 314).
- Los 3 motivos de estado, sus valores y etiquetas (§3.2, líneas 261 a 263; P-13): 1 Abierta y 2 Finalizada renombran los motivos por defecto; 159460001 Unida se agrega (prefijo de opciones 15946 × 10.000 + 1).
- La fórmula `sanic_antiguedad`: expresión, resultado `decimal` y precisión 2 (§4.2 línea 333, P-20); creada a mano en el editor de Power Apps (línea 335).
- Etiquetas «Si» / «No» y valor por defecto «No» de `sanic_correonuevoenfinalizada` (§4.2, línea 325).
- Nombre visible de la tabla, su plural y el de cada columna (§4.2 y P-17), con el mismo texto en 1033 y 3082 (`01-convenciones.md` §0 y §4).
- Descripciones de tabla y de columna: columna «Descripción» del diccionario (ver la decisión sobre las citas internas, abajo).

### Decisiones que este playbook toma

| # | Qué | Decisión | Motivo |
|---|---|---|---|
| 1 | Orden de `columnas[]` | El de las filas del diccionario §4.2, sin la primaria; la fórmula va al final. | Una fórmula se declara después de sus bases (`tabla.py` lo valida). |
| 2 | `depende_de` de `sanic_antiguedad` | `["sanic_sinrespuestadesde"]`: la única columna que lee la expresión. | El diccionario trae la expresión, no la lista de bases; la lista sale de leerla. |
| 3 | `descripcion` de los 3 motivos de estado | Cadena vacía. | El diccionario da etiqueta y «cómo se llega», no una descripción del motivo; el formato la declara opcional. |
| 4 | Orden de `motivos_estado` | Los dos por defecto, luego el agregado (el de la tabla del §3.2). | El de la tabla del diccionario. |
| 5 | Citas internas en las descripciones | Se quitaron de las descripciones las citas de decisión (D-nn, P-nn, R-nn, RF-nn) y las menciones de spikes; el resto del texto es el del diccionario. | La descripción la ve el usuario de la app; un código de decisión no le dice nada. Afecta a `sanic_fechaprimercorreo` y `sanic_sinrespuestadesde` (dice «Usuario local» donde el diccionario decía «`UL`»). |

Los nombres visibles de la tabla, de su plural y de las columnas, y los rangos y etiquetas de los enteros y del sí/no, **no** son decisiones de este playbook: el diccionario los trae (ver arriba).

### Lo que el diccionario decide y este formato no puede expresar

No se esquiva ni se cambia el diseño: se anota para quien lo cierre (skill, herramienta o plugin).

| Dato del diccionario | Dónde | Qué pasa con él |
|---|---|---|
| Concurrencia optimista activa (`IsOptimisticConcurrencyEnabled = true`): «el playbook la comprueba» | §4.2, línea 313 | Ni el formato ni `tabla.py` la leen ni la comparan. Se declara como comprobación manual de solo lectura en la sección 5 (punto 10); no la hace la herramienta. |
| `sanic_cantidadrespuestas`: «defecto 0» | §4.2, línea 332; §11.3, línea 704 | La columna `entero` no tiene clave de valor por defecto. Se declara el rango y la requerida; el 0 inicial lo escribe quien da de alta el registro (plugin de entrada de correo). |
| Auditoría de `statecode` y `statuscode`: «ambos auditados» | §4.2, línea 346 | El formato no tiene clave para auditar las columnas de estado y la herramienta no las toca. Con la auditoría de la tabla activa, la plataforma decide su valor por defecto; no se verifica aquí. |

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, la lista incluye 1033 y el idioma base del entorno está en ella | `[1033, 3082]` |
| 3 | Cada choice global que usa una columna existe en el entorno | `sanic_carp_ch_resultadoconversacion` (inventario 2.1); `sanic_carp_ch_motivosinrespuesta` (inventario 2.2) |
| 4 | Nombres lógicos con el patrón del proyecto, en minúscula y de 95 caracteres o menos; nombres visibles sin tildes ni ñ (BP-PP-197) | tabla `sanic_carp_tbl_conversacion`; columnas `sanic_<nombre>`; los de la sección 2 |
| 5 | La tabla no existe todavía (si existe, se aplica la sección 6) | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')` devuelve 404 |
| 6 | Las columnas de las que depende la fórmula están declaradas antes que ella y son del mismo registro | `sanic_sinrespuestadesde` (`fechahora`, Usuario local) antes de `sanic_antiguedad` |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 "Tabla con sus columnas", con el contrato de `references/herramientas/patrones.md`.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_conversacion.md
```

La herramienta crea la tabla, la primaria y las columnas propias en un solo POST; la primaria es autonumérica, así que la plataforma respeta su largo al crear, y escribe los motivos de estado (`InsertStatusValue` para los agregados, `UpdateOptionValue` para renombrar los dos por defecto).

**Características todavía no verificadas.** Este playbook usa columna de `formula`, `motivos_estado`: `tabla.py` las marca «todavía no verificadas contra la plataforma» y responde `bloqueado` hasta que se ensayen sobre una tabla descartable y pasen a verificadas. No es un defecto del playbook, y nunca se resuelve con `--permitir-no-verificadas` sobre esta tabla (la herramienta lo rechaza: no es una tabla `…_tbl_zz…`). Lo resuelve el arquitecto.

Si la plataforma rechaza `UpdateOptionValue` sobre `statuscode` (renombrar los dos motivos por defecto), `tabla.py` responde `bloqueado` y da el paso manual de renombre (en Power Apps, tabla > Columnas > Motivo para el estado). No se improvisa otra vía.

### Paso manual: columna de fórmula

La plataforma no soporta definir una columna de fórmula por código, así que la crea **una persona**, **después** de que exista la tabla con sus columnas base (con una base ausente la fórmula queda inválida):

1. En Power Apps: Soluciones → `sanic_carp_sol_controlarreglospago` → tabla Conversacion (`sanic_carp_tbl_conversacion`) → Columnas → Nueva columna.
2. Tipo de datos: Fórmula.
3. Nombre lógico exactamente `sanic_antiguedad` (con el prefijo del publisher `sanic`); nombre para mostrar «Antiguedad (horas)» (el mismo texto en 1033 y 3082) y la descripción de la sección 2.
4. Expresión: `DateDiff(sanic_sinrespuestadesde, Now(), TimeUnit.Minutes) / 60`
5. Resultado decimal con precisión 2.
6. Guardar y publicar.
7. Volver a correr `tabla.py`: la herramienta solo la verifica por lectura de metadata (punto 8 de la sección 5).

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_conversacion.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_conversacion.md --guardar
```

Los valores esperados salen del bloque de la sección 2; la primaria cuenta como una columna más y hereda la auditoría de la tabla.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = true` · `IsAuditEnabled = true` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción de la tabla | nombre «Conversacion» y plural «Conversaciones», cada uno con una etiqueta en 1033 y otra en 3082 y ninguna en otro idioma; descripción como en la sección 2 |
| 3 | Cada columna, con la tabla de abajo: tipo, requerida, protegida, auditoría, nombre visible (una etiqueta en 1033 y otra en 3082, iguales), descripción y lo propio del tipo | las 15 filas de la tabla de abajo |
| 4 | Ninguna columna propia de más, salvo lookups | solo las 15 de la tabla de abajo; lookups que nacerán con sus relaciones: `sanic_clienteid`, `sanic_oficialasignadoid`, `sanic_finalizadoporid`, `sanic_unidaaid` |
| 5 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 6 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 7 | Motivos de estado: `statecode` tiene exactamente los estados 0 y 1; `statuscode` tiene exactamente estos motivos, cada uno con una etiqueta en 1033 y otra en 3082 | 1 = «Abierta» (estado 0, defecto); 2 = «Finalizada» (estado 1, defecto); 159460001 = «Unida» (estado 1, agregado); ninguno más |
| 8 | Fórmula `sanic_antiguedad`, por lectura de metadata (sin escribir nada) | existe · `SourceType = 3` · `SourceTypeMask` sin el bit 32 y con el bit 1 · `DecimalType` con `Precision = 2` · `IsValidForCreate = false` · `IsValidForUpdate = false` · `IsSecured = false` · `IsAuditEnabled = false` · requerida `None` · nombre visible y descripción como la sección 2 · existe `sanic_sinrespuestadesde`, con `DateTimeBehavior = UserLocal` (la expresión usa `Now()`). Si la columna no existe, la herramienta la informa como paso manual pendiente |
| 9 | Muestra real: `muestra_tabla.py --guardar` (exporta la solución y compara contra el XML) | XML exportado en `playbooks/tabla/muestras/`, con las columnas, su tipo, requerida y etiquetas por idioma. La fórmula, los motivos de estado figuran como «sin comparar» hasta que una muestra real fije la comparación (lo dice la herramienta); mientras tanto el playbook no sale `OK` en ese punto. |
| 10 | Concurrencia optimista (la pide el diseño y `tabla.py` no la lee): `GET EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')?$select=IsOptimisticConcurrencyEnabled` | `true`. Comprobación manual de solo lectura; si fuera `false`, no se corrige aquí: se reporta al arquitecto |

Columnas (15, la primaria incluida):

| # | Nombre lógico | Nombre visible | Tipo en la plataforma y lo propio | Requerida | Protegida | Auditada |
|---|---|---|---|---|---|---|
| 0 | `sanic_nombre` | Numero | StringType · MaxLength 20 · FormatName Text · AutoNumberFormat `CONV-{SEQNUM:6}` · `IsPrimaryName = true` | No | No | Sí |
| 1 | `sanic_huellamessageid` | Huella del Message-ID | StringType · MaxLength 64 · FormatName Text | Sí | No | No |
| 2 | `sanic_fechaprimercorreo` | Fecha del primer correo | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | Sí | No | No |
| 3 | `sanic_asunto` | Asunto | StringType · MaxLength 400 · FormatName Text | No | No | No |
| 4 | `sanic_remitente` | Remitente | StringType · MaxLength 320 · FormatName Text | No | No | No |
| 5 | `sanic_sinrespuestadesde` | Sin respuesta desde | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | No |
| 6 | `sanic_correonuevoenfinalizada` | Correo nuevo en finalizada | BooleanType · DefaultValue false · TrueOption «Si» · FalseOption «No» | Sí | No | No |
| 7 | `sanic_resultado` | Resultado | PicklistType · GlobalOptionSet `sanic_carp_ch_resultadoconversacion` | No | No | Sí |
| 8 | `sanic_motivosinrespuesta` | Motivo de sin respuesta | PicklistType · GlobalOptionSet `sanic_carp_ch_motivosinrespuesta` | No | No | Sí |
| 9 | `sanic_detallesinrespuesta` | Detalle de sin respuesta | MemoType · MaxLength 1000 | No | No | Sí |
| 10 | `sanic_fechafinalizacion` | Fecha de finalizacion | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |
| 11 | `sanic_tiempoprimerarespuesta` | Tiempo de primera respuesta (minutos) | IntegerType · MinValue 0 · MaxValue 2147483647 | No | No | No |
| 12 | `sanic_tiempototal` | Tiempo total (minutos) | IntegerType · MinValue 0 · MaxValue 2147483647 | No | No | No |
| 13 | `sanic_cantidadrespuestas` | Cantidad de respuestas | IntegerType · MinValue 0 · MaxValue 2147483647 | Sí | No | No |
| 14 | `sanic_antiguedad` | Antiguedad (horas) | DecimalType · Precision 2 · SourceType 3 (fórmula; paso manual) | No | No | No |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada** (ni columnas, ni nombres visibles, ni la primaria).

Mientras falte la columna `sanic_antiguedad` el playbook **no está terminado**: la herramienta no la crea, informa el paso manual de la sección 4 y no cierra en éxito.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')`, posible solo mientras la tabla esté vacía y nada dependa de ella (relaciones, claves, vistas, formularios). **Irreversible desde que se crea**: el nombre lógico `sanic_carp_tbl_conversacion` y el de cada columna de la sección 2; el tipo de propiedad (`usuario`); el tipo de cada columna; tener actividades activadas (no se puede desactivar); los valores de los motivos de estado agregados una vez que haya datos con ellos (159460001): nunca se renumeran ni se reutilizan (BP-PP-193); el conjunto de estados `statecode` (0 y 1), fijo en la plataforma. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría y ampliar un largo (achicarlo, no); también se pueden activar notas y actividades (desactivarlas, no). Borrar una columna borra sus datos.

## 8. Fuera de alcance

- Lookups de la tabla (no son columnas de este playbook; nacen con su relación, diccionario §6.2): `sanic_clienteid` (REL-01), `sanic_oficialasignadoid` (REL-10), `sanic_finalizadoporid` (REL-11) y `sanic_unidaaid` (REL-09, autorreferida).
- La relación nativa Regarding Email → Conversación (NAT-01): la plataforma la crea sola al activar `actividades`; su nombre se confirma al construir. No se crea ni se verifica aquí.
- Con qué motivo nace un registro: este playbook no fija el motivo con el que se crea una conversación ni el motivo por defecto de cada estado. El diseño dice que nace Abierta (diccionario §3.2, línea 261); el alta (plugin de entrada de correo) manda `statuscode = 1` de forma explícita, porque qué motivo toma un registro creado sin mandarlo está pendiente de verificar. Las transiciones entre motivos las impone un plugin (D-30), no la plataforma.
- Clave alterna K-01 (`sanic_huellamessageid`), diccionario §7.
- Plugins: entrada de correo y fijación de las columnas solo-sistema (`sanic_huellamessageid`, `sanic_fechaprimercorreo`, `sanic_asunto`, `sanic_remitente`, `sanic_sinrespuestadesde`, `sanic_correonuevoenfinalizada`, `sanic_fechafinalizacion`, tiempos y cantidad de respuestas), validaciones del resultado y del motivo, Custom API de unión y propietario team (D-31).
- Vistas, formularios, quick find, seguridad de registro y roles; las columnas del Email (`sanic_tiemporespuesta`, `sanic_respondidoporid`; diccionario §5), que van en un playbook `columnas-estandar`.
