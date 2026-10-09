# Playbook: tabla · sanic_carp_tbl_cuota

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.4",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.4. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Las etiquetas son las mismas, en español y sin tildes (BP-PP-197), en los dos idiomas. La descripción va en prosa con tildes: no es un nombre visible.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_cuota",
  "displayname": {
    "1033": "Cuota",
    "3082": "Cuota"
  },
  "displayname_plural": {
    "1033": "Cuotas",
    "3082": "Cuotas"
  },
  "descripcion": "Cuota del calendario de un arreglo, con los datos de su pago. Una fila por cuota, hasta 60 por arreglo.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "motivos_estado": null,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Codigo",
      "3082": "Codigo"
    },
    "descripcion": "Código de la cuota: el código interno del arreglo y el número de cuota. Lo calcula el plugin que crea el calendario.",
    "largo": 30,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_numero",
      "displayname": {
        "1033": "Numero de cuota",
        "3082": "Numero de cuota"
      },
      "descripcion": "Número de orden de la cuota dentro del arreglo.",
      "tipo": "entero",
      "minimo": 1,
      "maximo": 60,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechavencimiento",
      "displayname": {
        "1033": "Fecha de vencimiento",
        "3082": "Fecha de vencimiento"
      },
      "descripcion": "Día en que vence la cuota. Solo fecha: la lista de riesgo compara por día. La regla de cálculo de fechas de vencimiento está abierta.",
      "tipo": "fecha",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_monto",
      "displayname": {
        "1033": "Monto",
        "3082": "Monto"
      },
      "descripcion": "Monto de la cuota, en la moneda del arreglo. Todas las cuotas valen la cuota mensual, salvo la última, que se achica a lo que queda cuando cuota × plazo supera el saldo remanente: saldo remanente − cuota × (plazo − 1). Lo fija el plugin del calendario.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechapago",
      "displayname": {
        "1033": "Fecha de pago",
        "3082": "Fecha de pago"
      },
      "descripcion": "Día en que se registró el pago de la cuota. Solo fecha: el formato tiene un par fecha y monto por cuota.",
      "tipo": "fecha",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_montopagado",
      "displayname": {
        "1033": "Monto pagado",
        "3082": "Monto pagado"
      },
      "descripcion": "Monto pagado de la cuota, en la moneda del arreglo; puede ser parcial. Un pago parcial saca la cuota de la lista de riesgo.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    }
  ]
}
```

### Conteo de columnas

El diccionario (§4.4) lista 6 columnas propias que no son lookup, la primaria incluida, y 1 lookup. Este playbook declara **6**: la primaria más 5 en `columnas[]`. Los lookups no están: nacen con su relación (ver «Fuera de alcance»).

### Viene del diseño (no lo decide este playbook)

- Nombre lógico de la tabla y de cada columna, tipo, rango, decimales, requerida y auditoría de cada columna: `02-diccionario-datos.md` §4.4. Ninguna columna de Cuota lleva seguridad por columna.
- Auditoría de la tabla = `true` (D-20: «Cuota (pagos)») y por columna solo las de pago, `sanic_fechapago` y `sanic_montopagado`; la primaria hereda `true` (§4.4, línea 421).
- Propiedad `usuario` (DD-01), `notas` y `actividades` en `false` (§4.4, línea 420).
- Primaria: `sanic_nombre`, texto de 30, requerida `true`, sin formato autonumérico (calculada por el plugin del calendario; §4.4 línea 422, §11.2 línea 682).
- Montos: `decimal` con 2 decimales, rango 0 a 999999999.99 (P-04). Fechas de vencimiento y de pago: `fecha`, solo fecha, irreversible (DD-04).
- Nombre visible de la tabla, su plural y el de cada columna (§4.4 y P-17), con el mismo texto en 1033 y 3082 (`01-convenciones.md` §0 y §4).
- Descripciones de tabla y de columna: columna «Descripción» del diccionario (ver la decisión sobre las citas internas, abajo).
- `motivos_estado = null`: el diccionario declara motivos solo para Conversación y Arreglo (§3.2, línea 255) y cuenta 8 en total (línea 776); Cuota conserva los dos por defecto.

### Decisiones que este playbook toma

| # | Qué | Decisión | Motivo |
|---|---|---|---|
| 1 | Orden de `columnas[]` | El de las filas del diccionario §4.4, sin la primaria. | El formato pide «el orden en que se quieren ver». |
| 2 | Citas internas en las descripciones | Se quitaron de las descripciones las citas de decisión (D-nn, P-nn, R-nn, RF-nn) y las menciones de spikes; el resto del texto es el del diccionario. | La descripción la ve el usuario de la app. Afecta a `sanic_fechavencimiento` y `sanic_fechapago`. |

Los nombres visibles de la tabla, de su plural y de las columnas, y los rangos y etiquetas de los enteros y del sí/no, **no** son decisiones de este playbook: el diccionario los trae (ver arriba).

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, la lista incluye 1033 y el idioma base del entorno está en ella | `[1033, 3082]` |
| 3 | Choices globales que usa la tabla | ninguno: ninguna columna es `choice` |
| 4 | Nombres lógicos con el patrón del proyecto, en minúscula y de 95 caracteres o menos; nombres visibles sin tildes ni ñ (BP-PP-197) | tabla `sanic_carp_tbl_cuota`; columnas `sanic_<nombre>`; los de la sección 2 |
| 5 | La tabla no existe todavía (si existe, se aplica la sección 6) | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cuota')` devuelve 404 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 "Tabla con sus columnas", con el contrato de `references/herramientas/patrones.md`.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_cuota.md
```

La herramienta crea la tabla, la primaria y las columnas propias en un solo POST; reenvía la definición de la primaria (largo y requerida) y publica.

**Características todavía no verificadas.** Este playbook usa columnas `decimal`: `tabla.py` las marca «todavía no verificadas contra la plataforma» y responde `bloqueado` hasta que se ensayen sobre una tabla descartable y pasen a verificadas. No es un defecto del playbook, y nunca se resuelve con `--permitir-no-verificadas` sobre esta tabla (la herramienta lo rechaza: no es una tabla `…_tbl_zz…`). Lo resuelve el arquitecto.

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_cuota.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_cuota.md --guardar
```

Los valores esperados salen del bloque de la sección 2; la primaria cuenta como una columna más y hereda la auditoría de la tabla.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cuota')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción de la tabla | nombre «Cuota» y plural «Cuotas», cada uno con una etiqueta en 1033 y otra en 3082 y ninguna en otro idioma; descripción como en la sección 2 |
| 3 | Cada columna, con la tabla de abajo: tipo, requerida, protegida, auditoría, nombre visible (una etiqueta en 1033 y otra en 3082, iguales), descripción y lo propio del tipo | las 6 filas de la tabla de abajo |
| 4 | Ninguna columna propia de más, salvo lookups | solo las 6 de la tabla de abajo; lookups que nacerán con sus relaciones: `sanic_arregloid` |
| 5 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 6 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 7 | Motivos de estado (`motivos_estado = null`) | `statecode` con exactamente los estados 0 y 1; `statuscode` con exactamente el valor 1 (estado 0) y el 2 (estado 1) |
| 8 | Muestra real: `muestra_tabla.py --guardar` (exporta la solución y compara contra el XML) | XML exportado en `playbooks/tabla/muestras/`, con las columnas, su tipo, requerida y etiquetas por idioma. Las columnas `decimal` figuran como «sin comparar» hasta que una muestra real fije la comparación (lo dice la herramienta); mientras tanto el playbook no sale `OK` en ese punto. |

Columnas (6, la primaria incluida):

| # | Nombre lógico | Nombre visible | Tipo en la plataforma y lo propio | Requerida | Protegida | Auditada |
|---|---|---|---|---|---|---|
| 0 | `sanic_nombre` | Codigo | StringType · MaxLength 30 · FormatName Text · `IsPrimaryName = true` | Sí | No | Sí |
| 1 | `sanic_numero` | Numero de cuota | IntegerType · MinValue 1 · MaxValue 60 | Sí | No | No |
| 2 | `sanic_fechavencimiento` | Fecha de vencimiento | DateTimeType · Format DateOnly · DateTimeBehavior DateOnly | Sí | No | No |
| 3 | `sanic_monto` | Monto | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | Sí | No | No |
| 4 | `sanic_fechapago` | Fecha de pago | DateTimeType · Format DateOnly · DateTimeBehavior DateOnly | No | No | Sí |
| 5 | `sanic_montopagado` | Monto pagado | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | No | No | Sí |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada** (ni columnas, ni nombres visibles, ni la primaria).

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_cuota')`, posible solo mientras la tabla esté vacía y nada dependa de ella (relaciones, claves, vistas, formularios). **Irreversible desde que se crea**: el nombre lógico `sanic_carp_tbl_cuota` y el de cada columna de la sección 2; el tipo de propiedad (`usuario`); el tipo de cada columna; el comportamiento de las columnas `fecha` (solo fecha): `sanic_fechavencimiento`, `sanic_fechapago`; el conjunto de estados `statecode` (0 y 1), fijo en la plataforma. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría y ampliar un largo (achicarlo, no); también se pueden activar notas y actividades (desactivarlas, no). Borrar una columna borra sus datos.

## 8. Fuera de alcance

- Lookup de la tabla (no es columna de este playbook; nace con su relación, diccionario §6.2): `sanic_arregloid` (REL-05, parental, requerido). Es la única relación parental de Cuota.
- No hay clave alterna en Cuota (P-19, diccionario §7 línea 579).
- Plugins: el calendario crea las cuotas y calcula la primaria `sanic_nombre` (`<primaria del arreglo>-C<NN>`) y el monto de la última cuota; el plugin limita la escritura del oficial a fecha y monto de pago y propaga el propietario team (D-31, P-11).
- Moneda: la cuota no lleva columna de moneda; hereda la de su arreglo (`sanic_moneda`, D-36). `sanic_monto` y `sanic_montopagado` son `decimal`, no el tipo Moneda de Dataverse (P-04).
- Vistas (la lista de riesgo de D-11 filtra por `sanic_fechavencimiento` y `sanic_fechapago`), formularios, seguridad de registro y roles.
