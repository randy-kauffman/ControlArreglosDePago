# Playbook: tabla · sanic_carp_tbl_cliente

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.1",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.1. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Las etiquetas son las mismas, en español y sin tildes (BP-PP-197), en los dos idiomas. La descripción va en prosa con tildes: no es un nombre visible.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_cliente",
  "displayname": {
    "1033": "Cliente",
    "3082": "Cliente"
  },
  "displayname_plural": {
    "1033": "Clientes",
    "3082": "Clientes"
  },
  "descripcion": "Persona que solicita o tiene un arreglo de pago. Tabla propia de la solución, sin usar la tabla estándar de contactos.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": false,
  "motivos_estado": null,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Nombre",
      "3082": "Nombre"
    },
    "descripcion": "Nombre con el que se muestra el cliente en listas y lookups. Lo calcula el sistema a partir del nombre completo; nadie lo digita.",
    "largo": 200,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_nombrecompleto",
      "displayname": {
        "1033": "Nombre completo",
        "3082": "Nombre completo"
      },
      "descripcion": "Nombre completo del cliente tal como lo digita el oficial.",
      "tipo": "texto",
      "largo": 200,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_cedula",
      "displayname": {
        "1033": "Cedula",
        "3082": "Cedula"
      },
      "descripcion": "Número de cédula del cliente. Dato sensible protegido con seguridad por columna; su unicidad la valida un plugin al crear, porque una columna protegida no puede ser parte de una clave alterna.",
      "tipo": "texto",
      "largo": 20,
      "requerida": true,
      "protegida": true,
      "auditoria": false
    },
    {
      "nombre": "sanic_correo",
      "displayname": {
        "1033": "Correo",
        "3082": "Correo"
      },
      "descripcion": "Correo electrónico del cliente. Si coincide con el remitente de un correo entrante, el plugin vincula la conversación al cliente.",
      "tipo": "texto",
      "largo": 320,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_celular",
      "displayname": {
        "1033": "Celular",
        "3082": "Celular"
      },
      "descripcion": "Número de celular del cliente.",
      "tipo": "texto",
      "largo": 20,
      "requerida": false,
      "protegida": false,
      "auditoria": false
    }
  ]
}
```

### Conteo de columnas

El diccionario (§4.1) lista 5 columnas propias que no son lookup, la primaria incluida, y ningún lookup. Este playbook declara **5**: la primaria más 4 en `columnas[]`. Los lookups no están: nacen con su relación (ver «Fuera de alcance»).

### Viene del diseño (no lo decide este playbook)

- Nombre lógico de la tabla y de cada columna, tipo, largo, requerida, columna protegida y auditoría de cada columna: `02-diccionario-datos.md` §4.1.
- Auditoría de la tabla = `false` (D-20: no copiar la cédula y el contacto a los logs); por eso la primaria hereda `false`. Fuente: §4.1, línea 289.
- Propiedad `usuario` (DD-01), `notas` y `actividades` en `false` (§4.1, línea 288).
- Primaria: `sanic_nombre`, largo 200, requerida `true`, sin formato autonumérico (calculada por plugin; §4.1 línea 290, §11.2 línea 679).
- Nombre visible de la tabla, su plural y el de cada columna (§4.1 y P-17), con el mismo texto en 1033 y 3082 (`01-convenciones.md` §0 y §4).
- Descripciones de tabla y de columna: columna «Descripción» del diccionario, sin cambios.
- `motivos_estado = null`: el diccionario declara motivos solo para Conversación y Arreglo (§3.2, línea 255) y cuenta 8 en total (línea 776); Cliente conserva los dos por defecto.

### Decisiones que este playbook toma

| # | Qué | Decisión | Motivo |
|---|---|---|---|
| 1 | Orden de `columnas[]` | El de las filas del diccionario §4.1, sin la primaria. | El formato pide «el orden en que se quieren ver». |

Los nombres visibles de la tabla, de su plural y de las columnas, y los rangos y etiquetas de los enteros y del sí/no, **no** son decisiones de este playbook: el diccionario los trae (ver arriba).

### Lo que el diccionario decide y este formato no puede expresar

No se esquiva ni se cambia el diseño: se anota para quien lo cierre (skill, herramienta o plugin).

| Dato del diccionario | Dónde | Qué pasa con él |
|---|---|---|
| `sanic_correo`: «texto T(320), formato Email». El formato del playbook no tiene clave `formato` en el texto y la herramienta crea toda columna de texto con `FormatName = Text` | §4.1, línea 298; §11.3, línea 693 | La columna nace con formato Texto. Cambiarlo a Email es un paso aparte (hueco de la skill o de la herramienta), no de este playbook. No se quitó ni se cambió el tipo para esquivarlo. |
| `sanic_celular`: «formato Texto» | §4.1, línea 299 | Es el formato que la herramienta crea: coincide, no hay nada que hacer. |

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, la lista incluye 1033 y el idioma base del entorno está en ella | `[1033, 3082]` |
| 3 | Choices globales que usa la tabla | ninguno: ninguna columna es `choice` |
| 4 | Nombres lógicos con el patrón del proyecto, en minúscula y de 95 caracteres o menos; nombres visibles sin tildes ni ñ (BP-PP-197) | tabla `sanic_carp_tbl_cliente`; columnas `sanic_<nombre>`; los de la sección 2 |
| 5 | La tabla no existe todavía (si existe, se aplica la sección 6) | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cliente')` devuelve 404 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 "Tabla con sus columnas", con el contrato de `references/herramientas/patrones.md`.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_cliente.md
```

La herramienta crea la tabla, la primaria y las columnas propias en un solo POST; agrega después, una por una, las columnas protegidas (`sanic_cedula`); reenvía la definición de la primaria (largo y requerida) y publica.

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_cliente.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_cliente.md --guardar
```

Los valores esperados salen del bloque de la sección 2; la primaria cuenta como una columna más y hereda la auditoría de la tabla.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cliente')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = false` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción de la tabla | nombre «Cliente» y plural «Clientes», cada uno con una etiqueta en 1033 y otra en 3082 y ninguna en otro idioma; descripción como en la sección 2 |
| 3 | Cada columna, con la tabla de abajo: tipo, requerida, protegida, auditoría, nombre visible (una etiqueta en 1033 y otra en 3082, iguales), descripción y lo propio del tipo | las 5 filas de la tabla de abajo |
| 4 | Ninguna columna propia de más, salvo lookups | solo las 5 de la tabla de abajo; lookups que nacerán con sus relaciones: ninguno |
| 5 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 6 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 7 | Motivos de estado (`motivos_estado = null`) | `statecode` con exactamente los estados 0 y 1; `statuscode` con exactamente el valor 1 (estado 0) y el 2 (estado 1) |
| 8 | Muestra real: `muestra_tabla.py --guardar` (exporta la solución y compara contra el XML) | XML exportado en `playbooks/tabla/muestras/`, con las columnas, su tipo, requerida y etiquetas por idioma. |

Columnas (5, la primaria incluida):

| # | Nombre lógico | Nombre visible | Tipo en la plataforma y lo propio | Requerida | Protegida | Auditada |
|---|---|---|---|---|---|---|
| 0 | `sanic_nombre` | Nombre | StringType · MaxLength 200 · FormatName Text · `IsPrimaryName = true` | Sí | No | No |
| 1 | `sanic_nombrecompleto` | Nombre completo | StringType · MaxLength 200 · FormatName Text | Sí | No | No |
| 2 | `sanic_cedula` | Cedula | StringType · MaxLength 20 · FormatName Text | Sí | Sí | No |
| 3 | `sanic_correo` | Correo | StringType · MaxLength 320 · FormatName Text | No | No | No |
| 4 | `sanic_celular` | Celular | StringType · MaxLength 20 · FormatName Text | No | No | No |

Todas las columnas figuran como no auditadas porque la auditoría de la tabla es `false` y ninguna columna la activa (D-20).

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada** (ni columnas, ni nombres visibles, ni la primaria).

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_cliente')`, posible solo mientras la tabla esté vacía y nada dependa de ella (relaciones, claves, vistas, formularios). **Irreversible desde que se crea**: el nombre lógico `sanic_carp_tbl_cliente` y el de cada columna de la sección 2; el tipo de propiedad (`usuario`); el tipo de cada columna; el conjunto de estados `statecode` (0 y 1), fijo en la plataforma. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría y ampliar un largo (achicarlo, no); también se pueden activar notas y actividades (desactivarlas, no). Borrar una columna borra sus datos.

## 8. Fuera de alcance

- Claves alternativas: ninguna en Cliente (la unicidad de la cédula la valida un plugin, no una clave; D-07, diccionario §7 línea 572).
- Perfil de seguridad de columna `CSP - CARP - Datos sensibles`: da permisos sobre `sanic_cedula`, que este playbook deja con `protegida: true` (diccionario §8).
- Plugins: cálculo de la primaria `sanic_nombre` (copia del nombre completo normalizado), normalización de cédula y correo (P-06), validación de unicidad de la cédula (D-07) y propietario team (D-31).
- Vistas, formularios, quick find (cédula, nombre, correo y celular; D-12), seguridad de registro y roles.
- Relaciones: Cliente no tiene lookups propios. Es el lado padre de REL-01 (Conversación) y REL-02 (Arreglo), que nacen con sus playbooks de relación.
