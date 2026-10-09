# Playbook: tabla · sanic_carp_tbl_arreglo

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.3",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.3. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Las etiquetas son las mismas, en español y sin tildes (BP-PP-197), en los dos idiomas. La descripción va en prosa con tildes: no es un nombre visible.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_arreglo",
  "displayname": {
    "1033": "Arreglo",
    "3082": "Arreglo"
  },
  "displayname_plural": {
    "1033": "Arreglos",
    "3082": "Arreglos"
  },
  "descripcion": "Arreglo de pago de un préstamo o una tarjeta, con los datos del formato F-NIC-0000052. Nace en borrador, se puede descartar y pasa a vigente al formalizarlo.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "motivos_estado": [
    {
      "statecode": 0,
      "valor": 1,
      "origen": "defecto",
      "etiqueta": {
        "1033": "Borrador",
        "3082": "Borrador"
      },
      "descripcion": ""
    },
    {
      "statecode": 0,
      "valor": 159460001,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Vigente",
        "3082": "Vigente"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 2,
      "origen": "defecto",
      "etiqueta": {
        "1033": "Terminado",
        "3082": "Terminado"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 159460002,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Anulado",
        "3082": "Anulado"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 159460003,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Descartado",
        "3082": "Descartado"
      },
      "descripcion": ""
    }
  ],
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Codigo interno",
      "3082": "Codigo interno"
    },
    "descripcion": "Código interno correlativo del arreglo, asignado por el sistema al crear, también en borrador. No es el número de arreglo del crédito.",
    "largo": 20,
    "requerida": false,
    "autonumerico": "ARR-{SEQNUM:6}"
  },
  "columnas": [
    {
      "nombre": "sanic_producto",
      "displayname": {
        "1033": "Producto crediticio",
        "3082": "Producto crediticio"
      },
      "descripcion": "Producto sobre el que se hace el arreglo: préstamo o tarjeta. No se cambia una vez que el borrador se congela.",
      "tipo": "choice",
      "choice": "sanic_carp_ch_productocrediticio",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_numeroproducto",
      "displayname": {
        "1033": "Numero de prestamo o tarjeta",
        "3082": "Numero de prestamo o tarjeta"
      },
      "descripcion": "Número del préstamo o de la tarjeta, en claro. Forma parte de la clave alterna del número de arreglo y se busca con quick find. Su tratamiento queda pendiente de Cumplimiento.",
      "tipo": "texto",
      "largo": 30,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_numeroarreglo",
      "displayname": {
        "1033": "Numero de arreglo",
        "3082": "Numero de arreglo"
      },
      "descripcion": "Consecutivo por crédito (cuántas veces ese préstamo o tarjeta tuvo arreglo). Vacío en borrador; lo fija el sistema al formalizar. Parte de la clave alterna.",
      "tipo": "entero",
      "minimo": 1,
      "maximo": 9999,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_numeroprovisional",
      "displayname": {
        "1033": "Numero provisional impreso",
        "3082": "Numero provisional impreso"
      },
      "descripcion": "Número que salió impreso en el formato para firma (max+1 de los arreglos formalizados del crédito). El sistema lo compara al formalizar. Se vacía al desbloquear o reabrir.",
      "tipo": "entero",
      "minimo": 1,
      "maximo": 9999,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_tipo",
      "displayname": {
        "1033": "Tipo de arreglo",
        "3082": "Tipo de arreglo"
      },
      "descripcion": "Tipo de arreglo: temporal o cancelación.",
      "tipo": "choice",
      "choice": "sanic_carp_ch_tipoarreglo",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_moneda",
      "displayname": {
        "1033": "Moneda",
        "3082": "Moneda"
      },
      "descripcion": "Moneda del arreglo: córdobas o dólares. Todos los montos del arreglo y de sus cuotas se interpretan en esta moneda; las cuotas la heredan y no llevan columna propia. Como dato del acuerdo, la bloquea el congelamiento.",
      "tipo": "choice",
      "choice": "sanic_carp_ch_moneda",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_canalorigen",
      "displayname": {
        "1033": "Canal de origen",
        "3082": "Canal de origen"
      },
      "descripcion": "Canal por el que llegó el pedido. Si el arreglo se registra desde una conversación y viene vacío, el plugin pre-create completa \"Correo\". Dato del acuerdo: lo bloquea el congelamiento.",
      "tipo": "choice",
      "choice": "sanic_carp_ch_canalorigen",
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_saldoinicial",
      "displayname": {
        "1033": "Saldo inicial",
        "3082": "Saldo inicial"
      },
      "descripcion": "Saldo inicial del crédito al momento del arreglo, en la moneda del arreglo.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_montoabonado",
      "displayname": {
        "1033": "Monto abonado",
        "3082": "Monto abonado"
      },
      "descripcion": "Monto abonado a la fecha, en la moneda del arreglo.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_saldoremanente",
      "displayname": {
        "1033": "Saldo remanente",
        "3082": "Saldo remanente"
      },
      "descripcion": "Saldo remanente por pagar = saldo inicial − monto abonado, en la moneda del arreglo. Lo calcula el plugin al crear el arreglo y al modificar el borrador, y queda congelado con el resto del acuerdo; nadie lo digita. No es una columna calculada ni de fórmula de Dataverse: el plugin escribe el valor.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_plazo",
      "displayname": {
        "1033": "Plazo en cuotas",
        "3082": "Plazo en cuotas"
      },
      "descripcion": "Cantidad de cuotas del arreglo, hasta 60 y sin superar la capacidad de la versión de plantilla.",
      "tipo": "entero",
      "minimo": 1,
      "maximo": 60,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_cuotamensual",
      "displayname": {
        "1033": "Cuota mensual",
        "3082": "Cuota mensual"
      },
      "descripcion": "Monto de la cuota mensual, en la moneda del arreglo.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_totalpagar",
      "displayname": {
        "1033": "Total a pagar",
        "3082": "Total a pagar"
      },
      "descripcion": "Total a pagar del arreglo = el menor entre cuota mensual × plazo y el saldo remanente, en la moneda del arreglo (si cuota × plazo supera el saldo remanente, la última cuota se achica y el total es el saldo remanente). Lo calcula el plugin al crear el arreglo y al modificar el borrador, y queda congelado con el resto del acuerdo; nadie lo digita. No es una columna calculada ni de fórmula de Dataverse: el plugin escribe el valor.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechainicio",
      "displayname": {
        "1033": "Fecha de inicio",
        "3082": "Fecha de inicio"
      },
      "descripcion": "Fecha de la primera cuota. Solo fecha porque representa un día de calendario, no un instante.",
      "tipo": "fecha",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechafin",
      "displayname": {
        "1033": "Fecha de finalizacion",
        "3082": "Fecha de finalizacion"
      },
      "descripcion": "Fecha de la última cuota. Solo fecha por la misma razón.",
      "tipo": "fecha",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_cuentadebitar",
      "displayname": {
        "1033": "Cuenta a debitar",
        "3082": "Cuenta a debitar"
      },
      "descripcion": "Cuenta desde la que se debitan las cuotas. Dato sensible protegido con seguridad por columna y sin auditoría.",
      "tipo": "texto",
      "largo": 20,
      "requerida": true,
      "protegida": true,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechaformalizacion",
      "displayname": {
        "1033": "Fecha de formalizacion",
        "3082": "Fecha de formalizacion"
      },
      "descripcion": "Fecha de formalización que se imprime en el formato. Es un valor almacenado, no calculado (la plantilla ya no trae =TODAY()): no cambia al reabrir el documento. No es la fecha del gesto Formalizar.",
      "tipo": "fecha",
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_comentarios",
      "displayname": {
        "1033": "Comentarios",
        "3082": "Comentarios"
      },
      "descripcion": "Comentarios del oficial, que se imprimen en el formato. El largo que cabe en la celda de la plantilla está abierto.",
      "tipo": "memo",
      "largo": 500,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechacongelamiento",
      "displayname": {
        "1033": "Fecha de congelamiento",
        "3082": "Fecha de congelamiento"
      },
      "descripcion": "Fecha de la generación del formato para firma. Desde ahí los datos del acuerdo, las cuotas y la versión de plantilla quedan bloqueados. Se vacía al desbloquear o reabrir.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_instantaneanombre",
      "displayname": {
        "1033": "Cliente al congelar: nombre",
        "3082": "Cliente al congelar: nombre"
      },
      "descripcion": "Nombre del cliente copiado al congelar, tal como se imprimió. Los cambios posteriores en el Cliente no alteran el documento. Se vacía al reabrir.",
      "tipo": "texto",
      "largo": 200,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_instantaneacedula",
      "displayname": {
        "1033": "Cliente al congelar: cedula",
        "3082": "Cliente al congelar: cedula"
      },
      "descripcion": "Cédula copiada al congelar. Mismo tratamiento que la del Cliente: seguridad por columna, sin auditoría, sin quick find ni vistas. Se purga con el arreglo.",
      "tipo": "texto",
      "largo": 20,
      "requerida": false,
      "protegida": true,
      "auditoria": false
    },
    {
      "nombre": "sanic_instantaneacorreo",
      "displayname": {
        "1033": "Cliente al congelar: correo",
        "3082": "Cliente al congelar: correo"
      },
      "descripcion": "Correo del cliente copiado al congelar, tal como se imprimió.",
      "tipo": "texto",
      "largo": 320,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_instantaneacelular",
      "displayname": {
        "1033": "Cliente al congelar: celular",
        "3082": "Cliente al congelar: celular"
      },
      "descripcion": "Celular del cliente copiado al congelar, tal como se imprimió.",
      "tipo": "texto",
      "largo": 20,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_fechaformalizado",
      "displayname": {
        "1033": "Formalizado el",
        "3082": "Formalizado el"
      },
      "descripcion": "Fecha y hora en que el oficial usó el gesto Formalizar (registro del gesto, distinto de la fecha impresa en el formato).",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_motivodescarte",
      "displayname": {
        "1033": "Motivo de descarte",
        "3082": "Motivo de descarte"
      },
      "descripcion": "Por qué un borrador no se concretó.",
      "tipo": "memo",
      "largo": 500,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_fechadescarte",
      "displayname": {
        "1033": "Fecha de descarte",
        "3082": "Fecha de descarte"
      },
      "descripcion": "Fecha y hora del gesto Descartar.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_motivoanulacion",
      "displayname": {
        "1033": "Motivo de anulacion",
        "3082": "Motivo de anulacion"
      },
      "descripcion": "Por qué se anuló un arreglo vigente.",
      "tipo": "memo",
      "largo": 500,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_fechaanulacion",
      "displayname": {
        "1033": "Fecha de anulacion",
        "3082": "Fecha de anulacion"
      },
      "descripcion": "Fecha y hora del gesto Anular.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_motivoreapertura",
      "displayname": {
        "1033": "Motivo de reapertura",
        "3082": "Motivo de reapertura"
      },
      "descripcion": "Por qué la jefatura reabrió un arreglo congelado o terminado (dato congelado a corregir, pago erróneo).",
      "tipo": "memo",
      "largo": 500,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_fechareapertura",
      "displayname": {
        "1033": "Fecha de reapertura",
        "3082": "Fecha de reapertura"
      },
      "descripcion": "Fecha y hora de la última reapertura.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_cantidadreaperturas",
      "displayname": {
        "1033": "Cantidad de reaperturas",
        "3082": "Cantidad de reaperturas"
      },
      "descripcion": "Contador de reaperturas del arreglo.",
      "tipo": "entero",
      "minimo": 0,
      "maximo": 999,
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_fechatermino",
      "displayname": {
        "1033": "Fecha de termino",
        "3082": "Fecha de termino"
      },
      "descripcion": "Fecha y hora en que el arreglo terminó, se anuló o se descartó. Alimenta la retención de 2 años (BulkDelete). Un borrador sin resolver no la tiene.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    }
  ]
}
```

### Conteo de columnas

El diccionario (§4.3) lista 33 columnas propias que no son lookup, la primaria incluida, y 8 lookups. Este playbook declara **33**: la primaria más 32 en `columnas[]`. Los lookups no están: nacen con su relación (ver «Fuera de alcance»).

### Viene del diseño (no lo decide este playbook)

- Nombre lógico de la tabla y de cada columna, tipo, largo, rango, decimales, requerida, protegida, auditoría y choice de cada columna: `02-diccionario-datos.md` §4.3.
- Auditoría de la tabla = `true` y auditoría de cada columna según la columna «Aud» (D-20, P-15); la primaria hereda `true`. No se auditan el número de producto, la cuenta a debitar ni la cédula copiada (§4.3, línea 356).
- Columnas protegidas: `sanic_cuentadebitar` y `sanic_instantaneacedula` (columna «Seg»; §8). Ninguna otra.
- Propiedad `usuario` (DD-01), `notas` y `actividades` en `false` (§4.3, línea 355).
- Primaria: `sanic_nombre`, autonumérica `ARR-{SEQNUM:6}`, largo 20, requerida `false` (§4.3 línea 357, §11.2 línea 681).
- Los 5 motivos de estado, sus valores y etiquetas (§3.2, líneas 269 a 273; P-13): 1 Borrador y 2 Terminado renombran los motivos por defecto; 159460001 Vigente, 159460002 Anulado y 159460003 Descartado se agregan.
- Montos: `decimal` con 2 decimales, rango 0 a 999999999.99, sin el tipo Moneda de Dataverse (P-04, DD-11). La moneda del arreglo es la columna `sanic_moneda`.
- Rangos enteros: los de la columna «Tipo y forma» (P-03; plazo 1..60 fijado por la definición, §11.3 línea 707). Fechas: `fecha` (solo fecha, irreversible) para inicio, fin y formalización impresa; `fechahora` (Usuario local) para el resto (DD-04).
- Nombre visible de la tabla, su plural y el de cada columna (§4.3 y P-17), con el mismo texto en 1033 y 3082 (`01-convenciones.md` §0 y §4).
- Descripciones de tabla y de columna: columna «Descripción» del diccionario (ver la decisión sobre las citas internas, abajo).

### Decisiones que este playbook toma

| # | Qué | Decisión | Motivo |
|---|---|---|---|
| 1 | Orden de `columnas[]` | El de las filas del diccionario §4.3, sin la primaria. | El formato pide «el orden en que se quieren ver». |
| 2 | `descripcion` de los 5 motivos de estado | Cadena vacía. | El diccionario da etiqueta y «cómo se llega», no una descripción del motivo; el formato la declara opcional. |
| 3 | Orden de `motivos_estado` | El de la tabla del diccionario §3.2: Borrador, Vigente, Terminado, Anulado, Descartado. | Los valores agregados salen en orden de correlativo (159460001, 002, 003). |
| 4 | Citas internas en las descripciones | Se quitaron de las descripciones las citas de decisión (D-nn, P-nn, R-nn, RF-nn) y las menciones del mockup; el resto del texto es el del diccionario. | La descripción la ve el usuario de la app. Afecta a `sanic_numeroproducto`, `sanic_moneda`, `sanic_canalorigen`, `sanic_saldoremanente`, `sanic_totalpagar`, `sanic_fechafin`, `sanic_fechaformalizacion` y `sanic_comentarios`. |

Los nombres visibles de la tabla, de su plural y de las columnas, y los rangos y etiquetas de los enteros y del sí/no, **no** son decisiones de este playbook: el diccionario los trae (ver arriba).

### Lo que el diccionario decide y este formato no puede expresar

No se esquiva ni se cambia el diseño: se anota para quien lo cierre (skill, herramienta o plugin).

| Dato del diccionario | Dónde | Qué pasa con él |
|---|---|---|
| `sanic_cantidadreaperturas`: «defecto 0» | §4.3, línea 395; §11.3, línea 705 | La columna `entero` no tiene clave de valor por defecto. Se declara el rango [0..999] y la requerida; el 0 inicial lo escribe el plugin de alta. |
| Auditoría de `statecode` y `statuscode`: «ambos auditados» | §4.3, línea 411 | El formato no tiene clave para auditar las columnas de estado y la herramienta no las toca. No se verifica aquí. |

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, la lista incluye 1033 y el idioma base del entorno está en ella | `[1033, 3082]` |
| 3 | Cada choice global que usa una columna existe en el entorno | `sanic_carp_ch_productocrediticio` (inventario 2.3); `sanic_carp_ch_tipoarreglo` (inventario 2.4); `sanic_carp_ch_moneda` (inventario 2.5); `sanic_carp_ch_canalorigen` (inventario 2.6) |
| 4 | Nombres lógicos con el patrón del proyecto, en minúscula y de 95 caracteres o menos; nombres visibles sin tildes ni ñ (BP-PP-197) | tabla `sanic_carp_tbl_arreglo`; columnas `sanic_<nombre>`; los de la sección 2 |
| 5 | La tabla no existe todavía (si existe, se aplica la sección 6) | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')` devuelve 404 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 "Tabla con sus columnas", con el contrato de `references/herramientas/patrones.md`.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_arreglo.md
```

La herramienta crea la tabla, la primaria y las columnas propias en un solo POST; agrega después, una por una, las columnas protegidas (`sanic_cuentadebitar`, `sanic_instantaneacedula`); la primaria es autonumérica, así que la plataforma respeta su largo al crear, y escribe los motivos de estado (`InsertStatusValue` para los agregados, `UpdateOptionValue` para renombrar los dos por defecto).

**Características todavía no verificadas.** Este playbook usa columnas `decimal`, `motivos_estado`: `tabla.py` las marca «todavía no verificadas contra la plataforma» y responde `bloqueado` hasta que se ensayen sobre una tabla descartable y pasen a verificadas. No es un defecto del playbook, y nunca se resuelve con `--permitir-no-verificadas` sobre esta tabla (la herramienta lo rechaza: no es una tabla `…_tbl_zz…`). Lo resuelve el arquitecto.

Si la plataforma rechaza `UpdateOptionValue` sobre `statuscode` (renombrar los dos motivos por defecto), `tabla.py` responde `bloqueado` y da el paso manual de renombre (en Power Apps, tabla > Columnas > Motivo para el estado). No se improvisa otra vía.

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_arreglo.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_arreglo.md --guardar
```

Los valores esperados salen del bloque de la sección 2; la primaria cuenta como una columna más y hereda la auditoría de la tabla.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción de la tabla | nombre «Arreglo» y plural «Arreglos», cada uno con una etiqueta en 1033 y otra en 3082 y ninguna en otro idioma; descripción como en la sección 2 |
| 3 | Cada columna, con la tabla de abajo: tipo, requerida, protegida, auditoría, nombre visible (una etiqueta en 1033 y otra en 3082, iguales), descripción y lo propio del tipo | las 33 filas de la tabla de abajo |
| 4 | Ninguna columna propia de más, salvo lookups | solo las 33 de la tabla de abajo; lookups que nacerán con sus relaciones: `sanic_clienteid`, `sanic_conversacionid`, `sanic_versionplantillaid`, `sanic_oficialid`, `sanic_formalizadoporid`, `sanic_descartadoporid`, `sanic_anuladoporid`, `sanic_reabiertoporid` |
| 5 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 6 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 7 | Motivos de estado: `statecode` tiene exactamente los estados 0 y 1; `statuscode` tiene exactamente estos motivos, cada uno con una etiqueta en 1033 y otra en 3082 | 1 = «Borrador» (estado 0, defecto); 159460001 = «Vigente» (estado 0, agregado); 2 = «Terminado» (estado 1, defecto); 159460002 = «Anulado» (estado 1, agregado); 159460003 = «Descartado» (estado 1, agregado); ninguno más |
| 8 | Muestra real: `muestra_tabla.py --guardar` (exporta la solución y compara contra el XML) | XML exportado en `playbooks/tabla/muestras/`, con las columnas, su tipo, requerida y etiquetas por idioma. Las columnas `decimal`, los motivos de estado figuran como «sin comparar» hasta que una muestra real fije la comparación (lo dice la herramienta); mientras tanto el playbook no sale `OK` en ese punto. |

Columnas (33, la primaria incluida):

| # | Nombre lógico | Nombre visible | Tipo en la plataforma y lo propio | Requerida | Protegida | Auditada |
|---|---|---|---|---|---|---|
| 0 | `sanic_nombre` | Codigo interno | StringType · MaxLength 20 · FormatName Text · AutoNumberFormat `ARR-{SEQNUM:6}` · `IsPrimaryName = true` | No | No | Sí |
| 1 | `sanic_producto` | Producto crediticio | PicklistType · GlobalOptionSet `sanic_carp_ch_productocrediticio` | Sí | No | No |
| 2 | `sanic_numeroproducto` | Numero de prestamo o tarjeta | StringType · MaxLength 30 · FormatName Text | Sí | No | No |
| 3 | `sanic_numeroarreglo` | Numero de arreglo | IntegerType · MinValue 1 · MaxValue 9999 | No | No | Sí |
| 4 | `sanic_numeroprovisional` | Numero provisional impreso | IntegerType · MinValue 1 · MaxValue 9999 | No | No | No |
| 5 | `sanic_tipo` | Tipo de arreglo | PicklistType · GlobalOptionSet `sanic_carp_ch_tipoarreglo` | Sí | No | No |
| 6 | `sanic_moneda` | Moneda | PicklistType · GlobalOptionSet `sanic_carp_ch_moneda` | Sí | No | No |
| 7 | `sanic_canalorigen` | Canal de origen | PicklistType · GlobalOptionSet `sanic_carp_ch_canalorigen` | Sí | No | Sí |
| 8 | `sanic_saldoinicial` | Saldo inicial | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | Sí | No | No |
| 9 | `sanic_montoabonado` | Monto abonado | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | Sí | No | No |
| 10 | `sanic_saldoremanente` | Saldo remanente | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | Sí | No | No |
| 11 | `sanic_plazo` | Plazo en cuotas | IntegerType · MinValue 1 · MaxValue 60 | Sí | No | No |
| 12 | `sanic_cuotamensual` | Cuota mensual | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | Sí | No | No |
| 13 | `sanic_totalpagar` | Total a pagar | DecimalType · Precision 2 · MinValue 0 · MaxValue 999999999.99 | Sí | No | No |
| 14 | `sanic_fechainicio` | Fecha de inicio | DateTimeType · Format DateOnly · DateTimeBehavior DateOnly | Sí | No | No |
| 15 | `sanic_fechafin` | Fecha de finalizacion | DateTimeType · Format DateOnly · DateTimeBehavior DateOnly | Sí | No | No |
| 16 | `sanic_cuentadebitar` | Cuenta a debitar | StringType · MaxLength 20 · FormatName Text | Sí | Sí | No |
| 17 | `sanic_fechaformalizacion` | Fecha de formalizacion | DateTimeType · Format DateOnly · DateTimeBehavior DateOnly | Sí | No | No |
| 18 | `sanic_comentarios` | Comentarios | MemoType · MaxLength 500 | No | No | No |
| 19 | `sanic_fechacongelamiento` | Fecha de congelamiento | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |
| 20 | `sanic_instantaneanombre` | Cliente al congelar: nombre | StringType · MaxLength 200 · FormatName Text | No | No | No |
| 21 | `sanic_instantaneacedula` | Cliente al congelar: cedula | StringType · MaxLength 20 · FormatName Text | No | Sí | No |
| 22 | `sanic_instantaneacorreo` | Cliente al congelar: correo | StringType · MaxLength 320 · FormatName Text | No | No | No |
| 23 | `sanic_instantaneacelular` | Cliente al congelar: celular | StringType · MaxLength 20 · FormatName Text | No | No | No |
| 24 | `sanic_fechaformalizado` | Formalizado el | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |
| 25 | `sanic_motivodescarte` | Motivo de descarte | MemoType · MaxLength 500 | No | No | Sí |
| 26 | `sanic_fechadescarte` | Fecha de descarte | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |
| 27 | `sanic_motivoanulacion` | Motivo de anulacion | MemoType · MaxLength 500 | No | No | Sí |
| 28 | `sanic_fechaanulacion` | Fecha de anulacion | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |
| 29 | `sanic_motivoreapertura` | Motivo de reapertura | MemoType · MaxLength 500 | No | No | Sí |
| 30 | `sanic_fechareapertura` | Fecha de reapertura | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |
| 31 | `sanic_cantidadreaperturas` | Cantidad de reaperturas | IntegerType · MinValue 0 · MaxValue 999 | Sí | No | Sí |
| 32 | `sanic_fechatermino` | Fecha de termino | DateTimeType · Format DateAndTime · DateTimeBehavior UserLocal | No | No | Sí |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada** (ni columnas, ni nombres visibles, ni la primaria).

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')`, posible solo mientras la tabla esté vacía y nada dependa de ella (relaciones, claves, vistas, formularios). **Irreversible desde que se crea**: el nombre lógico `sanic_carp_tbl_arreglo` y el de cada columna de la sección 2; el tipo de propiedad (`usuario`); el tipo de cada columna; el comportamiento de las columnas `fecha` (solo fecha): `sanic_fechainicio`, `sanic_fechafin`, `sanic_fechaformalizacion`; los valores de los motivos de estado agregados una vez que haya datos con ellos (159460001, 159460002, 159460003): nunca se renumeran ni se reutilizan (BP-PP-193); el conjunto de estados `statecode` (0 y 1), fijo en la plataforma. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría y ampliar un largo (achicarlo, no); también se pueden activar notas y actividades (desactivarlas, no). Borrar una columna borra sus datos.

## 8. Fuera de alcance

- Lookups de la tabla (no son columnas de este playbook; nacen con su relación, diccionario §6.2): `sanic_clienteid` (REL-02), `sanic_conversacionid` (REL-03), `sanic_versionplantillaid` (REL-04), `sanic_oficialid` (REL-12), `sanic_formalizadoporid` (REL-13), `sanic_descartadoporid` (REL-14), `sanic_anuladoporid` (REL-15) y `sanic_reabiertoporid` (REL-16). Los lookups requeridos del diseño (cliente, versión de plantilla, oficial) los exige la relación, no esta tabla.
- Con qué motivo nace un registro: este playbook no fija el motivo con el que se crea un arreglo ni el motivo por defecto de cada estado. El diseño dice que nace en Borrador (diccionario §3.2, línea 269); el alta (plugin) manda `statuscode = 1` de forma explícita. Las transiciones (Vigente, Terminado, Anulado, Descartado, reapertura) las impone un plugin (D-30).
- Clave alterna K-02 (`sanic_producto`, `sanic_numeroproducto`, `sanic_numeroarreglo`), diccionario §7.
- Perfil de seguridad de columna `CSP - CARP - Datos sensibles`: da permisos sobre `sanic_cuentadebitar` y `sanic_instantaneacedula`, que este playbook deja con `protegida: true` (diccionario §8).
- Plugins: normalización del número de producto (P-06), cálculo de `sanic_saldoremanente` y `sanic_totalpagar` (R-04), generación del calendario, congelamiento e instantánea del cliente, consecutivo del número de arreglo, reglas de los gestos y propietario team (D-31).
- Vistas, formularios, quick find (número de producto; D-12), seguridad de registro y roles. Las reglas de negocio abiertas del diccionario (§11.4: R-03, R-06, R-07, R-09) siguen abiertas: este playbook no las decide.
